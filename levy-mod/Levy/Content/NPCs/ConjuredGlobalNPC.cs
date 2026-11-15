using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common;
using Levy.Common.Mark;
using Levy.Common.Movement;
using Levy.Common.Officers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Venom;
using Levy.Common.Weapons;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// A CONJURED creature: a real vanilla (or modded) enemy type, spawned friendly by a summon officer and run on its OWN AI, pointed at the army's enemies instead of at a player.
	/// The Clothier raises tier-appropriate skeletons and the Zoologist calls strong bestiary creatures this way (see <see cref="ConjureRoster"/>).
	///
	/// How it fights for us (all of it server / single player; clients just draw what streams in):
	///  * FRIENDLY: <c>friendly</c> is set, so vanilla never lets it hurt a player and the army's own shots skip it. <see cref="CanHitPlayer"/> is also refused outright.
	///  * ITS AI TARGETS THE ENEMY: for the one AI call, a free-standing Player (<see cref="AimBody"/>) standing where the chosen enemy is (or at the officer's side when there is no enemy) takes
	///    Main.player[255], exactly the swap enemy targeting uses to point enemies at the army, run backwards. The creature's own AI then walks, jumps, flies and shoots at "the player" that is the enemy.
	///    Its TargetClosest is answered from that dummy (<see cref="TargetClosestDetour"/>), so no AI re-picks a real player.
	///  * DAMAGE: vanilla deals no NPC-vs-NPC contact, so a body touching an enemy strikes it as a soldier's blow does (credit to the commander, the army's shred mark, banners, venom). Its projectiles are
	///    flipped to the army's side as they spawn (<see cref="ConjuredProjectile"/>); an enemy's blow lands on it as on a soldier.
	///  * ENEMIES CHASE IT: it is a <see cref="ITargetBody"/> in <see cref="SummonedUnitSystem"/>, so the same targeting that sends enemies at soldiers sends them at it.
	///  * NO LOOT, NO FARMING: <see cref="PreKill"/> cancels vanilla's whole death (loot, coins, banner and kill counts, event progress); <c>SpawnedFromStatue</c> and zero value back it up.
	///  * OWNER-BOUND: it despawns the instant its officer avatar is gone; the owning kit also kills it on dismissal. Never saved. It never despawns for distance.
	///  * WHAT IT THROWS IS THE ARMY'S TOO: a projectile it fires is adopted (<see cref="ConjuredProjectile"/>), and so is an NPC its AI spawns (a caster's Burning Sphere, Chaos Ball, Water Sphere: <see cref="IsChild"/>),
	///    which would otherwise be a hostile NPC whose own AI homes on the nearest real player. A child runs on the same aim body, is spent by its first blow or after <see cref="LevyConstants.Summon.ConjureChildLifeTicks"/>.
	///    A creature with no enemy to shoot at throws nothing (a shot at its idle post would fly at the officer and the commander beside him).
	///  * IDLE DECAY: a squad that asks for it (the Clothier's) loses a creature that has dealt no damage for a while of a fight; every blow it lands, or an arrow of its that hits, starts the count again.
	/// </summary>
	public class ConjuredGlobalNPC : GlobalNPC, ITargetBody
	{
		public override bool InstancePerEntity => true;

		// ---- bound data (set server-side in Conjure, synced to clients) ----
		/// <summary>True for a creature an officer kit conjured. Everything below is meaningless otherwise.</summary>
		public bool Active;
		/// <summary>whoAmI of the officer avatar (a <see cref="SoldierNPC"/> in officer mode) that owns this creature. It dies with it.</summary>
		public int OwnerWho = -1;
		/// <summary>Player index credited for its kills (the commander), or -1.</summary>
		public int CommanderIndex = -1;
		/// <summary>The enemy it is set on this tick (an NPC index), or -1.</summary>
		public int Target = -1;
		/// <summary>The vanilla / modded type that was conjured (what the kit asked for).</summary>
		public int ConjuredType;
		/// <summary>Diagnostic for the world self-checks: blows landed by thrown orbs (children).</summary>
		public static int ChildBlows, Raised, Crumbled;
		/// <summary>Diagnostic: a running number given to each creature conjured (identity that survives slot reuse).</summary>
		public int Serial;
		/// <summary>Diagnostics: ticks without damage in a fight, and whether it sees a fight.</summary>
		public int IdleTicksNow => _idleTicks;
		public bool FightNow => _fight;
		/// <summary>True for an NPC a conjured creature's own AI spawned (a caster's orb): it is the army's too, a short-lived shot, not a squad member.</summary>
		public bool IsChild;
		/// <summary>For a child: the whoAmI of the creature that threw it (the blows it lands count as that creature's).</summary>
		public int ParentWho = -1;
		/// <summary>Ticks a fight may go by without this creature dealing damage before it crumbles (0 = never).</summary>
		public int IdleLifeTicks;
		/// <summary>Floor of its contact blow before the world-progress multiply (0 = none). Server only.</summary>
		public int MinDamage;

		private NPC _npc;
		private int _scan, _farTicks, _strikeCd, _immuneTime, _age, _idleTicks, _fightScan;
		private bool _deathHandled, _aiming, _idle, _fight;
		private int _side;
		private AimBody _aim;
		private Player _dummy;

		// =====================================================================================================
		// Spawning
		// =====================================================================================================
		/// <summary>
		/// Conjures one creature of <paramref name="type"/> beside <paramref name="avatar"/> for <paramref name="commanderIndex"/>. Returns its whoAmI, or -1 when no slot / safe spot / type is available.
		/// Server / single player only.
		/// </summary>
		/// <summary>Why the last <see cref="Conjure"/> returned -1 (diagnostics for the self-checks).</summary>
		public static string LastFail = "";

		public static int Conjure(NPC avatar, int commanderIndex, int type, int minLife = 0, int minDamage = 0, int idleLifeTicks = 0)
		{
			LastFail = "";
			if (!LevyUtil.IsServerOrSingle || avatar == null || !avatar.active || type <= 0 || type >= NPCLoader.NPCCount) { LastFail = $"preconditions (server={LevyUtil.IsServerOrSingle} avatar={avatar?.active} type={type})"; return -1; }
			Vector2 near = avatar.Bottom + new Vector2(Main.rand.Next(-28, 29), 0f);
			Vector2 pos = RosterSystem.SafeSpawnPosition(near);
			int idx = NPC.NewNPC(new EntitySource_WorldEvent("Levy:ConjuredCreature"), (int)pos.X, (int)pos.Y, type);
			if (idx < 0 || idx >= Main.maxNPCs) { LastFail = "NewNPC returned " + idx; return -1; }
			NPC n = Main.npc[idx];
			if (n.type != type || !n.TryGetGlobalNPC(out ConjuredGlobalNPC c)) { LastFail = $"spawned type {n.type} global={n.TryGetGlobalNPC(out ConjuredGlobalNPC _)}"; n.active = false; return -1; }
			c.Bind(n, avatar.whoAmI, commanderIndex, type);
			c.Serial = ++Raised;
			c.MinDamage = minDamage; c.IdleLifeTicks = idleLifeTicks;
			if (minLife > n.lifeMax) { n.lifeMax = minLife; n.life = minLife; } // never weaker than the squad it replaces
			n.Bottom = pos;
			n.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
			return idx;
		}

		/// <summary>Makes this NPC the army's: friendly, no loot, no despawn, no player damage. Idempotent; also what a client runs when the flag reaches it.</summary>
		private void Bind(NPC npc, int ownerWho, int commanderIndex, int type)
		{
			_npc = npc;
			Active = true; OwnerWho = ownerWho; CommanderIndex = commanderIndex; ConjuredType = type;
			Target = -1; _scan = 0; _farTicks = 0; _strikeCd = 0; _immuneTime = 0; _age = 0; _deathHandled = false; _idle = false; _idleTicks = 0; _fight = false; _fightScan = 0; IsChild = false; ParentWho = -1;
			Mark(npc);
		}

		/// <summary>The flags that make a hostile type the army's (also re-applied on a client when it learns the NPC is conjured).</summary>
		private static void Mark(NPC npc)
		{
			npc.friendly = true; npc.townNPC = false;
			npc.value = 0f; npc.SpawnedFromStatue = true; npc.dontCountMe = true;
			npc.netAlways = true; npc.boss = false; npc.rarity = 0;
			npc.timeLeft = NPC.activeTime;
		}

		/// <summary>True when this NPC is a live conjured creature of an officer.</summary>
		public static bool IsConjured(NPC npc) => npc != null && npc.active && npc.TryGetGlobalNPC(out ConjuredGlobalNPC c) && c.Active;

		/// <summary>True when a hostile the army would fight is within <see cref="LevyConstants.Summon.FightReachTiles"/> of <paramref name="officer"/> (a fight is on). Server / single player.</summary>
		public static bool EnemyNear(NPC officer)
		{
			if (officer == null || !officer.active) return false;
			float lim = LevyConstants.Summon.FightReachTiles * LevyConstants.TilePx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active || t.whoAmI == officer.whoAmI || Vector2.DistanceSquared(officer.Center, t.Center) > lim * lim) continue;
				if (LevyUtil.IsHostileTarget(t) && !SoldierTargeting.IsNeverTarget(t) && !IsConjured(t)) return true;
			}
			return false;
		}

		/// <summary>An NPC a conjured creature's own AI spawns (a Burning Sphere, a Chaos Ball) is the army's: friendly, owner-bound, aimed at the enemy by its own AI through the same aim body. With no enemy to throw it at, it never appears.</summary>
		public override void OnSpawn(NPC npc, IEntitySource source)
		{
			if (Active || !LevyUtil.IsServerOrSingle) return;
			if (source is not EntitySource_Parent { Entity: NPC parent } || parent.whoAmI == npc.whoAmI || !parent.TryGetGlobalNPC(out ConjuredGlobalNPC pc) || !pc.Active) return;
			if (pc.Target < 0 || pc.OwnerAvatar() == null) { npc.active = false; return; } // thrown at nothing: it would fly at the officer's side, where the commander stands
			if (!ConjureRoster.ChildAiOk(npc)) { npc.active = false; return; }              // an AI never checked against the aim body would home on the nearest real player
			Bind(npc, pc.OwnerWho, pc.CommanderIndex, npc.type);
			IsChild = true; ParentWho = parent.whoAmI; MinDamage = 0; IdleLifeTicks = 0; Target = pc.Target;
			npc.netUpdate = true;
		}

		/// <summary>
		/// Whether a projectile this creature's AI just fired may fly: only at an enemy it has picked. With no enemy (its aim body stands at its post beside the officer, next to the commander) or with the shot pointed at
		/// a real player and not at the enemy, it is cancelled.
		/// </summary>
		internal bool MayShoot(Projectile p)
		{
			if (Target < 0 || Target >= Main.maxNPCs || !Main.npc[Target].active) return false;
			if (p.aiStyle == ProjAIStylePlayerHoming) return false; // read from Projectile.cs: this AI steers at the nearest real player in 200 px whatever its hostile flag says
			if (p.velocity.Length() < 2f) return true;
			NPC enemy = Main.npc[Target];
			float aE = Angle(p.velocity, enemy.Center - p.Center);
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player pl = Main.player[i];
				if (!pl.active || pl.dead || Vector2.Distance(pl.Center, p.Center) > 90f * 16f) continue;
				float aP = Angle(p.velocity, pl.Center - p.Center);
				if (aP < 20f && aP + 5f < aE) return false;
			}
			return true;
		}

		/// <summary>Projectile AI style 51 (the hostile Lost Soul): it homes on Main.player[] within 200 px.</summary>
		private const int ProjAIStylePlayerHoming = 51;

		private static float Angle(Vector2 a, Vector2 b)
		{
			if (a.LengthSquared() < 1e-6f || b.LengthSquared() < 1e-6f) return 180f;
			return MathHelper.ToDegrees((float)Math.Acos(MathHelper.Clamp(Vector2.Dot(Vector2.Normalize(a), Vector2.Normalize(b)), -1f, 1f)));
		}

		/// <summary>This creature (or the one that threw it) just dealt damage: its idle count starts again.</summary>
		public void NoteDamageDealt()
		{
			_idleTicks = 0;
			if (IsChild && ParentWho >= 0 && ParentWho < Main.maxNPCs && Main.npc[ParentWho].TryGetGlobalNPC(out ConjuredGlobalNPC p) && p.Active) p.NoteDamageDealt();
		}

		// =====================================================================================================
		// ITargetBody: enemies chase it exactly as they chase a soldier or a summoned unit
		// =====================================================================================================
		public NPC NPC => _npc;
		public Player Dummy { get { EnsureDummy(); return _dummy; } }
		public int TargetAggro => 0;
		public bool IsAlive => Active && !IsChild && _npc != null && _npc.active && _npc.life > 0 && !_deathHandled; // a thrown orb is not a body the enemy chases or a squad member

		private void EnsureDummy()
		{
			if (_dummy != null) return;
			_dummy = new Player { whoAmI = SoldierNPC.DummySlot, active = true, dead = false, ghost = false, difficulty = 0, direction = 1 };
		}

		public void SyncDummyTransform()
		{
			EnsureDummy();
			NPC n = _npc; if (n == null) return;
			_dummy.width = n.width; _dummy.height = n.height;
			_dummy.position = n.position; _dummy.velocity = n.velocity;
			_dummy.direction = n.direction == 0 ? 1 : n.direction;
			_dummy.gravDir = 1f; _dummy.active = true; _dummy.dead = false; _dummy.ghost = false;
			_dummy.statLife = n.life; _dummy.statLifeMax = _dummy.statLifeMax2 = Math.Max(1, n.lifeMax);
			_dummy.whoAmI = SoldierNPC.DummySlot;
			BiomeMirror.Apply(_dummy, CommanderIndex, n.Center);
		}

		// =====================================================================================================
		// What its AI is pointed at
		// =====================================================================================================
		/// <summary>A free-standing player the creature's own AI reads in place of a real one: it stands on the enemy (or at the officer's side).</summary>
		private sealed class AimBody : ITargetBody
		{
			private readonly ConjuredGlobalNPC _owner; private readonly Player _p;
			public Vector2 Center, Velocity; public int W = 20, H = 42;
			public AimBody(ConjuredGlobalNPC owner) { _owner = owner; _p = new Player { whoAmI = SoldierNPC.DummySlot, active = true, dead = false, ghost = false, difficulty = 0, direction = 1 }; }
			public NPC NPC => _owner._npc;
			public Player Dummy => _p;
			public int TargetAggro => 0;
			public bool IsAlive => true;
			public void SyncDummyTransform()
			{
				_p.width = W; _p.height = H;
				_p.position = Center - new Vector2(W / 2f, H / 2f); _p.velocity = Velocity;
				_p.direction = Velocity.X < 0f ? -1 : 1; _p.gravDir = 1f;
				_p.active = true; _p.dead = false; _p.ghost = false; _p.statLife = _p.statLifeMax = _p.statLifeMax2 = 100; _p.whoAmI = SoldierNPC.DummySlot;
				BiomeMirror.Apply(_p, _owner.CommanderIndex, _owner._npc != null ? _owner._npc.Center : Center);
			}
		}

		/// <summary>The commander's officer avatar if it is still a live officer-mode soldier body; else null (triggers the creature's despawn).</summary>
		private NPC OwnerAvatar()
		{
			if (OwnerWho < 0 || OwnerWho >= Main.maxNPCs) return null;
			NPC n = Main.npc[OwnerWho];
			return n.active && n.life > 0 && n.ModNPC is SoldierNPC s && s.OfficerMode ? n : null;
		}

		private bool Flier => _npc.noGravity || _npc.aiStyle == NPCAIStyleID.Bat || _npc.aiStyle == NPCAIStyleID.DemonEye || _npc.aiStyle == NPCAIStyleID.Vulture
			|| _npc.aiStyle == NPCAIStyleID.HoveringFighter || _npc.aiStyle == NPCAIStyleID.FlyingFish || _npc.aiStyle == NPCAIStyleID.Jellyfish;

		/// <summary>An enemy this creature may go for: hostile by the army's own rules, near its officer, in reach of it. The commander's Hold Fire holds it too.</summary>
		private bool Pickable(NPC t, NPC owner)
		{
			if (t == null || !t.active || t.whoAmI == _npc.whoAmI) return false;
			if (owner.ModNPC is SoldierNPC boss && boss.Record != null && boss.Record.HoldFire) return false;
			if (!LevyUtil.IsHostileTarget(t) || SoldierTargeting.IsNeverTarget(t)) return false;
			float lim = LevyConstants.Summon.ConjureReachTiles * LevyConstants.TilePx;
			if (Vector2.Distance(_npc.Center, t.Center) > lim) return false;
			if (Vector2.Distance(owner.Center, t.Center) > LevyConstants.Summon.MeleeLeashTiles * LevyConstants.TilePx) return false; // fights beside its officer, never chases a straggler across the map
			if (!Flier)
			{
				bool level = Math.Abs(t.Center.Y - _npc.Center.Y) <= LevyConstants.Summon.MeleeVerticalReachTiles * LevyConstants.TilePx + t.height * 0.5f;
				if (!level && !Collision.CanHitLine(_npc.position, _npc.width, _npc.height, t.position, t.width, t.height)) return false; // it cannot climb the sky after a flier; a shooter may still have the line
			}
			return true;
		}

		private int FindTarget(NPC owner)
		{
			int best = -1; float bestD = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active) continue;
				float d = Vector2.DistanceSquared(_npc.Center, t.Center);
				if (d >= bestD || !Pickable(t, owner)) continue;
				bestD = d; best = i;
			}
			return best;
		}

		/// <summary>Where its idle goal is: a few tiles off to the side of its officer it is already on, fanned out per creature so a squad does not stack.</summary>
		private Vector2 FollowSpot(NPC owner)
		{
			float standoff = (LevyConstants.Summon.FollowStandoffTiles + (_npc.whoAmI % 3)) * LevyConstants.TilePx;
			float fromOwner = _npc.Center.X - owner.Center.X;
			if (Math.Abs(fromOwner) > 48f) _side = Math.Sign(fromOwner);
			else if (_side == 0) _side = _npc.whoAmI % 2 == 0 ? -1 : 1;
			float y = Flier ? owner.Center.Y - LevyConstants.Summon.FlyHoverHeightTiles * LevyConstants.TilePx : owner.Center.Y;
			return new Vector2(owner.Center.X + _side * standoff, y);
		}

		// =====================================================================================================
		// AI: the creature's own AI, run against a dummy standing where the enemy is
		// =====================================================================================================
		public override bool PreAI(NPC npc)
		{
			if (!Active) return true;
			_npc = npc;
			if (LevyUtil.IsClient) return false;      // clients run no AI: the server streams position, velocity and frame (netAlways)
			_age++;
			if (_immuneTime > 0) _immuneTime--;
			if (_strikeCd > 0) _strikeCd--;
			NPC owner = OwnerAvatar();
			if (owner == null) { Despawn(); return false; }
			Mark(npc); // another mod's hook may have cleared a flag; the army's creature stays the army's

			if (IsChild)
			{
				if (_age > LevyConstants.Summon.ConjureChildLifeTicks) { Despawn(); return false; }  // a spent orb
			}
			else if (IdleLifeTicks > 0)
			{
				if (--_fightScan <= 0) { _fightScan = LevyConstants.Summon.FightScanTicks; _fight = EnemyNear(owner); }
				if (_fight && ++_idleTicks > IdleLifeTicks) { Crumble(); return false; }           // it has dealt no damage for a while of a fight
				int left = IdleLifeTicks - _idleTicks;
				npc.alpha = left < 120 ? (int)(200f * (1f - left / 120f)) : 0;                       // it fades out over its last two seconds
			}
			if (++_scan >= LevyConstants.Summon.ScanIntervalTicks) { _scan = 0; Target = FindTarget(owner); }
			else if (Target >= 0 && (Target >= Main.maxNPCs || !Pickable(Main.npc[Target], owner))) Target = -1;

			_aim ??= new AimBody(this);
			if (Target >= 0)
			{
				NPC t = Main.npc[Target];
				_aim.Center = t.Center; _aim.Velocity = t.velocity; _aim.W = Math.Max(8, t.width); _aim.H = Math.Max(8, t.height);
				_idle = false; _farTicks = 0;
			}
			else
			{
				Vector2 spot = FollowSpot(owner);
				_aim.Center = spot; _aim.Velocity = Vector2.Zero; _aim.W = 20; _aim.H = 42;
				_idle = Math.Abs(spot.X - npc.Center.X) < LevyConstants.Summon.StopDistTiles * LevyConstants.TilePx;
				if (Vector2.Distance(npc.Center, owner.Center) > LevyConstants.Summon.LeashTiles * LevyConstants.TilePx)
				{
					if (++_farTicks >= LevyConstants.Summon.TeleportAfterTicks) { _farTicks = 0; TeleportNear(owner); return false; } // pits, long falls: brought to the officer
				}
				else _farTicks = 0;
			}
			int slot = EnemyTargeting.SlotFor(npc);
			npc.target = slot;
			npc.targetRect = new Rectangle((int)(_aim.Center.X - _aim.W / 2f), (int)(_aim.Center.Y - _aim.H / 2f), _aim.W, _aim.H);
			_aiming = true;
			EnemyTargeting.Swap(_aim, slot);
			return true;
		}

		public override void PostAI(NPC npc)
		{
			if (!Active) return;
			if (_aiming) { _aiming = false; EnemyTargeting.Unswap(); }
			if (LevyUtil.IsClient) return;
			if (_idle && !Flier) { npc.velocity.X *= 0.4f; if (Math.Abs(npc.velocity.X) < 0.1f) npc.velocity.X = 0f; } // at its post beside the officer it stands, not shuffles
			if (npc.wet && !npc.lavaWet && !Flier && npc.velocity.Y > -3f) npc.velocity.Y -= 0.15f; // lend a hand out of water
			if (_immuneTime == 0) ContactDamage();                  // an enemy touching it
			if (!npc.active || npc.life <= 0) return;
			StrikeEnemies();                                        // it touching an enemy
		}

		// ---- the answer to every "who is the closest player" the creature's AI asks ----
		internal static void Hook()
		{
			On_NPC.Transform += TransformDetour;
			On_NPC.TargetClosest += TargetClosestDetour;
			On_NPC.TargetClosestUpgraded += TargetClosestUpgradedDetour;
		}

		internal static void Unhook()
		{
			On_NPC.Transform -= TransformDetour;
			On_NPC.TargetClosest -= TargetClosestDetour;
			On_NPC.TargetClosestUpgraded -= TargetClosestUpgradedDetour;
		}

		/// <summary>A creature that changes form (the Vampire Bat becoming a Vampire) is rebuilt by vanilla from its new type's defaults, which would drop the army's marks and leave a hostile enemy beside the commander: the marks are put back.</summary>
		private static void TransformDetour(On_NPC.orig_Transform orig, NPC self, int newType)
		{
			if (!self.TryGetGlobalNPC(out ConjuredGlobalNPC c) || !c.Active) { orig(self, newType); return; }
			int owner = c.OwnerWho, cmd = c.CommanderIndex, conjured = c.ConjuredType, target = c.Target, parent = c.ParentWho, idle = c.IdleLifeTicks, minDamage = c.MinDamage;
			bool child = c.IsChild;
			orig(self, newType);
			if (!self.TryGetGlobalNPC(out ConjuredGlobalNPC c2)) return;
			c2.Bind(self, owner, cmd, conjured);
			c2.Target = target; c2.ParentWho = parent; c2.IdleLifeTicks = idle; c2.MinDamage = minDamage; c2.IsChild = child;
			self.netUpdate = true;
		}

		private static void TargetClosestDetour(On_NPC.orig_TargetClosest orig, NPC self, bool faceTarget)
		{
			if (self.TryGetGlobalNPC(out ConjuredGlobalNPC c) && c.Active && c._aim != null) { c.AnswerTargetClosest(faceTarget); return; } // also when a hit re-asks (StrikeNPC): never a real player
			orig(self, faceTarget);
		}

		private static void TargetClosestUpgradedDetour(On_NPC.orig_TargetClosestUpgraded orig, NPC self, bool faceTarget, Vector2? checkPosition)
		{
			if (self.TryGetGlobalNPC(out ConjuredGlobalNPC c) && c.Active && c._aim != null) { c.AnswerTargetClosest(faceTarget); return; }
			orig(self, faceTarget, checkPosition);
		}

		/// <summary>The creature's "target the closest player" is the dummy standing on its enemy: the same fields vanilla's search would have filled in.</summary>
		private void AnswerTargetClosest(bool faceTarget)
		{
			NPC n = _npc;
			n.target = EnemyTargeting.SlotFor(n);
			Rectangle r = new Rectangle((int)(_aim.Center.X - _aim.W / 2f), (int)(_aim.Center.Y - _aim.H / 2f), _aim.W, _aim.H);
			n.targetRect = r;
			if (faceTarget)
			{
				n.direction = r.Center.X < n.Center.X ? -1 : 1;
				n.directionY = r.Center.Y < n.Center.Y ? -1 : 1;
			}
		}

		// =====================================================================================================
		// Blows: the creature's strike, and an enemy's strike on it
		// =====================================================================================================
		/// <summary>The creature's damage in classic numbers: its own damage figure with the game mode's enemy multiplier taken back out (tier-appropriate, never scaled up).</summary>
		private int BlowDamage()
		{
			float mult = Math.Max(1f, Main.GameModeInfo.EnemyDamageMultiplier);
			int own = Math.Max(1, (int)Math.Round(_npc.damage / mult));
			return MinDamage > 0 ? Math.Max(own, (int)(MinDamage * OfficerBrain.TownDamageScale())) : own;
		}

		/// <summary>One blow on each enemy its body touches: the same strike a soldier's sword makes (commander credited first, shred mark, banner, venom). One blow per cooldown. Server / single player.</summary>
		private void StrikeEnemies()
		{
			if (_strikeCd > 0 || _npc.damage <= 0) return;
			Rectangle mine = _npc.Hitbox; mine.Inflate((int)LevyConstants.Summon.MeleeReachPx, (int)LevyConstants.Summon.MeleeReachPx);
			NPC owner = OwnerAvatar();
			if (owner?.ModNPC is SoldierNPC boss && boss.Record != null && boss.Record.HoldFire) return;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active || i == _npc.whoAmI || !mine.Intersects(t.Hitbox)) continue;
				if (!LevyUtil.IsHostileTarget(t) || SoldierTargeting.IsNeverTarget(t)) continue;
				int dmg = MarkSystem.Amp(t, BlowDamage());
				float banner = BannerBuffs.Offense(CommanderIndex, t);
				if (banner != 1f) dmg = Math.Max(1, (int)Math.Round(dmg * banner));
				if (CommanderIndex >= 0 && CommanderIndex < Main.maxPlayers && Main.player[CommanderIndex].active) t.PlayerInteraction(CommanderIndex); // credit before the blow: a killing blow reads the interaction list
				int dir = t.Center.X >= _npc.Center.X ? 1 : -1;
				t.SimpleStrikeNPC(dmg, dir, false, 3f, null, true, 0f, true);
				if (t.active) VenomSystem.OnArmyHit(t, _npc.Center);
				_strikeCd = LevyConstants.Summon.ConjureStrikeCooldownTicks;
				NoteDamageDealt();
				if (IsChild) ChildBlows++;
				if (IsChild && LevyUtil.IsProjectileNpc(_npc.type)) Despawn();                      // an orb is spent by the blow it lands
				return;
			}
		}

		/// <summary>An overlapping hostile hurts the creature (vanilla applies no NPC-vs-NPC contact for it). One hit per immune window.</summary>
		private void ContactDamage()
		{
			Rectangle mine = _npc.Hitbox;
			float cx = _npc.Center.X, cy = _npc.Center.Y;
			float halfW = _npc.width * 0.5f + LevyConstants.ContactBroadPhasePx, halfH = _npc.height * 0.5f + LevyConstants.ContactBroadPhasePx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC a = Main.npc[i];
				if (!a.active || a.friendly || a.damage <= 0 || i == _npc.whoAmI) continue;
				if (Math.Abs(a.Center.X - cx) > a.width * 0.5f + halfW || Math.Abs(a.Center.Y - cy) > a.height * 0.5f + halfH) continue;
				if (!global::Levy.Common.Util.HostileContact.Hits(a, i, mine, global::Levy.Common.Util.HostileContact.Stand, _npc, out _, out float mult)) continue; // the player's own hit rules (SoldierNPC.Damage.cs); one window for every immunity slot
				int dir = a.Center.X < cx ? 1 : -1;
				int raw = Math.Max(1, (int)Math.Round(Main.DamageVar(a.damage * mult) * BannerBuffs.Defense(CommanderIndex, a)));
				_npc.SimpleStrikeNPC(raw, dir, false, 0f, null, false, 0f, true);
				_immuneTime = LevyConstants.ImmuneTicksOnHit;
				if (_npc.active && _npc.life > 0) _npc.netUpdate = true;
				return;
			}
		}

		public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
		{
			if (Active) modifiers.DefenseEffectiveness *= LevyUtil.ModeDefenseEffectiveness() / 0.5f;
		}

		/// <summary>Falls apart: it dealt no damage for too long of a fight. No loot, no kill count (<see cref="PreKill"/>).</summary>
		private void Crumble()
		{
			Crumbled++;
			for (int i = 0; i < 12; i++) { int d = Dust.NewDust(_npc.position, _npc.width, _npc.height, DustID.Bone, 0f, -1.5f, 100, default, 1.1f); Main.dust[d].noGravity = true; }
			Despawn();
		}

		private void Despawn()
		{
			_deathHandled = true;
			_npc.life = 0; _npc.active = false; _npc.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, _npc.whoAmI);
		}

		private bool TeleportNear(NPC owner)
		{
			if (!FollowerTravel.FindStandingSpot(owner.Center, 8, out Vector2 spot)) return false;
			_npc.position = new Vector2(spot.X - _npc.width / 2f, spot.Y - _npc.height);
			_npc.oldPosition = _npc.position; _npc.velocity = Vector2.Zero; _npc.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: _npc.whoAmI);
			return true;
		}

		// =====================================================================================================
		// Vanilla hooks: it is the army's, so it is never the player's problem and never loot
		// =====================================================================================================
		public override bool CheckDead(NPC npc) { if (Active) _deathHandled = true; return true; }
		public override bool PreKill(NPC npc) => !Active;                  // no loot, coins, banner or kill count, no event progress
		public override bool CheckActive(NPC npc) => !Active;              // the kit owns the lifecycle, not distance
		public override bool? CanBeHitByItem(NPC npc, Player player, Item item) => Active ? false : null;
		public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile) => Active && projectile.friendly && !projectile.hostile ? false : null;
		public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => !Active;
		public override bool? DrawHealthBar(NPC npc, byte hbPosition, ref float scale, ref Vector2 position) => Active ? false : null; // we draw our own bar

		public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (!Active || Main.dedServ) return;
			int w = Math.Clamp(npc.width, LevyConstants.Summon.HpBarWidthPx, 90), h = LevyConstants.Summon.HpBarHeightPx;
			Vector2 top = npc.Top - screenPos;
			float frac = MathHelper.Clamp(npc.life / (float)Math.Max(1, npc.lifeMax), 0f, 1f);
			var pixel = TextureAssets.MagicPixel.Value;
			int bx = (int)(top.X - w * 0.5f), by = (int)(top.Y - LevyConstants.Summon.HpBarOffsetY);
			spriteBatch.Draw(pixel, new Rectangle(bx - 1, by - 1, w + 2, h + 2), new Color(20, 20, 20, 200));
			spriteBatch.Draw(pixel, new Rectangle(bx, by, (int)(w * frac), h), Color.Lerp(Color.Red, Color.LimeGreen, frac));
		}

		// =====================================================================================================
		// Net: clients need to know it is conjured (friendly, owner, commander, target); life/lifeMax ride the vanilla NPC sync
		// =====================================================================================================
		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
		{
			bitWriter.WriteBit(Active);
			if (!Active) return;
			bitWriter.WriteBit(IsChild);
			binaryWriter.Write((short)OwnerWho); binaryWriter.Write((short)CommanderIndex); binaryWriter.Write((short)Target); binaryWriter.Write((short)ConjuredType);
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
		{
			bool a = bitReader.ReadBit();
			if (!a) { Active = false; return; }
			IsChild = bitReader.ReadBit();
			OwnerWho = binaryReader.ReadInt16(); CommanderIndex = binaryReader.ReadInt16(); Target = binaryReader.ReadInt16(); ConjuredType = binaryReader.ReadInt16();
			_npc = npc;
			if (!Active) { Active = true; _deathHandled = false; }
			Mark(npc);
		}
	}

	/// <summary>
	/// A projectile a conjured creature's AI fires is the army's: it spawns friendly and not hostile, owned by the real player index (what every other army shot is owned by), credited to the commander,
	/// and an NPC-style shot (the commander's own on-hit item effects and drops never run for it). What such a projectile spawns in turn is the army's too. It never touches a player.
	/// </summary>
	public class ConjuredProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		public bool Conjured;
		public int CommanderIndex = -1;
		/// <summary>whoAmI of the conjured creature that fired it (a blow that lands counts as that creature's), or -1.</summary>
		public int SourceWho = -1;

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (source is EntitySource_Parent { Entity: NPC npc } && npc.TryGetGlobalNPC(out ConjuredGlobalNPC c) && c.Active)
			{
				if (!LevyUtil.IsClient && !c.MayShoot(projectile)) { projectile.active = false; return; } // no enemy to shoot at, or it would fly at a player: it never leaves the muzzle
				Adopt(projectile, c.CommanderIndex); SourceWho = npc.whoAmI;
			}
			else if (source is EntitySource_Parent { Entity: Projectile parent } && parent.TryGetGlobalProjectile(out ConjuredProjectile pp) && pp.Conjured) { Adopt(projectile, pp.CommanderIndex); SourceWho = pp.SourceWho; }
		}

		private void Adopt(Projectile p, int commander)
		{
			Conjured = true; CommanderIndex = commander;
			p.hostile = false; p.friendly = true; p.npcProj = true; p.noDropItem = true;
			p.owner = SoldierMinions.TrueMyPlayer;
		}

		public override void PostAI(Projectile projectile)
		{
			if (Conjured && (projectile.hostile || !projectile.friendly)) { projectile.hostile = false; projectile.friendly = true; } // a projectile that re-arms itself as hostile in its own AI stays on our side
		}

		public override bool CanHitPlayer(Projectile projectile, Player target) => !Conjured;

		/// <summary>Diagnostics for the world self-checks: blows a conjured creature's projectiles landed on NPCs, and the damage they did.</summary>
		public static int HitsOnNpcs, DamageOnNpcs;
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!Conjured) return;
			HitsOnNpcs++; DamageOnNpcs += damageDone;
			if (SourceWho >= 0 && SourceWho < Main.maxNPCs && Main.npc[SourceWho].TryGetGlobalNPC(out ConjuredGlobalNPC c) && c.Active) c.NoteDamageDealt(); // an arrow that lands keeps its archer standing
		}
	}

	/// <summary>Per-world plumbing for <see cref="ConjuredGlobalNPC"/>: the TargetClosest answers.</summary>
	public class ConjuredSystem : ModSystem
	{
		public override void Load() => ConjuredGlobalNPC.Hook();
		public override void Unload() => ConjuredGlobalNPC.Unhook();
	}
}
