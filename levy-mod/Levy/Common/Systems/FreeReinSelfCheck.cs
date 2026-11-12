using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Dialogue;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Systems
{
	/// <summary>
	/// Boot self-check of Free Rein's home leash and off-duty life ("[FreeRein] self-check ... fail=N", headless server boots; LEVY_FREEREINCHECK=0 skips).
	/// Pure rules only: the box around the base, the roof rule for wander spots, the pastime weights and the dialogue file. The walking itself is measured by the lab (lab/travel/freereinlab).
	/// </summary>
	public class FreeReinSelfCheck : ModSystem
	{
		private int pass, fail;
		private void Check(string name, bool ok, string detail = "")
		{
			if (ok) pass++; else fail++;
			Mod.Logger.Info($"[FreeRein] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}");
		}

		public override void PostSetupRecipes() => LoadSafe.Run(Run);

		private void Run()
		{
			string env = Environment.GetEnvironmentVariable("LEVY_FREEREINCHECK");
			if (env == "0" || (!Main.dedServ && env != "1")) return;
			pass = fail = 0;
			try
			{
				var a = new Vector2(1000f * 16f, 400f * 16f);
				int r = LevyConstants.FreeReinRadiusDefault;
				// 1. the box: inside along the ground, outside past the radius, outside below the base floor's reach (a hole, a shaft), outside in the sky past its reach
				Check("1a base is inside", SoldierNPC.BoxOutside(a, a, r) == 0f);
				Check("1b ground at the radius is inside, one tile more is outside", SoldierNPC.BoxOutside(a + new Vector2(r * 16f, 0f), a, r) == 0f && SoldierNPC.BoxOutside(a + new Vector2((r + 1) * 16f, 0f), a, r) > 0f);
				Check("1c a hole floor 30 rows under the base is outside however near it lies", SoldierNPC.BoxOutside(a + new Vector2(5f * 16f, 30f * 16f), a, r) > 0f);
				Check("1d a pit 8 rows deep and a hill 40 rows up are inside", SoldierNPC.BoxOutside(a + new Vector2(10f * 16f, 8f * 16f), a, r) == 0f && SoldierNPC.BoxOutside(a + new Vector2(10f * 16f, -40f * 16f), a, r) == 0f);
				Check("1e the elevator's cavern 130 rows down is far outside", SoldierNPC.BoxOutside(a + new Vector2(40f * 16f, 130f * 16f), a, r) >= 100f);
				Check("1f a base 150 rows underground has its own box (the same numbers around it)", SoldierNPC.BoxOutside(new Vector2(1000f * 16f, 550f * 16f), new Vector2(1000f * 16f, 550f * 16f), r) == 0f);
				Check("1g the old circle let a levy 400 tiles out; the box does not", SoldierNPC.BoxOutside(a + new Vector2(400f * 16f, 0f), a, r) > 0f && SoldierNPC.BoxOutside(a + new Vector2(400f * 16f, 0f), a, LevyConstants.FreeReinLeashTiles) == 0f);
				Check("1h the default radius is inside the option's range", r >= 60 && r <= 600);

				// 2. the roof rule: a house roof is fine, a cave under a hill is not, a base that is itself under rock allows more
				Check("2a three tiles of roof is fine on the surface", SoldierNPC.OverburdenOk(3, 0));
				Check("2b twenty tiles of rock is a cave", !SoldierNPC.OverburdenOk(20, 0));
				Check("2c a base under 30 tiles of rock allows spots under 30 to 40", SoldierNPC.OverburdenOk(35, 30) && !SoldierNPC.OverburdenOk(45, 30));

				// 2d. a pit floor is lower than the base, open above and walled in; a courtyard at base level or a roofed room is not (pure part: the wall test reads tiles, so only the cheap exits are checked here)
				Check("2d a spot level with the base is never a pit floor", !SoldierNPC.PitFloor(100, 400, 0, 400));
				Check("2e a roofed spot is never a pit floor", !SoldierNPC.PitFloor(100, 420, 5, 400));

				// 3. the pastime weights follow the sky and the props
				float[] day = SoldierNPC.LzWeights(true, false, 0.3f, true, true, true, true);
				float[] night = SoldierNPC.LzWeights(false, false, 0f, true, true, true, true);
				float[] rain = SoldierNPC.LzWeights(true, true, 0f, true, true, true, true);
				float[] bare = SoldierNPC.LzWeights(true, false, 0f, false, false, false, false);
				float[] bareNight = SoldierNPC.LzWeights(false, false, 0f, false, false, false, false);
				Check("3a by day a levy flies a kite, never sleeps", day[SoldierNPC.LzKite] > 0f && day[SoldierNPC.LzSleep] == 0f && day[SoldierNPC.LzStars] == 0f);
				Check("3b a windy day favours the kite", day[SoldierNPC.LzKite] > SoldierNPC.LzWeights(true, false, 0f, true, true, true, true)[SoldierNPC.LzKite]);
				Check("3c at night with a bed, sleeping is the likeliest pastime", night[SoldierNPC.LzSleep] > night[SoldierNPC.LzRoast] && night[SoldierNPC.LzSleep] > night[SoldierNPC.LzSit] && night[SoldierNPC.LzKite] == 0f);
				Check("3d at night with no bed, a fire and a seat are the likeliest", SoldierNPC.LzWeights(false, false, 0f, true, true, false, false)[SoldierNPC.LzSleep] == 0f && SoldierNPC.LzWeights(false, false, 0f, true, true, false, false)[SoldierNPC.LzSit] >= 5f);
				Check("3e rain cancels the kite and the stars", rain[SoldierNPC.LzKite] == 0f && rain[SoldierNPC.LzStars] == 0f && rain[SoldierNPC.LzRoast] > 0f);
				Check("3f with no props only the sky and a cheer remain", bare[SoldierNPC.LzRoast] == 0f && bare[SoldierNPC.LzKeg] == 0f && bare[SoldierNPC.LzSit] == 0f && bare[SoldierNPC.LzSleep] == 0f && bare[SoldierNPC.LzKite] > 0f && bare[SoldierNPC.LzCheer] > 0f && bareNight[SoldierNPC.LzStars] > 0f);
				Check("3g a keg is only a choice when the army has one", day[SoldierNPC.LzKeg] > 0f && bare[SoldierNPC.LzKeg] == 0f);

				// 4. the dialogue file speaks for both new triggers
				Check("4a FIRESIDE has lines", DialogueTable.Get(DialogueTrigger.FIRESIDE) != null);
				Check("4b PASTIME has lines", DialogueTable.Get(DialogueTrigger.PASTIME) != null);
				Check("4c PASTIME_NIGHT has lines", DialogueTable.Get(DialogueTrigger.PASTIME_NIGHT) != null);

				// 5. the numbers are sane
				Check("5a a pastime lasts seconds to a minute", LevyConstants.LeisureKiteMaxTicks <= 60 * 80 && LevyConstants.LeisureCheerMinTicks >= 60 * 3 && LevyConstants.LeisureRoastMinTicks >= 60 * 10);
				Check("5b not everyone idles at once", LevyConstants.LeisureMaxShare < 1f && LevyConstants.LeisureMaxShare > 0.3f);
			}
			catch (Exception e) { fail++; Mod.Logger.Error("[FreeRein] self-check threw: " + e); }
			if (fail == 0) Mod.Logger.Info($"[FreeRein] self-check OK pass={pass} fail=0");
			else Mod.Logger.Error($"[FreeRein] self-check FAILURE pass={pass} fail={fail}");
		}
	}
}
