using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// REAL SUMMON MINIONS anchored to a soldier. A summoner soldier keeps up the vanilla minion projectile its staff makes (a Pygmy, a Pirate, a Desert Tiger...), and that
	/// projectile runs its OWN vanilla AI: ground walkers walk, the tiger pounces, flyers fly. Only the "owner" it reads is the soldier.
	///
	/// HOW IT IS ANCHORED. Vanilla minion code reads <c>Main.player[Projectile.owner]</c> for everything: the home position it follows (Center, direction, velocity, feet), the
	/// attack target (<c>MinionAttackTargetNPC</c>), the spacing index (<c>numMinions</c> -> <c>minionPos</c>), the damage it deals (<c>GetTotalDamage(Summon)</c> applied to
	/// <c>originalDamage</c> every tick), and the slot cap that kills the overflow. So a soldier's minion is spawned with owner 255 (<see cref="SoldierNPC.DummySlot"/>, the same
	/// owner the held-weapon chain uses and what a dedicated server's own projectiles carry) and tagged with the soldier through <see cref="SoldierProjectileTag.SoldierIndex"/>.
	/// Every <c>Projectile.Update</c> of such a minion is wrapped (On_Projectile.Update detour, try/finally):
	///  * the soldier's Dummy is put into Main.player[255] through <see cref="EnemyTargeting.Swap"/> (the swap enemies and held weapons use), positioned at the soldier, so the
	///    minion follows THAT soldier;
	///  * the Dummy's <c>MinionAttackTargetNPC</c> is the soldier's own target (which already folds in the commander's focus designation), else a whip-tagged enemy, so the
	///    minion attacks what the soldier fights; vanilla proximity picks otherwise;
	///  * the Dummy's <c>numMinions</c> / <c>slotsMinions</c> / <c>ownedProjectileCounts</c> are reset once a tick (<see cref="PrepareTick"/>) and counted from the soldier's own
	///    flock only, so each soldier's minions space out among themselves (vanilla minionPos) and never touch the commander's minion slots;
	///  * on the server / single player Main.myPlayer is 255 for the call (owner-gated vanilla code: target picking, pounces, shots and Projectile.Damage run only when
	///    owner == Main.myPlayer; a dedicated server already is 255), restored in the same finally;
	///  * maxMinions is raised for the call so vanilla's overflow-kill never fires (the soldier's upkeep owns the count).
	/// Remote clients run the same wrapper as a NON-owner (vanilla multiplayer semantics: the server's net updates drive the minion, the client just simulates between them).
	/// SLOT 255 DISCIPLINE: Swap in try, Unswap in finally, EnsureSlotRestored in the same finally, plus PreUpdateProjectiles/PostUpdateProjectiles; Main.myPlayer and
	/// maxMinions are restored in that finally too.
	///
	/// WHAT STAYS FAKE. Only vanilla minions are driven: a modded minion's own AI usually insists on its owner's ModPlayer buff and would die on the dummy. Those, the Stardust Dragon
	/// (a chain of linked segments that find each other by owner), and any type that throws while driven (see <see cref="MarkBad"/>) keep the old stand-in, <see cref="Content.Projectiles.SoldierMinion"/>.
	///
	/// CONTROLLER MINIONS. The Desert Tiger staff casts a GEM (Projectile 831); the tiger itself (833-835) is spawned by Player.UpdateStormTigerStatus from the gem count, and
	/// Abigail's Flower casts a counter (970) that spawns Abigail (963). Neither runs on the dummy, so <see cref="MaintainCompanion"/> mirrors that rule per soldier.
	/// </summary>
	public static class SoldierMinions
	{
		private const int Slot = SoldierNPC.DummySlot;
		/// <summary>An owner slot nothing real uses: other soldiers' same-type minions are parked here for the length of a group-indexed minion's Update.</summary>
		private static readonly int ParkedOwner = Main.maxPlayers - 1;

		/// <summary>The soldier whose minion is being created right now; <see cref="SoldierProjectileTag.OnSpawn"/> tags the projectile before the first spawn packet is sent.</summary>
		internal static SoldierNPC PendingBody;

		/// <summary>Vanilla minions that cannot be driven by a lone dummy: the Stardust Dragon's linked segments and the Stardust Guardian (an armor-bonus stand).</summary>
		private static readonly HashSet<int> Undriven = new()
		{
			ProjectileID.StardustDragon1, ProjectileID.StardustDragon2, ProjectileID.StardustDragon3, ProjectileID.StardustDragon4, ProjectileID.StardustGuardian,
		};

		/// <summary>Minion types whose AI sorts itself among "every projectile of my type and owner" (Projectile.AI_GetMyGroupIndexAndFillBlackList): idle slots on a circle around the owner.</summary>
		private static readonly HashSet<int> GroupIndexed = new()
		{
			ProjectileID.BatOfLight, ProjectileID.EmpressBlade, ProjectileID.BabyBird, ProjectileID.StormTigerGem, ProjectileID.AbigailCounter, ProjectileID.Smolstar,
		};

		/// <summary>Types that threw while being driven this session; their staves fall back to the stand-in.</summary>
		private static readonly HashSet<int> BadTypes = new();
		private static readonly List<Projectile> Parked = new();

		/// <summary>The real Main.myPlayer while a minion's Update has it faked to 255 (single player); children created in that window are re-owned to it.</summary>
		private static bool _faked;
		private static int _trueMyPlayer;
		internal static int TrueMyPlayer => _faked ? _trueMyPlayer : Main.myPlayer;

		internal static void Reset() { BadTypes.Clear(); Parked.Clear(); PendingBody = null; _faked = false; }

		// =====================================================================================================
		// Identity
		// =====================================================================================================
		/// <summary>A vanilla minion projectile run for a soldier: minion flag, owner 255, tagged with a soldier.</summary>
		internal static bool IsDriven(Projectile p) =>
			p.active && p.minion && p.owner == Slot && p.type < ProjectileID.Count
			&& p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0;

		/// <summary>True when <paramref name="p"/> is a driven minion belonging to the soldier at NPC index <paramref name="soldierIndex"/>.</summary>
		internal static bool OwnedBy(Projectile p, int soldierIndex) =>
			IsDriven(p) && p.GetGlobalProjectile<SoldierProjectileTag>().SoldierIndex == soldierIndex;

		/// <summary>Whether the staff's minion is run as a real vanilla projectile (vanilla type, a minion, not a known-unsupported one, not one that threw).</summary>
		public static bool CanDrive(int shoot)
		{
			if (shoot <= 0 || shoot >= ProjectileID.Count || Undriven.Contains(shoot) || BadTypes.Contains(shoot)) return false;
			Projectile sample = WeaponClassifier.ProjectileSample(shoot);
			return sample != null && sample.minion && sample.aiStyle > 0;
		}

		/// <summary>Controller minions (Desert Tiger gem, Abigail counter) count levels, not a flock.</summary>
		private static bool IsController(int shoot) => shoot == ProjectileID.StormTigerGem || shoot == ProjectileID.AbigailCounter;

		/// <summary>Whether a projectile of <paramref name="type"/> belongs to the flock of a staff whose shot is <paramref name="shoot"/> (variants of one cast, and a controller's companion).</summary>
		internal static bool InFamily(int shoot, int type)
		{
			if (type == shoot) return true;
			return shoot switch
			{
				ProjectileID.Pygmy => type >= ProjectileID.Pygmy && type <= ProjectileID.Pygmy4,
				ProjectileID.VenomSpider => type >= ProjectileID.VenomSpider && type <= ProjectileID.DangerousSpider,
				ProjectileID.OneEyedPirate => type >= ProjectileID.OneEyedPirate && type <= ProjectileID.PirateCaptain,
				ProjectileID.Retanimini => type == ProjectileID.Retanimini + 1,
				ProjectileID.StormTigerGem => type >= ProjectileID.StormTigerTier1 && type <= ProjectileID.StormTigerTier3,
				ProjectileID.AbigailCounter => type == ProjectileID.AbigailMinion,
				_ => false,
			};
		}

		/// <summary>Minions this staff may field: the soldier's capacity, with controllers allowed to stack higher (they are level markers).</summary>
		internal static int Capacity(SoldierNPC s, Item weapon)
		{
			if (!IsController(weapon.shoot)) return s.MinionCapacity(weapon);
			float cap = s.Dummy != null ? Math.Max(1, s.Dummy.maxMinions) : 1f;
			return Math.Clamp((int)Math.Floor(cap / SoldierNPC.SlotsPerMinion(weapon) + 0.001f), 1, LevyConstants.MinionControllerMaxPerSoldier);
		}

		/// <summary>The staff-shot type a minion type belongs to (an alternate Pygmy -> Pygmy, a tiger tier -> its gem...), so a failure marks the whole staff.</summary>
		private static int FamilyRoot(int type)
		{
			if (type >= ProjectileID.Pygmy && type <= ProjectileID.Pygmy4) return ProjectileID.Pygmy;
			if (type >= ProjectileID.VenomSpider && type <= ProjectileID.DangerousSpider) return ProjectileID.VenomSpider;
			if (type >= ProjectileID.OneEyedPirate && type <= ProjectileID.PirateCaptain) return ProjectileID.OneEyedPirate;
			if (type == ProjectileID.Retanimini + 1) return ProjectileID.Retanimini;
			if (type >= ProjectileID.StormTigerTier1 && type <= ProjectileID.StormTigerTier3) return ProjectileID.StormTigerGem;
			if (type == ProjectileID.AbigailMinion) return ProjectileID.AbigailCounter;
			return type;
		}

		/// <summary>Remembers that this type misbehaved under the dummy; its staff keeps the stand-in from now on.</summary>
		private static void MarkBad(Projectile p, Exception e)
		{
			if (BadTypes.Add(FamilyRoot(p.type))) ModContent.GetInstance<Levy>()?.Logger.Warn("Levy: real minion type " + p.type + " threw while driven for a soldier; its staff falls back to the stand-in minion: " + e);
		}

		/// <summary>Removes every real minion (and companion) this soldier has out. Server / single player.</summary>
		internal static void KillAll(SoldierNPC s)
		{
			int me = s.NPC.whoAmI;
			for (int i = 0; i < Main.maxProjectiles; i++)
				if (OwnedBy(Main.projectile[i], me)) Main.projectile[i].Kill();
		}

		// =====================================================================================================
		// Spawning (server / single player)
		// =====================================================================================================
		/// <summary>The vanilla cast of one staff: the minion(s) it makes, with the starting ai[1] / localAI[0] Player.ItemCheck_Shoot / SpawnMinionOnCursor give them. Returns false when nothing was created.</summary>
		internal static bool Summon(SoldierNPC s, Item weapon, int damage)
		{
			int shoot = weapon.shoot;
			int type = shoot;
			float ai1 = 0f, localAi0 = 0f;
			bool pair = false;
			switch (weapon.type)
			{
				case ItemID.PygmyStaff: type = Main.rand.Next(ProjectileID.Pygmy, ProjectileID.Pygmy4 + 1); ai1 = LevyConstants.PygmySpawnAi1; localAi0 = LevyConstants.PygmySpawnLocalAi0; break;
				case ItemID.SpiderStaff:
				case ItemID.PirateStaff: type = shoot + Main.rand.Next(3); break;
				case ItemID.OpticStaff: ai1 = 1f; pair = true; break;
				case ItemID.HornetStaff:
				case ItemID.ImpStaff:
				case ItemID.TempestStaff:
				case ItemID.XenoStaff:
				case ItemID.StardustCellStaff: ai1 = 1f; break;
			}

			float scatter = Main.rand.NextFloat(-1f, 1f) * LevyConstants.MinionSpawnScatterPx;
			Vector2 at = s.NPC.Center + new Vector2(scatter, 0f);
			if (Collision.SolidCollision(at - LevyConstants.MinionSpawnProbe * 0.5f, (int)LevyConstants.MinionSpawnProbe.X, (int)LevyConstants.MinionSpawnProbe.Y)) at = s.NPC.Center; // scatter must not drop a walker inside a wall
			Projectile first = NewMinion(s, at, type, damage, weapon.knockBack, ai1);
			if (first == null) return false;
			if (localAi0 > 0f) first.localAI[0] = localAi0;
			if (pair) NewMinion(s, at + new Vector2(LevyConstants.MinionSpawnScatterPx * 0.5f, 0f), shoot + 1, damage, weapon.knockBack, ai1); // the Optic Staff always casts both twins
			return true;
		}

		/// <summary>
		/// Creates one minion owned by 255 and tagged with the soldier. NewProjectile only assigns ai[] and syncs when Owner == Main.myPlayer, so the machine claims 255 for the call
		/// (restored in the finally); the tag is applied in OnSpawn via <see cref="PendingBody"/> so the very first spawn packet already names the soldier.
		/// </summary>
		private static Projectile NewMinion(SoldierNPC s, Vector2 pos, int type, int damage, float knockback, float ai1)
		{
			int savedMyPlayer = Main.myPlayer;
			PendingBody = s;
			try
			{
				if (savedMyPlayer != Slot) { _trueMyPlayer = savedMyPlayer; _faked = true; }
				Main.myPlayer = Slot;
				int idx = Projectile.NewProjectile(s.NPC.GetSource_FromAI(), pos, Vector2.Zero, type, Math.Max(0, damage), knockback, Slot, 0f, ai1);
				if (idx < 0 || idx >= Main.maxProjectiles) return null;
				Projectile p = Main.projectile[idx];
				if (!p.active || p.type != type) return null;
				p.npcProj = true; // the commander's on-hit effects and item drops never run for a soldier's minion
				p.noDropItem = true;
				if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) tag.SoldierIndex = s.NPC.whoAmI;
				p.netUpdate = true;
				return p;
			}
			finally
			{
				Main.myPlayer = savedMyPlayer;
				_faked = false;
				PendingBody = null;
			}
		}

		/// <summary>
		/// Vanilla re-derives a minion's damage every tick as <c>owner.GetTotalDamage(Summon).ApplyTo(originalDamage)</c>; the owner here is the dummy, so its own summon ratio is divided
		/// back out and the soldier's weapon damage x grade (<paramref name="desiredDamage"/>) is what the minion deals. Also keeps the staff's knockback.
		/// </summary>
		internal static void Retune(SoldierNPC s, Projectile p, Item weapon, int desiredDamage)
		{
			p.knockBack = weapon.knockBack;
			DamageClass cls = p.DamageType ?? DamageClass.Summon;
			float ratio = 1f;
			if (s.Dummy != null) ratio = s.Dummy.GetTotalDamage(cls).ApplyTo(100f) / 100f;
			ratio = Math.Clamp(ratio, LevyConstants.MinionDamageRatioMin, LevyConstants.MinionDamageRatioMax);
			int original = Math.Max(1, (int)Math.Round(desiredDamage / ratio));
			if (p.originalDamage != original) p.originalDamage = original;
		}

		/// <summary>
		/// Controller staves: the Desert Tiger's rank follows the gem count (vanilla GetDesiredStormTigerMinionRank: more than 0 / 3 / 6 gems), Abigail exists while a counter does.
		/// Keeps exactly the right companion for this soldier, killing a stale tier or a duplicate, and creates it when missing (vanilla: UpdateStormTigerStatus / UpdateAbigailStatus).
		/// <paramref name="flock"/> is the soldier's real minion indices.
		/// </summary>
		internal static void MaintainCompanion(SoldierNPC s, Item weapon, List<int> flock)
		{
			int shoot = weapon.shoot;
			bool tiger = shoot == ProjectileID.StormTigerGem;
			if (!tiger && shoot != ProjectileID.AbigailCounter) return;
			int controllers = 0;
			foreach (int i in flock) if (Main.projectile[i].active && Main.projectile[i].type == shoot) controllers++;
			int want = -1;
			if (tiger) want = controllers > LevyConstants.TigerTier3GemsOver ? ProjectileID.StormTigerTier3 : controllers > LevyConstants.TigerTier2GemsOver ? ProjectileID.StormTigerTier2 : controllers > 0 ? ProjectileID.StormTigerTier1 : -1;
			else if (controllers > 0) want = ProjectileID.AbigailMinion;

			bool have = false;
			foreach (int i in flock)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || p.type == shoot) continue;
				if (p.type != want || have) p.Kill();
				else have = true;
			}
			if (want <= 0 || have) return;
			Projectile c = NewMinion(s, s.NPC.Center, want, 0, 0f, tiger ? 1f : 0f);
			if (c != null && tiger) c.localAI[0] = LevyConstants.TigerSpawnLocalAi0;
		}

		// =====================================================================================================
		// Per-tick preparation: what the dummy looks like to its own minions
		// =====================================================================================================
		/// <summary>
		/// Once a game tick, before any projectile updates: every soldier with driven minions gets its dummy's minion counters zeroed and recounted from ITS flock only
		/// (so minionPos is a per-soldier spacing index and the commander's slots are never touched), its Desert Tiger / Abigail level data refreshed, its animation clock set and
		/// (server / single player) its attack target chosen.
		/// </summary>
		internal static void PrepareTick()
		{
			uint now = Main.GameUpdateCount;
			bool authority = LevyUtil.IsServerOrSingle;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!IsDriven(p)) continue;
				SoldierNPC s = MiscProjSwap.BodyOf(p);
				if (s == null) continue;
				Player d = s.Dummy;
				if (s.MinionPrepStamp != now)
				{
					s.MinionPrepStamp = now;
					d.numMinions = 0;
					d.slotsMinions = 0f;
					Array.Clear(d.ownedProjectileCounts, 0, d.ownedProjectileCounts.Length);
					d.highestStormTigerGemOriginalDamage = 0;
					d.highestAbigailCounterOriginalDamage = 0;
					d.miscCounter = (int)(now % 300u);
					d.MinionAttackTargetNPC = authority ? AttackTarget(s) : -1;
				}
				if (p.type < d.ownedProjectileCounts.Length) d.ownedProjectileCounts[p.type]++;
				if (p.type == ProjectileID.StormTigerGem) d.highestStormTigerGemOriginalDamage = Math.Max(d.highestStormTigerGemOriginalDamage, p.originalDamage);
				else if (p.type == ProjectileID.AbigailCounter) d.highestAbigailCounterOriginalDamage = Math.Max(d.highestAbigailCounterOriginalDamage, p.originalDamage);
			}
		}

		/// <summary>The enemy this soldier's minions are told to attack: the soldier's own target (incl. the commander's focus) when it is a live hostile inside the leash, else the nearest whip-tagged enemy in the leash, else -1 (vanilla proximity).</summary>
		private static int AttackTarget(SoldierNPC s)
		{
			if (s.Record == null || s.Record.HoldFire) return -1;
			float leashSq = LevyConstants.MinionLeashPx * LevyConstants.MinionLeashPx;
			Vector2 home = s.NPC.Center;
			int idx = s.TargetIndex;
			if (idx >= 0 && idx < Main.maxNPCs)
			{
				NPC t = Main.npc[idx];
				if (t.active && t.life > 0 && LevyUtil.IsHostileTarget(t) && !SoldierTargeting.IsNeverTarget(t) && Vector2.DistanceSquared(t.Center, home) <= leashSq) return idx;
			}
			int best = -1; float bestSq = leashSq;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.life <= 0 || !LevyNpcData.IsNpcTagged(n) || !LevyUtil.IsHostileTarget(n) || SoldierTargeting.IsNeverTarget(n)) continue;
				float dSq = Vector2.DistanceSquared(n.Center, home);
				if (dSq < bestSq) { bestSq = dSq; best = i; }
			}
			return best;
		}

		// =====================================================================================================
		// The Update wrapper
		// =====================================================================================================
		/// <summary>True when a projectile is a soldier's minion or one of its shots and the soldier is told to hold fire: it deals no damage.</summary>
		internal static bool SuppressedByHoldFire(Projectile p, int soldierIndex)
		{
			bool minionShot = p.minion || (p.type > 0 && p.type < ProjectileID.Sets.MinionShot.Length && ProjectileID.Sets.MinionShot[p.type]);
			if (!minionShot) return false;
			SoldierNPC s = EnemyTargeting.LiveBody(soldierIndex);
			return s != null && s.Record.HoldFire;
		}

		/// <summary>A projectile created inside a driven minion's Update (a stinger, a spear, a fireball) is owned by 255 there; in single player it is handed to the real owner so Projectile.Damage processes it.</summary>
		internal static void ReownChild(Projectile parent, Projectile child)
		{
			if (Main.netMode != NetmodeID.SinglePlayer || parent.owner != Slot || !parent.minion || child.owner != Slot) return;
			child.owner = TrueMyPlayer;
		}

		internal static void RunUpdate(On_Projectile.orig_Update orig, Projectile p, int i)
		{
			SoldierNPC body = MiscProjSwap.BodyOf(p);
			bool authority = LevyUtil.IsServerOrSingle;
			if (body == null)
			{
				// soldier dead / dismissed / slot reused: the flock ends. A client that has not met the body yet just waits: no AI against the null player.
				if (authority) p.Kill();
				return;
			}

			Player d = body.Dummy;
			int savedMyPlayer = Main.myPlayer, savedMax = d.maxMinions;
			bool swapped = false, parked = false;
			try
			{
				if (p.timeLeft < LevyConstants.MinionLifeTicks / 2) p.timeLeft = LevyConstants.MinionLifeTicks; // vanilla keeps a minion alive while its buff flag is up; the soldier's upkeep is the buff here
				EnemyTargeting.Swap(body); // dummy -> Main.player[255], at the soldier
				swapped = true;
				d.maxMinions = LevyConstants.MinionDriverMaxMinions;
				if (authority && savedMyPlayer != Slot) { _trueMyPlayer = savedMyPlayer; _faked = true; Main.myPlayer = Slot; }
				if (GroupIndexed.Contains(p.type)) { ParkOtherFlocks(p, body.NPC.whoAmI); parked = true; }
				orig(p, i);
			}
			catch (Exception e)
			{
				// never take the game down for one odd minion: log it once, drop this one, and let its staff use the stand-in from now on
				MarkBad(p, e);
				try { if (authority && p.active) p.Kill(); } catch (Exception) { }
				p.active = false;
			}
			finally
			{
				if (parked) UnparkOtherFlocks();
				Main.myPlayer = savedMyPlayer;
				_faked = false;
				d.maxMinions = savedMax;
				if (swapped) EnemyTargeting.Unswap();
				EnemyTargeting.EnsureSlotRestored(); // never leave a dummy in Main.player[255]
			}

			// keep remote screens honest: the server re-sends every real minion now and then (vanilla only sends when the AI flags a change)
			if (Main.netMode == NetmodeID.Server && p.active && ((uint)p.whoAmI + Main.GameUpdateCount) % (uint)LevyConstants.MinionNetRefreshTicks == 0)
				NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, p.whoAmI);
		}

		/// <summary>Other soldiers' minions of the same type sit on a throwaway owner for the length of the call, so a group-indexed AI sorts itself among ITS soldier's flock only.</summary>
		private static void ParkOtherFlocks(Projectile p, int soldierIndex)
		{
			Parked.Clear();
			for (int j = 0; j < Main.maxProjectiles; j++)
			{
				Projectile q = Main.projectile[j];
				if (!q.active || j == p.whoAmI || q.type != p.type || q.owner != Slot) continue;
				if (q.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex == soldierIndex) continue;
				q.owner = ParkedOwner;
				Parked.Add(q);
			}
		}

		private static void UnparkOtherFlocks()
		{
			foreach (Projectile q in Parked) q.owner = Slot;
			Parked.Clear();
		}
	}

	/// <summary>Installs the Projectile.Update wrapper for real minions and the per-tick dummy preparation; keeps slot 255 clean around the projectile phase.</summary>
	public class SoldierMinionSystem : ModSystem
	{
		public override void Load() => On_Projectile.Update += UpdateDetour;

		public override void Unload()
		{
			On_Projectile.Update -= UpdateDetour;
			SoldierMinions.Reset();
			EnemyTargeting.EnsureSlotRestored();
		}

		public override void OnWorldLoad() => SoldierMinions.Reset();
		public override void OnWorldUnload() { SoldierMinions.Reset(); EnemyTargeting.EnsureSlotRestored(); }

		public override void PreUpdateProjectiles()
		{
			EnemyTargeting.EnsureSlotRestored();
			SoldierMinions.PrepareTick();
		}

		public override void PostUpdateProjectiles() => EnemyTargeting.EnsureSlotRestored();

		private static void UpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			if (self.minion && self.owner == SoldierNPC.DummySlot && SoldierMinions.IsDriven(self)) SoldierMinions.RunUpdate(orig, self, i);
			else orig(self, i);
		}
	}
}
