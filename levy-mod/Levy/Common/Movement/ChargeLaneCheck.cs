using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Levy.Common.Movement
{
	/// <summary>Boot self-check (headless server): charge-lane detection from a boss's state and the way out of a lane. Logged as "[ChargeLane]", summary "self-check pass=N fail=M".</summary>
	public class ChargeLaneCheck : ModSystem
	{
		public override void PostSetupRecipes()
		{
			if (!Terraria.Main.dedServ) return;
			int pass = 0, fail = 0;
			void Check(string name, bool ok, string detail = "") { if (ok) pass++; else fail++; Mod.Logger.Info($"[ChargeLane] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}"); }
			try
			{
				Vector2 boss = new Vector2(10000f, 5000f), still = Vector2.Zero, dashing = new Vector2(-20f, 0f);
				// Leviathan (900 x 450): line-up = ai0 2, ai1 even
				bool a = ChargeLanes.LeviathanLane(2f, 0f, 1f, boss, 900f, 450f, new Vector2(4f, 0.5f), 12000f, true, out var lineUp);
				Check("line-up (ai0=2, ai1 even) is a lane of kind 1, heading toward the army", a && lineUp.Kind == 1 && lineUp.Dir == 1 && System.Math.Abs(lineUp.Cy - 5000f) < 1f, $"kind={lineUp.Kind} dir={lineUp.Dir}");
				Check("the lane is as tall as the body plus the margin (half 243 px)", System.Math.Abs(lineUp.Half - 243f) < 0.5f, $"half={lineUp.Half}");
				bool b = ChargeLanes.LeviathanLane(2f, 1f, 1f, boss, 900f, 450f, dashing, 12000f, true, out var dash);
				Check("dash (ai1 odd, moving left) is a lane of kind 2 heading left", b && dash.Kind == 2 && dash.Dir == -1);
				Check("a hover (ai0=0) of a boss that may not charge yet is no lane", !ChargeLanes.LeviathanLane(0f, 0f, 1f, boss, 900f, 450f, still, 12000f, false, out _));
				Check("the aberration phase (ai0=1) of a boss that may not charge yet is no lane", !ChargeLanes.LeviathanLane(1f, 0f, 1f, boss, 900f, 450f, still, 12000f, false, out _));
				Check("a hover of a boss that may charge is a standing lane (kind 0)", ChargeLanes.LeviathanLane(0f, 0f, 1f, boss, 900f, 450f, still, 12000f, true, out var hv) && hv.Kind == 0);
				Check("a state outside 0 / 1 / 2 is no lane", !ChargeLanes.LeviathanLane(3f, 0f, 1f, boss, 900f, 450f, still, 12000f, true, out _));
				Check("a Leviathan still fading in (opacity 0.5) is no lane", !ChargeLanes.LeviathanLane(2f, 0f, 0.5f, boss, 900f, 450f, still, 12000f, true, out _));
				// who is in the lane (levy half height 21)
				Check("a levy level with the boss, 800 px ahead, is in the lane", ChargeLanes.InLane(dash, new Vector2(9200f, 5000f), 21f));
				Check("a levy 260 px above the centre line is in the lane (243 + 21 reach)", ChargeLanes.InLane(dash, new Vector2(9200f, 4740f), 21f));
				Check("a levy 280 px above is out", !ChargeLanes.InLane(dash, new Vector2(9200f, 4720f), 21f));
				Check("a levy 600 px behind the boss (beyond its tail) is out", !ChargeLanes.InLane(dash, new Vector2(10600f, 5000f), 21f));
				// the way out
				bool p1 = ChargeLanes.Plan(lineUp, 5000f, 21f, true, 6f, 140, false, 5000f + 16f * 40f, 0, 0f, out int d1, out float t1);
				Check("a winged levy in the middle of a lane with a deep floor goes up (parity 0), to above the band edge", p1 && d1 == -1 && t1 < 5000f - 243f - 21f, $"dir={d1} y={t1:0}");
				bool p2 = ChargeLanes.Plan(lineUp, 5000f, 21f, true, 6f, 140, false, 5000f + 16f * 40f, 1, 0f, out int d2, out _);
				Check("with parity 1 and an equal way each side it goes down (the army splits)", p2 && d2 == 1, $"dir={d2}");
				bool p3 = ChargeLanes.Plan(lineUp, 5000f, 21f, true, 6f, 140, false, 5100f, 1, 0f, out int d3, out _);
				Check("with the floor 100 px below the only way is up", p3 && d3 == -1);
				Check("no flight and no drop gives no plan", !ChargeLanes.Plan(lineUp, 5000f, 21f, false, 6f, 0, false, 9000f, 0, 0f, out _, out _));
				Check("fuel for 10 ticks cannot cover 290 px at 6 px/tick: no plan", !ChargeLanes.Plan(lineUp, 5000f, 21f, true, 6f, 10, false, 5100f, 0, 0f, out _, out _));
				bool p4 = ChargeLanes.Plan(lineUp, 5000f, 21f, false, 6f, 0, true, 5000f + 16f * 40f, 0, 0f, out int d4, out _);
				Check("a levy that cannot fly but can drop through a platform goes down", p4 && d4 == 1);
				// a fast body that is not a Leviathan: only a very large, level, fast, hurting body is a lane
				Check("a 100x100 body at 25 px/tick is no lane (the threat field has it)", !ChargeLanes.FastBodyLane(boss, new Vector2(25f, 0f), 100f, 100f, 100, out _));
				Check("a 700x400 body at 18 px/tick going level is a lane", ChargeLanes.FastBodyLane(boss, new Vector2(18f, 1f), 700f, 400f, 100, out var fb) && fb.Kind == 3 && fb.Dir == 1);
				Check("a 700x400 body at 18 px/tick going steeply down is no lane", !ChargeLanes.FastBodyLane(boss, new Vector2(8f, 16f), 700f, 400f, 100, out _));
				Check("a 700x400 body at 8 px/tick is no lane", !ChargeLanes.FastBodyLane(boss, new Vector2(8f, 0f), 700f, 400f, 100, out _));
			}
			catch (System.Exception e) { Check("self-check ran", false, e.GetType().Name + ": " + e.Message); }
			Mod.Logger.Info($"[ChargeLane] self-check pass={pass} fail={fail}");
		}
	}
}
