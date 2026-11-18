using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// SETS and ACC parts of the rules check. Every armor set of every loaded mod is found the way the game finds it (vanilla: <c>Player.UpdateArmorSets</c> tried on every head, body and legs slot; a mod: every piece's
	/// <c>ModItem.IsArmorSet</c> over the pieces of its own mod), then worn in full by a levy (the real stat pass, <c>RefreshStats</c>) and by a Player in a spare slot (<c>ResetEffects</c>, <c>UpdateEquips</c>,
	/// <c>UpdateArmorSets</c>, the mods' <c>PostUpdateEquips</c> and <c>PostUpdateMiscEffects</c> as local player). The check reads what the set changes on each (a fingerprint of the Player's public numbers and flags,
	/// the damage and crit modifiers of every damage class, and every ModPlayer field of every mod): the set must give the levy's dummy the same bonus text, and every number or flag the set moves on the Player must move
	/// on the levy's dummy the same way. The ACC part does the same for every accessory worn alone.
	/// </summary>
	public partial class LevyRulesCheck
	{
		private sealed class SetRow { public Item Head, Body, Legs; public string Mod; public string Name => Lang.GetItemNameValue(Head.type) + "/" + Lang.GetItemNameValue(Body.type) + "/" + Lang.GetItemNameValue(Legs.type); }

		// ------------------------------------------------------------------------------------------------ discovery
		private static List<SetRow> FindSets()
		{
			var heads = new List<Item>(); var bodies = new List<Item>(); var legs = new List<Item>();
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				Item it = ContentSamples.ItemsByType[t];
				if (it == null || it.IsAir || it.vanity) continue;
				if (it.headSlot >= 0) heads.Add(it);
				if (it.bodySlot >= 0) bodies.Add(it);
				if (it.legSlot >= 0) legs.Add(it);
			}
			var rows = new List<SetRow>();
			// ---- vanilla: one item per distinct slot id, every combination run through Player.UpdateArmorSets
			var vh = heads.Where(i => i.type < ItemID.Count).GroupBy(i => i.headSlot).Select(g => g.First()).ToList();
			var vb = bodies.Where(i => i.type < ItemID.Count).GroupBy(i => i.bodySlot).Select(g => g.First()).ToList();
			var vl = legs.Where(i => i.type < ItemID.Count).GroupBy(i => i.legSlot).Select(g => g.First()).ToList();
			var sp = new Player { whoAmI = 252 };
			int tried = 0;
			foreach (var h in vh)
				foreach (var b in vb)
					foreach (var l in vl)
					{
						tried++;
						sp.armor[0] = h; sp.armor[1] = b; sp.armor[2] = l;
						sp.head = h.headSlot; sp.body = b.bodySlot; sp.legs = l.legSlot;
						sp.setBonus = "";
						try { sp.UpdateArmorSets(252); } catch (Exception) { continue; }
						if (!string.IsNullOrEmpty(sp.setBonus)) rows.Add(new SetRow { Head = h, Body = b, Legs = l, Mod = "Terraria" });
						if ((tried & 0xFFFFF) == 0) sp = new Player { whoAmI = 252 }; // a million passes of accumulated stat bonuses are not worth keeping
					}
			Note($"sets: {vh.Count} head, {vb.Count} body, {vl.Count} legs slots tried together ({tried:N0} combinations) found {rows.Count} vanilla sets");
			// ---- every mod: its own pieces, whichever piece owns the IsArmorSet test
			foreach (Mod mod in ModLoader.Mods)
			{
				if (mod.Name == "ModLoader") continue;
				var mh = heads.Where(i => i.ModItem?.Mod == mod).ToList(); var mb = bodies.Where(i => i.ModItem?.Mod == mod).ToList(); var ml = legs.Where(i => i.ModItem?.Mod == mod).ToList();
				if (mh.Count == 0 || mb.Count == 0 || ml.Count == 0) continue;
				var owns = new Dictionary<int, bool>();
				bool Owns(Item i) { if (!owns.TryGetValue(i.type, out bool v)) owns[i.type] = v = i.ModItem != null && i.ModItem.GetType().GetMethod("IsArmorSet", BindingFlags.Public | BindingFlags.Instance)?.DeclaringType != typeof(ModItem); return v; }
				int found = 0;
				foreach (var h in mh)
				{
					bool ho = Owns(h);
					foreach (var b in mb)
					{
						bool bo = Owns(b);
						foreach (var l in ml)
						{
							if (!ho && !bo && !Owns(l)) continue;
							bool yes = false;
							try { yes = (ho && h.ModItem.IsArmorSet(h, b, l)) || (bo && b.ModItem.IsArmorSet(h, b, l)) || (Owns(l) && l.ModItem.IsArmorSet(h, b, l)); } catch (Exception) { }
							if (yes) { rows.Add(new SetRow { Head = h, Body = b, Legs = l, Mod = mod.Name }); found++; }
						}
					}
				}
				Note($"sets: {mod.Name}: {mh.Count} head, {mb.Count} body, {ml.Count} legs pieces found {found} sets");
			}
			return Reduce(rows);
		}

		/// <summary>A set test that ignores one piece (a set bonus that needs only the helmet and the breastplate) matches every piece of that kind: the pairs that match more than eight of them keep one row, the one whose pieces share a name.</summary>
		private static List<SetRow> Reduce(List<SetRow> rows)
		{
			var cHB = rows.GroupBy(r => (r.Head.type, r.Body.type)).ToDictionary(g => g.Key, g => g.Count());
			var cHL = rows.GroupBy(r => (r.Head.type, r.Legs.type)).ToDictionary(g => g.Key, g => g.Count());
			var cBL = rows.GroupBy(r => (r.Body.type, r.Legs.type)).ToDictionary(g => g.Key, g => g.Count());
			string W(Item i) => (Lang.GetItemNameValue(i.type) ?? "").Split(' ')[0];
			int Score(SetRow r) => (W(r.Head) == W(r.Body) ? 1 : 0) + (W(r.Body) == W(r.Legs) ? 1 : 0) + (W(r.Head) == W(r.Legs) ? 1 : 0);
			var list = new List<SetRow>(); var keep = new Dictionary<string, SetRow>();
			foreach (var r in rows)
			{
				bool lw = cHB[(r.Head.type, r.Body.type)] > 8, bw = cHL[(r.Head.type, r.Legs.type)] > 8, hw = cBL[(r.Body.type, r.Legs.type)] > 8;
				if (!lw && !bw && !hw) { list.Add(r); continue; }
				string key = r.Mod + "|" + (hw ? "*" : r.Head.type.ToString()) + "|" + (bw ? "*" : r.Body.type.ToString()) + "|" + (lw ? "*" : r.Legs.type.ToString());
				if (!keep.TryGetValue(key, out var cur) || Score(r) > Score(cur)) keep[key] = r;
			}
			list.AddRange(keep.Values);
			return list;
		}

		// ------------------------------------------------------------------------------------------------ the fingerprint
		private static FieldInfo[] _playerFields;
		private static List<(Mod mod, ModPlayer mp, FieldInfo[] fields)> _modPlayers;
		private static List<DamageClass> _classes;
		private static readonly HashSet<string> Visual = new() { "face", "neck", "back", "front", "handon", "handoff", "shoe", "waist", "balloon", "shield", "beard", "head", "body", "legs", "wings", "faceFlower", "backpack", "tail", "eyeWhite", "skinVariant" };
		private static readonly HashSet<string> Noise = new() { "position", "velocity", "oldPosition", "oldVelocity", "miscCounter", "whoAmI", "statLife", "statMana", "lifeRegenTime", "lifeRegenCount", "manaRegenCount", "manaRegenDelay", "breath", "breathCD", "itemTime", "itemAnimation", "gfxOffY", "legFrameCounter", "bodyFrameCounter", "ghostFade", "ghostDir", "afkCounter", "potionDelay", "fallStart", "fallStart2", "lastCreatureHit", "sandStormCounter", "yoraiz0rEye", "timeSinceLastDashStarted", "oldDirection", "sunflower", "rocketTime" };

		private static void InitPrint()
		{
			if (_playerFields != null) return;
			_playerFields = typeof(Player).GetFields(BindingFlags.Public | BindingFlags.Instance)
				.Where(f => !f.IsStatic && !Visual.Contains(f.Name) && !Noise.Contains(f.Name) && (f.FieldType == typeof(bool) || f.FieldType == typeof(int) || f.FieldType == typeof(float) || f.FieldType == typeof(double) || f.FieldType == typeof(byte) || f.FieldType == typeof(short) || f.FieldType == typeof(StatModifier))).ToArray();
			_modPlayers = new();
			foreach (Mod m in ModLoader.Mods)
			{
				if (m.Name == "ModLoader") continue;
				foreach (ModPlayer mp in m.GetContent<ModPlayer>())
				{
					var fs = mp.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
						.Where(f => !f.IsStatic && !f.IsInitOnly && !(m.Name == "CalamityMod" && global::Levy.Common.Compat.Calamity.CalamityLevyPatches.LightingOnly(f.Name)) && (f.FieldType == typeof(bool) || f.FieldType == typeof(int) || f.FieldType == typeof(float) || f.FieldType == typeof(double) || f.FieldType == typeof(byte) || f.FieldType == typeof(short) || f.FieldType == typeof(long) || f.FieldType == typeof(StatModifier))).ToArray();
					if (fs.Length > 0) _modPlayers.Add((m, mp, fs));
				}
			}
			_classes = new List<DamageClass> { DamageClass.Generic, DamageClass.Melee, DamageClass.MeleeNoSpeed, DamageClass.Ranged, DamageClass.Magic, DamageClass.Summon, DamageClass.SummonMeleeSpeed, DamageClass.MagicSummonHybrid, DamageClass.Throwing };
			_classes.AddRange(ModContent.GetContent<DamageClass>());
		}

		private static void Put(Dictionary<string, double> d, string key, object v)
		{
			switch (v)
			{
				case bool b: d[key] = b ? 1 : 0; break;
				case int i: d[key] = i; break;
				case float f: if (!float.IsNaN(f) && !float.IsInfinity(f)) d[key] = f; break;
				case double x: if (!double.IsNaN(x) && !double.IsInfinity(x)) d[key] = x; break;
				case byte by: d[key] = by; break;
				case short sh: d[key] = sh; break;
				case long l: d[key] = l; break;
				case StatModifier m: d[key + ".add"] = m.Additive; d[key + ".mult"] = m.Multiplicative; d[key + ".flat"] = m.Flat; d[key + ".base"] = m.Base; break;
			}
		}

		private static Dictionary<string, double> Print(Player p)
		{
			InitPrint();
			var d = new Dictionary<string, double>(2048);
			foreach (var f in _playerFields) { try { Put(d, f.Name, f.GetValue(p)); } catch (Exception) { } }
			d["statDefense"] = (int)p.statDefense; d["statDefense.add"] = p.statDefense.AdditiveBonus.Value; d["statDefense.mult"] = p.statDefense.FinalMultiplier.Value;
			foreach (var dc in _classes)
			{
				string n = dc.FullName;
				Put(d, "dmg:" + n, p.GetDamage(dc)); Put(d, "crit:" + n, p.GetCritChance(dc)); Put(d, "atk:" + n, p.GetAttackSpeed(dc)); Put(d, "kb:" + n, p.GetKnockback(dc)); Put(d, "pen:" + n, p.GetArmorPenetration(dc));
			}
			foreach (var (mod, mp, fields) in _modPlayers)
			{
				ModPlayer inst; try { inst = p.GetModPlayer(mp); } catch (Exception) { continue; }
				string pre = mod.Name + "." + mp.GetType().Name + ".";
				foreach (var f in fields) { try { Put(d, pre + f.Name, f.GetValue(inst)); } catch (Exception) { } }
			}
			// the buffs the pass left on the Player
			for (int b = 0; b < p.buffType.Length; b++) if (p.buffType[b] > 0) d["buff:" + p.buffType[b]] = 1;
			return d;
		}

		/// <summary>What <paramref name="on"/> changes against <paramref name="off"/>, on one wearer: field -> delta (|delta| above noise).</summary>
		private static Dictionary<string, double> Delta(Dictionary<string, double> on, Dictionary<string, double> off)
		{
			var r = new Dictionary<string, double>();
			foreach (var kv in on)
			{
				off.TryGetValue(kv.Key, out double o);
				double dv = kv.Value - o;
				if (Math.Abs(dv) > 1e-4) r[kv.Key] = dv;
			}
			foreach (var kv in off) if (!on.ContainsKey(kv.Key) && Math.Abs(kv.Value) > 1e-4) r[kv.Key] = -kv.Value;
			return r;
		}

		/// <summary>The fields the Player's delta moves that the levy's delta does not move the same way.</summary>
		private static List<string> Missing(Dictionary<string, double> refDelta, Dictionary<string, double> levyDelta)
		{
			var miss = new List<string>();
			foreach (var kv in refDelta)
			{
				if (kv.Key.StartsWith("buff:") || kv.Key == "statManaMax2" || kv.Key == "statLifeMax2" || kv.Key == "sporeSac" || kv.Key == "crystalLeaf" || kv.Key.EndsWith(".coinDropMult")) continue; // coinDropMult follows the wearer's luck; // sporeSac: the levy's Spore Sac is hand-made (AccessoryProc.cs) and held back from the real step on purpose; // life and mana pools are the record's (crystals and the pool the gear adds are read by SyncDummyMana) // a buff a set casts on its wearer; a levy's buffs are on its body, and the two the army builds by hand (Stardust Guardian, Chlorophyte leaf) are checked on their own
				levyDelta.TryGetValue(kv.Key, out double lv);
				if (Math.Abs(lv) < 1e-4 || Math.Sign(lv) != Math.Sign(kv.Value)) miss.Add($"{kv.Key} Player {kv.Value:0.###} levy {lv:0.###}");
			}
			return miss;
		}

		// ------------------------------------------------------------------------------------------------ one wearer each
		private static Dictionary<string, double> LevyPrint(SoldierNPC s, Item head, Item body, Item legs, Item acc, out string bonus)
		{
			var g = s.Record.Gear;
			foreach (Item it in new[] { head, body, legs, acc }) if (it != null) it.playerIndexTheItemIsReservedFor = 0; // made while the commander was the local player, as in single player: the levy's own slot must be put on them
			g.Head = head ?? new Item(); g.Body = body ?? new Item(); g.Legs = legs ?? new Item();
			g.Accessories[0] = acc ?? new Item();
			s.StatsDirty = true; s.RefreshStats(); s.RefreshStats(); // two passes: a Player runs the stat pass every tick, and a mod's timers and counters (Thorium's shield timers, Calamity's cooldowns) settle on the second
			bonus = s.Dummy.setBonus ?? "";
			return Print(s.Dummy);
		}

		private static Dictionary<string, double> RefPrint(Item head, Item body, Item legs, Item acc, out string bonus)
		{
			var rp = NewRef(500);
			RefEquip(rp, head, body, legs, acc != null ? new[] { acc } : new Item[0]);
			AsRef(rp, () => { RefStatPass(rp); RefStatPass(rp); });
			bonus = rp.setBonus ?? "";
			var d = Print(rp);
			return d;
		}

		private static Item Copy(Item it) => it == null ? null : Make(it.type);

		private static void ClearSpawns() { for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false; }

		// ------------------------------------------------------------------------------------------------ SETS
		private static void SetsPart()
		{
			Arena(FloorY, Half); PlaceCommander(-150);
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			var sets = FindSets();
			double findS = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) / (double)System.Diagnostics.Stopwatch.Frequency;
			Note($"sets: found {sets.Count} armor sets in {findS:0.0}s ({string.Join(", ", sets.GroupBy(r => r.Mod).Select(g => g.Key + " " + g.Count()))})");
			Check($"sets: the loaded mods' armor sets were found ({sets.Count})", sets.Count >= 60);
			var s = Levy(new Vector2((Cx + 10) * 16f, _floorY * 16f)); if (s == null) { Check("sets: the levy spawns", false); return; }
			int passN = 0, failN = 0, noText = 0, errN = 0, controlHit = 0, controlN = 0; long fieldsSeen = 0;
			var perMod = new Dictionary<string, (int total, int pass, int fail)>();
			var failures = new List<string>(); var quiet = new List<string>();
			try
			{
				Step(5);
				var levyOff = LevyPrint(s, null, null, null, null, out _);
				var refOff = RefPrint(null, null, null, null, out _);
				foreach (var row in sets)
				{
					try
					{
						Remove(s); s = Levy(new Vector2((Cx + 10) * 16f, _floorY * 16f)); s?.AllowFriendlyBuffs(); // a fresh levy for every set: a mod's timers and counters on the dummy carry over from the set before otherwise
						var lOn = LevyPrint(s, Copy(row.Head), Copy(row.Body), Copy(row.Legs), null, out string lb);
						ClearSpawns();
						var rOn = RefPrint(Copy(row.Head), Copy(row.Body), Copy(row.Legs), null, out string rb);
						ClearSpawns();
						var rd = Delta(rOn, refOff);
						var miss = Missing(rd, Delta(lOn, levyOff));
						fieldsSeen += rd.Count; if (rd.Count > 0) { controlN++; if (Missing(rd, new Dictionary<string, double>()).Count > 0) controlHit++; } else quiet.Add(row.Name); // the control: a levy with nothing on must not look like it wears the set
						if (rd.ContainsKey("buff:" + BuffID.StardustGuardianMinion) && !s.WearsStardustSet) miss.Add("Stardust Guardian not fielded (hand-made twin)");
						if (rd.ContainsKey("buff:" + BuffID.LeafCrystal) && !s.WearsChlorophyteSet) miss.Add("Chlorophyte leaf not fielded (hand-made twin)");
						bool textOk = lb == rb;
						if (string.IsNullOrEmpty(rb)) noText++;
						bool ok = textOk && miss.Count == 0;
						var pm = perMod.TryGetValue(row.Mod, out var cur) ? cur : (0, 0, 0);
						perMod[row.Mod] = (pm.Item1 + 1, pm.Item2 + (ok ? 1 : 0), pm.Item3 + (ok ? 0 : 1));
						if (ok) passN++;
						else
						{
							failN++;
							string msg = $"[{row.Mod}] {row.Name}: " + (textOk ? "" : $"bonus text Player '{Trim(rb)}' levy '{Trim(lb)}'; ") + (miss.Count > 0 ? $"{miss.Count} effects not applied: " + string.Join("; ", miss.Take(4)) : "");
							failures.Add(msg);
						}
					}
					catch (Exception e) { errN++; failures.Add($"[{row.Mod}] {row.Name}: threw {e.GetType().Name}: {e.Message}"); }
				}
			}
			finally { Remove(s); ClearSpawns(); }
			Note($"sets control: the Player's sets move {fieldsSeen:N0} numbers and flags in all; a levy wearing nothing is told apart from the Player in {controlHit} of {controlN} sets");
			if (quiet.Count > 0) Note($"sets: {quiet.Count} sets change nothing the fingerprint can see on a Player (nothing to compare): " + string.Join("; ", quiet.Take(40)));
			Check($"sets control: the comparison can tell a levy with no set from a Player in a set ({controlHit} of {controlN})", controlN > 0 && controlHit >= controlN * 0.9);
			foreach (var kv in perMod.OrderBy(k => k.Key)) Note($"sets table: {kv.Key,-20} sets {kv.Value.total,4}  pass {kv.Value.pass,4}  fail {kv.Value.fail,4}");
			Note($"sets table: total {sets.Count}, pass {passN}, fail {failN}, threw {errN}; {noText} of them have no bonus text on a Player either (their effects are compared all the same)");
			foreach (var f in failures.Take(120)) Note("sets mismatch " + f);
			if (failures.Count > 120) Note($"sets: {failures.Count - 120} more failures not listed");
			foreach (var kv in perMod.OrderBy(k => k.Key)) Check($"sets [{kv.Key}]: every set gives a levy what it gives a Player ({kv.Value.pass} of {kv.Value.total})", kv.Value.fail == 0);
		}

		private static string Trim(string s) => s == null ? "" : s.Replace("\n", " | ");

		// ------------------------------------------------------------------------------------------------ ACC
		private static void AccessoriesPart()
		{
			Arena(FloorY, Half); PlaceCommander(-150);
			var accs = new List<Item>();
			for (int t = 1; t < ItemLoader.ItemCount; t++) { Item it = ContentSamples.ItemsByType[t]; if (it != null && !it.IsAir && it.accessory && !it.vanity) accs.Add(it); }
			var s = Levy(new Vector2((Cx + 10) * 16f, _floorY * 16f)); if (s == null) { Check("acc: the levy spawns", false); return; }
			var skipped = new List<string>(); int passN = 0, failN = 0, errN = 0, controlHit = 0, controlN = 0; long fieldsSeen = 0; var perMod = new Dictionary<string, (int total, int pass, int fail)>(); var failures = new List<string>();
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			try
			{
				Step(5);
				var levyOff = LevyPrint(s, null, null, null, null, out _);
				var refOff = RefPrint(null, null, null, null, out _);
				foreach (var a in accs)
				{
					string mod = a.ModItem?.Mod.Name ?? "Terraria";
					if (a.type == ItemID.SporeSac || (a.ModItem != null && mod == "CalamityMod" && a.ModItem.Name == "FishStocks")) { skipped.Add(Lang.GetItemNameValue(a.type)); continue; } // the Spore Sac is a hand-made twin on a levy (AccessoryProc.cs) and throws in vanilla's step for a Player in a slot of the test's choosing; Fish Stocks' numbers are a random walk
					try
					{
						Remove(s); s = Levy(new Vector2((Cx + 10) * 16f, _floorY * 16f)); s?.AllowFriendlyBuffs(); // a fresh levy for every accessory (see SetsPart)
						var lOn = LevyPrint(s, null, null, null, Make(a.type), out _);
						ClearSpawns();
						var rOn = RefPrint(null, null, null, Make(a.type), out _);
						ClearSpawns();
						var rd = Delta(rOn, refOff);
						var miss = Missing(rd, Delta(lOn, levyOff));
						fieldsSeen += rd.Count; if (rd.Count > 0) { controlN++; if (Missing(rd, new Dictionary<string, double>()).Count > 0) controlHit++; }
						var pm = perMod.TryGetValue(mod, out var cur) ? cur : (0, 0, 0);
						perMod[mod] = (pm.Item1 + 1, pm.Item2 + (miss.Count == 0 ? 1 : 0), pm.Item3 + (miss.Count == 0 ? 0 : 1));
						if (miss.Count == 0) passN++;
						else { failN++; failures.Add($"[{mod}] {Lang.GetItemNameValue(a.type)} (defense {a.defense}): {miss.Count} effects not applied: " + string.Join("; ", miss.Take(4)) + $" [statDefense Player {rOn.GetValueOrDefault("statDefense")} (bare {refOff.GetValueOrDefault("statDefense")}), levy {lOn.GetValueOrDefault("statDefense")} (bare {levyOff.GetValueOrDefault("statDefense")}); levy life {s.NPC.life}/{s.NPC.lifeMax} dummy {s.Dummy.statLife}/{s.Dummy.statLifeMax2}; body buffs [{string.Join(",", Enumerable.Range(0, s.NPC.buffType.Length).Where(i => s.NPC.buffType[i] > 0).Select(i => s.NPC.buffType[i] + ":" + s.NPC.buffTime[i]))}] dummy buffs [{string.Join(",", s.Dummy.buffType.Where(t => t > 0))}]]"); }
					}
					catch (Exception e) { errN++; failures.Add($"[{mod}] {Lang.GetItemNameValue(a.type)}: threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}"); }
				}
			}
			finally { Remove(s); ClearSpawns(); }
			double secs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) / (double)System.Diagnostics.Stopwatch.Frequency;
			if (skipped.Count > 0) Note("acc: not compared: " + string.Join(", ", skipped));
			Note($"acc control: {controlN} accessories move something on the Player ({fieldsSeen:N0} numbers and flags in all); a levy wearing nothing is told apart in {controlHit} of them");
			Check($"acc control: the comparison can tell a levy with no accessory from a Player wearing one ({controlHit} of {controlN})", controlN > 0 && controlHit >= controlN * 0.9);
			foreach (var kv in perMod.OrderBy(k => k.Key)) Note($"acc table: {kv.Key,-20} accessories {kv.Value.total,4}  pass {kv.Value.pass,4}  fail {kv.Value.fail,4}");
			Note($"acc table: total {accs.Count}, pass {passN}, fail {failN}, threw {errN} ({secs:0.0}s)");
			foreach (var f in failures.Take(150)) Note("acc mismatch " + f);
			if (failures.Count > 150) Note($"acc: {failures.Count - 150} more failures not listed");
			foreach (var kv in perMod.OrderBy(k => k.Key)) Check($"acc [{kv.Key}]: every accessory gives a levy what it gives a Player ({kv.Value.pass} of {kv.Value.total})", kv.Value.fail == 0);
		}
	}
}
