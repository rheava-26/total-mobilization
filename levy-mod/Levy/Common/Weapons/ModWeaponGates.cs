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

		/// <summary>
		/// Modded weapons MEASURED unusable in levy hands (the army weapon sweep: a real player deals damage with them, a levy none, whatever the commander is doing), where the cause is beyond a table fix.
		/// They are never issued and a levy already carrying one uses his sidearm, so he fights with something that works. Mod -> item internal name -> why (mechanism only). Remove a row once the weapon is made to work.
		/// </summary>
		private static readonly Dictionary<string, Dictionary<string, string>> UnusableByLevies = new()
		{
			["StarsAbove"] = new()
			{
				["Mercy"] = "its swing is placed from its owner's use state, which for a levy's shot is the commander's: the blade appears far off the map",
				["LiberationBlazing"] = "its follow-up shots come out at the commander, not at the levy",
				["DreadnoughtChemtank"] = "the levy never finds a moment to fire it (its use needs a held state the soldier does not have)",
				["CaesuraOfDespair"] = "a companion the item summons while a player holds it; a levy never holds it that way, so nothing fights",
				["HollowheartAlbion"] = "a companion the item summons while a player holds it; a levy never holds it that way, so nothing fights",
				["KonpakuKatana"] = "a companion the item summons while a player holds it; a levy never holds it that way, so nothing fights",
				["SakuraVengeance"] = "its slash is spawned by the item's own use handler, which a levy never runs: every swing deals nothing (stopgap until levies run a modded item's use handler)",
			},
			["StarlightRiver"] = new()
			{
				["BreachCannon"] = "the tower fixes itself to a wall spot its item picks when a player fires; a levy's tower has none and lands at the corner of the world",
				["FrostFlail"] = "the flail only exists while its owner holds the button; a levy's use spawns nothing",
				["Gunchucks"] = "its shots spawn at the levy but are steered by the owner's cursor and never reach the enemy",
				["VitricMagicBook"] = "its spikes are placed for the owner and never strike the levy's enemy",
			},
		};
		private static readonly HashSet<int> _reportedUnusable = new();
		/// <summary>Self-check: every row of <see cref="UnusableByLevies"/> as (mod, item name).</summary>
		internal static IEnumerable<(string mod, string name)> UnusableRows() { foreach (var m in UnusableByLevies) foreach (var r in m.Value) yield return (m.Key, r.Key); }

		/// <summary>True for a weapon in <see cref="UnusableByLevies"/>.</summary>
		internal static bool UnusableByLevy(Item item, out string why)
		{
			why = null;
			return item?.ModItem != null && UnusableByLevies.TryGetValue(item.ModItem.Mod.Name, out var t) && t.TryGetValue(item.ModItem.Name, out why);
		}

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
			if (UnusableByLevy(item, out string why))
			{
				if (_reportedUnusable.Add(item.type)) ModContent.GetInstance<Levy>()?.Logger.Info($"[Gate] {item.ModItem.FullName} is not issued to levies: {why}");
				return true;
			}
			return ProbeLocked(item);
		}

		/// <summary>
		/// The general rule: a modded WEAPON whose own <c>CanUseItem</c> refuses a pristine player (full mana, plenty of minion and sentry slots, none of its own projectiles out, no alt-fire) is locked by something the world has not reached (a boss not
		/// downed, a quest not done), so nobody can use it and the armory does not issue it. Read from the item's real code, cached per item type for 3 s; any failure reads as "usable".
		/// </summary>
		private static bool ProbeLocked(Item item)
		{
			if (item.damage <= 0 || item.consumable || item.accessory || item.ammo > 0 || item.mountType >= 0 || item.createTile >= 0 || (item.shoot <= 0 && item.useStyle == 0)) return false;
			// a held-item weapon whose use is shut for good (CanUseItem is the constant false: Halley's Inferno, the Starfleet, the Starmada) is used through the holdout the mod spawns for whoever holds it, not locked by the world
			if (ModShotKinds.HoldSpawned(item) && ModShotKinds.Of(item) == ModShotKind.Holdout) return false;
			uint now = Main.GameUpdateCount;
			if (_probed.TryGetValue(item.type, out var hit) && now - hit.tick < 180 && now >= hit.tick) return hit.locked;
			bool locked = false;
			try
			{
				var p = _probePlayer ??= new Player();
				p.ResetEffects(); p.statMana = p.statManaMax2 = 1000; p.statLife = p.statLifeMax2 = 500; p.altFunctionUse = 0;
				// a summon staff that asks for a number of minion slots (Calamity's Cosmilamp 2, Kingof Constellations 4, Temporal Umbrella 5, Endogenesis 10, Lilies of Finality 2 free) is asking about its holder's gear, not the world:
				// the probe player has plenty, as SummonCap's does. With the bare 1 slot of a fresh player every such staff read as locked and was never issued (summonsweep 2026-11-22: eight post-Moon Lord Calamity staves).
				p.maxMinions = 100; p.slotsMinions = 0f; p.maxTurrets = 100;
				Array.Clear(p.ownedProjectileCounts, 0, p.ownedProjectileCounts.Length);
				var sample = new Item(); sample.SetDefaults(item.type);
				Item saved = p.inventory[0]; int savedSel = p.selectedItem;
				p.inventory[0] = sample; p.selectedItem = 0;
				// a placement check in the item's own CanUseItem (a tower or nest that asks "is the tile under the cursor free?") reads the STATIC cursor tile, which is the commander's: judged on her cursor the staff read as
				// locked whenever she pointed at the ground. The probe asks about an open sky tile, as a player with a free spot to place it would be asked.
				int savedTx = Player.tileTargetX, savedTy = Player.tileTargetY;
				try { Player.tileTargetX = Math.Max(10, Main.maxTilesX / 2); Player.tileTargetY = 12; HoldForProbe(sample, p); locked = !ItemLoader.CanUseItem(sample, p); }
				finally { p.inventory[0] = saved; p.selectedItem = savedSel; Player.tileTargetX = savedTx; Player.tileTargetY = savedTy; }
			}
			catch (Exception) { locked = false; }
			_probed[item.type] = (now, locked);
			if (locked) { ProbeLocks++; if (_reported.Add(item.type)) ModContent.GetInstance<Levy>()?.Logger.Info($"[Gate] {item.Name} ({item.ModItem?.FullName}) refuses use from a pristine player (its own CanUseItem): not issued, not used until the world allows it"); }
			return locked;
		}

		/// <summary>
		/// A summon staff whose own HoldItem sets what its CanUseItem reads (the Abandoned Slime Staff counts its holder's free slots there and refuses a use with none) is held by the probe first, as a player holds it when she
		/// uses it: without that every such staff read as locked and was never issued. The probe player stands on a slot nothing uses (its "my minions" scan finds none), and anything the hook makes is ended at once.
		/// </summary>
		private static void HoldForProbe(Item sample, Player p)
		{
			if (sample.ModItem == null || !(sample.CountsAsClass(DamageClass.Summon) || sample.sentry)) return;
			var hold = sample.ModItem.GetType().GetMethod("HoldItem", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Player) }, null);
			if (hold == null || hold.DeclaringType == typeof(ModItem)) return;
			int who = p.whoAmI; p.whoAmI = Main.maxPlayers - 4;
			var before = new bool[Main.maxProjectiles];
			for (int i = 0; i < Main.maxProjectiles; i++) before[i] = Main.projectile[i].active;
			try { sample.ModItem.HoldItem(p); }
			catch (Exception) { }
			finally
			{
				p.whoAmI = who;
				for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active && !before[i]) Main.projectile[i].active = false;
			}
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
