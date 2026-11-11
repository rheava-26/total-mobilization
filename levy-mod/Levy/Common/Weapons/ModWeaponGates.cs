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

		private static readonly Dictionary<string, MemberInfo> _fields = new();
		private static readonly Dictionary<int, (uint tick, bool locked)> _probed = new();
		private static Player _probePlayer;
		private static readonly HashSet<int> _reported = new();
		/// <summary>Lab counters: items found locked by their own CanUseItem (not in the table).</summary>
		internal static int ProbeLocks;

		/// <summary>True while this item's own use gate is shut in the current world.</summary>
		public static bool Locked(Item item)
		{
			if (item == null || item.IsAir || item.type < Terraria.ID.ItemID.Count || item.ModItem == null) return false;
			if (item.ModItem.Mod.Name == "CalamityMod" && CalamityGates.TryGetValue(item.ModItem.Name, out Gate g)) return !FlagUp(g);
			return ProbeLocked(item);
		}

		/// <summary>
		/// The general rule: a modded WEAPON whose own <c>CanUseItem</c> refuses a pristine player (full mana, none of its own projectiles out, no alt-fire) is locked by something the world has not reached (a boss not
		/// downed, a quest not done), so nobody can use it and the armory does not issue it. Read from the item's real code, cached per item type for 3 s; any failure reads as "usable".
		/// </summary>
		private static bool ProbeLocked(Item item)
		{
			if (item.damage <= 0 || item.consumable || item.accessory || item.ammo > 0 || item.mountType >= 0 || item.createTile >= 0 || (item.shoot <= 0 && item.useStyle == 0)) return false;
			uint now = Main.GameUpdateCount;
			if (_probed.TryGetValue(item.type, out var hit) && now - hit.tick < 180 && now >= hit.tick) return hit.locked;
			bool locked = false;
			try
			{
				var p = _probePlayer ??= new Player();
				p.ResetEffects(); p.statMana = p.statManaMax2 = 1000; p.statLife = p.statLifeMax2 = 500; p.altFunctionUse = 0;
				Array.Clear(p.ownedProjectileCounts, 0, p.ownedProjectileCounts.Length);
				var sample = new Item(); sample.SetDefaults(item.type);
				Item saved = p.inventory[0]; int savedSel = p.selectedItem;
				p.inventory[0] = sample; p.selectedItem = 0;
				try { locked = !ItemLoader.CanUseItem(sample, p); }
				finally { p.inventory[0] = saved; p.selectedItem = savedSel; }
			}
			catch (Exception) { locked = false; }
			_probed[item.type] = (now, locked);
			if (locked) { ProbeLocks++; if (_reported.Add(item.type)) ModContent.GetInstance<Levy>()?.Logger.Info($"[Gate] {item.Name} ({item.ModItem?.FullName}) refuses use from a pristine player (its own CanUseItem): not issued, not used until the world allows it"); }
			return locked;
		}

		private static bool FlagUp(Gate g)
		{
			try
			{
				string key = g.Type + "." + g.Field;
				if (!_fields.TryGetValue(key, out MemberInfo f))
				{
					f = null;
					if (ModLoader.TryGetMod(g.Mod, out Mod m))
					{
						Type t = m.Code?.GetType(g.Type); // Calamity 2.x keeps its milestones as static PROPERTIES, older builds as fields
						f = (MemberInfo)t?.GetProperty(g.Field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) ?? t?.GetField(g.Field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
					}
					_fields[key] = f;
				}
				if (f == null) return true; // an unreadable flag never locks a weapon
				object v = f is PropertyInfo pi ? pi.GetValue(null) : ((FieldInfo)f).GetValue(null);
				return v is not bool b || b;
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
					Type t = m.Code?.GetType("CalamityMod.DownedBossSystem");
					PropertyInfo pr = t?.GetProperty(name.Trim(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
					if (pr != null && pr.PropertyType == typeof(bool) && pr.CanWrite) { pr.SetValue(null, true); continue; }
					FieldInfo f = t?.GetField(name.Trim(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
					if (f != null && f.FieldType == typeof(bool)) f.SetValue(null, true);
				}
				catch (Exception) { }
			}
		}
	}
}
