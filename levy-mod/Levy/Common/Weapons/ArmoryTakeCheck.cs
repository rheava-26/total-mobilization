using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Content.TileEntities;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Boot self-check of TAKING A WEAPON A LEVY IS HOLDING from the armory screen. Logs "Levy armory-take self-check: pass=N fail=M". It takes a held weapon (a prefixed one) with the armory's own action and asserts the player has
	/// that one item with its prefix and stack, the levy no longer holds it, the levy fell back to the armory's next pick (or to nothing when the racks hold nothing he can use), every item in the armory, the levies' kits and the player's
	/// pack is still there exactly once (a census before and after is identical), a repeated click or a wrong type moves nothing, and the in-use list a multiplayer client receives carries the item, its prefix, the holder and the slot.
	/// </summary>
	public class ArmoryTakeCheck : ModSystem
	{
		private static Item Make(int type, int stack = 1, int prefix = 0) { var it = new Item(); it.SetDefaults(type); it.stack = stack; if (prefix > 0) it.Prefix(prefix); return it; }
		private static string N(Item i) => i == null || i.IsAir ? "-" : (i.prefix > 0 ? "p" + i.prefix + " " : "") + i.Name + (i.stack > 1 ? "x" + i.stack : "");

		/// <summary>Every item in the armory's racks, the levies' kits and the player's pack, by (type, prefix) with the stacks added.</summary>
		private static Dictionary<(int, int), int> Census(ArmoryTE te, IEnumerable<SoldierRecord> levies, Player p)
		{
			var d = new Dictionary<(int, int), int>();
			void Add(Item it) { if (it == null || it.IsAir || it.stack <= 0) return; d[(it.type, it.prefix)] = d.TryGetValue((it.type, it.prefix), out int n) ? n + it.stack : it.stack; }
			foreach (var arr in new[] { te.Storage, te.IssueWeapons, te.IssueArmor, te.IssueAmmo, te.IssuePotions }) foreach (var it in arr) Add(it);
			foreach (var s in levies) foreach (var it in s.Gear.AllItems()) Add(it);
			foreach (var it in p.inventory) Add(it);
			return d;
		}

		private static bool Same(Dictionary<(int, int), int> a, Dictionary<(int, int), int> b) => a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out int n) && n == kv.Value);
		private static int Have(Player p, int type, int prefix) => p.inventory.Where(i => i != null && !i.IsAir && i.type == type && i.prefix == prefix).Sum(i => i.stack);

		public override void PostSetupRecipes()
		{
			var failures = new List<string>(); int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			void Guarded(string name, Action body) { try { body(); } catch (Exception e) { failures.Add(name + " threw " + e.GetType().Name + ": " + e.Message + " @ " + e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()); } }

			Guarded("take a held weapon", () =>
			{
				var te = new ArmoryTE(); te.Storage[0] = Make(ItemID.Minishark); te.Storage[1] = Make(ItemID.MusketBall, 300);
				var held = Make(ItemID.Musket, 1, PrefixID.Unreal);
				var s = new SoldierRecord { Id = 700 }; s.Gear.Weapon = held; s.Gear.SetLocked(SoldierGear.SlotWeapon, true);
				var other = new SoldierRecord { Id = 701 }; other.Gear.Weapon = Make(ItemID.IronBroadsword);
				var list = new List<SoldierRecord> { s, other };
				var player = new Player();
				ArmoryTE.TestServed = list;
				try
				{
					Check(held.prefix == PrefixID.Unreal, "the test weapon carries the Unreal prefix");
					var before = Census(te, list, player);
					var res = te.TakeHeld(700, SoldierGear.SlotWeapon, ItemID.Musket, out Item taken, out Item fallback);
					Check(res == TakeHeldResult.Ok && taken != null && taken.type == ItemID.Musket && taken.prefix == PrefixID.Unreal && taken.stack == 1, "the take succeeds and returns the one prefixed Musket (" + res + ", " + N(taken) + ")");
					ArmoryTE.GiveToPlayer(player, taken, direct: true);
					Check(Have(player, ItemID.Musket, PrefixID.Unreal) == 1, "the player has the Musket, prefix kept (" + Have(player, ItemID.Musket, PrefixID.Unreal) + ")");
					Check(!s.Gear.AllItems().Any(i => i.type == ItemID.Musket) && !ReferenceEquals(s.Gear.Weapon, held), "the levy no longer has it");
					Check(s.Gear.HasWeapon && s.Gear.Weapon.type == ItemID.Minishark && fallback != null && fallback.type == ItemID.Minishark, "the levy fell back to the armory's next pick, the Minishark (" + N(s.Gear.Weapon) + ")");
					Check(!s.Gear.IsLocked(SoldierGear.SlotWeapon), "the hand assignment went with the item");
					Check(Same(before, Census(te, list, player)), "item count is conserved: the racks, the kits and the pack hold exactly what they held before");
					Check(other.Gear.HasWeapon && other.Gear.Weapon.type == ItemID.IronBroadsword, "another levy's weapon is untouched");
					var again = te.TakeHeld(700, SoldierGear.SlotWeapon, ItemID.Musket, out Item t2, out _);
					Check(again == TakeHeldResult.SlotChanged && t2 == null && Have(player, ItemID.Musket, PrefixID.Unreal) == 1 && Same(before, Census(te, list, player)), "a second click on the same cell moves nothing (" + again + ")");
					var wrong = te.TakeHeld(701, SoldierGear.SlotWeapon, ItemID.Musket, out Item t3, out _);
					Check(wrong == TakeHeldResult.SlotChanged && t3 == null && other.Gear.HasWeapon, "a click that names the wrong item moves nothing (" + wrong + ")");
					var gone = te.TakeHeld(999, SoldierGear.SlotWeapon, ItemID.Musket, out Item t4, out _);
					Check(gone == TakeHeldResult.NotServed && t4 == null, "a levy this armory does not serve is refused (" + gone + ")");
				}
				finally { ArmoryTE.TestServed = null; }
			});

			Guarded("fall back to nothing", () =>
			{
				var te = new ArmoryTE();
				var s = new SoldierRecord { Id = 710 }; s.Gear.Weapon = Make(ItemID.Musket, 1, PrefixID.Unreal);
				var list = new List<SoldierRecord> { s }; var player = new Player();
				ArmoryTE.TestServed = list;
				try
				{
					var before = Census(te, list, player);
					var res = te.TakeHeld(710, SoldierGear.SlotWeapon, ItemID.Musket, out Item taken, out Item fallback);
					ArmoryTE.GiveToPlayer(player, taken, direct: true);
					Check(res == TakeHeldResult.Ok && !s.Gear.HasWeapon && (fallback == null || fallback.IsAir), "with nothing in the racks the levy is left with nothing (" + N(s.Gear.Weapon) + ")");
					Check(Have(player, ItemID.Musket, PrefixID.Unreal) == 1 && Same(before, Census(te, list, player)), "the player has the one Musket and the count is conserved");
				}
				finally { ArmoryTE.TestServed = null; }
			});

			Guarded("take a stack", () =>
			{
				var te = new ArmoryTE();
				var s = new SoldierRecord { Id = 720 }; s.Gear.Weapon = Make(ItemID.Musket); s.Gear.Ammo = Make(ItemID.MusketBall, 200);
				var list = new List<SoldierRecord> { s }; var player = new Player();
				player.inventory[0] = Make(ItemID.MusketBall, 700); // a stack already in the pack: the 200 stacks onto it, none lost
				ArmoryTE.TestServed = list;
				try
				{
					var before = Census(te, list, player);
					var res = te.TakeHeld(720, SoldierGear.SlotAmmo, ItemID.MusketBall, out Item taken, out _);
					Check(res == TakeHeldResult.Ok && taken != null && taken.stack == 200, "the whole carried stack is taken (" + N(taken) + ")");
					ArmoryTE.GiveToPlayer(player, taken, direct: true);
					Check(Have(player, ItemID.MusketBall, 0) == 900 && Same(before, Census(te, list, player)), "the stack lands in the pack and the count is conserved (" + Have(player, ItemID.MusketBall, 0) + ")");
				}
				finally { ArmoryTE.TestServed = null; }
			});

			Guarded("in-use list over the wire", () =>
			{
				var te = new ArmoryTE();
				var s = new SoldierRecord { Id = 730, Name = "Tester" }; s.Gear.Weapon = Make(ItemID.Musket, 1, PrefixID.Unreal);
				ArmoryTE.TestServed = new List<SoldierRecord> { s };
				try
				{
					using var ms = new MemoryStream(); using var w = new BinaryWriter(ms);
					te.WriteInUse(w); w.Flush(); ms.Position = 0;
					using var r = new BinaryReader(ms);
					r.ReadInt32(); // the armory id the packet handler reads first
					var client = new ArmoryTE(); client.ReadInUse(r);
					ArmoryTE.TestServed = null;
					var seen = typeof(ArmoryTE).GetField("_inUse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(client) as List<InUseEntry>;
					var e = seen != null && seen.Count > 0 ? seen.FirstOrDefault(x => x.Item.type == ItemID.Musket) : default;
					Check(seen != null && e.Item != null && e.Item.prefix == PrefixID.Unreal && e.Takeable && e.SoldierId == 730 && e.Slot == SoldierGear.SlotWeapon && e.Holder == "Tester",
						"a client's list carries the item with its prefix, the holder and the slot (" + (seen == null ? "no list" : seen.Count + " entries") + ")");
				}
				finally { ArmoryTE.TestServed = null; }
			});

			if (failures.Count == 0) Mod.Logger.Info($"Levy armory-take self-check: pass={ok} fail=0");
			else { Mod.Logger.Warn($"Levy armory-take self-check: pass={ok} fail={failures.Count} FAILURE(S) !!!"); foreach (var f in failures) Mod.Logger.Warn("Levy armory-take self-check FAILURE: " + f); }
		}
	}
}
