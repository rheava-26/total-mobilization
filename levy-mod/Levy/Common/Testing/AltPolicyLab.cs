using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Levy.Common.Testing
{
	/// <summary>
	/// ALT-FIRE POLICY PROTOTYPE (altfire-survey; harness only, changes nothing unless LEVY_HARNESS and LEVY_ALT_MODE are both set).
	///
	/// The soldier's alt path (WeaponUse.TryAltAttack) asks <c>ItemLoader.CanUseItem(item, dummy)</c> with <c>dummy.altFunctionUse == 2</c> and falls back to the primary when that says no.
	/// This GlobalItem sits in that call and answers for the experiment, so the production code is untouched:
	///   LEVY_ALT_MODE=random  every alt is let through (today's behaviour: AltUseChance 0.8, 75-105 tick gap)
	///   LEVY_ALT_MODE=left    every alt is vetoed: the soldier fires only the left-click attack
	///   LEVY_ALT_MODE=policy  an alt is let through only when the item's row in the policy table (LEVY_ALT_TABLE, tools/altfire/altfire_items.tsv) says the situation fits
	/// Policies it can evaluate here (the harness has one target, optionally a pinned crowd via LEVY_HARNESS_CROWD=N, no incoming fire):
	///   ALT_HEAVY (tough target, distance band), ALT_AOE (crowd size around the target), ALT_RATE (turbo inside a distance), ALT_MIX (generic: a lower chance),
	///   TOGGLE_N (swap every N primary shots), DETONATE (enough of the weapon's own projectiles out), PLACE (none of its placed object out),
	///   HOLD_VARIANT (set Calamity's mouseRight on the dummy at the moment the shot spawns, when the crowd/tough condition holds), NEVER.
	/// Per-item counters (alt asked / let through / vetoed, primary shots) are appended to the trial's notes column.
	/// </summary>
	public class AltPolicyLab : GlobalItem
	{
		internal static readonly string Mode = (Environment.GetEnvironmentVariable("LEVY_ALT_MODE") ?? "").Trim().ToLowerInvariant();
		internal static readonly int CrowdN = Math.Max(1, Math.Min(12, int.TryParse(Environment.GetEnvironmentVariable("LEVY_HARNESS_CROWD"), out int cn) ? cn : 1));
		/// <summary>LEVY_ALT_TOUGH=0/1 forces the "boss or tough target" condition (the harness target has a million life, so it would always read tough).</summary>
		internal static readonly int ToughEnv = int.TryParse(Environment.GetEnvironmentVariable("LEVY_ALT_TOUGH"), out int te) ? te : -1;
		private static bool Active => CombatHarness.Enabled && Mode != "";

		private sealed class Row { public string Policy = "ALT_MIX"; public Dictionary<string, string> P = new(StringComparer.OrdinalIgnoreCase); }
		private static Dictionary<string, Row> _table;
		private static readonly Dictionary<string, int[]> Count = new(); // name -> { asked, passed, vetoed, primaryShots, holdSet }
		private static readonly Dictionary<string, int> PrimSince = new();

		private static Row Lookup(string name)
		{
			if (_table == null)
			{
				_table = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
				try
				{
					string path = Environment.GetEnvironmentVariable("LEVY_ALT_TABLE");
					if (!string.IsNullOrEmpty(path) && File.Exists(path))
					{
						int ci = 0, cp = 6, cq = 7; // columns by header name: item, policy, params (tools/altfire/*_altfire_items.tsv)
						foreach (string line in File.ReadAllLines(path))
						{
							if (line.StartsWith("#")) continue;
							string[] c = line.Split('\t');
							if (c[0] == "item") { ci = Array.IndexOf(c, "item"); cp = Array.IndexOf(c, "policy"); cq = Array.IndexOf(c, "params"); continue; }
							if (c.Length <= cq || c[ci].Length == 0) continue;
							var r = new Row { Policy = c[cp].Trim() };
							foreach (string kv in c[cq].Split(';', StringSplitOptions.RemoveEmptyEntries))
							{
								int eq = kv.IndexOf('=');
								if (eq > 0) r.P[kv.Substring(0, eq).Trim()] = kv.Substring(eq + 1).Trim();
							}
							_table[c[ci].Trim()] = r;
						}
					}
				}
				catch (Exception) { }
			}
			return _table.TryGetValue(name, out Row row) ? row : null;
		}

		private static float F(Row r, string k, float d) => r.P.TryGetValue(k, out string v) && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : d;

		private static int[] C(string name) { if (!Count.TryGetValue(name, out int[] a)) Count[name] = a = new int[5]; return a; }

		internal static string TakeNotes()
		{
			if (!Active || Count.Count == 0) { Count.Clear(); PrimSince.Clear(); return ""; }
			var sb = new StringBuilder();
			sb.Append(" ALT[").Append(Mode).Append(CrowdN > 1 ? ",crowd" + CrowdN : "").Append("]:");
			foreach (var kv in Count) sb.Append(' ').Append(kv.Key).Append(" asked=").Append(kv.Value[0]).Append(" pass=").Append(kv.Value[1]).Append(" veto=").Append(kv.Value[2]).Append(" prim=").Append(kv.Value[3]).Append(kv.Value[4] > 0 ? " hold=" + kv.Value[4] : "");
			Count.Clear(); PrimSince.Clear();
			return sb.ToString();
		}

		// ---- situation, read from the harness' pinned targets (a real implementation reads SoldierNPC's current target and the squad's enemy list) ----
		private struct Ctx { public bool Has; public float Dist; public int Crowd; public bool Tough; }

		private static Ctx Read(Player d, float crowdRadiusTiles)
		{
			var c = new Ctx();
			NPC best = null; float bd = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !n.TryGetGlobalNPC(out HarnessTarget ht) || !ht.IsTarget) continue;
				float dd = Vector2.DistanceSquared(d.Center, n.Center);
				if (dd < bd) { bd = dd; best = n; }
			}
			if (best == null) return c;
			c.Has = true; c.Dist = (float)Math.Sqrt(bd) / 16f; c.Tough = ToughEnv >= 0 ? ToughEnv == 1 : (best.lifeMax >= 600 || best.boss);
			float r2 = crowdRadiusTiles * 16f; r2 *= r2;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget && Vector2.DistanceSquared(best.Center, n.Center) <= r2) c.Crowd++;
			}
			return c;
		}

		private static int CountProj(string modProjName)
		{
			int n = 0;
			for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && p.ModProjectile != null && p.ModProjectile.Name == modProjName) n++; }
			return n;
		}

		private static bool Eval(Row r, string name, Player d)
		{
			Ctx x = Read(d, F(r, "radius", 8f));
			if (!x.Has) return false;
			float minD = F(r, "minDist", 0f), maxD = F(r, "maxDist", 99f);
			switch (r.Policy)
			{
				case "NEVER": return false;
				case "ALT_HEAVY": return x.Tough && x.Dist >= minD && x.Dist <= maxD;
				case "ALT_AOE": return x.Crowd >= (int)F(r, "minEnemies", 3f) && x.Dist >= minD && x.Dist <= maxD;
				case "ALT_RATE": return x.Dist <= maxD;
				case "ALT_FAR": return x.Dist >= minD;
				case "ALT_ALWAYS": return true;
				case "TOGGLE_N": { PrimSince.TryGetValue(name, out int n); if (n >= (int)F(r, "afterShots", 8f)) { PrimSince[name] = 0; return true; } return false; }
				case "DETONATE": return r.P.TryGetValue("proj", out string pn) && CountProj(pn) >= (int)F(r, "minOut", 1f);
				case "PLACE": return r.P.TryGetValue("proj", out string pp) && CountProj(pp) < (int)F(r, "maxOut", 1f) && x.Dist <= maxD;
				case "HOLD_VARIANT": return true; // the alt goes through the normal path too; the hold flag is set in Shoot
				default: // ALT_MIX / unlisted: the generic default, a lower chance than today's 0.8 (the caller already rolled 0.8)
					return Main.rand.NextFloat() < F(r, "p", 0.4f) / 0.8f;
			}
		}

		public override bool CanUseItem(Item item, Player player)
		{
			if (!Active || player.altFunctionUse != 2) return true;
			string name = item.ModItem?.Name ?? item.Name;
			int[] c = C(name); c[0]++;
			bool ok;
			if (Mode == "left") ok = false;
			else if (Mode == "random") ok = true;
			else ok = Eval(Lookup(name) ?? new Row(), name, player);
			if (ok) c[1]++; else c[2]++;
			return ok;
		}

		public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			if (!Active) return true;
			string name = item.ModItem?.Name ?? item.Name;
			if (player.altFunctionUse != 2)
			{
				int[] c = C(name); c[3]++;
				PrimSince[name] = PrimSince.TryGetValue(name, out int n) ? n + 1 : 1;
			}
			// HOLD_VARIANT: weapons that read "right mouse held" when their holdout spawns (Hellborn, Ion Blaster, Hellkite...). left = never held; random = always; policy = when the condition holds.
			Row r = Mode == "policy" ? Lookup(name) : null;
			if (Mode == "left" || Mode == "random" || (r != null && r.Policy == "HOLD_VARIANT"))
			{
				bool hold = false;
				if (Mode == "random") hold = HoldTable(name) && Main.rand.NextFloat() < 0.8f;
				else if (r != null && r.Policy == "HOLD_VARIANT")
				{
					Ctx x = Read(player, F(r, "radius", 8f));
					string cond = r.P.TryGetValue("cond", out string cs) ? cs : "aoe";
					hold = x.Has && (cond == "heavy" ? x.Tough && x.Dist >= F(r, "minDist", 0f) : x.Crowd >= (int)F(r, "minEnemies", 3f));
				}
				if (SetMouseRight(player, hold) && hold) C(name)[4]++;
			}
			return true;
		}

		/// <summary>In random mode the hold items follow the old 80% rule only if the table lists them as HOLD_VARIANT (so the comparison is like for like).</summary>
		private static bool HoldTable(string name) { Row r = Lookup(name); return r != null && r.Policy == "HOLD_VARIANT"; }

		// ---- Calamity's CalamityPlayer.mouseRight, by reflection (no compile-time reference) ----
		private static bool _ci, _cok; private static ModPlayer _cpBase; private static MethodInfo _getMp; private static FieldInfo _mr;
		private static bool SetMouseRight(Player p, bool v)
		{
			if (!_ci)
			{
				_ci = true;
				try
				{
					if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
					{
						Type t = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
						_cpBase = ModContent.GetContent<ModPlayer>().FirstOrDefault(m => m.GetType() == t);
						_getMp = typeof(Player).GetMethods().First(m => m.Name == "GetModPlayer" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1).MakeGenericMethod(t);
						_mr = t.GetField("mouseRight", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						_cok = _cpBase != null && _mr != null;
					}
				}
				catch (Exception) { _cok = false; }
			}
			if (!_cok) return false;
			try { object cp = _getMp.Invoke(p, new object[] { _cpBase }); _mr.SetValue(cp, v); return true; } catch (Exception) { return false; }
		}
	}
}
