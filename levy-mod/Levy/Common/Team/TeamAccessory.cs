using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Team
{
	/// <summary>
	/// TEAM ACCESSORIES in the armory. An accessory whose effect reaches every ally (Paladin's Shield family, Trinket of Chi, Affliction, Rampart of Deities) is worth carrying
	/// for ONE wearer: a second wearer adds nothing (Trinket of Chi's own regen test is `trinketOfChi || chiRegen`, one boost either way). So the armory adds a large score bonus
	/// to such an item while nobody in the army (any levy, or the commander) wears its effect, and scores it normally once someone does.
	/// <para/>Detector: equip the item on a scratch Player, run the vanilla equip passes and read which <see cref="TeamItems.Auras"/> flags rose. That covers combined accessories and any
	/// modded item that raises a registered flag. A short curated list backs it up when the probe cannot run (the registry not built, a scratch-player failure).
	/// </summary>
	public static class TeamAccessory
	{
		/// <summary>Score added while the effect is unclaimed (an accessory tier is worth 1000), enough to beat any normal pick.</summary>
		public const float UnclaimedBonus = 6000f;

		private static readonly Dictionary<int, string[]> _cache = new();
		private static Player _scratch;
		private static bool _broken;
		private static readonly string[] _none = Array.Empty<string>();

		/// <summary>The effect ids (see <see cref="TeamItems.Auras"/>) this accessory raises for its wearer; empty for a normal accessory.</summary>
		public static string[] KeysOf(Item it)
		{
			if (it == null || it.IsAir || !it.accessory || it.vanity) return _none;
			if (_cache.TryGetValue(it.type, out var c)) return c;
			string[] keys = _none;
			try { keys = Measure(it.type); }
			catch (Exception e) { if (!_broken) { _broken = true; Levy.Instance?.Logger.Warn("Levy: team accessory probe failed, using the curated list only: " + e.Message); } }
			if (keys.Length == 0) keys = Curated(it);
			if (TeamItems.Built) _cache[it.type] = keys; // never cache before the registry exists
			return keys;
		}

		public static bool IsTeam(Item it) => KeysOf(it).Length > 0;

		private static string[] Curated(Item it)
		{
			if (it.type == ItemID.PaladinsShield || it.type == ItemID.FrozenShield || it.type == ItemID.HeroShield) return new[] { "paladin" };
			if (it.ModItem?.Mod?.Name == "CalamityMod")
				switch (it.ModItem.Name)
				{
					case "TrinketofChi": return new[] { "chi" };
					case "Affliction": return new[] { "affliction" };
					case "RampartofDeities": return new[] { "paladin" };
				}
			return _none;
		}

		private static string[] Measure(int type)
		{
			if (!TeamItems.Built || TeamItems.Auras.Count == 0) return _none;
			if (_scratch == null)
			{
				_scratch = new Player();
				_scratch.whoAmI = SoldierNPC.DummySlot;
				_scratch.active = true; _scratch.dead = false; _scratch.ghost = false;
				_scratch.selectedItem = 0; _scratch.team = 0;
			}
			var p = _scratch;
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			for (int i = 0; i < p.dye.Length; i++) p.dye[i] = new Item();
			p.inventory[0] = new Item();
			p.armor[3].SetDefaults(type);
			p.statLife = p.statLifeMax2 = p.statLifeMax = 500; // Paladin's Shield only raises its flag above a quarter life
			p.ResetEffects();
			p.UpdateEquips(SoldierNPC.DummySlot);
			try { PlayerLoader.PostUpdateEquips(p); } catch (Exception) { }
			List<string> keys = null;
			foreach (var a in TeamItems.Auras)
			{
				bool on = false;
				try { on = a.Flag(p); } catch (Exception) { }
				if (on) (keys ??= new List<string>()).Add(a.Id);
			}
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			p.ResetEffects();
			return keys?.ToArray() ?? _none;
		}

		/// <summary>Effects already worn by <paramref name="army"/> (levies other than <paramref name="self"/>) or by any active player (the commander), locked or not.</summary>
		public static HashSet<string> Claimed(IEnumerable<SoldierRecord> army, SoldierRecord self)
		{
			var set = new HashSet<string>();
			if (army != null)
				foreach (var r in army)
				{
					if (r == null || ReferenceEquals(r, self) || r.Gear == null) continue;
					int n = Math.Min(r.AccessorySlots, r.Gear.Accessories.Length);
					for (int i = 0; i < n; i++) foreach (var k in KeysOf(r.Gear.Accessories[i])) set.Add(k);
				}
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				var pl = Main.player[i];
				if (pl == null || !pl.active || i == SoldierNPC.DummySlot) continue;
				for (int s = 3; s < 10; s++) foreach (var k in KeysOf(pl.armor[s])) set.Add(k);
			}
			return set;
		}

		/// <summary>The score bonus for <paramref name="it"/>: large when it raises a team effect nobody in <paramref name="claimed"/> has, else 0.</summary>
		public static float Bonus(Item it, HashSet<string> claimed)
		{
			var keys = KeysOf(it);
			if (keys.Length == 0) return 0f;
			foreach (var k in keys) if (claimed != null && claimed.Contains(k)) return 0f;
			return UnclaimedBonus;
		}

		public static void Reset() { _cache.Clear(); _scratch = null; _broken = false; }

		/// <summary>Boot self-check: the first levy takes the Trinket of Chi (or the vanilla Paladin's Shield), the second does not while a better normal accessory is stocked.</summary>
		public static void SelfCheck(Mod mod)
		{
			if (!Main.dedServ) return;
			int pass = 0, fail = 0;
			void Check(string name, bool ok, string detail = "") { if (ok) pass++; else fail++; mod.Logger.Info($"[TeamAccCheck] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}"); }
			var savedServed = ArmoryTE.TestServed;
			try
			{
				Item Make(int t) { var i = new Item(); i.SetDefaults(t); return i; }
				bool cal = ModContent.TryFind("CalamityMod/TrinketofChi", out ModItem chiMi);
				int teamType = cal ? chiMi.Type : ItemID.PaladinsShield;
				string teamName = cal ? "Trinket of Chi" : "Paladin's Shield";
				Check("detector flags " + teamName, IsTeam(Make(teamType)), string.Join(",", KeysOf(Make(teamType))));
				Check("detector flags the vanilla shield family", IsTeam(Make(ItemID.PaladinsShield)) && IsTeam(Make(ItemID.FrozenShield)) && IsTeam(Make(ItemID.HeroShield)));
				Check("detector leaves Band of Regeneration alone", !IsTeam(Make(ItemID.BandofRegeneration)));
				if (cal && ModContent.TryFind("CalamityMod/Affliction", out ModItem aff)) Check("detector flags Affliction", IsTeam(Make(aff.Type)));
				if (cal && ModContent.TryFind("CalamityMod/RampartofDeities", out ModItem ram)) Check("detector flags Rampart of Deities", IsTeam(Make(ram.Type)));

				var r1 = new SoldierRecord { Id = 11, Grade = Grade.Levy };
				var r2 = new SoldierRecord { Id = 12, Grade = Grade.Levy };
				ArmoryTE.TestServed = new List<SoldierRecord> { r1, r2 };
				var te = new ArmoryTE();
				te.Deposit(Make(teamType)); te.Deposit(Make(ItemID.ObsidianShield)); te.Deposit(Make(ItemID.AnkhShield));
				var first = te.TakeAccessory(r1);
				Check("first levy takes " + teamName + " over stronger normal accessories", first != null && first.type == teamType, first?.Name ?? "null");
				if (first != null) r1.Gear.Accessories[0] = first;
				te.Deposit(Make(teamType));
				var second = te.TakeAccessory(r2);
				Check("second levy does not take a second " + teamName, second != null && second.type != teamType, second?.Name ?? "null");
			}
			catch (Exception e) { Check("self-check ran", false, e.GetType().Name + ": " + e.Message); }
			finally { ArmoryTE.TestServed = savedServed; }
			mod.Logger.Info($"[TeamAccCheck] DONE pass={pass} fail={fail}");
		}
	}
}
