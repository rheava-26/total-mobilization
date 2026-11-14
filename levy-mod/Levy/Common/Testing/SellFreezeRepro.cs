using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Logistics;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Reproduces the sell-page freeze: a big armory, a town of shopkeepers in reach, then what the Logistics sell page does on "Select matching": Selling.Select over the storage, and the sell summary that follows
	/// (Selling.BestBuyer over every selected item, re-run about once a second while the page is open). Env LEVY_SELLREPRO=1 (LEVY_PERFSCENE_SLOTS, default 3000; LEVY_SELLREPRO_KEEPERS, default 16) runs it after recipes
	/// are final and logs "[SellRepro]" lines with ms per call. It uses only calls that exist on both sides of the pricing fix, so the same file times the old and the new code. Also supplies the stock builder the cost scene reuses.
	/// </summary>
	public class SellFreezeRepro : ModSystem
	{
		public override void PostSetupRecipes()
		{
			if (Environment.GetEnvironmentVariable("LEVY_SELLREPRO") != "1") return;
			global::Levy.Common.Util.LoadSafe.Run(Run);
		}

		internal static int EnvInt(string name, int def) => int.TryParse(Environment.GetEnvironmentVariable(name), out int v) && v > 0 ? v : def;

		/// <summary>An unplaced armory filled with a mix of every issuable weapon, armor piece, accessory, ammo and potion the loaded mods have (40% weapons, 20% armor, 20% accessories, 10% ammo/potions, 10% empty).</summary>
		internal static ArmoryTE BuildStock(int slots, out int filled, out string kinds)
		{
			var weapons = new List<int>(); var armor = new List<int>(); var acc = new List<int>(); var ammo = new List<int>(); var potions = new List<int>();
			foreach (var kv in ContentSamples.ItemsByType)
			{
				var it = kv.Value; if (it == null || it.IsAir || it.type <= 0) continue;
				if (it.damage > 0 && it.maxStack == 1 && it.pick == 0 && it.axe == 0 && it.hammer == 0 && !it.accessory && WeaponClassifier.IsIssuableWeapon(it)) weapons.Add(it.type);
				else if (it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0) { if (!it.vanity) armor.Add(it.type); }
				else if (WeaponClassifier.IsUsefulAccessory(it)) acc.Add(it.type);
				else if (it.ammo > 0 && it.damage > 0) ammo.Add(it.type);
				else if (it.consumable && (it.healLife > 0 || it.healMana > 0 || it.buffType > 0)) potions.Add(it.type);
			}
			var rnd = new Random(20261113);
			void Shuffle(List<int> l) { for (int i = l.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); } }
			Shuffle(weapons); Shuffle(armor); Shuffle(acc); Shuffle(ammo); Shuffle(potions);
			var te = new ArmoryTE(); te.Storage = new Item[slots];
			for (int i = 0; i < slots; i++) te.Storage[i] = new Item();
			Item Make(int type, int stack) { var it = new Item(); it.SetDefaults(type); it.stack = Math.Min(stack, Math.Max(1, it.maxStack)); return it; }
			int w = 0, a = 0, c = 0, m = 0, p = 0; filled = 0;
			for (int i = 0; i < slots; i++)
			{
				int roll = i % 10; int type = 0, stack = 1;
				if (roll < 4 && weapons.Count > 0) type = i < 25 * 10 && i % 10 == 0 ? weapons[0] : weapons[w++ % weapons.Count]; // about 25 copies of the first weapon
				else if (roll < 6 && armor.Count > 0) type = armor[a++ % armor.Count];
				else if (roll < 8 && acc.Count > 0) type = acc[c++ % acc.Count];
				else if (roll == 8 && ammo.Count > 0) { type = ammo[m++ % ammo.Count]; stack = 999; }
				else if (roll == 9 && potions.Count > 0) { type = potions[p++ % potions.Count]; stack = 30; }
				if (type == 0) continue;
				te.Storage[i] = Make(type, stack); filled++;
			}
			kinds = $"{weapons.Count} weapon kinds, {armor.Count} armor, {acc.Count} accessories, {ammo.Count} ammo, {potions.Count} potions in the loaded mods";
			return te;
		}

		private static readonly short[] Town =
		{
			NPCID.Merchant, NPCID.Nurse, NPCID.ArmsDealer, NPCID.Dryad, NPCID.Demolitionist, NPCID.Clothier, NPCID.GoblinTinkerer, NPCID.Wizard, NPCID.Mechanic, NPCID.Truffle,
			NPCID.Steampunker, NPCID.DyeTrader, NPCID.PartyGirl, NPCID.Cyborg, NPCID.Painter, NPCID.WitchDoctor, NPCID.Pirate, NPCID.Stylist, NPCID.Angler, NPCID.TaxCollector, NPCID.Golfer, NPCID.BestiaryGirl, NPCID.Princess,
		};

		/// <summary>Puts town NPCs (shopkeepers) into the first NPC slots at <paramref name="at"/>, active and settled. Returns an undo.</summary>
		internal static Action PlaceTown(int count, Microsoft.Xna.Framework.Vector2 at)
		{
			var saved = new List<(int, NPC)>();
			for (int i = 0; i < count && i < 190; i++)
			{
				saved.Add((i, Main.npc[i]));
				var n = new NPC(); n.SetDefaults(Town[i % Town.Length]); n.whoAmI = i; n.active = true; n.homeless = false; n.homeTileX = (int)(at.X / 16f) + i; n.homeTileY = (int)(at.Y / 16f);
				n.position = at; Main.npc[i] = n;
			}
			return () => { foreach (var (i, n) in saved) Main.npc[i] = n; };
		}

		/// <summary>Makes a payer exist (a dedicated server with no world has no active player). Returns an undo.</summary>
		internal static Action EnsurePayer(Microsoft.Xna.Framework.Vector2 at)
		{
			if (Main.netMode == NetmodeID.SinglePlayer) return () => { };
			var p = Main.player[0]; bool was = p.active; var pos = p.position; p.active = true; p.position = at;
			return () => { p.active = was; p.position = pos; };
		}

		private static void Run()
		{
			int slots = EnvInt("LEVY_PERFSCENE_SLOTS", 3000), keepers = EnvInt("LEVY_SELLREPRO_KEEPERS", 16);
			var log = Levy.Instance?.Logger; Action undoNpc = null, undoPlayer = null;
			double Ms(long t0) => (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
			try
			{
				try { ProgressionTier.EnsureBuilt(m => log?.Info("[SellRepro] " + m)); } catch (Exception) { }
				var te = BuildStock(slots, out int filled, out string kinds);
				var at = te.Center; undoNpc = PlaceTown(keepers, at); undoPlayer = EnsurePayer(at);
				int reach = Shopping.Keepers(te).Count; var payer = Shopping.Payer(te);
				log?.Info($"[SellRepro] stock: {filled}/{slots} slots ({kinds}); {keepers} town NPCs placed, {reach} shopkeepers in reach, payer={(payer == null ? "NONE" : payer.name + "#" + payer.whoAmI)}");
				var tiers = Selling.Tiers;
				var cases = new List<(string, Selling.Filter)>();
				for (int i = 0; i < tiers.Length; i++) cases.Add(("tier " + tiers[i].Key, new Selling.Filter { TierLo = tiers[i].Lo, TierHi = tiers[i].Hi }));
				cases.Add(("every tier", new Selling.Filter { TierLo = 0f, TierHi = 99f }));
				cases.Add(("category Melee", new Selling.Filter { Category = SellCat.Melee }));
				foreach (var (name, f) in cases)
				{
					var res = new List<(double sel, double info)>(); int n = 0;
					for (int rep = 0; rep < 3; rep++)
					{
						long t = Stopwatch.GetTimestamp(); var pick = Selling.Select(te, f); double sel = Ms(t); n = pick.Count;
						t = Stopwatch.GetTimestamp(); var (buyer, total) = Selling.BestBuyer(te, pick); double info = Ms(t);
						res.Add((sel, info));
					}
					log?.Info($"[SellRepro] {name,-22} selected {n,5} slots: Select {res.Average(r => r.sel),9:F2} ms, sell summary (BestBuyer, {reach} shopkeepers) {res.Average(r => r.info),11:F1} ms (runs {string.Join("/", res.Select(r => r.info.ToString("F0")))})");
				}
			}
			catch (Exception e) { log?.Warn("[SellRepro] threw " + e.GetType().Name + ": " + e.Message + " " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim()))); }
			finally { undoPlayer?.Invoke(); undoNpc?.Invoke(); ProgressionTier.Reset(); }
		}
	}
}
