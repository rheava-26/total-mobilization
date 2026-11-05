using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Config;
using Levy.Common.Globals;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Common.Weapons;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// GEAR UPKEEP: the per-tick slice of <c>Player.Update</c> that the soldier's hidden dummy never ran, so worn gear works for a levy the way it does for a player.
	/// <para/>The dummy only ever got <c>RefreshStats</c> (one ResetEffects + UpdateEquips + UpdateArmorSets a second). Everything gear does ACROSS TICKS therefore never happened:
	/// the timers <c>Player.Update</c> counts down (petalTimer, boneGloveTimer, shadowDodgeTimer, crystalLeafCooldown), the counters UpdateEquips advances (Volatile Gelatin), the armor-set effects that cast
	/// only when <c>whoAmI == Main.myPlayer</c>, mod buffs, and the mod hooks Player.Update runs after the equips (Calamity regenerates rogue stealth in PostUpdateMiscEffects,
	/// which is why a rogue levy's stealth was always 0).
	/// <para/>Each tick, for a soldier whose gear is tick-dependent (<see cref="UpkeepActive"/>; every other soldier pays nothing), <see cref="GearFrame"/> runs on the dummy:
	/// timers, ResetEffects (then the owned-projectile counts are put back: ResetEffects ends in ResetProjectileCaches and would zero them), the buff pass, UpdateEquips, the by-hand extra accessory, the set pass,
	/// the station and potion stat deltas, PostUpdateEquips and PostUpdateMiscEffects. RefreshStats runs the very same frame once a second and reads the stats after it, so the dummy never holds a half-built stat set.
	/// <para/>The frame borrows the soldier's owner slot and position exactly as <c>RunAccessoryProc</c> does (whoAmI = <see cref="WeaponUse.OwnerFor"/>, Center = the body) and points Thorium's local-player cache at
	/// the dummy for its length (<see cref="OwnerPuppet.SwapThorium"/>), so Thorium gear never touches the commander. Projectiles it spawns are caught as they are created and adopted
	/// (<see cref="WeaponUse.TagSoldierProjectile"/>). Ownership of weapon shots is untouched.
	/// <para/>Hand-made duplicates: items the real frame now drives switch their twin off (<see cref="GearUpkeep.Mode"/> 2): Volatile Gelatin. What the frame would also spawn but a hand-made twin covers
	/// (Crystal Leaf, Stardust Guardian, Spore Sac) stays hand-made: those projectiles are bound to the commander's flags in vanilla (a later step), so the sets are run on a slot nobody holds and any stray spawn is removed and counted.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Vanilla accessories whose effect lives in a per-tick step the dummy never ran (an UpdateEquips counter, a timer, or a hurt reaction).</summary>
		private static readonly HashSet<int> UpkeepVanillaAccessories = new() { ItemID.VolatileGelatin, ItemID.BoneGlove, ItemID.StarCloak, ItemID.StarVeil, ItemID.BeeCloak, ItemID.CrimsonCloak };

		/// <summary>True when the soldier's worn gear needs the per-tick frame: any modded armor or accessory (their hooks are the point), or a vanilla piece in <see cref="UpkeepVanillaAccessories"/>. Set in RefreshStats.</summary>
		private bool _upkeepGear;
		private int _upkeepFails; private uint _upkeepPausedUntil;
		private bool _upkeepWarned;
		private int[] _upkeepCounts;
		/// <summary>Tick (Main.GameUpdateCount) a weapon use last began for this soldier (WeaponUse.FinishUse). The next frame lets the dummy see it as the first frame of an item use.</summary>
		internal uint LastUseTick;

		/// <summary>The soldier's gear is tick-dependent and the real upkeep is enabled (config / harness mode), the stat pipeline is up, and the soldier is not paused after failures.</summary>
		public bool UpkeepActive => _upkeepGear && GearUpkeep.Mode > 0 && !OfficerMode && Record != null && Dummy != null && PipelineOk && Main.GameUpdateCount >= _upkeepPausedUntil;

		private bool ComputeUpkeepNeed()
		{
			var g = Record?.Gear; if (g == null) return false;
			if (NeedsUpkeep(g.Head) || NeedsUpkeep(g.Body) || NeedsUpkeep(g.Legs)) return true;
			int slots = Math.Min(Record.AccessorySlots, g.Accessories.Length);
			for (int i = 0; i < slots; i++) if (NeedsUpkeep(g.Accessories[i])) return true;
			return false;
		}

		private static bool NeedsUpkeep(Item it) => it != null && !it.IsAir && (it.type >= ItemID.Count || UpkeepVanillaAccessories.Contains(it.type));

		/// <summary>True when this soldier's Volatile Gelatin is driven by the real UpdateEquips step, so the hand-made twin stands down.</summary>
		private bool RealGelatin => UpkeepActive && GearUpkeep.Mode >= 2;

		// ------------------------------------------------------------------------------------------------------------
		// The frame
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>One tick of upkeep for a soldier that is not being refreshed this tick (RefreshStats runs the same frame itself).</summary>
		private void GearTick()
		{
			if (!UpkeepActive || !IsAlive) return;
			GearUpkeep.PrepareCountsOncePerTick();
			long t0 = Stopwatch.GetTimestamp();
			try { GearFrame(); _upkeepFails = 0; }
			catch (Exception e) { UpkeepFailed(e); }
			GearUpkeep.Ticks += Stopwatch.GetTimestamp() - t0; GearUpkeep.Frames++;
		}

		private void UpkeepFailed(Exception e)
		{
			if (e is System.Reflection.TargetInvocationException { InnerException: not null } t) e = t.InnerException;
			if (++_upkeepFails >= 3)
			{
				_upkeepPausedUntil = Main.GameUpdateCount + (uint)LevyConstants.PipelineRetryTicks; _upkeepFails = 0;
				if (!_upkeepWarned) { _upkeepWarned = true; Levy.Instance?.Logger.Warn("Levy: gear upkeep failed on soldier '" + SoldierName + "' (" + GearSummary() + "), paused for " + LevyConstants.PipelineRetryTicks / 60 + "s: " + e); }
			}
			GearUpkeep.Failures++;
		}

		/// <summary>
		/// The per-tick slice, on the dummy with its gear equipped (EquipDummy). The reset, equips and set pass throw to the caller; the mod hooks around them are individually guarded.
		/// </summary>
		private void GearFrame()
		{
			Player d = Dummy;
			int life0 = d.statLife, mana0 = d.statMana;
			int savedWho = d.whoAmI; Vector2 savedCenter = d.Center;
			float savedSlots = d.slotsMinions; int savedNum = d.numMinions;
			int hs = d.highestStormTigerGemOriginalDamage, ha = d.highestAbigailCounterOriginalDamage;
			int ownerSlot = WeaponUse.OwnerFor(this);
			object thorium = null;
			var prev = GearUpkeep.Body; GearUpkeep.Body = this;
			int spawnStart = GearUpkeep.Spawned.Count;
			try
			{
				d.whoAmI = ownerSlot;
				d.Center = NPC.Center;
				thorium = OwnerPuppet.SwapThorium(d, true);

				// The timers Player.Update counts down once a tick.
				if (d.petalTimer > 0) d.petalTimer--;
				if (d.shadowDodgeTimer > 0) d.shadowDodgeTimer--;
				if (d.boneGloveTimer > 0) d.boneGloveTimer--;
				if (d.crystalLeafCooldown > 0) d.crystalLeafCooldown--;

				// ResetEffects ends in ResetProjectileCaches, which zeroes the owned counts: put them back (PrepareCountsOncePerTick filled them) or the Stardust Guardian is cast every tick.
				int[] counts = d.ownedProjectileCounts;
				if (_upkeepCounts == null || _upkeepCounts.Length != counts.Length) _upkeepCounts = new int[counts.Length];
				Array.Copy(counts, _upkeepCounts, counts.Length);
				d.ResetEffects();
				Array.Copy(_upkeepCounts, d.ownedProjectileCounts, counts.Length);
				d.numMinions = savedNum; d.slotsMinions = savedSlots; d.highestStormTigerGemOriginalDamage = hs; d.highestAbigailCounterOriginalDamage = ha;

				// Buffs: only the mod's. A vanilla buff on the dummy comes from a set bonus whose stat side is built by hand (ApplyPotionBuffs / ApplySetStats), which would count twice.
				RunHook(() =>
				{
					for (int b = 0; b < d.buffType.Length; b++) if (d.buffType[b] > 0 && d.buffType[b] < BuffID.Count) d.DelBuff(b);
					PlayerLoader.PreUpdateBuffs(d);
					for (int b = 0; b < d.buffType.Length; b++) if (d.buffType[b] > 0) { d.UpdateBuffs(ownerSlot); break; }
					PlayerLoader.PostUpdateBuffs(d);
				});

				d.UpdateEquips(ownerSlot);
				ApplyExtraAccessories(d);
				// The sets Levy builds by hand (Chlorophyte leaf, Stardust Guardian) run on a slot nobody holds, so vanilla's own cast (a minion bound to the commander) never fires.
				UpdateDummyArmorSets(WearsChlorophyteSet || WearsStardustSet ? DummySlot - 2 : ownerSlot);
				for (int b = 0; b < d.buffType.Length; b++) if (d.buffType[b] == BuffID.LeafCrystal) d.DelBuff(b);
				try { StationBuffSystem.Apply(d, Stations); } catch (Exception e) { Levy.Instance.Logger.Warn("Levy: station buffs failed on a soldier: " + e.Message); }
				try { _buffLifeRegen = ApplyPotionBuffs(d); } catch (Exception e) { _buffLifeRegen = 0; Levy.Instance.Logger.Warn("Levy: potion buffs failed on a soldier: " + e.Message); }

				// Player.Update's tail: the mod hooks that read the finished equip pass. Calamity regenerates rogue stealth in the last.
				RunHook(() => PlayerLoader.PostUpdateEquips(d));
				// The dummy never runs ItemCheck, so it never "uses" its weapon; mod hooks that key off the first frame of an item use (Calamity spends rogue stealth there) see the soldier's use as it begins.
				int animSaved = d.itemAnimation, animMaxSaved = d.itemAnimationMax, timeSaved = d.itemTime;
				Item held = d.inventory[d.selectedItem];
				if (LastUseTick != 0 && Main.GameUpdateCount - LastUseTick == 1 && held != null && !held.IsAir)
				{
					d.itemAnimationMax = Math.Max(1, held.useAnimation); d.itemAnimation = d.itemAnimationMax - 1;
					d.itemTime = Math.Max(1, (int)(held.useTime / Math.Max(0.1f, d.GetTotalAttackSpeed(held.DamageType))));
					GearUpkeep.UseSignals++;
				}
				else { d.itemAnimation = 0; d.itemAnimationMax = 0; d.itemTime = 0; }
				RunHook(() => PlayerLoader.PostUpdateMiscEffects(d));
				d.itemAnimation = animSaved; d.itemAnimationMax = animMaxSaved; d.itemTime = timeSaved;
			}
			finally
			{
				d.whoAmI = savedWho; d.Center = savedCenter;
				OwnerPuppet.RestoreThorium(thorium);
				GearUpkeep.Body = prev;
				AdoptUpkeepSpawns(spawnStart);
				WriteBackVitals(d, life0, mana0);
			}
		}

		private void RunHook(Action a)
		{
			try { a(); }
			catch (Exception e)
			{
				if (e is System.Reflection.TargetInvocationException { InnerException: not null } t) e = t.InnerException;
				GearUpkeep.HookFailures++;
				if (GearUpkeep.WarnOnce(e)) Levy.Instance?.Logger.Warn("Levy: a gear hook threw during upkeep (once per kind): " + e);
			}
		}

		/// <summary>The frame may have healed, spent or restored the dummy's life and mana (Spectre-style heals, life-spending items, mana hooks): write the change back, or the next sync overwrites it. Never kills the soldier.</summary>
		private void WriteBackVitals(Player d, int life0, int mana0)
		{
			d.dead = false; d.ghost = false;
			int dl = d.statLife - life0;
			if (dl != 0 && NPC.active && NPC.life > 0)
			{
				int to = Math.Clamp(NPC.life + dl, 1, Math.Max(1, NPC.lifeMax));
				if (to != NPC.life) { NPC.life = to; NPC.netUpdate = true; if (Record != null) Record.CurrentHp = NPC.life; GearUpkeep.LifeWriteBacks++; }
			}
			d.statLife = NPC.life;
			int dm = d.statMana - mana0;
			if (dm != 0 && Record != null && Record.MaxMana > 0) { Record.Mana = Math.Clamp(Record.Mana + dm, 0, Record.MaxMana); GearUpkeep.ManaWriteBacks++; }
			SyncDummyMana();
		}

		/// <summary>Adopts what the frame spawned. A projectile a hand-made twin already covers is removed (and counted) so the soldier never has two of it.</summary>
		private void AdoptUpkeepSpawns(int start)
		{
			var list = GearUpkeep.Spawned;
			try
			{
				for (int k = start; k < list.Count; k++)
				{
					int idx = list[k];
					if (idx < 0 || idx >= Main.maxProjectiles) continue;
					Projectile p = Main.projectile[idx];
					if (!p.active) continue;
					if (GearUpkeep.CoveredByHandMade(p)) { GearUpkeep.Dropped[p.type] = GearUpkeep.Dropped.TryGetValue(p.type, out int n) ? n + 1 : 1; p.active = false; p.timeLeft = 0; continue; }
					WeaponUse.TagSoldierProjectile(this, p, p.DamageType);
					GearUpkeep.AdoptedByType[p.type] = GearUpkeep.AdoptedByType.TryGetValue(p.type, out int c) ? c + 1 : 1;
					GearUpkeep.Adopted++;
				}
			}
			catch (Exception e) { if (_accessoryProcWarned.Add(-ItemID.VolatileGelatin)) Levy.Instance?.Logger.Warn("Levy: adopting upkeep projectiles failed: " + e.Message); }
			finally { if (start == 0) list.Clear(); else if (list.Count > start) list.RemoveRange(start, list.Count - start); }
		}

		// ------------------------------------------------------------------------------------------------------------
		// Hurt-driven gear (Star Cloak, mod on-hurt hooks): Player.Hurt on the dummy from the soldier's damage intake
		// ------------------------------------------------------------------------------------------------------------
		private bool _inGearHurt;

		/// <summary>
		/// Runs the dummy's own <c>Player.Hurt</c> for a hit the soldier just took, so accessories that react to being hurt (Star Cloak's falling stars, mod on-hurt hooks) fire. The damage itself was
		/// already dealt to the NPC: the dummy is given room to take it and cannot die, and only a heal the hooks add is written back.
		/// </summary>
		internal void GearOnHurt(int damage, int attackerIndex)
		{
			if (_inGearHurt || damage <= 0 || !UpkeepActive || LevyUtil.IsClient || !IsAlive) return;
			_inGearHurt = true;
			Player d = Dummy;
			int savedWho = d.whoAmI; Vector2 savedCenter = d.Center; int savedLife = d.statLife, savedOrbs = d.beetleOrbs, savedShields = d.solarShields;
			object thorium = null;
			var prev = GearUpkeep.Body; GearUpkeep.Body = this;
			int spawnStart = GearUpkeep.Spawned.Count;
			try
			{
				d.whoAmI = WeaponUse.OwnerFor(this); d.Center = NPC.Center;
				thorium = OwnerPuppet.SwapThorium(d, true);
				int room = Math.Max(d.statLifeMax2, 1) + damage + 1000;
				d.statLife = room; d.immune = false; d.immuneTime = 0; d.dead = false;
				savedOrbs = d.beetleOrbs; savedShields = d.solarShields; d.beetleOrbs = 0; d.solarShields = 0; // the beetle orb / solar shield losses on a hit are built by hand (SetsOnHurt): vanilla's own would count twice
				NPC src = attackerIndex >= 0 && attackerIndex < Main.maxNPCs && Main.npc[attackerIndex].active ? Main.npc[attackerIndex] : null;
				var reason = src != null ? PlayerDeathReason.ByNPC(src.whoAmI) : PlayerDeathReason.ByCustomReason(Terraria.Localization.NetworkText.FromLiteral(SoldierName));
				var info = new Player.HurtInfo { DamageSource = reason, Damage = damage, HitDirection = src != null && src.Center.X > NPC.Center.X ? -1 : 1, CooldownCounter = -1, Dodgeable = false, Knockback = 0f, PvP = false, SoundDisabled = true, DustDisabled = true };
				d.Hurt(info, true);
				int heal = d.statLife - (room - damage);
				if (heal > 0 && NPC.life > 0) { NPC.life = Math.Min(NPC.lifeMax, NPC.life + heal); NPC.netUpdate = true; if (Record != null) Record.CurrentHp = NPC.life; GearUpkeep.LifeWriteBacks++; }
				GearUpkeep.HurtCalls++;
			}
			catch (Exception e)
			{
				GearUpkeep.HookFailures++;
				if (GearUpkeep.WarnOnce(e)) Levy.Instance?.Logger.Warn("Levy: Player.Hurt on a soldier dummy threw (once per kind): " + e);
			}
			finally
			{
				d.whoAmI = savedWho; d.Center = savedCenter; d.statLife = NPC.life > 0 ? NPC.life : savedLife; d.beetleOrbs = savedOrbs; d.solarShields = savedShields; d.dead = false; d.immune = false; d.immuneTime = 0;
				OwnerPuppet.RestoreThorium(thorium);
				GearUpkeep.Body = prev;
				AdoptUpkeepSpawns(spawnStart);
				_inGearHurt = false;
			}
		}
	}

	/// <summary>Shared state for the gear upkeep: mode switch, the spawn catcher's list, and the counters the harness reports.</summary>
	public static class GearUpkeep
	{
		private static int _envMode = -1, _override = -1;
		/// <summary>0 off · 1 frame on, hand-made duplicates left on (measures the doubling) · 2 frame on, verified duplicates off. Default 2 when the config allows it; env LEVY_UPKEEP and the harness override win.</summary>
		public static int Mode
		{
			get
			{
				if (_override >= 0) return _override;
				if (_envMode < 0) { string v = Environment.GetEnvironmentVariable("LEVY_UPKEEP"); _envMode = !string.IsNullOrEmpty(v) && int.TryParse(v, out int m) ? Math.Clamp(m, 0, 2) : 100; }
				if (_envMode != 100) return _envMode;
				try { return LevyConfig.Get.RealGearUpkeep ? 2 : 0; } catch (Exception) { return 2; }
			}
		}
		internal static void SetOverride(int mode) { _override = mode; }

		/// <summary>The soldier whose frame is running (the projectile catcher records into <see cref="Spawned"/> only then).</summary>
		internal static SoldierNPC Body;
		internal static readonly List<int> Spawned = new();

		// counters (harness)
		public static long Ticks; public static int Frames, Failures, HookFailures, Adopted, LifeWriteBacks, ManaWriteBacks, HurtCalls, UseSignals;
		public static readonly Dictionary<int, int> Dropped = new(), AdoptedByType = new();
		private static readonly HashSet<string> _warned = new();
		internal static bool WarnOnce(Exception e) => _warned.Add(e.GetType().Name + "@" + (e.StackTrace ?? "").Split('\n')[0].Trim());
		internal static void ResetCounters() { Ticks = 0; Frames = Failures = HookFailures = Adopted = LifeWriteBacks = ManaWriteBacks = HurtCalls = UseSignals = 0; Dropped.Clear(); AdoptedByType.Clear(); }

		/// <summary>Projectiles whose job a hand-made twin already does (the leaf and the Guardian are bound to the commander in vanilla; Spore Sac keeps its per-soldier trap cap by hand).</summary>
		internal static bool CoveredByHandMade(Projectile p) => p.type == ProjectileID.CrystalLeaf || p.type == ProjectileID.StardustGuardian || p.aiStyle == ProjAIStyleID.SporeTrap;

		private static uint _prepTick = uint.MaxValue;
		/// <summary>Once a tick, before the first frame: the owned-projectile counts of every upkeep soldier's dummy, recounted from the soldier-tagged projectiles alive now (vanilla counts these in Player.Update).</summary>
		internal static void PrepareCountsOncePerTick()
		{
			uint now = Main.GameUpdateCount;
			if (_prepTick == now) return;
			_prepTick = now;
			bool any = false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC s || !s.UpkeepActive) continue;
				Array.Clear(s.Dummy.ownedProjectileCounts, 0, s.Dummy.ownedProjectileCounts.Length); any = true;
			}
			if (!any) return;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || !p.TryGetGlobalProjectile(out SoldierProjectileTag t) || t.SoldierIndex < 0 || t.SoldierIndex >= Main.maxNPCs) continue;
				NPC n = Main.npc[t.SoldierIndex];
				if (!n.active || n.ModNPC is not SoldierNPC s || !s.UpkeepActive) continue;
				int[] c = s.Dummy.ownedProjectileCounts;
				if (p.type < c.Length) c[p.type]++;
			}
		}
	}

	/// <summary>Records every projectile created while a soldier's upkeep frame (or its Hurt call) is running, so the frame adopts exactly those.</summary>
	public class GearUpkeepSpawnCatcher : GlobalProjectile
	{
		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (GearUpkeep.Body != null && GearUpkeep.Spawned.Count < 4096) GearUpkeep.Spawned.Add(projectile.whoAmI);
		}
	}
}
