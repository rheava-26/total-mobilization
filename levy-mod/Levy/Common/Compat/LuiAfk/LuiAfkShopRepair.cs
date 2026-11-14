using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Levy.Common.Compat.LuiAfk
{
	/// <summary>
	/// Runtime repairs for LuiAFK Reborn (miningcracks_take_on_luiafk, by miningcrack and TheBrutalSkull; original LuiAFK by Luiafk). Nothing of LuiAFK is copied or redistributed: everything is
	/// reached by name at runtime and only while that mod is loaded (ModLoader.TryGetMod), so with LuiAFK absent every member here is inert. Each repair answers one defect reproduced on a headless boot of
	/// tML 2026.08 and 2026.10 (see LuiAfkShopCheck for the evidence the boot prints):
	///
	///  1. Fairy Merchant shop ("LuiAFK Fairy Merchant Shop") is built in ModNPC.AddShops by copying the vanilla Merchant and then appending the 1.3-era list. Every item the 1.3 list shares with the
	///     Merchant therefore appears twice (Torch, Glowstick, Arrow, Shuriken, Rope, Iron Anvil, both potions). Duplicates are dropped at the moment the shop is filled.
	///  2. The same AddShops guards four of its items with plain `if (NPC.AnyNPCs(19))`, `if (NPC.savedMech)`, `if (NPC.savedWizard)` evaluated ONCE, while mods load, when no world exists: all four are false, so the
	///     Musket Ball, Wire, Actuator and item 500 never appear. They are added back as real shop conditions that are read every time the shop opens.
	///  3. Steampunker: LuiAFK adds Red Solution for a Crimson world on a Blood Moon or eclipse, which the vanilla shop already sells there; the duplicate is dropped at fill time.
	///  4. LuiafkPlayer.ResetEffects calls LuiafkUI.ResetUIs() for the player whose whoAmI equals Main.myPlayer, and its UI objects are only built for clients. On a dedicated server Main.myPlayer is 255,
	///     which is also the whoAmI of every detached Player a mod builds (Levy's armour and accessory reads): their ResetEffects throws a NullReferenceException out of LuiAFK. Real players (slots 0-254) are not
	///     affected. On a dedicated server the method is skipped (it only resets client UI state).
	/// Env LEVY_LUIAFK_REPAIR=0 switches all four off (lab A/B for the self-check).
	/// </summary>
	public static class LuiAfkShops
	{
		public const string ModName = "miningcracks_take_on_luiafk";
		public const string FairyShopName = "LuiAFK Fairy Merchant Shop";

		/// <summary>Lab A/B switch.</summary>
		public static readonly bool Disabled = Environment.GetEnvironmentVariable("LEVY_LUIAFK_REPAIR") == "0";

		/// <summary>The four items LuiAFK's AddShops tried to gate on world state at load time (see the class summary).</summary>
		public static readonly int[] FrozenItems = { ItemID.MusketBall, ItemID.Wire, ItemID.Actuator, 500 };

		public static bool TryGet(out Mod lui) => ModLoader.TryGetMod(ModName, out lui) && lui != null;

		public static int NpcTypeOf(string name)
		{
			if (!TryGet(out Mod lui)) return -1;
			return lui.TryFind(name, out ModNPC npc) ? npc.Type : -1;
		}

		public static Condition SavedMechanic => new Condition(Language.GetOrRegister("Mods.Levy.Compat.LuiAfk.SavedMechanic", () => "Rescued the Mechanic"), () => NPC.savedMech);
		public static Condition SavedWizard => new Condition(Language.GetOrRegister("Mods.Levy.Compat.LuiAfk.SavedWizard", () => "Rescued the Wizard"), () => NPC.savedWizard);

		/// <summary>Drops every repeated item type from a filled shop array, keeping the first and closing the gap. Returns how many were dropped.</summary>
		public static int DropDuplicates(Item[] items)
		{
			if (items == null) return 0;
			var seen = new HashSet<int>();
			int write = 0, dropped = 0;
			for (int i = 0; i < items.Length; i++)
			{
				Item it = items[i];
				if (it == null || it.IsAir) continue;
				if (!seen.Add(it.type)) { dropped++; continue; }
				items[write++] = it;
			}
			for (int i = write; i < items.Length; i++) items[i] = null; // Chest.SetupShop turns the nulls into empty slots straight after this hook
			return dropped;
		}
	}

	public class LuiAfkShopRepair : GlobalNPC
	{
		/// <summary>Counts for the self-check: how many duplicates the fill hook has dropped this session.</summary>
		public static int DuplicatesDropped;

		public override void ModifyShop(NPCShop shop)
		{
			if (LuiAfkShops.Disabled || shop.Name != LuiAfkShops.FairyShopName) return;
			int fairy = LuiAfkShops.NpcTypeOf("MobileMerchant");
			if (fairy < 0 || shop.NpcType != fairy) return;
			shop.Add(ItemID.MusketBall, Condition.NpcIsPresent(NPCID.ArmsDealer));
			shop.Add(ItemID.Wire, LuiAfkShops.SavedMechanic);
			shop.Add(ItemID.Actuator, LuiAfkShops.SavedMechanic);
			shop.Add(500, LuiAfkShops.SavedWizard);
		}

		public override void ModifyActiveShop(NPC npc, string shopName, Item[] items)
		{
			if (LuiAfkShops.Disabled || items == null) return;
			if (npc.type != NPCID.Steampunker && npc.type != LuiAfkShops.NpcTypeOf("MobileMerchant")) return;
			if (!LuiAfkShops.TryGet(out _)) return;
			DuplicatesDropped += LuiAfkShops.DropDuplicates(items);
		}
	}

	/// <summary>Repair 4: LuiAFK's UI reset on a dedicated server.</summary>
	public class LuiAfkServerRepair : ModSystem
	{
		private delegate void OrigVoid();
		public static bool Hooked;

		public override void PostSetupContent()
		{
			if (LuiAfkShops.Disabled || !Main.dedServ || !LuiAfkShops.TryGet(out Mod lui)) return;
			try
			{
				Type ui = lui.Code?.GetType("miningcracks_take_on_luiafk.UI.LuiafkUI");
				MethodInfo reset = ui?.GetMethod("ResetUIs", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
				if (reset == null) { Mod.Logger.Info("LuiAFK server repair: LuiafkUI.ResetUIs not found (the mod changed?), nothing patched."); return; }
				MonoModHooks.Add(reset, (Action<OrigVoid>)(orig => { if (!Main.dedServ) orig(); }));
				Hooked = true;
			}
			catch (Exception e) { Mod.Logger.Warn("LuiAFK server repair: could not patch LuiafkUI.ResetUIs: " + e.Message); }
		}
	}
}
