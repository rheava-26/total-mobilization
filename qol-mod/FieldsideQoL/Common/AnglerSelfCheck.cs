using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace FieldsideQoL.Common
{
	/// <summary>
	/// Boot self-check of the Angler fix, logged as "[FieldsideAngler]" lines. Runs on a dedicated server when FIELDSIDE_SELFCHECK=1 (the other boots never run it).
	/// With Wrath of the Gods loaded it forces the rift eclipse flag on (WotG's own AvatarHasCoveredMoon), calls Main.AnglerQuestSwap() many times with a fixed random seed and checks:
	///   option ON  - the quest fish vary, include normal quest fish, and Veilray still shows up in the rotation;
	///   option OFF - every result is WotG's forced Veilray;
	///   rift OFF   - the rotation is normal and never Veilray (Veilray is not offered outside the rift).
	/// Without Wrath of the Gods it checks the fix is inactive and the vanilla rotation still varies. State it touches is restored.
	/// </summary>
	public class AnglerSelfCheck : ModSystem
	{
		const int Swaps = 600;
		int _pass, _fail;

		void Check(string name, bool ok, string detail = "")
		{
			if (ok) _pass++; else _fail++;
			Mod.Logger.Info($"[FieldsideAngler] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " (" + detail + ")" : "")}");
		}

		public override void PostAddRecipes()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("FIELDSIDE_SELFCHECK") != "1") return;
			try { Run(); }
			catch (Exception e) { Check("self-check ran without an exception", false, e.GetType().Name + ": " + e.Message); }
			Mod.Logger.Info($"[FieldsideAngler] self-check pass={_pass} fail={_fail}");
		}

		List<int> Roll(int n)
		{
			var fish = new List<int>();
			for (int i = 0; i < n; i++)
			{
				Main.AnglerQuestSwap();
				fish.Add(Main.anglerQuestItemNetIDs[Main.anglerQuest]);
			}
			return fish;
		}

		void Run()
		{
			var cfg = AnglerConfig.Get;
			bool savedOption = cfg.NormalQuestsDuringRift;
			int savedQuest = Main.anglerQuest;
			var savedRand = Main.rand;
			bool haveWotg = ModLoader.TryGetMod(WotgAnglerFix.WotgName, out Mod wotg);
			Check("Main.anglerQuestItemNetIDs is populated", Main.anglerQuestItemNetIDs.Length > 40, $"{Main.anglerQuestItemNetIDs.Length} entries");

			if (!haveWotg)
			{
				Main.rand = new UnifiedRandom(7);
				var fish = Roll(Swaps);
				Check("without Wrath of the Gods the fix is inactive", !WotgAnglerFix.Active);
				Check("without Wrath of the Gods the vanilla rotation varies", fish.Distinct().Count() >= 5, $"{fish.Distinct().Count()} distinct fish in {Swaps} swaps");
				Main.rand = savedRand; Main.anglerQuest = savedQuest;
				return;
			}

			Check("Wrath of the Gods found and the fix is active", WotgAnglerFix.Active, $"WotG {wotg.Version}");
			int veilray = WotgAnglerFix.VeilrayType;
			Check("Veilray is a quest fish in the rotation list", veilray > 0 && Array.IndexOf(Main.anglerQuestItemNetIDs, veilray) >= 0, $"type {veilray}");
			if (!WotgAnglerFix.Active) return;

			PropertyInfo moon = wotg.Code.GetType("NoxusBoss.Core.World.GameScenes.AvatarAppearances.PostMLRiftAppearanceSystem")?.GetProperty("AvatarHasCoveredMoon", BindingFlags.Public | BindingFlags.Static);
			Check("rift eclipse flag found", moon != null);
			if (moon == null) return;
			object savedMoon = moon.GetValue(null);
			try
			{
				moon.SetValue(null, true);
				Check("rift eclipse is on", WotgAnglerFix.RiftOngoing);

				cfg.NormalQuestsDuringRift = true;
				Main.rand = new UnifiedRandom(12345);
				var on = Roll(Swaps);
				int onDistinct = on.Distinct().Count();
				int onNonVeilray = on.Count(f => f != veilray);
				Check("option ON: the quest fish vary during the rift", onDistinct >= 8, $"{onDistinct} distinct fish in {Swaps} swaps");
				Check("option ON: normal quest fish come up", onNonVeilray > Swaps / 2, $"{onNonVeilray} of {Swaps} are not Veilray");
				Check("option ON: Veilray stays in the rotation", on.Contains(veilray), $"{on.Count(f => f == veilray)} of {Swaps}");

				cfg.NormalQuestsDuringRift = false;
				Main.rand = new UnifiedRandom(12345);
				var off = Roll(Swaps);
				Check("option OFF: every quest is Veilray during the rift", off.All(f => f == veilray), $"{off.Count(f => f == veilray)} of {Swaps} are Veilray");

				moon.SetValue(null, false);
				cfg.NormalQuestsDuringRift = true;
				Main.rand = new UnifiedRandom(12345);
				var calm = Roll(Swaps);
				Check("rift off: the rotation is normal and has no Veilray", calm.Distinct().Count() >= 8 && !calm.Contains(veilray), $"{calm.Distinct().Count()} distinct, {calm.Count(f => f == veilray)} Veilray");
			}
			finally
			{
				moon.SetValue(null, savedMoon);
				cfg.NormalQuestsDuringRift = savedOption;
				Main.rand = savedRand;
				Main.anglerQuest = savedQuest;
			}
		}
	}
}
