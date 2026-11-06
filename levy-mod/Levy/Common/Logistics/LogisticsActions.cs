using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Net;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Common.Logistics
{
	/// <summary>
	/// Every logistics action the screen can take, in one place. In single player the action runs here; on a multiplayer client it becomes a packet and the SERVER does it (it owns the stock, the money and the armory),
	/// then sends the new order book back. Handlers re-check everything: the armory exists, the sender stands near it, the numbers are in range.
	/// Wire layouts (after the packet byte), all client -> server:
	///  LogisticsOrderEdit: byte action(0 save,1 delete,2 pause,3 resume), int orderId (0 = new); action 0 then LogisticsOrder.Write(false)
	///  LogisticsSell: int armoryId, bool shimmerBack, byte kind(0 slots,1 filter), then kind 0: ushort count, ushort[] storage indices; kind 1: byte category (0 none), sbyte tierIndex (-1 none)
	///  LogisticsCraft: int armoryId, int recipeIndex (Main.recipe), int crafts
	///  LogisticsBuy: int armoryId, byte kind, int type, int count, long budget   (one standing rule, run once now)
	/// </summary>
	public static class LogisticsActions
	{
		private static bool Near(int who, ArmoryTE a) => who >= 0 && who < Main.maxPlayers && Main.player[who].active && LevyUtil.WithinTiles(Main.player[who].Center, a.Center, LevyConstants.UI.ArmoryEditRangeTiles);
		private static ArmoryTE Armory(int id) => TileEntity.ByID.TryGetValue(id, out var te) ? te as ArmoryTE : null;
		private static void Tell(int who, string text) { if (who >= 0 && who < Main.maxPlayers) RosterSystem.SendToPlayer(Main.player[who], text); else if (Main.netMode == NetmodeID.SinglePlayer) Main.NewText(text); }

		// ---------------------------------------------------------------- orders
		public static void SaveOrder(LogisticsOrder o)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) { var p = LevyPackets.New(LevyPacket.LogisticsOrderEdit); p.Write((byte)0); p.Write(o.Id); o.Write(p, false); p.Send(); }
			else ApplyOrderEdit(Main.myPlayer, 0, o.Id, o);
		}
		public static void DeleteOrder(int id) => Edit(1, id);
		public static void SetPaused(int id, bool paused) => Edit(paused ? (byte)2 : (byte)3, id);
		private static void Edit(byte action, int id)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) { var p = LevyPackets.New(LevyPacket.LogisticsOrderEdit); p.Write(action); p.Write(id); p.Send(); }
			else ApplyOrderEdit(Main.myPlayer, action, id, null);
		}

		internal static void OnOrderEdit(BinaryReader r, int who)
		{
			byte action = r.ReadByte(); int id = r.ReadInt32();
			LogisticsOrder o = null; if (action == 0) { try { o = LogisticsOrder.Read(r); } catch (Exception) { return; } }
			if (Main.netMode != NetmodeID.Server) return;
			ApplyOrderEdit(who, action, id, o);
		}

		private static void ApplyOrderEdit(int who, byte action, int id, LogisticsOrder o)
		{
			if (action == 0)
			{
				var a = Armory(o.ArmoryId); if (a == null || Main.netMode == NetmodeID.Server && !Near(who, a)) return;
				o.Id = id; o.Status = ""; o.Checklist = new();
				var stored = OrderBook.Upsert(o);
				if (stored == null) { Tell(who, LevyUtil.T("Logistics.OrderRefused")); return; }
				LogisticsRunner.Run(stored);
			}
			else
			{
				var cur = OrderBook.Find(id); if (cur == null) return;
				var a = Armory(cur.ArmoryId); if (a != null && Main.netMode == NetmodeID.Server && !Near(who, a)) return;
				if (action == 1) OrderBook.Remove(id);
				else { cur.Paused = action == 2; OrderBook.Touch(); LogisticsRunner.Run(cur); }
			}
			OrderBook.Broadcast();
		}

		// ---------------------------------------------------------------- sell
		public static void Sell(ArmoryTE a, IList<int> slots, bool shimmerBack)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				var p = LevyPackets.New(LevyPacket.LogisticsSell); p.Write(a.ID); p.Write(shimmerBack); p.Write((byte)0);
				var list = slots.Where(i => i >= 0 && i < ushort.MaxValue).Take(2000).ToList();
				p.Write((ushort)list.Count); foreach (int i in list) p.Write((ushort)i); p.Send();
			}
			else DoSell(Main.myPlayer, a, slots, shimmerBack);
		}

		public static void SellFiltered(ArmoryTE a, Selling.Filter f, int tierIndex, bool shimmerBack)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				var p = LevyPackets.New(LevyPacket.LogisticsSell); p.Write(a.ID); p.Write(shimmerBack); p.Write((byte)1);
				p.Write((byte)(f.Category.HasValue ? (byte)f.Category.Value : 0)); p.Write((sbyte)tierIndex); p.Send();
			}
			else DoSell(Main.myPlayer, a, Selling.Select(a, f), shimmerBack);
		}

		internal static void OnSell(BinaryReader r, int who)
		{
			int armoryId = r.ReadInt32(); bool shimmer = r.ReadBoolean(); byte kind = r.ReadByte();
			List<int> slots = null; Selling.Filter f = null;
			if (kind == 0) { int n = r.ReadUInt16(); slots = new List<int>(n); for (int i = 0; i < n; i++) slots.Add(r.ReadUInt16()); }
			else { byte cat = r.ReadByte(); sbyte tier = r.ReadSByte(); f = FilterOf(cat, tier); }
			if (Main.netMode != NetmodeID.Server) return;
			var a = Armory(armoryId); if (a == null || !Near(who, a)) return;
			DoSell(who, a, slots ?? Selling.Select(a, f), shimmer);
		}

		public static Selling.Filter FilterOf(byte cat, int tierIndex)
		{
			var f = new Selling.Filter();
			if (cat >= 1 && cat <= (byte)SellCat.Other) f.Category = (SellCat)cat;
			if (tierIndex >= 0 && tierIndex < Selling.Tiers.Length) { f.TierLo = Selling.Tiers[tierIndex].Lo; f.TierHi = Selling.Tiers[tierIndex].Hi; }
			return f;
		}

		private static void DoSell(int who, ArmoryTE a, IList<int> slots, bool shimmer)
		{
			var res = Selling.Run(a, slots, shimmer);
			Tell(who, res.Items + res.Shimmered == 0 ? LevyUtil.T("Logistics.SoldNothing") :
				res.Coin > 0 ? LevyUtil.T("Logistics.Sold", res.Items, LevyUtil.Coins(res.Coin), res.Buyer) + (res.Shimmered > 0 ? " " + LevyUtil.T("Logistics.ShimmeredN", res.Shimmered) : "") : LevyUtil.T("Logistics.ShimmeredN", res.Shimmered));
			a.Sync();
		}

		// ---------------------------------------------------------------- craft now
		public static void CraftNow(ArmoryTE a, Recipe r, int crafts)
		{
			if (r == null || r.RecipeIndex < 0) return;
			if (Main.netMode == NetmodeID.MultiplayerClient) { var p = LevyPackets.New(LevyPacket.LogisticsCraft); p.Write(a.ID); p.Write(r.RecipeIndex); p.Write(crafts); p.Send(); }
			else DoCraft(Main.myPlayer, a, r, crafts);
		}

		internal static void OnCraft(BinaryReader rd, int who)
		{
			int id = rd.ReadInt32(), idx = rd.ReadInt32(), crafts = rd.ReadInt32();
			if (Main.netMode != NetmodeID.Server) return;
			var a = Armory(id); if (a == null || !Near(who, a) || idx < 0 || idx >= Main.recipe.Length) return;
			var r = Main.recipe[idx]; if (r == null || r.Disabled || r.createItem == null || r.createItem.IsAir) return;
			DoCraft(who, a, r, Math.Clamp(crafts, 1, LogisticsConstants.MaxTotal));
		}

		private static void DoCraft(int who, ArmoryTE a, Recipe r, int crafts)
		{
			if (who < 0 || who >= Main.maxPlayers || !Main.player[who].active) return;
			var pool = CraftBrowser.PoolFor(a, Main.player[who]);
			int made = CraftBrowser.Craft(r, crafts, a, pool);
			Tell(who, made == 0 ? LevyUtil.T("Logistics.CraftNothing") : LevyUtil.T("Logistics.Crafted", made, Lang.GetItemNameValue(r.createItem.type)));
			a.Sync();
			CraftBrowser.Version++;
		}

		// ---------------------------------------------------------------- buy
		public static void BuyNow(ArmoryTE a, BuyRule rule)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				var p = LevyPackets.New(LevyPacket.LogisticsBuy); p.Write(a.ID); p.Write(rule.Kind); p.Write(rule.Type); p.Write(rule.Count); p.Write(rule.Budget); p.Send();
			}
			else DoBuy(Main.myPlayer, a, rule);
		}

		internal static void OnBuy(BinaryReader r, int who)
		{
			int id = r.ReadInt32(); var rule = new BuyRule { Kind = r.ReadByte(), Type = r.ReadInt32(), Count = r.ReadInt32(), Budget = r.ReadInt64() };
			if (Main.netMode != NetmodeID.Server) return;
			var a = Armory(id); if (a == null || !Near(who, a) || rule.Kind > 2) return;
			rule.Count = Math.Clamp(rule.Count, 0, 9999); rule.Budget = Math.Clamp(rule.Budget, 0, 100_000_000L);
			DoBuy(who, a, rule);
		}

		private static void DoBuy(int who, ArmoryTE a, BuyRule rule)
		{
			var offers = Shopping.AllOffers(a);
			long spent = Shopping.RunRule(a, rule, offers);
			Tell(who, rule.Bought == 0 ? LevyUtil.T("Logistics.BoughtNothing") : LevyUtil.T("Logistics.Bought", rule.Bought, LevyUtil.Coins(spent)));
			a.Sync();
		}
	}

	/// <summary>
	/// The hook the Seeker and Spectator potions (built later) call to let the player choose "find this item". <see cref="PickTarget"/> opens a chooser fed by the Magic Storage item list when a network is linked
	/// and by the armory's own storage otherwise; whichever way, the chosen item type arrives at the callback. <see cref="Target"/> is the standing choice (0 = none), which the potion reads.
	/// </summary>
	public static class FindTarget
	{
		/// <summary>The item type the player last chose to find, or 0.</summary>
		public static int Target;
		/// <summary>Raised when a target is chosen (the potion highlights the nearest of it).</summary>
		public static event Action<int> Chosen;
		/// <summary>The UI registers how to show a chooser: given the callback and the candidate item types. Set by the logistics screen; null on a dedicated server.</summary>
		public static Action<Action<int>, IReadOnlyList<int>> Chooser;

		public static void Set(int type) { Target = type; Chosen?.Invoke(type); }

		/// <summary>Candidate item types for the chooser: the Magic Storage network's items when <paramref name="armory"/> is linked to one, else what the armory's storage holds.</summary>
		public static List<int> Candidates(ArmoryTE armory)
		{
			var set = new SortedSet<int>();
			try { var net = NetworkStocks.ItemTypes?.Invoke(armory); if (net != null) foreach (int t in net) set.Add(t); } catch (Exception) { }
			if (armory?.Storage != null) foreach (var it in armory.Storage) if (it != null && !it.IsAir) set.Add(it.type);
			return set.ToList();
		}

		/// <summary>Opens the chooser; the chosen type is stored in <see cref="Target"/> and passed to <paramref name="onPicked"/>. False when no chooser is available (dedicated server or no UI).</summary>
		public static bool PickTarget(ArmoryTE armory, Action<int> onPicked = null)
		{
			if (Chooser == null) return false;
			Chooser(t => { Set(t); onPicked?.Invoke(t); }, Candidates(armory));
			return true;
		}
	}
}
