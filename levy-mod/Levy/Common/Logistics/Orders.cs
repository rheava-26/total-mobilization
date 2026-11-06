using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Net;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Common.Logistics
{
	public enum OrderMode : byte { Bucket = 0, Random = 1 }

	/// <summary>One line of an order: a weapon (one type) or an armor set (up to three types made together), and its share of the order.</summary>
	public sealed class OrderLine
	{
		public int[] Types = Array.Empty<int>();
		/// <summary>Relative share (percent when the lines add to 100).</summary>
		public int Weight = 1;
		/// <summary>Units this line owes (set when the order is created or its total changes).</summary>
		public int Planned;
		/// <summary>Units made so far.</summary>
		public int Done;
		public int Left => Math.Max(0, Planned - Done);
	}

	/// <summary>A standing buy: a specific item, any gun the shops sell, or any ammo, up to a count and a copper budget.</summary>
	public sealed class BuyRule
	{
		public const byte KindItem = 0, KindAnyGun = 1, KindAnyAmmo = 2;
		public byte Kind;
		/// <summary>Item type for <see cref="KindItem"/>; for <see cref="KindAnyAmmo"/> an ammo id (0 = any); unused for guns.</summary>
		public int Type;
		public int Count, Bought;
		public long Budget, Spent;
		public bool Finished => Bought >= Count || Spent >= Budget;
	}

	/// <summary>One row of the materials checklist: what the whole order needs of an item, what stock holds, and what is missing.</summary>
	public struct ChecklistRow { public int Type; public long Need, Have; public long Missing => Math.Max(0, Need - Have); }

	/// <summary>A craft-into-armory order. Saved with the world; the server runs it (<see cref="LogisticsRunner"/>) and sends the book to clients.</summary>
	public sealed class LogisticsOrder
	{
		public int Id;
		public string Name = "";
		public int ArmoryId;
		public OrderMode Mode;
		public int Total;
		public bool Paused;
		public int Seed;
		/// <summary>When a needed material is short and a shopkeeper in range sells it, buy it (within <see cref="ShortfallBudget"/>).</summary>
		public bool BuyShortfalls;
		public long ShortfallBudget, ShortfallSpent;
		public List<OrderLine> Lines = new();
		public List<BuyRule> Rules = new();
		// ---- runtime (server computes, wire carries; never saved)
		public string Status = "";
		public List<ChecklistRow> Checklist = new();

		public int Made { get { int n = 0; foreach (var l in Lines) n += l.Done; return n; } }
		public int Owed { get { int n = 0; foreach (var l in Lines) n += l.Left; return n; } }
		public bool Complete => Lines.Count > 0 && Owed == 0;

		/// <summary>Sets every line's quota from the total and the weights.</summary>
		public void Replan()
		{
			var plan = OrderMath.Split(Total, Lines.Select(l => l.Weight).ToArray());
			for (int i = 0; i < Lines.Count; i++) Lines[i].Planned = Math.Max(plan[i], Lines[i].Done);
		}

		// ---- serialization (one format for save, wire and the self-check)
		public void Write(BinaryWriter w, bool withRuntime)
		{
			w.Write(Id); w.Write(Name ?? ""); w.Write(ArmoryId); w.Write((byte)Mode); w.Write(Total); w.Write(Paused); w.Write(Seed);
			w.Write(BuyShortfalls); w.Write(ShortfallBudget); w.Write(ShortfallSpent);
			w.Write((byte)Lines.Count);
			foreach (var l in Lines)
			{
				w.Write((byte)l.Types.Length); foreach (int t in l.Types) w.Write(t);
				w.Write(l.Weight); w.Write(l.Planned); w.Write(l.Done);
			}
			w.Write((byte)Rules.Count);
			foreach (var r in Rules) { w.Write(r.Kind); w.Write(r.Type); w.Write(r.Count); w.Write(r.Bought); w.Write(r.Budget); w.Write(r.Spent); }
			w.Write(withRuntime);
			if (withRuntime)
			{
				w.Write(Status ?? "");
				w.Write((byte)Math.Min(255, Checklist.Count));
				for (int i = 0; i < Checklist.Count && i < 255; i++) { w.Write(Checklist[i].Type); w.Write(Checklist[i].Need); w.Write(Checklist[i].Have); }
			}
		}

		public static LogisticsOrder Read(BinaryReader r)
		{
			var o = new LogisticsOrder { Id = r.ReadInt32(), Name = r.ReadString(), ArmoryId = r.ReadInt32(), Mode = (OrderMode)r.ReadByte(), Total = r.ReadInt32(), Paused = r.ReadBoolean(), Seed = r.ReadInt32() };
			o.BuyShortfalls = r.ReadBoolean(); o.ShortfallBudget = r.ReadInt64(); o.ShortfallSpent = r.ReadInt64();
			int nl = r.ReadByte();
			for (int i = 0; i < nl; i++)
			{
				var l = new OrderLine(); int nt = r.ReadByte(); l.Types = new int[nt];
				for (int k = 0; k < nt; k++) l.Types[k] = r.ReadInt32();
				l.Weight = r.ReadInt32(); l.Planned = r.ReadInt32(); l.Done = r.ReadInt32();
				o.Lines.Add(l);
			}
			int nr = r.ReadByte();
			for (int i = 0; i < nr; i++) o.Rules.Add(new BuyRule { Kind = r.ReadByte(), Type = r.ReadInt32(), Count = r.ReadInt32(), Bought = r.ReadInt32(), Budget = r.ReadInt64(), Spent = r.ReadInt64() });
			if (r.ReadBoolean())
			{
				o.Status = r.ReadString(); int nc = r.ReadByte();
				for (int i = 0; i < nc; i++) o.Checklist.Add(new ChecklistRow { Type = r.ReadInt32(), Need = r.ReadInt64(), Have = r.ReadInt64() });
			}
			return o;
		}

		/// <summary>Clamps everything a client could have sent wrong: counts, shares, item types, totals. A server never trusts a client's order.</summary>
		public bool Sanitize()
		{
			Total = Math.Clamp(Total, 0, LogisticsConstants.MaxTotal);
			if (Lines.Count == 0 || Lines.Count > LogisticsConstants.MaxLines) return false;
			if (Rules.Count > 8) Rules.RemoveRange(8, Rules.Count - 8);
			Name = (Name ?? "").Length > 40 ? Name.Substring(0, 40) : Name ?? "";
			foreach (var l in Lines)
			{
				if (l.Types.Length == 0 || l.Types.Length > 3) return false;
				foreach (int t in l.Types) if (t <= 0 || t >= ItemLoader.ItemCount) return false;
				l.Weight = Math.Clamp(l.Weight, 0, 1000); l.Planned = Math.Clamp(l.Planned, 0, LogisticsConstants.MaxTotal); l.Done = Math.Clamp(l.Done, 0, LogisticsConstants.MaxTotal);
			}
			foreach (var r in Rules) { r.Count = Math.Clamp(r.Count, 0, 9999); r.Bought = Math.Clamp(r.Bought, 0, 9999); r.Budget = Math.Clamp(r.Budget, 0, 100_000_000L); r.Spent = Math.Clamp(r.Spent, 0, 100_000_000L); if (r.Kind > 2) return false; }
			ShortfallBudget = Math.Clamp(ShortfallBudget, 0, 100_000_000L); ShortfallSpent = Math.Clamp(ShortfallSpent, 0, 100_000_000L);
			return true;
		}
	}

	/// <summary>All orders of the world. Saved with the world, sent whole to clients on join and after every change.</summary>
	public class OrderBook : ModSystem
	{
		public static readonly List<LogisticsOrder> Orders = new();
		private static int _nextId = 1;
		/// <summary>Bumped on every change a client should redraw for.</summary>
		public static int Version { get; private set; }

		public override void OnWorldLoad() => Clear();
		public override void OnWorldUnload() => Clear();
		public override void ClearWorld() => Clear();
		public static void Clear() { Orders.Clear(); _nextId = 1; Version++; }
		public static void Touch() => Version++;

		public static LogisticsOrder Find(int id) { foreach (var o in Orders) if (o.Id == id) return o; return null; }

		public static List<LogisticsOrder> For(int armoryId) => Orders.Where(o => o.ArmoryId == armoryId).ToList();

		/// <summary>Adds (id 0 or unknown) or replaces an order, after sanitising. Returns the stored order or null if refused.</summary>
		public static LogisticsOrder Upsert(LogisticsOrder o)
		{
			if (!o.Sanitize()) return null;
			var cur = Find(o.Id);
			if (cur == null)
			{
				if (Orders.Count >= LogisticsConstants.MaxOrders) return null;
				o.Id = _nextId++; if (o.Seed == 0) o.Seed = Main.rand?.Next(1, int.MaxValue) ?? 1;
				foreach (var l in o.Lines) l.Done = 0;
				o.Replan(); Orders.Add(o); Version++; return o;
			}
			// an edit keeps progress: lines are matched by their types; a changed total or share re-plans, never below what is already made
			foreach (var l in o.Lines) { var old = cur.Lines.FirstOrDefault(x => x.Types.SequenceEqual(l.Types)); l.Done = old?.Done ?? 0; }
			o.Id = cur.Id; o.Seed = cur.Seed; o.Replan();
			Orders[Orders.IndexOf(cur)] = o; Version++;
			return o;
		}

		public static bool Remove(int id) { var o = Find(id); if (o == null) return false; Orders.Remove(o); Version++; return true; }

		public override void SaveWorldData(TagCompound tag)
		{
			var list = new List<TagCompound>();
			foreach (var o in Orders)
			{
				using var ms = new MemoryStream(); using (var w = new BinaryWriter(ms)) o.Write(w, false);
				list.Add(new TagCompound { ["d"] = ms.ToArray() });
			}
			tag["orders"] = list; tag["next"] = _nextId;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Orders.Clear(); _nextId = tag.GetInt("next"); if (_nextId < 1) _nextId = 1;
			if (tag.ContainsKey("orders"))
				foreach (var t in tag.GetList<TagCompound>("orders"))
				{
					try { using var r = new BinaryReader(new MemoryStream(t.GetByteArray("d"))); var o = Read(r); if (o.Sanitize()) { Orders.Add(o); _nextId = Math.Max(_nextId, o.Id + 1); } }
					catch (Exception e) { Mod.Logger.Warn("Levy/Logistics: dropped an unreadable order: " + e.Message); }
				}
			Version++;
		}

		private static LogisticsOrder Read(BinaryReader r) => LogisticsOrder.Read(r);

		// A joining client gets the whole book with the rest of the world data.
		public override void NetSend(BinaryWriter writer) => WriteBook(writer);
		public override void NetReceive(BinaryReader reader) => ReadBook(reader);

		public static void WriteBook(BinaryWriter w)
		{
			w.Write((byte)Math.Min(255, Orders.Count));
			for (int i = 0; i < Orders.Count && i < 255; i++) Orders[i].Write(w, true);
		}

		public static void ReadBook(BinaryReader r)
		{
			Orders.Clear(); int n = r.ReadByte();
			for (int i = 0; i < n; i++) Orders.Add(LogisticsOrder.Read(r));
			Version++;
		}

		/// <summary>Server: send the whole book to every client (or one). Orders are few and small.</summary>
		public static void Broadcast(int toWho = -1)
		{
			if (Main.netMode != NetmodeID.Server) { Version++; return; }
			var p = LevyPackets.New(LevyPacket.LogisticsSnapshot); WriteBook(p); p.Send(toWho);
		}
	}
}
