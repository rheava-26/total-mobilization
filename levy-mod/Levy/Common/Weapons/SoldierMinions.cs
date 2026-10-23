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
	/// WHAT STAYS FAKE. Only vanilla minions are driven: a modded minion's own AI usually insists on its owner's ModPlayer buff and would die on the dummy. Those, the Stardust Guardian
	/// (an armor-set stand, not a minion), and any type that throws while driven (see <see cref="MarkBad"/>) keep the old stand-in, <see cref="Content.Projectiles.SoldierMinion"/>.
	///
	/// THE STARDUST DRAGON. Head 625, two bodies 626/627 per cast, tail 628: a chain whose segments find the one ahead through <c>Projectile.GetByUUID(owner, ai[0])</c>, and vanilla refuses that
	/// lookup for owner 255 (<c>owner >= 255</c> returns -1), so a segment owned by 255 would never follow anything. The dragon's segments are therefore owned by their OWN slot,
	/// <see cref="DragonOwner"/> (a throwaway slot below 255), for their whole life (so spawn packets, kill packets and every vanilla owner scan stay consistent on remote screens). Each segment's
	/// Update aliases the soldier's Dummy into that slot (<see cref="AliasDummy"/>) next to the usual slot-255 swap, fakes Main.myPlayer to that slot, and parks every OTHER soldier's segments on
	/// <see cref="DragonParkOwner"/>, so the owner scans in Projectile.Damage (FindStardustDragonHeadOfOwner) and in the AI's relink fallback only ever see this soldier's chain.
	/// The chain is spawned exactly like Player.ItemCheck_Shoot does it (see <see cref="SummonDragon"/>): head, then each segment's ai[0] = the previous segment, localAI[1] = the next.
	///
	/// CONTROLLER MINIONS. The Desert Tiger staff casts a GEM (Projectile 831); the tiger itself (833-835) is spawned by Player.UpdateStormTigerStatus from the gem count, and
	/// Abigail's Flower casts a counter (970) that spawns Abigail (963). Neither runs on the dummy, so <see cref="MaintainCompanion"/> mirrors that rule per soldier.
	/// </summary>
	public static class SoldierMinions
	{
		private const int Slot = SoldierNPC.DummySlot;
		/// <summary>An owner slot nothing real uses: other soldiers' same-type minions are parked here for the length of a group-indexed minion's Update.</summary>
		private static readonly int ParkedOwner = Main.maxPlayers - 1;
		/// <summary>The owner slot of every Stardust Dragon segment of every soldier (vanilla's UUID lookup refuses owner 255, so the dragon cannot live on the shared minion owner). Never a real player in practice.</summary>
		private static readonly int DragonOwner = Main.maxPlayers - 2;
		/// <summary>Where the OTHER soldiers' dragon segments sit for the length of one segment's Update, so owner scans see only that soldier's chain.</summary>
		private static readonly int DragonParkOwner = Main.maxPlayers - 3;

		/// <summary>The soldier whose minion is being created right now; <see cref="SoldierProjectileTag.OnSpawn"/> tags the projectile before the first spawn packet is sent.</summary>
		internal static SoldierNPC PendingBody;

		/// <summary>Vanilla minions that cannot be driven by a lone dummy: the Stardust Guardian (an armor-bonus stand, not a minion).</summary>
		private static readonly HashSet<int> Undriven = new()
		{
			ProjectileID.StardustGuardian,
		};

		/// <summary>Minion types whose AI sorts itself among "every projectile of my type and owner" (Projectile.AI_GetMyGroupIndexAndFillBlackList): idle slots on a circle around the owner.</summary>
		private static readonly HashSet<int> GroupIndexed = new()
		{
			ProjectileID.BatOfLight, ProjectileID.EmpressBlade, ProjectileID.BabyBird, ProjectileID.StormTigerGem, ProjectileID.AbigailCounter, ProjectileID.Smolstar,
		};

		/// <summary>Types that threw while being driven this session; their staves fall back to the stand-in.</summary>
		private static readonly HashSet<int> BadTypes = new();
		private static readonly List<Projectile> Parked = new();
		private static readonly List<Projectile> ParkedDragons = new();

		// Per-tick index of the driven minions that need their neighbours parked while they update (group-indexed minion types, Stardust Dragon segments), built once by PrepareTick from the
		// pass it already makes over the projectile array. Parking used to walk all 1000 projectile slots (and look up each one's tag) for EVERY such minion on EVERY tick; it now walks
		// only these lists, with the owning soldier read from a parallel array instead of a per-entry lookup.
		private static readonly List<Projectile> GroupMinionsNow = new();
		private static readonly List<int> GroupMinionOwners = new();
		private static readonly List<Projectile> DragonSegmentsNow = new();
		private static readonly List<int> DragonSegmentOwners = new();

		/// <summary>The slot-aliasing bookkeeping of <see cref="AliasDummy"/>: the real Player that stood in the dragon's owner slot while the soldier's dummy took its place.</summary>
		private static Player _aliasBackup;
		private static int _aliasSlot = -1;
		private static bool _aliased;

		/// <summary>The real Main.myPlayer while a minion's Update has it faked to 255 (single player); children created in that window are re-owned to it.</summary>
		private static bool _faked;
		private static int _trueMyPlayer;
		internal static int TrueMyPlayer => _faked ? _trueMyPlayer : Main.myPlayer;

		internal static void Reset()
		{
			BadTypes.Clear(); Parked.Clear(); ParkedDragons.Clear();
			GroupMinionsNow.Clear(); GroupMinionOwners.Clear(); DragonSegmentsNow.Clear(); DragonSegmentOwners.Clear();
			PendingBody = null; _faked = false; EnsureAliasRestored();
		}

		// =====================================================================================================
		// Identity
		// =====================================================================================================
		/// <summary>A vanilla minion projectile run for a soldier: minion flag, owner 255, tagged with a soldier.</summary>
		internal static bool IsDriven(Projectile p) =>
			p.active && p.minion && p.type < ProjectileID.Count && (p.owner == Slot || (p.owner == DragonOwner && IsDragonType(p.type)))
			&& p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0;

		/// <summary>The four segments of the Stardust Dragon: head 625, bodies 626 / 627, tail 628.</summary>
		internal static bool IsDragonType(int type) => type >= ProjectileID.StardustDragon1 && type <= ProjectileID.StardustDragon4;

		/// <summary>The owner slot a driven minion of <paramref name="type"/> lives on: 255, except the Stardust Dragon's own slot.</summary>
		private static int OwnerSlotFor(int type) => IsDragonType(type) ? DragonOwner : Slot;

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
				ProjectileID.StardustDragon1 => IsDragonType(type),
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
			if (IsDragonType(type)) return ProjectileID.StardustDragon1;
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
				if (OwnedBy(Main.projectile[i], me)) Dismiss(Main.projectile[i]);
		}

		/// <summary>
		/// Ends a real minion. A Stardust Dragon segment is owned by a slot that is not this machine's Main.myPlayer, and vanilla broadcasts a kill only for the machine's own projectiles,
		/// so a dedicated server claims the segment's slot for the Kill (restored in the finally) or remote screens would keep the segment forever.
		/// </summary>
		internal static void Dismiss(Projectile p)
		{
			if (!p.active) return;
			if (Main.netMode != NetmodeID.Server || !IsDragonType(p.type) || p.owner == Main.myPlayer) { p.Kill(); return; }
			int saved = Main.myPlayer;
			try { Main.myPlayer = p.owner; p.Kill(); }
			finally { Main.myPlayer = saved; }
		}

		// =====================================================================================================
		// Spawning (server / single player)
		// =====================================================================================================
		/// <summary>The vanilla cast of one staff: the minion(s) it makes, with the starting ai[1] / localAI[0] Player.ItemCheck_Shoot / SpawnMinionOnCursor give them. Returns false when nothing was created.</summary>
		internal static bool Summon(SoldierNPC s, Item weapon, int damage)
		{
			int shoot = weapon.shoot;
			if (shoot == ProjectileID.StardustDragon1) return SummonDragon(s, weapon, damage);
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
		/// Creates one minion owned by 255 (a Stardust Dragon segment: by <see cref="DragonOwner"/>) and tagged with the soldier. NewProjectile only assigns ai[] and syncs when Owner == Main.myPlayer,
		/// so the machine claims that slot for the call (restored in the finally); the tag is applied in OnSpawn via <see cref="PendingBody"/> so the very first spawn packet already names the soldier.
		/// <paramref name="ai0"/> is only meaningful for a dragon segment: the index of the segment ahead of it (vanilla turns it into that segment's UUID).
		/// </summary>
		private static Projectile NewMinion(SoldierNPC s, Vector2 pos, int type, int damage, float knockback, float ai1, float ai0 = 0f)
		{
			int savedMyPlayer = Main.myPlayer, ownerSlot = OwnerSlotFor(type);
			PendingBody = s;
			try
			{
				if (savedMyPlayer != ownerSlot) { _trueMyPlayer = savedMyPlayer; _faked = true; }
				Main.myPlayer = ownerSlot;
				int idx = Projectile.NewProjectile(s.NPC.GetSource_FromAI(), pos, Vector2.Zero, type, Math.Max(0, damage), knockback, ownerSlot, ai0, ai1);
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

		// =====================================================================================================
		// The Stardust Dragon: a chain of linked segments
		// =====================================================================================================
		/// <summary>
		/// One cast of the Stardust Dragon Staff, mirroring Player.ItemCheck_Shoot (item 3531). No dragon yet: head 625, two bodies 626 / 627 and tail 628 are created in order, each with ai[0] = the
		/// index of the segment ahead of it (NewProjectile turns that into the UUID the AI follows) and localAI[1] = the index of the segment behind it. A dragon already out grows by two bodies
		/// inserted just ahead of the tail. A half-built chain (a segment missing) is never extended: <see cref="DragonIntact"/> tears it down first, so this only ever sees none or a whole one.
		/// </summary>
		private static bool SummonDragon(SoldierNPC s, Item weapon, int damage)
		{
			int me = s.NPC.whoAmI, head = -1, tail = -1;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile q = Main.projectile[i];
				if (!OwnedBy(q, me)) continue;
				if (q.type == ProjectileID.StardustDragon1) head = i;
				else if (q.type == ProjectileID.StardustDragon4) tail = i;
			}
			Vector2 at = s.NPC.Center;
			float kb = weapon.knockBack;
			if (head < 0 && tail < 0)
			{
				Projectile h = NewMinion(s, at, ProjectileID.StardustDragon1, damage, kb, 0f);
				if (h == null) return false;
				Projectile b1 = NewMinion(s, at, ProjectileID.StardustDragon2, damage, kb, 0f, h.whoAmI);
				Projectile b2 = b1 == null ? null : NewMinion(s, at, ProjectileID.StardustDragon3, damage, kb, 0f, b1.whoAmI);
				Projectile t = b2 == null ? null : NewMinion(s, at, ProjectileID.StardustDragon4, damage, kb, 0f, b2.whoAmI);
				if (t == null)
				{
					// a segment could not be created (projectile table full): no half dragon is left behind
					foreach (Projectile q in new[] { h, b1, b2 }) if (q != null) Dismiss(q);
					return false;
				}
				b1.localAI[1] = b2.whoAmI;
				b2.localAI[1] = t.whoAmI;
				foreach (Projectile q in new[] { h, b1, b2, t }) q.netUpdate = true;
				return true;
			}
			if (head < 0 || tail < 0) return false;

			Projectile tl = Main.projectile[tail];
			int ahead = Projectile.GetByUUID(DragonOwner, (int)tl.ai[0]);
			if (ahead < 0) return false;
			Projectile a = NewMinion(s, at, ProjectileID.StardustDragon2, damage, kb, 0f, ahead);
			Projectile b = a == null ? null : NewMinion(s, at, ProjectileID.StardustDragon3, damage, kb, 0f, a.whoAmI);
			if (b == null)
			{
				if (a != null) Dismiss(a);
				return false;
			}
			a.localAI[1] = b.whoAmI;
			b.localAI[1] = tail;
			a.ai[1] = b.ai[1] = tl.ai[1] = 1f; // the AI clears it next tick and flags a net update (vanilla does the same after splicing)
			tl.ai[0] = b.whoAmI; // the tail now follows the new last body
			a.netUpdate = b.netUpdate = tl.netUpdate = true;
			return true;
		}

		/// <summary>
		/// True when the soldier's dragon segments (<paramref name="flock"/>, indices of its real minions) form exactly one chain: one head, one tail, and walking from the tail along each segment's
		/// link reaches the head through every body once. An empty flock is intact. Anything else (a segment killed by something else, a stale link) is rebuilt from scratch by the caller.
		/// </summary>
		internal static bool DragonIntact(SoldierNPC s, List<int> flock)
		{
			int me = s.NPC.whoAmI, heads = 0, tails = 0, total = 0, head = -1, tail = -1;
			foreach (int i in flock)
			{
				Projectile q = Main.projectile[i];
				if (!q.active || !IsDragonType(q.type)) continue;
				total++;
				if (q.type == ProjectileID.StardustDragon1) { heads++; head = i; }
				else if (q.type == ProjectileID.StardustDragon4) { tails++; tail = i; }
			}
			if (total == 0) return true;
			if (heads != 1 || tails != 1) return false;
			int cur = tail, steps = 0;
			while (cur != head)
			{
				if (++steps >= total) return false; // a loop, or a chain longer than the flock
				Projectile q = Main.projectile[cur];
				int ahead = Projectile.GetByUUID(DragonOwner, (int)q.ai[0]);
				if (ahead < 0 || ahead == cur) return false;
				Projectile pa = Main.projectile[ahead];
				if (!OwnedBy(pa, me) || !IsDragonType(pa.type) || pa.type == ProjectileID.StardustDragon4) return false;
				cur = ahead;
			}
			return steps == total - 1;
		}

		/// <summary>
		/// Shortens an over-long dragon (the soldier's capacity dropped) by unlinking the body just ahead of the tail, as vanilla's overflow branch does: the tail is re-pointed at the segment
		/// that body followed, then the body is removed. Never goes below the two bodies of one cast (a tail that follows the head directly kills the whole dragon in vanilla).
		/// Returns the slot total after trimming; the removed segments leave <paramref name="flock"/>.
		/// </summary>
		internal static float TrimDragon(List<int> flock, float have, float want)
		{
			int bodies = 0, tailIdx = -1;
			foreach (int i in flock)
			{
				Projectile q = Main.projectile[i];
				if (!q.active) continue;
				if (q.type == ProjectileID.StardustDragon2 || q.type == ProjectileID.StardustDragon3) bodies++;
				else if (q.type == ProjectileID.StardustDragon4) tailIdx = i;
			}
			while (have > want + LevyConstants.MinionSlotEpsilon && bodies > 2 && tailIdx >= 0)
			{
				Projectile tail = Main.projectile[tailIdx];
				int bi = Projectile.GetByUUID(DragonOwner, (int)tail.ai[0]);
				if (bi < 0) break;
				Projectile body = Main.projectile[bi];
				if (body.type != ProjectileID.StardustDragon2 && body.type != ProjectileID.StardustDragon3) break;
				tail.ai[0] = body.ai[0];
				tail.ai[1] = 1f;
				tail.netUpdate = true;
				int ahead = Projectile.GetByUUID(DragonOwner, (int)body.ai[0]);
				if (ahead >= 0 && Main.projectile[ahead].type != ProjectileID.StardustDragon1) Main.projectile[ahead].localAI[1] = tailIdx;
				have -= body.minionSlots;
				bodies--;
				Dismiss(body);
				flock.Remove(bi);
			}
			return have;
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
			GroupMinionsNow.Clear(); GroupMinionOwners.Clear(); DragonSegmentsNow.Clear(); DragonSegmentOwners.Clear();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!IsDriven(p)) continue;
				// index the minions whose Update parks their neighbours (see ParkOtherFlocks / ParkOtherDragons): one entry per driven minion, with its soldier read once, here
				bool dragon = IsDragonType(p.type);
				if (dragon || GroupIndexed.Contains(p.type))
				{
					int owner = p.GetGlobalProjectile<SoldierProjectileTag>().SoldierIndex;
					if (dragon) { DragonSegmentsNow.Add(p); DragonSegmentOwners.Add(owner); }
					else { GroupMinionsNow.Add(p); GroupMinionOwners.Add(owner); }
				}
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
				if (authority) Dismiss(p);
				return;
			}

			Player d = body.Dummy;
			int savedMyPlayer = Main.myPlayer, savedMax = d.maxMinions, ownerSlot = p.owner;
			bool swapped = false, parked = false, parkedDragons = false;
			try
			{
				if (p.timeLeft < LevyConstants.MinionLifeTicks / 2) p.timeLeft = LevyConstants.MinionLifeTicks; // vanilla keeps a minion alive while its buff flag is up; the soldier's upkeep is the buff here
				EnemyTargeting.Swap(body); // dummy -> Main.player[255], at the soldier
				swapped = true;
				if (ownerSlot != Slot) AliasDummy(ownerSlot, d); // a dragon segment reads Main.player[its own owner slot]: the same dummy stands there too
				d.maxMinions = LevyConstants.MinionDriverMaxMinions;
				if (authority && savedMyPlayer != ownerSlot) { _trueMyPlayer = savedMyPlayer; _faked = true; Main.myPlayer = ownerSlot; }
				if (GroupIndexed.Contains(p.type)) { parked = true; ParkOtherFlocks(p, body.NPC.whoAmI); } // flag first: a throw halfway still restores what was moved
				if (IsDragonType(p.type)) { parkedDragons = true; ParkOtherDragons(p, body.NPC.whoAmI); }
				orig(p, i);
			}
			catch (Exception e)
			{
				// never take the game down for one odd minion: log it once, drop this one, and let its staff use the stand-in from now on
				MarkBad(p, e);
				try { if (authority && p.active) Dismiss(p); } catch (Exception) { }
				p.active = false;
			}
			finally
			{
				if (parked) UnparkOtherFlocks();
				if (parkedDragons) UnparkOtherDragons();
				Main.myPlayer = savedMyPlayer;
				_faked = false;
				d.maxMinions = savedMax;
				if (swapped) EnemyTargeting.Unswap();
				EnsureAliasRestored(); // never leave a dummy in the dragon's owner slot
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
			// walks this tick's index of group-indexed minions (PrepareTick), not the whole projectile array: cost per call is the number of such minions, not 1000 slots
			for (int k = 0; k < GroupMinionsNow.Count; k++)
			{
				Projectile q = GroupMinionsNow[k];
				if (!q.active || q.whoAmI == p.whoAmI || q.type != p.type || q.owner != Slot) continue;
				if (GroupMinionOwners[k] == soldierIndex) continue;
				q.owner = ParkedOwner;
				Parked.Add(q);
			}
		}

		private static void UnparkOtherFlocks()
		{
			foreach (Projectile q in Parked) q.owner = Slot;
			Parked.Clear();
		}

		/// <summary>
		/// Every OTHER soldier's dragon segments sit on <see cref="DragonParkOwner"/> for the length of this segment's Update. Vanilla scans by owner when a dragon segment hits something
		/// (Projectile.Damage borrows the first head of the owner for the shared hit cooldowns) and when a link breaks (the AI's relink fallback), so without this one soldier's dragon could
		/// borrow another's head. The direct UUID link cannot cross soldiers: UUIDs are projectile indices, unique per slot.
		/// </summary>
		private static void ParkOtherDragons(Projectile p, int soldierIndex)
		{
			ParkedDragons.Clear();
			for (int k = 0; k < DragonSegmentsNow.Count; k++)
			{
				Projectile q = DragonSegmentsNow[k];
				if (!q.active || q.whoAmI == p.whoAmI || q.owner != DragonOwner || !IsDragonType(q.type)) continue;
				if (DragonSegmentOwners[k] == soldierIndex) continue;
				q.owner = DragonParkOwner;
				ParkedDragons.Add(q);
			}
		}

		private static void UnparkOtherDragons()
		{
			foreach (Projectile q in ParkedDragons) q.owner = DragonOwner;
			ParkedDragons.Clear();
		}

		/// <summary>Puts the soldier's dummy into <c>Main.player[slot]</c> (a dragon segment's owner slot), remembering the Player that stood there. Paired with <see cref="EnsureAliasRestored"/>.</summary>
		private static void AliasDummy(int slot, Player dummy)
		{
			if (_aliased) EnsureAliasRestored();
			_aliasBackup = Main.player[slot];
			_aliasSlot = slot;
			Main.player[slot] = dummy;
			_aliased = true;
		}

		/// <summary>Gives the dragon's owner slot its own Player back. Idempotent; called in every finally and from the system's per-tick safety net.</summary>
		internal static void EnsureAliasRestored()
		{
			if (!_aliased) return;
			if (_aliasSlot >= 0 && _aliasSlot < Main.player.Length) Main.player[_aliasSlot] = _aliasBackup ?? new Player();
			_aliasBackup = null;
			_aliasSlot = -1;
			_aliased = false;
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
			SoldierMinions.EnsureAliasRestored();
			SoldierMinions.PrepareTick();
		}

		public override void PostUpdateProjectiles()
		{
			SoldierMinions.EnsureAliasRestored();
			EnemyTargeting.EnsureSlotRestored();
		}

		private static void UpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			HangWatch.Proj(self); // diagnostic breadcrumb (see HangWatch): which projectile's Update started last
			if (self.minion && SoldierMinions.IsDriven(self)) SoldierMinions.RunUpdate(orig, self, i);
			else orig(self, i);
		}
	}
}
