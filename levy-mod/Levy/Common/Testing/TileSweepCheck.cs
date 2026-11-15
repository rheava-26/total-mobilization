using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Weapons;
using Kind = Levy.Common.Weapons.WorldAlter.Kind;

namespace Levy.Common.Testing
{
	/// <summary>
	/// THE TILE-BREAKER SWEEP: every weapon, thrown explosive, tool and round of every loaded mod, fired through the game's own shot code into a block field (<see cref="TileSandbox"/>), and the field compared tile by tile afterwards.
	/// Logged as "[TileSweep]" lines when a world loads on a dedicated server with LEVY_TILESWEEP=1 (lab: the full sweep, writes the table the army reads, LEVY_TILESWEEP_OUT=path) or LEVY_SELFCHECK_WORLD=1 (ship: a time-boxed sample
	/// of the same sweep; the runtime classifier <see cref="WorldAlter"/> must flag everything the sample observed; pass=N fail=M).
	/// <para/>Pairs: a weapon that takes ammo is fired with EVERY round of its kind; anything else is fired once. Every projectile type is also spawned on its own, plain and flagged the way the army flags its shots.
	/// A weapon is world-altering by itself when every shot it made altered the field; a round is when its own projectile does, or when most weapons that deliver it alter the field; any other altering pair is a pair rule.
	/// Changes that only cut what vanilla lets any sword cut (grass blades, vines, cobweb) are counted apart (Soft) and never bench anything.
	/// </summary>
	public class TileSweepCheck : ModSystem
	{
		private static bool _armed, _done; private static int _calls, _pass, _fail; private static Mod _mod; private static bool _lab;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ) return;
			_lab = Environment.GetEnvironmentVariable("LEVY_TILESWEEP") == "1";
			bool ship = Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_TILESWEEPWORLD") != "0";
			if (!_lab && !ship) return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			if (_lab) Main.OnTickForThirdPartySoftwareOnly += Drive;
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }

		private static void Drive() { if (!_armed || _done || !Main.dedServ) return; if (++_calls < 6) return; RunIfArmed(); }

		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true;
			try { Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			_mod.Logger.Info($"[TileSweep] self-check pass={_pass} fail={_fail}");
			_mod.Logger.Info("[TileSweep] done");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[TileSweep] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[TileSweep] OBSERVED " + s);

		public static string KeyOf(int itemType) => WorldAlter.KeyOf(itemType);
		private static bool Hard(Kind k) => WorldAlter.Hard(k) != Kind.None;

		private sealed class Row { public int Weapon, Ammo; public Kind Kind; public SortedSet<int> Made; public string Error; public bool Altered; }

		private static void Run()
		{
			var sw = Stopwatch.StartNew();
			bool cal = TileSandbox.Calibrated(out string why);
			Check("the sandbox is calibrated (a bomb changes the field, a bullet does not): " + why, cal);
			if (!cal) return;
			int budgetSeconds = int.TryParse(Environment.GetEnvironmentVariable("LEVY_TILESWEEP_SECONDS"), out int bs) && bs > 0 ? bs : _lab ? int.MaxValue : 70; // ship: a time-boxed sample
			string only = Environment.GetEnvironmentVariable("LEVY_TILESWEEP_ONLY"); // lab: comma-separated item name fragments
			int pairBudget = int.TryParse(Environment.GetEnvironmentVariable("LEVY_TILESWEEP_MAXPAIRS"), out int mp) && mp > 0 ? mp : int.MaxValue;

			// ---- candidates: anything that fires something, and every round
			var weapons = new List<int>(); var ammoByKind = new Dictionary<int, List<int>>();
			for (int i = 1; i < ItemLoader.ItemCount; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item s) || s == null || s.IsAir || s.IsACoin) continue;
				if (s.ammo > 0) { // notAmmo is no reason to skip: Sand blocks are rounds of the Sandgun
				 if (!ammoByKind.TryGetValue(s.ammo, out var l)) ammoByKind[s.ammo] = l = new List<int>(); l.Add(i); }
				if (s.shoot > ProjectileID.None && IsHook(s.shoot)) continue; // a grappling hook needs a real player (and throws without one) and alters nothing
				if (s.shoot > ProjectileID.None || s.useAmmo > 0) weapons.Add(i);
			}
			if (!_lab) weapons = weapons.Where(w => w < ItemID.Count).ToList(); // the ship sample is vanilla only: modded projectiles throw on a stand-in player (and tML logs every throw), the lab sweep covers them
			if (!string.IsNullOrEmpty(only)) weapons = weapons.Where(w => only.Split(',').Any(n => Lang.GetItemNameValue(w).Contains(n, StringComparison.OrdinalIgnoreCase) || KeyOf(w).Contains(n, StringComparison.OrdinalIgnoreCase))).ToList();
			if (!_lab) { var rnd = new Random(20261114); for (int i = weapons.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (weapons[i], weapons[j]) = (weapons[j], weapons[i]); } } // a time cut then leaves an even sample of every mod
			Note($"candidates: {weapons.Count} items that fire something, {ammoByKind.Values.Sum(l => l.Count)} rounds in {ammoByKind.Count} kinds, {ProjectileLoader.ProjectileCount - 1} projectile types ({ProjectileID.Count - 1} vanilla)");

			// lab: LEVY_TILESWEEP_SHOOT="Bomb,RocketLauncher+RocketII" prints the detail of those shots
			foreach (string spec in (Environment.GetEnvironmentVariable("LEVY_TILESWEEP_SHOOT") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
			{
				string[] ab = spec.Split('+'); int wi = WorldAlter.ItemFromKey(ab[0]), ai = ab.Length > 1 ? WorldAlter.ItemFromKey(ab[1]) : 0;
				var r = TileSandbox.Shoot(wi, ai);
				Note($"shot {spec}: {TileSandbox.KindText(r.Kind)} ({r.Changed} tiles, {r.Ticks} ticks, error {r.Error ?? "none"}, made {string.Join(",", r.Made.Select(ProjName))}, tile types {string.Join(",", r.Types.Select(TileTypeName))})");
			}

			// ---- every projectile type on its own: spawned plain and army-flagged above the field
			var pAlter = new Dictionary<int, Kind>(); var pSafe = new List<int>(); var pThrew = new List<int>(); int projTested = 0, softProj = 0;
			var debugProj = (Environment.GetEnvironmentVariable("LEVY_TILESWEEP_PROJ") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
			var swp = Stopwatch.StartNew(); int scanHit = 0, scanMiss = 0, scanOver = 0, scanOk = 0; var scanMissList = new List<string>();
			var skipProj = new HashSet<string>(WorldAlter.TableKeys('Q'));
			for (int t = 1; t < ProjectileLoader.ProjectileCount; t++)
			{
				if (t >= ProjectileID.Count && ProjectileLoader.GetProjectile(t) == null) continue;
				if (IsHook(t)) continue;
				if (!_lab && t >= ProjectileID.Count) continue;
				if (!_lab && skipProj.Contains(WorldAlter.ProjectileKeyOf(t))) continue; // threw in the lab sweep: not run again in a boot that must stay free of exceptions
				if (!_lab && sw.Elapsed.TotalSeconds > budgetSeconds * 0.4) break;
				projTested++;
				var plain = TileSandbox.Spawn(t, false, 300); var army = TileSandbox.Spawn(t, true, 300);
				Kind k = plain.Kind | army.Kind; bool threw = plain.Error != null || army.Error != null;
				if (_lab && !threw) foreach (int col in TileSandbox.Zones.Skip(1)) { if (Hard(k)) break; var more = TileSandbox.Spawn(t, false, 300, col); k |= more.Kind; if (more.Error != null) threw = true; } // lab: also over the Corruption, Crimson, Hallow and jungle patches
				if (threw) pThrew.Add(t);
				if (debugProj.Length > 0 && debugProj.Any(d => ProjName(t).Equals(d, StringComparison.OrdinalIgnoreCase))) Note($"projectile {ProjName(t)}: plain {TileSandbox.KindText(plain.Kind)} ({plain.Changed} tiles, {plain.Ticks} ticks, error {plain.Error ?? "none"}, made {string.Join(",", plain.Made.Select(ProjName))}, tile types {string.Join(",", plain.Types.Select(TileTypeName))}); army-flagged {TileSandbox.KindText(army.Kind)} ({army.Changed} tiles)");
				if (Hard(k)) pAlter[t] = k; else { pSafe.Add(t); if (k != Kind.None) softProj++; }
				if (t >= ProjectileID.Count)
				{
					Kind scan = WorldAlter.ScanProjectileOnly(t);
					if (Hard(k) && scan != Kind.None) scanHit++; else if (Hard(k)) { scanMiss++; if (scanMissList.Count < 25) scanMissList.Add(WorldAlter.ProjectileKeyOf(t) + " [" + TileSandbox.KindText(k) + "]"); } else if (scan != Kind.None) scanOver++; else scanOk++;
				}
			}
			Note($"{projTested} projectile types spawned over the field in {swp.Elapsed.TotalSeconds:F1}s: {pAlter.Count} alter it ({pAlter.Count(kv => kv.Key < ProjectileID.Count)} vanilla, {pAlter.Count(kv => kv.Key >= ProjectileID.Count)} modded); {softProj} only cut vanilla's cuttable decor (grass, vines, cobweb) and are kept; {pThrew.Count} threw");
			Note("vanilla projectiles that alter it: " + string.Join(", ", pAlter.Where(kv => kv.Key < ProjectileID.Count).Select(kv => ProjName(kv.Key) + "[" + TileSandbox.KindText(kv.Value) + "]")));
			foreach (var g in pAlter.Where(kv => kv.Key >= ProjectileID.Count).GroupBy(kv => ProjectileLoader.GetProjectile(kv.Key).Mod.Name)) Note($"  modded projectiles that alter it, {g.Key}: {g.Count()} (e.g. {string.Join(", ", g.Take(12).Select(kv => ProjName(kv.Key)))})");
			Note($"compiled-code scan vs measurement, modded projectiles: caught {scanHit} of {scanHit + scanMiss} that alter the field (recall {(scanHit + scanMiss == 0 ? 100.0 : 100.0 * scanHit / (scanHit + scanMiss)):F0}%), flagged {scanOver} that did not, agreed on {scanOk} safe ones");
			foreach (var m in scanMissList) Note("scan MISSED: " + m);

			// ---- the pairs
			var skipWeapon = new HashSet<string>(WorldAlter.TableKeys('E'));
			var rows = new List<Row>(); int pairs = 0; var perWeapon = new Dictionary<int, List<Row>>(); int skipped = 0;
			foreach (int w in weapons)
			{
				if (pairs >= pairBudget || sw.Elapsed.TotalSeconds > budgetSeconds) { skipped++; continue; }
				if (!_lab && skipWeapon.Contains(KeyOf(w))) { skipped++; continue; }
				Item ws = ContentSamples.ItemsByType[w];
				var rounds = new List<int>();
				if (ws.useAmmo > 0) { if (ammoByKind.TryGetValue(ws.useAmmo, out var l)) rounds.AddRange(l); } else rounds.Add(0);
				if (!_lab) rounds = rounds.Where(r => r < ItemID.Count).ToList();
				if (!_lab && rounds.Count > 6) // a sample: the first and last rounds and every round the table flags (the check below must agree on each)
				{
					var flagged = rounds.Where(r => r > 0 && WorldAlter.Alters(ContentSamples.ItemsByType[r])).ToList();
					rounds = new[] { rounds[0], rounds[rounds.Count - 1] }.Concat(flagged).Distinct().Take(12).ToList();
				}
				var wl = new List<Row>(); perWeapon[w] = wl;
				foreach (int r in rounds)
				{
					pairs++;
					var res = TileSandbox.Shoot(w, r);
					var row = new Row { Weapon = w, Ammo = r, Kind = res.Kind, Made = res.Made, Error = res.Error };
					row.Altered = Hard(res.Kind) || res.Made.Any(pAlter.ContainsKey); // the field says so, or a projectile it made is one the sweep saw alter the field somewhere (a falling-sand shot may land on sand and change nothing here)
					rows.Add(row); wl.Add(row);
				}
			}
			Note($"sandbox integrity: the pristine copy was rebuilt {TileSandbox.Repairs} times because a shot reached it");
			Note($"{pairs} (weapon, round) pairs fired in {sw.Elapsed.TotalSeconds:F1}s total ({TileSandbox.Ms / Math.Max(1, TileSandbox.Runs):F2} ms per test){(skipped > 0 ? "; " + skipped + " items not reached or skipped (time box / threw in the lab sweep)" : "")}");

			// ---- weapon level: every shot it made altered the field. round level: its own projectile does, or most weapons that deliver it alter. Anything else altering is a pair rule.
			var wBreak = new Dictionary<int, Kind>(); var aBreak = new Dictionary<int, Kind>(); var xBreak = new List<Row>(); var eWeapon = new List<int>();
			foreach (var kv in perWeapon)
			{
				var good = kv.Value.Where(r => r.Error == null).ToList();
				if (kv.Value.Count > 0 && good.Count == 0) { eWeapon.Add(kv.Key); continue; } // every shot threw: nothing measured (decided below from its compiled code)
				if (good.Count > 0 && good.All(r => r.Altered)) { Kind k = Kind.None; foreach (var r in good) k |= r.Kind; wBreak[kv.Key] = k; }
			}
			// A round is altering when it is what makes the difference: among the weapons that fire it AND another round of its kind to different effect, most alter the field with this round and not with the others.
			// (Launchers swap the projectile by round, bullets and arrows mostly do not; a weapon that alters or stays safe whatever the round says nothing about the round.)
			var byWeapon = rows.Where(r => r.Error == null && r.Ammo > 0 && !wBreak.ContainsKey(r.Weapon)).GroupBy(r => r.Weapon).ToDictionary(g => g.Key, g => g.ToList());
			foreach (var kv in ammoByKind)
				foreach (int a in kv.Value)
				{
					Item sample = ContentSamples.ItemsByType[a];
					Kind own = sample.shoot > 0 && pAlter.TryGetValue(sample.shoot, out Kind pk) ? pk : Kind.None;
					int discriminating = 0, altered = 0; Kind seenKind = own;
					foreach (var wl in byWeapon.Values)
					{
						var mine = wl.FirstOrDefault(r => r.Ammo == a); if (mine == null) continue;
						bool others = wl.Any(r => r.Ammo != a && r.Altered != mine.Altered);
						if (!others && wl.Any(r => r.Ammo != a)) continue; // the same outcome for every round: not a statement about this one
						discriminating++; if (mine.Altered) { altered++; seenKind |= mine.Kind; }
					}
					if (own != Kind.None || (discriminating > 0 && altered * 2 >= discriminating)) aBreak[a] = seenKind;
				}
			foreach (var r in rows) if (r.Error == null && r.Altered && !wBreak.ContainsKey(r.Weapon) && !(r.Ammo > 0 && aBreak.ContainsKey(r.Ammo))) xBreak.Add(r);
			foreach (int w in eWeapon) { Kind scan = WorldAlter.ScanOnly(ContentSamples.ItemsByType[w]); if (scan != Kind.None) wBreak[w] = scan; }
			Note($"world-altering: {wBreak.Count} items by themselves, {aBreak.Count} rounds, {xBreak.Count} single (weapon, round) pairs; {eWeapon.Count} items threw on every shot (decided from their compiled code: {eWeapon.Count(w => wBreak.ContainsKey(w))} altering)");

			// ---- by mod
			string ModOf(int t) => ContentSamples.ItemsByType[t].ModItem?.Mod.Name ?? "Terraria";
			foreach (var g in wBreak.Keys.Concat(aBreak.Keys).GroupBy(ModOf)) Note($"  {g.Key}: {g.Count(k => wBreak.ContainsKey(k))} items, {g.Count(k => aBreak.ContainsKey(k))} rounds");
			Note("by-itself, vanilla: " + string.Join(", ", wBreak.Where(kv => kv.Key < ItemID.Count).Select(kv => Lang.GetItemNameValue(kv.Key) + "[" + TileSandbox.KindText(kv.Value) + "]").Take(120)));
			Note("rounds, vanilla: " + string.Join(", ", aBreak.Where(kv => kv.Key < ItemID.Count).Select(kv => Lang.GetItemNameValue(kv.Key) + "[" + TileSandbox.KindText(kv.Value) + "]")));
			Note("by-itself, modded: " + string.Join(", ", wBreak.Where(kv => kv.Key >= ItemID.Count).Select(kv => KeyOf(kv.Key) + "[" + TileSandbox.KindText(kv.Value) + "]").Take(150)));
			Note("rounds, modded: " + string.Join(", ", aBreak.Where(kv => kv.Key >= ItemID.Count).Select(kv => KeyOf(kv.Key) + "[" + TileSandbox.KindText(kv.Value) + "]")));
			Note("pair-only (first 80): " + string.Join(", ", xBreak.Take(80).Select(r => KeyOf(r.Weapon) + "+" + (r.Ammo > 0 ? KeyOf(r.Ammo) : "-"))));
			foreach (Kind k in new[] { Kind.Tiles, Kind.Walls, Kind.Liquid, Kind.Convert, Kind.Place, Kind.Paint, Kind.Other })
			{
				var ks = wBreak.Where(kv => kv.Value.HasFlag(k)).Select(kv => KeyOf(kv.Key)).Concat(aBreak.Where(kv => kv.Value.HasFlag(k)).Select(kv => KeyOf(kv.Key))).ToList();
				Note($"kind {k}: {ks.Count} items/rounds" + (ks.Count > 0 ? ": " + string.Join(", ", ks.Take(60)) : ""));
			}
			int errs = rows.Count(r => r.Error != null); Note($"{errs} of {rows.Count} pairs threw while firing (nothing to measure)");
			foreach (var g in rows.Where(r => r.Error != null).GroupBy(r => r.Error).OrderByDescending(g => g.Count()).Take(8)) Note($"  error x{g.Count()}: {g.Key} (e.g. {KeyOf(g.First().Weapon)})");

			// ---- vanilla's own flags
			var flags = new List<string>();
			foreach (var (typ, label) in new[] { (typeof(ItemID.Sets), "ItemID.Sets"), (typeof(ProjectileID.Sets), "ProjectileID.Sets") })
				foreach (var f in typ.GetFields(BindingFlags.Public | BindingFlags.Static))
				{
					string n = f.Name; if (!new[] { "Drill", "Bomb", "Explosive", "Chainsaw", "Tile", "Pick", "Rocket", "Dynamite", "Liquid", "Mining" }.Any(x => n.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
					int count = -1; if (f.GetValue(null) is bool[] arr) count = arr.Count(b => b);
					flags.Add($"{label}.{n}{(count >= 0 ? "(" + count + " set)" : "")}");
				}
			Note("vanilla sets that name tile or blast behaviour: " + string.Join(", ", flags));

			// ---- the runtime classifier against the measurement
			int sMiss = 0, sOver = 0, sOk = 0; var scanMisses = new List<string>();
			int miss = 0, over = 0, ok = 0; var missList = new List<string>();
			var scanMemo = new Dictionary<int, Kind>(); Kind Scan(Item it) { if (it == null) return Kind.None; if (!scanMemo.TryGetValue(it.type, out Kind k)) scanMemo[it.type] = k = WorldAlter.ScanOnly(it); return k; }
			foreach (var r in rows)
			{
				if (r.Error != null) continue;
				bool observed = r.Altered;
				Item ws = ContentSamples.ItemsByType[r.Weapon]; Item am = r.Ammo > 0 ? ContentSamples.ItemsByType[r.Ammo] : null;
				bool scanned = WorldAlter.IsTool(ws) || Scan(ws) != Kind.None || (am != null && Scan(am) != Kind.None);
				if (observed && !scanned) { sMiss++; if (scanMisses.Count < 30) scanMisses.Add(KeyOf(r.Weapon) + "+" + (r.Ammo > 0 ? KeyOf(r.Ammo) : "-") + " [" + TileSandbox.KindText(r.Kind) + "] made " + string.Join("|", r.Made.Select(ProjName))); } else if (!observed && scanned) sOver++; else sOk++;
				if (WorldAlter.Disabled) continue;
				bool flagged = WorldAlter.AltersWith(ws, am);
				if (observed && !flagged) { miss++; if (missList.Count < 40) missList.Add(KeyOf(r.Weapon) + "+" + (r.Ammo > 0 ? KeyOf(r.Ammo) : "-") + " [" + TileSandbox.KindText(r.Kind) + "] made " + string.Join("|", r.Made.Select(ProjName))); }
				else if (!observed && flagged) over++;
				else ok++;
			}
			int altPairs = rows.Count(r => r.Error == null && r.Altered);
			Note($"compiled-code scan alone (no table), over the {rows.Count} pairs: caught {altPairs - sMiss} of {altPairs} pairs that altered the field, flagged {sOver} that did not, agreed {sOk}");
			foreach (var m in scanMisses) Note("scan alone MISSED: " + m);
			foreach (var m in missList) Note("MISSED by the classifier: " + m);
			if (WorldAlter.Disabled) Note("LEVY_WORLDALTER=0: the classifier is off, its check is skipped");
			else Check($"the army's classifier flags every shot that altered the field: {rows.Count - errs} measured pairs, missed {miss}, flagged without a measured change {over}, agreeing {ok}", miss == 0);
			if (!WorldAlter.Disabled)
			{
				int pMiss = pAlter.Keys.Count(t => !WorldAlter.AltersProjectile(t));
				Check($"the classifier knows every projectile type that altered the field: {pAlter.Count} measured, missed {pMiss}", pMiss == 0);
			}

			string outp = Environment.GetEnvironmentVariable("LEVY_TILESWEEP_OUT");
			if (!string.IsNullOrEmpty(outp) && _lab && string.IsNullOrEmpty(only) && pairBudget == int.MaxValue)
			{
				var lines = new List<string>();
				foreach (var kv in wBreak.OrderBy(k => KeyOf(k.Key), StringComparer.Ordinal)) lines.Add($"W\t{KeyOf(kv.Key)}\t{TileSandbox.KindText(WorldAlter.Hard(kv.Value))}");
				foreach (var kv in aBreak.OrderBy(k => KeyOf(k.Key), StringComparer.Ordinal)) lines.Add($"A\t{KeyOf(kv.Key)}\t{TileSandbox.KindText(WorldAlter.Hard(kv.Value))}");
				foreach (var r in xBreak.OrderBy(r => KeyOf(r.Weapon), StringComparer.Ordinal).ThenBy(r => KeyOf(r.Ammo), StringComparer.Ordinal)) lines.Add($"X\t{KeyOf(r.Weapon)}\t{(r.Ammo > 0 ? KeyOf(r.Ammo) : "-")}\t{TileSandbox.KindText(WorldAlter.Hard(r.Kind))}");
				foreach (int w in weapons.OrderBy(KeyOf, StringComparer.Ordinal)) if (!wBreak.ContainsKey(w) && !aBreak.ContainsKey(w)) lines.Add($"S\t{KeyOf(w)}");
				foreach (var l in ammoByKind.Values) foreach (int a in l.OrderBy(KeyOf, StringComparer.Ordinal)) if (!aBreak.ContainsKey(a) && !wBreak.ContainsKey(a)) lines.Add($"S\t{KeyOf(a)}");
				foreach (int w in perWeapon.Where(kv => kv.Value.Any(r => r.Error != null)).Select(kv => kv.Key).OrderBy(KeyOf, StringComparer.Ordinal)) lines.Add($"E\t{KeyOf(w)}"); // any shot that threw: skipped by the ship sample, which must stay free of exceptions
				foreach (var kv in pAlter.OrderBy(k => WorldAlter.ProjectileKeyOf(k.Key), StringComparer.Ordinal)) lines.Add($"P\t{WorldAlter.ProjectileKeyOf(kv.Key)}\t{TileSandbox.KindText(WorldAlter.Hard(kv.Value))}");
				foreach (int t in pSafe.Where(x => x >= ProjectileID.Count).OrderBy(x => WorldAlter.ProjectileKeyOf(x), StringComparer.Ordinal)) lines.Add($"R\t{WorldAlter.ProjectileKeyOf(t)}");
				foreach (int t in pThrew.OrderBy(x => WorldAlter.ProjectileKeyOf(x), StringComparer.Ordinal)) lines.Add($"Q\t{WorldAlter.ProjectileKeyOf(t)}");
				var distinct = lines.Distinct().ToList();
				try { File.WriteAllLines(outp, distinct); Note($"table written: {distinct.Count} lines -> {outp}"); } catch (Exception e) { Note("could not write the table: " + e.Message); }
				var detail = new List<string> { "weapon\tround\tkind\tproducedProjectiles\terror" };
				foreach (var r in rows) detail.Add($"{KeyOf(r.Weapon)}\t{(r.Ammo > 0 ? KeyOf(r.Ammo) : "-")}\t{TileSandbox.KindText(r.Kind)}\t{string.Join("|", r.Made.Select(ProjName))}\t{r.Error}");
				try { File.WriteAllLines(outp + ".pairs.tsv", detail); } catch { }
			}

			// ---- the Demolitionist and the thrown shots of every officer, then everything the army shot in this boot
			DemolitionistScenario();
			ArmyWatch();
		}

		private static bool IsHook(int t) { try { return ContentSamples.ProjectilesByType.TryGetValue(t, out var pr) && pr != null && pr.aiStyle == ProjAIStyleID.Hook; } catch (Exception) { return false; } }

		private static string TileTypeName(int t) => t < TileID.Count ? TileID.Search.GetName(t) : (TileLoader.GetTile(t)?.Name ?? "#" + t);

		private static string ProjName(int t)
		{
			if (t < ProjectileID.Count) return ProjectileID.Search.GetName(t);
			ModProjectile mp = ProjectileLoader.GetProjectile(t); return mp == null ? "#" + t : mp.Mod.Name + "/" + mp.Name;
		}

		/// <summary>Every projectile type the army made during this boot's world checks (levies, officers, drones, turrets), measured in the field one by one.</summary>
		private static void ArmyWatch()
		{
			var seen = AlterWatch.Seen.OrderByDescending(kv => kv.Value).ToList();
			var bad = new List<string>();
			foreach (var kv in seen)
			{
				// measured in the field only where a stand-in player cannot make the shot throw (and tML log it): Levy's own and Mechanization's, and the stable ones: other types are judged by the table
				string mod = kv.Key >= ProjectileID.Count ? ProjectileLoader.GetProjectile(kv.Key)?.Mod.Name : null;
				bool own = mod == "Levy" || mod == "Mechanization";
				var m = own ? TileSandbox.Spawn(kv.Key, true) : new TileSandbox.Result();
				if (Hard(m.Kind) || WorldAlter.AltersProjectile(kv.Key)) bad.Add(ProjName(kv.Key) + " x" + kv.Value + " [" + TileSandbox.KindText(m.Kind) + "]");
			}
			Note($"projectile types the army made during the world checks: {seen.Count} ({string.Join(", ", seen.Take(25).Select(kv => ProjName(kv.Key) + " x" + kv.Value))}{(seen.Count > 25 ? ", ..." : "")})");
			Check($"nothing the army shot during the world checks alters the field: {seen.Count} projectile types, altering: {(bad.Count == 0 ? "none" : string.Join(", ", bad))}", bad.Count == 0);
		}

		/// <summary>
		/// What the Demolitionist officer throws. His lobbed weapon and the sappers' grenade are vanilla's Grenade; his periodic heavy charge was vanilla's Dynamite (measured here as the control: it breaks tiles and walls) and is now
		/// <see cref="global::Levy.Content.Projectiles.SapperCharge"/>. The real kit is run in the field: a Demolitionist, a hostile in reach, 400 ticks of his ability (the charge is thrown every 360), every shot stepped to its end, the field compared.
		/// </summary>
		private static void DemolitionistScenario()
		{
			int sapper = ModContent.ProjectileType<global::Levy.Content.Projectiles.SapperCharge>();
			foreach (int type in new[] { ProjectileID.Grenade, ProjectileID.Dynamite, sapper })
			{
				var army = TileSandbox.Spawn(type, true); var plain = TileSandbox.Spawn(type, false);
				Note($"Demolitionist projectile {ProjName(type)}: army-flagged -> {TileSandbox.KindText(army.Kind)} ({army.Changed} tiles); plain -> {TileSandbox.KindText(plain.Kind)} ({plain.Changed} tiles); made {string.Join(",", army.Made.Select(ProjName))}");
			}
			Check("vanilla's Dynamite breaks tiles and walls in the field (the control: what he threw before)", Hard(TileSandbox.Spawn(ProjectileID.Dynamite, true).Kind));
			Check("his grenade changes nothing in the field", !Hard(TileSandbox.Spawn(ProjectileID.Grenade, true).Kind));
			Check("his charge (SapperCharge) changes nothing in the field", !Hard(TileSandbox.Spawn(sapper, true).Kind) && !Hard(TileSandbox.Spawn(sapper, false).Kind));
			var kit = new global::Levy.Common.Officers.DemolitionistKit();
			var res = TileSandbox.Run(p =>
			{
				NPC demo = Main.npc[0]; demo.SetDefaults(NPCID.Demolitionist); demo.active = true; demo.townNPC = true; demo.life = demo.lifeMax;
				demo.Center = p.Center; demo.direction = 1; demo.spriteDirection = 1;
				NPC foe = Main.npc[1]; foe.SetDefaults(NPCID.GreenSlime); foe.active = true; foe.life = foe.lifeMax = 5000; foe.Center = p.Center + new Vector2(14 * 16f, 6 * 16f);
				var ctx = new global::Levy.Common.Officers.OfficerContext { Npc = demo, Def = global::Levy.Common.Officers.OfficerRegistry.Get(NPCID.Demolitionist), Commander = p, State = kit.CreateState(), Slot = 0 };
				for (int i = 0; i < 400; i++) { demo.Center = p.Center; foe.Center = p.Center + new Vector2(14 * 16f, 6 * 16f); kit.Tick(ctx); }
			}, 700);
			Note($"the real Demolitionist kit run in the field: made {string.Join(", ", res.Made.Select(ProjName))}; field -> {TileSandbox.KindText(res.Kind)} ({res.Changed} tiles), error {res.Error ?? "none"}");
			Check("the Demolitionist's kit threw something (the scenario is live)", res.Made.Count > 0 && res.Made.Contains(sapper));
			Check("the Demolitionist's kit changes nothing in the field", !Hard(res.Kind));
			foreach (var def in global::Levy.Common.Officers.OfficerRegistry.All)
			{
				int proj = def.Weapon?.Projectile ?? 0; if (proj <= 0) continue;
				var r = TileSandbox.Spawn(proj, true);
				Check($"officer {def.Key}: his weapon's projectile ({ProjName(proj)}) changes nothing in the field", !Hard(r.Kind));
			}
		}
	}
}
