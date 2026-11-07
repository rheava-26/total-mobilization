using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Systems;

namespace Levy.Common.Options
{
	/// <summary>Boot self-check ("[FearCheck]" lines, headless server boots): config defaults, formation math, the scene scan box and the commander's station buffs.</summary>
	public class FearEventsSelfCheck : ModSystem
	{
		int pass, fail;
		void Check(string name, bool ok, string detail = "")
		{
			if (ok) pass++; else fail++;
			Mod.Logger.Info($"[FearCheck] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}");
		}

		public override void PostSetupRecipes()
		{
			string env = System.Environment.GetEnvironmentVariable("LEVY_FEARCHECK");
			if (env == "0" || (!Main.dedServ && env != "1")) return;
			pass = fail = 0;
			try
			{
				var c = LevyConfig.Get; // the loaded instance (tModLoader applies the DefaultValue attributes on load)
				Check("config default fear model is Events", c.FearModel == FearModelSetting.Events);
				Check("config default formation defense is on", c.FormationDefense);
				if (System.Environment.GetEnvironmentVariable("LEVY_FEARMODEL") == null) Check("shipped fear model in force is Events", LevyOptions.Fear == FearModel.Events, LevyOptions.Fear.ToString());
				if (System.Environment.GetEnvironmentVariable("LEVY_SURV") == null) Check("shipped survivability in force is formation only", LevyOptions.Surv == SurvMode.Formation, LevyOptions.Surv.ToString());
				Check("formation: 0 allies = 0%", LevyOptions.DrFor(0) == 0f);
				Check("formation: 3 allies = 6%", System.Math.Abs(LevyOptions.DrFor(3) - 0.06f) < 1e-5f, LevyOptions.DrFor(3).ToString());
				Check("formation: caps at 20%", System.Math.Abs(LevyOptions.DrFor(40) - 0.20f) < 1e-5f, LevyOptions.DrFor(40).ToString());

				Check("scene box is vanilla's 169x124", Main.buffScanAreaWidth == 169 && Main.buffScanAreaHeight == 124, Main.buffScanAreaWidth + "x" + Main.buffScanAreaHeight);

				var p = new Player();
				p.active = true;
				Check("commander with no station buffs gives none", StationBuffSystem.CommanderFlags(p) == StationFlags.None);
				p.AddBuff(BuffID.Sharpened, 600); p.AddBuff(BuffID.AmmoBox, 600); p.AddBuff(BuffID.Clairvoyance, 600); p.AddBuff(BuffID.Bewitched, 600); p.AddBuff(BuffID.WarTable, 600);
				StationFlags want = StationFlags.Sharpening | StationFlags.AmmoBox | StationFlags.CrystalBall | StationFlags.Bewitching | StationFlags.WarTable;
				Check("commander's activated station buffs map to levy flags", StationBuffSystem.CommanderFlags(p) == want, StationBuffSystem.CommanderFlags(p).ToString());

				var d = new Player();
				d.ResetEffects();
				StationBuffSystem.Apply(d, StationFlags.Sharpening | StationFlags.WarTable | StationFlags.Bewitching | StationFlags.CrystalBall | StationFlags.CatBast);
				Check("station flags change the stat pipeline", d.maxTurrets > 1 && d.maxMinions > 1 && d.statDefense >= LevyConstants.CatBastDefense && d.GetArmorPenetration(DamageClass.Melee) >= LevyConstants.SharpenedArmorPen);
				var h = new Player();
				h.ResetEffects();
				StationBuffSystem.Apply(h, StationFlags.Sunflower);
				Check("happy: sunflower flag speeds the levy up", h.moveSpeed > 1.05f && h.sunflower, h.moveSpeed.ToString());
				Check("campfire + heart lantern regen is 3 units", StationBuffSystem.LifeRegen(StationFlags.Campfire | StationFlags.HeartLantern) == 3);
				Check("sunflowers halve fear gain", System.Math.Abs(LevyConstants.SunflowerFearMult - 0.5f) < 1e-5f);
				Check("honey regen is 2 units", StationBuffSystem.LifeRegen(StationFlags.Honey) == 2);
			}
			catch (System.Exception e) { Check("no exception", false, e.ToString()); }
			Mod.Logger.Info($"[FearCheck] SUMMARY pass={pass} fail={fail}");
		}
	}
}
