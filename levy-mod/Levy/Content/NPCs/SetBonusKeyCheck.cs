using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Boot self-check for the Armor Set Bonus key (SoldierNPC.SetBonusKey.cs), logged as "[SetBonusCheck]" lines on a dedicated-server boot: a reference Player in the full Vortex set must have the
	/// set recognised, and the very call a levy makes (<c>Player.KeyDoubleTap</c>) must toggle the stealth on and off.
	/// </summary>
	public class SetBonusKeyCheck : ModSystem
	{
		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(PostSetupRecipesCore);

		private void PostSetupRecipesCore()
		{
			if (!Main.dedServ) return;
			int pass = 0, fail = 0;
			void Check(string name, bool ok, string detail = "") { if (ok) pass++; else fail++; Mod.Logger.Info($"[SetBonusCheck] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}"); }
			try
			{
				var p = new Player();
				p.armor[0] = new Item(); p.armor[0].SetDefaults(ItemID.VortexHelmet);
				p.armor[1] = new Item(); p.armor[1].SetDefaults(ItemID.VortexBreastplate);
				p.armor[2] = new Item(); p.armor[2].SetDefaults(ItemID.VortexLeggings);
				p.head = p.armor[0].headSlot; p.body = p.armor[1].bodySlot; p.legs = p.armor[2].legSlot;
				p.ResetEffects();
				p.UpdateArmorSets(250);
				Check("full Vortex set recognised on a bare Player", p.setVortex);
				bool before = p.vortexStealthActive;
				p.KeyDoubleTap(Main.ReversedUpDownArmorSetBonuses ? 1 : 0);
				Check("KeyDoubleTap switches Vortex stealth on", !before && p.vortexStealthActive);
				p.KeyDoubleTap(Main.ReversedUpDownArmorSetBonuses ? 1 : 0);
				Check("KeyDoubleTap switches Vortex stealth off", !p.vortexStealthActive);
			}
			catch (Exception e) { Check("set bonus key self-check ran", false, e.GetType().Name + ": " + e.Message); }
			Mod.Logger.Info($"[SetBonusCheck] DONE pass={pass} fail={fail}");
		}
	}
}
