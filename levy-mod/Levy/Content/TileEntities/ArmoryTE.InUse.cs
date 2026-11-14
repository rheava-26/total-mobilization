using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.ID;
using Levy.Common;
using Levy.Common.Soldiers;

namespace Levy.Content.TileEntities
{
	/// <summary>Why an item is out of the racks: a levy carries it, or it is on the loan ledger (a combat drone mounts it).</summary>
	public enum InUseKind : byte { Carried, Lent }

	/// <summary>One item that left this armory and is in use: the real item instance, who has it, and the holder's name (null for a loan nobody named: a combat drone).</summary>
	public readonly struct InUseEntry
	{
		public readonly Item Item; public readonly InUseKind Kind; public readonly string Holder;
		public InUseEntry(Item item, InUseKind kind, string holder) { Item = item; Kind = kind; Holder = holder; }
	}

	/// <summary>
	/// What the armory grid shows besides the racks: the gear the levies it serves are carrying and the weapons on its loan ledger. Neither is in <see cref="Storage"/> (an issue empties the slot, a loan moves the
	/// weapon to <see cref="Lent"/>), so the panel draws them as read-only cells. The list is taken at most every <see cref="LevyConstants.UI.ArmoryInUseTicks"/> ticks and only when the armory panel asks for it,
	/// and it carries a stamp that changes only when the list does, so the panel rebuilds its grid on a real change and never on a timer. Server / single player: a multiplayer client holds no served list and no ledger, so its list is empty.
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
			if (_inUseTick == uint.MaxValue || now - _inUseTick >= (uint)LevyConstants.UI.ArmoryInUseTicks || now < _inUseTick) { _inUseTick = now; RebuildInUse(); }
			stamp = _inUseStamp;
			return _inUse;
		}

		private void RebuildInUse()
		{
			var fresh = new List<InUseEntry>(_inUse.Count + 8);
			long h = 17;
			void Add(Item it, InUseKind kind, string holder, int holderId)
			{
				if (it == null || it.IsAir || it.stack <= 0) return;
				fresh.Add(new InUseEntry(it, kind, holder));
				unchecked { h = h * 31 + RuntimeHelpers.GetHashCode(it); h = h * 31 + holderId * 4 + (int)kind; }
			}
			if (Main.netMode != NetmodeID.MultiplayerClient)
			{
				foreach (var it in Lent)
				{
					_loanHolders.TryGetValue(it, out var who);
					Add(it, InUseKind.Lent, who, who == null ? -1 : who.GetHashCode());
				}
				foreach (var rec in ServedSoldiers())
				{
					var g = rec?.Gear; if (g == null) continue;
					string name = string.IsNullOrEmpty(rec.Name) ? "?" : rec.Name; int id = rec.Id;
					Add(g.Weapon, InUseKind.Carried, name, id); Add(g.Sidearm, InUseKind.Carried, name, id);
					foreach (var x in g.ExtraSidearms) Add(x, InUseKind.Carried, name, id);
					Add(g.Head, InUseKind.Carried, name, id); Add(g.Body, InUseKind.Carried, name, id); Add(g.Legs, InUseKind.Carried, name, id);
					for (int i = 0, n = AccSlotsOf(rec); i < n; i++) Add(g.Accessories[i], InUseKind.Carried, name, id);
					Add(g.Ammo, InUseKind.Carried, name, id); Add(g.Hook, InUseKind.Carried, name, id); Add(g.Camera, InUseKind.Carried, name, id);
				}
			}
			if (_loanHolders.Count > 0) PruneLoanHolders();
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
