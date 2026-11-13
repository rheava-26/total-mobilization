using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Observe;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Boot self-check of the Armory UX batch (dedicated server only; throwaway armories and records): the rule list's new fields round-trip and old data loads as no limit, the sidearm limit and the summon-weapon rule
	/// shape a levy's kit and take back what is over, a stripped levy is issued nothing, hand-set items can be listed and cleared without the item in the armory, the map order needs a double-click, and the levy
	/// cap curve. Logs "[ArmoryUX] ..." lines and a final "[ArmoryUX] self-check DONE pass=N fail=M".
	/// </summary>
	public sealed class ArmoryUXCheck : ModSystem
	{
		private static Item Make(int type, int stack = 1) { var i = new Item(); i.SetDefaults(type); i.stack = stack; return i; }
		private static string N(Item it) => it == null || it.IsAir ? "-" : it.Name;

		public override void PostSetupRecipes()
		{
			if (!Main.dedServ) return;
			int pass = 0, fail = 0;
			void Check(string name, bool ok, string detail = "") { if (ok) pass++; else fail++; Mod.Logger.Info($"[ArmoryUX] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}"); }
			var savedServed = ArmoryTE.TestServed;
			try
			{
				WeaponRank.Force(WeaponRankMode.Damage, false);
				ArmorDoctrine.Reset();
				RulesFields(Check);
				SidearmRules(Check);
				Stripped(Check);
				HandSet(Check);
				Cameras(Check);
				var why = ""; Check("map click: a drag never sends, a single click does nothing, a double-click sends", MapClicks.SelfCheck(out why), why);
				CapCurve(Check);
			}
			catch (Exception e) { fail++; Mod.Logger.Info("[ArmoryUX] FAIL threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
			finally { ArmoryTE.TestServed = savedServed; WeaponRank.Release(); }
			Mod.Logger.Info($"[ArmoryUX] self-check DONE pass={pass} fail={fail}");
		}

		private delegate void CheckFn(string name, bool ok, string detail = "");

		private static void Cameras(CheckFn check)
		{
			int gopro = ModContent.ItemType<Content.Items.Observe.GoProItem>();
			Item[] Stk(int cams) { var l = new List<Item> { Make(ItemID.Musket), Make(ItemID.MusketBall, 300), Make(ItemID.IronBroadsword), Make(ItemID.BandofRegeneration), Make(ItemID.HermesBoots), Make(ItemID.CloudinaBottle), Make(ItemID.ShinyRedBalloon) }; for (int i = 0; i < cams; i++) l.Add(Make(gopro)); return l.ToArray(); }
			SoldierRecord[] Crew() { var a = new SoldierRecord[4]; for (int i = 0; i < 4; i++) { a[i] = new SoldierRecord { Id = 700 + i }; Armor(a[i]); } a[2].Veteran = true; return a; }
			static int Cams(SoldierRecord r) => (r.Gear.HasCamera ? 1 : 0);
			static int AccGoPro(SoldierRecord r, int t) => r.Gear.Accessories.Count(a => !a.IsAir && a.type == t);
			static string Accs(SoldierRecord r) => string.Join(",", r.Gear.Accessories.Take(r.AccessorySlots).Select(a => a.IsAir ? "-" : a.type.ToString()));

			var ctl = Crew(); var teC = Armory(null, Stk(0)); ArmoryTE.TestServed = ctl.ToList();
			foreach (var r in ctl) teC.IssueGear(r);
			var cr = Crew(); var te = Armory(null, Stk(2)); ArmoryTE.TestServed = cr.ToList();
			foreach (var r in cr) te.IssueGear(r);
			check("2 GoPros and 4 levies: exactly 2 levies carry a camera", cr.Sum(Cams) == 2, string.Join(",", cr.Select(Cams)));
			check("the cameras go to the sergeant, then the lowest id", cr[2].Gear.HasCamera && cr[0].Gear.HasCamera && !cr[1].Gear.HasCamera && !cr[3].Gear.HasCamera);
			check("no GoPro sits in an accessory slot", cr.Sum(r => AccGoPro(r, gopro)) == 0);
			check("accessory allocation is the same as with no GoPros stocked", Enumerable.Range(0, 4).All(i => Accs(cr[i]) == Accs(ctl[i])), string.Join(" | ", cr.Select(Accs)));
			check("the camera slot holds a GoPro and Carries sees it", cr[0].Gear.Camera.type == gopro && Content.Items.Observe.GoProItem.Carries(cr[0].Gear) && !Content.Items.Observe.GoProItem.Carries(cr[1].Gear));
			var te3 = Armory(null, Stk(6)); var c3 = Crew(); ArmoryTE.TestServed = c3.ToList();
			foreach (var r in c3) te3.IssueGear(r);
			check("more GoPros than levies: one each, the rest stay in stock", c3.All(r => Cams(r) == 1) && te3.Storage.Count(i => !i.IsAir && i.type == gopro) == 2);
			// return
			var back = cr[0].Gear; te.ReturnGear(back);
			check("returning gear returns the camera to the Armory", !back.HasCamera && te.Storage.Any(i => !i.IsAir && i.type == gopro));
			// hand rules
			var g = new SoldierGear();
			check("only a GoPro fits the camera slot", g.CanHold(SoldierGear.SlotCamera, Make(gopro)) && !g.CanHold(SoldierGear.SlotCamera, Make(ItemID.HermesBoots)) && !g.CanHold(SoldierGear.SlotCamera, Make(ItemID.Musket)));
			check("a GoPro does not fit an accessory slot", !g.CanHold(SoldierGear.SlotAcc0, Make(gopro)));
			// save / load / clone / sync
			g.Camera = Make(gopro); g.SetLocked(SoldierGear.SlotCamera, true);
			var ld = SoldierGear.Load(g.Save());
			check("camera and its lock survive save and load", ld.Camera.type == gopro && ld.IsLocked(SoldierGear.SlotCamera) && g.Clone().Camera.type == gopro && g.AllItems().Any(i => i.type == gopro));
			using (var ms = new MemoryStream()) {
				using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) g.WriteVisual(w);
				var vis = new SoldierGear(); using (var rd = new BinaryReader(new MemoryStream(ms.ToArray()))) vis.ReadVisual(rd);
				check("the camera reaches clients", vis.Camera.type == gopro && Content.Items.Observe.GoProItem.Carries(vis));
			}
			// migration of an old save: GoPro in accessory slot 1, locked, slot still holding a real accessory beside it
			var old = new SoldierGear(); old.Accessories[0] = Make(ItemID.HermesBoots); old.Accessories[1] = Make(gopro); old.SetLocked(SoldierGear.SlotAcc0 + 1, true);
			var oldTag = old.Save(); oldTag.Remove("cam"); // an older save has no camera entry
			var mig = SoldierGear.Load(oldTag);
			check("an old save's accessory-slot GoPro moves to the camera slot", mig.Camera.type == gopro && mig.Accessories[1].IsAir && mig.Accessories[0].type == ItemID.HermesBoots && mig.IsLocked(SoldierGear.SlotCamera) && !mig.IsLocked(SoldierGear.SlotAcc0 + 1));
			var oldFull = new SoldierGear(); oldFull.Camera = Make(gopro); oldFull.Accessories[0] = Make(gopro);
			var mig2 = SoldierGear.Load(oldFull.Save());
			check("an accessory-slot GoPro still counts when the camera slot is taken", Content.Items.Observe.GoProItem.Carries(mig2) && mig2.Camera.type == gopro);
			check("a levy with a GoPro only in an accessory slot still carries", Content.Items.Observe.GoProItem.Carries(new SoldierGear { Accessories = new[] { Make(gopro), new Item(), new Item(), new Item(), new Item(), new Item() } }));
		}

		private static void RulesFields(CheckFn check)
		{
			var r = ArmoryRules.Default();
			check("a new rule list has no limit", r.SidearmLimit == LevyConstants.MaxSidearms && !r.LimitsLoadouts && r.IsNeutral);
			r.SidearmLimit = 1;
			check("a limited list is not neutral", r.LimitsLoadouts && !r.IsNeutral);
			var back = ArmoryRules.FromBytes(r.ToBytes());
			check("the limits round-trip through the wire format", back.SidearmLimit == 1 && back.Blocks.Count == 1);
			var clone = r.Clone(); var cp = ArmoryRules.Default(); cp.CopyFrom(r);
			check("Clone and CopyFrom carry the limits", clone.SidearmLimit == 1 && cp.SidearmLimit == 1);
			var plain = ArmoryRules.Default(); var bytes = plain.ToBytes();
			var old = new byte[bytes.Length - 2]; Array.Copy(bytes, old, old.Length); // data written before the limits existed
			var oldRead = ArmoryRules.FromBytes(old);
			check("data written before the limits existed loads as no limit", oldRead.SidearmLimit == LevyConstants.MaxSidearms && oldRead.Blocks.Count == 1);
			// a list saved with the retired summon toggle OFF reads as no limit: the toggle is gone and nothing in the file can switch the old behaviour back on
			var toggled = plain.ToBytes(); toggled[toggled.Length - 1] = 0;
			var toggledRead = ArmoryRules.FromBytes(toggled);
			check("a list saved with the old summon toggle off loads as an ordinary list", toggledRead.SidearmLimit == LevyConstants.MaxSidearms && !toggledRead.LimitsLoadouts && toggledRead.IsNeutral && toggledRead.Blocks.Count == 1);
		}

		private static ArmoryTE Armory(ArmoryRules rules, params Item[] stock)
		{
			var te = new ArmoryTE();
			for (int i = 0; i < stock.Length; i++) te.Storage[i] = stock[i];
			if (rules != null) te.Rules.CopyFrom(rules);
			return te;
		}

		private static void Armor(SoldierRecord r) { r.Gear.Head = Make(ItemID.VortexHelmet); r.Gear.Body = Make(ItemID.VortexBreastplate); r.Gear.Legs = Make(ItemID.VortexLeggings); }
		private static string Kit(SoldierRecord r) => $"{N(r.Gear.Weapon)}; sidearms {string.Join(" | ", r.Gear.Sidearms().Select(N))}";
		private static Item[] Stock() => new[] { Make(ItemID.Musket), Make(ItemID.MusketBall, 300), Make(ItemID.IronBroadsword), Make(ItemID.SlimeStaff), Make(ItemID.QueenSpiderStaff) };
		/// <summary>Puts accessories on a levy by hand (locked, so the armory leaves them alone).</summary>
		private static void Wear(SoldierRecord r, params int[] accessories) { for (int i = 0; i < accessories.Length; i++) { r.Gear.Accessories[i] = Make(accessories[i]); r.Gear.SetLocked(SoldierGear.SlotAcc0 + i, true); } }
		private static int Staves(SoldierRecord r) => r.Gear.Sidearms().Count(s => !s.IsAir && SidearmPolicy.IsStaff(s));
		private static int Sentries(SoldierRecord r) => r.Gear.Sidearms().Count(s => !s.IsAir && SidearmPolicy.IsSentry(s));
		private static int Summons(SoldierRecord r) => r.Gear.Sidearms().Count(s => !s.IsAir && SidearmPolicy.IsDeployer(s));

		private static void SidearmRules(CheckFn check)
		{
			// no limit: the full kit, as before
			var free = new SoldierRecord { Id = 901 };
			var te0 = Armory(null, Stock()); Armor(free); ArmoryTE.TestServed = new List<SoldierRecord> { free }; te0.IssueGear(free);
			check("with no limit and no extra summon slot a ranged levy carries a blade and no summon weapon", free.Gear.SidearmCount == 1 && Summons(free) == 0, Kit(free));

			// no extra slot: no summon item, whatever the armory stocks. An extra minion slot lets a staff ride as a sidearm, an extra sentry slot a sentry staff.
			var minionSlot = new SoldierRecord { Id = 911 }; Armor(minionSlot); Wear(minionSlot, ItemID.PapyrusScarab);
			var te6 = Armory(null, Stock()); ArmoryTE.TestServed = new List<SoldierRecord> { minionSlot }; te6.IssueGear(minionSlot);
			check("with an extra minion slot a ranged levy carries a minion staff in a sidearm slot", Staves(minionSlot) == 1 && Sentries(minionSlot) == 0 && minionSlot.Gear.SidearmCount == 2, Kit(minionSlot));
			var sentrySlot = new SoldierRecord { Id = 912 }; Armor(sentrySlot); Wear(sentrySlot, ItemID.SquireShield);
			var te7 = Armory(null, Stock()); ArmoryTE.TestServed = new List<SoldierRecord> { sentrySlot }; te7.IssueGear(sentrySlot);
			check("with an extra sentry slot a ranged levy carries a sentry staff and no minion staff", Sentries(sentrySlot) == 1 && Staves(sentrySlot) == 0, Kit(sentrySlot));
			var bothSlots = new SoldierRecord { Id = 913 }; Armor(bothSlots); Wear(bothSlots, ItemID.PapyrusScarab, ItemID.SquireShield);
			var te8 = Armory(null, Stock()); ArmoryTE.TestServed = new List<SoldierRecord> { bothSlots }; te8.IssueGear(bothSlots);
			check("with both extra slots he carries a staff and a sentry beside his blade", Staves(bothSlots) == 1 && Sentries(bothSlots) == 1 && bothSlots.Gear.SidearmCount == 3, Kit(bothSlots));
			var capped = ArmoryRules.Default(); capped.SidearmLimit = 1;
			var limited = new SoldierRecord { Id = 914 }; Armor(limited); Wear(limited, ItemID.PapyrusScarab, ItemID.SquireShield);
			var te9 = Armory(capped, Stock()); ArmoryTE.TestServed = new List<SoldierRecord> { limited }; te9.IssueGear(limited);
			check("a summon sidearm counts against the sidearm limit", limited.Gear.SidearmCount == 1, Kit(limited));
			// the slot goes away with the gear: the staff goes back to the armory on the next pass
			var lost = new SoldierRecord { Id = 915 }; Armor(lost); lost.Gear.Weapon = Make(ItemID.Musket); lost.Gear.ExtraSidearms[0] = Make(ItemID.SlimeStaff); lost.Gear.ExtraSidearms[1] = Make(ItemID.QueenSpiderStaff);
			var te10 = Armory(null, Make(ItemID.MusketBall, 300)); ArmoryTE.TestServed = new List<SoldierRecord> { lost }; te10.UpgradeWeapons(lost);
			check("a levy with no extra summon slot gives a summon sidearm back on the next pass", Summons(lost) == 0, Kit(lost));
			check("the returned staff and sentry are in the armory", te10.Storage.Count(i => i != null && !i.IsAir && (i.type == ItemID.SlimeStaff || i.type == ItemID.QueenSpiderStaff)) == 2);
			// a summoner is unchanged: no extra slot needed
			var summoner = new SoldierRecord { Id = 916 }; summoner.Gear.Weapon = Make(ItemID.HornetStaff); summoner.Gear.Head = Make(ItemID.SpiderMask); summoner.Gear.Body = Make(ItemID.SpiderBreastplate); summoner.Gear.Legs = Make(ItemID.SpiderGreaves);
			var te11 = Armory(null, Make(ItemID.QueenSpiderStaff)); ArmoryTE.TestServed = new List<SoldierRecord> { summoner }; te11.IssueGear(summoner);
			check("a summoner carries summon weapons whatever his gear gives", Summons(summoner) >= 1, Kit(summoner));

			var one = ArmoryRules.Default(); one.SidearmLimit = 1;
			var l1 = new SoldierRecord { Id = 902 }; var te1 = Armory(one, Stock()); Armor(l1); ArmoryTE.TestServed = new List<SoldierRecord> { l1 }; te1.IssueGear(l1);
			check("a limit of 1 gives one sidearm", l1.Gear.SidearmCount == 1, Kit(l1));

			var zero = ArmoryRules.Default(); zero.SidearmLimit = 0;
			var l0 = new SoldierRecord { Id = 903 }; var te2 = Armory(zero, Stock()); Armor(l0); ArmoryTE.TestServed = new List<SoldierRecord> { l0 }; te2.IssueGear(l0);
			check("a limit of 0 gives none", l0.Gear.SidearmCount == 0 && l0.Gear.HasWeapon, Kit(l0));

			// tightening the rules takes the excess back on the next pass; locked slots stay
			var over = new SoldierRecord { Id = 905 }; var te4 = Armory(null, Stock()); Armor(over); Wear(over, ItemID.PapyrusScarab); ArmoryTE.TestServed = new List<SoldierRecord> { over }; te4.IssueGear(over);
			int before = over.Gear.SidearmCount;
			var tight = ArmoryRules.Default(); tight.SidearmLimit = 1; te4.Rules.CopyFrom(tight);
			over.Gear.Accessories[0] = new Item(); over.Gear.SetLocked(SoldierGear.SlotAcc0, false); // the minion slot goes too
			te4.UpgradeWeapons(over);
			check("tightening the rules returns the excess sidearms to the armory", before == 2 && over.Gear.SidearmCount <= 1 && Summons(over) == 0, $"{before} -> {over.Gear.SidearmCount}: {Kit(over)}");
			int staves = te4.Storage.Count(i => i != null && !i.IsAir && (i.type == ItemID.SlimeStaff || i.type == ItemID.QueenSpiderStaff));
			check("the returned and the unissued staves are all in storage", staves == 2, staves.ToString());

			// a summoner is never limited
			var sumRules = ArmoryRules.Default(); sumRules.SidearmLimit = 0;
			var sm = new SoldierRecord { Id = 906 }; sm.Gear.Weapon = Make(ItemID.HornetStaff); sm.Gear.Head = Make(ItemID.SpiderMask); sm.Gear.Body = Make(ItemID.SpiderBreastplate); sm.Gear.Legs = Make(ItemID.SpiderGreaves);
			var te5 = Armory(sumRules, Make(ItemID.SlimeStaff), Make(ItemID.QueenSpiderStaff), Make(ItemID.ThornWhip)); ArmoryTE.TestServed = new List<SoldierRecord> { sm }; te5.UpgradeWeapons(sm); te5.IssueGear(sm);
			check("a summoner keeps his summon weapons whatever the limit", Summons(sm) >= 1, Kit(sm));
		}

		private static void Stripped(CheckFn check)
		{
			var te = Armory(null, Stock());
			var lv = new SoldierRecord { Id = 910 }; Armor(lv); ArmoryTE.TestServed = new List<SoldierRecord> { lv }; te.IssueGear(lv);
			bool armed = lv.Gear.HasWeapon;
			te.ReturnGear(lv.Gear); lv.Disarmed = true;
			check("a stripped levy's kit is back in the armory", armed && !lv.Gear.HasWeapon && te.Storage.Any(i => i != null && i.type == ItemID.Musket));
			te.IssueGear(lv); te.UpgradeWeapons(lv);
			check("the armory issues a stripped levy nothing", !lv.Gear.HasWeapon && lv.Gear.SidearmCount == 0 && lv.Gear.Head.IsAir);
			lv.Disarmed = false; te.IssueGear(lv);
			check("issuing a kit again arms him", lv.Gear.HasWeapon, Kit(lv));
			lv.Disarmed = true;
			var ms = new MemoryStream(); using (var w = new BinaryWriter(ms)) lv.WriteSnapshot(w);
			var read = SoldierRecord.ReadSnapshot(new BinaryReader(new MemoryStream(ms.ToArray())));
			var tag = SoldierRecord.Load(lv.Save());
			check("the stripped flag reaches clients and survives a save", read.Disarmed && tag.Disarmed);
		}

		private static void HandSet(CheckFn check)
		{
			var te = Armory(null, Make(ItemID.Musket));
			var ov = te.Rules.OverrideFor(ItemID.Musket, true); ov.Rank = RankOverrideMode.Prefer;
			te.Rules.Changed();
			var other = te.Rules.OverrideFor(ItemID.IronBroadsword, true); other.Rank = RankOverrideMode.Never;
			var listed = te.Rules.Overrides.Where(kv => kv.Value.Rank != RankOverrideMode.Auto).ToList();
			check("preferred and never items are listed from the rules, with no item in the armory", listed.Count == 2);
			ov.Rank = RankOverrideMode.Auto; other.Rank = RankOverrideMode.Auto; te.Rules.Prune(); te.Rules.Changed();
			check("clearing them from the list empties the overrides", te.Rules.Overrides.Count == 0 && te.Rules.IsNeutral);
		}

		// ------------------------------------------------------------------ the levy cap curve
		private void CapCurve(CheckFn check)
		{
			int start = LevyConstants.DefaultLeviesAtStart, pre = LevyConstants.DefaultLeviesPre, hm = LevyConstants.DefaultLeviesHm, post = LevyConstants.DefaultLeviesPost;
			// the vanilla bosses, in the order they fall, with their classic weights (the same table the progression system reads)
			(int tier, float w)[] bosses =
			{
				(0, LevyConstants.CpKingSlime), (0, LevyConstants.CpEyeOfCthulhu), (0, LevyConstants.CpEaterOrBrain), (0, LevyConstants.CpQueenBee), (0, LevyConstants.CpSkeletron), (0, LevyConstants.CpDeerclops), (0, LevyConstants.CpWallOfFlesh),
				(1, LevyConstants.CpQueenSlime), (1, LevyConstants.CpEachMech), (1, LevyConstants.CpEachMech), (1, LevyConstants.CpEachMech), (1, LevyConstants.CpPlantera), (1, LevyConstants.CpGolem), (1, LevyConstants.CpEmpress),
				(1, LevyConstants.CpFishron), (1, LevyConstants.CpCultist), (1, LevyConstants.CpMoonLord),
			};
			int At(int downCount, int s, int p, int h, int po, int cap)
			{
				var l = new List<TierBoss>(); for (int i = 0; i < bosses.Length; i++) l.Add(new TierBoss(bosses[i].tier, bosses[i].w, i < downCount));
				return LevyTierMath.Own(l, s, p, h, po, cap, 99);
			}
			string[] names = { "start", "King Slime + Eye", "Skeletron", "Wall of Flesh", "3 mechs + Queen Slime", "Plantera + Golem", "Moon Lord" };
			int[] counts = { 0, 2, 5, 7, 11, 13, 17 };
			var sb = new System.Text.StringBuilder();
			bool ok = true;
			for (int i = 0; i < counts.Length; i++)
			{
				int stage = counts[i] < 7 ? 0 : counts[i] < 17 ? 1 : 2;
				int o = At(counts[i], 10, 20, 40, 60, stage == 0 ? 20 : stage == 1 ? 40 : 60), n = At(counts[i], start, pre, hm, post, stage == 0 ? pre : stage == 1 ? hm : post);
				sb.Append(names[i]).Append(' ').Append(o).Append("->").Append(n).Append(i < counts.Length - 1 ? ", " : "");
				if (i > 0 && i < 4 && n < o) ok = false; // before hardmode the new curve is never lower
			}
			Mod.Logger.Info("[ArmoryUX] levy cap old->new at each stage: " + sb);
			check("the new curve gives more levies before hardmode and fewer at the end", ok && pre > 20 && post < 60 && start > 10 && start <= pre && pre <= hm && hm <= post);
			check("the old 10/20/40/60 curve is still reproducible from the config values", At(17, 10, 20, 40, 60, 60) == 40 && At(0, 10, 20, 40, 60, 20) == 10);
		}

	}
}
