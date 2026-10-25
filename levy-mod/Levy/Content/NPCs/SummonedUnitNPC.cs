using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Movement;
using Levy.Common.Mark;
using Levy.Common.Officers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Venom;
using Levy.Common.Weapons;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// A friendly combat unit summoned by an officer kit: a REAL NPC with a healthbar, not a HP-less minion. It borrows the
	/// <see cref="SoldierNPC"/> pattern — a friendly, non-town, aiStyle -1 body that enemies chase through the slot-255 swap
	/// (it is an <see cref="ITargetBody"/> fed into <see cref="EnemyTargeting.Bodies"/> by <see cref="SummonedUnitSystem"/>) —
	/// but carries no gear, no record and no roster seat, so it never counts against manpower, command points or the render cap.
	///
	/// One NPC type serves every summon kit: the unit is parameterized by a <see cref="SummonKind"/> (HP, damage, flintlock/melee
	/// projectile, borrowed sprite, speed). It is OWNER-BOUND to the summoning officer avatar (its whoAmI) and auto-despawns the
	/// instant that avatar is gone, dismissed or dead; the kit also kills the whole squad on dismissal, so this is a safety net.
	/// Server / single player drives the brain and fires the weapon; clients draw the borrowed sprite and the healthbar. Transient:
	/// never saved — the owning kit respawns the squad on reload.
	/// </summary>
	public class SummonedUnitNPC : ModNPC, ITargetBody
	{
		public const int BodyWidth = 20, BodyHeight = 40;

		// ---- bound data (set server-side in Init, synced to clients) ----
		/// <summary>Index into <see cref="SummonKinds"/>; -1 until bound. Clients resolve the kind from it for drawing and the bar.</summary>
		public int KindId = -1;
		/// <summary>whoAmI of the officer avatar (a <see cref="SoldierNPC"/> in officer mode) that owns this unit. The unit dies with it.</summary>
		public int OwnerWho = -1;
		/// <summary>Player index to credit for this unit's kills/banners (the commander), or -1. Read by <see cref="Common.Globals.SoldierProjectileTag"/>.</summary>
		public int CommanderIndex = -1;

		/// <summary>Free-standing Player swapped into Main.player[255] for one enemy AI call so the enemy chases this body (never in Main.player[] otherwise).</summary>
		public Player Dummy { get; private set; }

		// ---- brain state (server) ----
		private int _cooldown, _scan, _target = -1, _side, _farTicks, _jumpCooldown, _age, _immuneTime;
		private bool _deathHandled;

		public SummonKind Kind => SummonKinds.ById(KindId);

		public override string Texture => "Levy/Content/NPCs/SoldierNPC"; // reuse the soldier placeholder; the real draw borrows a vanilla sprite

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 1;
			NPCID.Sets.ActsLikeTownNPC[Type] = true;          // same as SoldierNPC: keeps it out of hostile spawn logic and consistent for targeting
			NPCID.Sets.NoTownNPCHappiness[Type] = true;
			NPCID.Sets.CantTakeLunchMoney[Type] = true;
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
		}

		public override void SetDefaults()
		{
			NPC.width = BodyWidth; NPC.height = BodyHeight;
			NPC.friendly = true; NPC.townNPC = false;
			NPC.aiStyle = -1;
			NPC.damage = 0; NPC.defense = 0;
			NPC.lifeMax = 100; NPC.life = NPC.lifeMax; // placeholder until Init / sync sets it from the kind
			NPC.knockBackResist = 0.5f;
			NPC.npcSlots = 0.5f;
			NPC.HitSound = SoundID.NPCHit1; NPC.DeathSound = SoundID.NPCDeath1;
			NPC.lavaImmune = false; NPC.noGravity = false; NPC.noTileCollide = false;
			NPC.rarity = 0; NPC.value = 0f;
			NPC.netAlways = true; // like soldiers/avatars: stream position every tick (clients run no brain)
			EnsureDummy();
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) { }

		/// <summary>Binds this unit to a kind, an owning officer avatar and a commander (server). Call once right after NPC.NewNPC, then sync.</summary>
		public void Init(SummonKind kind, int ownerWho, int commanderIndex)
		{
			KindId = kind?.Id ?? -1;
			OwnerWho = ownerWho;
			CommanderIndex = commanderIndex;
			ApplyBody(kind);
			NPC.knockBackResist = kind != null && kind.Turret ? 0f : 0.5f; // an emplacement is not shoved around
			int hp = Math.Max(1, kind?.MaxHp ?? 100);
			NPC.lifeMax = hp; NPC.life = hp;
			NPC.GivenName = kind?.Name ?? "";
			NPC.stepSpeed = 1f;
			_deathHandled = false;
			_side = 0; _target = -1; _cooldown = 0; _scan = 0; _farTicks = 0; _age = 0;
			NPC.netUpdate = true;
		}

		/// <summary>
		/// Sizes the hitbox to the kind (a standing man, or a cannon). The top-left corner stays put: that is what the position sync carries, so a client that learns the kind late still lines up
		/// with the server, and the server places the feet itself right after <see cref="Init"/>.
		/// </summary>
		private void ApplyBody(SummonKind kind)
		{
			if (kind == null) return;
			NPC.width = kind.Width; NPC.height = kind.Height;
			NPC.noGravity = kind.Flying; NPC.noTileCollide = kind.Flying; // a flier ignores gravity and terrain (a sword through a cave wall, a drone over a pit); every machine derives it from the kind
		}

		private void EnsureDummy()
		{
			if (Dummy != null) return;
			Dummy = new Player { whoAmI = SoldierNPC.DummySlot, active = true, dead = false, ghost = false, difficulty = 0, direction = 1 };
			Dummy.width = BodyWidth; Dummy.height = BodyHeight;
		}

		// =====================================================================================================
		// ITargetBody — enemies chase this body exactly like a soldier's (EnemyTargeting swaps Dummy into slot 255).
		// =====================================================================================================
		public int TargetAggro => Kind?.Aggro ?? 0;
		public bool IsAlive => NPC.active && NPC.life > 0 && !_deathHandled && KindId >= 0;

		public void SyncDummyTransform()
		{
			EnsureDummy();
			Dummy.width = NPC.width; Dummy.height = NPC.height;
			Dummy.position = NPC.position; Dummy.velocity = NPC.velocity;
			Dummy.direction = NPC.direction == 0 ? 1 : NPC.direction;
			Dummy.gravDir = 1f; Dummy.active = true; Dummy.dead = false; Dummy.ghost = false;
			Dummy.statLife = NPC.life; Dummy.statLifeMax = Dummy.statLifeMax2 = Math.Max(1, NPC.lifeMax);
			Dummy.whoAmI = SoldierNPC.DummySlot;
		}

		// =====================================================================================================
		// AI: server/single drives the brain; clients only keep the dummy co-located for the targeting swap.
		// =====================================================================================================
		public override void AI()
		{
			EnsureDummy();
			SyncDummyTransform();
			if (KindId < 0 || Kind == null) return; // not yet bound/synced
			if (LevyUtil.IsClient) return;          // clients run no brain; position streams via netAlways

			_age++;
			if (_immuneTime > 0) _immuneTime--;
			NPC owner = OwnerAvatar();
			if (owner == null) { Despawn(); return; }                              // owner gone/dismissed/dead: unit goes with it
			if (Kind.LifespanTicks > 0 && _age >= Kind.LifespanTicks) { Despawn(); return; }

			if (_immuneTime == 0) ContactDamage();                                 // melee enemies (vanilla deals no NPC-vs-NPC contact); hostile projectiles already strike friendly NPCs
			if (!NPC.active || NPC.life <= 0) return;

			Combat();
			if (Kind.Melee) MeleeAi(owner); else RangedAi(owner);
			SyncDummyTransform();
		}

		/// <summary>The stand-off profile: fire at a hostile in reach without ever chasing it; otherwise hold near the officer (a post, a hover, or a walk).</summary>
		private void RangedAi(NPC owner)
		{
			bool engaging = _target >= 0 && _target < Main.maxNPCs && Shootable(Main.npc[_target]);
			if (engaging)
			{
				NPC t = Main.npc[_target];
				NPC.direction = t.Center.X < NPC.Center.X ? -1 : 1; NPC.spriteDirection = NPC.direction;
				if (Kind.Flying) NPC.velocity *= LevyConstants.Summon.FlyDamp;           // hover where it is while it shoots
				else
				{
					NPC.velocity.X *= 0.8f; if (Math.Abs(NPC.velocity.X) < 0.1f) NPC.velocity.X = 0f; // stand and fight, never chase
					if (NPC.wet && NPC.velocity.Y > -3f) NPC.velocity.Y -= 0.35f;
				}
				if (_cooldown <= 0) { Fire(t); _cooldown = Kind.CooldownTicks + Main.rand.Next(Math.Max(1, Kind.RandomCooldownTicks)); }
			}
			else Idle(owner);
		}

		/// <summary>
		/// The charger profile: run (or fly) down the nearest hostile near the officer and cut it on contact, one blow per cooldown. With nothing to charge it falls back to the officer.
		/// A ground charger works from its feet (it never leaves the ground after a flier); a flying one goes straight through the air at the target's centre and keeps pressing on it.
		/// </summary>
		private void MeleeAi(NPC owner)
		{
			bool engaging = _target >= 0 && _target < Main.maxNPCs && Shootable(Main.npc[_target]);
			if (!engaging) { Idle(owner); return; }

			NPC t = Main.npc[_target];
			NPC.direction = t.Center.X < NPC.Center.X ? -1 : 1; NPC.spriteDirection = NPC.direction;
			bool contact = InContact(t);
			if (Kind.Flying) FlyToward(t.Center, Kind.MoveSpeedPx, 0f);
			else if (contact) { NPC.velocity.X *= 0.8f; if (Math.Abs(NPC.velocity.X) < 0.1f) NPC.velocity.X = 0f; } // planted against it while it swings
			else WalkToward(t.Center.X, NPC.width * 0.5f, Kind.MoveSpeedPx);

			if (contact && _cooldown <= 0)
			{
				Strike(t);
				_cooldown = Kind.CooldownTicks + Main.rand.Next(Math.Max(1, Kind.RandomCooldownTicks));
			}
		}

		/// <summary>Nothing to fight: hold the post (a cannon), hover beside the officer (a flier) or walk beside him.</summary>
		private void Idle(NPC owner)
		{
			if (Kind.Turret) HoldPost(owner);
			else if (Kind.Flying) HoverOwner(owner);
			else FollowOwner(owner);
		}

		/// <summary>True when this unit's hitbox, grown by the melee reach, touches the target's.</summary>
		private bool InContact(NPC t)
		{
			Rectangle mine = NPC.Hitbox;
			mine.Inflate((int)LevyConstants.Summon.MeleeReachPx, (int)LevyConstants.Summon.MeleeReachPx);
			return mine.Intersects(t.Hitbox);
		}

		/// <summary>
		/// One melee blow: the same strike a soldier's sword makes. The commander is credited with the target before the blow (a killing blow drops loot synchronously inside it, and banners and
		/// the "defeated by" line read the interaction list), the army's shred mark amps it, and the Witch Doctor's venom may land. Server / single player only (this runs inside the brain).
		/// </summary>
		private void Strike(NPC t)
		{
			SummonKind k = Kind;
			int dmg = MarkSystem.Amp(t, ScaledDamage(k));
			if (CommanderIndex >= 0 && CommanderIndex < Main.maxPlayers && Main.player[CommanderIndex].active) t.PlayerInteraction(CommanderIndex);
			int dir = t.Center.X >= NPC.Center.X ? 1 : -1;
			t.SimpleStrikeNPC(dmg, dir, false, k.Knockback, null, true, 0f, true); // noPlayerInteraction: the commander was credited above (the strike's own credit would name the dedicated server's slot)
			if (t.active) VenomSystem.OnArmyHit(t, NPC.Center);
		}

		/// <summary>The kind's base damage after the world-progress and game-mode multipliers: what one blow or shot does right now.</summary>
		private static int ScaledDamage(SummonKind k)
		{
			int dmg = (int)(k.BaseDamage * OfficerBrain.TownDamageScale());   // scales with world progress so summons stay tier-appropriate
			if (Main.expertMode) dmg = (int)(dmg * Main.GameModeInfo.TownNPCDamageMultiplier);
			return Math.Max(1, dmg);
		}

		/// <summary>The owning officer avatar if it is still a live officer-mode soldier body; else null (triggers self-despawn).</summary>
		private NPC OwnerAvatar()
		{
			if (OwnerWho < 0 || OwnerWho >= Main.maxNPCs) return null;
			NPC n = Main.npc[OwnerWho];
			return n.active && n.life > 0 && n.ModNPC is SoldierNPC s && s.OfficerMode ? n : null;
		}

		private void Despawn()
		{
			_deathHandled = true;
			NPC.life = 0; NPC.active = false; NPC.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
		}

		/// <summary>Takes contact damage from an overlapping hostile (vanilla applies no NPC-vs-NPC contact, so the unit applies it itself, as a soldier does). One hit per immune window.</summary>
		private void ContactDamage()
		{
			Rectangle mine = NPC.Hitbox;
			float cx = NPC.Center.X, cy = NPC.Center.Y;
			float halfW = NPC.width * 0.5f + LevyConstants.ContactBroadPhasePx, halfH = NPC.height * 0.5f + LevyConstants.ContactBroadPhasePx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC a = Main.npc[i];
				if (!a.active || a.friendly || a.damage <= 0 || i == NPC.whoAmI) continue; // friendly skips our own side (soldiers, avatars, other units)
				if (Math.Abs(a.Center.X - cx) > a.width * 0.5f + halfW || Math.Abs(a.Center.Y - cy) > a.height * 0.5f + halfH) continue;
				if (!NPCLoader.CanHitNPC(a, NPC)) continue;
				int slot = -1; float mult = 1f;
				Rectangle rect = a.Hitbox;
				NPC.GetMeleeCollisionData(mine, i, ref slot, ref mult, ref rect);
				if (!mine.Intersects(rect)) continue;

				int dir = a.Center.X < cx ? 1 : -1;
				int raw = Math.Max(1, Main.DamageVar(a.damage * mult));
				NPC.SimpleStrikeNPC(raw, dir, false, 0f, null, false, 0f, true); // ModifyIncomingHit applies the player-defense formula; the server broadcasts the strike
				_immuneTime = LevyConstants.ImmuneTicksOnHit;
				if (NPC.active && NPC.life > 0) NPC.netUpdate = true;
				return; // one contact hit per tick
			}
		}

		// ---- combat ----
		private void Combat()
		{
			if (_cooldown > 0) _cooldown--;
			if (++_scan >= LevyConstants.Summon.ScanIntervalTicks) { _scan = 0; _target = FindTarget(); }
			else if (_target >= 0 && (_target >= Main.maxNPCs || !Shootable(Main.npc[_target]))) _target = -1;
		}

		private int FindTarget()
		{
			int best = -1; float bestD = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active) continue;
				float d = Vector2.Distance(NPC.Center, t.Center);
				if (d >= bestD || !Shootable(t)) continue;
				bestD = d; best = i;
			}
			return best;
		}

		private bool Shootable(NPC t)
		{
			if (!LevyUtil.IsHostileTarget(t) || SoldierTargeting.IsNeverTarget(t)) return false;
			if (Vector2.Distance(NPC.Center, t.Center) > Kind.RangeTiles * LevyConstants.TilePx) return false;
			if (Kind.Melee && !CanCharge(t)) return false;
			if (Kind.Melee && Kind.Flying) return true; // a flying charger needs no sightline: it goes straight through the terrain
			return Collision.CanHitLine(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height);
		}

		/// <summary>
		/// A melee unit fights beside its officer: the target must be within the leash of him, and a ground unit also needs it at about its own height (it cannot climb the sky after a flier).
		/// </summary>
		private bool CanCharge(NPC t)
		{
			NPC owner = OwnerAvatar();
			if (owner != null && Vector2.Distance(owner.Center, t.Center) > LevyConstants.Summon.MeleeLeashTiles * LevyConstants.TilePx) return false;
			if (!Kind.Flying && Math.Abs(t.Center.Y - NPC.Center.Y) > LevyConstants.Summon.MeleeVerticalReachTiles * LevyConstants.TilePx + t.height * 0.5f) return false;
			return true;
		}

		private void Fire(NPC target)
		{
			SummonKind k = Kind;
			int dir = NPC.spriteDirection == 0 ? 1 : NPC.spriteDirection;
			Vector2 aim = new Vector2(dir, -0.2f);
			float frac = MathHelper.Clamp(Vector2.Distance(NPC.Center, target.Center) / (k.RangeTiles * LevyConstants.TilePx), 0f, 1f);
			Vector2 v = NPC.DirectionTo(target.Center + new Vector2(0f, -k.GravityCorrection * frac));
			if (!v.HasNaNs()) aim = v;
			aim *= k.ProjectileSpeed;

			int dmg = ScaledDamage(k);

			float muzzle = NPC.width * 0.5f + 4f; // just past the body: 14 px for a man, the barrel's end for a cannon
			int idx = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center.X + dir * muzzle, NPC.Center.Y - 2f,
				aim.X, aim.Y, k.ProjectileType, dmg, k.Knockback, SoldierMinions.TrueMyPlayer);
			if (idx >= 0 && idx < Main.maxProjectiles)
			{
				Projectile p = Main.projectile[idx];
				p.friendly = true; p.hostile = false; p.npcProj = true; p.noDropItem = true; // CommanderIndex credit is tagged in SoldierProjectileTag.OnSpawn
			}
			if (Main.netMode != NetmodeID.Server) Terraria.Audio.SoundEngine.PlaySound(k.FireSound, NPC.Center);
		}

		/// <summary>
		/// An emplacement (a cannon): never walks, so it stays where it was deployed and faces the way the officer faces. When the army marches on past the kind's leash for a few seconds it is
		/// redeployed beside the officer (the same bring-along the foot units use), so it never gets left behind.
		/// </summary>
		private void HoldPost(NPC owner)
		{
			NPC npc = NPC;
			npc.velocity.X *= 0.5f; if (Math.Abs(npc.velocity.X) < 0.1f) npc.velocity.X = 0f;
			int dir = owner.direction == 0 ? 1 : owner.direction;
			npc.direction = dir; npc.spriteDirection = dir;
			float leash = (Kind.LeashTiles > 0f ? Kind.LeashTiles : LevyConstants.Summon.LeashTiles) * LevyConstants.TilePx;
			if (Vector2.Distance(npc.Center, owner.Center) > leash)
			{
				if (++_farTicks >= LevyConstants.Summon.TeleportAfterTicks) { _farTicks = 0; TeleportNear(owner); }
			}
			else _farTicks = 0;
		}

		// ---- movement: hold near the owner, step ledges, hop small walls; never wander off (a far unit is brought back) ----
		/// <summary>The x this unit idles at beside its officer: a few tiles off to the side it is already on, fanned out per unit so a squad does not stack.</summary>
		private float FollowGoalX(NPC owner)
		{
			NPC npc = NPC;
			float standoff = (LevyConstants.Summon.FollowStandoffTiles + (npc.whoAmI % 3)) * LevyConstants.TilePx;
			float fromOwner = npc.Center.X - owner.Center.X;
			if (Math.Abs(fromOwner) > 48f) _side = Math.Sign(fromOwner);
			else if (_side == 0) _side = npc.whoAmI % 2 == 0 ? -1 : 1;
			return owner.Center.X + _side * standoff;
		}

		/// <summary>Counts the ticks this unit has been left far behind and brings it to the officer once it has been long enough (pits, long falls). True when it was moved this tick.</summary>
		private bool LeashTeleport(NPC owner)
		{
			if (Vector2.Distance(NPC.Center, owner.Center) > LevyConstants.Summon.LeashTiles * LevyConstants.TilePx)
			{
				if (++_farTicks >= LevyConstants.Summon.TeleportAfterTicks) { _farTicks = 0; if (TeleportNear(owner)) return true; }
			}
			else _farTicks = 0;
			return false;
		}

		private void FollowOwner(NPC owner)
		{
			float goalX = FollowGoalX(owner);
			if (LeashTeleport(owner)) return;
			WalkToward(goalX, LevyConstants.Summon.StopDistTiles * LevyConstants.TilePx, Kind.MoveSpeedPx);
		}

		/// <summary>Walk toward <paramref name="goalX"/>, stopping inside <paramref name="stopPx"/> of it; step ledges and hop walls on the way.</summary>
		private void WalkToward(float goalX, float stopPx, float max)
		{
			NPC npc = NPC;
			float dx = goalX - npc.Center.X;
			if (_jumpCooldown > 0) _jumpCooldown--;
			bool grounded = npc.velocity.Y == 0f;
			if (Math.Abs(dx) > stopPx)
			{
				int dir = dx > 0f ? 1 : -1;
				npc.direction = dir; npc.spriteDirection = dir;
				float vx = npc.velocity.X;
				if (Math.Sign(vx) == -dir) vx *= 0.85f;
				vx += dir * LevyConstants.Summon.AccelPx;
				if (Math.Abs(vx) > max) vx = dir * max;
				npc.velocity.X = vx;
				if (grounded) Walk(dir);
			}
			else { npc.velocity.X *= 0.8f; if (Math.Abs(npc.velocity.X) < 0.1f) npc.velocity.X = 0f; }
			if (npc.wet && !npc.lavaWet && npc.velocity.Y > -3f) npc.velocity.Y -= 0.35f; // swim out of water
		}

		/// <summary>A flier idling: hover above its follow spot beside the officer, bobbing, and be brought along if left far behind.</summary>
		private void HoverOwner(NPC owner)
		{
			float goalX = FollowGoalX(owner);
			if (LeashTeleport(owner)) return;
			float bob = (float)Math.Sin(_age * 0.06 + NPC.whoAmI) * LevyConstants.Summon.FlyBobPx;
			Vector2 goal = new Vector2(goalX, owner.Center.Y - LevyConstants.Summon.FlyHoverHeightTiles * LevyConstants.TilePx + bob);
			FlyToward(goal, Kind.MoveSpeedPx, LevyConstants.Summon.FlyArriveDistPx);
			if (Math.Abs(NPC.velocity.X) > 0.3f) { NPC.direction = NPC.velocity.X > 0f ? 1 : -1; NPC.spriteDirection = NPC.direction; }
		}

		/// <summary>Accelerate toward <paramref name="goal"/> up to <paramref name="max"/> px/tick; inside <paramref name="arriveDistPx"/> of it, settle (the speed bleeds off).</summary>
		private void FlyToward(Vector2 goal, float max, float arriveDistPx)
		{
			Vector2 d = goal - NPC.Center;
			float dist = d.Length();
			if (dist <= arriveDistPx || dist < 1f) { NPC.velocity *= LevyConstants.Summon.FlyDamp; return; }
			NPC.velocity += d / dist * LevyConstants.Summon.FlyAccelPx;
			float sp = NPC.velocity.Length();
			if (sp > max) NPC.velocity *= max / sp;
		}

		/// <summary>Grounded and walking: step up a ledge; hop a wall of 2..max tiles; stop at lava or a bottomless drop (the leash brings it back).</summary>
		private void Walk(int dir)
		{
			NPC npc = NPC;
			Collision.StepUp(ref npc.position, ref npc.velocity, npc.width, npc.height, ref npc.stepSpeed, ref npc.gfxOffY);
			int fx = (int)((npc.Center.X + dir * (npc.width / 2f + 4f)) / 16f);
			int fy = (int)((npc.Bottom.Y - 1f) / 16f);
			int max = LevyConstants.Summon.MaxJumpTiles;
			int h = 0;
			while (h <= max + 1 && TileNav.IsSolid(fx, fy - h)) h++;
			if (h >= 2 && h <= max && _jumpCooldown <= 0 && !TileNav.IsSolid(fx, fy - h - 1)
				&& !Collision.SolidCollision(npc.position - new Vector2(0f, 16f), npc.width, npc.height))
			{
				npc.velocity.Y = -(float)Math.Sqrt(2f * LevyConstants.Summon.Gravity * (h * LevyConstants.TilePx + LevyConstants.Summon.JumpClearPx));
				_jumpCooldown = 20;
				npc.netUpdate = true;
			}
			else if (h == 0)
			{
				int drop = TileNav.DropDepth(fx, fy);
				if (drop == -2 || drop == -1) npc.velocity.X = 0f; // lava or no floor in reach: wait
			}
			else if (h > max) npc.velocity.X *= 0.5f; // a wall too tall: ease to a stop
		}

		private bool TeleportNear(NPC owner)
		{
			if (!FollowerTravel.FindStandingSpot(owner.Center, 8, out Vector2 spot)) return false;
			NPC.position = new Vector2(spot.X - NPC.width / 2f, spot.Y - NPC.height);
			NPC.oldPosition = NPC.position;
			NPC.velocity = Vector2.Zero;
			NPC.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
			return true;
		}

		// =====================================================================================================
		// Vanilla NPC hooks
		// =====================================================================================================
		public override bool CheckDead() { _deathHandled = true; return true; }
		public override bool PreKill() => false;              // no vanilla loot
		public override bool CheckActive() => false;          // the owner kit owns the lifecycle, not distance
		public override bool NeedSaving() => false;           // transient: respawned by the kit
		public override bool UsesPartyHat() => false;
		public override bool CanChat() => false;
		public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
		public override bool? CanBeHitByItem(Player player, Item item) => false;
		public override bool? CanBeHitByProjectile(Projectile projectile) => projectile.friendly && !projectile.hostile ? false : null;
		public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false; // we draw our own bar

		public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
		{
			modifiers.DefenseEffectiveness *= LevyUtil.ModeDefenseEffectiveness() / 0.5f;
		}

		// =====================================================================================================
		// Draw: borrow a vanilla NPC sprite (no new art) and a Nameplates-style healthbar.
		// =====================================================================================================
		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			SummonKind k = Kind;
			if (k == null || Main.dedServ) return true;
			try
			{
				Main.instance.LoadNPC(k.DrawNpcType);
				Texture2D tex = TextureAssets.Npc[k.DrawNpcType]?.Value;
				if (tex == null) return true;
				int frames = Math.Max(1, Main.npcFrameCount[k.DrawNpcType]);
				int frameH = tex.Height / frames;
				Rectangle frame = new Rectangle(0, 0, tex.Width, frameH); // frame 0 (standing); no frame-layout guesswork
				int sdir = NPC.direction == 0 ? 1 : NPC.direction;
				SpriteEffects fx = sdir == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
				Color c = Lighting.GetColor((int)(NPC.Center.X / 16f), (int)(NPC.Center.Y / 16f), drawColor);
				if (k.Flying)
				{
					// A flier hangs in the air: the sprite is centred on the hitbox and leans into its motion (every machine derives the lean from the streamed velocity).
					float lean = MathHelper.Clamp(NPC.velocity.X * 0.06f, -0.45f, 0.45f);
					spriteBatch.Draw(tex, NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY), frame, c, lean, new Vector2(frame.Width / 2f, frame.Height / 2f), 1f, fx, 0f);
					return false;
				}
				Vector2 origin = new Vector2(frame.Width / 2f, frame.Height);
				float bob = Math.Abs(NPC.velocity.X) > 0.3f && NPC.velocity.Y == 0f ? (float)Math.Sin(Main.GameUpdateCount * 0.3) * 2f : 0f;
				Vector2 pos = new Vector2(NPC.Center.X, NPC.Bottom.Y + 4f + bob) - screenPos + new Vector2(0f, NPC.gfxOffY);
				spriteBatch.Draw(tex, pos, frame, c, 0f, origin, 1f, fx, 0f);
				return false;
			}
			catch (Exception e)
			{
				Levy.Instance.Logger.Warn("Levy: drawing a summoned unit's borrowed sprite failed: " + e.Message);
				return true; // fall back to the placeholder
			}
		}

		public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (Main.dedServ || KindId < 0) return;
			// Nameplates-style HP bar over the head (same six lines the soldier bar uses).
			int w = LevyConstants.Summon.HpBarWidthPx, h = LevyConstants.Summon.HpBarHeightPx;
			Vector2 top = NPC.Top - screenPos;
			float frac = MathHelper.Clamp(NPC.life / (float)Math.Max(1, NPC.lifeMax), 0f, 1f);
			var pixel = TextureAssets.MagicPixel.Value;
			int bx = (int)(top.X - w * 0.5f), by = (int)(top.Y - LevyConstants.Summon.HpBarOffsetY);
			spriteBatch.Draw(pixel, new Rectangle(bx - 1, by - 1, w + 2, h + 2), new Color(20, 20, 20, 200));
			spriteBatch.Draw(pixel, new Rectangle(bx, by, (int)(w * frac), h), Color.Lerp(Color.Red, Color.LimeGreen, frac));
		}

		// =====================================================================================================
		// Net: clients need the kind (sprite + bar), owner and commander. Life/lifeMax ride the vanilla NPC sync.
		// =====================================================================================================
		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.Write((short)KindId);
			writer.Write((short)OwnerWho);
			writer.Write((short)CommanderIndex);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			KindId = reader.ReadInt16();
			OwnerWho = reader.ReadInt16();
			CommanderIndex = reader.ReadInt16();
			SummonKind k = Kind;
			if (k != null)
			{
				ApplyBody(k);
				if (NPC.lifeMax != k.MaxHp) NPC.lifeMax = Math.Max(1, k.MaxHp); // per-instance cap is not in the vanilla sync
				NPC.GivenName = k.Name;
			}
			EnsureDummy();
		}
	}
}
