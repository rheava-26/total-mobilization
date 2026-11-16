using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;
using Levy.Common;
using Levy.Common.Net;
using Levy.Common.Soldiers;

namespace Levy.Content.TileEntities
{
	/// <summary>Why an item is out of the racks: a levy carries it, or it is on the loan ledger (a combat drone mounts it).</summary>
	public enum InUseKind : byte { Carried, Lent }

	/// <summary>One item that left this armory and is in use: the real item instance, who has it, and the holder's name (null for a loan nobody named: a combat drone).</summary>
	public readonly struct InUseEntry
	{
		public readonly Item Item; public readonly InUseKind Kind; public readonly string Holder;
		/// <summary>Who carries it and in which gear slot (<see cref="SoldierGear.SlotWeapon"/> ...), so the panel can take it. A loan has neither (-1 / <see cref="NoSlot"/>): only the drone can give it back.</summary>
		public readonly int SoldierId; public readonly int Slot;
		public const int NoSlot = 255;
		public bool Takeable => Kind == InUseKind.Carried && SoldierId >= 0 && Slot != NoSlot;
		public InUseEntry(Item item, InUseKind kind, string holder, int soldierId = -1, int slot = NoSlot) { Item = item; Kind = kind; Holder = holder; SoldierId = soldierId; Slot = slot; }
	}

	/// <summary>
	/// What the armory grid shows besides the racks: the gear the levies it serves are carrying and the weapons on its loan ledger. Neither is in <see cref="Storage"/> (an issue empties the slot, a loan moves the
	/// weapon to <see cref="Lent"/>), so the panel draws them as cells of their own. The list is taken at most every <see cref="LevyConstants.UI.ArmoryInUseTicks"/> ticks and only when the armory panel asks for it,
	/// and it carries a stamp that changes only when the list does, so the panel rebuilds its grid on a real change and never on a timer. Server / single player build it from the served levies and the ledger;
	/// a multiplayer client asks the server for it (<see cref="LevyPacket.ArmoryInUseRequest"/>) at the same pace and shows the last answer. A carried item can be taken (<c>ArmoryTE.TakeHeld</c>); a loan cannot.
	/// </summary>
	public partial class ArmoryTE
	{
		private readonly List<InUseEntry> _inUse = new();
		private uint _inUseTick = uint.MaxValue; private long _inUseHash; private int _inUseStamp;
		private readonly Dictionary<Item, string> _loanHolders = new(ReferenceEqualityComparer.Instance);

		/// <summary>Names who holds a loaned item (the borrower's own name); a loan nobody names reads as a combat drone. Dropped with the loan.</summary>
		public void NoteLoan(Item it, string holder)
		{
			if (it == null) return;
			if (string.IsNullOrEmpty(holder)) _loanHolders.Remove(it); else _loanHolders[it] = holder;
		}

		/// <summary>The in-use list for the armory panel and a stamp that changes whenever the list does (same stamp = same entries, in the same order).</summary>
		public IReadOnlyList<InUseEntry> InUse(out int stamp)
		{
			uint now = Main.GameUpdateCount;
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				if (_inUseTick == uint.MaxValue || now - _inUseTick >= (uint)LevyConstants.UI.ArmoryInUseTicks || now < _inUseTick)
				{
					_inUseTick = now;
					var req = LevyPackets.New(LevyPacket.ArmoryInUseRequest); req.Write(ID); req.Send();
				}
				stamp = _inUseStamp;
				return _inUse;
			}
			if (_inUseTick == uint.MaxValue || now - _inUseTick >= (uint)LevyConstants.UI.ArmoryInUseTicks || now < _inUseTick) { _inUseTick = now; RebuildInUse(); }
			stamp = _inUseStamp;
			return _inUse;
		}

		private void RebuildInUse()
		{
			var fresh = new List<InUseEntry>(_inUse.Count + 8);
			long h = 17;
			void Add(Item it, InUseKind kind, string holder, int holderId, int slot = InUseEntry.NoSlot)
			{
				if (it == null || it.IsAir || it.stack <= 0) return;
				fresh.Add(new InUseEntry(it, kind, holder, kind == InUseKind.Carried ? holderId : -1, slot));
				unchecked { h = h * 31 + RuntimeHelpers.GetHashCode(it); h = h * 31 + holderId * 4 + (int)kind; }
			}
			foreach (var it in Lent)
			{
				_loanHolders.TryGetValue(it, out var who);
				Add(it, InUseKind.Lent, who, who == null ? -1 : who.GetHashCode());
			}
			foreach (var rec in ServedSoldiers())
			{
				var g = rec?.Gear; if (g == null) continue;
				string name = string.IsNullOrEmpty(rec.Name) ? "?" : rec.Name; int id = rec.Id;
				void Slot(int slot) => Add(g.Get(slot), InUseKind.Carried, name, id, slot);
				Slot(SoldierGear.SlotWeapon); Slot(SoldierGear.SlotSidearm);
				for (int x = 0; x < g.ExtraSidearms.Length; x++) Slot(SoldierGear.SlotSide2 + x);
				Slot(SoldierGear.SlotHead); Slot(SoldierGear.SlotBody); Slot(SoldierGear.SlotLegs);
				for (int i = 0, n = AccSlotsOf(rec); i < n; i++) Slot(SoldierGear.SlotAcc0 + i);
				Slot(SoldierGear.SlotAmmo); Slot(SoldierGear.SlotHook); Slot(SoldierGear.SlotCamera);
				Slot(SoldierGear.SlotWings); Slot(SoldierGear.SlotBoots);
			}
			if (_loanHolders.Count > 0) PruneLoanHolders();
			if (h != _inUseHash || fresh.Count != _inUse.Count) { _inUseHash = h; _inUse.Clear(); _inUse.AddRange(fresh); _inUseStamp++; }
		}

		/// <summary>Server: the list this armory sends a client that asked (the cached list, so a burst of requests rebuilds nothing).</summary>
		public void WriteInUse(System.IO.BinaryWriter w)
		{
			var list = InUse(out _);
			w.Write(ID); w.Write(list.Count);
			foreach (var e in list)
			{
				w.Write((byte)e.Kind); w.Write(e.SoldierId); w.Write((byte)e.Slot); w.Write(e.Holder ?? "");
				ItemIO.Send(e.Item, w, writeStack: true);
			}
		}

		/// <summary>Client: takes the server's answer as the list the panel shows. The stamp moves only when the list really changed.</summary>
		public void ReadInUse(System.IO.BinaryReader r)
		{
			int n = r.ReadInt32(); var fresh = new List<InUseEntry>(n); long h = 17;
			for (int i = 0; i < n; i++)
			{
				var kind = (InUseKind)r.ReadByte(); int sid = r.ReadInt32(); int slot = r.ReadByte(); string holder = r.ReadString();
				Item it = ItemIO.Receive(r, readStack: true);
				if (it == null || it.IsAir) continue;
				fresh.Add(new InUseEntry(it, kind, kind == InUseKind.Lent && holder.Length == 0 ? null : holder, sid, slot));
				unchecked { h = h * 31 + it.type; h = h * 31 + it.prefix; h = h * 31 + it.stack; h = h * 31 + sid * 4 + (int)kind; h = h * 31 + slot; }
			}
			if (h != _inUseHash || fresh.Count != _inUse.Count) { _inUseHash = h; _inUse.Clear(); _inUse.AddRange(fresh); _inUseStamp++; }
		}

		/// <summary>Forgets the names of loans that came back.</summary>
		private void PruneLoanHolders()
		{
			List<Item> gone = null;
			foreach (var k in _loanHolders.Keys) if (IndexOfRef(Lent, k) < 0) (gone ??= new List<Item>()).Add(k);
			if (gone != null) foreach (var k in gone) _loanHolders.Remove(k);
		}
	}
}
