using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// "[OnlyRule]": the category rule mode Only (designer: "only guns", the army is issued nothing from other categories), in the same real world scene as <see cref="ArmoryRulesWorldCheck"/> (a placed barracks and armory, real records, the real Re-arm and upgrade passes).
	/// What it pins down: a new category rule starts as Include; Only bars every other kind from primaries AND sidearms; it is a stock filter wherever the block sits (above or below the class balance, the designer's own
	/// class balance included); a levy holding another kind keeps it until one of the kind is free; an unarmed levy is armed from the rest when none is left (nobody is ever left bare by the rule); two Only blocks add up;
	/// the list survives the world save, the multiplayer wire and an old (pre-Only) save; an older build reading the new list would see a weight rule and keep every other block.
	/// </summary>
	public partial class ArmoryRulesWorldCheck
	{
		private static int _opass, _ofail;

		private static void OCheck(string name, bool ok) { if (ok) _opass++; else _ofail++; _mod.Logger.Info($"[OnlyRule] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void ONote(string s) => _mod.Logger.Info("[OnlyRule] OBSERVED " + s);

		/// <summary>Weapons in hand or on the belt (primary, sidearm, extra sidearms) whose categories are none of <paramref name="cats"/>.</summary>
		private static int Outside(Scene s, params string[] cats)
		{
			var r = s.Armory.Rules; int n = 0;
			using var _rs = ArmoryRules.Enter(r);
			void One(Item it) { if (it == null || it.IsAir) return; var c = r.CatsOf(it); if (!cats.Any(x => ArmoryRules.Has(c, x))) n++; }
			foreach (var l in s.Levies) { var g = l.Gear; One(g.Weapon); One(g.Sidearm); foreach (var e in g.ExtraSidearms) One(e); }
			return n;
		}

		private static RuleBlock OnlyB(ArmoryRules r, string cat) => Cat(r, CatMode.Only, cat);

		/// <summary>The designer's own class balance: at least <paramref name="min"/>, at most <paramref name="max"/> and a share of 2 for each vanilla class.</summary>
		private static void Balance(ArmoryRules r, int min, int max)
		{
			var b = r.AddBlock(RuleKind.ClassBalance);
			foreach (var cls in WeaponTags.VanillaClasses) { var e = b.EntryFor(cls, true); e.Min = min; e.Max = max; e.Weight = 2f; }
		}

		private static void StockShort(ArmoryTE te, int guns, bool withGunsOnly = false)
		{
			Clear(te);
			Stock(te, new int[] { ItemID.MusketBall }, 8, 999); Stock(te, new int[] { ItemID.WoodenArrow }, 4, 999);
			if (guns > 0) Stock(te, Guns.Take(guns), 1);
			if (!withGunsOnly) { Stock(te, Swords, 2); Stock(te, Bows, 2); Stock(te, Staves, 2); Stock(te, Spears, 2); }
		}

		private static void OnlyMode_(Scene s)
		{
			_opass = _ofail = 0;
			try { OnlyModeBody(s); }
			catch (Exception e) { OCheck("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			_mod.Logger.Info($"[OnlyRule] self-check pass={_opass} fail={_ofail}");
		}

		private static void OnlyModeBody(Scene s)
		{
			var te = s.Armory; int n = s.Levies.Count;

			// ---- the default and the rule's own queries
			var fresh = new ArmoryRules();
			var added = fresh.AddBlock(RuleKind.Category);
			OCheck($"a newly added category rule defaults to Include (got {added.Mode})", added.Mode == CatMode.Include);
			var q = new ArmoryRules(); OnlyB(q, "gun"); q.Changed();
			using (ArmoryRules.Enter(q))
			{
				var sword = Make(ItemID.GoldBroadsword); var gun = Make(ItemID.Musket); var bow = Make(ItemID.IronBow);
				OCheck("Only guns bars a sword and lets a gun through", q.Excluded(sword) && !q.Excluded(gun));
				OCheck("Only guns: a sword ranks 0, a gun ranks above 0", WeaponRank.Of(sword) == 0f && WeaponRank.Of(gun) > 0f);
				OCheck("Only guns claims a gun (the armor follows it, never the other way round)", q.Claims(gun) && !q.Claims(sword));
				using (ArmoryRules.RelaxOnly()) OCheck("the unarmed-levy fallback lifts the Only rule for one pick", !q.Excluded(sword));
				OCheck("and the rule is back once the pick is made", q.Excluded(sword));
				var two = new ArmoryRules(); OnlyB(two, "gun"); OnlyB(two, "bow"); two.Changed();
				OCheck("two Only blocks add up: guns and bows pass, swords do not", !two.Excluded(gun) && !two.Excluded(bow) && two.Excluded(sword));
				var off = new ArmoryRules(); OnlyB(off, "gun").On = false; off.Changed();
				OCheck("an Only block switched off bars nothing", !off.Excluded(sword));
				var nev = new ArmoryRules(); OnlyB(nev, "gun"); var l = nev.AddBlock(RuleKind.ItemList); l.List = ListMode.Never; l.Items.Add(ArmoryRules.KeyOf(ItemID.Handgun)); nev.Changed();
				using (ArmoryRules.RelaxOnly()) OCheck("the fallback never lifts a never-use list", nev.Excluded(Make(ItemID.Handgun)));
			}

			// ---- the army: every stock variant, a deal before the rule, then Re-arm; and the upgrade passes alone
			foreach (var variant in new[] { "plain", "mixed armor", "strong non-guns + mixed armor" })
			{
				StockVariant(s, variant); Ui(te, r => r.Blocks.Add(new RuleBlock { Kind = RuleKind.ClassBalance }));
				Rearm(s);
				string before = Tally(s, te.Rules);
				Ui(te, r => { r.Blocks.Add(new RuleBlock { Kind = RuleKind.ClassBalance }); OnlyB(r, "gun"); });
				Rearm(s);
				ONote($"[{variant}] Only guns, Re-arm: before [{before}] after [{Tally(s, te.Rules)}]");
				OCheck($"[{variant}] Only guns, then Re-arm: all {n} levies carry a gun ({CountCat(s, "gun")} do, {Bare(s)} bare)", CountCat(s, "gun") == n);
				OCheck($"[{variant}] Only guns, then Re-arm: no weapon of another kind in any hand or on any belt ({Outside(s, "gun")} found)", Outside(s, "gun") == 0);
				OCheck($"[{variant}] Only guns, then Re-arm: every gun has bullets ({GunsWithAmmo(s)} of {n})", GunsWithAmmo(s) == n);
				StockVariant(s, variant); Ui(te, r => r.Blocks.Add(new RuleBlock { Kind = RuleKind.ClassBalance }));
				Rearm(s);
				Ui(te, r => OnlyB(r, "gun"));
				Upgrades(s);
				ONote($"[{variant}] Only guns, upgrade passes only: {Tally(s, te.Rules)}");
				OCheck($"[{variant}] Only guns, upgrade passes only: all {n} levies carry a gun ({CountCat(s, "gun")} do)", CountCat(s, "gun") == n);
			}

			// ---- too few guns: nobody is left bare, the holders keep theirs, and they come to the guns once there are some
			StockShort(te, 3); foreach (var lv in s.Levies) lv.Gear = new SoldierGear();
			Ui(te, r => r.Blocks.Add(new RuleBlock { Kind = RuleKind.ClassBalance })); Rearm(s);
			Ui(te, r => OnlyB(r, "gun"));
			Upgrades(s);
			ONote("3 guns, the army armed before the rule, upgrade passes only: " + Tally(s, te.Rules));
			OCheck($"3 guns for {n} armed levies: the 3 guns are used ({CountCat(s, "gun")})", CountCat(s, "gun") == 3);
			OCheck($"3 guns for {n} armed levies: nobody is disarmed by the rule ({Bare(s)} bare)", Bare(s) == 0);
			Rearm(s);
			ONote("3 guns, Re-arm (every kit back to the armory first): " + Tally(s, te.Rules));
			OCheck($"3 guns, Re-arm: the 3 guns are used and nobody is left bare ({CountCat(s, "gun")} guns, {Bare(s)} bare)", CountCat(s, "gun") == 3 && Bare(s) == 0);
			Stock(te, Guns, 2);
			foreach (var lv in s.Levies) lv.Gear.UpgradeCheckTick = 0;
			Upgrades(s, 6);
			ONote("guns deposited, upgrade passes: " + Tally(s, te.Rules));
			OCheck($"guns deposited: the levies who took another kind trade it for a gun ({CountCat(s, "gun")} of {n})", CountCat(s, "gun") == n);
			OCheck($"guns deposited: no other kind is left on a belt ({Outside(s, "gun")} found)", Outside(s, "gun") == 0);

			// ---- no guns at all, a fresh army: armed from the rest
			StockShort(te, 0); foreach (var lv in s.Levies) lv.Gear = new SoldierGear();
			Ui(te, r => OnlyB(r, "gun"));
			Rearm(s);
			ONote("no guns in stock, a fresh army: " + Tally(s, te.Rules));
			OCheck($"no guns in stock: a fresh army is still armed ({Bare(s)} bare of {n})", Bare(s) == 0);

			// ---- the class balance (the designer's own: at least 8, at most 20, a share of 2 each), above and below the Only block; and a tighter one whose maximum bites
			foreach (var (label, min, max) in new[] { ("designer's 8/20/2", 8, 20), ("at least 1, at most 3, share 2", 1, 3) })
			foreach (bool onlyFirst in new[] { false, true })
			foreach (var variant in new[] { "plain", "mixed armor" })
			{
				StockVariant(s, variant); Ui(te, r => r.Blocks.Add(new RuleBlock { Kind = RuleKind.ClassBalance })); Rearm(s);
				Ui(te, r => { if (onlyFirst) OnlyB(r, "gun"); Balance(r, min, max); if (!onlyFirst) OnlyB(r, "gun"); });
				Rearm(s);
				string where = onlyFirst ? "Only above the class balance" : "Only below the class balance";
				ONote($"[{variant}] {label}, {where}: {Tally(s, te.Rules)}");
				OCheck($"[{variant}] class balance {label}, {where}: all {n} levies carry a gun ({CountCat(s, "gun")} do, {Bare(s)} bare)", CountCat(s, "gun") == n);
				OCheck($"[{variant}] class balance {label}, {where}: no other kind issued ({Outside(s, "gun")} found)", Outside(s, "gun") == 0);
			}

			// ---- two Only blocks: guns and bows
			StockShort(te, 3); Stock(te, Bows, 2); foreach (var lv in s.Levies) lv.Gear = new SoldierGear();
			Ui(te, r => { OnlyB(r, "gun"); OnlyB(r, "bow"); });
			Rearm(s);
			ONote("Only guns + Only bows, 3 guns and 20 bows: " + Tally(s, te.Rules));
			OCheck($"Only guns and Only bows: every levy carries one of the two ({CountCat(s, "gun") + CountCat(s, "bow")} of {n}), nothing else issued ({Outside(s, "gun", "bow")})", CountCat(s, "gun") + CountCat(s, "bow") == n && Outside(s, "gun", "bow") == 0);

			// ---- save, wire, old save
			Ui(te, r => { r.Blocks.Add(new RuleBlock { Kind = RuleKind.ClassBalance }); OnlyB(r, "gun"); });
			var want = te.Rules.ToBytes();
			var tag = new TagCompound(); te.SaveData(tag);
			var loaded = new ArmoryTE(); loaded.LoadData(tag);
			OCheck("an Only rule survives the world save and load (same bytes, mode Only)", loaded.Rules.ToBytes().SequenceEqual(want) && loaded.Rules.Blocks.Count == 2 && loaded.Rules.Blocks[1].Mode == CatMode.Only && loaded.Rules.Blocks[1].Cat == "gun");
			byte[] wire; using (var ms = new MemoryStream()) { using (var w = new BinaryWriter(ms)) te.NetSend(w); wire = ms.ToArray(); }
			var client = new ArmoryTE(); using (var ms = new MemoryStream(wire)) using (var rd = new BinaryReader(ms)) client.NetReceive(rd);
			OCheck("an Only rule arrives on a multiplayer client (same bytes)", client.Rules.ToBytes().SequenceEqual(want));
			te.SetRules(ArmoryRules.FromBytes(client.Rules.ToBytes()));
			StockVariant(s, "mixed armor"); Rearm(s);
			OCheck($"an Only rule from the client's packet works: all {n} carry a gun ({CountCat(s, "gun")}), nothing else issued ({Outside(s, "gun")})", CountCat(s, "gun") == n && Outside(s, "gun") == 0);
			te.Rules = loaded.Rules; te.Rules.Changed();
			StockVariant(s, "mixed armor"); Rearm(s);
			OCheck($"an Only rule loaded from a world save works: all {n} carry a gun ({CountCat(s, "gun")})", CountCat(s, "gun") == n);

			// an old save: the bytes a pre-Only build wrote (format 1: a class balance with a magic share, an Include for guns, no overrides, the sidearm limit and the retired toggle)
			byte[] old;
			using (var ms = new MemoryStream())
			{
				using (var w = new BinaryWriter(ms))
				{
					w.Write((byte)1); w.Write((ushort)2);
					w.Write((byte)RuleKind.ClassBalance); w.Write(true); w.Write((byte)1); w.Write("magic"); w.Write(1.5f); w.Write((short)0); w.Write((short)-1); w.Write((short)-1);
					w.Write((byte)RuleKind.Category); w.Write(true); w.Write((byte)2); w.Write("gun"); w.Write("sword"); w.Write(2f); w.Write((short)1);
					w.Write((ushort)0);
					w.Write((byte)3); w.Write(true);
				}
				old = ms.ToArray();
			}
			var oldTe = new ArmoryTE(); oldTe.LoadData(new TagCompound { ["rulesV1"] = old });
			var ob = oldTe.Rules.Blocks;
			OCheck("an old save (written before Only existed) loads with its blocks and modes intact", ob.Count == 2 && ob[0].Kind == RuleKind.ClassBalance && ob[0].Classes.Count == 1 && ob[0].Classes[0].Weight == 1.5f && ob[1].Kind == RuleKind.Category && ob[1].Mode == CatMode.Include && ob[1].Cat == "gun");
			OCheck("an old save reads back to the same bytes it was written with", oldTe.Rules.ToBytes().SequenceEqual(old));
			var noRules = new ArmoryTE(); noRules.LoadData(new TagCompound());
			OCheck("a save with no rules loads the default list", noRules.Rules.Blocks.Count == 1 && noRules.Rules.IsNeutral);
			// an older build reading a new list: the Only block differs from an Include block in its mode byte alone, and that byte is above the old builds' last mode, which they read as a weight rule
			var withOnly = new ArmoryRules(); OnlyB(withOnly, "gun"); var withInc = new ArmoryRules(); Cat(withInc, CatMode.Include, "gun");
			byte[] bo = withOnly.ToBytes(), bi = withInc.ToBytes();
			int diff = 0, at = -1; if (bo.Length == bi.Length) for (int i = 0; i < bo.Length; i++) if (bo[i] != bi[i]) { diff++; at = i; }
			OCheck($"an Only block is an Include block but for the mode byte (differs at {diff} byte(s), value {(at >= 0 ? bo[at] : -1)})", bo.Length == bi.Length && diff == 1 && bo[at] == (byte)CatMode.Only && bo[at] > (byte)CatMode.Min);
			var unknown = (byte[])bo.Clone(); if (at >= 0) unknown[at] = 9;
			var u = ArmoryRules.FromBytes(unknown);
			OCheck("a mode this build does not know reads as a weight rule, and the list is kept", u.Blocks.Count == 1 && u.Blocks[0].Mode == CatMode.Weight && u.Blocks[0].Cat == "gun");

			Ui(te, r => r.Blocks.Add(new RuleBlock { Kind = RuleKind.ClassBalance }));
		}
	}
}
