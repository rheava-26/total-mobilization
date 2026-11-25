using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
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
	/// THE KILL CASCADE. A minion's hit ends in NPC.StrikeNPC (the only strike call in Projectile.cs), and a lethal one runs the whole death cascade (HitEffect, NPC.checkDead -> NPCLoot, every
	/// mod's OnKill, event and boss-log bookkeeping) synchronously inside it. That has to happen on the real machine, exactly as for a normal soldier shot (owned by <see cref="TrueMyPlayer"/>): the
	/// real Main.myPlayer and the real null player in slot 255, never a stand-in Player there (with the dummy in the slot that cascade hung the game; held-weapon beams got the same fix in
	/// HeldWeapons.RunDamage). The projectile cannot simply be handed to the real player for its Damage call the way a beam is: vanilla reads the owner's slot for the Desert Tiger / Abigail level
	/// terms of the damage (the dummy's ownedProjectileCounts, set by <see cref="PrepareTick"/>), for the Stardust Dragon's shared hit cooldown (an owner scan for the head) and for the per-owner
	/// NPC hit immunity (npc.immune[owner]), which would also be shared with every other soldier shot. So the projectile keeps its owner and only the cascade moves: <see cref="RunDamage"/> arms the
	/// minion's one Projectile.Damage call and <see cref="RunStrike"/> lifts the Update's whole context (dummy out of slot 255 and out of the dragon's owner slot, Main.myPlayer back to the real
	/// one) for exactly the length of the NPC.StrikeNPC inside it, then puts it back in a finally. The damage number, the hit rate and everything the hit does before and after the strike still
	/// run against the dummy, unchanged. Server / single player only.
	///
	/// MODDED MINIONS run the same way: their staff's buff is applied to the dummy each tick (<see cref="ApplyModdedBuff"/>) so the ModPlayer flag their AI polls stays up, and the dummy already carries every mod's ModPlayer.
	/// WHAT STAYS FAKE. The Stardust Guardian (an armor-set stand, not a minion), a modded minion that throws while driven or dies right after every cast (see <see cref="MarkBad"/> / <see cref="NoteModdedCast"/>)
	/// and a staff whose shot is not a minion keep the stand-in, <see cref="Content.Projectiles.SoldierMinion"/> (a flying minion that shoots / rams, never a melee swing).
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
		/// <summary>The slot a held modded weapon whose holdout is found through the owner's projectile-UUID table lives in (Projectile.GetByUUID refuses owner 255): the same throwaway slot the dragons use.</summary>
		internal static int UuidOwnerSlot => DragonOwner;
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
		/// <summary>True when this staff shot was marked bad this session (its staff uses the stand-in). Test / log use.</summary>
		internal static bool IsBadStaff(int shoot) => BadTypes.Contains(shoot);
		private static readonly List<Projectile> Parked = new();
		private static readonly List<Projectile> ParkedDragons = new();

		// Per-tick index of the driven minions that need their neighbours parked while they update (group-indexed minion types, Stardust Dragon segments), built once by PrepareTick from the
		// pass it already makes over the projectile array. Parking used to walk all 1000 projectile slots (and look up each one's tag) for EVERY such minion on EVERY tick; it now walks
		// only these lists, with the owning soldier read from a parallel array instead of a per-entry lookup.
		private static readonly List<Projectile> GroupMinionsNow = new();
		private static readonly List<int> GroupMinionOwners = new();
		private static readonly List<Projectile> DragonSegmentsNow = new();
		private static readonly List<int> DragonSegmentOwners = new();
		// Every driven projectile on the shared owner 255 this tick (minions and the shots a driven minion keeps on 255) with its soldier, and whether more than one soldier has any: the modded minions that scan "every projectile of my
		// owner" (Tenryu's dragons collect their segments that way: two soldiers' tails collide in one dictionary) get the other soldiers' projectiles parked for their update (see ParkOtherSoldiersIndexed).
		private static readonly List<Projectile> DrivenNow = new();
		private static readonly List<int> DrivenNowOwners = new();
		private static int _drivenFirstSoldier = -1;
		private static bool _drivenMulti;
		/// <summary>Lab switch (env LEVY_PARKMODDED=0): modded minions see the other soldiers' projectiles on their owner slot again, as before the owner-scan fix (the A/B before / after).</summary>
		private static readonly bool ParkModded = Environment.GetEnvironmentVariable("LEVY_PARKMODDED") != "0";
		/// <summary>Lab counter: updates of modded minions that ran with the other soldiers' projectiles parked.</summary>
		internal static int ModdedParks;

		/// <summary>The slot-aliasing bookkeeping of <see cref="AliasDummy"/>: the real Player that stood in the dragon's owner slot while the soldier's dummy took its place.</summary>
		private static Player _aliasBackup;
		private static int _aliasSlot = -1;
		private static bool _aliased;

		/// <summary>The real Main.myPlayer while a minion's Update has it faked to 255 (single player); children created in that window are re-owned to it.</summary>
		private static bool _faked;
		private static int _trueMyPlayer;
		internal static int TrueMyPlayer => _faked ? _trueMyPlayer : Main.myPlayer;
		/// <summary>Lets another frame (the gear upkeep) make slot 255 the machine's player while <see cref="TrueMyPlayer"/> still answers with the real one. Returns a token for <see cref="EndFake"/> (bit 1 = was already faked, bit 2 = this call faked it; upper bits keep the previous real slot).</summary>
		internal static int BeginFake(int trueMy)
		{
			int tok = (_faked ? 1 : 0) | (_trueMyPlayer << 2);
			if (trueMy != Slot) { if (!_faked) tok |= 2; _trueMyPlayer = _faked ? _trueMyPlayer : trueMy; _faked = true; }
			return tok;
		}
		internal static void EndFake(int tok) { _faked = (tok & 1) != 0; _trueMyPlayer = tok >> 2; }

		// ---- the Update frame and the strike window (see "THE KILL CASCADE" above) ----
		/// <summary>The driven minion whose Projectile.Update is inside orig right now (dummy swap, dragon alias and faked Main.myPlayer all in place); null outside <see cref="RunUpdate"/>.</summary>
		private static Projectile _frameProj;
		/// <summary>The soldier that frame runs for, and the owner slot it aliased the dummy into (255, or <see cref="DragonOwner"/> for a dragon segment).</summary>
		private static SoldierNPC _frameBody;
		private static int _frameOwnerSlot;
		/// <summary>True for the length of one driven minion's Projectile.Damage call on the server / single player: a StrikeNPC inside it runs in the real-player context.</summary>
		private static bool _strikeArmed;
		/// <summary>True while the Update's swap is lifted for a strike, so a nested strike (an on-hit hook, the cascade itself) passes straight through.</summary>
		private static bool _realContext;

		internal static void Reset()
		{
			BadTypes.Clear(); ShortLives.Clear(); LearnedBuff.Clear(); Parked.Clear(); ParkedAll.Clear(); ParkedDragons.Clear(); CastSlots.Clear(); LateSlotted.Clear(); Stackers.Clear(); NoRealShoot.Clear(); EmptyCasts.Clear(); Helpers.Clear();
			GroupMinionsNow.Clear(); GroupMinionOwners.Clear(); DragonSegmentsNow.Clear(); DragonSegmentOwners.Clear(); DrivenNow.Clear(); DrivenNowOwners.Clear(); _drivenFirstSoldier = -1; _drivenMulti = false; Benches.Clear();
			PendingBody = null; _faked = false; EnsureAliasRestored(); ClearFrame();
		}

		/// <summary>Nothing of a minion's Update frame or strike window may outlive the projectile phase; the per-tick safety net and every reset call this.</summary>
		internal static void ClearFrame()
		{
			_frameProj = null; _frameBody = null; _frameOwnerSlot = 0; _strikeArmed = false; _realContext = false;
		}

		// =====================================================================================================
		// Identity
		// =====================================================================================================
		/// <summary>A vanilla minion projectile run for a soldier: minion flag, owner 255, tagged with a soldier.</summary>
		internal static bool IsDriven(Projectile p) =>
			p.active && p.minion && (p.owner == Slot || (p.owner == DragonOwner && IsDragonType(p.type)))
			&& p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0;

		/// <summary>The four segments of the Stardust Dragon: head 625, bodies 626 / 627, tail 628.</summary>
		internal static bool IsDragonType(int type) => type >= ProjectileID.StardustDragon1 && type <= ProjectileID.StardustDragon4;

		/// <summary>The owner slot a driven minion of <paramref name="type"/> lives on: 255, except the Stardust Dragon's own slot.</summary>
		private static int OwnerSlotFor(int type) => IsDragonType(type) ? DragonOwner : Slot;

		/// <summary>Staves (item types) whose slot-free shot was seen to make a minion that takes slots and stays (the Midnight Sun Beacon: its shot rises and becomes a UFO). Learned from the first such child; session only.</summary>
		private static readonly HashSet<int> LastingMakers = new();
		/// <summary>True when the staff's slot-free shot has been seen to turn into a minion that takes slots, so the staff is cast like any minion staff, not only while its soldier fights.</summary>
		internal static bool MakesLastingMinions(int itemType)
		{
			if (LastingMakers.Contains(itemType)) return true;
			if (!LastingScan.TryGetValue(itemType, out bool v))
			{
				try { v = ScanShotForLastingChild(itemType); } catch (Exception) { v = false; }
				LastingScan[itemType] = v;
			}
			return v;
		}
		private static readonly Dictionary<int, bool> LastingScan = new();

		/// <summary>
		/// Reads the IL of the staff's shot AI for the projectile types it makes (<c>ModContent.ProjectileType&lt;T&gt;()</c>): true when one of them is a minion that takes slots. The Midnight Sun Beacon's shot (no slots of its own,
		/// 420 ticks of rising) makes the UFO that stays. Any failure reads as "no"; the first such child seen in play teaches the same thing (<see cref="NoteLastingChild"/>).
		/// </summary>
		private static bool ScanShotForLastingChild(int itemType)
		{
			if (!ContentSamples.ItemsByType.TryGetValue(itemType, out Item it) || it.shoot < ProjectileID.Count) return false;
			ModProjectile mp = ProjectileLoader.GetProjectile(it.shoot);
			var m = mp?.GetType().GetMethod("AI", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
			byte[] il = m?.GetMethodBody()?.GetILAsByteArray();
			if (il == null) return false;
			for (int i = 0; i + 4 < il.Length; i++)
			{
				if (il[i] != 0x28 && il[i] != 0x6F) continue; // call, callvirt
				int token = BitConverter.ToInt32(il, i + 1);
				if ((int)((uint)token >> 24) != 0x2B) continue; // a generic method instance
				System.Reflection.MethodBase target;
				try { target = m.Module.ResolveMethod(token, mp.GetType().GenericTypeArguments, null); } catch (Exception) { continue; }
				if (target == null || target.Name != "ProjectileType" || !target.IsGenericMethod) continue;
				Type arg = target.GetGenericArguments()[0];
				int type = (int)typeof(ModContent).GetMethod("ProjectileType", Type.EmptyTypes).MakeGenericMethod(arg).Invoke(null, null);
				if (type >= ProjectileID.Count && ContentSamples.ProjectilesByType.TryGetValue(type, out Projectile sample) && sample.minion && !sample.sentry && sample.minionSlots > ChildSlotMin) return true;
			}
			return false;
		}
		/// <summary>A modded minion that takes no slot just made a minion that does (OnSpawn of the child): the staff of the parent is a minion staff whatever its shot's own slots say.</summary>
		internal static void NoteLastingChild(Projectile parent, Projectile child)
		{
			if (!parent.minion || !child.minion || child.sentry || child.minionSlots <= ChildSlotMin || parent.minionSlots > ChildSlotMin || parent.type < ProjectileID.Count) return;
			Item staff = MiscProjSwap.BodyOf(parent)?.SummonWeaponFor(parent.type);
			if (staff == null || staff.shoot != parent.type) return;
			if (LastingMakers.Add(staff.type)) ModContent.GetInstance<Levy>()?.Logger.Info("Levy: " + staff.Name + "'s slot-free shot made a minion that takes " + child.minionSlots + " slot(s): cast as a minion staff from now on.");
		}

		/// <summary>True when <paramref name="p"/> is a driven minion belonging to the soldier at NPC index <paramref name="soldierIndex"/>.</summary>
		internal static bool OwnedBy(Projectile p, int soldierIndex) =>
			IsDriven(p) && p.GetGlobalProjectile<SoldierProjectileTag>().SoldierIndex == soldierIndex;

		/// <summary>Whether the staff's minion is run as a real vanilla projectile for THIS soldier: <see cref="CanDrive(int)"/> and the soldier is not benched for the staff (one of its real minions threw lately).</summary>
		public static bool CanDrive(int shoot, int soldierIndex) => CanDrive(shoot) && !IsBenched(shoot, soldierIndex, Main.GameUpdateCount);

		/// <summary>Whether the staff's minion is run as a real vanilla projectile (vanilla type, a minion, not a known-unsupported one, not one that threw for several soldiers). A soldier it threw for alone is <see cref="IsBenched"/>.</summary>
		public static bool CanDrive(int shoot)
		{
			if (shoot <= 0 || Undriven.Contains(shoot) || BadTypes.Contains(shoot)) return false;
			Projectile sample = WeaponClassifier.ProjectileSample(shoot);
			if (sample == null || !sample.minion) return false;
			// A modded minion runs its own AI under the same dummy owner; its buff is applied to the dummy each tick (see ApplyModdedBuff). Many modded minions use aiStyle 0 (custom AI).
			return shoot >= ProjectileID.Count || sample.aiStyle > 0;
		}

		/// <summary>A minion-flagged child counts toward its soldier's flock only when it takes more than this many slots.</summary>
		internal const float ChildSlotMin = 0.01f;

		/// <summary>The slots one live minion counts for: its own, or for a modded minion that reports none (its AI sets it later or never) the cast's slot cost. When the staff's cast cost is already known from the pieces that do report slots (<paramref name="castKnown"/>), a piece that reports none is a companion that takes no slot (the Endo Hydra's body).</summary>
		internal static float SlotOf(Projectile p, float perCast, bool castKnown = false) => p.minionSlots > 0.01f || p.type < ProjectileID.Count ? p.minionSlots : castKnown ? 0f : perCast;

		/// <summary>True for a minion that is a direct cast of the staff (created by <see cref="NewMinion"/>), false for a minion-flagged child a modded minion's own AI made.</summary>
		internal static bool IsCast(Projectile p) => p.type < ProjectileID.Count || (p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.Cast);

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

		/// <summary>Who threw for one staff root: the soldiers (NPC index) that threw, each with its last throw, its count and the tick its bench ends.</summary>
		private sealed class BenchInfo { public readonly Dictionary<int, (uint last, int strikes, uint until)> By = new(); public bool Logged; }
		private static readonly Dictionary<int, BenchInfo> Benches = new();
		/// <summary>Lab: when set (> 0) the bench length in ticks instead of <see cref="LevyConstants.MinionBenchTicks"/> (the A/B bench check retries within its window).</summary>
		internal static int LabBenchTicks;
		/// <summary>Lab: the next driven minion of this type of this soldier throws once inside its update, as if another mod's hook had (see the summon A/B bench check).</summary>
		internal static int LabThrowSoldier = -1, LabThrowType, LabThrows;
		/// <summary>Lab counters: throws noted, and staves benched for everyone.</summary>
		internal static int BenchNotes, BenchTypeWide;

		/// <summary>True while <paramref name="soldierIndex"/> is benched for the staff whose shot is <paramref name="root"/> (a real minion of it threw for this soldier less than its bench ago).</summary>
		internal static bool IsBenched(int root, int soldierIndex, uint now) =>
			Benches.Count > 0 && Benches.TryGetValue(root, out BenchInfo b) && b.By.TryGetValue(soldierIndex, out var e) && now < e.until;

		/// <summary>
		/// A real minion of <paramref name="root"/> threw while driven for soldier <paramref name="soldierIndex"/>. That soldier alone uses the stand-in for the staff for the bench (retried after it, longer with each further throw); logged once per staff with the exception.
		/// Only when <see cref="LevyConstants.MinionBenchLevies"/> different soldiers have thrown for it within <see cref="LevyConstants.MinionBenchWindowTicks"/> is the staff benched for everyone for the session (a bug in the staff or in a mod it
		/// meets, not in one levy's state). Returns true when that happened.
		/// </summary>
		internal static bool NoteThrow(int root, int soldierIndex, uint now, Exception e, string what)
		{
			if (!Benches.TryGetValue(root, out BenchInfo b)) Benches[root] = b = new BenchInfo();
			BenchNotes++;
			b.By.TryGetValue(soldierIndex, out var prev);
			if (prev.strikes > 0 && now < prev.until) return false; // the rest of that soldier's flock throwing before the upkeep has dismissed it: the same event, not another strike
			int strikes = (prev.strikes > 0 && now - prev.last <= LevyConstants.MinionBenchWindowTicks ? prev.strikes : 0) + 1;
			long baseLen = LabBenchTicks > 0 ? LabBenchTicks : LevyConstants.MinionBenchTicks, cap = LabBenchTicks > 0 ? LabBenchTicks * 8L : LevyConstants.MinionBenchMaxTicks;
			long len = Math.Min(baseLen << Math.Min(strikes - 1, 10), cap);
			b.By[soldierIndex] = (now, strikes, now + (uint)len);
			int recent = 0; foreach (var kv in b.By) if (now - kv.Value.last <= LevyConstants.MinionBenchWindowTicks) recent++;
			bool wide = recent >= LevyConstants.MinionBenchLevies && BadTypes.Add(root);
			if (wide) BenchTypeWide++;
			Levy lv = ModContent.GetInstance<Levy>();
			bool lab = what == "self-test" || e.Message.StartsWith("lab-injected"); // the bench checks' own throws: logged without the exception, which the ship's exception count would take for a real one
			string why = lab ? e.Message : e.GetType().Name + ": " + e.Message;
			if (what == "self-test") return wide;
			if (wide) lv?.Logger.Warn("Levy: real minion of staff shot " + root + " (" + what + ") threw for " + recent + " different soldiers within " + LevyConstants.MinionBenchWindowTicks / 60 + " s; its staff falls back to the stand-in minion for everyone: " + why);
			else if (!b.Logged) { b.Logged = true; lv?.Logger.Warn("Levy: real minion of staff shot " + root + " (" + what + ") threw while driven for soldier " + soldierIndex + "; that soldier uses the stand-in minion for " + len / 60 + " s, then tries the real one again (others are not affected): " + (lab ? why : e.ToString())); }
			return wide;
		}

		/// <summary>Whether the staff is benched for every soldier (several of them threw). Test / log use.</summary>
		internal static bool BenchedForAll(int root) => BadTypes.Contains(root);

		/// <summary>A real minion threw under the dummy: its soldier is benched for the staff (see <see cref="NoteThrow"/>); the minion is dropped by the caller.</summary>
		private static void MarkBad(Projectile p, SoldierNPC body, Exception e)
		{
			int root = FamilyRoot(p.type);
			Item staff = body?.SummonWeaponFor(p.type);
			if (p.type >= ProjectileID.Count && staff != null && staff.shoot > 0) root = staff.shoot; // a modded child that throws takes its staff back to the stand-in
			NoteThrow(root, body != null ? body.NPC.whoAmI : -1, Main.GameUpdateCount, e, p.type + " " + (p.ModProjectile?.FullName ?? "vanilla"));
			if (body != null) body.FlockDirty = true; // the upkeep looks at the flock now: the benched soldier's real minions of the staff are dismissed and its stand-ins made
		}

		/// <summary>Self-check of the bench rules with a made-up staff root and explicit ticks (no real staff is touched): one soldier's throw benches only that soldier, the bench ends and doubles, three soldiers bench the staff for all. Returns what failed, empty when all hold.</summary>
		internal static string BenchSelfTest()
		{
			var bad = new List<string>();
			int root = -7001; uint t0 = 100000;
			int savedLab = LabBenchTicks; LabBenchTicks = 0;
			var savedBad = new HashSet<int>(BadTypes);
			int notes = BenchNotes, wide0 = BenchTypeWide;
			try
			{
				Benches.Remove(root); BadTypes.Remove(root);
				uint len = (uint)LevyConstants.MinionBenchTicks;
				bool wide = NoteThrow(root, 3, t0, new InvalidOperationException("self-test"), "self-test");
				if (wide) bad.Add("one throw benched the staff for everyone");
				if (!IsBenched(root, 3, t0 + 1)) bad.Add("the soldier that threw is not benched");
				if (IsBenched(root, 4, t0 + 1)) bad.Add("another soldier is benched by it");
				if (BadTypes.Contains(root)) bad.Add("the staff is type-wide bad after one throw");
				if (IsBenched(root, 3, t0 + len + 1)) bad.Add("the bench does not end (no retry)");
				NoteThrow(root, 3, t0 + len + 5, new InvalidOperationException("self-test"), "self-test");
				if (!IsBenched(root, 3, t0 + len + 5 + len + 1)) bad.Add("a second throw of the same soldier does not bench it longer");
				if (BadTypes.Contains(root)) bad.Add("two throws of ONE soldier benched the staff for everyone");
				NoteThrow(root, 4, t0 + 2 * len, new InvalidOperationException("self-test"), "self-test");
				if (BadTypes.Contains(root)) bad.Add("two soldiers already bench the staff for everyone");
				bool w3 = NoteThrow(root, 5, t0 + 2 * len + 1, new InvalidOperationException("self-test"), "self-test");
				if (!w3 || !BadTypes.Contains(root)) bad.Add("three soldiers do not bench the staff for everyone");
			}
			finally { Benches.Remove(root); BadTypes.Clear(); foreach (int b in savedBad) BadTypes.Add(b); LabBenchTicks = savedLab; BenchNotes = notes; BenchTypeWide = wide0; }
			return string.Join("; ", bad);
		}

		/// <summary>Consecutive immediate self-kills per staff shot (a modded cast that died within a few ticks of spawning).</summary>
		private static readonly Dictionary<int, int> ShortLives = new();

		/// <summary>
		/// A modded cast just died by itself (not dismissed by the upkeep). Judged by LIFETIME ONLY, never by hits (a minion with nothing in its range hits nothing and is fine): three in a row that died
		/// within <see cref="LevyConstants.ModdedMinionShortLifeTicks"/> ticks of spawning mean the minion cannot live under a dummy owner (it needs a companion part its staff's Shoot makes, an owner state
		/// the dummy lacks...), so its staff uses the stand-in from then on. Any longer life resets the count.
		/// </summary>
		internal static void NoteModdedDeath(Projectile p, int age)
		{
			Item staff = MiscProjSwap.BodyOf(p)?.SummonWeaponFor(p.type);
			if (staff == null || staff.shoot <= 0) return; // the soldier gave the staff back (rearm), left, or fell: a death with no staff in hand is not evidence about the staff
			if (SoldierNPC.TransientStaff(staff)) return; // a self-ending cast (a bomber that dives, a fist that strikes) dies within moments by design: it says nothing about whether it can live under a dummy owner
			SoldierNPC holder = MiscProjSwap.BodyOf(p);
			if (holder?.Dummy != null && Math.Max(p.minionSlots, SoldierNPC.SlotsPerMinion(staff)) > Math.Max(1, holder.Dummy.maxMinions) + LevyConstants.MinionSlotEpsilon)
			{
				if (TooBig.Add((staff.type, Math.Max(1, holder.Dummy.maxMinions)))) ModContent.GetInstance<Levy>()?.Logger.Info("Levy: " + staff.Name + "'s minion takes " + Math.Max(p.minionSlots, SoldierNPC.SlotsPerMinion(staff)) + " slots, more than a holder with " + holder.Dummy.maxMinions + " has: it ended itself, and is not cast at that many.");
				return; // a minion that takes more slots than its holder has ends itself in its first update, as it does for a player with those slots (Ares' Exoskeleton's cannons at 1 slot): that says nothing about the staff, and a holder with more slots must still get the real minion
			}
			int root = staff.shoot;
			if (age >= LevyConstants.ModdedMinionShortLifeTicks) { ShortLives[root] = 0; return; }
			int n = ShortLives.GetValueOrDefault(root) + 1;
			ShortLives[root] = n;
			if (n >= LevyConstants.ModdedMinionMaxShortLives && BadTypes.Add(root))
				ModContent.GetInstance<Levy>()?.Logger.Warn("Levy: modded minion " + (p.ModProjectile?.FullName ?? p.type.ToString()) + " died within " + LevyConstants.ModdedMinionShortLifeTicks + " ticks of spawning " + n + " times in a row for a soldier; its staff falls back to the stand-in minion.");
		}

		/// <summary>The children a modded flock may have alive for a holder with <paramref name="slots"/> minion slots: what a player with those slots holds (the heaviest staff measured keeps 12 a slot at its peak), never under the old fixed floor.</summary>
		internal static int ChildrenAliveLimit(int slots) => Math.Max(LevyConstants.ModdedMinionMaxChildrenAlive, LevyConstants.ModdedMinionChildrenPerSlot * Math.Max(1, slots));

		/// <summary>
		/// A modded flock whose minions flood the world with projectiles (more than <see cref="LevyConstants.ModdedMinionMaxChildSpawnsPerPass"/> created in one upkeep pass, or more than
		/// <see cref="ChildrenAliveLimit"/> alive on <see cref="LevyConstants.ModdedMinionFloodPasses"/> passes in a row): the excess projectiles are ended now and the staff uses the stand-in from then on (remembered per staff, logged once).
		/// </summary>
		internal static void FloodGuard(SoldierNPC s, Item weapon, int spawned, int alive, int limit)
		{
			if (BadTypes.Add(weapon.shoot))
				ModContent.GetInstance<Levy>()?.Logger.Warn("Levy: modded minion of " + weapon.Name + " flooded the world with projectiles (" + spawned + " created in one pass, " + alive + " alive, the limit for " + Math.Max(1, limit) + " alive); its staff falls back to the stand-in minion.");
			KillAll(s);
			int me = s.NPC.whoAmI;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile q = Main.projectile[i];
				if (q.active && !q.minion && q.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex == me) q.timeLeft = Math.Min(q.timeLeft, 1);
			}
		}

		/// <summary>Removes every real minion (and companion) this soldier has out. Server / single player.</summary>
		internal static void KillAll(SoldierNPC s)
		{
			int me = s.NPC.whoAmI;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (OwnedBy(p, me) || (IsDrivenHelper(p) && p.GetGlobalProjectile<SoldierProjectileTag>().SoldierIndex == me)) Dismiss(p); // his gear's pets and helpers go with him too
			}
		}

		/// <summary>
		/// Ends a real minion. A Stardust Dragon segment is owned by a slot that is not this machine's Main.myPlayer, and vanilla broadcasts a kill only for the machine's own projectiles,
		/// so a dedicated server claims the segment's slot for the Kill (restored in the finally) or remote screens would keep the segment forever.
		/// </summary>
		internal static void Dismiss(Projectile p)
		{
			if (!p.active) return;
			if (p.TryGetGlobalProjectile(out SoldierProjectileTag dt)) dt.Dismissed = true; // an upkeep decision, not the minion dying by itself
			if (Main.netMode != NetmodeID.Server || !IsDragonType(p.type) || p.owner == Main.myPlayer) { KillAlone(p); return; }
			int saved = Main.myPlayer;
			try { Main.myPlayer = p.owner; p.Kill(); }
			finally { Main.myPlayer = saved; }
		}

		private static readonly List<Projectile> ParkedKill = new();
		/// <summary>
		/// Kill() for a modded minion outside its own update: its OnKill may scan "every projectile of my owner" (the dragon segments of Tenryu end every head of their owner), so the other soldiers' driven projectiles sit on the parked owner for the call.
		/// A kill inside a driven minion's own frame (already parked there) is a plain Kill.
		/// </summary>
		private static void KillAlone(Projectile p)
		{
			SoldierProjectileTag t = null;
			if (!ParkModded || _frameProj != null || !p.minion || p.type < ProjectileID.Count || p.owner != Slot || !p.TryGetGlobalProjectile(out t) || t.SoldierIndex < 0) { p.Kill(); return; }
			int me = t.SoldierIndex;
			ParkedKill.Clear();
			try
			{
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile q = Main.projectile[i];
					if (!q.active || q.whoAmI == p.whoAmI || q.owner != Slot || !q.TryGetGlobalProjectile(out SoldierProjectileTag qt) || qt.SoldierIndex < 0 || qt.SoldierIndex == me || !(q.minion || qt.DrivenChild)) continue;
					q.owner = ParkedOwner; ParkedKill.Add(q);
				}
				p.Kill();
			}
			finally { foreach (Projectile q in ParkedKill) if (q.owner == ParkedOwner) q.owner = Slot; ParkedKill.Clear(); }
		}

		// =====================================================================================================
		// Spawning (server / single player)
		// =====================================================================================================
		/// <summary>The vanilla cast of one staff: the minion(s) it makes, with the starting ai[1] / localAI[0] Player.ItemCheck_Shoot / SpawnMinionOnCursor give them. Returns false when nothing was created.</summary>
		/// <summary>Lab counters (the summon-loop check reads them): casts of a minion staff, helper swings of a full flock, sentry plantings.</summary>
		internal static int CastTally, HelperTally, SentryTally;
		/// <summary>Lab counters: casts whose Shoot powered up a minion already out instead of making one (Saros Possession, Sirius, Void Eater Marionette), and RECAST uses of a full flock's staff.</summary>
		internal static int StackTally, RecastTally;

		internal static bool Summon(SoldierNPC s, Item weapon, int damage)
		{
			int shoot = weapon.shoot;
			if (shoot == ProjectileID.StardustDragon1) return SummonDragon(s, weapon, damage);
			if (UsesRealShoot(weapon) && SummonReal(s, weapon, damage)) return true;
			if (UsesRealShoot(weapon) && !NoRealShoot.Contains(weapon.type) && _lastStacked) return true; // the Shoot powered up the minion already out: nothing to cast directly
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
		/// A throwaway owner slot (the dragon's, <see cref="DragonOwner"/>) holds the Player the game made for it before any mod was loaded, which carries no ModPlayer set. A mod's OnSpawn hook that reads
		/// <c>GetModPlayer</c> of a projectile's owner (an element table that asks what the owner wears) throws on it for every segment spawned. Such a slot gets a Player made with every loaded mod's ModPlayers, once.
		/// </summary>
		private static void ReadyOwnerSlot(int slot)
		{
			Player p = Main.player[slot], reference = Main.player[0];
			if (p == null || reference == null || p.active || global::Levy.Common.Owner.SoldierOwner.IsDummy(p)) return;
			if (p.ModPlayers.Length != reference.ModPlayers.Length) Main.player[slot] = new Player();
		}

		/// <summary>
		/// Creates one minion owned by 255 (a Stardust Dragon segment: by <see cref="DragonOwner"/>) and tagged with the soldier. NewProjectile only assigns ai[] and syncs when Owner == Main.myPlayer,
		/// so the machine claims that slot for the call (restored in the finally); the tag is applied in OnSpawn via <see cref="PendingBody"/> so the very first spawn packet already names the soldier.
		/// <paramref name="ai0"/> is only meaningful for a dragon segment: the index of the segment ahead of it (vanilla turns it into that segment's UUID).
		/// </summary>
		private static Projectile NewMinion(SoldierNPC s, Vector2 pos, int type, int damage, float knockback, float ai1, float ai0 = 0f)
		{
			int savedMyPlayer = Main.myPlayer, ownerSlot = OwnerSlotFor(type);
			if (ownerSlot != Slot) ReadyOwnerSlot(ownerSlot);
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
				if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) { tag.SoldierIndex = s.NPC.whoAmI; tag.Cast = true; }
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
		/// Adopts a helper minion a worn ACCESSORY cast during the gear upkeep frame (see SoldierNPC.GearFrame): it is owned by 255 and tagged with the soldier, exactly as <see cref="NewMinion"/> leaves a staff's minion, so every
		/// Update of it runs under the soldier's dummy (its Calamity / mod flags, buffs, position, target). It is not a cast of a staff (<c>Cast</c> false), so the staff's flock upkeep leaves it alone; it ends when the accessory
		/// stops being worn (its own AI sees the dummy's flag drop) or the soldier is gone. False for a hostile soldier's, a sentry or a non-minion, which keep the ordinary adoption.
		/// </summary>
		internal static bool AdoptAccessoryMinion(SoldierNPC s, Projectile p)
		{
			if (s == null || p == null || !p.active || !p.minion || p.sentry || s.Hostile || !p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) return false;
			p.owner = Slot;
			p.npcProj = true; // the commander's on-hit effects and item drops never run for a soldier's minion
			p.noDropItem = true;
			p.friendly = true; p.hostile = false;
			tag.SoldierIndex = s.NPC.whoAmI; tag.Cast = false;
			p.netUpdate = true;
			return true;
		}

		/// <summary>
		/// Adopts a PET or other HELPER (not a minion) a worn accessory or its buff made during the gear upkeep frame (see GearUpkeep.IsGearHelper): owned by 255 and tagged with the soldier like an accessory minion, so every Update of it runs
		/// under the soldier's dummy (<see cref="IsDrivenHelper"/>): it follows that soldier, the dummy's own flags keep it alive (Daawnlight Spirit Origin's archer reads the wearer's pet flag), and it ends when the gear comes off or
		/// the soldier is gone. It takes no minion slot and is never part of a flock. False for a hostile soldier's or a minion, which keep their own adoption.
		/// </summary>
		internal static bool AdoptAccessoryHelper(SoldierNPC s, Projectile p)
		{
			if (s == null || p == null || !p.active || p.minion || p.sentry || p.hostile || s.Hostile || !p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) return false;
			p.owner = Slot;
			p.npcProj = true; // the commander's on-hit effects and item drops never run for it
			p.noDropItem = true;
			tag.SoldierIndex = s.NPC.whoAmI; tag.Cast = false; tag.GearHelper = true;
			p.netUpdate = true;
			HelpersAdopted++;
			return true;
		}
		/// <summary>Lab counter: helpers <see cref="AdoptAccessoryHelper"/> took.</summary>
		internal static int HelpersAdopted;

		/// <summary>A gear helper of a soldier (a pet, a companion: see <see cref="AdoptAccessoryHelper"/>): owner 255, not a minion, tagged as a helper of a soldier. Known on clients too (the helper bit is synced).</summary>
		internal static bool IsDrivenHelper(Projectile p) =>
			p.active && !p.minion && p.owner == Slot && p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.GearHelper && tag.SoldierIndex >= 0;

		/// <summary>
		/// Vanilla re-derives a minion's damage every tick as <c>owner.GetTotalDamage(Summon).ApplyTo(originalDamage)</c>; the owner here is the dummy, so its own summon ratio is divided
		/// back out and the soldier's weapon damage x grade (<paramref name="desiredDamage"/>) is what the minion deals. Also keeps the staff's knockback.
		/// </summary>
		internal static void Retune(SoldierNPC s, Projectile p, Item weapon, int desiredDamage)
		{
			if (p.TryGetGlobalProjectile(out SoldierProjectileTag ft) && ft.DamageFactor != 1f) desiredDamage = Math.Max(1, (int)Math.Round(desiredDamage * ft.DamageFactor)); // the staff's own factor on the cast's damage (AdoptMade stored it), kept by every upkeep retune
			p.knockBack = weapon.knockBack;
			DamageClass cls = p.DamageType ?? DamageClass.Summon;
			float ratio = 1f;
			if (s.Dummy != null) ratio = s.Dummy.GetTotalDamage(cls).ApplyTo(100f) / 100f;
			ratio = Math.Clamp(ratio, LevyConstants.MinionDamageRatioMin, LevyConstants.MinionDamageRatioMax);
			int original = Math.Max(1, (int)Math.Round(desiredDamage / ratio));
			if (p.originalDamage != original) p.originalDamage = original;
		}

		// =====================================================================================================
		// A modded staff's own Shoot (the cast, and the helper a full flock still gets)
		// =====================================================================================================
		/// <summary>Slots one real cast of a staff takes (item type to the summed slots of the minions its own Shoot made), learned from the first cast. Staves whose pieces report no slots yet keep the sample's cost.</summary>
		internal static readonly Dictionary<int, float> CastSlots = new();
		/// <summary>Staves (item types) whose cast minion is born with no slots and takes them in its own first update (Glacial Embrace's spikes set 1 each tick, the Void Eater Marionette adds the slots it asks for): a minion staff, not a self-ending cast, though its shot reports none.</summary>
		internal static readonly HashSet<int> LateSlotted = new();
		/// <summary>Staves (item types) whose own Shoot threw or made nothing twice: they keep the direct cast of their shot.</summary>
		private static readonly HashSet<int> NoRealShoot = new();
		private static readonly Dictionary<int, int> EmptyCasts = new();
		private sealed class HelperInfo { public int Probes; public bool Seen; }
		/// <summary>Staff item type to what its Shoot has been seen to make while the flock is full (Dormant Brimseeker: an aura that empowers the minions).</summary>
		private static readonly Dictionary<int, HelperInfo> Helpers = new();

		/// <summary>A modded staff is cast through its own Shoot hook, as a player's swing does it (the minion's companion parts, the extra ai values, the summoner shot a minion comes out of); vanilla staves keep the cast table in <see cref="Summon"/>.</summary>
		internal static bool UsesRealShoot(Item weapon) =>
			weapon != null && weapon.ModItem != null && weapon.shoot >= ProjectileID.Count && LevyConfig.Get.RealWeaponShoot && !NoRealShoot.Contains(weapon.type);

		/// <summary>
		/// Runs the staff's ModifyShootStats and Shoot with the soldier's dummy as the player (see HeldWeapons.ShootHooks) and returns what they made, still owned by 255. Every OTHER soldier's minions sit on the parked
		/// owner for the call: a Shoot counts the owner's minions to decide between its cast and its fallback, and all soldiers share owner 255. The staff's buff, which Shoot adds to the dummy, is put back afterwards.
		/// </summary>
		/// <summary>Lab A/B (env LEVY_SUMMON_CASTAT=target): every modded cast is made at the soldier's target (when it is not inside a wall), as the summon sweep's parity commander casts at hers.</summary>
		private static readonly bool CastAtTargetLab = Environment.GetEnvironmentVariable("LEVY_SUMMON_CASTAT") == "target";
		/// <summary>
		/// Modded staves cast where a player's cursor is, at the enemy, when their minion does worse for being made beside its owner: the Enchanted Conch's Hermit Crab sprawled 22 tiles from its owner and 15 from the enemy and did 0.27 to
		/// 0.54 of the player's damage (summon A/B 2026-11-25, three boots); cast at the target it does 0.67 every time. Most staves lose by it (Ethereal Subjugator, Caustic Staff, Black Hawk Remote 0.9 down to 0.2), so it is a short list,
		/// not the default (LEVY_SUMMON_CASTAT=target casts every modded staff there, for the A/B). The Infected Remote is a different case, see <see cref="CastFarNames"/>.
		/// </summary>
		private static readonly HashSet<string> CastAtTargetNames = new() { "EnchantedConch" };
		internal static bool CastsAtTarget(Item weapon) => weapon?.ModItem != null && weapon.ModItem.Mod.Name == "CalamityMod" && CastAtTargetNames.Contains(weapon.ModItem.Name);
		/// <summary>
		/// Staves whose minion idles for as long as it is inside a bubble around its owner and leaves that bubble only by its own momentum: the Infected Remote's Plague Princess returns from her AI while she is within 10 tiles of her owner,
		/// before she looks for a target (PlaguePrincess.DoBehavior_HoverNearOwner), and she is born with no speed. A player's cursor is rarely that close and he keeps moving, so she is born outside the bubble and arrives with speed; a levy casts at his own feet and stands still, and
		/// the Princess then hung beside him for the whole fight (0 hits in 720 ticks against 98 for the player's). These casts are made that far (px) from the soldier, towards his target (else the way he faces), where a player's cursor would be. Measured: the summon A/B.
		/// </summary>
		private static readonly Dictionary<string, float> CastFarNames = new() { ["InfectedRemote"] = 14f * 16f };
		internal static float CastFarPx(Item weapon) => weapon?.ModItem != null && weapon.ModItem.Mod.Name == "CalamityMod" && CastFarNames.TryGetValue(weapon.ModItem.Name, out float px) ? px : 0f;
		private static List<Projectile> RunShoot(SoldierNPC s, Item weapon, int damage, out bool threw)
		{
			// a self-ending cast (a bomber, a fist, a field: see SoldierNPC.TransientStaff) is aimed where a player aims it, at the enemy he fights; a minion that stays is cast beside the soldier. Casting every modded staff at the
			// target was measured too (LEVY_SUMMON_CASTAT=target, summon sweep 2026-11-22) and was mixed: Vigilance 0.25 -> 0.78 and the Pole Warper 0.62 -> 0.93 of the commander's damage, but the Ethereal Subjugator, the Gastric
			// Belcher, the Resurrection Butterfly and Entropy's Vigil lost half or more, so it is not the default.
			bool atTarget = SoldierNPC.TransientStaff(weapon) || (CastsAtTarget(weapon) && s.TargetIndex >= 0 && s.TargetIndex < Main.maxNPCs && Main.npc[s.TargetIndex].active) || (CastAtTargetLab && s.TargetIndex >= 0 && s.TargetIndex < Main.maxNPCs && Main.npc[s.TargetIndex].active);
			Player d = s.Dummy; threw = false;
			RefreshCounts(s); // a Shoot that asks the owner how many of its minion are out (to grow it, or to make a fresh one) must be told the truth, not last tick's count
			var moved = new List<Projectile>();
			int me = s.NPC.whoAmI, saved = Main.myPlayer;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == Slot && p.minion && p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex >= 0 && t.SoldierIndex != me) { p.owner = ParkedOwner; moved.Add(p); }
			}
			float scatter = Main.rand.NextFloat(-1f, 1f) * LevyConstants.MinionSpawnScatterPx;
			Vector2 at = s.NPC.Center + new Vector2(scatter, 0f);
			if (Collision.SolidCollision(at - LevyConstants.MinionSpawnProbe * 0.5f, (int)LevyConstants.MinionSpawnProbe.X, (int)LevyConstants.MinionSpawnProbe.Y)) at = s.NPC.Center;
			if (atTarget)
			{
				Vector2 aim = s.AimCursor() - new Vector2(0f, 16f * AltPolicies.CastUp(weapon));
				if (SoldierNPC.TransientStaff(weapon) || !Collision.SolidCollision(aim - LevyConstants.MinionSpawnProbe * 0.5f, (int)LevyConstants.MinionSpawnProbe.X, (int)LevyConstants.MinionSpawnProbe.Y)) at = aim;
			}
			float far = CastFarPx(weapon);
			if (far > 0f)
			{
				bool hasTarget = s.TargetIndex >= 0 && s.TargetIndex < Main.maxNPCs && Main.npc[s.TargetIndex].active;
				Vector2 dir = hasTarget ? Main.npc[s.TargetIndex].Center - s.NPC.Center : Vector2.Zero;
				if (dir.LengthSquared() < 1f) dir = new Vector2(s.NPC.direction == 0 ? 1f : s.NPC.direction, -0.15f);
				dir.Normalize();
				at = s.NPC.Center + dir * far; // a flier that ignores tiles: no wall test
			}
			List<Projectile> made;
			SaveBuffs(d);
			PendingBody = s;
			try
			{
				if (saved != Slot) { _trueMyPlayer = saved; _faked = true; }
				using (new AltPolicies.AimScope(d, at))
					made = HeldWeapons.ShootHooks(s, weapon, new Vector2(0f, -Math.Max(1f, weapon.shootSpeed)), damage, Slot, false, HeldCast(s, weapon, damage), out threw);
			}
			finally
			{
				Main.myPlayer = saved; _faked = false; PendingBody = null;
				s.TruthUntil = Main.GameUpdateCount + 2;
				RestoreBuffs(d);
				foreach (Projectile p in moved) if (p.active && p.owner == ParkedOwner) p.owner = Slot;
			}
			return made;
		}

		/// <summary>
		/// The cast as Player.ItemCheck makes it for a player holding the staff: the staff's own HoldItem first (a staff that reads its holder's free slots there, the Abandoned Slime Staff, sizes its slime from them), then
		/// ModifyShootStats and Shoot exactly as <see cref="HeldWeapons.ShootHooks"/> runs them. Only the staff's own HoldItem runs, not every mod's global one.
		/// </summary>
		private static Action<Terraria.DataStructures.IEntitySource, Vector2> HeldCast(SoldierNPC s, Item weapon, int damage) => (src, v0) =>
		{
			Player d = s.Dummy;
			// the holder is not this machine's player for the hook: a staff's HoldItem that toggles its minion on a hotkey (the Void Eater Marionette's fetch mode: "Main.myPlayer == player.whoAmI && JustPressedKeybind")
			// never fires for a levy, who presses no keys (measured: the worm toggled into fetching on some casts and dealt 64 damage a second instead of 5k)
			int my = Main.myPlayer;
			try { Main.myPlayer = d.whoAmI == Main.maxPlayers - 2 ? Main.maxPlayers - 3 : Main.maxPlayers - 2; weapon.ModItem?.HoldItem(d); } catch (Exception) { }
			finally { Main.myPlayer = my; }
			Vector2 pos = s.NPC.Center, v = v0; int type = weapon.shoot, dmg = damage; float kb = weapon.knockBack;
			try
			{
				CombinedHooks.ModifyShootStats(d, weapon, ref pos, ref v, ref type, ref dmg, ref kb);
				var source = src as Terraria.DataStructures.EntitySource_ItemUse_WithAmmo ?? new Terraria.DataStructures.EntitySource_ItemUse_WithAmmo(d, weapon, 0);
				if (CombinedHooks.Shoot(d, weapon, source, pos, v, type, dmg, kb)) Projectile.NewProjectile(source, pos, v, type, dmg, kb, Slot);
			}
			catch (Exception e)
			{
				if (_shootThrowLogged.Add(weapon.type))
				{
					int mine = 0, any = 0; for (int i = 0; i < Main.maxProjectiles; i++) { Projectile q = Main.projectile[i]; if (q.active && q.type == weapon.shoot) { any++; if (q.owner == d.whoAmI) mine++; } }
					ModContent.GetInstance<Levy>()?.Logger.Info("Levy: " + weapon.Name + "'s Shoot threw for a soldier (" + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim() + "; its shot: owned by the dummy's count " + (weapon.shoot < d.ownedProjectileCounts.Length ? d.ownedProjectileCounts[weapon.shoot] : -1) + ", in the world " + mine + " on slot " + d.whoAmI + " of " + any + ")");
				}
				throw;
			}
		};
		private static readonly HashSet<int> _shootThrowLogged = new();

		/// <summary>True for the minion a cast is "of" (the staff's own shot or a variant of it); the other parts a Shoot makes are companions that count through their own slots.</summary>
		private static bool IsMainPiece(Item weapon, Projectile p) => p.type == weapon.shoot || InFamily(weapon.shoot, p.type);

		/// <summary>The factor a piece a staff's Shoot just made carries on the cast's damage (the Abandoned Slime's 0.8 + 0.2 a free slot, a companion given double damage); 1 when it has none. Stored on the projectile's tag and applied by every <see cref="Retune"/>.</summary>
		private static float PieceFactor(Item weapon, Projectile p, int damage)
		{
			if (weapon.damage <= 0 || p.originalDamage <= 0 || p.originalDamage == damage) return 1f;
			return Math.Clamp(p.originalDamage / (float)weapon.damage, 0.1f, 10f);
		}

		/// <summary>Takes a minion a staff's Shoot made as a driven minion of the soldier (owner 255, tagged, retuned).</summary>
		private static void AdoptMade(SoldierNPC s, Item weapon, Projectile p, int damage, bool cast)
		{
			p.npcProj = true; p.noDropItem = true; p.friendly = true; p.hostile = false;
			if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) { tag.SoldierIndex = s.NPC.whoAmI; tag.Cast = cast; tag.DamageFactor = PieceFactor(weapon, p, damage); }
			Retune(s, p, weapon, damage);
			p.netUpdate = true;
		}

		/// <summary>
		/// One cast of a modded staff through its own Shoot. The minions it made become the soldier's (the one the cast is "of" is a cast, its companions count through their slots), and the slots the whole cast
		/// takes (one of each kind of part) are remembered so the flock is sized right (Lilies of Finality: two different minions at once). False when the Shoot threw or made no minion; a staff that does that twice in a row is cast directly from then on.
		/// </summary>
		private static bool SummonReal(SoldierNPC s, Item weapon, int damage)
		{
			_lastStacked = false;
			SnapFlock(s);
			List<Projectile> made = RunShoot(s, weapon, damage, out bool threw);
			float slots = 0f, all = 0f; int minions = 0;
			var seenTypes = new HashSet<int>();
			foreach (Projectile p in made)
			{
				if (!p.active) continue;
				if (p.minion && !p.sentry)
				{
					minions++;
					if (seenTypes.Add(p.type)) slots += p.minionSlots; // one of each part for a staff with a limit of its own: the Flowers of Mortality's five petals replace the last five, so its limit counts petals
					all += p.minionSlots;
					AdoptMade(s, weapon, p, damage, IsMainPiece(weapon, p));
				}
				else WeaponUse.TagSoldierProjectile(s, p, weapon.DamageType ?? DamageClass.Summon);
			}
			if (!SummonCap.IsCapped(weapon)) slots = all; // a staff with no limit keeps every piece a cast makes: the Pole Warper's two half-slot magnets are one slot a cast, as for a player
			if (!threw && minions == 0 && FlockChanged(s))
			{
				// the Shoot powered up a minion already out instead of making one (the Saros Possession's aura, Sirius, the Void Eater Marionette take another slot each cast): a cast, not an empty one
				_lastStacked = true; StackTally++; EmptyCasts[weapon.type] = 0; Stackers.Add(weapon.type);
				return false;
			}
			if (!threw && minions == 0 && made.Count > 0 && SoldierNPC.TransientStaff(weapon))
			{
				// a self-ending staff's Shoot that made something else (the Universe Splitter on its cooldown throws out scrap): what a player's click makes then, not a staff that cannot work under the soldier
				_lastStacked = true; EmptyCasts[weapon.type] = 0;
				return false;
			}
			if (threw || minions == 0)
			{
				int n = EmptyCasts.GetValueOrDefault(weapon.type) + 1; EmptyCasts[weapon.type] = n;
				if ((threw || n >= 2) && NoRealShoot.Add(weapon.type))
					ModContent.GetInstance<Levy>()?.Logger.Warn("Levy: " + weapon.Name + "'s own Shoot " + (threw ? "threw" : "made no minion") + " for a soldier; its minion is cast directly from now on.");
				return false;
			}
			EmptyCasts[weapon.type] = 0;
			if (slots > ChildSlotMin) CastSlots[weapon.type] = slots; // the latest cast's cost: a minion sized from its holder's free slots (the Abandoned Slime) costs what his slots allow now
			TrimMade(s, made);
			return true;
		}

		/// <summary>
		/// A cast that does not fit his minion slots keeps only the part that does, as for a player: a minion a player's gear has no room for ends itself (Calamity's minions check the owner's slots, the Aqueous Hunter Drone's three
		/// 4-slot drones at 1 slot are gone at once, Entropy's Vigil keeps one of its three). The pieces just made are ended, newest first, until his flock fits.
		/// </summary>
		private static void TrimMade(SoldierNPC s, List<Projectile> made)
		{
			float cap = s.Dummy != null ? Math.Max(1, s.Dummy.maxMinions) : 1f, total = 0f;
			int me = s.NPC.whoAmI;
			for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && p.minion && !p.sentry && p.minionSlots > ChildSlotMin && OwnedBy(p, me)) total += p.minionSlots; }
			for (int k = made.Count - 1; k >= 0 && total > cap + LevyConstants.MinionSlotEpsilon; k--)
			{
				Projectile p = made[k];
				if (!p.active || !p.minion || p.sentry || p.minionSlots <= ChildSlotMin) continue;
				total -= p.minionSlots;
				Dismiss(p);
				TrimTally++;
			}
		}
		/// <summary>
		/// The dummy's minion counters (numMinions, slotsMinions, ownedProjectileCounts) counted now from this soldier's own flock. PrepareTick does this once a tick, but only for a soldier that has a driven projectile left: a flock that just died
		/// left the counts of its last tick standing, and a staff's Shoot that finds "one of my minion is out" makes nothing (the Void Eater Marionette throws on it).
		/// </summary>
		internal static void RefreshCounts(SoldierNPC s)
		{
			Player d = s.Dummy;
			if (d == null) return;
			d.numMinions = 0; d.slotsMinions = 0f;
			Array.Clear(d.ownedProjectileCounts, 0, d.ownedProjectileCounts.Length);
			int me = s.NPC.whoAmI;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || p.owner != Slot) continue;
				if (!OwnedBy(p, me) && !IsDrivenChild(p)) continue;
				if (!(p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex == me)) continue;
				if (p.type < d.ownedProjectileCounts.Length) d.ownedProjectileCounts[p.type]++;
				if (p.minion) { d.numMinions++; d.slotsMinions += p.minionSlots; }
			}
		}

		/// <summary>(staff item type, minion slots of the holder) pairs where the staff's one minion was seen taking more slots than the holder has.</summary>
		internal static readonly HashSet<(int, int)> TooBig = new();

		/// <summary>Lab counter: pieces of a cast that did not fit his slots and were ended at once.</summary>
		internal static int TrimTally;

		/// <summary>True when the last <see cref="SummonReal"/> was a cast that powered up a minion already out (see <see cref="FlockChanged"/>).</summary>
		private static bool _lastStacked;
		/// <summary>Staves (item types) whose cast was seen powering up the minion already out (see <see cref="FlockChanged"/>): their minion is a flock of one that grows, never a self-ending cast or a summoner shot with a helper.</summary>
		private static readonly HashSet<int> Stackers = new();
		internal static bool IsStacker(int itemType) => Stackers.Contains(itemType);
		private static readonly List<(Projectile p, float a0, float a1, float a2, float slots, bool net)> _flockSnap = new();

		/// <summary>What this soldier's own minions look like before a cast (ai, slots, the net flag), so a cast that changes one of them instead of making a new one is told from an empty one.</summary>
		private static void SnapFlock(SoldierNPC s)
		{
			_flockSnap.Clear();
			int me = s.NPC.whoAmI;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (OwnedBy(p, me) && IsCast(p)) _flockSnap.Add((p, p.ai[0], p.ai[1], p.ai[2], p.minionSlots, p.netUpdate));
			}
		}

		/// <summary>True when the Shoot just run changed one of the minions <see cref="SnapFlock"/> saw (its ai or slots, or it flagged it for a net update).</summary>
		private static bool FlockChanged(SoldierNPC s)
		{
			foreach (var f in _flockSnap)
			{
				Projectile p = f.p;
				if (!p.active) continue;
				if (p.ai[0] != f.a0 || p.ai[1] != f.a1 || p.ai[2] != f.a2 || p.minionSlots != f.slots || (p.netUpdate && !f.net)) return true;
			}
			return false;
		}

		/// <summary>
		/// A RECAST use of a full flock's staff (see AltPolicies.RecastWanted): the staff's own Shoot run again with the flock out, as a player using it again at full slots does. Whatever it makes is the soldier's: a minion is
		/// adopted as a cast (a staff whose flock was not as full as it looked), anything else is tagged as his shot.
		/// </summary>
		internal static void Recast(SoldierNPC s, Item weapon, int damage)
		{
			if (!UsesRealShoot(weapon)) return;
			RecastTally++;
			foreach (Projectile p in RunShoot(s, weapon, damage, out _))
			{
				if (!p.active) continue;
				if (p.minion && !p.sentry) AdoptMade(s, weapon, p, damage, IsMainPiece(weapon, p));
				else WeaponUse.TagSoldierProjectile(s, p, weapon.DamageType ?? DamageClass.Summon);
			}
		}

		/// <summary>The staff's own CanUseItem with the soldier's dummy holding it (a staff that refuses a use while its ray is out, the Saros Possession, or while its field is out, the Universe Splitter). Any failure reads as "usable".</summary>
		internal static bool CanUse(SoldierNPC s, Item weapon)
		{
			Player d = s?.Dummy;
			if (d == null || weapon?.ModItem == null) return true;
			Item saved = d.inventory[0]; int sel = d.selectedItem;
			RefreshCounts(s);
			try { d.inventory[0] = weapon; d.selectedItem = 0; return ItemLoader.CanUseItem(weapon, d); }
			catch (Exception) { return true; }
			finally { d.inventory[0] = saved; d.selectedItem = sel; }
		}

		/// <summary>
		/// A staff whose own shot takes no slot (a summoner shot the minion comes out of) is swung again by a player when the flock is full, and its Shoot then makes something else: the Dormant Brimseeker's aura that
		/// empowers its Brimseekers. Learned here: the soldier's full flock gets the staff's Shoot now and then, and what it makes that takes no slot is kept; anything that would take a slot is ended. A staff that
		/// has never made such a helper after two tries is left alone.
		/// </summary>
		internal static void Helper(SoldierNPC s, Item weapon, int damage)
		{
			if (!UsesRealShoot(weapon) || Stackers.Contains(weapon.type)) return; // a staff that grows its minion: swung again it would grow it past the flock's slots
			Projectile sample = WeaponClassifier.ProjectileSample(weapon.shoot);
			if (sample == null || sample.minionSlots > ChildSlotMin) return;
			if (!Helpers.TryGetValue(weapon.type, out HelperInfo info)) Helpers[weapon.type] = info = new HelperInfo();
			if (!info.Seen && info.Probes >= 2) return;
			if (!CanUse(s, weapon)) return; // a player cannot swing it again while its own use gate is shut (the Universe Splitter while its field is out)
			if (!info.Seen) info.Probes++;
			HelperTally++; // the staff's own Shoot really runs
			foreach (Projectile p in RunShoot(s, weapon, damage, out _))
			{
				if (!p.active) continue;
				if (!p.minion || p.sentry) { WeaponUse.TagSoldierProjectile(s, p, weapon.DamageType ?? DamageClass.Summon); continue; }
				if (p.minionSlots > ChildSlotMin || p.type == weapon.shoot) { p.active = false; continue; } // a cast, not a helper: the flock is full (ended quietly: a Dismiss would run its OnKill)
				AdoptMade(s, weapon, p, damage, false);
				info.Seen = true;
			}
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
			GroupMinionsNow.Clear(); GroupMinionOwners.Clear(); DragonSegmentsNow.Clear(); DragonSegmentOwners.Clear(); DrivenNow.Clear(); DrivenNowOwners.Clear(); _drivenFirstSoldier = -1; _drivenMulti = false;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!IsDriven(p))
				{
					// a driven minion's own shot (kept on 255 in its frame) counts for its soldier as a player's own projectiles count for her: a minion that makes its ring of shots only while it owns none (the Void Concentration Staff),
					// a staff that refuses a use while its ray is out (the Saros Possession) and a minion that holds its fire while that ray lives all read these counts
					if (p.active && p.owner == Slot && !p.minion && p.TryGetGlobalProjectile(out SoldierProjectileTag ct) && ct.DrivenChild && ct.SoldierIndex >= 0 && MiscProjSwap.BodyOf(p) is SoldierNPC cb && cb.Dummy != null)
					{
						NoteDriven(p, ct.SoldierIndex);
						PrepDummy(cb, now, authority);
						if (p.type < cb.Dummy.ownedProjectileCounts.Length) cb.Dummy.ownedProjectileCounts[p.type]++;
					}
					continue;
				}
				// index the minions whose Update parks their neighbours (see ParkOtherFlocks / ParkOtherDragons): one entry per driven minion, with its soldier read once, here
				bool dragon = IsDragonType(p.type);
				if (p.owner == Slot) NoteDriven(p, p.GetGlobalProjectile<SoldierProjectileTag>().SoldierIndex);
				if (dragon || GroupIndexed.Contains(p.type))
				{
					int owner = p.GetGlobalProjectile<SoldierProjectileTag>().SoldierIndex;
					if (dragon) { DragonSegmentsNow.Add(p); DragonSegmentOwners.Add(owner); }
					else { GroupMinionsNow.Add(p); GroupMinionOwners.Add(owner); }
				}
				SoldierNPC s = MiscProjSwap.BodyOf(p);
				if (s == null) continue;
				Player d = s.Dummy;
				PrepDummy(s, now, authority);
				if (p.type < d.ownedProjectileCounts.Length) d.ownedProjectileCounts[p.type]++;
				if (p.type == ProjectileID.StormTigerGem) d.highestStormTigerGemOriginalDamage = Math.Max(d.highestStormTigerGemOriginalDamage, p.originalDamage);
				else if (p.type == ProjectileID.AbigailCounter) d.highestAbigailCounterOriginalDamage = Math.Max(d.highestAbigailCounterOriginalDamage, p.originalDamage);
			}
		}

		private static void NoteDriven(Projectile p, int soldier)
		{
			DrivenNow.Add(p); DrivenNowOwners.Add(soldier);
			if (_drivenFirstSoldier < 0) _drivenFirstSoldier = soldier; else if (soldier != _drivenFirstSoldier) _drivenMulti = true;
		}

		/// <summary>Once a tick per soldier, before its first driven projectile is counted: the dummy's minion counters zeroed, its clock set and its attack target chosen (see <see cref="PrepareTick"/>).</summary>
		private static void PrepDummy(SoldierNPC s, uint now, bool authority)
		{
			if (s.MinionPrepStamp == now) return;
			Player d = s.Dummy;
			s.MinionPrepStamp = now;
			d.numMinions = 0;
			d.slotsMinions = 0f;
			Array.Clear(d.ownedProjectileCounts, 0, d.ownedProjectileCounts.Length);
			d.highestStormTigerGemOriginalDamage = 0;
			d.highestAbigailCounterOriginalDamage = 0;
			d.miscCounter = (int)(now % 300u);
			d.MinionAttackTargetNPC = authority ? AttackTarget(s) : -1;
		}

		/// <summary>The enemy this soldier's minions are told to attack: the soldier's own target (incl. the commander's focus) when it is a live hostile inside the leash, else the nearest whip-tagged enemy in the leash, else -1 (vanilla proximity).</summary>
		internal static int AttackTarget(SoldierNPC s)
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

		/// <summary>
		/// A projectile created inside a driven minion's Update (a stinger, a spear, a fireball) is owned by 255 there; in single player a vanilla one is handed to the real owner so Projectile.Damage processes it.
		/// Two kinds are NOT handed over. A child that is itself a minion (the Dormant Brimseeker's Summoner makes its Brimseeker as it dies, a summoner's babies) stays a driven minion of the soldier: handed to the
		/// commander it would take the commander's own minion slots (vanilla's slot overflow ends it at once when they are full) and its AI would read the commander's buffs and flags. A modded shot keeps owner 255
		/// too and is updated in its parent's frame (<see cref="IsDrivenChild"/>): modded children find their parent by owner and identity (the Deathstare Rod's beam), which a hand-over breaks, and a driven frame
		/// already claims the owner slot for damage.
		/// </summary>
		internal static void ReownChild(Projectile parent, Projectile child)
		{
			if (parent.owner != Slot || child.owner != Slot || child.minion) return;
			bool fromChild = !parent.minion && !IsDrivenHelper(parent); // a gear helper's shot (Luxor's Gift fires from its helper) is handled like a minion's; a shot of a driven shot (the Gamma Hydra's canister bursts into homing bullets) stays on 255 like its parent, whatever its type
			if (fromChild && !IsDrivenChild(parent)) return;
			if ((fromChild || child.type >= ProjectileID.Count) && child.TryGetGlobalProjectile(out SoldierProjectileTag tag)) { tag.DrivenChild = true; return; } // on a dedicated server too: its parent lookups need the frame
			if (!Util.LevyUtil.SinglePlayerLike) return;
			child.owner = TrueMyPlayer;
		}

		/// <summary>A modded shot of a driven minion that stays on owner 255 and is updated in its minion's frame (see <see cref="ReownChild"/>).</summary>
		internal static bool IsDrivenChild(Projectile p) =>
			p.active && p.owner == Slot && !p.minion && p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.DrivenChild && tag.SoldierIndex >= 0;

		/// <summary>One bullet stack stood in the dummy's ammo slots for a modded minion's Update: a minion that fires "the owner's gun" asks the owner for ammo (Calamity's Black Hawk Remote, Cosmic Viper Engine, Tactical Plague Engine).</summary>
		private static Item _minionAmmo;
		private static int _ammoSlot = -1;
		private static void GiveMinionAmmo(Player d)
		{
			_ammoSlot = -1;
			for (int i = 54; i < 58 && i < d.inventory.Length; i++)
				if (d.inventory[i] == null || d.inventory[i].IsAir)
				{
					if (_minionAmmo == null) { _minionAmmo = new Item(); _minionAmmo.SetDefaults(ItemID.MusketBall); }
					_minionAmmo.stack = _minionAmmo.maxStack;
					d.inventory[i] = _minionAmmo; _ammoSlot = i;
					return;
				}
		}
		private static void TakeMinionAmmo(Player d)
		{
			if (_ammoSlot >= 0 && _ammoSlot < d.inventory.Length && ReferenceEquals(d.inventory[_ammoSlot], _minionAmmo)) d.inventory[_ammoSlot] = new Item();
			_ammoSlot = -1;
		}

		/// <summary>
		/// A modded minion's AI keeps itself alive through its owner's ModPlayer flag, which the staff's buff sets (<c>ModBuff.Update</c> raises the flag while the owner owns the minion and refreshes the
		/// buff time). The dummy owner carries the mods' ModPlayer instances, so the staff's buff is run on it here, each tick, through a scratch buff slot: the flag is up for the minion's Update and
		/// the dummy's real buff slot is put back untouched. A staff with no buff (a minion that needs none) is simply driven.
		/// </summary>
		/// <summary>Modded minion type -> the modded buff its AI added to the dummy owner (learned from the first Update; used when the staff sets no buffType).</summary>
		private static readonly Dictionary<int, int> LearnedBuff = new();
		private static void LearnBuff(Player d, Projectile p)
		{
			if (LearnedBuff.ContainsKey(p.type)) return;
			for (int i = 0; i < Math.Min(d.buffType.Length, d.buffTime.Length); i++)
			{
				int b = d.buffType[i];
				if (b < BuffID.Count || d.buffTime[i] <= 0) continue;
				bool had = false;
				for (int k = 0; k < _savedN; k++) if (SavedBuffType[k] == b) { had = true; break; }
				if (!had) { LearnedBuff[p.type] = b; return; }
			}
		}

		// sized by the dummy's own arrays, not Player.MaxBuffs: a mod's ExtraPlayerBuffSlots raises MaxBuffs after load, and a Player built before that keeps 44 slots
		private static int[] SavedBuffType = Array.Empty<int>(), SavedBuffTime = Array.Empty<int>();
		private static int _savedN;
		internal static void SaveBuffs(Player d)
		{
			_savedN = Math.Min(d.buffType.Length, d.buffTime.Length);
			if (SavedBuffType.Length < _savedN) { SavedBuffType = new int[_savedN]; SavedBuffTime = new int[_savedN]; }
			Array.Copy(d.buffType, SavedBuffType, _savedN); Array.Copy(d.buffTime, SavedBuffTime, _savedN);
		}
		internal static void RestoreBuffs(Player d) { int n = Math.Min(_savedN, Math.Min(d.buffType.Length, d.buffTime.Length)); Array.Copy(SavedBuffType, d.buffType, n); Array.Copy(SavedBuffTime, d.buffTime, n); }

		private static void ApplyModdedBuff(SoldierNPC body, Projectile p)
		{
			Item staff = body.SummonWeaponFor(p.type);
			int bt = staff != null && staff.buffType > 0 ? staff.buffType : LearnedBuff.GetValueOrDefault(p.type); // a staff that sets no Item.buffType: the buff the minion's own AI was seen adding to its owner
			ModBuff mb = bt > 0 ? ModContent.GetModBuff(bt) : null;
			if (mb == null) return;
			Player d = body.Dummy;
			if (p.type < d.ownedProjectileCounts.Length && d.ownedProjectileCounts[p.type] <= 0) d.ownedProjectileCounts[p.type] = 1; // cast earlier this tick, before the per-tick count
			int t0 = d.buffType[0], time0 = d.buffTime[0], idx = 0;
			try
			{
				d.buffType[0] = bt; d.buffTime[0] = 2;
				mb.Update(d, ref idx);
			}
			finally { d.buffType[0] = t0; d.buffTime[0] = time0; }
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
			bool swapped = false, parked = false, parkedDragons = false, buffsSaved = false, ammoGiven = false, aimed = false, parkedAll = false, savedChannel = d.channel;
			// a minion of a RECAST staff (AltPolicies.RecastWanted) decides between growing and firing from its owner's free slots: it sees the slots the soldier's upkeep keeps (not the driver's cap) and only his own flock
			bool recastMinion = p.minion && body.RecastShoot > 0 && InFamily(body.RecastShoot, p.type);
			// a modded minion in its first update (a cast's own birth tick), and in the two ticks after a cast of its soldier's staff (the staff's Shoot may have asked it to grow: the Void Eater Marionette), is told what a player's would be: his real maxMinions and only his own flock. One that sizes itself from the free slots it finds (the Void Eater Marionette adds as many
			// as it asks for and the owner has free) then gets what a player's gets, not the 1000 the driven update otherwise reports
			bool firstTick = authority && p.minion && !p.sentry && p.type >= ProjectileID.Count && !recastMinion && p.TryGetGlobalProjectile(out SoldierProjectileTag firstTag) && firstTag.Cast && (firstTag.Age == 0 || Main.GameUpdateCount <= body.TruthUntil);
			Vector2 savedScreen = default, calDelta = default; int savedMx = 0, savedMy = 0; float calRot = 0f; object cal = null;
			Projectile outerFrameProj = _frameProj; SoldierNPC outerFrameBody = _frameBody; int outerFrameSlot = _frameOwnerSlot; bool outerArmed = _strikeArmed, outerReal = _realContext;
			try
			{
				if (p.minion && p.timeLeft < LevyConstants.MinionLifeTicks / 2 && (!authority || IsCast(p))) p.timeLeft = LevyConstants.MinionLifeTicks; // vanilla keeps a minion alive while its buff flag is up; the soldier's upkeep is the buff here
				EnemyTargeting.Swap(body); // dummy -> Main.player[255], at the soldier
				swapped = true;
				if (ownerSlot != Slot) AliasDummy(ownerSlot, d); // a dragon segment reads Main.player[its own owner slot]: the same dummy stands there too
				d.maxMinions = recastMinion ? Math.Max(1, (int)Math.Round(body.MinionSlotBudget)) : firstTick ? Math.Max(1, savedMax) : LevyConstants.MinionDriverMaxMinions;
				if (body.RecastShoot > 0) d.channel = Main.GameUpdateCount < body.RecastHoldUntil; // the levy's button, held while a RECAST staff's channelled use lasts (the Saros ray lives while its owner channels)
				if (p.type >= ProjectileID.Count && p.TryGetGlobalProjectile(out SoldierProjectileTag at)) at.Age++;
				if (p.type >= ProjectileID.Count) { SaveBuffs(d); buffsSaved = true; if (!(p.TryGetGlobalProjectile(out SoldierProjectileTag ht) && ht.GearHelper)) ApplyModdedBuff(body, p); if (p.minion) { GiveMinionAmmo(d); ammoGiven = true; } } // a modded minion's AI re-adds its own buff to its owner every tick: the dummy's real buffs are put back in the finally (a gear helper is kept by the gear's own buff, not a staff's)
				if (authority && savedMyPlayer != ownerSlot) { _trueMyPlayer = savedMyPlayer; _faked = true; Main.myPlayer = ownerSlot; }
				if (GroupIndexed.Contains(p.type)) { parked = true; ParkOtherFlocks(p, body.NPC.whoAmI); } // flag first: a throw halfway still restores what was moved
				if (IsDragonType(p.type)) { parkedDragons = true; ParkOtherDragons(p, body.NPC.whoAmI); }
				// a modded minion that scans "every projectile of my owner" (Tenryu's dragons build their segment table that way and throw on a second soldier's tail) sees only its own soldier's projectiles, as it does for a player
				if (recastMinion || firstTick) { parkedAll = true; ParkOtherSoldiers(p, body.NPC.whoAmI); }
				else if (ParkModded && _drivenMulti && p.minion && p.type >= ProjectileID.Count && ownerSlot == Slot) { parkedAll = true; ModdedParks++; ParkOtherSoldiersIndexed(p, body.NPC.whoAmI); }
				// a gear helper whose AI reads its owner's cursor (Luxor's Gift turns to Calamity's per-player mouseWorld) sees the levy's aim, as a soldier shot does (OwnerPuppet): the dummy's own cursor is never set, and a cursor
				// on the owner's centre gives a zero direction, which made the helper's position NaN
				// the same for any modded minion or minion shot whose own code reads its owner's cursor (the Saros ray turns to Calamity's per-player cursor): on a single-player machine the plain cursor is the COMMANDER's mouse.
				// A RECAST staff's minion gets it too, for the shot it makes in its frame (the ray is aimed in its OnSpawn).
				if (p.ModProjectile != null && CombatFix.On(CombatFix.Cursor) && (recastMinion || OwnerLockProbe.ReadsCursor(p.type)))
				{
					Vector2 aim = body.AimCursor();
					savedScreen = Main.screenPosition; savedMx = Main.mouseX; savedMy = Main.mouseY; aimed = true;
					global::Levy.Common.Owner.AimView.Set(aim, body.NPC.Center);
					cal = AltPolicies.CalamityPlayerOf(d);
					if (cal != null) { (calDelta, calRot) = AltPolicies.GetCalAimPublic(cal); AltPolicies.SetCalAimPublic(cal, aim - d.MountedCenter); }
				}
				_frameProj = p; _frameBody = body; _frameOwnerSlot = ownerSlot; _strikeArmed = false; _realContext = false; // the context RunStrike lifts around a strike, and puts back
				if (LabThrowSoldier >= 0 && body.NPC.whoAmI == LabThrowSoldier && p.type == LabThrowType && LabThrows < 1) { LabThrows++; throw new InvalidOperationException("lab-injected throw (as another mod's hook would)"); }
				float slotsBefore = p.minionSlots;
				orig(p, i);
				// a modded minion that was just born, or that sized itself in this update from the 1000 slots its AI is told its owner has (the Void Eater Marionette adds the slots it asks for, Glacial Embrace's spikes take one each), is checked
				// against his real slots next tick, not at the next 20-tick upkeep: a player's own count of free slots stops it there
				if (authority && p.active && p.minion && !p.sentry && p.type >= ProjectileID.Count && p.minionSlots > ChildSlotMin && slotsBefore <= ChildSlotMin && IsCast(p))
				{
					Item sized = body.SummonWeaponFor(p.type);
					if (sized != null && IsMainPiece(sized, p) && LateSlotted.Add(sized.type)) ModContent.GetInstance<Levy>()?.Logger.Info("Levy: " + sized.Name + "'s minion takes its slots in its own update: a minion staff, not a self-ending cast.");
				}
				if (authority && p.active && p.minion && !p.sentry && p.type >= ProjectileID.Count && (Math.Abs(p.minionSlots - slotsBefore) > 0.001f || (p.TryGetGlobalProjectile(out SoldierProjectileTag ft) && ft.Age <= 1))) body.FlockDirty = true;
			}
			catch (Exception e)
			{
				// never take the game down for one odd minion: log it once, drop this one, and let its staff use the stand-in from now on
				MarkBad(p, body, e);
				try { if (authority && p.active) Dismiss(p); } catch (Exception) { }
				p.active = false;
			}
			finally
			{
				_frameProj = outerFrameProj; _frameBody = outerFrameBody; _frameOwnerSlot = outerFrameSlot; _strikeArmed = outerArmed; _realContext = outerReal;
				if (aimed) { Main.screenPosition = savedScreen; Main.mouseX = savedMx; Main.mouseY = savedMy; if (cal != null) AltPolicies.SetCalAimPublic(cal, calDelta, calRot); }
				if (parked) UnparkOtherFlocks();
				if (parkedDragons) UnparkOtherDragons();
				if (parkedAll) UnparkOtherSoldiers();
				if (body.RecastShoot > 0) d.channel = savedChannel;
				Main.myPlayer = savedMyPlayer;
				_faked = false;
				d.maxMinions = savedMax;
				if (ammoGiven) TakeMinionAmmo(d);
				if (buffsSaved) { LearnBuff(d, p); RestoreBuffs(d); }
				if (swapped) EnemyTargeting.Unswap();
				EnsureAliasRestored(); // never leave a dummy in the dragon's owner slot
				EnemyTargeting.EnsureSlotRestored(); // never leave a dummy in Main.player[255]
			}

			// keep remote screens honest: the server re-sends every real minion now and then (vanilla only sends when the AI flags a change)
			if (Main.netMode == NetmodeID.Server && p.active && ((uint)p.whoAmI + Main.GameUpdateCount) % (uint)LevyConstants.MinionNetRefreshTicks == 0)
				NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, p.whoAmI);
		}

		// =====================================================================================================
		// The kill cascade (see the class summary): Projectile.Damage arms a window, NPC.StrikeNPC lifts the swap inside it.
		// =====================================================================================================
		/// <summary>True when <paramref name="p"/> is the driven minion whose Update is running under the dummy swap right now (the one cheap test the Damage detour runs first).</summary>
		internal static bool IsFrameProjectile(Projectile p) => ReferenceEquals(p, _frameProj);

		/// <summary>
		/// Projectile.GetByUUID answers -1 for owner 255 (it only takes owners below 255), so a modded minion's part that finds its parent that way (the Mounted Scanner's laser, the Endo Hydra's heads) would find nothing
		/// under the shared soldier owner. Inside a driven minion's frame the lookup is answered from the projectile array instead: the part of owner 255 whose identity or UUID is the one asked for. Elsewhere vanilla's answer stands.
		/// </summary>
		internal static int UuidLookup(int owner, int uuid)
		{
			if (_frameProj == null || owner != Slot || uuid < 0 || uuid >= Main.maxProjectiles) return -1;
			Projectile q = Main.projectile[uuid];
			if (q.active && q.owner == Slot && (q.identity == uuid || q.projUUID == uuid)) return uuid;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				q = Main.projectile[i];
				if (q.active && q.owner == Slot && (q.identity == uuid || q.projUUID == uuid)) return i;
			}
			return -1;
		}

		/// <summary>
		/// The one Projectile.Damage call of a driven minion mid-Update (called from the Damage detour on the server / single player only). The projectile keeps its owner and the dummy stays in
		/// place for all the arithmetic; the call just arms <see cref="RunStrike"/> so the NPC.StrikeNPC inside it, and the death cascade that strike runs synchronously, happens in the real-player
		/// context. Nested-safe: the previous armed state is put back in the finally.
		/// </summary>
		internal static void RunDamage(On_Projectile.orig_Damage orig, Projectile p)
		{
			if (!ReferenceEquals(p, _frameProj) || _frameBody == null || !LevyUtil.IsServerOrSingle) { orig(p); return; }
			bool outerArmed = _strikeArmed;
			_strikeArmed = true;
			try { orig(p); }
			finally { _strikeArmed = outerArmed; }
		}

		/// <summary>
		/// NPC.StrikeNPC detour. Outside an armed driven-minion Damage call (every other strike in the game) it is a flag test and a straight call to orig. Inside one it lifts the Update's whole
		/// context for the length of the strike, which is where a lethal hit runs HitEffect, NPC.checkDead and NPCLoot (every mod's OnKill, event and boss-log bookkeeping) synchronously:
		///  * Main.myPlayer goes back to the real player (single player; a dedicated server already is 255 and is left alone);
		///  * the dummy leaves Main.player[255] (EnemyTargeting.Unswap puts the real null player back) and, for a Stardust Dragon segment, leaves its owner slot too.
		/// The finally puts all of it back in the order <see cref="RunUpdate"/> built it, so the rest of the Damage call and of the Update see exactly what they always did. The cascade therefore
		/// resolves as it does for a normal soldier shot. Credit is not touched here: LootCredit has already named the commander (ModifyHitNPC runs before the strike) and the strike itself is
		/// npcProj (noPlayerInteraction), so there is no second interaction and no loot credit for slot 255.
		/// </summary>
		internal static int RunStrike(On_NPC.orig_StrikeNPC_HitInfo_bool_bool orig, NPC npc, NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction)
		{
			SoldierNPC body = _frameBody;
			if (!_strikeArmed || _realContext || body == null || body.Dummy == null) return orig(npc, hit, fromNet, noPlayerInteraction);

			int ownerSlot = _frameOwnerSlot;
			bool wasFaked = _faked;
			int fakedMyPlayer = Main.myPlayer, trueMyPlayer = _trueMyPlayer;
			try
			{
				if (wasFaked) { Main.myPlayer = trueMyPlayer; _faked = false; }
				EnemyTargeting.Unswap();
				EnsureAliasRestored();
				_realContext = true;
				return orig(npc, hit, fromNet, noPlayerInteraction);
			}
			finally
			{
				_realContext = false;
				if (wasFaked) { _trueMyPlayer = trueMyPlayer; _faked = true; Main.myPlayer = fakedMyPlayer; }
				EnemyTargeting.Swap(body); // dummy -> Main.player[255] again (Swap lifts any swap left over first)
				if (ownerSlot != Slot) AliasDummy(ownerSlot, body.Dummy);
			}
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

		private static readonly List<Projectile> ParkedAll = new();
		/// <summary>
		/// Every OTHER soldier's minion (any type) sits on the parked owner for the length of the call: a minion that sums "every minion of my owner" to find its free slots (the Saros aura, Sirius, the Void Eater Marionette) would
		/// otherwise count the whole army's minions on the shared slot 255. Used for RECAST staves' minions, whose growth and ray depend on it; one walk of the projectile array per such update.
		/// </summary>
		private static void ParkOtherSoldiers(Projectile p, int soldierIndex)
		{
			ParkedAll.Clear();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile q = Main.projectile[i];
				if (!q.active || q.whoAmI == p.whoAmI || q.owner != Slot || !q.minion || !q.TryGetGlobalProjectile(out SoldierProjectileTag t) || t.SoldierIndex < 0 || t.SoldierIndex == soldierIndex) continue;
				q.owner = ParkedOwner;
				ParkedAll.Add(q);
			}
		}

		/// <summary>The same as <see cref="ParkOtherSoldiers"/> from this tick's index of driven projectiles (no walk of the whole projectile array), and the shots a driven minion keeps on 255 go too: one cheap pass per modded minion update, and only when a second soldier has any.</summary>
		private static void ParkOtherSoldiersIndexed(Projectile p, int soldierIndex)
		{
			ParkedAll.Clear();
			for (int k = 0; k < DrivenNow.Count; k++)
			{
				if (DrivenNowOwners[k] == soldierIndex) continue;
				Projectile q = DrivenNow[k];
				if (!q.active || q.whoAmI == p.whoAmI || q.owner != Slot) continue;
				q.owner = ParkedOwner;
				ParkedAll.Add(q);
			}
		}

		private static void UnparkOtherSoldiers()
		{
			foreach (Projectile q in ParkedAll) if (q.owner == ParkedOwner) q.owner = Slot;
			ParkedAll.Clear();
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
		internal static void AliasDummy(int slot, Player dummy)
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

	/// <summary>
	/// Installs the Projectile.Update wrapper for real minions and the per-tick dummy preparation; keeps slot 255 clean around the projectile phase. Also installs the NPC.StrikeNPC
	/// interposer that runs a driven minion's kill cascade in the real-player context (armed by the Projectile.Damage detour in <see cref="HeldWeaponSystem"/>; see <see cref="SoldierMinions.RunStrike"/>).
	/// </summary>
	public class SoldierMinionSystem : ModSystem
	{
		public override void Load()
		{
			Logging.IgnoreExceptionContents("lab-injected throw"); // the summon A/B's bench check throws on purpose; tML's own "silently caught" log line would count as an exception in the ship report
			On_Projectile.Update += UpdateDetour;
			On_NPC.StrikeNPC_HitInfo_bool_bool += StrikeDetour;
			On_Projectile.GetByUUID_int_int += UuidDetour;
			On_Player.CanPullItem += PullDetour;
		}

		/// <summary>
		/// A soldier's dummy never pulls an item to itself: it has no real inventory and never picks anything up, so a minion that fetches the items its owner could pull (the Void Eater Marionette plays fetch whenever an item lies within
		/// 50 tiles) carried one to the levy forever and stopped fighting (summon sweep 2026-11-22: 64 damage a second instead of 5k with a stack of gel on the floor).
		/// </summary>
		private static bool PullDetour(On_Player.orig_CanPullItem orig, Player self, Item item, Player.ItemSpaceStatus status)
		{ _hl_PullDetour_1++;
			// vanilla asks this for every item near every player each tick: a real player in its own slot is answered at once
			if (self.whoAmI < Main.maxPlayers - 4 && ReferenceEquals(Main.player[self.whoAmI], self)) return orig(self, item, status);
			return !global::Levy.Common.Owner.SoldierOwner.IsDummy(self) && orig(self, item, status);
		} private static int _hl_PullDetour_1;

		private static int UuidDetour(On_Projectile.orig_GetByUUID_int_int orig, int owner, int uuid)
		{ _hl_UuidDetour_2++;
			int r = orig(owner, uuid);
			return r >= 0 || owner != SoldierNPC.DummySlot ? r : SoldierMinions.UuidLookup(owner, uuid);
		} private static int _hl_UuidDetour_2;

		public override void Unload()
		{
			On_Projectile.Update -= UpdateDetour;
			On_NPC.StrikeNPC_HitInfo_bool_bool -= StrikeDetour;
			On_Projectile.GetByUUID_int_int -= UuidDetour;
			On_Player.CanPullItem -= PullDetour;
			SoldierMinions.Reset();
			EnemyTargeting.EnsureSlotRestored();
		}

		public override void OnWorldLoad() => SoldierMinions.Reset();
		public override void OnWorldUnload() { SoldierMinions.Reset(); EnemyTargeting.EnsureSlotRestored(); }

		public override void PreUpdateProjectiles()
		{
			EnemyTargeting.EnsureSlotRestored();
			SoldierMinions.EnsureAliasRestored();
			SoldierMinions.ClearFrame();
			SoldierMinions.PrepareTick();
		}

		public override void PostUpdateProjectiles()
		{
			SoldierMinions.EnsureAliasRestored();
			SoldierMinions.ClearFrame();
			EnemyTargeting.EnsureSlotRestored();
		}

		private static void UpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{ _hl_UpdateDetour_1++;
			HangWatch.Proj(self); // diagnostic breadcrumb (see HangWatch): which projectile's Update started last
			if (self.minion ? SoldierMinions.IsDriven(self) : (self.owner == SoldierNPC.DummySlot && (SoldierMinions.IsDrivenChild(self) || SoldierMinions.IsDrivenHelper(self)))) SoldierMinions.RunUpdate(orig, self, i);
			else orig(self, i);
		} private static int _hl_UpdateDetour_1;

		private static int _hl_StrikeDetour_1;
		private static int StrikeDetour(On_NPC.orig_StrikeNPC_HitInfo_bool_bool orig, NPC self, NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction)
		{ _hl_StrikeDetour_1++; return SoldierMinions.RunStrike(orig, self, hit, fromNet, noPlayerInteraction); }
	}
}
