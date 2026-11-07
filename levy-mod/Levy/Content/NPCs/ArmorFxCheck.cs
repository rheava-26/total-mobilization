using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Boot self-check for the levy armor visual effects (SoldierNPC.ArmorFx.cs), logged as "[ArmorFxCheck]" lines on a dedicated-server boot: a reference Player in Ninja armor must get
	/// armorEffectDrawShadow from the same Player.SetArmorEffectVisuals call a levy makes, the afterimage history must fill, and the Ninja and Platinum sets are measured through the
	/// stat pass a levy runs (ResetEffects, UpdateEquips, UpdateArmorSets), so the numbers a designer asks about come from the real code.
	/// </summary>
	public class ArmorFxCheck : ModSystem
	{
		private static Player Wear(int h, int b, int l)
		{
			var p = new Player();
			p.armor[0] = new Item(); p.armor[0].SetDefaults(h);
			p.armor[1] = new Item(); p.armor[1].SetDefaults(b);
			p.armor[2] = new Item(); p.armor[2].SetDefaults(l);
			p.head = p.armor[0].headSlot; p.body = p.armor[1].bodySlot; p.legs = p.armor[2].legSlot;
			return p;
		}

		private static string Stats(Player p)
		{
			p.ResetEffects();
			p.head = p.armor[0].headSlot; p.body = p.armor[1].bodySlot; p.legs = p.armor[2].legSlot;
			p.UpdateEquips(250);
			p.UpdateArmorSets(250);
			var all = DamageClass.Generic;
			return $"defense={p.statDefense} setBonus='{p.setBonus}' dmg(all)={p.GetDamage(all).Additive:0.00}x melee={p.GetDamage(DamageClass.Melee).Additive:0.00}x ranged={p.GetDamage(DamageClass.Ranged).Additive:0.00}x magic={p.GetDamage(DamageClass.Magic).Additive:0.00}x "
				+ $"crit(all)={p.GetCritChance(all):0}% ranged={p.GetCritChance(DamageClass.Ranged):0}% moveSpeed={p.moveSpeed:0.00} maxRunSpeed={p.maxRunSpeed:0.0} noThrowCost={p.ammoCost80 || p.ammoCost75}/{p.ammoCost80} aggro={p.aggro} endurance={p.endurance:0.00}";
		}

		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(PostSetupRecipesCore);

		private void PostSetupRecipesCore()
		{
			if (!Main.dedServ) return;
			int pass = 0, fail = 0;
			void Check(string name, bool ok, string detail = "") { if (ok) pass++; else fail++; Mod.Logger.Info($"[ArmorFxCheck] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}"); }
			try
			{
				var ninja = Wear(ItemID.NinjaHood, ItemID.NinjaShirt, ItemID.NinjaPants);
				ninja.ResetEffects(); // clears the flags, then SetArmorEffectVisuals runs from head/body/legs, exactly the vanilla path
				ninja.head = ninja.armor[0].headSlot; ninja.body = ninja.armor[1].bodySlot; ninja.legs = ninja.armor[2].legSlot;
				ninja.SetArmorEffectVisuals(ninja);
				Check("Ninja set raises armorEffectDrawShadow", ninja.armorEffectDrawShadow, $"head {ninja.head} body {ninja.body} legs {ninja.legs}");
				ninja.position = new Microsoft.Xna.Framework.Vector2(1000f, 1000f);
				for (int t = 0; t < 9; t++) { ninja.position.X += 3f; ninja.UpdateSocialShadow(); }
				Check("Afterimage history fills from UpdateSocialShadow", ninja.shadowPos[0] != Microsoft.Xna.Framework.Vector2.Zero && ninja.shadowPos[2] != Microsoft.Xna.Framework.Vector2.Zero);
				var plat = Wear(ItemID.PlatinumHelmet, ItemID.PlatinumChainmail, ItemID.PlatinumGreaves);
				plat.ResetEffects(); plat.head = plat.armor[0].headSlot; plat.body = plat.armor[1].bodySlot; plat.legs = plat.armor[2].legSlot;
				plat.SetArmorEffectVisuals(plat);
				Check("Platinum set has no afterimage", !plat.armorEffectDrawShadow);
				Mod.Logger.Info("[ArmorFxCheck] NINJA    " + Stats(Wear(ItemID.NinjaHood, ItemID.NinjaShirt, ItemID.NinjaPants)));
				Mod.Logger.Info("[ArmorFxCheck] PLATINUM " + Stats(Wear(ItemID.PlatinumHelmet, ItemID.PlatinumChainmail, ItemID.PlatinumGreaves)));
			}
			catch (Exception e) { Check("armor fx self-check ran", false, e.GetType().Name + ": " + e.Message); }
			Mod.Logger.Info($"[ArmorFxCheck] DONE pass={pass} fail={fail}");
		}
	}
}
