using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;
using Levy.Common.Logistics;
using Levy.Common.Weapons;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// Autosell. The player marks an item type "Sell" on the item panel, with a number of copies to keep (0 by default). About twice a second the server (or single player) sells every copy of a marked
	/// type in bulk storage beyond that number, whatever brought it there: a deposit, a shift-click, Deposit junk, levies hauling loot home, a Magic Storage craft returned to the armory. The price is
	/// vanilla's plain sell price (value / 5, no shopkeeper needed) and the coins go to the treasury of the Barracks that pays for this armory (<see cref="LinkedBarracks"/>: the nearest Barracks in
	/// link range that links to it), or into storage as coins when no Barracks does.
	/// Never sold: anything outside bulk storage (the issue rows, gear a levy carries, weapons lent to drones), a favourited item, an item a rule names for use (<see cref="ArmoryRules.NamesForUse"/>),
	/// coins and worthless items. The cheapest copies go first, so the kept ones are the best prefixes. A mark is saved with the armory and synced with the tile entity; a multiplayer client asks the
	/// server to change one (ArmorySellMark). After an edit the sweep waits <see cref="SellGraceTicks"/> ticks, so a keep number typed digit by digit is never read half-typed.
	/// </summary>
	public partial class ArmoryTE
	{
		public const int MaxSellMarks = 512, MaxSellKeep = 99999, SellSweepTicks = 30, SellGraceTicks = 180;

		/// <summary>Item key (<see cref="ArmoryRules.KeyOf"/>) to the copies kept. A mark of a mod that is not loaded is kept and saved, and does nothing.</summary>
		public readonly Dictionary<string, int> SellMarks = new();

		/// <summary>Coins (copper) and items autosell has sold since the world loaded (the panel's note and the world check read them).</summary>
		public long SoldCopper; public int SoldItems;

		private uint _sellEditTick; private bool _sellEdited; private int _sellTimer;

		/// <summary>True when the type is marked; <paramref name="keep"/> is its kept count.</summary>
		public bool IsSellMarked(int type, out int keep)
		{
			keep = 0;
			string k = type > 0 ? ArmoryRules.KeyOf(type) : null;
			return k != null && SellMarks.TryGetValue(k, out keep);
		}

		/// <summary>Coins and other items that can never be sold this way (the mark is refused).</summary>
		public static bool CanMarkForSale(int type)
		{
			if (type <= 0 || type == ItemID.CopperCoin || type == ItemID.SilverCoin || type == ItemID.GoldCoin || type == ItemID.PlatinumCoin) return false;
			if (type == Terraria.ModLoader.ModContent.ItemType<Terraria.ModLoader.Default.UnloadedItem>()) return false; // the stand-in for every item of a mod that is not loaded
			return ArmoryRules.KeyOf(type) != null;
		}

		/// <summary>Sets, changes or clears a mark (server, single player, and a client's own copy until the sync arrives). Returns true when anything changed.</summary>
		public bool SetSellMark(int type, bool on, int keep)
		{
			if (!CanMarkForSale(type)) return false;
			string k = ArmoryRules.KeyOf(type);
			keep = Math.Clamp(keep, 0, MaxSellKeep);
			bool changed;
			if (!on) changed = SellMarks.Remove(k);
			else if (SellMarks.TryGetValue(k, out int old)) { changed = old != keep; SellMarks[k] = keep; }
			else { if (SellMarks.Count >= MaxSellMarks) return false; SellMarks[k] = keep; changed = true; }
			if (changed) { _sellEditTick = Main.GameUpdateCount; _sellEdited = true; MarkDirty(); }
			return changed;
		}

		/// <summary>Called from <see cref="Update"/>: a sweep every <see cref="SellSweepTicks"/> ticks while marks exist, held off just after an edit.</summary>
		private void TickAutoSell()
		{
			if (SellMarks.Count == 0 || Main.netMode == NetmodeID.MultiplayerClient) return;
			if (++_sellTimer < SellSweepTicks) return;
			_sellTimer = 0;
			if (_sellEdited && unchecked(Main.GameUpdateCount - _sellEditTick) < SellGraceTicks) return;
			_sellEdited = false;
			RunAutoSell();
		}

		/// <summary>True when a storage item may be sold by autosell (marked type aside): not favourited, worth something, not a coin, and no rule names it for use.</summary>
		public bool AutoSellable(Item it)
		{
			if (!Selling.Sellable(it)) return false;
			return !Rules.NamesForUse(it.type);
		}

		/// <summary>Sells every marked copy in bulk storage beyond its kept count, pays the treasury and returns the copper paid. Server / single player.</summary>
		public long RunAutoSell()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || SellMarks.Count == 0) return 0;
			Dictionary<int, int> keepOf = null;
			foreach (var kv in SellMarks)
			{
				int t = ArmoryRules.TypeOf(kv.Key);
				if (t > 0) (keepOf ??= new Dictionary<int, int>())[t] = kv.Value;
			}
			if (keepOf == null) return 0;
			// copies of each marked type in storage, and the slots that may be sold
			Dictionary<int, long> held = null; Dictionary<int, List<int>> slots = null;
			for (int i = 0; i < Storage.Length; i++)
			{
				var it = Storage[i];
				if (IsAir(it) || !keepOf.ContainsKey(it.type)) continue;
				held ??= new Dictionary<int, long>(); slots ??= new Dictionary<int, List<int>>();
				held[it.type] = (held.TryGetValue(it.type, out long h) ? h : 0) + it.stack;
				if (!AutoSellable(it)) continue;
				if (!slots.TryGetValue(it.type, out var l)) slots[it.type] = l = new List<int>();
				l.Add(i);
			}
			if (held == null) return 0;
			long gross = 0; int sold = 0;
			foreach (var kv in held)
			{
				long excess = kv.Value - keepOf[kv.Key];
				if (excess <= 0 || !slots.TryGetValue(kv.Key, out var l)) continue;
				l.Sort((a, b) => { int c = Storage[a].value.CompareTo(Storage[b].value); return c != 0 ? c : b.CompareTo(a); }); // cheapest first, then the last slot first
				foreach (int i in l)
				{
					if (excess <= 0) break;
					var it = Storage[i];
					int take = (int)Math.Min(it.stack, excess);
					gross += (long)it.value * take; sold += take; excess -= take;
					if (take >= it.stack) Storage[i] = new Item(); else it.stack -= take;
				}
			}
			if (sold == 0) return 0;
			long copper = gross / 5;
			SoldItems += sold; SoldCopper += copper;
			MarkDirty();
			if (copper > 0) Selling.Pay(this, copper);
			return copper;
		}

		// ------------------------------------------------------------------ persistence and wire

		private void SaveSellMarks(TagCompound tag)
		{
			if (SellMarks.Count == 0) return; // nothing written: an old build reading this save loses nothing
			var keys = new List<string>(SellMarks.Count); var keeps = new List<int>(SellMarks.Count);
			foreach (var kv in SellMarks) { keys.Add(kv.Key); keeps.Add(kv.Value); }
			tag["sellV1"] = keys; tag["sellKeepV1"] = keeps;
		}

		private void LoadSellMarks(TagCompound tag)
		{
			SellMarks.Clear();
			if (!tag.ContainsKey("sellV1")) return;
			var keys = tag.GetList<string>("sellV1"); var keeps = tag.GetList<int>("sellKeepV1");
			for (int i = 0; i < keys.Count && SellMarks.Count < MaxSellMarks; i++)
				if (!string.IsNullOrEmpty(keys[i])) SellMarks[keys[i]] = Math.Clamp(i < keeps.Count ? keeps[i] : 0, 0, MaxSellKeep);
		}

		private void WriteSellMarks(BinaryWriter w)
		{
			int n = Math.Min(SellMarks.Count, MaxSellMarks);
			w.Write((ushort)n);
			int written = 0;
			foreach (var kv in SellMarks) { if (written++ >= n) break; w.Write(kv.Key); w.Write(kv.Value); }
		}

		private void ReadSellMarks(BinaryReader r)
		{
			int n = r.ReadUInt16();
			var fresh = new Dictionary<string, int>();
			for (int i = 0; i < n; i++) { string k = r.ReadString(); int keep = r.ReadInt32(); if (i < MaxSellMarks && !string.IsNullOrEmpty(k)) fresh[k] = Math.Clamp(keep, 0, MaxSellKeep); }
			SellMarks.Clear();
			foreach (var kv in fresh) SellMarks[kv.Key] = kv.Value;
		}
	}
}
