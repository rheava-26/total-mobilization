using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Modded weapons that refuse to be used by anyone until a world milestone: the item's own <c>CanUseItem</c> says no (a player holding it gets nothing). Levy never calls the primary <c>CanUseItem</c>, so a levy
	/// would swing / cast it as a dud. Each row names the weapon, the milestone flag it waits for and the reason, read from the item's own code (explicit table, no probing of a weapon's behaviour at runtime).
	/// While <see cref="Locked"/> a weapon is not issued and a soldier already carrying it uses his sidearm (<see cref="WeaponUse.HasResources"/>); the moment the milestone is reached it is usable again.
	/// </summary>
	public static class ModWeaponGates
	{
		private readonly struct Gate { public readonly string Mod, Type, Field, Why; public Gate(string mod, string type, string field, string why) { Mod = mod; Type = type; Field = field; Why = why; } }

		/// <summary>Calamity item internal name -> the gate.</summary>
		private static readonly Dictionary<string, Gate> CalamityGates = new()
		{
			// Murasama.CanUseItem returns IDUnlocked(player) = DownedBossSystem.downedDoG: the blade cannot be used before the Devourer of Gods is downed.
			["Murasama"] = new Gate("CalamityMod", "CalamityMod.DownedBossSystem", "downedDoG", "CanUseItem is false until the Devourer of Gods is downed"),
		};

		private static readonly Dictionary<string, FieldInfo> _fields = new();

		/// <summary>True while this item's own use gate is shut in the current world.</summary>
		public static bool Locked(Item item)
		{
			if (item == null || item.IsAir || item.type < Terraria.ID.ItemID.Count || item.ModItem == null || item.ModItem.Mod.Name != "CalamityMod") return false;
			if (!CalamityGates.TryGetValue(item.ModItem.Name, out Gate g)) return false;
			return !FlagUp(g);
		}

		private static bool FlagUp(Gate g)
		{
			try
			{
				string key = g.Type + "." + g.Field;
				if (!_fields.TryGetValue(key, out FieldInfo f))
				{
					f = null;
					if (ModLoader.TryGetMod(g.Mod, out Mod m)) f = m.Code?.GetType(g.Type)?.GetField(g.Field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
					_fields[key] = f;
				}
				return f == null || (bool)f.GetValue(null); // an unreadable flag never locks a weapon
			}
			catch (Exception) { return true; }
		}

		/// <summary>Measurement only: sets a Calamity milestone flag (LEVY_CR_DOWNED=downedDoG,...) so a gated weapon can be measured as the unlocked weapon it becomes.</summary>
		internal static void LabSetFlags(string csv)
		{
			if (string.IsNullOrEmpty(csv)) return;
			foreach (string name in csv.Split(','))
			{
				try
				{
					if (!ModLoader.TryGetMod("CalamityMod", out Mod m)) return;
					FieldInfo f = m.Code?.GetType("CalamityMod.DownedBossSystem")?.GetField(name.Trim(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
					if (f != null && f.FieldType == typeof(bool)) f.SetValue(null, true);
				}
				catch (Exception) { }
			}
		}
	}
}
