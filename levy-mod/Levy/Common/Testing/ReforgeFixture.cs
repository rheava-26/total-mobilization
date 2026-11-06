using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// A detached armory with a known hoard, for the reforge self-check and probe (TEST-ONLY). Storage holds weapons deposited at known ages, the rack two more, and five levies carry four primaries and a sidearm.
	/// The world clock is pinned to <see cref="Now"/> while a fixture is alive (call <see cref="Release"/> when done).
	/// </summary>
	internal sealed class ReforgeFixture
	{
		public const long Now = 100 * LevyClock.TicksPerDay;
		public const long Hour = LevyClock.TicksPerDay / 24, Day = LevyClock.TicksPerDay;

		public ArmoryTE Armory;
		public BarracksTE Payer;
		public readonly List<SoldierRecord> Souls = new();

		public static Item Make(int type) { var it = new Item(); it.SetDefaults(type); return it; }

		private static Item Aged(int type, long age) { var it = Make(type); DepositStamp.Set(it, age < 0 ? 0 : Now - age); return it; }

		/// <summary>The best prefix the weapon can roll, by the reforge's own quality measure.</summary>
		public static Item WithBestPrefix(int type)
		{
			var it = Make(type);
			var sample = Make(type);
			var best = ArmoryTE.ValidPrefixes(sample);
			if (best.Count > 0) it.Prefix(best[0]);
			return it;
		}

		public static ReforgeFixture Build(long treasury)
		{
			LevyClock.Force(Now);
			var f = new ReforgeFixture { Armory = new ArmoryTE(), Payer = new BarracksTE { Treasury = treasury } };
			var s = f.Armory.Storage;
			s[0] = Aged(ItemID.WoodenBow, Hour);                // recent
			s[1] = Aged(ItemID.Musket, 2 * Day);
			s[2] = Aged(ItemID.CopperBroadsword, 10 * Day);
			s[3] = WithBestPrefix(ItemID.Handgun); DepositStamp.Set(s[3], DepositStamp.Adopted); // already as good as it gets; old
			s[4] = Aged(ItemID.AmethystStaff, -1);              // never stamped
			s[5] = Aged(ItemID.IronBow, 5 * Hour);              // recent
			s[6] = Aged(ItemID.BladeofGrass, 20 * Day);
			s[7] = Aged(ItemID.Minishark, Hour);                // recent
			s[8] = Make(ItemID.Torch);                          // not a weapon
			s[9] = Aged(ItemID.SilverBroadsword, Hour); s[9].favorited = true; // favourited: never touched
			f.Armory.IssueWeapons[0] = Aged(ItemID.CopperBow, Hour / 2); // recent
			f.Armory.IssueWeapons[1] = Aged(ItemID.IronBroadsword, 40 * Day);
			f.Souls.Add(Soul(1, Aged(ItemID.Musket, 3 * Day), null));
			f.Souls.Add(Soul(2, Aged(ItemID.SilverBroadsword, 15 * Day), Aged(ItemID.TopazStaff, 15 * Day)));
			f.Souls.Add(Soul(3, Aged(ItemID.WoodenBow, 2 * Hour), null));   // recent
			f.Souls.Add(Soul(4, new Item(), null));                         // bare-handed
			ArmoryTE.TestServed = f.Souls;
			return f;
		}

		private static SoldierRecord Soul(int id, Item weapon, Item sidearm)
		{
			var r = new SoldierRecord { Id = id, Name = "Test " + id, Kind = SoldierKind.Militia };
			r.Gear.Weapon = weapon;
			if (sidearm != null) r.Gear.Sidearm = sidearm;
			return r;
		}

		public void Release() { ArmoryTE.TestServed = null; LevyClock.Force(null); }

		/// <summary>Seeds the game's random so a run repeats exactly.</summary>
		public static void Seed(int seed) { Main.rand = new UnifiedRandom(seed); }

		/// <summary>"region:slot name" for every candidate the plan selects, in run order.</summary>
		public List<string> Selected(ReforgePlan plan)
		{
			var l = new List<string>();
			foreach (var c in Armory.ReforgeCandidates())
				if (ArmoryTE.InTargetSet(plan, c, Armory.Rules, LevyClock.Now)) l.Add(Label(c));
			return l;
		}

		public static string Label(ReforgeCandidate c) => (c.Held ? (c.Region == 2 ? "hand#" : "side#") + c.Holder.Id : (c.Region == 0 ? "store" : "rack") + c.Slot) + " " + c.Item.Name;
	}
}
