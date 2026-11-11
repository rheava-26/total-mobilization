using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Boot self-check of "Stand down all" / "Remobilize all": the sweep puts every Regular of the commander in the reserve, a second sweep is a no-op, the reverse brings them all back,
	/// and another commander's levies and the militia are left alone. Pure data (records with no bodies), cleaned up in the same call. Never throws.
	/// </summary>
	public class ReserveAllCheck : ModSystem
	{
		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(Run);

		private void Run()
		{
			var failures = new List<string>(); int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			var roster = RosterSystem.Instance; var added = new List<SoldierRecord>();
			try
			{
				if (roster == null) throw new InvalidOperationException("no roster system");
				var me = new Player { name = "Reserve Check" };
				SoldierRecord Mk(int id, string cmd, SoldierKind kind = SoldierKind.Regular) { var r = new SoldierRecord { Id = id, Name = "Pvt. " + id, Kind = kind, Grade = Grade.Levy, CommanderName = cmd }; roster.Regulars.Add(r); added.Add(r); return r; }
				var a = Mk(990001, me.name); var b = Mk(990002, me.name); var c = Mk(990003, me.name);
				var other = Mk(990004, "Someone Else"); var militia = Mk(990005, me.name, SoldierKind.Militia);
				b.Reserve = true; // one is already in the reserve before the sweep

				int n = roster.SetReserveAll(me, true);
				Check(a.Reserve && b.Reserve && c.Reserve, "stand down all: every Regular of the commander is in the reserve");
				Check(n == 2, "stand down all: counted the two that moved, not the one already benched (" + n + ")");
				Check(!other.Reserve, "stand down all: another commander's levy is left alone");
				Check(!militia.Reserve, "stand down all: the militia is left alone");
				Check(roster.SetReserveAll(me, true) == 0, "stand down all twice is a no-op");

				n = roster.SetReserveAll(me, false);
				Check(!a.Reserve && !b.Reserve && !c.Reserve, "remobilize all: every benched Regular is back on duty");
				Check(n == 3, "remobilize all: counted the three that moved (" + n + ")");
				Check(a.Order == SoldierOrder.Follow && !a.StoodDown, "remobilize all: they come back Following");
				Check(roster.SetReserveAll(me, false) == 0, "remobilize all twice is a no-op");
				Check(!other.Reserve && !militia.Reserve, "remobilize all: nobody else was touched");
			}
			catch (Exception e) { failures.Add("threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim()); }
			finally { foreach (var r in added) roster?.Regulars.Remove(r); }
			if (failures.Count == 0) Mod.Logger.Info($"[ReserveAll] self-check ok ({ok} checks: stand down all / remobilize all round trip)");
			else { Mod.Logger.Warn($"[ReserveAll] self-check {failures.Count} FAILURE(S) ({ok} ok) !!!"); foreach (var f in failures) Mod.Logger.Warn("  reserveall: " + f); }
		}
	}
}
