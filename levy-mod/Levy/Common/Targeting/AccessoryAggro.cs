using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Content.NPCs;

namespace Levy.Common.Targeting
{
	/// <summary>
	/// How much aggro an accessory adds, read the way the armory reads class fit: equip it on a private scratch Player, run the vanilla equip pass and
	/// compare Player.aggro with a bare player's. Cached per item type; 0 when the pipeline is off or the read fails.
	/// </summary>
	public static class AccessoryAggro
	{
		private static readonly Dictionary<int, int> _cache = new();
		private static Player _scratch;
		private static bool _broken;

		/// <summary>Aggro the accessory adds (positive = taunt; negative = stealth, reported as-is).</summary>
		public static int Of(Item acc)
		{
			if (acc == null || acc.IsAir || _broken) return 0;
			if (_cache.TryGetValue(acc.type, out int cached)) return cached;
			int value = 0;
			try
			{
				if (LevyConfig.Get.UseVanillaStatPipeline) value = Measure(acc.type);
			}
			catch (Exception e)
			{
				_broken = true;
				Levy.Instance?.Logger.Warn("Levy: accessory aggro read failed, accessories will be issued without an aggro preference: " + e);
			}
			_cache[acc.type] = value;
			return value;
		}

		private static int Measure(int type)
		{
			if (_scratch == null)
			{
				_scratch = new Player();
				_scratch.whoAmI = SoldierNPC.DummySlot;
				_scratch.active = true; _scratch.dead = false; _scratch.ghost = false;
				_scratch.selectedItem = 0;
			}
			var p = _scratch;
			int baseline = Run(p, 0);
			int with = Run(p, type);
			return with - baseline;
		}

		private static int Run(Player p, int accessoryType)
		{
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			for (int i = 0; i < p.dye.Length; i++) p.dye[i] = new Item();
			if (accessoryType != 0) p.armor[3].SetDefaults(accessoryType);
			p.inventory[0] = new Item();
			p.selectedItem = 0;
			p.ResetEffects();
			p.UpdateEquips(SoldierNPC.DummySlot);
			p.UpdateArmorSets(SoldierNPC.DummySlot);
			int aggro = p.aggro;
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item(); // leave the scratch player clean
			return aggro;
		}

		public static void Reset() { _cache.Clear(); _scratch = null; _broken = false; }
	}

	/// <summary>Clears <see cref="AccessoryAggro"/>'s cache and scratch Player on reload.</summary>
	public class AccessoryAggroSystem : ModSystem
	{
		public override void OnWorldLoad() => AccessoryAggro.Reset();
		public override void Unload() => AccessoryAggro.Reset();
	}
}
