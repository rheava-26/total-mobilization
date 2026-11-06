using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Thorium;
using Levy.Common.Net;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Team
{
	/// <summary>
	/// Nebula armor boosters are team property in vanilla: a booster picked up by one player is relayed to every teammate within 700 px of the pickup (MessageBuffer packet 102 calls NebulaLevelup on each).
	/// A commander's levies are teammates, so a booster the commander takes (hook on Player.NebulaLevelup, the local player's only) goes to every levy of his within 700 px, and a booster a levy takes in the Nebula set
	/// (SoldierNPC.NebulaBoosterArrived) goes to the commander and the other levies within 700 px of it. The levy booster uses the same three kinds, level cap and 480-tick timer as the levy's own pickup.
	/// </summary>
	public static class TeamNebula
	{
		private static bool _suppress;
		public static bool Installed { get; private set; }
		/// <summary>Self-check counter: boosters passed between the commander and the levies.</summary>
		public static int Shared;

		public static void Install() { if (Installed) return; On_Player.NebulaLevelup += Hook; Installed = true; }
		public static void Uninstall() { if (!Installed) return; On_Player.NebulaLevelup -= Hook; Installed = false; }

		/// <summary>Vanilla's three booster buffs (first level): life 173, mana 176, damage 179. The levy's own order is life 0, damage 1, mana 2.</summary>
		public static int KindOf(int type) => type == BuffID.NebulaUpLife1 ? 0 : type == BuffID.NebulaUpDmg1 ? 1 : type == BuffID.NebulaUpMana1 ? 2 : -1;
		public static int TypeOf(int kind) => kind == 0 ? BuffID.NebulaUpLife1 : kind == 1 ? BuffID.NebulaUpDmg1 : BuffID.NebulaUpMana1;

		private static void Hook(On_Player.orig_NebulaLevelup orig, Player self, int type)
		{
			orig(self, type);
			if (_suppress || !TeamItems.IsLocal(self) || KindOf(type) < 0) return;
			try { TeamNet.RequestNebula(self, type); }
			catch (Exception e) { TeamItems.WarnOnce("nebula share", e); }
		}

		/// <summary>Server / single player: the commander took a booster; his levies within 700 px of him get it.</summary>
		public static void ShareFromPlayer(Player p, int buffType)
		{
			int kind = KindOf(buffType);
			if (kind < 0 || LevyUtil.IsClient) return;
			foreach (SoldierNPC s in TeamItems.Levies(p))
				if (Vector2.Distance(p.Center, s.NPC.Center) <= LevyConstants.TeamNebulaRangePx) { s.TeamNebulaBooster(kind); Shared++; }
		}

		/// <summary>Server / single player: a levy took a booster; the commander and the other levies within 700 px of him get it.</summary>
		public static void ShareFromLevy(SoldierNPC src, int kind)
		{
			if (LevyUtil.IsClient || src == null) return;
			Player p = TeamItems.CommanderOf(src);
			if (p == null) return;
			if (Vector2.Distance(p.Center, src.NPC.Center) <= LevyConstants.TeamNebulaRangePx)
			{
				_suppress = true;
				try { TeamNet.Nebula(p, TypeOf(kind)); Shared++; } finally { _suppress = false; }
			}
			foreach (SoldierNPC s in TeamItems.Levies(p))
				if (s != src && Vector2.Distance(src.NPC.Center, s.NPC.Center) <= LevyConstants.TeamNebulaRangePx) { s.TeamNebulaBooster(kind); Shared++; }
		}

		/// <summary>Client side of a booster the server lays on this player: relay is already done, so the hook must not send it back.</summary>
		public static void ApplyFromServer(Player p, int buffType)
		{
			_suppress = true;
			try { p.NebulaLevelup(buffType); } finally { _suppress = false; }
		}
	}

	/// <summary>
	/// Info accessories (Watch, Compass, Depth Meter, Fish Finder, Weather Radio, Sextant, Radar, Tally Counter, Metal Detector, Lifeform Analyzer, Stopwatch, DPS Meter, Goblin Tech, Ruler...) are shared by teammates within 800 px
	/// (Player.RefreshInfoAccsFromTeamPlayers). A levy wearing one is a teammate, so the commander's readouts take what the levies within 800 px carry.
	/// </summary>
	public static class TeamInfo
	{
		public static bool Installed { get; private set; }
		private static uint _stamp = uint.MaxValue; private static int _cachedFor = -1;
		private static readonly List<Item> _items = new();

		public static void Install() { if (Installed) return; On_Player.RefreshInfoAccsFromTeamPlayers += Hook; Installed = true; }
		public static void Uninstall() { if (!Installed) return; On_Player.RefreshInfoAccsFromTeamPlayers -= Hook; Installed = false; }

		private static void Hook(On_Player.orig_RefreshInfoAccsFromTeamPlayers orig, Player self)
		{
			orig(self);
			if (self.whoAmI != Main.myPlayer || Main.dedServ) return;
			try
			{
				if (_cachedFor != self.whoAmI || Main.GameUpdateCount - _stamp >= 30u) Gather(self);
				for (int i = 0; i < _items.Count; i++) self.RefreshInfoAccsFromItemType(_items[i]);
			}
			catch (Exception e) { TeamItems.WarnOnce("info accessory share", e); }
		}

		/// <summary>The accessory items carried by the commander's levies within 800 px (refreshed twice a second).</summary>
		public static void Gather(Player p)
		{
			_stamp = Main.GameUpdateCount; _cachedFor = p.whoAmI; _items.Clear();
			foreach (SoldierNPC s in TeamItems.Levies(p))
			{
				if (Vector2.Distance(p.Center, s.NPC.Center) > LevyConstants.TeamInfoRangePx || s.Record?.Gear == null) continue;
				int slots = Math.Min(s.Record.AccessorySlots, s.Record.Gear.Accessories.Length);
				for (int i = 0; i < slots; i++)
				{
					Item it = s.Record.Gear.Accessories[i];
					if (it != null && !it.IsAir && !_items.Exists(x => x.type == it.type)) _items.Add(it);
				}
			}
		}
	}
}
