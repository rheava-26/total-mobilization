using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.Localization;
using Levy.Common.Config;
using Levy.Common.Dialogue;
using Levy.Common.Net;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Systems
{
	/// <summary>
	/// Wage Mutiny (LevyConfig.WageMutiny, off by default). A Regular whose wage the treasury cannot pay revolts at once: he refuses every order,
	/// robs town NPCs (who sulk), attacks the commander and takes coins from the commander's pockets and banks until his back pay is covered by
	/// what he took or by the treasury. State lives on <see cref="SoldierRecord"/> (Mutinous / WageOwed / WageLooted) and money only ever moves on
	/// the server / in single player; the robbed player's own client removes the coins from its own arrays (client-authoritative inventory).
	/// With the toggle off, or while wages are paid, nothing here runs.
	/// </summary>
	public static class MutinySystem
	{
		private static readonly int[] CoinTypes = { ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin, ItemID.PlatinumCoin };
		private static int _settleTimer;

		public static bool Enabled => LevyConfig.Get.WageMutiny;
		public static bool IsMutinous(SoldierRecord r) => r != null && r.Mutinous && r.Kind == SoldierKind.Regular && Enabled;
		/// <summary>Back pay the soldier has not yet collected, in copper.</summary>
		public static long Remaining(SoldierRecord r) => r == null ? 0L : Math.Max(0L, r.WageOwed - r.WageLooted);

		private static SoldierNPC BodyOf(SoldierRecord rec) => rec != null && rec.HasBody ? Main.npc[rec.BodyIndex].ModNPC as SoldierNPC : null;

		// =====================================================================================================
		// Starting and ending a mutiny (server / single player)
		// =====================================================================================================

		/// <summary>Dawn payroll failed for these soldiers' barracks: each revolts now, owed today's wage. Loud: chat for everyone, a shout over every body.</summary>
		public static void Begin(List<SoldierRecord> unpaid, BarracksTE te)
		{
			if (!Enabled || LevyUtil.IsClient || unpaid == null) return;
			int count = 0; long owed = 0;
			foreach (var r in unpaid)
			{
				if (r.Kind != SoldierKind.Regular || r.Mutinous) continue;
				r.Mutinous = true;
				r.WageOwed = EconomySystem.WagePerDay(r.Grade, r.StoodDown);
				r.WageLooted = 0;
				count++; owed += r.WageOwed;
				var body = BodyOf(r);
				if (body != null) { body.OnMutinyBegun(); DialogueSystem.FloatingText(body, LevyUtil.T("Mutiny.Shout"), DialogueTrigger.MUTINY); }
			}
			if (count == 0) return;
			string town = te != null ? SettlementSystem.Instance.TownName(te) : "";
			LevyUtil.Chat(LevyUtil.T("Mutiny.Begin", town, count, LevyUtil.Coins(owed)), LevyConstants.Mutiny.Alarm);
			LevyUtil.Chat(LevyUtil.T("Mutiny.BeginHint"), LevyConstants.Mutiny.Alarm);
		}

		/// <summary>Ends one soldier's mutiny: the debt is cleared and he takes his orders again.</summary>
		private static void Resolve(SoldierRecord rec, string chatKey)
		{
			rec.Mutinous = false; rec.DaysUnpaid = 0; rec.WageOwed = 0; rec.WageLooted = 0;
			var body = BodyOf(rec);
			if (body != null)
			{
				body.ReceiveOrder(rec.Order); // back to the standing order (and net sync)
				DialogueSystem.FloatingText(body, LevyUtil.T("Mutiny.Calm"), DialogueTrigger.RALLIED);
				DialogueSystem.Bark(body, DialogueTrigger.PAYDAY);
			}
			if (chatKey != null) LevyUtil.Chat(LevyUtil.T(chatKey, rec.Name), new Color(150, 255, 150));
		}

		/// <summary>
		/// Every <see cref="LevyConstants.Mutiny.SettleIntervalTicks"/> while any mutineer exists: a soldier whose loot covers his claim stands down;
		/// the rest are paid from their paying barracks' treasury when it covers the whole group's back pay at once (the same all-or-nothing rule as payday).
		/// Turning the config off clears every mutiny silently.
		/// </summary>
		public static void Tick()
		{
			if (LevyUtil.IsClient) return;
			if (++_settleTimer < LevyConstants.Mutiny.SettleIntervalTicks) return;
			_settleTimer = 0;
			var regulars = RosterSystem.Instance.Regulars;
			bool any = false;
			foreach (var r in regulars) if (r.Mutinous) { any = true; break; }
			if (!any) return;

			bool enabled = Enabled;
			if (!enabled)
			{
				foreach (var r in regulars) if (r.Mutinous) Resolve(r, null);
				RosterSystem.Instance.SendRosterSnapshot();
				return;
			}

			var groups = new Dictionary<int, (BarracksTE te, long sum, List<SoldierRecord> list)>();
			int resolved = 0;
			foreach (var r in regulars)
			{
				if (!r.Mutinous) continue;
				if (Remaining(r) <= 0) { Resolve(r, "Mutiny.CalmLoot"); resolved++; continue; }
				var te = RosterSystem.PayingBarracks(r);
				if (te == null) continue;
				if (!groups.TryGetValue(te.ID, out var g)) groups[te.ID] = g = (te, 0L, new List<SoldierRecord>());
				g.list.Add(r);
				groups[te.ID] = (g.te, g.sum + Remaining(r), g.list);
			}
			foreach (var g in groups.Values)
			{
				if (g.te.Treasury < g.sum) continue;
				g.te.Treasury -= g.sum;
				foreach (var r in g.list) { Resolve(r, null); resolved++; }
				LevyUtil.Chat(LevyUtil.T("Mutiny.PaidOff", SettlementSystem.Instance.TownName(g.te), LevyUtil.Coins(g.sum)), new Color(150, 255, 150));
				RosterSystem.SyncBarracks(g.te);
			}
			if (resolved == 0) return;
			bool left = false;
			foreach (var r in regulars) if (r.Mutinous) { left = true; break; }
			if (!left) LevyUtil.Chat(LevyUtil.T("Mutiny.Over"), new Color(150, 255, 150));
			RosterSystem.Instance.SendRosterSnapshot();
		}

		// =====================================================================================================
		// Robbing town NPCs (server / single player)
		// =====================================================================================================

		/// <summary>A town NPC a mutineer may rob: alive, a real resident (not a pet), and not already sour from an earlier robbery.</summary>
		public static bool Robbable(NPC n)
			=> n != null && n.active && n.townNPC && n.life > 0 && !NPCID.Sets.IsTownPet[n.type] && n.TryGetGlobalNPC(out Globals.MutinyNpc mood) && !mood.IsRobbed;

		/// <summary>The mutineer takes the NPC's purse (never more than his remaining claim); the NPC is robbed and its mood turns for a day.</summary>
		public static void RobNpc(SoldierNPC s, NPC victim)
		{
			var rec = s?.Record;
			if (rec == null || victim == null || LevyUtil.IsClient) return;
			long take = Math.Min(Remaining(rec), EconomySystem.LevyWage * LevyConstants.Mutiny.NpcPurseWages);
			if (take <= 0) return;
			if (!victim.TryGetGlobalNPC(out Globals.MutinyNpc mood)) return;
			mood.Rob(victim);
			rec.WageLooted += take;
			if (Main.netMode != NetmodeID.MultiplayerClient)
			{
				EmoteBubble.NewBubble(EmoteID.EmotionAnger, new WorldUIAnchor(victim), LevyConstants.DialogueEmoteBubbleTicks);
				DialogueSystem.FloatingText(s, LevyUtil.T("Mutiny.RobbedNpc", victim.GivenOrTypeName, LevyUtil.Coins(take)), DialogueTrigger.MUTINY);
			}
			s.NPC.netUpdate = true;
		}

		// =====================================================================================================
		// Attacking the commander and robbing pockets / banks
		// =====================================================================================================

		/// <summary>Coins the player carries in pockets and in all four banks (Piggy Bank, Safe, Defender's Forge, Void Vault), by this machine's copy.</summary>
		public static long CoinsOf(Player p)
		{
			if (p == null) return 0;
			long total = CoinsIn(p.inventory, InventoryCoinSlots);
			foreach (var bank in Banks(p)) total += CoinsIn(bank, bank.Length);
			return total;
		}

		/// <summary>A mutineer reached the commander: the server takes the claim off the server's copy of the player's coins and tells the player's client to do the actual taking and the hit.</summary>
		public static void StrikePlayer(SoldierNPC s, Player target)
		{
			var rec = s?.Record;
			if (rec == null || target == null || !target.active || target.dead || LevyUtil.IsClient) return;
			long take = Math.Min(Remaining(rec), CoinsOf(target));
			if (take <= 0) return;
			rec.WageLooted += take;
			int damage = Math.Max(LevyConstants.Mutiny.StrikeMinDamage, (int)(target.statLifeMax2 * LevyConstants.Mutiny.StrikeLifeFraction));
			int dir = s.NPC.Center.X < target.Center.X ? 1 : -1;
			if (Main.netMode == NetmodeID.Server)
			{
				var pk = LevyPackets.New(LevyPacket.MutinyRob);
				pk.Write(take); pk.Write(damage); pk.Write(dir); pk.Write(rec.Name ?? "");
				pk.Send(toClient: target.whoAmI);
			}
			else ApplyRobbery(target, take, damage, dir, rec.Name);
			s.NPC.netUpdate = true;
		}

		/// <summary>Client side of MutinyRob: long copper, int damage, int hitDirection, string soldierName.</summary>
		internal static void OnRobPacket(BinaryReader r, int fromWhoAmI)
		{
			long copper = r.ReadInt64(); int damage = r.ReadInt32(); int dir = r.ReadInt32(); string name = r.ReadString();
			if (Main.netMode != NetmodeID.MultiplayerClient) return;
			var p = Main.LocalPlayer;
			if (p == null || !p.active || p.dead) return;
			ApplyRobbery(p, Math.Clamp(copper, 0L, long.MaxValue), damage, dir, name);
		}

		/// <summary>
		/// Runs where the robbed player's inventory is authoritative (single player, or that player's own client): takes up to <paramref name="copper"/>
		/// from the pockets first, then the Piggy Bank, Safe, Defender's Forge and Void Vault. Only coin stacks are touched; other items stay put.
		/// </summary>
		private static void ApplyRobbery(Player p, long copper, int damage, int dir, string soldier)
		{
			long left = copper, spill = 0, fromBanks = 0;
			var source = p.GetSource_Misc("Levy:Mutiny");
			var touched = new List<int>();
			left -= TakeCoins(p.inventory, InventoryCoinSlots, left, touched, ref spill);
			SyncSlots(p, touched, PlayerItemSlotID.Inventory0, p.inventory);
			int bankIndex = 0;
			int[] bankBase = { PlayerItemSlotID.Bank1_0, PlayerItemSlotID.Bank2_0, PlayerItemSlotID.Bank3_0, PlayerItemSlotID.Bank4_0 };
			foreach (var bank in Banks(p))
			{
				if (left <= 0) break;
				touched.Clear();
				long got = TakeCoins(bank, bank.Length, left, touched, ref spill);
				left -= got; fromBanks += got;
				SyncSlots(p, touched, bankBase[bankIndex++], bank);
			}
			if (spill > 0) // no free slot for the change: it falls at the player's feet, never lost
				for (int i = CoinTypes.Length - 1; i >= 0; i--)
				{
					int n = Utils.CoinsSplit(spill)[i];
					if (n > 0) p.QuickSpawnItem(source, CoinTypes[i], n);
				}
			long taken = copper - left;
			if (taken > 0)
			{
				string text = LevyUtil.T(fromBanks > 0 ? "Mutiny.RobbedBank" : "Mutiny.RobbedPlayer", soldier, LevyUtil.Coins(taken));
				if (Main.netMode != NetmodeID.Server) { Main.NewText(text, LevyConstants.Mutiny.Alarm); CombatText.NewText(p.getRect(), LevyConstants.Mutiny.Alarm, "-" + LevyUtil.Coins(taken)); }
			}
			if (damage > 0) p.Hurt(PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(LevyUtil.T("Mutiny.DeathReason", p.name, soldier))), damage, dir);
		}

		// ---- coin arrays ----
		/// <summary>Pocket slots that can hold coins: the 50 inventory slots, the 4 coin slots and the 4 ammo slots (not the cursor slot).</summary>
		private const int InventoryCoinSlots = 58;

		private static IEnumerable<Item[]> Banks(Player p)
		{
			yield return p.bank.item; yield return p.bank2.item; yield return p.bank3.item; yield return p.bank4.item;
		}

		private static long CoinsIn(Item[] arr, int count)
		{
			long total = 0;
			for (int i = 0; i < count && i < arr.Length; i++) total += EconomySystem.CoinValue(arr[i]);
			return total;
		}

		/// <summary>
		/// Removes up to <paramref name="amount"/> copper of coins from one array. The array's coin stacks are cleared and the remainder is written
		/// back as the fewest stacks, into the freed slots first and then any empty slot; whatever finds no slot is added to <paramref name="spill"/>.
		/// </summary>
		private static long TakeCoins(Item[] arr, int count, long amount, List<int> touched, ref long spill)
		{
			if (amount <= 0) return 0;
			long total = 0; var coinSlots = new List<int>();
			for (int i = 0; i < count && i < arr.Length; i++) { long v = EconomySystem.CoinValue(arr[i]); if (v > 0) { total += v; coinSlots.Add(i); } }
			if (total <= 0) return 0;
			long take = Math.Min(amount, total), remain = total - take;
			foreach (int i in coinSlots) { arr[i].TurnToAir(); touched.Add(i); }
			if (remain <= 0) return take;

			var slots = new List<int>(coinSlots);
			for (int i = 0; i < count && i < arr.Length; i++) if (arr[i].IsAir && !slots.Contains(i)) slots.Add(i);
			int[] split = Utils.CoinsSplit(remain); // copper, silver, gold, platinum
			int next = 0;
			for (int d = CoinTypes.Length - 1; d >= 0; d--)
			{
				int left = split[d];
				int max = ContentSamples.ItemsByType.TryGetValue(CoinTypes[d], out var sample) ? Math.Max(1, sample.maxStack) : 100;
				while (left > 0)
				{
					int put = Math.Min(left, max);
					if (next >= slots.Count) { spill += (long)put * CoinMultiplier(d); left -= put; continue; }
					int slot = slots[next++];
					arr[slot].SetDefaults(CoinTypes[d]); arr[slot].stack = put;
					if (!touched.Contains(slot)) touched.Add(slot);
					left -= put;
				}
			}
			return take;
		}

		private static long CoinMultiplier(int denomination) => denomination switch { 0 => 1L, 1 => 100L, 2 => 10000L, _ => 1000000L };

		/// <summary>On a multiplayer client, tells the server about every slot that changed (vanilla does not do this for direct array writes).</summary>
		private static void SyncSlots(Player p, List<int> slots, int slotBase, Item[] arr)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient) return;
			foreach (int i in slots) NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, p.whoAmI, slotBase + i, arr[i].prefix);
		}
	}
}
