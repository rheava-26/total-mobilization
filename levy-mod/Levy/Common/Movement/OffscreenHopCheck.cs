using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Movement
{
	/// <summary>Boot self-check (headless server): who can see a point (the 2400x1400 box around every active player) and which hop target the catch-up chooses. Logged as "[HopCheck]".</summary>
	public class OffscreenHopCheck : ModSystem
	{
		public override void PostSetupRecipes()
		{
			if (!Main.dedServ) return;
			int pass = 0, fail = 0;
			void Check(string name, bool ok, string detail = "") { if (ok) pass++; else fail++; Mod.Logger.Info($"[HopCheck] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}"); }
			try
			{
				var one = new List<Vector2> { new Vector2(10000f, 5000f) };
				Check("a point 1199 px across and 699 px up from a player is seen", OffscreenHop.SeenBy(new Vector2(11199f, 4301f), one));
				Check("1201 px across is not seen", !OffscreenHop.SeenBy(new Vector2(11201f, 5000f), one));
				Check("701 px above is not seen", !OffscreenHop.SeenBy(new Vector2(10000f, 4299f), one));
				var two = new List<Vector2> { new Vector2(10000f, 5000f), new Vector2(30000f, 5000f) };
				Check("with two players a point near the second is seen, one between them is not", OffscreenHop.SeenBy(new Vector2(30500f, 5300f), two) && !OffscreenHop.SeenBy(new Vector2(20000f, 5000f), two));

				// a route of ten waypoints 1000 px apart, a player standing at waypoint 9: waypoints 8 and 9 are in the box (1000 px away is inside 1200), 7 is the furthest that is out of sight
				var route = new List<Vector2>(); for (int i = 0; i < 10; i++) route.Add(new Vector2(i * 1000f, 0f));
				var watcher = new List<Vector2> { route[9] };
				int pick = OffscreenHop.PickWaypoint(route.Count, 2, k => !OffscreenHop.SeenBy(route[k], watcher));
				Check("the hop goes to the furthest waypoint out of sight (index 7 of 0..9, player at 9)", pick == 7, $"pick={pick}");
				Check("no waypoint ahead out of sight gives no hop", OffscreenHop.PickWaypoint(route.Count, 8, k => !OffscreenHop.SeenBy(route[k], watcher)) == -1);
				Check("with nobody near the hop goes to the end of the route", OffscreenHop.PickWaypoint(route.Count, 2, k => !OffscreenHop.SeenBy(route[k], new List<Vector2> { new Vector2(-50000f, 0f) })) == 9);

				// the way to a goal that a player can see: the point nearest the goal that is outside the box and at least 10 tiles from the start
				Vector2 me = new Vector2(0f, 0f), goal = new Vector2(200f * 16f, 0f);
				var atGoal = new List<Vector2> { goal };
				bool got = OffscreenHop.PickOnTheWay(me, goal, 10f, q => !OffscreenHop.SeenBy(q, atGoal), out Vector2 at);
				Check("on the way to a seen goal the hop lands outside the box, nearest the goal", got && at.X < goal.X - 1200f && at.X > goal.X - 1200f - 4.1f * 16f, $"at.X={at.X:0} goal.X={goal.X:0}");
				bool none = OffscreenHop.PickOnTheWay(me, new Vector2(15f * 16f, 0f), 10f, q => !OffscreenHop.SeenBy(q, new List<Vector2> { me }), out _);
				Check("a goal 15 tiles away under the same player gives no hop", !none);
			}
			catch (System.Exception e) { Check("self-check ran", false, e.GetType().Name + ": " + e.Message); }
			Mod.Logger.Info($"[HopCheck] DONE pass={pass} fail={fail}");
		}
	}
}
