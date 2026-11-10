using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;
using Levy.Common.Config;
using Levy.Common.UI;
using Levy.Common.Util;

namespace Levy.Common.Systems
{
	/// <summary>Recommended levy-count ranges per tier and the gentle "too few / too many" notes for values far outside them. Pure.</summary>
	public static class LevyAnchors
	{
		/// <summary>Inclusive recommended range of the end-of-tier count: pre-hardmode (Wall of Flesh), hardmode (Moon Lord), post-Moon Lord (the final boss).</summary>
		public static readonly int[] RecommendedLow = { 12, 28, 40 };
		/// <summary>Recommended range of the starting count.</summary>
		public const int StartLow = 6, StartHigh = 20;
		public static readonly int[] RecommendedHigh = { 30, 60, 90 };
		public static readonly string[] TierKeys = { "Pre", "Hardmode", "Post" };

		/// <summary>Localization keys (under Mods.Levy) of every note that applies, in tier order. Empty when everything sits inside the recommended ranges.</summary>
		public static List<string> Warnings(int start, int pre, int hm, int post, int max)
		{
			var w = new List<string>();
			if (start < StartLow) w.Add("Setup.Warn.StartFew");
			else if (start > pre) w.Add("Setup.Warn.StartAbovePre");
			int[] v = { pre, hm, post };
			for (int t = 0; t < 3; t++)
			{
				if (v[t] < RecommendedLow[t]) w.Add("Setup.Warn." + TierKeys[t] + "Few");
				else if (v[t] > RecommendedHigh[t]) w.Add("Setup.Warn." + TierKeys[t] + "Many");
			}
			if (max < post) w.Add("Setup.Warn.MaxBelowPost");
			return w;
		}

		public static List<string> Warnings(LevyConfig c) => Warnings(c.LeviesAtStart, c.LeviesByEndOfPreHardmode, c.LeviesByEndOfHardmode, c.LeviesByEndOfPostMoonLord, c.MaxLevies);

		/// <summary>The one-paragraph explanation of how the three numbers work, with this config's values.</summary>
		public static string Explain(int start, int pre, int hm, int post) => LevyUtil.T("Setup.Anchors", start, pre, hm, Math.Max(0, post - hm));
	}

	/// <summary>Accessory slot arithmetic. Pure.</summary>
	public static class LevySlotMath
	{
		/// <summary>Slots a levy of a grade opens: the grade's, the Guide to Expertise's when read, and the boss milestones', never more than a soldier can carry.</summary>
		public static int Compute(int gradeSlots, int expertiseSlots, bool expertiseRead, int milestoneSlots, int soldierMax)
			=> Math.Clamp(Math.Max(0, gradeSlots) + (expertiseRead ? Math.Max(0, expertiseSlots) : 0) + Math.Max(0, milestoneSlots), 0, soldierMax);

		public static int GradeSlots(LevyConfig c, Grade g) => g switch { Grade.Levy => c.LevyAccessorySlots, Grade.ManAtArms => c.ManAtArmsAccessorySlots, _ => c.ChampionAccessorySlots };

		public static int ExpertiseSlots { get { try { return LevyConfig.Get.ExpertiseGuideSlots; } catch (Exception) { return LevyConstants.ExpertiseAccessorySlots; } } }
	}

	/// <summary>Boss milestone rewards. Pure apart from the display helper.</summary>
	public static class MilestoneMath
	{
		/// <summary>Sum of every enabled milestone whose boss is beaten, per <paramref name="reached"/>.</summary>
		public static void Apply(IEnumerable<MilestoneReward> list, Func<string, bool> reached, out int slots, out int levies)
		{
			slots = 0; levies = 0;
			if (list == null) return;
			foreach (var m in list)
			{
				if (m == null || !m.Enabled || string.IsNullOrWhiteSpace(m.Boss)) continue;
				bool ok; try { ok = reached(m.Boss.Trim()); } catch (Exception) { ok = false; }
				if (!ok) continue;
				slots += Math.Max(0, m.AccessorySlots); levies += Math.Max(0, m.Levies);
			}
		}

		/// <summary>Display name of a milestone's boss: the vanilla id with spaces, or the name as typed.</summary>
		public static string BossName(string id)
		{
			if (string.IsNullOrWhiteSpace(id)) return "(no boss)";
			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < id.Length; i++) { if (i > 0 && char.IsUpper(id[i]) && char.IsLower(id[i - 1])) sb.Append(' '); sb.Append(id[i]); }
			return sb.ToString();
		}

		/// <summary>What a milestone grants, in the config list: "Wall Of Flesh: +1 accessory slot".</summary>
		public static string Describe(MilestoneReward m)
		{
			string a = m.AccessorySlots > 0 ? "+" + m.AccessorySlots + (m.AccessorySlots == 1 ? " accessory slot" : " accessory slots") : "";
			string l = m.Levies > 0 ? "+" + m.Levies + (m.Levies == 1 ? " levy" : " levies") : "";
			string grant = a.Length > 0 && l.Length > 0 ? a + ", " + l : a.Length > 0 ? a : l.Length > 0 ? l : "nothing";
			return BossName(m.Boss) + ": " + grant + (m.Enabled ? "" : " (off)");
		}
	}

	public enum SetupPreset : byte { Default = 0, Relaxed = 1, Hardcore = 2 }

	/// <summary>The new-world presets. They touch only the level of difficulty knobs, nothing else in the config.</summary>
	public static class SetupPresets
	{
		public readonly struct Values
		{
			public readonly int Start, Pre, Hm, Post, Max; public readonly bool BossesTarget, BossesPunish;
			public Values(int start, int pre, int hm, int post, int max, bool target, bool punish) { Start = start; Pre = pre; Hm = hm; Post = post; Max = max; BossesTarget = target; BossesPunish = punish; }
		}

		public static Values Of(SetupPreset p) => p switch
		{
			SetupPreset.Relaxed => new Values(14, 30, 60, 90, 90, false, false),
			SetupPreset.Hardcore => new Values(6, 14, 30, 45, 45, true, true),
			_ => new Values(10, 20, 40, 60, 60, false, true),
		};

		public static void Apply(LevyConfig c, Values v)
		{
			c.LeviesAtStart = v.Start; c.LeviesByEndOfPreHardmode = v.Pre; c.LeviesByEndOfHardmode = v.Hm; c.LeviesByEndOfPostMoonLord = v.Post; c.MaxLevies = Math.Max(v.Max, v.Post);
			c.BossesTargetSoldiers = v.BossesTarget; c.BossesPunishSoldiers = v.BossesPunish;
		}

		public static void Apply(LevyConfig c, SetupPreset p) => Apply(c, Of(p));

		/// <summary>Single player only: sets the values, writes the config file and runs the change hook. Returns false in multiplayer, where only the host edits the config.</summary>
		public static bool ApplyAndSave(Values v)
		{
			if (Main.netMode != NetmodeID.SinglePlayer) return false;
			var c = LevyConfig.Get;
			Apply(c, v);
			try { typeof(ConfigManager).GetMethod("Save", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(ModConfig) }, null)?.Invoke(null, new object[] { c }); } catch (Exception e) { Levy.Instance.Logger.Warn("Levy setup: config save failed: " + e.Message); }
			c.OnChanged();
			return true;
		}

		public static string Name(SetupPreset p) => LevyUtil.T("Setup.Preset." + p);
		public static string Blurb(SetupPreset p) => LevyUtil.T("Setup.Preset." + p + "Blurb");
	}

	/// <summary>Saved per world: whether the new-world setup popup was answered, and with what.</summary>
	public static class SetupState
	{
		public const byte Unanswered = 0, Answered = 1, Skipped = 2;

		public static void Write(TagCompound tag, byte state, SetupPreset preset, bool customized)
		{
			if (state == Unanswered) return;
			tag["setupState"] = state; tag["setupPreset"] = (byte)preset; tag["setupCustom"] = customized;
		}

		public static byte Read(TagCompound tag, out SetupPreset preset, out bool customized)
		{
			byte s = tag.GetByte("setupState");
			preset = (SetupPreset)Math.Clamp((int)tag.GetByte("setupPreset"), 0, 2);
			customized = tag.GetBool("setupCustom");
			return s > Skipped ? Unanswered : s;
		}
	}

	/// <summary>The per-world setup answer. Single player only: the popup edits the server config, which only the host can do.</summary>
	public class WorldSetupSystem : ModSystem
	{
		public static byte State;
		public static SetupPreset Preset;
		public static bool Customized;

		public static bool NeedsPopup => Main.netMode == NetmodeID.SinglePlayer && State == SetupState.Unanswered;

		public static void Answer(byte state, SetupPreset preset, bool customized) { State = state; Preset = preset; Customized = customized; }

		public override void ClearWorld() { State = SetupState.Unanswered; Preset = SetupPreset.Default; Customized = false; }
		public override void SaveWorldData(TagCompound tag) => SetupState.Write(tag, State, Preset, Customized);
		public override void LoadWorldData(TagCompound tag) { State = SetupState.Read(tag, out var p, out var c); Preset = p; Customized = c; }
	}

	/// <summary>Opens the setup popup when the player enters a world that was never set up.</summary>
	public class WorldSetupPlayer : ModPlayer
	{
		public override void OnEnterWorld()
		{
			if (Player.whoAmI == Main.myPlayer && WorldSetupSystem.NeedsPopup) UISystem.Instance?.OpenSetup();
		}
	}

	/// <summary>/levysetup: re-opens the setup popup.</summary>
	public class LevySetupCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "levysetup";
		public override string Description => LevyUtil.T("Setup.CommandHelp");
		public override void Action(CommandCaller caller, string input, string[] args) => UISystem.Instance?.OpenSetup();
	}

	/// <summary>Config-change notes: gentle warnings in chat and the log when levy counts sit far outside the recommended range.</summary>
	public static class LevySettingsNotes
	{
		public static void OnConfigChanged(LevyConfig c)
		{
			try
			{
				var w = LevyAnchors.Warnings(c);
				if (w.Count == 0) return;
				foreach (var k in w)
				{
					string text = LevyUtil.T(k);
					Levy.Instance.Logger.Info("Levy settings note: " + text);
					if (!Main.dedServ && !Main.gameMenu && Main.netMode != NetmodeID.Server) Main.NewText(text, 230, 200, 120);
				}
			}
			catch (Exception) { }
		}
	}

	/// <summary>Boot self-checks for the settings layer.</summary>
	public class LevySettingsCheck : ModSystem
	{
		/// <summary>Every field LevyConfig has had. A saved config file keys on these names, so the list only ever grows (FallenStarsPerManaCrystal moved to the progression config, whose recipe cost it now sets; a saved value for it is ignored).</summary>
		private static readonly string[] FrozenFields =
		{
			"BossHpScalingPercentPerCp", "BossHpScalingCapPercent", "BossesPunishSoldiers", "BossesTargetSoldiers", "FearMultiplier", "SoldierDamageMultiplier", "RenderCap", "Tombstones",
			"EnemiesTargetSoldiers", "UseVanillaStatPipeline", "ClassMatchIssue", "WeaponRanking", "SkipArmoredWeapons", "AimAccuracy", "AutoUpgradeWeapons", "RealWeaponShoot", "AutoFeedCrystals",
			"ArmoryTurnsStarsIntoMana", "SoldiersTriggerPressurePlates", "SoldierEffectsAtSoldier", "RealGearUpkeep", "SoldiersUseHooks", "ExtraTargetingAiStyles",
			"DoNotAttackNpcNames", "WageMultiplier", "AutoMusterDefault", "MilitiaFieldCap", "WageMutiny", "NurseHealsSoldiers", "Spoils", "MagicStorageSupply", "LeviesAtStart", "LeviesByEndOfPreHardmode",
			"LeviesByEndOfHardmode", "LeviesByEndOfPostMoonLord", "MaxLevies", "Nameplates", "MilitiaDeathMessages", "RegularDeathMessages",
			"NonSummonersCarrySummonItem", "ArmyEntityBudget", "ReservedProjectileSlots", "ReservedNpcSlots", "ArmySoundVolume",
		};

		public override void PostAddRecipes()
		{
			var fails = new List<string>();
			try
			{
				// 1. field-name stability
				var have = new HashSet<string>();
				foreach (var f in typeof(LevyConfig).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) have.Add(f.Name);
				foreach (var n in FrozenFields) if (!have.Contains(n)) fails.Add("config field missing: " + n);
				// every field sits under a header section: the first field of each category carries a [Header]
				int headers = 0; foreach (var f in typeof(LevyConfig).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) if (f.GetCustomAttribute<HeaderAttribute>() != null) headers++;
				if (headers < 10) fails.Add("expected 10+ config headers, found " + headers);

				if (typeof(ConfigManager).GetMethod("Save", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(ModConfig) }, null) == null) fails.Add("ConfigManager.Save(ModConfig) not found: presets would not persist to disk");
				// 2. anchor math + recommended-range warnings
				if (LevyAnchors.Warnings(10, 20, 40, 60, 60).Count != 0) fails.Add("default counts should raise no note");
				var few = LevyAnchors.Warnings(6, 6, 40, 60, 60); if (few.Count != 1 || few[0] != "Setup.Warn.PreFew") fails.Add("6/40/60 should note PreFew");
				var many = LevyAnchors.Warnings(10, 20, 40, 150, 200); if (many.Count != 1 || many[0] != "Setup.Warn.PostMany") fails.Add("20/40/150 should note PostMany");
				var mx = LevyAnchors.Warnings(10, 20, 40, 60, 50); if (mx.Count != 1 || mx[0] != "Setup.Warn.MaxBelowPost") fails.Add("max 50 below post 60 should note MaxBelowPost");
				var sf = LevyAnchors.Warnings(4, 20, 40, 60, 60); if (sf.Count != 1 || sf[0] != "Setup.Warn.StartFew") fails.Add("start 4 should note StartFew");
				var sa = LevyAnchors.Warnings(25, 20, 40, 60, 60); if (sa.Count != 1 || sa[0] != "Setup.Warn.StartAbovePre") fails.Add("start 25 above pre 20 should note StartAbovePre");
				foreach (SetupPreset p in Enum.GetValues(typeof(SetupPreset)))
				{
					var v = SetupPresets.Of(p);
					if (LevyAnchors.Warnings(v.Start, v.Pre, v.Hm, v.Post, v.Max).Count != 0) fails.Add("preset " + p + " raises a range note");
					if (v.Start > v.Pre || v.Pre > v.Hm || v.Hm > v.Post || v.Max < v.Post) fails.Add("preset " + p + " counts out of order");
				}
				// vanilla end-of-tier anchors under each preset: killing every boss of a tier lands exactly on its target
				{
					var v = SetupPresets.Of(SetupPreset.Relaxed);
					var all = new List<TierBoss> { new(0, 2f, true), new(0, 3f, true), new(1, 2f, true), new(1, 2f, true), new(2, 0f, true) };
					int endAll = LevyTierMath.Compute(all, v.Start, v.Pre, v.Hm, v.Post, v.Max);
					if (endAll != v.Post) fails.Add($"Relaxed: every boss down expected {v.Post}, got {endAll}");
					var preOnly = new List<TierBoss> { new(0, 2f, true), new(0, 3f, true), new(1, 2f, false), new(1, 2f, false) };
					int endPre = LevyTierMath.Compute(preOnly, v.Start, v.Pre, v.Hm, v.Post, v.Max);
					if (endPre != v.Pre) fails.Add($"Relaxed: pre-hardmode cleared expected {v.Pre}, got {endPre}");
				}

				// 3. milestone reward application
				var ms = new List<MilestoneReward>
				{
					new("WallOfFlesh", 1, 0, true), new("Plantera", 0, 4, true), new("MoonLord", 1, 6, false), new("Plantera", 2, 1, true), new("", 5, 5, true), null,
				};
				MilestoneMath.Apply(ms, id => id == "WallOfFlesh" || id == "Plantera", out int sl, out int lv);
				if (sl != 3 || lv != 5) fails.Add($"milestones expected 3 slots / 5 levies, got {sl}/{lv}");
				MilestoneMath.Apply(ms, id => false, out sl, out lv);
				if (sl != 0 || lv != 0) fails.Add("no boss beaten must grant nothing");
				foreach (var d in MilestoneReward.Defaults()) if (d.Enabled) fails.Add("default milestone " + d.Boss + " is on; defaults must not change play");
				if (MilestoneMath.Describe(new MilestoneReward("WallOfFlesh", 1, 0, true)) != "Wall Of Flesh: +1 accessory slot") fails.Add("milestone description text wrong");

				// 4. slots: defaults equal the old grade table + Guide of Expertise; milestones add; the soldier maximum holds
				for (int g = 0; g < 3; g++)
					foreach (bool read in new[] { false, true })
					{
						int legacy = Math.Min(LevyConstants.GradeSlots[g] + (read ? LevyConstants.ExpertiseAccessorySlots : 0), SoldierGear_Max);
						int now = LevySlotMath.Compute(new[] { 1, 3, 5 }[g], 1, read, 0, SoldierGear_Max);
						if (legacy != now) fails.Add($"slots grade {g} expertise {read}: legacy {legacy}, now {now}");
					}
				if (LevySlotMath.Compute(5, 1, true, 3, 6) != 6) fails.Add("slot total must hold at the soldier maximum");
				if (LevySlotMath.Compute(1, 1, false, 1, 6) != 2) fails.Add("milestone slot not added");

				// 5. popup persistence round trip
				var tag = new TagCompound();
				SetupState.Write(tag, SetupState.Unanswered, SetupPreset.Hardcore, true);
				if (SetupState.Read(tag, out _, out _) != SetupState.Unanswered) fails.Add("unanswered must not write");
				SetupState.Write(tag, SetupState.Answered, SetupPreset.Hardcore, true);
				byte rs = SetupState.Read(tag, out var rp, out bool rc);
				if (rs != SetupState.Answered || rp != SetupPreset.Hardcore || !rc) fails.Add("setup answer did not round-trip");
				var tag2 = new TagCompound(); SetupState.Write(tag2, SetupState.Skipped, SetupPreset.Default, false);
				if (SetupState.Read(tag2, out _, out _) != SetupState.Skipped) fails.Add("skipped did not round-trip");
				// preset application on a bare config object
				var bare = new LevyConfig(); SetupPresets.Apply(bare, SetupPreset.Hardcore);
				if (bare.LeviesAtStart != 6 || bare.LeviesByEndOfPreHardmode != 14 || !bare.BossesTargetSoldiers || bare.MaxLevies < bare.LeviesByEndOfPostMoonLord) fails.Add("Hardcore preset did not apply");
				SetupPresets.Apply(bare, SetupPreset.Relaxed);
				if (bare.BossesTargetSoldiers || bare.BossesPunishSoldiers || bare.LeviesByEndOfPostMoonLord != 90 || bare.LeviesAtStart != 14) fails.Add("Relaxed preset did not apply");
				if (SetupPresets.Of(SetupPreset.Default).Start != LevyConstants.DefaultLeviesAtStart) fails.Add("Default preset start must equal the config default");
			}
			catch (Exception e) { fails.Add("threw " + e.GetType().Name + ": " + e.Message); }
			if (fails.Count == 0) Mod.Logger.Info("Levy settings self-check: OK");
			else foreach (var f in fails) Mod.Logger.Warn("Levy settings self-check FAILED: " + f);
		}

		private const int SoldierGear_Max = global::Levy.Common.Soldiers.SoldierGear.AccessorySlots;
	}
}
