using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// GEAR COST probe for the calrev harness (lab "lagsweep", LEVY_GEARCOST=1): what each levy's puppet player costs through the equipment hooks of every loaded mod, per levy per tick.
	/// Instrumented once, at the first fight (PerfProbe: inclusive and exclusive wall time and calls per method, main thread only):
	///  - every ModPlayer hook override of every loaded mod (ResetEffects, PreUpdate, UpdateEquips, PostUpdateEquips, PostUpdateMiscEffects, PostUpdateBuffs, life regen, ...);
	///  - every GlobalItem / GlobalBuff override and every ModItem / ModBuff override named UpdateAccessory, UpdateEquip, UpdateArmorSet, UpdateInventory, UpdateVanitySet or Update;
	///  - every method of the types named in LEVY_GEARCOST_DEEP (default CalamityPlayer: its helpers, MiscEffects, OtherBuffEffects, UpdateRippers...);
	///  - Levy's own frame steps (GearFrame, DebuffDot, RefreshStats, GearOnHurt) and the base game's Player.ResetEffects / UpdateEquips / UpdateBuffs, so the hooks sit inside the frame that calls them.
	/// Timing starts 6 s into each fight (the opening wave) and the table goes to &lt;out&gt;.gearcost.tsv at the end of the fight: one row per method, ms per tick (exclusive, inclusive), calls per tick, and the mean levies alive.
	/// Measurement only.
	/// </summary>
	public partial class CombatHarness
	{
		internal static readonly bool GcOn = Environment.GetEnvironmentVariable("LEVY_GEARCOST") == "1";
		private static bool _gcInstalled; private static int _gcTicks; private static long _gcLevyTicks;
		private static readonly HashSet<string> GcItemHooks = new() { "UpdateAccessory", "UpdateEquip", "UpdateArmorSet", "UpdateInventory", "UpdateVanitySet", "Update", "UpdateBuffs" };

		private static void GcInstall()
		{
			if (_gcInstalled) return; _gcInstalled = true;
			PerfProbe.SetMainThread();
			var deep = (Environment.GetEnvironmentVariable("LEVY_GEARCOST_DEEP") ?? "CalamityPlayer").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
			int before = PerfProbe.Wrapped;
			foreach (Mod mod in ModLoader.Mods)
			{
				if (mod.Name == "ModLoader" || mod.Code == null) continue;
				Type[] types; try { types = mod.Code.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); }
				foreach (Type t in types)
				{
					try
					{
						if (t.IsAbstract || t.IsGenericTypeDefinition || t.Name.Contains('<') || (t.Namespace ?? "").Contains("Testing")) continue;
						bool player = typeof(ModPlayer).IsAssignableFrom(t), gItem = typeof(GlobalItem).IsAssignableFrom(t), gBuff = typeof(GlobalBuff).IsAssignableFrom(t);
						bool mItem = typeof(ModItem).IsAssignableFrom(t), mBuff = typeof(ModBuff).IsAssignableFrom(t), deepT = deep.Contains(t.Name);
						if (!(player || gItem || gBuff || mItem || mBuff || deepT)) continue;
						foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
						{
							if (m.IsAbstract || m.IsGenericMethodDefinition || m.IsSpecialName || m.Name.Contains('<')) continue;
							MethodInfo b; try { b = m.GetBaseDefinition(); } catch (Exception) { continue; }
							bool hook = b != m && b.DeclaringType != null && (b.DeclaringType == typeof(ModPlayer) || b.DeclaringType == typeof(GlobalItem) || b.DeclaringType == typeof(GlobalBuff) || b.DeclaringType == typeof(ModItem) || b.DeclaringType == typeof(ModBuff));
							bool want = deepT || (hook && (player || ((gItem || gBuff || mItem || mBuff) && GcItemHooks.Contains(m.Name))));
							if (!want) continue;
							string kind = player ? "player" : gItem ? "gitem" : gBuff ? "gbuff" : mItem ? "item" : mBuff ? "buff" : "deep";
							// ModItem / ModBuff overrides are filed by hook name (thousands of items): the label says which mod and which hook; players and globals by type
							string label = (mItem || mBuff) && !deepT ? $"{mod.Name}:{kind}*.{m.Name}" : $"{mod.Name}:{(deepT && !hook ? "~" : "")}{t.Name}.{m.Name}";
							PerfProbe.Wrap(m, label);
						}
					}
					catch (Exception) { }
				}
			}
			void Own(Type t, string name, string label) { try { foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(x => x.Name == name && x.DeclaringType == t)) PerfProbe.Wrap(m, label); } catch (Exception) { } }
			Own(typeof(SoldierNPC), "GearFrame", "Levy:frame.GearFrame"); Own(typeof(SoldierNPC), "DebuffDot", "Levy:frame.DebuffDot"); Own(typeof(SoldierNPC), "RefreshStats", "Levy:frame.RefreshStats"); Own(typeof(SoldierNPC), "GearOnHurt", "Levy:frame.GearOnHurt");
			Own(typeof(SoldierNPC), "AIBody", "Levy:frame.AIBody");
			Own(typeof(Player), "ResetEffects", "Terraria:Player.ResetEffects"); Own(typeof(Player), "UpdateEquips", "Terraria:Player.UpdateEquips"); Own(typeof(Player), "UpdateBuffs", "Terraria:Player.UpdateBuffs"); Own(typeof(Player), "UpdateArmorSets", "Terraria:Player.UpdateArmorSets");
			global::Levy.Levy.Instance.Logger.Info($"HARNESS-GC: instrumented {PerfProbe.Wrapped - before} methods ({PerfProbe.WrapFailed} failed)");
		}

		private static void GcTick(CrState st)
		{
			if (!GcOn || st == null) return;
			if (st.Tick == 1) { GcInstall(); PerfProbe.On = false; }
			if (st.Tick == 360) { PerfProbe.Reset(); PerfProbe.On = true; _gcTicks = 0; _gcLevyTicks = 0; }
			if (st.Tick > 360) { _gcTicks++; foreach (var l in st.Levies) if (!l.Dead) _gcLevyTicks++; }
		}

		private void GcFinish(CrRun run)
		{
			if (!GcOn) return;
			PerfProbe.On = false;
			if (_gcTicks <= 0) return;
			double levies = _gcLevyTicks / (double)_gcTicks;
			var rows = PerfProbe.Rows(_gcTicks);
			try
			{
				using var w = new StreamWriter(_crOut + ".gearcost.tsv", true);
				foreach (var r in rows) w.WriteLine(string.Join("\t", run.Key, _gcTicks, levies.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), r.Name,
					r.ExclMs.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture), r.InclMs.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture), r.CallsPerTick.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS-GC: write failed " + e.Message); }
			Mod.Logger.Info($"HARNESS-GC: {run.Key} {_gcTicks} ticks, {levies:0.0} levies; top: " + string.Join(", ", rows.Take(8).Select(r => $"{r.Name} {r.ExclMs:0.000}")));
			PerfProbe.Reset();
		}
	}
}
