using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Testing;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>When a soldier right-clicks. One kind per alt-fire pattern; see tools/altfire/patterns.md.</summary>
	public enum AltPolicyKind
	{
		Never, Mix, Heavy, Aoe, Rate, Far, Always, ToggleN, Detonate, Place, Guard, MeterReady, HoldVariant, ManaRefill, StackUp, AllyHeal, DashClose, PortalUse,
	}

	/// <summary>The numbers of one policy row (the <c>params</c> column: <c>key=value;key=value</c>). Unset values keep the defaults below.</summary>
	public struct AltParams
	{
		public float P, MinDist, MaxDist, Radius, HpBelow, OwnHpBelow, OwnHpAbove, ManaBelow, Near, NoEnemyWithin, Min, Max, ToughLife;
		public int MinEnemies, Enemies, MinOut, MaxOut, AfterShots, MinWounded, MaxStacks;
		public bool Tough, Invert, MinFull, Primary, Steer;
		/// <summary>Projectile name (resolved against the item's mod) for <see cref="AltPolicyKind.Detonate"/> / <see cref="AltPolicyKind.Place"/>.</summary>
		public string Proj, Cond, Meter, MeterMax;

		public static AltParams Defaults() => new AltParams
		{
			P = 0.4f, MinDist = 0f, MaxDist = 999f, Radius = 8f, HpBelow = 1f, OwnHpBelow = 2f, OwnHpAbove = -1f, ManaBelow = 1f, Near = 8f, NoEnemyWithin = -1f, Min = 1f, Max = 100f, ToughLife = 600f,
			MinEnemies = 2, Enemies = 0, MinOut = 1, MaxOut = 1, AfterShots = 8, MinWounded = 1, MaxStacks = 3, Cond = "aoe",
		};

		private static float F(string v, float d) => float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : d;
		private static int I(string v, int d) => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : d;

		/// <summary>Parses a <c>params</c> cell. Unknown keys are ignored.</summary>
		public static AltParams Parse(string cell)
		{
			AltParams a = Defaults();
			if (string.IsNullOrWhiteSpace(cell)) return a;
			foreach (string kv in cell.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				int eq = kv.IndexOf('=');
				if (eq <= 0) continue;
				string k = kv.Substring(0, eq).Trim(), v = kv.Substring(eq + 1).Trim();
				switch (k.ToLowerInvariant())
				{
					case "p": a.P = F(v, a.P); break;
					case "mindist": a.MinDist = F(v, a.MinDist); break;
					case "maxdist": a.MaxDist = F(v, a.MaxDist); break;
					case "radius": a.Radius = F(v, a.Radius); break;
					case "hpbelow": a.HpBelow = F(v, a.HpBelow); break;
					case "ownhpbelow": a.OwnHpBelow = F(v, a.OwnHpBelow); break;
					case "ownhpabove": a.OwnHpAbove = F(v, a.OwnHpAbove); break;
					case "manabelow": a.ManaBelow = F(v, a.ManaBelow); break;
					case "near": a.Near = F(v, a.Near); break;
					case "noenemywithin": a.NoEnemyWithin = F(v, a.NoEnemyWithin); break;
					case "minenemies": a.MinEnemies = I(v, a.MinEnemies); break;
					case "enemies": a.Enemies = I(v, a.Enemies); break;
					case "minout": a.MinOut = I(v, a.MinOut); break;
					case "maxout": a.MaxOut = I(v, a.MaxOut); break;
					case "aftershots": a.AfterShots = I(v, a.AfterShots); break;
					case "minwounded": a.MinWounded = I(v, a.MinWounded); break;
					case "maxstacks": a.MaxStacks = I(v, a.MaxStacks); break;
					case "tough": a.Tough = v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase); break;
					case "invert": a.Invert = v == "1"; break;
					case "primary": a.Primary = v == "1"; break;
					case "steer": a.Steer = v == "1"; break;
					case "proj": a.Proj = v; break;
					case "cond": a.Cond = v; break;
					case "meter": a.Meter = v; break;
					case "max": if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float mx)) a.Max = mx; else a.MeterMax = v; break;
					case "min": if (v.Equals("full", StringComparison.OrdinalIgnoreCase)) a.MinFull = true; else a.Min = F(v, a.Min); break;
				}
			}
			return a;
		}
	}

	/// <summary>One row of the registry: what to do for one item.</summary>
	public sealed class AltPolicy
	{
		public AltPolicyKind Kind = AltPolicyKind.Mix;
		public AltParams P = AltParams.Defaults();
		/// <summary>The owning mod and item name of the row (for logs and projectile lookup); empty for the default row.</summary>
		public string Mod = "", Item = "";
		/// <summary>Projectile type resolved from <see cref="AltParams.Proj"/> after content load; -1 when none/unresolved.</summary>
		public int ProjType = -1;
		/// <summary>Free text from the survey (evidence, caveats).</summary>
		public string Note = "";
	}

	/// <summary>Per-soldier alt-fire bookkeeping that is not worth a field on SoldierNPC.</summary>
	internal sealed class AltState
	{
		public int PrimarySinceAlt, AltStacks;
		public bool MouseRightSet;
		public int LastItemType = -1;
		public uint HoldCheckTick;
	}

	/// <summary>
	/// A lazy view of the situation for one right-click decision. Every number is computed on first use and cached for the decision: a policy that only needs the distance never scans the NPC list.
	/// </summary>
	public sealed class AltCtx
	{
		public readonly SoldierNPC S;
		public readonly NPC Target;
		private float _dist = -1f; private int _crowd = -1; private float _crowdR = -1f; private int _tough = -1;
		public AltCtx(SoldierNPC s, NPC target) { S = s; Target = target; }

		// Fixed values for the self check: when set they stand in for what the world would say.
		internal float? TDist, TSelfHp, TMana; internal int? TCrowd, TOut, TWounded, TNear; internal bool? TTough; internal Dictionary<string, float> TMeter; internal AltState TState;

		/// <summary>Tiles from the soldier to the target's centre.</summary>
		public float Dist => TDist ?? (_dist >= 0f ? _dist : (_dist = Target == null ? 999f : Vector2.Distance(S.NPC.Center, Target.Center) / 16f));

		/// <summary>True for a boss or a target with <paramref name="life"/> or more max life. The harness can force it (LEVY_ALT_TOUGH).</summary>
		public bool IsTough(float life)
		{
			if (TTough.HasValue) return TTough.Value;
			if (CombatHarness.Enabled && AltPolicyLab.ToughEnv >= 0) return AltPolicyLab.ToughEnv == 1;
			if (_tough < 0) _tough = Target != null && (Target.boss || Target.lifeMax >= life) ? 1 : 0;
			return _tough == 1;
		}

		/// <summary>Hostile, chaseable NPCs within <paramref name="radiusTiles"/> of the target (the target counts).</summary>
		public int Crowd(float radiusTiles)
		{
			if (TCrowd.HasValue) return TCrowd.Value;
			if (_crowd >= 0 && Math.Abs(_crowdR - radiusTiles) < 0.01f) return _crowd;
			int n = 0;
			if (Target != null)
			{
				float r2 = radiusTiles * 16f; r2 *= r2;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC e = Main.npc[i];
					if (e.active && LevyUtil.IsHostileTarget(e) && Vector2.DistanceSquared(e.Center, Target.Center) <= r2) n++;
				}
			}
			_crowd = n; _crowdR = radiusTiles;
			return n;
		}

		/// <summary>Hostile NPCs within <paramref name="tiles"/> of the soldier.</summary>
		public int Near(float tiles)
		{
			if (TNear.HasValue) return TNear.Value;
			int n = 0; float r2 = tiles * 16f; r2 *= r2;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC e = Main.npc[i];
				if (e.active && LevyUtil.IsHostileTarget(e) && Vector2.DistanceSquared(e.Center, S.NPC.Center) <= r2) n++;
			}
			return n;
		}

		public float SelfHp => TSelfHp ?? (S.NPC.lifeMax > 0 ? (float)S.NPC.life / S.NPC.lifeMax : 1f);
		public float ManaFrac => TMana ?? (S.Record != null && S.Record.MaxMana > 0 ? (float)S.Record.Mana / S.Record.MaxMana : 1f);

		/// <summary>This soldier's live projectiles of <paramref name="projType"/>.</summary>
		public int Out(int projType)
		{
			if (TOut.HasValue) return TOut.Value;
			if (projType < 0) return 0;
			int n = 0, me = S.NPC.whoAmI;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.type == projType && p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex == me) n++;
			}
			return n;
		}

		/// <summary>Allies (the commander and this commander's soldiers) within <paramref name="radiusTiles"/> of the soldier that are below <paramref name="below"/> of their health.</summary>
		public int Wounded(float radiusTiles, float below)
		{
			if (TWounded.HasValue) return TWounded.Value;
			int n = 0; float r2 = radiusTiles * 16f; r2 *= r2;
			Player c = S.Commander;
			if (c != null && c.active && !c.dead && c.statLifeMax2 > 0 && (float)c.statLife / c.statLifeMax2 < below && Vector2.DistanceSquared(c.Center, S.NPC.Center) <= r2) n++;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC a = Main.npc[i];
				if (!a.active || a.whoAmI == S.NPC.whoAmI || a.ModNPC is not SoldierNPC o || o.Record == null || a.lifeMax <= 0) continue;
				if (!string.Equals(o.Record.CommanderName ?? "", S.Record?.CommanderName ?? "", StringComparison.Ordinal)) continue;
				if ((float)a.life / a.lifeMax < below && Vector2.DistanceSquared(a.Center, S.NPC.Center) <= r2) n++;
			}
			return n;
		}

		/// <summary>A named field or property of the dummy's Calamity / Thorium player, as a float (bool = 0/1). NaN when unreadable.</summary>
		public float Meter(string name) => TMeter != null ? (TMeter.TryGetValue(name ?? "", out float v) ? v : float.NaN) : AltPolicies.ReadMeter(S.Dummy, name);
	}

	/// <summary>
	/// The right-click policy registry. Rows (an embedded TSV, <c>Common/Weapons/AltPolicies.tsv</c>) are resolved to item types after content load; a row whose mod is not loaded is skipped, so a
	/// row exists only while its item does. Other mods can add rows with <see cref="Register"/>. <see cref="Want"/> replaces the old random roll: it decides, from the situation, whether this attack is
	/// the alt. It runs BEFORE <c>ItemLoader.AltFunctionUse</c> is probed, because some probes launch the alt themselves (Hydra).
	/// </summary>
	public static class AltPolicies
	{
		private static readonly Dictionary<int, AltPolicy> ByItem = new();
		private static readonly AltPolicy Default = new AltPolicy { Kind = AltPolicyKind.Mix };
		private static readonly ConditionalWeakTable<SoldierNPC, AltState> States = new();
		private static readonly Dictionary<string, int[]> Count = new();
		private static readonly HashSet<string> _warned = new();

		/// <summary>Rows loaded from the embedded table (after resolution), for the self check and logs.</summary>
		public static int RowCount => ByItem.Count;
		/// <summary>Rows in the table whose mod is not loaded or whose item was not found.</summary>
		public static int SkippedRows { get; private set; }
		public static int TableRows { get; private set; }
		public static int UnresolvedProj { get; private set; }
		/// <summary>True when some loaded row has <c>steer=1</c> (its projectiles follow the cursor, so they are steered at the soldier's target while they fly).</summary>
		public static bool AnySteer { get; private set; }

		internal static AltState StateOf(SoldierNPC s) => States.GetOrCreateValue(s);

		/// <summary>The policy for an item type; the generic default (<c>ALT_MIX p=0.4</c>) when it has no row.</summary>
		public static AltPolicy For(int itemType) => ByItem.TryGetValue(itemType, out AltPolicy p) ? p : Default;
		public static bool HasRow(int itemType) => ByItem.ContainsKey(itemType);

		/// <summary>Adds or replaces the row for <c>mod/item</c>. Returns false when that item does not exist (mod absent).</summary>
		public static bool Register(string mod, string item, AltPolicy policy)
		{
			if (policy == null || !ModContent.TryFind(mod, item, out ModItem mi)) return false;
			Resolve(policy, mod, item);
			ByItem[mi.Type] = policy;
			return true;
		}

		private static void Resolve(AltPolicy p, string mod, string item)
		{
			p.Mod = mod; p.Item = item; p.ProjType = -1;
			if (!string.IsNullOrEmpty(p.P.Proj) && ModContent.TryFind(mod, p.P.Proj, out ModProjectile mp)) p.ProjType = mp.Type;
		}

		// ---------------------------------------------------------------------------------------------
		// Loading
		// ---------------------------------------------------------------------------------------------
		private static readonly Dictionary<string, AltPolicyKind> Names = new(StringComparer.OrdinalIgnoreCase)
		{
			["NEVER"] = AltPolicyKind.Never, ["ALT_MIX"] = AltPolicyKind.Mix, ["ALT_HEAVY"] = AltPolicyKind.Heavy, ["ALT_AOE"] = AltPolicyKind.Aoe, ["ALT_RATE"] = AltPolicyKind.Rate,
			["ALT_FAR"] = AltPolicyKind.Far, ["ALT_ALWAYS"] = AltPolicyKind.Always, ["TOGGLE_N"] = AltPolicyKind.ToggleN, ["DETONATE"] = AltPolicyKind.Detonate, ["PLACE"] = AltPolicyKind.Place,
			["GUARD"] = AltPolicyKind.Guard, ["METER_READY"] = AltPolicyKind.MeterReady, ["HOLD_VARIANT"] = AltPolicyKind.HoldVariant, ["MANA_REFILL"] = AltPolicyKind.ManaRefill,
			["STACK_UP"] = AltPolicyKind.StackUp, ["ALLY_HEAL"] = AltPolicyKind.AllyHeal, ["DASH_CLOSE"] = AltPolicyKind.DashClose, ["PORTAL_USE"] = AltPolicyKind.PortalUse,
		};

		/// <summary>Parses a table (header line <c>mod item policy params [note]</c>, tab separated, <c>#</c> comments) into unresolved rows.</summary>
		public static List<(string mod, string item, AltPolicy policy)> ParseTable(string text)
		{
			var rows = new List<(string, string, AltPolicy)>();
			int cm = 0, ci = 1, cp = 2, cq = 3, cn = 4;
			foreach (string raw in text.Split('\n'))
			{
				string line = raw.TrimEnd('\r');
				if (line.Length == 0 || line[0] == '#') continue;
				string[] c = line.Split('\t');
				if (c[0] == "mod" && c.Length > 2 && c[1] == "item") { cm = 0; ci = 1; cp = Array.IndexOf(c, "policy"); cq = Array.IndexOf(c, "params"); cn = Array.IndexOf(c, "note"); continue; }
				if (c.Length <= cp || c[ci].Length == 0 || !Names.TryGetValue(c[cp].Trim(), out AltPolicyKind kind)) continue;
				var pol = new AltPolicy { Kind = kind, P = AltParams.Parse(cq >= 0 && c.Length > cq ? c[cq] : ""), Note = cn >= 0 && c.Length > cn ? c[cn] : "" };
				rows.Add((c[cm].Trim(), c[ci].Trim(), pol));
			}
			return rows;
		}

		/// <summary>Loads the embedded table and resolves it. Called from PostSetupContent (every mod's items exist by then).</summary>
		internal static void Load(Mod levy)
		{
			ByItem.Clear(); SkippedRows = 0; TableRows = 0; AnySteer = false;
			try
			{
				byte[] bytes = levy.GetFileBytes("Common/Weapons/AltPolicies.tsv");
				foreach (var (mod, item, pol) in ParseTable(System.Text.Encoding.UTF8.GetString(bytes)))
				{
					TableRows++;
					if (!ModLoader.HasMod(mod) || !ModContent.TryFind(mod, item, out ModItem mi)) { SkippedRows++; continue; }
					Resolve(pol, mod, item);
					ByItem[mi.Type] = pol;
					if (pol.P.Steer) AnySteer = true;
					if (!string.IsNullOrEmpty(pol.P.Proj) && pol.ProjType < 0) { UnresolvedProj++; levy.Logger.Info($"Levy alt-fire policies: {mod}/{item} names projectile '{pol.P.Proj}', which does not exist; that row never counts its own objects."); }
				}
				levy.Logger.Info($"Levy alt-fire policies: {ByItem.Count} rows resolved of {TableRows} ({SkippedRows} skipped: mod not loaded or item absent).");
			}
			catch (Exception e) { levy.Logger.Warn("Levy alt-fire policies: table failed to load; every alt uses the default (ALT_MIX p=0.4): " + e.Message); }
		}

		internal static void Unload() { ByItem.Clear(); Count.Clear(); _warned.Clear(); _meters.Clear(); _mp.Clear(); }

		// ---------------------------------------------------------------------------------------------
		// The gate
		// ---------------------------------------------------------------------------------------------
		/// <summary>Ticks to wait before asking again after a NEVER row (the gate is skipped, not rolled).</summary>
		public const int NeverRecheckTicks = 600;
		/// <summary>Ticks to wait before re-evaluating a situational policy that said no (keeps a crowd scan off every shot).</summary>
		public const int SituationalRecheckTicks = 15;

		/// <summary>
		/// Whether this attack should be the item's right-click. Replaces the old <c>AltUseChance</c> roll and is called BEFORE <c>ItemLoader.AltFunctionUse</c> is probed (some probes launch the alt:
		/// Hydra). <paramref name="recheck"/> is the number of ticks the caller should wait before asking again when the answer is no (0 = ask on the next attack).
		/// </summary>
		public static bool Want(SoldierNPC s, Item item, NPC target, out int recheck)
		{
			recheck = 0;
			AltPolicy pol = For(item.type);
			bool ok;
			string mode = CombatHarness.Enabled ? AltPolicyLab.Mode : "";
			if (mode == "left") ok = false;
			else if (mode == "random") ok = Main.rand.NextFloat() < LevyConstants.AltUseChance;
			else ok = Evaluate(s, item, pol, target, out recheck);
			if (CombatHarness.Enabled && mode != "") AltPolicyLab.Asked(item, ok);
			return ok;
		}

		public static bool Want(SoldierNPC s, Item item, NPC target) => Want(s, item, target, out _);

		/// <summary>The decision for one policy row (no harness override). Public so self checks can drive every kind.</summary>
		public static bool Evaluate(SoldierNPC s, Item item, AltPolicy pol, NPC target, out int recheck)
		{
			recheck = 0;
			AltParams a = pol.P;
			switch (pol.Kind)
			{
				case AltPolicyKind.Never: recheck = NeverRecheckTicks; return false;
				case AltPolicyKind.PortalUse: recheck = NeverRecheckTicks; return false; // not an item alt: the Dark Gate compat drives it
				case AltPolicyKind.Mix: return Main.rand.NextFloat() < a.P;
				case AltPolicyKind.Always: return true;
			}
			var x = new AltCtx(s, target);
			bool ok = EvaluateCtx(s, item, pol, x);
			if (!ok) recheck = SituationalRecheckTicks;
			return ok;
		}

		/// <summary>The situational kinds, on a prepared context.</summary>
		public static bool EvaluateCtx(SoldierNPC s, Item item, AltPolicy pol, AltCtx x)
		{
			AltParams a = pol.P;
			bool inBand = x.Dist >= a.MinDist && x.Dist <= a.MaxDist;
			switch (pol.Kind)
			{
				case AltPolicyKind.Heavy: return (!a.Tough || x.IsTough(a.ToughLife)) && inBand;
				case AltPolicyKind.Aoe: return x.Crowd(a.Radius) >= a.MinEnemies && inBand;
				case AltPolicyKind.Rate: return x.Dist <= a.MaxDist;
				case AltPolicyKind.Far: return x.Dist >= a.MinDist;
				case AltPolicyKind.ToggleN: return (x.TState ?? StateOf(x.S)).PrimarySinceAlt >= a.AfterShots;
				case AltPolicyKind.Detonate:
					if (PlayerConflict(pol, item, x)) return false;
					if (x.Out(pol.ProjType) < a.MinOut || x.Dist < a.MinDist) return false;
					return a.OwnHpBelow > 1.5f || x.SelfHp < a.OwnHpBelow;
				case AltPolicyKind.Place:
					if (PlayerConflict(pol, item, x)) return false;
					return x.Out(pol.ProjType) < a.MaxOut && x.Dist <= a.MaxDist && (!a.Tough || x.IsTough(a.ToughLife));
				case AltPolicyKind.Guard:
					if (x.SelfHp < a.HpBelow && x.Near(a.Near) >= Math.Max(1, a.Enemies)) return true;
					return a.Enemies > 0 && x.Near(a.Near) >= a.Enemies && x.SelfHp < Math.Min(1f, a.HpBelow + 0.25f);
				case AltPolicyKind.MeterReady:
				{
					if (a.OwnHpBelow < 1.5f && x.SelfHp >= a.OwnHpBelow) return false;
					float m = x.Meter(a.Meter ?? "");
					if (float.IsNaN(m)) { WarnOnce("meter:" + pol.Item, $"Levy alt-fire: meter '{a.Meter}' of {pol.Item} is unreadable; that alt is used as ALT_MIX p=0.3."); return Main.rand.NextFloat() < 0.3f; }
					bool ready;
					if (a.Invert) { float mx = !string.IsNullOrEmpty(a.MeterMax) ? x.Meter(a.MeterMax) : a.Max; ready = !float.IsNaN(mx) && mx > 0 && m / mx < 0.3f; }
					else if (a.MinFull) { float mx = !string.IsNullOrEmpty(a.MeterMax) ? x.Meter(a.MeterMax) : a.Max; ready = m >= (float.IsNaN(mx) ? 100f : mx) - 0.01f; }
					else ready = m >= a.Min;
					if (!ready) return false;
					return !a.Tough || x.IsTough(a.ToughLife) || x.Crowd(8f) >= 3;
				}
				case AltPolicyKind.HoldVariant: return HoldCond(a, x);
				case AltPolicyKind.ManaRefill:
					if (x.ManaFrac >= a.ManaBelow) return false;
					if (a.NoEnemyWithin >= 0f && x.Near(Math.Max(1f, a.NoEnemyWithin)) > 0 && a.NoEnemyWithin > 0f) return false;
					return a.MaxDist >= 900f || x.Dist <= a.MaxDist;
				case AltPolicyKind.StackUp:
					return (x.TState ?? StateOf(x.S)).AltStacks < a.MaxStacks && x.IsTough(a.ToughLife) && inBand;
				case AltPolicyKind.AllyHeal: return x.Wounded(a.Radius, a.HpBelow) >= a.MinWounded;
				case AltPolicyKind.DashClose: return x.Dist >= a.MinDist;
			}
			return false;
		}

		private static bool HoldCond(AltParams a, AltCtx x)
		{
			switch ((a.Cond ?? "aoe").ToLowerInvariant())
			{
				case "heavy": return x.IsTough(a.ToughLife) && x.Dist >= a.MinDist && x.Dist <= a.MaxDist;
				case "rate": return x.Dist <= a.MaxDist;
				case "close": return x.Dist <= a.MaxDist && x.Crowd(Math.Max(3f, a.MaxDist * 0.6f)) >= a.MinEnemies;
				default: return x.Crowd(a.Radius) >= a.MinEnemies;
			}
		}

		private static void WarnOnce(string key, string msg)
		{
			if (_warned.Add(key)) ModContent.GetInstance<Levy>()?.Logger.Info(msg);
		}


		// ---------------------------------------------------------------------------------------------
		// Interaction weapons: things that are placed, detonated, recalled or stepped through
		// ---------------------------------------------------------------------------------------------
		/// <summary>
		/// True when using this weapon would collide with the commander's own use of the same object: the commander holds the same item, or owns a live projectile of the row's object type.
		/// (Soldier shots carry the commander's player slot as owner, so an item that kills or detonates "my projectiles of this type" would take the commander's with it.) A soldier then holds off.
		/// </summary>
		public static bool PlayerConflict(AltPolicy pol, Item item, AltCtx x)
		{
			Player c = x.S?.Commander;
			if (c == null || !c.active || c.dead) return false;
			try { if (c.HeldItem != null && c.HeldItem.type == item.type) return true; } catch (Exception) { }
			return PlayerOwnsObject(pol.ProjType);
		}

		/// <summary>True when a live projectile of this type exists that no soldier fired (the commander's own).</summary>
		public static bool PlayerOwnsObject(int projType)
		{
			if (projType < 0) return false;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || p.type != projType) continue;
				if (!p.TryGetGlobalProjectile(out SoldierProjectileTag t) || t.SoldierIndex < 0) return true;
			}
			return false;
		}

		/// <summary>
		/// The guard on a PRIMARY attack that places a one-at-a-time object (barriers, auras, anvils, hammers, the energy projector). Their Shoot kills "all of the owner's" earlier objects, and soldier
		/// shots share the commander's owner slot, so an unguarded soldier would delete the commander's object (and other soldiers'). Allowed only when no other object of the type is out except this
		/// soldier's own, and the commander is not holding the same item.
		/// </summary>
		internal static bool PrimaryAllowed(SoldierNPC s, Item item)
		{
			if (!ByItem.TryGetValue(item.type, out AltPolicy pol) || !pol.P.Primary) return true;
			var x = new AltCtx(s, null);
			if (PlayerConflict(pol, item, x)) return false;
			if (pol.ProjType < 0) return true;
			int mine = x.Out(pol.ProjType), total = 0;
			for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && p.type == pol.ProjType) total++; }
			return total <= mine; // only this soldier's own object(s) are out
		}

		// ---------------------------------------------------------------------------------------------
		// Soldier-side bookkeeping
		// ---------------------------------------------------------------------------------------------
		/// <summary>A primary attack was dispatched: counts toward TOGGLE_N and ends a STACK_UP run.</summary>
		internal static void NotePrimary(SoldierNPC s, Item item)
		{
			AltState st = StateOf(s);
			st.PrimarySinceAlt++;
			st.AltStacks = 0;
			if (CombatHarness.Enabled) AltPolicyLab.Primary(item);
		}

		/// <summary>An alt fired.</summary>
		internal static void NoteAlt(SoldierNPC s, Item item)
		{
			AltState st = StateOf(s);
			st.PrimarySinceAlt = 0;
			st.AltStacks++;
		}

		// ---------------------------------------------------------------------------------------------
		// HoldVariant: the right button picks the variant of a channelled holdout when it spawns
		// ---------------------------------------------------------------------------------------------
		/// <summary>
		/// Before an attack: sets (or clears) <c>mouseRight</c> on the dummy's Calamity player for items whose variant is picked by the held right button, from the row's condition. Cleared as soon as the
		/// soldier holds anything else, so a stale button never reaches another weapon. No-op when Calamity is not loaded.
		/// </summary>
		internal static void HoldVariant(SoldierNPC s, Item item, NPC target)
		{
			Player d = s.Dummy;
			if (d == null) return;
			AltState st = StateOf(s);
			AltPolicy pol = For(item.type);
			string mode = CombatHarness.Enabled ? AltPolicyLab.Mode : "";
			bool isHold = pol.Kind == AltPolicyKind.HoldVariant && HasRow(item.type);
			if (!isHold)
			{
				if (st.MouseRightSet) { SetMouseRight(d, false); st.MouseRightSet = false; }
				return;
			}
			uint now = Main.GameUpdateCount;
			if (st.LastItemType == item.type && now - st.HoldCheckTick < (uint)SituationalRecheckTicks) return; // the variant is re-decided a few times a second, not every tick
			st.LastItemType = item.type; st.HoldCheckTick = now;
			bool want;
			if (mode == "left") want = false;
			else if (mode == "random") want = Main.rand.NextFloat() < LevyConstants.AltUseChance;
			else want = HoldCond(pol.P, new AltCtx(s, target));
			if (SetMouseRight(d, want)) { st.MouseRightSet = want; if (want && CombatHarness.Enabled) AltPolicyLab.Hold(item); }
		}

		/// <summary>A holdout was just spawned for a soldier (HeldWeapons.TryFire): the variant button is set for it.</summary>
		internal static void OnHoldoutSpawn(SoldierNPC s, Item item, NPC target) => HoldVariant(s, item, target);

		// ---------------------------------------------------------------------------------------------
		// AimPoint: the dummy's cursor on the target around a Shoot call
		// ---------------------------------------------------------------------------------------------
		/// <summary>
		/// Puts the cursor on <paramref name="target"/> for the span of a <c>using</c> block: <c>Main.mouseX/Y</c> (so <c>Main.MouseWorld</c> reads the target) and Calamity's per-player
		/// <c>mouseWorld</c>. Items that place or steer something at the cursor (Face Melter's amplifier, Frigidflash Bolt's vortex, the ocarina note fields) then act on the enemy, as for a player aiming.
		/// </summary>
		public readonly struct AimScope : IDisposable
		{
			private readonly bool _on; private readonly Vector2 _screen; private readonly int _mx, _my;
			private readonly Player _d; private readonly object _calPlayer; private readonly Vector2 _delta; private readonly float _rot;
			public AimScope(Player dummy, Vector2 worldPoint)
			{
				_d = dummy; _on = false; _screen = default; _mx = _my = 0; _calPlayer = null; _delta = default; _rot = 0f;
				if (dummy == null) return;
				_on = true;
				_screen = Main.screenPosition; _mx = Main.mouseX; _my = Main.mouseY;
				Main.screenPosition = Vector2.Zero;
				Main.mouseX = (int)worldPoint.X; Main.mouseY = (int)worldPoint.Y;
				_calPlayer = CalamityPlayerOf(dummy);
				if (_calPlayer != null) { (_delta, _rot) = GetCalAim(_calPlayer); SetCalAim(_calPlayer, worldPoint - dummy.MountedCenter); }
			}
			public void Dispose()
			{
				if (!_on) return;
				Main.screenPosition = _screen; Main.mouseX = _mx; Main.mouseY = _my;
				if (_calPlayer != null) SetCalAim(_calPlayer, _delta, _rot);
			}
		}

		// ---------------------------------------------------------------------------------------------
		// Reflection into other mods' ModPlayers (no compile-time reference to Calamity)
		// ---------------------------------------------------------------------------------------------
		private sealed class ModPlayerAccess { public ModPlayer Base; public MethodInfo Get; public Type Type; }
		private static readonly Dictionary<string, ModPlayerAccess> _mp = new();
		private static readonly Dictionary<string, MemberInfo> _meters = new();

		private static ModPlayerAccess Access(string mod, string typeName)
		{
			string key = mod + "/" + typeName;
			if (_mp.TryGetValue(key, out ModPlayerAccess acc)) return acc;
			acc = null;
			try
			{
				if (ModLoader.TryGetMod(mod, out Mod m) && m.Code != null)
				{
					Type t = m.Code.GetType(typeName);
					ModPlayer b = t == null ? null : ModContent.GetContent<ModPlayer>().FirstOrDefault(x => x.GetType() == t);
					if (b != null)
					{
						MethodInfo g = typeof(Player).GetMethods().First(mi => mi.Name == "GetModPlayer" && mi.IsGenericMethodDefinition && mi.GetParameters().Length == 1).MakeGenericMethod(t);
						acc = new ModPlayerAccess { Base = b, Get = g, Type = t };
					}
				}
			}
			catch (Exception) { acc = null; }
			_mp[key] = acc;
			return acc;
		}

		private const string CalMod = "CalamityMod", CalPlayerType = "CalamityMod.CalPlayer.CalamityPlayer";
		private const string ThMod = "ThoriumMod", ThPlayerType = "ThoriumMod.ThoriumPlayer";

		private static object PlayerOf(Player p, string mod, string type)
		{
			ModPlayerAccess a = Access(mod, type);
			if (a == null || p == null) return null;
			try { return a.Get.Invoke(p, new object[] { a.Base }); } catch (Exception) { return null; }
		}

		internal static object CalamityPlayerOf(Player p) => PlayerOf(p, CalMod, CalPlayerType);

		private static FieldInfo _fMouseRight, _fDelta, _fRot;
		private static bool _calFieldsTried;
		private static void CalFields()
		{
			if (_calFieldsTried) return;
			_calFieldsTried = true;
			Type t = Access(CalMod, CalPlayerType)?.Type;
			if (t == null) return;
			const BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			_fMouseRight = t.GetField("mouseRight", f); _fDelta = t.GetField("mouseWorldDeltaFromPlayer", f); _fRot = t.GetField("mouseRotationFromPlayer", f);
		}

		/// <summary>Sets <c>CalamityPlayer.mouseRight</c> on a dummy. False when Calamity is not loaded.</summary>
		public static bool SetMouseRight(Player p, bool v)
		{
			object cp = CalamityPlayerOf(p); CalFields();
			if (cp == null || _fMouseRight == null) return false;
			try { _fMouseRight.SetValue(cp, v); return true; } catch (Exception) { return false; }
		}

		/// <summary>Reads <c>CalamityPlayer.mouseRight</c> (null when unavailable).</summary>
		public static bool? GetMouseRight(Player p)
		{
			object cp = CalamityPlayerOf(p); CalFields();
			if (cp == null || _fMouseRight == null) return null;
			try { return (bool)_fMouseRight.GetValue(cp); } catch (Exception) { return null; }
		}

		internal static (Vector2, float) GetCalAimPublic(object cp) => GetCalAim(cp);
		internal static void SetCalAimPublic(object cp, Vector2 delta, float? rot = null) { if (rot.HasValue) SetCalAim(cp, delta, rot.Value); else SetCalAim(cp, delta); }

		private static (Vector2, float) GetCalAim(object cp)
		{
			CalFields();
			try { return (_fDelta != null ? (Vector2)_fDelta.GetValue(cp) : default, _fRot != null ? (float)_fRot.GetValue(cp) : 0f); } catch (Exception) { return (default, 0f); }
		}

		private static void SetCalAim(object cp, Vector2 delta) => SetCalAim(cp, delta, delta.ToRotation());
		private static void SetCalAim(object cp, Vector2 delta, float rot)
		{
			CalFields();
			try { _fDelta?.SetValue(cp, delta); _fRot?.SetValue(cp, rot); } catch (Exception) { }
		}

		/// <summary>The Calamity-reported cursor offset of a dummy (self checks).</summary>
		public static Vector2? CalamityAimDelta(Player p)
		{
			object cp = CalamityPlayerOf(p); CalFields();
			if (cp == null || _fDelta == null) return null;
			try { return (Vector2)_fDelta.GetValue(cp); } catch (Exception) { return null; }
		}

		/// <summary>Reads a meter by field or property name from the dummy's Calamity player, then its Thorium player. NaN when no such member exists.</summary>
		public static float ReadMeter(Player p, string name)
		{
			if (p == null || string.IsNullOrEmpty(name)) return float.NaN;
			foreach (var (mod, type) in new[] { (CalMod, CalPlayerType), (ThMod, ThPlayerType) })
			{
				object o = PlayerOf(p, mod, type);
				if (o == null) continue;
				string key = type + "." + name;
				if (!_meters.TryGetValue(key, out MemberInfo mi))
				{
					const BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
					mi = (MemberInfo)o.GetType().GetField(name, f) ?? o.GetType().GetProperty(name, f);
					_meters[key] = mi;
				}
				if (mi == null) continue;
				try
				{
					object v = mi is FieldInfo fi ? fi.GetValue(o) : ((PropertyInfo)mi).GetValue(o);
					switch (v)
					{
						case bool b: return b ? 1f : 0f;
						case int i: return i;
						case float f2: return f2;
						case double dd: return (float)dd;
						case short sh: return sh;
						case byte by: return by;
						case long l: return l;
					}
				}
				catch (Exception) { }
			}
			return float.NaN;
		}

		// ---------------------------------------------------------------------------------------------
		// Self check (boot)
		// ---------------------------------------------------------------------------------------------
		/// <summary>Result lines of the registry self check: parse/Want for every pattern on synthetic contexts. Empty failures = pass.</summary>
		public static List<string> SelfCheck()
		{
			var fails = new List<string>();
			void Check(bool cond, string what) { if (!cond) fails.Add(what); }

			// parsing
			AltParams p = AltParams.Parse("minDist=5;maxDist=30;tough=1;minEnemies=3;radius=6;proj=Foo;maxOut=2;cond=heavy;min=full;ownHpBelow=0.9;p=0.25");
			Check(p.MinDist == 5f && p.MaxDist == 30f && p.Tough && p.MinEnemies == 3 && p.Radius == 6f && p.Proj == "Foo" && p.MaxOut == 2 && p.Cond == "heavy" && p.MinFull && Math.Abs(p.OwnHpBelow - 0.9f) < 1e-4 && Math.Abs(p.P - 0.25f) < 1e-4, "AltParams.Parse reads every key");
			Check(AltParams.Parse("").MinEnemies == 2 && AltParams.Parse(null).P == 0.4f, "AltParams defaults");
			var rows = ParseTable("mod\titem\tpolicy\tparams\tnote\nCalamityMod\tFoo\tALT_AOE\tminEnemies=4\thi\n#c\nCalamityMod\tBar\tBOGUS\t\t\nThoriumMod\tDarkGate\tPORTAL_USE\t\t\n");
			Check(rows.Count == 2 && rows[0].policy.Kind == AltPolicyKind.Aoe && rows[0].policy.P.MinEnemies == 4 && rows[1].policy.Kind == AltPolicyKind.PortalUse, "ParseTable skips unknown policies and comments");

			// registry resolution against the embedded table
			Check(TableRows > 100, $"embedded table loaded ({TableRows} rows)");
			if (ModLoader.HasMod("CalamityMod"))
			{
				Check(ModContent.TryFind("CalamityMod", "Hydra", out ModItem hy) && For(hy.Type).Kind == AltPolicyKind.Detonate, "Calamity Hydra resolves to DETONATE");
				Check(ModContent.TryFind("CalamityMod", "CrackshotColt", out ModItem cc) && For(cc.Type).Kind == AltPolicyKind.Never, "coin weapon Crackshot Colt is NEVER");
			}
			else Check(SkippedRows >= 50, "rows of a missing mod are skipped");
			Check(For(ItemID.Handgun).Kind == AltPolicyKind.Mix && Math.Abs(For(ItemID.Handgun).P.P - 0.4f) < 1e-4, "unlisted item defaults to ALT_MIX p=0.4");
			Check(Register("NoSuchMod", "X", new AltPolicy()) == false, "Register on an absent mod returns false");

			// Want per pattern, on fixed contexts (no world needed)
			bool W(string pol, string prm, Action<AltCtx> set)
			{
				var pl = new AltPolicy { Kind = Names[pol], P = AltParams.Parse(prm) };
				var x = new AltCtx(null, null); set(x);
				return EvaluateCtx(null, new Item(), pl, x);
			}
			var never = new AltPolicy { Kind = AltPolicyKind.Never };
			Check(!Evaluate(null, new Item(), never, null, out int rc) && rc == NeverRecheckTicks, "NEVER: false and long recheck");
			Check(Evaluate(null, new Item(), new AltPolicy { Kind = AltPolicyKind.Always }, null, out _), "ALWAYS: true");
			int hits = 0; var mix = new AltPolicy { Kind = AltPolicyKind.Mix, P = AltParams.Parse("p=0.5") };
			for (int i = 0; i < 400; i++) if (Evaluate(null, new Item(), mix, null, out _)) hits++;
			Check(hits > 140 && hits < 260, $"MIX p=0.5 rolls about half ({hits}/400)");
			Check(W("ALT_HEAVY", "tough=1;minDist=8;maxDist=40", x => { x.TDist = 20; x.TTough = true; }), "HEAVY: tough target in the band");
			Check(!W("ALT_HEAVY", "tough=1;minDist=8;maxDist=40", x => { x.TDist = 20; x.TTough = false; }), "HEAVY: not against trash");
			Check(!W("ALT_HEAVY", "tough=1;minDist=8;maxDist=40", x => { x.TDist = 3; x.TTough = true; }), "HEAVY: not too close");
			Check(W("ALT_AOE", "minEnemies=3;radius=8;minDist=4", x => { x.TDist = 10; x.TCrowd = 4; }), "AOE: a crowd");
			Check(!W("ALT_AOE", "minEnemies=3;radius=8;minDist=4", x => { x.TDist = 10; x.TCrowd = 1; }), "AOE: not a lone target");
			Check(W("ALT_RATE", "maxDist=20", x => x.TDist = 10) && !W("ALT_RATE", "maxDist=20", x => x.TDist = 30), "RATE: inside the range only");
			Check(W("ALT_FAR", "minDist=6", x => x.TDist = 9) && !W("ALT_FAR", "minDist=6", x => x.TDist = 2), "FAR: only when out of reach");
			var st = new AltState { PrimarySinceAlt = 12 };
			Check(W("TOGGLE_N", "afterShots=12", x => x.TState = st) && !W("TOGGLE_N", "afterShots=12", x => x.TState = new AltState { PrimarySinceAlt = 3 }), "TOGGLE_N: every N primaries");
			Check(W("DETONATE", "minOut=3;minDist=5", x => { x.TOut = 4; x.TDist = 9; }) && !W("DETONATE", "minOut=3;minDist=5", x => { x.TOut = 1; x.TDist = 9; }), "DETONATE: only with enough out");
			Check(!W("DETONATE", "minOut=2;ownHpBelow=0.9", x => { x.TOut = 3; x.TDist = 9; x.TSelfHp = 1f; }) && W("DETONATE", "minOut=2;ownHpBelow=0.9", x => { x.TOut = 3; x.TDist = 9; x.TSelfHp = 0.5f; }), "DETONATE: heal-on-return only when hurt");
			Check(W("PLACE", "maxOut=1;maxDist=25;tough=1", x => { x.TOut = 0; x.TDist = 10; x.TTough = true; }) && !W("PLACE", "maxOut=1;maxDist=25;tough=1", x => { x.TOut = 1; x.TDist = 10; x.TTough = true; }), "PLACE: one at a time");
			Check(W("GUARD", "near=8;hpBelow=0.6;enemies=3", x => { x.TSelfHp = 0.4f; x.TNear = 4; }) && !W("GUARD", "near=8;hpBelow=0.6;enemies=3", x => { x.TSelfHp = 1f; x.TNear = 0; }), "GUARD: pressure only");
			var m = new Dictionary<string, float> { ["fuel"] = 5f };
			Check(W("METER_READY", "meter=fuel;min=1", x => x.TMeter = m) && !W("METER_READY", "meter=fuel;min=9", x => x.TMeter = m), "METER_READY: meter at or over the minimum");
			Check(W("METER_READY", "meter=fuel;min=1;tough=1", x => { x.TMeter = m; x.TTough = true; }) && !W("METER_READY", "meter=fuel;min=1;tough=1", x => { x.TMeter = m; x.TTough = false; x.TCrowd = 1; }), "METER_READY: saved for tough targets");
			Check(W("HOLD_VARIANT", "cond=heavy;minDist=6", x => { x.TTough = true; x.TDist = 10; }) && W("HOLD_VARIANT", "cond=aoe;minEnemies=2", x => x.TCrowd = 3) && !W("HOLD_VARIANT", "cond=aoe;minEnemies=2", x => x.TCrowd = 1), "HOLD_VARIANT: condition picks the variant");
			Check(W("MANA_REFILL", "manaBelow=0.3", x => { x.TMana = 0.1f; x.TDist = 3; }) && !W("MANA_REFILL", "manaBelow=0.3", x => { x.TMana = 0.9f; x.TDist = 3; }), "MANA_REFILL: only when the pool is low");
			Check(W("STACK_UP", "maxStacks=3;tough=1", x => { x.TState = new AltState { AltStacks = 1 }; x.TTough = true; x.TDist = 5; }) && !W("STACK_UP", "maxStacks=3;tough=1", x => { x.TState = new AltState { AltStacks = 3 }; x.TTough = true; x.TDist = 5; }), "STACK_UP: up to the stack cap");
			Check(W("ALLY_HEAL", "minWounded=2;hpBelow=0.7", x => x.TWounded = 2) && !W("ALLY_HEAL", "minWounded=2;hpBelow=0.7", x => x.TWounded = 1), "ALLY_HEAL: enough wounded allies");
			Check(W("DASH_CLOSE", "minDist=10", x => x.TDist = 14) && !W("DASH_CLOSE", "minDist=10", x => x.TDist = 4), "DASH_CLOSE: closes a gap");
			Check(!Evaluate(null, new Item(), new AltPolicy { Kind = AltPolicyKind.PortalUse }, null, out _), "PORTAL_USE: not an item alt");
			Check(Register("NoSuchMod", "X", new AltPolicy()) == false, "Register on an absent mod returns false");
			return fails;
		}
	}

	/// <summary>
	/// CURSOR-STEERED PROJECTILES. Some shots follow the owner's cursor for their whole flight (Frigidflash Bolt's vortex, Face Melter's amplifier beam): they read the owner's cursor every frame, not only
	/// when fired. For a soldier holding an item whose row has <c>steer=1</c>, the cursor (Main.mouse and Calamity's per-player mouseWorld of the projectile's owner) is put on the soldier's target for the
	/// span of that projectile's AI and put back after. Does nothing unless a loaded row asks for it.
	/// </summary>
	public class SteerToTargetProjectile : GlobalProjectile
	{
		private static bool _on; private static Vector2 _screen; private static int _mx, _my; private static object _cal; private static Vector2 _delta; private static float _rot;

		public override bool PreAI(Projectile p)
		{
			_on = false;
			if (!p.friendly || (!AltPolicies.AnySteer && p.ModProjectile == null) || !p.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex < 0 || tag.SoldierIndex >= Main.maxNPCs) return true;
			if (Main.npc[tag.SoldierIndex].ModNPC is not SoldierNPC s || s.Record == null) return true;
			Item held = WeaponUse.HeldItem(s);
			bool rowSteer = held != null && !held.IsAir && AltPolicies.AnySteer && AltPolicies.For(held.type).P.Steer && AltPolicies.HasRow(held.type);
			// Any modded soldier shot whose own AI reads the owner's cursor (Calamity's FlareBolt, charge bolts, vortexes...) gets the cursor on the soldier's target too: without it the shot flies at the commander's last cursor spot.
			if (!rowSteer && !(p.ModProjectile != null && !ModShotKinds.Off && p.owner < Main.maxPlayers && OwnerLockProbe.ReadsCursor(p.type))) return true;
			int ti = s.TargetIndex;
			if (ti < 0 || ti >= Main.maxNPCs || !Main.npc[ti].active) return true;
			Vector2 at = Main.npc[ti].Center;
			_on = true;
			_screen = Main.screenPosition; _mx = Main.mouseX; _my = Main.mouseY;
			Main.screenPosition = Vector2.Zero; Main.mouseX = (int)at.X; Main.mouseY = (int)at.Y;
			_cal = p.owner >= 0 && p.owner < Main.maxPlayers ? AltPolicies.CalamityPlayerOf(Main.player[p.owner]) : null;
			if (_cal != null) { (_delta, _rot) = AltPolicies.GetCalAimPublic(_cal); AltPolicies.SetCalAimPublic(_cal, at - Main.player[p.owner].MountedCenter); }
			return true;
		}

		public override void PostAI(Projectile p)
		{
			if (!_on) return;
			_on = false;
			Main.screenPosition = _screen; Main.mouseX = _mx; Main.mouseY = _my;
			if (_cal != null) AltPolicies.SetCalAimPublic(_cal, _delta, _rot);
			_cal = null;
		}
	}

	/// <summary>Loads the policy table once every mod's content exists, and runs the boot self check.</summary>
	public class AltPoliciesSystem : ModSystem
	{
		public override void PostSetupContent()
		{
			AltPolicies.Load(Mod);
			try
			{
				var fails = AltPolicies.SelfCheck();
				if (fails.Count == 0) Mod.Logger.Info("Levy alt-fire policies self check: PASS");
				else foreach (string f in fails) Mod.Logger.Warn("Levy alt-fire policies self check FAIL: " + f);
			}
			catch (Exception e) { Mod.Logger.Warn("Levy alt-fire policies self check threw: " + e.Message); }
		}

		public override void Unload() => AltPolicies.Unload();
	}
}
