using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

// This file has no Levy dependency on purpose: the same source is compiled into a stand-alone probe mod, so the "Levy not loaded" arm of the loot audit
// measures the identical kill procedure with only the other mods (Calamity) in play.
namespace LootAuditKit
{
	/// <summary>Counts every item a kill drops (the spawn is swallowed, so 400 item slots never run out), every NPCLoot call and every OnKill call.</summary>
	public static class AuditCore
	{
		public static readonly Dictionary<int, long> Items = new();
		public static int LootCalls, KillCalls, SpawnFails;
		static bool _hooked, _count;

		public static void Hook()
		{
			if (_hooked) return;
			_hooked = true;
			On_Item.NewItem_Inner += ItemDetour;
			On_NPC.NPCLoot += LootDetour;
		}

		public static void Unhook()
		{
			if (!_hooked) return;
			_hooked = false;
			On_Item.NewItem_Inner -= ItemDetour;
			On_NPC.NPCLoot -= LootDetour;
		}

		static int ItemDetour(On_Item.orig_NewItem_Inner orig, IEntitySource src, int X, int Y, int W, int H, Item clone, int type, int stack, bool noBroadcast, int pfix, bool noGrab, bool reverse)
		{
			if (!_count) return orig(src, X, Y, W, H, clone, type, stack, noBroadcast, pfix, noGrab, reverse);
			int t = clone != null ? clone.type : type, s = clone != null ? clone.stack : stack;
			Items[t] = (Items.TryGetValue(t, out long v) ? v : 0) + s;
			return Main.maxItems;
		}

		static void LootDetour(On_NPC.orig_NPCLoot orig, NPC self)
		{
			if (_count) LootCalls++;
			orig(self);
		}

		public sealed class Result
		{
			public int Kills, LootCalls, OnKills, SpawnFails;
			public Dictionary<int, long> Items = new();
			public long Total() { long n = 0; foreach (var kv in Items) if (kv.Key < ItemID.CopperCoin || kv.Key > ItemID.PlatinumCoin) n += kv.Value; return n; }   // coins excluded: see CoinCopper
			public long CoinCopper() { long n = 0; foreach (var kv in Items) { if (kv.Key == ItemID.CopperCoin) n += kv.Value; else if (kv.Key == ItemID.SilverCoin) n += kv.Value * 100; else if (kv.Key == ItemID.GoldCoin) n += kv.Value * 10000; else if (kv.Key == ItemID.PlatinumCoin) n += kv.Value * 1000000; } return n; }
		}

		/// <summary>Spawns and kills <paramref name="n"/> of one NPC type, one at a time. <paramref name="credit"/> names the killer on the NPC just before the lethal strike (the commander, or a levy body).</summary>
		public static Result Kill(int npcType, int n, int tileX, int tileY, Action<NPC, int> credit)
		{
			Items.Clear(); LootCalls = KillCalls = SpawnFails = 0; _count = true;
			var r = new Result();
			try
			{
				for (int k = 0; k < n; k++)
				{
					int idx = NPC.NewNPC(new EntitySource_WorldEvent("LootAudit"), tileX * 16, tileY * 16, npcType);
					if (idx < 0 || idx >= Main.maxNPCs) { r.SpawnFails++; continue; }
					NPC npc = Main.npc[idx];
					if (npc.type != npcType) { npc.active = false; r.SpawnFails++; continue; }   // spawn code swapped the type
					npc.life = 1; npc.value = Math.Max(npc.value, 0f);
					credit(npc, k);
					npc.SimpleStrikeNPC(99999, 0, false, 0f, null, false, 0f, true);
					if (npc.active) { npc.active = false; r.SpawnFails++; continue; }     // did not die: not counted as a kill
					r.Kills++;
				}
			}
			finally { _count = false; }
			r.LootCalls = LootCalls; r.OnKills = KillCalls; r.Items = new Dictionary<int, long>(Items);
			return r;
		}

		/// <summary>What the spawner would use for this player right now: vanilla 600 / 5, then every mod's EditSpawnRate.</summary>
		public static (int rate, int max) SpawnRate(Player p)
		{
			int rate = 600, max = 5;
			NPCLoader.EditSpawnRate(p, ref rate, ref max);
			return (rate, max);
		}

		/// <summary>A fresh Player in slot 0, built after every mod loaded so other mods' ModPlayer and buff arrays exist (the server's slot-0 player predates them).</summary>
		public static Player NewPlayer(string name)
		{
			var np = new Player { name = name, active = true, whoAmI = 0 };
			np.statLife = np.statLifeMax = np.statLifeMax2 = 400;
			Main.player[0] = np;
			return np;
		}

		/// <summary>A weapon-like item: it deals damage and does not stack (swords, bows, staves).</summary>
		public static bool IsWeapon(int type)
		{
			try { return ContentSamples.ItemsByType.TryGetValue(type, out Item it) && it.damage > 0 && it.maxStack == 1 && !it.accessory && it.ammo == 0; } catch { return false; }
		}

		public static string ItemName(int type)
		{
			try { return (type < ItemID.Count ? ItemID.Search.GetName(type) : ItemLoader.GetItem(type)?.FullName) ?? type.ToString(); } catch { return type.ToString(); }
		}
	}

	public class AuditKillCounter : GlobalNPC
	{
		public override void OnKill(NPC npc) => AuditCore.KillCalls++;
	}
}
