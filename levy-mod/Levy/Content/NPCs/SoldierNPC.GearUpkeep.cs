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
		private static readonly HashSet<int> UpkeepVanillaAccessories = new() { ItemID.VolatileGelatin, ItemID.StarCloak, ItemID.StarVeil, ItemID.BeeCloak, ItemID.CrimsonCloak };

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
			if (Common.Team.TeamItems.HoldsStatBuff(NPC)) return true; // a team item's mod buff raises a flag the mod's later hooks read: that needs the frame
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
			long t0 = GearUpkeep.Timing ? Stopwatch.GetTimestamp() : 0L;
			try { GearFrame(); _upkeepFails = 0; }
			catch (Exception e) { UpkeepFailed(e); }
			if (GearUpkeep.Timing) { GearUpkeep.Ticks += Stopwatch.GetTimestamp() - t0; GearUpkeep.Frames++; }
		}

		private int[] _nzIdx = new int[32], _nzVal = new int[32];

		/// <summary>Remembers the non-zero entries of <paramref name="counts"/> (indices and values); returns how many.</summary>
		private int SaveOwnedCounts(int[] counts)
		{
			int n = 0, off = 0;
			var span = new ReadOnlySpan<int>(counts);
			while (off < span.Length)
			{
				int i = span.Slice(off).IndexOfAnyExcept(0);
				if (i < 0) break;
				i += off;
				if (n == _nzIdx.Length) { Array.Resize(ref _nzIdx, n * 2); Array.Resize(ref _nzVal, n * 2); }
				_nzIdx[n] = i; _nzVal[n] = counts[i]; n++;
				off = i + 1;
			}
			return n;
		}

		/// <summary>Makes <paramref name="counts"/> hold exactly what <see cref="SaveOwnedCounts"/> saw: everything cleared, then the saved entries put back.</summary>
		private void RestoreOwnedCounts(int[] counts, int n)
		{
			Array.Clear(counts, 0, counts.Length);
			for (int k = 0; k < n; k++) counts[_nzIdx[k]] = _nzVal[k];
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
			const int ownerSlot = DummySlot; // the frame runs in slot 255, NEVER the commander's (see EnterFrameSlot)
			object thorium = null;
			FrameSlot fs = default;
			Vector2 savedScreen = Main.screenPosition; int savedMx = Main.mouseX, savedMy = Main.mouseY; bool frameAimed = false;
			var prev = GearUpkeep.Body; GearUpkeep.Body = this;
			int spawnStart = GearUpkeep.Spawned.Count;
			long pf = global::Levy.Common.Util.LevyProf.Begin();
			try
			{
				d.Center = NPC.Center;
				fs = EnterFrameSlot();
				thorium = OwnerPuppet.SwapThorium(d, true);
				{ NPC aimAt = KeyTarget(); if (aimAt != null) { Main.screenPosition = Vector2.Zero; Main.mouseX = (int)aimAt.Center.X; Main.mouseY = (int)aimAt.Center.Y; frameAimed = true; } } // the cursor is on the enemy (Prismatic lasers and the Forbidden Circlet read it)

				global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.GfSlot, pf); pf = global::Levy.Common.Util.LevyProf.Begin();
				// The timers Player.Update counts down once a tick.
				if (d.petalTimer > 0) d.petalTimer--;
				if (d.shadowDodgeTimer > 0) d.shadowDodgeTimer--;
				if (d.boneGloveTimer > 0) d.boneGloveTimer--;
				if (d.crystalLeafCooldown > 0) d.crystalLeafCooldown--;

				// ResetEffects ends in ResetProjectileCaches, which zeroes the owned counts: put them back (PrepareCountsOncePerTick filled them) or the Stardust Guardian is cast every tick.
				// (The array is one int per projectile type, thousands with Calamity, and all but a few entries are zero: only the non-zero ones are saved and put back; a full-array copy twice per soldier per tick was a measurable slice of the army's tick.)
				int[] counts = d.ownedProjectileCounts;
				int nz = SaveOwnedCounts(counts);
				global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.GfCopy, pf); pf = global::Levy.Common.Util.LevyProf.Begin();
				d.ResetEffects();
				global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.GfReset, pf); pf = global::Levy.Common.Util.LevyProf.Begin();
				RestoreOwnedCounts(d.ownedProjectileCounts, nz);
				global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.GfCopy, pf); pf = global::Levy.Common.Util.LevyProf.Begin();
				d.numMinions = savedNum; d.slotsMinions = savedSlots; d.highestStormTigerGemOriginalDamage = hs; d.highestAbigailCounterOriginalDamage = ha;

				// Buffs: only the mod's. A vanilla buff on the dummy comes from a set bonus whose stat side is built by hand (ApplyPotionBuffs / ApplySetStats), which would count twice.
				RunHook(() =>
				{
					for (int b = 0; b < d.buffType.Length; b++) if (d.buffType[b] > 0 && d.buffType[b] < BuffID.Count && !(GearUpkeep.Mode == 3 && d.buffType[b] == BuffID.LeafCrystal)) d.DelBuff(b);
					PlayerLoader.PreUpdateBuffs(d);
					for (int b = 0; b < d.buffType.Length; b++) if (d.buffType[b] > 0) { d.UpdateBuffs(ownerSlot); break; }
					PlayerLoader.PostUpdateBuffs(d);
				});

				global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.GfBuffs, pf); pf = global::Levy.Common.Util.LevyProf.Begin();
				// Spore Sac keeps its hand-made trap cap per soldier (AccessoryProc.cs): its real step is held back by lifting the item out of the slot for the call, so nothing is spawned only to be removed.
				Item sac = null; int sacSlot = -1;
				if (GearUpkeep.Mode != 3) for (int a = 3; a < 3 + DummyAccessorySlots && a < d.armor.Length; a++) if (d.armor[a] != null && d.armor[a].type == ItemID.SporeSac) { sac = d.armor[a]; sacSlot = a; d.armor[a] = _blankSlot; break; }
				try { d.UpdateEquips(ownerSlot); }
				finally { if (sacSlot >= 0) d.armor[sacSlot] = sac; }
				global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.GfEquips, pf); pf = global::Levy.Common.Util.LevyProf.Begin();
				ApplyExtraAccessories(d);
				// The sets Levy builds by hand (Chlorophyte leaf, Stardust Guardian) run on a slot nobody holds, so vanilla's own cast (a minion bound to the commander) never fires.
				UpdateDummyArmorSets((WearsChlorophyteSet || WearsStardustSet) && GearUpkeep.Mode != 3 ? DummySlot - 2 : ownerSlot);
				if (GearUpkeep.Mode != 3) for (int b = 0; b < d.buffType.Length; b++) if (d.buffType[b] == BuffID.LeafCrystal) d.DelBuff(b);
				try { StationBuffSystem.Apply(d, Stations); } catch (Exception e) { Levy.Instance.Logger.Warn("Levy: station buffs failed on a soldier: " + e.Message); }
				try { _buffLifeRegen = ApplyPotionBuffs(d); } catch (Exception e) { _buffLifeRegen = 0; Levy.Instance.Logger.Warn("Levy: potion buffs failed on a soldier: " + e.Message); }

				global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.GfSets, pf); pf = global::Levy.Common.Util.LevyProf.Begin();
				// Player.Update's tail: the mod hooks that read the finished equip pass. Calamity regenerates rogue stealth in the last.
				// What the mods' PostUpdateEquips add to lifeRegen (Calamity adds the regeneration of its buffs and gear here, a dummy never runs Player.UpdateLifeRegen) is the levy's regeneration, in the same units as the Campfire's.
				int regen0 = d.lifeRegen;
				RunHook(() => PlayerLoader.PostUpdateEquips(d));
				if (d.lifeRegen > regen0) _buffLifeRegen += d.lifeRegen - regen0;
				Common.Team.TeamItems.LevyThoriumEquips(this, d, ref _buffLifeRegen); // Thorium's Ring of Unity and life recovery for the dummy
				global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.GfPost, pf); pf = global::Levy.Common.Util.LevyProf.Begin();
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
				Rippers_Restore(); // ResetEffects cleared Calamity's Rage / Adrenaline mode flags; Calamity's fill/burn step (in PostUpdateMiscEffects) reads them
				RunHook(() => PlayerLoader.PostUpdateMiscEffects(d));
				d.itemAnimation = animSaved; d.itemAnimationMax = animMaxSaved; d.itemTime = timeSaved;
				global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.GfMisc, pf); pf = global::Levy.Common.Util.LevyProf.Begin();
			}
			finally
			{
				if (frameAimed) { Main.screenPosition = savedScreen; Main.mouseX = savedMx; Main.mouseY = savedMy; }
				ExitFrameSlot(fs); // the real Main.myPlayer / Main.player[255] first: RestoreThorium finds the commander through them
				d.whoAmI = savedWho; d.Center = savedCenter;
				OwnerPuppet.RestoreThorium(thorium);
				GearUpkeep.Body = prev;
				AdoptUpkeepSpawns(spawnStart);
				WriteBackVitals(d, life0, mana0);
				global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.GfExit, pf);
			}
		}

		// ------------------------------------------------------------------------------------------------------------
		// The frame slot. In single player (and for a host who also plays) Main.myPlayer is the commander, and vanilla / mod code in the equip pass has "whoAmI == Main.myPlayer" blocks that read and
		// write Main.LocalPlayer, play sounds and touch other local-player state. Borrowing the commander's slot would aim every one of them at the REAL commander. So the frame stands the dummy in slot 255
		// (the null-player slot, as the soldier minions do), makes 255 the machine's player for its length, and lets WeaponUse.OwnerFor keep answering with the real one (SoldierMinions fake). Whatever the
		// frame does to "the local player" lands on the dummy. Sounds are muted for the length (a levy's gear is not a sound the listener should hear 40 times a tick). On a dedicated server
		// Main.myPlayer already is 255 and only the array slot is lent.
		// ------------------------------------------------------------------------------------------------------------
		private struct FrameSlot { public Player Backup; public int MyPlayer, FakeToken; public float Volume; public bool Active, Ded; }

		private FrameSlot EnterFrameSlot()
		{
			if (GearUpkeep.LegacySlot) { Dummy.whoAmI = WeaponUse.OwnerFor(this); return default; } // HARNESS A/B only (env LEVY_UPKEEP_LEGACYSLOT=1): the first version's borrowed commander slot
			var f = new FrameSlot { Active = true, Backup = Main.player[DummySlot], MyPlayer = Main.myPlayer, Volume = Main.soundVolume, Ded = Main.dedServ };
			Dummy.whoAmI = DummySlot;
			Main.player[DummySlot] = Dummy;
			f.FakeToken = SoldierMinions.BeginFake(f.MyPlayer);
			Main.myPlayer = DummySlot;
			// Local-player blocks also write PROCESS-WIDE state the dummy has no business touching: Calamity swaps TextureAssets.FlyingCarpet / the buff icons and sets BossHealthBarManager flags under
			// "!Main.dedServ && whoAmI == Main.myPlayer"; sounds, dust, shaders and UI sit behind the same test. The frame runs as a dedicated server does (which is how every measurement here was taken) so
			// none of that fires; restored in the finally. The frame is synchronous on the main thread.
			Main.dedServ = true;
			if (!f.Ded) Main.soundVolume = 0f;
			return f;
		}

		private void ExitFrameSlot(FrameSlot f)
		{
			if (!f.Active) return;
			Main.myPlayer = f.MyPlayer;
			SoldierMinions.EndFake(f.FakeToken);
			Main.player[DummySlot] = f.Backup ?? Main.player[DummySlot];
			Main.dedServ = f.Ded;
			Main.soundVolume = f.Volume;
			Dummy.whoAmI = DummySlot;
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
		private void AdoptUpkeepSpawns(int start, bool churnCheck = true)
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
					if (GearUpkeep.CoveredByHandMade(p) || (churnCheck && GearUpkeep.OwnerBound(p.type, NPC.whoAmI))) { GearUpkeep.Dropped[p.type] = GearUpkeep.Dropped.TryGetValue(p.type, out int n) ? n + 1 : 1; p.active = false; p.timeLeft = 0; continue; }
					// A helper MINION an accessory casts (Calamity's Elemental in a Bottle, any mod's "summons a minion while worn"): its AI reads the owner Player's flags and buffs, so it is adopted as a DRIVEN minion
					// (owner 255, the soldier's dummy stands there for its Update; SoldierMinions) and never given to the commander, whose flags are not set and who would end it on its first tick.
					if (!(p.minion && SoldierMinions.AdoptAccessoryMinion(this, p))) WeaponUse.TagSoldierProjectile(this, p, p.DamageType);
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
			FrameSlot fs = default;
			var prev = GearUpkeep.Body; GearUpkeep.Body = this;
			int spawnStart = GearUpkeep.Spawned.Count;
			try
			{
				d.Center = NPC.Center;
				fs = EnterFrameSlot();
				thorium = OwnerPuppet.SwapThorium(d, true);
				int room = Math.Max(d.statLifeMax2, 1) + damage + 1000;
				d.statLife = room; d.immune = false; d.immuneTime = 0; d.dead = false;
				savedOrbs = d.beetleOrbs; savedShields = d.solarShields; d.beetleOrbs = 0; d.solarShields = 0; // the beetle orb / solar shield losses on a hit are built by hand (SetsOnHurt): vanilla's own would count twice
				NPC src = attackerIndex >= 0 && attackerIndex < Main.maxNPCs && Main.npc[attackerIndex].active ? Main.npc[attackerIndex] : null;
				var reason = src != null ? PlayerDeathReason.ByNPC(src.whoAmI) : PlayerDeathReason.ByCustomReason(Terraria.Localization.NetworkText.FromLiteral(SoldierName));
				var info = new Player.HurtInfo { DamageSource = reason, Damage = damage, HitDirection = src != null && src.Center.X > NPC.Center.X ? -1 : 1, CooldownCounter = -1, Dodgeable = false, Knockback = 0f, PvP = false, SoundDisabled = true, DustDisabled = true };
				d.Hurt(info, true);
				if (LevyConstants.PlayerParityIFrames && LevyConstants.ScaledImmune(d.immuneTime) > ImmuneTime) ImmuneTime = LevyConstants.ScaledImmune(d.immuneTime); // the real Player.Hurt window, mod additions included (never shorter than HitImmuneTicks)
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
				ExitFrameSlot(fs);
				d.whoAmI = savedWho; d.Center = savedCenter; d.statLife = NPC.life > 0 ? NPC.life : savedLife; d.beetleOrbs = savedOrbs; d.solarShields = savedShields; d.dead = false; d.immune = false; d.immuneTime = 0;
				OwnerPuppet.RestoreThorium(thorium);
				GearUpkeep.Body = prev;
				AdoptUpkeepSpawns(spawnStart, false); // a hit can legitimately loose several (Star Cloak: three stars): no churn test here
				_inGearHurt = false;
			}
		}
	}

	/// <summary>Shared state for the gear upkeep: mode switch, the spawn catcher's list, and the counters the harness reports.</summary>
	public static class GearUpkeep
	{
		private static int _envMode = -1, _override = -1, _cfgMode = 2; private static uint _cfgTick = uint.MaxValue;
		/// <summary>0 off · 1 frame on, hand-made duplicates left on (measures the doubling) · 2 frame on, verified duplicates off · 3 EXPERIMENT (harness only): the vanilla Crystal Leaf and Stardust Guardian are let through and their hand-made twins switched off, to measure what the real ones do under a shared owner. Default 2 when the config allows it; env LEVY_UPKEEP and the harness override win.</summary>
		public static int Mode
		{
			get
			{
				if (_override >= 0) return _override;
				if (_envMode < 0) { string v = Environment.GetEnvironmentVariable("LEVY_UPKEEP"); _envMode = !string.IsNullOrEmpty(v) && int.TryParse(v, out int m) ? Math.Clamp(m, 0, 3) : 100; }
				if (_envMode != 100) return _envMode;
				if (_cfgTick == Main.GameUpdateCount) return _cfgMode; // the config is read once a tick, not once per soldier per call
				_cfgTick = Main.GameUpdateCount;
				try { _cfgMode = LevyConfig.Get.RealGearUpkeep ? 2 : 0; } catch (Exception) { _cfgMode = 2; }
				return _cfgMode;
			}
		}
		internal static readonly bool LegacySlot = Environment.GetEnvironmentVariable("LEVY_UPKEEP_LEGACYSLOT") == "1";
		internal static void SetOverride(int mode) { _override = mode; }

		/// <summary>The soldier whose frame is running (the projectile catcher records into <see cref="Spawned"/> only then).</summary>
		internal static SoldierNPC Body;
		internal static readonly List<int> Spawned = new();

		// counters (harness)
		/// <summary>Frame timing is lab-only (LEVY_PROF / LEVY_HARNESS): two clock reads per soldier per tick are not free.</summary>
		public static readonly bool Timing = Common.Util.LevyProf.On || Environment.GetEnvironmentVariable("LEVY_HARNESS") == "1";
		public static long Ticks; public static int Frames, Failures, HookFailures, Adopted, LifeWriteBacks, ManaWriteBacks, HurtCalls, UseSignals;
		public static readonly Dictionary<int, int> Dropped = new(), AdoptedByType = new(), SpawnCounts = new();
		/// <summary>HARNESS: count every projectile spawn by type (short-lived shots like the leaf's are missed by a once-a-tick sample).</summary>
		public static bool CountSpawns;
		private static readonly HashSet<string> _warned = new();
		internal static bool WarnOnce(Exception e) => _warned.Add(e.GetType().Name + "@" + (e.StackTrace ?? "").Split('\n')[0].Trim());
		internal static void ResetCounters() { Ticks = 0; Frames = Failures = HookFailures = Adopted = LifeWriteBacks = ManaWriteBacks = HurtCalls = UseSignals = 0; Dropped.Clear(); AdoptedByType.Clear(); SpawnCounts.Clear(); _churn.Clear(); _bound.Clear(); OwnerBoundTypes.Clear(); }

		/// <summary>Projectiles whose job a hand-made twin already does (the leaf and the Guardian are bound to the commander in vanilla; Spore Sac keeps its per-soldier trap cap by hand).</summary>
		internal static bool CoveredByHandMade(Projectile p) => (Mode != 3 && (p.type == ProjectileID.CrystalLeaf || p.type == ProjectileID.StardustGuardian)) || p.aiStyle == ProjAIStyleID.SporeTrap;

		// ---- churn breaker ----
		// Some gear casts a standing helper that vanilla / the mod keeps alive by reading the OWNER player's flag (Blunder Booster's aura, minions bound to a buff on the owner). With every soldier shot owned by the
		// commander that flag is not set, so the helper ends itself the next tick and the gear casts it again: a spawn and a sync packet every other tick for nothing. A type the frame spawns far faster than
		// anything lives is OWNER-BOUND: it is dropped (and logged once) until soldiers own their shots, which is the later ownership step.
		private const int ChurnWindowTicks = 120, ChurnLimit = 30, ChurnHoldTicks = 60 * 60;
		private static readonly Dictionary<long, (uint windowStart, int n)> _churn = new();
		private static readonly Dictionary<int, uint> _bound = new();
		public static readonly Dictionary<int, int> OwnerBoundTypes = new();
		internal static bool OwnerBound(int type, int soldier)
		{
			long key = type * 4096L + soldier; // counted per soldier: an army of Gelatin wearers is a lot of spawns, but each casts only one in forty ticks
			uint now = Main.GameUpdateCount;
			if (_bound.TryGetValue(type, out uint until))
			{
				if (now < until) return true;
				_bound.Remove(type); _churn.Clear(); // retry after the hold: ownership may have changed
			}
			_churn.TryGetValue(key, out var c);
			if (c.n == 0 || now - c.windowStart > ChurnWindowTicks) c = (now, 0);
			c.n++;
			_churn[key] = c;
			if (c.n <= ChurnLimit) return false;
			_bound[type] = now + ChurnHoldTicks;
			OwnerBoundTypes[type] = OwnerBoundTypes.TryGetValue(type, out int k) ? k + 1 : 1;
			string name = ProjectileLoader.GetProjectile(type)?.FullName ?? ProjectileID.Search.GetName(type);
			Levy.Instance?.Logger.Info("Levy: gear upkeep dropped '" + name + "': spawned " + c.n + " times in " + (now - c.windowStart) + " ticks (it is bound to its owner's flags and cannot live while soldier shots are owned by the commander; it works once soldiers own their shots).");
			return true;
		}

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
			if (GearUpkeep.CountSpawns) GearUpkeep.SpawnCounts[projectile.type] = GearUpkeep.SpawnCounts.TryGetValue(projectile.type, out int n) ? n + 1 : 1;
			if (GearUpkeep.Body != null && GearUpkeep.Spawned.Count < 4096) GearUpkeep.Spawned.Add(projectile.whoAmI);
		}
	}
}
