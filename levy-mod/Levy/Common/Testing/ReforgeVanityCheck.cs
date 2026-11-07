using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Soldiers;
using Levy.Common.Vanity;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Boot-time self-check of targeted reforging and the vanity sets: the reforge plan's wire form and target selection in every mode, the prefix goal, the spending cap and the held-weapon write-back;
	/// the vanity list's save, load and wire round trips, target assignment, "one set dresses everyone", the social-slot render path and "vanity never changes a stat". Throwaway objects only, never throws;
	/// logs one summary line, or every failure. In the style of <see cref="ArmoryRulesCheck"/>.
	/// </summary>
	public class ReforgeVanityCheck : ModSystem
	{
		private static Item Make(int type) => ReforgeFixture.Make(type);

		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(PostSetupRecipesCore);

		private void PostSetupRecipesCore()
		{
			var failures = new List<string>();
			int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			void Guard(string name, Action body)
			{
				try { body(); }
				catch (Exception e) { failures.Add($"{name} threw {e.GetType().Name}: {e.Message}"); }
			}
			var rand = Main.rand;
			try
			{
				Guard("plan wire", () => CheckPlanWire(Check));
				Guard("target selection", () => CheckSelection(Check));
				Guard("prefix goal", () => CheckPrefixGoal(Check));
				Guard("spending cap", () => CheckCap(Check));
				Guard("preview and old reforge", () => CheckPreviewAndLegacy(Check));
				Guard("vanity round trips", () => CheckVanityRoundTrips(Check));
				Guard("vanity targets", () => CheckVanityTargets(Check));
				Guard("vanity render path", () => CheckVanityRender(Check));
			}
			finally { Main.rand = rand; ArmoryTE.TestServed = null; LevyClock.Force(null); }
			try
			{
				if (failures.Count == 0) Mod.Logger.Info($"Levy reforge and vanity self-check: {ok} checks OK.");
				else
				{
					Mod.Logger.Warn($"Levy reforge and vanity self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!");
					foreach (string f in failures) Mod.Logger.Warn("Levy reforge and vanity self-check FAILED: " + f);
				}
			}
			catch (Exception) { }
		}

		// ------------------------------------------------------------------ reforge

		private static void CheckPlanWire(Action<bool, string> check)
		{
			var p = new ReforgePlan { Mode = ReforgeMode.Picked, RecentDays = 7, ClassKey = "ranged", Category = "bow", Goal = ReforgeGoal.Prefix, FloorIndex = 2, PrefixId = 81, CapCopper = 123456 };
			p.TogglePick(0, 5); p.TogglePick(1, 2); p.TogglePick(0, 5); p.TogglePick(0, 9);
			var q = ReforgePlan.FromBytes(p.ToBytes());
			check(q.Mode == ReforgeMode.Picked && q.RecentDays == 7 && q.ClassKey == "ranged" && q.Category == "bow" && q.Goal == ReforgeGoal.Prefix && q.FloorIndex == 2 && q.PrefixId == 81 && q.CapCopper == 123456, "plan fields survive the wire");
			check(q.Picks.Count == 2 && q.IsPicked(1, 2) && q.IsPicked(0, 9) && !q.IsPicked(0, 5), "picks survive the wire and a second toggle un-picks");
			check(ReforgePlan.FromBytes(null).Mode == ReforgeMode.Issued && ReforgePlan.FromBytes(new byte[] { 1, 2, 3 }).CapCopper == 0, "garbage reads as the default plan");
			var wild = new ReforgePlan { Mode = ReforgeMode.Group, RecentDays = 999999, CapCopper = -5, FloorIndex = 99 };
			var w = ReforgePlan.FromBytes(wild.ToBytes());
			check(w.RecentDays == ReforgePlan.MaxRecentDays && w.CapCopper == 0 && w.FloorIndex == LevyConstants.UI.ReforgeFloorTopFraction.Length - 1, "out-of-range numbers are clamped on read");
		}

		private static string[] Names(IEnumerable<string> labels) => labels.Select(l => l.Substring(l.IndexOf(' ') + 1)).OrderBy(x => x).ToArray();
		private static bool Same(IEnumerable<string> got, params string[] want) => Names(got).SequenceEqual(want.OrderBy(x => x));

		private static void CheckSelection(Action<bool, string> check)
		{
			var f = ReforgeFixture.Build(1_000_000);
			try
			{
				var issued = f.Selected(new ReforgePlan { Mode = ReforgeMode.Issued });
				check(Same(issued, "Musket", "Silver Broadsword", "Topaz Staff", "Wooden Bow"), "Issued = the four weapons in levies' hands: " + string.Join(", ", issued));
				check(issued.All(s => s.StartsWith("hand#") || s.StartsWith("side#")), "Issued touches nothing in storage or on the rack");

				var recent1 = f.Selected(new ReforgePlan { Mode = ReforgeMode.Recent, RecentDays = 1 });
				check(Same(recent1, "Wooden Bow", "Iron Bow", "Minishark", "Copper Bow", "Wooden Bow"), "Recent 1 day = stamped within a day (the Adopted, unstamped and old ones stay out): " + string.Join(", ", recent1));
				var recent7 = f.Selected(new ReforgePlan { Mode = ReforgeMode.Recent, RecentDays = 7 });
				check(recent7.Count == 7 && Names(recent7).Contains("Musket") && !Names(recent7).Contains("Copper Broadsword"), "Recent 7 days adds the 2- and 3-day-old Muskets (7), not the 10-day-old sword: " + string.Join(", ", recent7));

				var picked = new ReforgePlan { Mode = ReforgeMode.Picked };
				picked.TogglePick(0, 2); picked.TogglePick(0, 6); picked.TogglePick(1, 1); picked.TogglePick(0, 8); picked.TogglePick(0, 9);
				var pk = f.Selected(picked);
				check(Same(pk, "Copper Broadsword", "Blade of Grass", "Iron Broadsword"), "Picked = exactly the picked weapons (a Torch and a favourited sword picked by mistake are skipped): " + string.Join(", ", pk));
				picked.ClassKey = "ranged";
				check(f.Selected(picked).Count == 3, "a hand pick ignores the class filter");

				var ranged = f.Selected(new ReforgePlan { Mode = ReforgeMode.Group, ClassKey = "ranged" });
				check(ranged.Count == 8 && Names(ranged).All(n => new[] { "Wooden Bow", "Musket", "Handgun", "Iron Bow", "Minishark", "Copper Bow" }.Contains(n)), "Group by class ranged = the 8 ranged weapons: " + string.Join(", ", ranged));
				var bows = f.Selected(new ReforgePlan { Mode = ReforgeMode.Group, Category = "bow" });
				check(Same(bows, "Wooden Bow", "Iron Bow", "Copper Bow", "Wooden Bow"), "Group by type bow = the four bows: " + string.Join(", ", bows));
				var rangedRecent = f.Selected(new ReforgePlan { Mode = ReforgeMode.Recent, RecentDays = 1, ClassKey = "ranged" });
				check(rangedRecent.Count == 5, "a class filter narrows Recent too (5 ranged weapons were added within a day)");
				check(f.Selected(new ReforgePlan { Mode = ReforgeMode.Recent, RecentDays = 7, ClassKey = "magic" }).Count == 0, "Recent + magic finds nothing recent that is magic");
				var all = f.Selected(new ReforgePlan { Mode = ReforgeMode.Group });
				check(all.Count == 14, "Group with no filter = every reforgeable weapon (14), never the Torch or the favourite: " + all.Count);
			}
			finally { f.Release(); }
		}

		private static void CheckPrefixGoal(Action<bool, string> check)
		{
			var f = ReforgeFixture.Build(10_000_000);
			try
			{
				var sample = Make(ItemID.CopperBroadsword);
				var valid = ArmoryTE.ValidPrefixes(sample);
				check(valid.Count > 5 && valid.All(id => sample.CanApplyPrefix(id)), "a broadsword has a list of valid prefixes, all of which it can take (" + valid.Count + ")");
				check(valid.Contains(PrefixID.Legendary) && !valid.Contains(PrefixID.Unreal), "Legendary is valid for a sword, Unreal (a ranged prefix) is not");
				var forGroup = f.Armory.ValidPrefixesFor(new ReforgePlan { Mode = ReforgeMode.Group, ClassKey = "melee" });
				check(forGroup.Count > 0 && forGroup.Any(p => p.id == PrefixID.Legendary && p.weapons == 4) && forGroup.All(p => p.id != PrefixID.Unreal), "the picker lists Legendary for all 4 melee weapons and no ranged-only prefix");

				ReforgeFixture.Seed(2024);
				var plan = new ReforgePlan { Mode = ReforgeMode.Group, ClassKey = "melee", Goal = ReforgeGoal.Prefix, PrefixId = PrefixID.Legendary };
				long before = f.Payer.Treasury;
				var res = f.Armory.ReforgeTargeted(f.Payer, plan);
				check(res.Selected == 4, "the run selected the 4 melee weapons (" + res.Selected + ")");
				check(res.Reached >= 1, "at least one sword reached Legendary within the roll cap (" + res.Reached + "/4)");
				int legendary = 0;
				foreach (var c in f.Armory.ReforgeCandidates()) if (c.Item.prefix == PrefixID.Legendary) legendary++;
				check(legendary == res.Reached, "weapons wearing Legendary = weapons reported as reached (" + legendary + " vs " + res.Reached + ")");
				check(before - f.Payer.Treasury == res.Spent && res.Spent > 0, "the treasury paid exactly what the report says");
				foreach (var c in f.Armory.ReforgeCandidates())
					if (c.Item.prefix != PrefixID.Legendary && c.Item.prefix != 0 && ArmoryTE.PrefixQuality(c.Item, Make(c.Item.type)) < 1.0) { check(false, c.Item.Name + " ended worse than unprefixed"); }

				// a prefix the weapons cannot take: nothing is rolled, nothing is paid
				var f2 = ReforgeFixture.Build(10_000_000);
				try
				{
					var bad = f2.Armory.ReforgeTargeted(f2.Payer, new ReforgePlan { Mode = ReforgeMode.Group, ClassKey = "melee", Goal = ReforgeGoal.Prefix, PrefixId = PrefixID.Unreal });
					check(bad.Selected == 4 && bad.NotApplicable == 4 && bad.Rolls == 0 && bad.Spent == 0 && f2.Payer.Treasury == 10_000_000, "a prefix no selected sword can take costs nothing and touches nothing");
					// already at the goal: a weapon wearing the named prefix is left alone
					var done = f2.Armory.ReforgeTargeted(f2.Payer, new ReforgePlan { Mode = ReforgeMode.Picked, Picks = { (0, 3) }, Goal = ReforgeGoal.Floor, FloorIndex = 3 });
					check(done.Selected == 1 && done.AlreadyMet == 1 && done.Spent == 0, "a weapon already at the best prefix is not rerolled");
				}
				finally { f2.Release(); }

				// the floor goal reaches the floor, on a held weapon, and writes it back to the levy
				var f3 = ReforgeFixture.Build(10_000_000);
				try
				{
					ReforgeFixture.Seed(77);
					var held = f3.Armory.ReforgeTargeted(f3.Payer, new ReforgePlan { Mode = ReforgeMode.Issued, Goal = ReforgeGoal.Floor, FloorIndex = 1 });
					check(held.Selected == 4 && held.Weapons == 4, "Issued + Great floor rerolled the four held weapons (" + held.Weapons + ")");
					int prefixed = 0;
					foreach (var s in f3.Souls) { if (s.Gear.Weapon.prefix > 0) prefixed++; if (s.Gear.Sidearm != null && s.Gear.Sidearm.prefix > 0) prefixed++; }
					check(prefixed >= 3, "the reforged weapons are in the levies' hands (" + prefixed + " of 4 carry a modifier)");
					check(f3.Armory.Storage.Take(8).All(it => it.IsAir || it.prefix == 0 || it.type == ItemID.Handgun), "an Issued run never touched storage");
				}
				finally { f3.Release(); }
			}
			finally { f.Release(); }
		}

		private static void CheckCap(Action<bool, string> check)
		{
			var plan = new ReforgePlan { Mode = ReforgeMode.Group, Goal = ReforgeGoal.Floor, FloorIndex = 3 };
			const long Treasury = 2_000_000_000L;
			var free = ReforgeFixture.Build(Treasury);
			long freeSpent, cap;
			try
			{
				long sum = 0; int n = 0;
				foreach (var c in free.Armory.ReforgeCandidates()) if (ArmoryTE.IsReforgeable(c.Item)) { sum += ArmoryTE.ReforgePrice(c.Item); n++; }
				cap = Math.Max(500, sum / Math.Max(1, n) * 12); // about twelve average rolls
				ReforgeFixture.Seed(5);
				var r = free.Armory.ReforgeTargeted(free.Payer, plan);
				freeSpent = r.Spent;
				check(r.Spent > cap * 3 && !r.CapHit, $"without a cap the best-floor run over the whole hoard spends far more than the cap used below ({r.Spent} vs {cap} copper)");
			}
			finally { free.Release(); }

			var f = ReforgeFixture.Build(Treasury);
			try
			{
				ReforgeFixture.Seed(5);
				plan.CapCopper = cap;
				var res = f.Armory.ReforgeTargeted(f.Payer, plan);
				check(res.Spent <= cap, $"the capped run never spends past the cap ({res.Spent} of {cap})");
				check(Treasury - f.Payer.Treasury == res.Spent, "the treasury paid exactly what was spent");
				check(res.CapHit && res.Short > 0, "the cap stopped the run with weapons left short");
				check(res.Rolls > 0 && res.Spent > cap / 2, $"the run uses most of the cap before it stops ({res.Rolls} rolls, {res.Spent} of {cap} copper)");
				check(res.Spent < freeSpent, $"the capped run spent less than the uncapped one ({res.Spent} < {freeSpent})");
			}
			finally { f.Release(); }

			var broke = ReforgeFixture.Build(300);
			try
			{
				ReforgeFixture.Seed(6);
				var res = broke.Armory.ReforgeTargeted(broke.Payer, new ReforgePlan { Mode = ReforgeMode.Group, Goal = ReforgeGoal.Floor, FloorIndex = 3, CapCopper = 1_000_000_000L });
				check(res.Spent <= 300 && res.RanDry && !res.CapHit && broke.Payer.Treasury >= 0, $"a treasury smaller than the cap runs dry first and never goes negative ({res.Spent} spent)");
			}
			finally { broke.Release(); }
		}

		private static void CheckPreviewAndLegacy(Action<bool, string> check)
		{
			var f = ReforgeFixture.Build(2_000_000_000L);
			try
			{
				var plan = new ReforgePlan { Mode = ReforgeMode.Group, Goal = ReforgeGoal.Floor, FloorIndex = 1 };
				var pv = f.Armory.PreviewReforge(plan, f.Payer);
				check(pv.Selected == 14 && pv.AlreadyMet == 1 && pv.Weapons == 13 && pv.EstCost > 0, $"preview of the whole hoard: 14 selected, the Handgun already there, 13 to roll, a cost above 0 ({pv.Selected}/{pv.AlreadyMet}/{pv.Weapons}/{pv.EstCost})");
				plan.CapCopper = 100;
				var capped = f.Armory.PreviewReforge(plan, f.Payer);
				check(capped.EstCost == 100 && capped.CapApplied, "the preview cost is cut to the cap");
				plan.CapCopper = 0;
				ReforgeFixture.Seed(9);
				var res = f.Armory.ReforgeTargeted(f.Payer, plan);
				check(res.Weapons == pv.Weapons && res.Selected == pv.Selected, "the run touched exactly the weapons the preview named");
				check(res.Spent > 0 && res.Spent < pv.EstCost * 6 && res.Spent * 6 > pv.EstCost, $"the real cost is within a factor of six of the estimate (est {pv.EstCost}, spent {res.Spent})");
			}
			finally { f.Release(); }

			var g = ReforgeFixture.Build(2_000_000_000L);
			try
			{
				ReforgeFixture.Seed(3);
				var res = g.Armory.ReforgeToFloor(g.Payer, 1);
				check(res.Weapons > 0 && res.Spent > 0, "the old reforge-to-floor still runs");
				check(g.Souls.All(s => s.Gear.Weapon.prefix == 0), "and still leaves levies' hands alone");
			}
			finally { g.Release(); }
		}

		// ------------------------------------------------------------------ vanity

		private static ArmoryVanity SampleVanity()
		{
			var v = new ArmoryVanity();
			var a = v.AddSet(); a.Target = VanityTargetKind.All; a.Items[VanitySet.Head] = Make(ItemID.FamiliarWig); a.Items[VanitySet.DyeHead] = Make(ItemID.RedDye);
			var b = v.AddSet(); b.Target = VanityTargetKind.Class; b.ClassKey = "ranged"; b.Items[VanitySet.Body] = Make(ItemID.FamiliarShirt); b.Items[VanitySet.Legs] = Make(ItemID.FamiliarPants);
			var c = v.AddSet(); c.Target = VanityTargetKind.Levies; c.Levies.AddRange(new[] { 7, 9 }); c.Items[VanitySet.Head] = Make(ItemID.TopHat); c.On = true;
			return v;
		}

		/// <summary>Null when the two lists agree, else what differs first.</summary>
		private static string VanityDiff(ArmoryVanity x, ArmoryVanity y)
		{
			if (x.Sets.Count != y.Sets.Count) return $"set count {x.Sets.Count} vs {y.Sets.Count}";
			for (int i = 0; i < x.Sets.Count; i++)
			{
				var p = x.Sets[i]; var q = y.Sets[i];
				if (p.On != q.On) return $"set {i} on {p.On} vs {q.On}";
				if (p.Target != q.Target) return $"set {i} target {p.Target} vs {q.Target}";
				if (p.ClassKey != q.ClassKey) return $"set {i} class {p.ClassKey} vs {q.ClassKey}";
				if (!p.Levies.SequenceEqual(q.Levies)) return $"set {i} levies [{string.Join(",", p.Levies)}] vs [{string.Join(",", q.Levies)}]";
				for (int k = 0; k < VanitySet.Slots; k++) if (p.Items[k].type != q.Items[k].type) return $"set {i} slot {k} item {p.Items[k].type} vs {q.Items[k].type}";
			}
			return null;
		}

		private static bool SameVanity(ArmoryVanity x, ArmoryVanity y) => VanityDiff(x, y) == null;

		private static void CheckVanityRoundTrips(Action<bool, string> check)
		{
			var v = SampleVanity();
			check(SameVanity(v, ArmoryVanity.FromBytes(v.ToBytes())), "the vanity list survives the wire form: " + VanityDiff(v, ArmoryVanity.FromBytes(v.ToBytes())));

			var te = new ArmoryTE(); te.Vanity = SampleVanity(); te.Vanity.Rev = 4;
			var tag = new TagCompound();
			te.SaveData(tag);
			using (var ms = new MemoryStream()) { TagIO.ToStream(tag, ms); ms.Position = 0; tag = TagIO.FromStream(ms); }
			var back = new ArmoryTE(); back.LoadData(tag);
			check(SameVanity(te.Vanity, back.Vanity), "the vanity list survives a save and load of the armory (through the tag stream): " + VanityDiff(te.Vanity, back.Vanity));
			check(new ArmoryTE().SaveDataKeys().All(k => k != "vanityV1"), "an armory with no vanity writes nothing for it");

			var net = new ArmoryTE(); net.Vanity = SampleVanity(); net.Vanity.Rev = 6; net.Rules.Rev = 3;
			using (var ms = new MemoryStream())
			{
				using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) net.NetSend(w);
				ms.Position = 0;
				var peer = new ArmoryTE();
				using var r = new BinaryReader(ms);
				peer.NetReceive(r);
				check(SameVanity(net.Vanity, peer.Vanity) && peer.Vanity.Rev == 6, "the vanity list rides the armory's tile entity sync, with its revision");
				check(ms.Position == ms.Length, "the sync read consumed the whole payload (vanity is last on the wire)");
			}
			var held = new ArmoryTE(); held.Vanity = SampleVanity(); held.VanityHeldBack = true;
			var empty = new ArmoryTE();
			using (var ms = new MemoryStream())
			{
				using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) empty.NetSend(w);
				ms.Position = 0;
				using var r = new BinaryReader(ms);
				held.NetReceive(r);
			}
			check(held.Vanity.Sets.Count == 3, "a client mid-edit does not lose its vanity to a sync that arrives meanwhile");

			// the server's set path: clamp, bump the revision
			var server = new ArmoryTE(); server.Vanity.Rev = 2;
			var incoming = ArmoryVanity.FromBytes(SampleVanity().ToBytes());
			server.SetVanity(incoming);
			check(server.Vanity.Rev == 3 && SameVanity(server.Vanity, SampleVanity()), "the server applies a received list and bumps the revision");

			// hostile input
			var evil = new ArmoryVanity(); var s = evil.AddSet(); s.Items[VanitySet.Head] = Make(ItemID.CopperBroadsword); s.Items[VanitySet.Body] = Make(ItemID.FamiliarShirt); s.Items[VanitySet.Body].stack = 50; s.Items[VanitySet.DyeHead] = Make(ItemID.Torch);
			var cleaned = ArmoryVanity.FromBytes(evil.ToBytes()).Sets[0];
			check(cleaned.Items[VanitySet.Head].IsAir && cleaned.Items[VanitySet.Body].type == ItemID.FamiliarShirt && cleaned.Items[VanitySet.Body].stack == 1, "a sword in the head slot is dropped on read, and a stack of 50 shirts becomes one");
			if (!Main.dedServ) check(cleaned.Items[VanitySet.DyeHead].IsAir, "a torch in a dye slot is dropped on read (checked on clients only: a dedicated server cannot tell a dye)");
			check(ArmoryVanity.FromBytes(new byte[] { 1, 99, 0, 1 }).Sets.Count == 0 && ArmoryVanity.FromBytes(null).Sets.Count == 0, "garbage reads as an empty list");
			var many = new ArmoryVanity(); for (int i = 0; i < 40; i++) { var m = new VanitySet(); many.Sets.Add(m); }
			check(ArmoryVanity.FromBytes(many.ToBytes()).Sets.Count == ArmoryVanity.MaxSets, "a list longer than the row limit is cut at the limit");
		}

		private static void CheckVanityTargets(Action<bool, string> check)
		{
			var v = SampleVanity();
			var seven = v.Resolve(7, "ranged");
			check(seven.Head.type == ItemID.TopHat && seven.Body.type == ItemID.FamiliarShirt && seven.Legs.type == ItemID.FamiliarPants, "a named ranged levy: his own hat beats everyone's wig; class body and legs still apply");
			check(seven.Items[VanitySet.DyeHead].type == ItemID.RedDye, "the dye of the 'everyone' row still reaches a slot the named row left without a dye");
			var eight = v.Resolve(8, "melee");
			check(eight.Head.type == ItemID.FamiliarWig && eight.Body == null && eight.Legs == null, "an unnamed melee levy gets only the 'everyone' wig");
			var nine = v.Resolve(9, "ranged");
			check(nine.Head.type == ItemID.TopHat, "every levy named in a row wears it (9)");
			var none = v.Resolve(5, null);
			check(none.Head.type == ItemID.FamiliarWig && none.Body == null, "a levy with no class is dressed by the 'everyone' row only");

			v.Sets[0].On = false;
			check(v.Resolve(8, "melee").Head == null, "turning a row off takes it off its levies");
			v.Sets[0].On = true;
			var top = v.AddSet(); top.Items[VanitySet.Head] = Make(ItemID.TopHat);
			check(v.Resolve(8, "melee").Head.type == ItemID.FamiliarWig, "of two 'everyone' rows the upper one wins a slot");
			v.Move(v.Sets.Count - 1, -(v.Sets.Count - 1));
			check(v.Resolve(8, "melee").Head.type == ItemID.TopHat, "moving a row up gives it the slot");

			var shared = SampleVanity();
			var wig = shared.Sets[0].Items[VanitySet.Head];
			bool same = true;
			for (int id = 100; id < 200; id++) same &= ReferenceEquals(shared.Resolve(id, "melee").Head, wig);
			check(same && wig.stack == 1 && !wig.IsAir, "one stored set dresses a hundred levies and is not consumed");

			// class of a levy comes from his weapon under the armory's rules
			var armory = new ArmoryTE();
			var rec = new SoldierRecord { Id = 3 }; rec.Gear.Weapon = Make(ItemID.WoodenBow);
			check(VanityLookup.ClassKeyOf(armory, rec) == "ranged", "a levy with a bow is of the ranged class");
			rec.Gear.Weapon = Make(ItemID.CopperBroadsword);
			check(VanityLookup.ClassKeyOf(armory, rec) == "melee", "a levy with a sword is of the melee class");
			check(VanityLookup.ClassKeyOf(armory, new SoldierRecord()) == null, "a bare-handed levy has no class");
		}

		private static void CheckVanityRender(Action<bool, string> check)
		{
			var look = SampleVanity().Resolve(7, "ranged");
			var blank = new Item();
			var p = new Player();
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = blank;
			for (int i = 0; i < p.dye.Length; i++) p.dye[i] = blank;
			var ironHead = Make(ItemID.IronHelmet); var ironBody = Make(ItemID.IronChainmail); var ironLegs = Make(ItemID.IronGreaves);
			p.armor[0] = ironHead; p.armor[1] = ironBody; p.armor[2] = ironLegs;
			p.PlayerFrame();
			check(p.head == ironHead.headSlot && p.body == ironBody.bodySlot && p.legs == ironLegs.legSlot, "without vanity the frame shows the real armor");

			VanityDress.ApplySlots(p, look, blank);
			p.PlayerFrame();
			check(p.head == look.Head.headSlot && p.body == look.Body.bodySlot && p.legs == look.Legs.legSlot, "with vanity in the social slots the frame shows the vanity (head/body/legs ids switch)");
			check(ReferenceEquals(p.armor[0], ironHead) && ReferenceEquals(p.armor[1], ironBody) && ReferenceEquals(p.armor[2], ironLegs), "the real armor slots are never touched");

			VanityDress.ApplyDyes(p, look, blank);
			check(ReferenceEquals(p.dye[0], look.Items[VanitySet.DyeHead]) && p.dye[1].IsAir && p.dye[2].IsAir, "the head dye slot holds the row's dye; a vanity piece with no dye in its row clears the armor's dye (body, legs)");
			p.UpdateDyes();
			check(p.cHead == p.dye[0].dye && p.cBody == 0 && p.cLegs == 0, "UpdateDyes reads the dye slots (head dye id " + p.cHead + (Main.dedServ ? "; a dedicated server has no shader table, so dye ids are 0 here" : "") + ")");

			VanityDress.ClearSlots(p, blank);
			p.PlayerFrame();
			check(p.head == ironHead.headSlot && p.body == ironBody.bodySlot, "clearing the social slots brings the real armor back");

			// stats: the same armor with and without vanity gives the same numbers
			float[] Stats(Player pl)
			{
				pl.ResetEffects();
				pl.UpdateEquips(0);
				return new float[] { pl.statDefense, pl.GetDamage(DamageClass.Generic).Additive, pl.GetCritChance(DamageClass.Generic), pl.moveSpeed, pl.maxRunSpeed, pl.statLifeMax2, pl.endurance, pl.GetAttackSpeed(DamageClass.Generic) };
			}
			var q = new Player();
			for (int i = 0; i < q.armor.Length; i++) q.armor[i] = blank;
			q.armor[0] = Make(ItemID.TitaniumHelmet); q.armor[1] = Make(ItemID.TitaniumBreastplate); q.armor[2] = Make(ItemID.TitaniumLeggings);
			var bare = Stats(q);
			var glam = SampleVanity(); glam.Sets[0].Items[VanitySet.Head] = Make(ItemID.SolarFlareHelmet); glam.Sets[1].Items[VanitySet.Body] = Make(ItemID.SolarFlareBreastplate); glam.Sets[1].Items[VanitySet.Legs] = Make(ItemID.SolarFlareLeggings);
			VanityDress.ApplySlots(q, glam.Resolve(1, "ranged"), blank);
			var dressed = Stats(q);
			check(bare.SequenceEqual(dressed), "dressing a levy in Solar Flare vanity changes no stat: defense " + bare[0] + " vs " + dressed[0] + ", damage " + bare[1] + " vs " + dressed[1]);
		}
	}

	internal static class ArmoryTeTestExt
	{
		/// <summary>The keys SaveData writes (test helper).</summary>
		public static IEnumerable<string> SaveDataKeys(this ArmoryTE te) { var t = new TagCompound(); te.SaveData(t); return t.Select(kv => kv.Key).ToList(); }
	}
}
