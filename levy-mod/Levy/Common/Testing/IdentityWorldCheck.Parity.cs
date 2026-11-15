using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Owner;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>Counts the projectiles that appear inside a levy's gear frame while a parity run is on (by type): what gear casts for a levy.</summary>
	public class ParityCensus : GlobalProjectile
	{
		public static bool On; public static readonly Dictionary<int, int> Spawns = new();
		public override void OnSpawn(Projectile projectile, IEntitySource source) { if (On && GearUpkeep.Body != null) Spawns[projectile.type] = Spawns.TryGetValue(projectile.type, out int n) ? n + 1 : 1; }
	}

	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- gear parity: the identity guard costs a levy nothing
		/// <summary>
		/// What the identity guard may NOT change: what a levy's gear does. Every modded armor piece and accessory of every loaded mod is worn by one levy; the world is stepped, and the
		/// dummy's whole state (every scalar field of the Player and of every ModPlayer) plus the projectiles that appeared are fingerprinted once with the guard off and once with it on. A run with the guard off
		/// is repeated first, and fields that differ between those two identical runs are noise and ignored. A difference that remains is something the guard changed for that item.
		/// </summary>
		private static void GearParity()
		{
			// unset or 0 skips: a headless world makes some gear throw by itself (the keybind a Calamity accessory reads is not registered on a server) and every caught exception is a log line the ship counts;
			// 1 runs every item, s a sample of about 120 spread over the list
			string pmode = Environment.GetEnvironmentVariable("LEVY_IDENTITY_PARITY");
			if (pmode != "1" && pmode != "s") return;
			int cap = int.TryParse(Environment.GetEnvironmentVariable("LEVY_IDENTITY_PARITY_MAX"), out int c) ? c : 100000;
			var items = new List<(int type, int slot, string label)>();
			for (int i = 1; i < ItemLoader.ItemCount && items.Count < cap; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item s) || s == null || s.IsAir) continue;
				string mod = s.ModItem?.Mod.Name;
				if (mod == "Levy" || mod == "Mechanization" || mod == "Warbands" || mod == "AlchemistNPCLite") continue;
				bool vanillaKeep = i == ItemID.VolatileGelatin || i == ItemID.StarCloak || i == ItemID.StarVeil || i == ItemID.BeeCloak || i == ItemID.CrimsonCloak;
				if (i < ItemID.Count && !vanillaKeep) continue;
				Item it = new Item(); it.SetDefaults(i);
				var g = new SoldierGear();
				int slot = -1;
				if (s.accessory && !s.vanity && g.CanHold(SoldierGear.SlotWings, it)) slot = SoldierGear.SlotWings;
				else if (s.accessory && !s.vanity && g.CanHold(SoldierGear.SlotBoots, it)) slot = SoldierGear.SlotBoots;
				else if (s.accessory && !s.vanity && g.CanHold(SoldierGear.SlotAcc0, it)) slot = SoldierGear.SlotAcc0;
				else if (s.headSlot >= 0 && !s.vanity && g.CanHold(SoldierGear.SlotHead, it)) slot = SoldierGear.SlotHead;
				else if (s.bodySlot >= 0 && !s.vanity && g.CanHold(SoldierGear.SlotBody, it)) slot = SoldierGear.SlotBody;
				else if (s.legSlot >= 0 && !s.vanity && g.CanHold(SoldierGear.SlotLegs, it)) slot = SoldierGear.SlotLegs;
				if (slot < 0) continue;
				items.Add((i, slot, (mod ?? "Terraria") + ":" + s.Name));
			}
			if (items.Count == 0) { Note("gear parity: no modded armor or accessory is loaded, nothing to compare"); return; }
			if (pmode == "s" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_IDENTITY_PARITY_ONLY"))) { int stride = Math.Max(1, items.Count / 120); items = items.Where((it, k) => k % stride == 0).ToList(); }
			string only = Environment.GetEnvironmentVariable("LEVY_IDENTITY_PARITY_ONLY"); // lab: a ';' list of item names to re-check
			if (!string.IsNullOrEmpty(only)) { var names = only.Replace('_', ' ').Split(';'); items = items.Where(it => names.Any(n => it.label.EndsWith(":" + n))).ToList(); }
			bool was = IdentityGuard.Disabled;
			var diffItems = new List<string>(); int done = 0, threw = 0; var keyTally = new Dictionary<string, int>();
			var sw = Stopwatch.StartNew();
			try
			{
				foreach (var (type, slot, label) in items)
				{
					try
					{
						done++;
						var (real, spawnsDiffer, spawnA, spawnB) = ParityCompare(type, slot);
						if (real.Count > 0 || spawnsDiffer)
						{
							// a difference counts only when a second, independent comparison finds the same thing (a one-off is the world's own noise: a periodic event landing in one run's window)
							var (real2, spawnsDiffer2, _, _) = ParityCompare(type, slot);
							real.IntersectWith(real2); spawnsDiffer &= spawnsDiffer2;
							if (real.Count > 0 || spawnsDiffer)
							{
								diffItems.Add($"{label} [{string.Join(", ", real.Take(6))}{(real.Count > 6 ? ", +" + (real.Count - 6) : "")}]{(spawnsDiffer ? $" spawns off: {spawnA} / on: {spawnB}" : "")}");
								foreach (string k in real) keyTally[k] = keyTally.TryGetValue(k, out int n) ? n + 1 : 1;
							}
						}
					}
					catch (Exception e) { threw++; if (threw <= 3) Note($"gear parity: {label} threw {e.GetType().Name}: {e.Message}"); }
				}
			}
			finally { IdentityGuard.Disabled = was; ParityCensus.On = false; }
			Note($"gear parity: {done} of {items.Count} modded armor pieces and accessories fingerprinted (threw {threw}) in {sw.ElapsedMilliseconds} ms; guard dispatches hidden by mod: {string.Join(", ", IdentityGuard.HiddenBy.Select(kv => kv.Key + "=" + kv.Value))}; mirrored by mod: {string.Join(", ", IdentityGuard.MirroredBy.Select(kv => kv.Key + "=" + kv.Value))}");
			foreach (string d in diffItems.Take(60)) Note("gear parity, guard changes this item: " + d);
			if (diffItems.Count > 60) Note($"gear parity: {diffItems.Count - 60} more items differ");
			Note("gear parity, fields the guard moved (item count): " + string.Join(", ", keyTally.OrderByDescending(kv => kv.Value).Take(25).Select(kv => kv.Key + "=" + kv.Value)));
			Check($"gear parity: the identity guard changes no stat, ModPlayer field or cast of {done} modded gear items (changed: {diffItems.Count})", done > 0 && diffItems.Count == 0 && threw == 0);
		}

		/// <summary>One comparison: the guard off twice (what differs between those is noise), then on. Returns the fields and casts that differ beyond the noise.</summary>
		private static (HashSet<string> real, bool spawnsDiffer, string spawnA, string spawnB) ParityCompare(int type, int slot)
		{
			var a1 = ParityRun(type, slot, true);
			var a2 = ParityRun(type, slot, true);
			var b = ParityRun(type, slot, false);
			var noise = new HashSet<string>(); PlayerProbe.Differences(a1.snap, a2.snap, noise);
			var real = new HashSet<string>(); PlayerProbe.Differences(a1.snap, b.snap, real); real.ExceptWith(noise);
			string spawnA = SpawnText(a1.spawns), spawnB = SpawnText(b.spawns), spawnNoise = SpawnText(a2.spawns);
			return (real, spawnA != spawnB && spawnA == spawnNoise, spawnA, spawnB);
		}

		private static string SpawnText(Dictionary<int, int> d) => string.Join(",", d.OrderBy(kv => kv.Key).Select(kv => kv.Key + "x" + kv.Value));

		private static (Dictionary<string, string> snap, Dictionary<int, int> spawns) ParityRun(int itemType, int slot, bool guardOff)
		{
			IdentityGuard.Disabled = guardOff;
			ClearSoldiers();
			Main.rand = new Terraria.Utilities.UnifiedRandom(4242);
			Item w = new Item(); w.SetDefaults(ItemID.CopperShortsword);
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Parity",
				CommanderName = "IdentityTester", CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true);
			Item it = new Item(); it.SetDefaults(itemType);
			rec.Gear.Set(slot, it); rec.Gear.SetLocked(slot, true);
			rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow;
			RosterSystem.Instance.Regulars.Add(rec);
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) throw new Exception("levy did not spawn");
			Main.npc[body].Bottom = at;
			ParityCensus.Spawns.Clear(); ParityCensus.On = true;
			Step(45);
			ParityCensus.On = false;
			var sn = (SoldierNPC)Main.npc[body].ModNPC;
			Player d = sn.Dummy;
			var snap = PlayerProbe.Snapshot(d);
			return (snap, new Dictionary<int, int>(ParityCensus.Spawns));
		}
	}
}
