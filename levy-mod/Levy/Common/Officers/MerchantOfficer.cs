using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.Buffs;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE QUARTERMASTER. The Merchant is a FRONTLINE officer, a mobile fence you drag along with the army. He throws knives at anything that strays into reach (his
	/// <see cref="OfficerWeapon"/>), and his kit is the trade: soldiers near him are handed the basic stock they can use (ammo for the weapon in hand, a potion if they would
	/// drink one) and kept under a small Morale lift, while <see cref="MerchantFenceGlobalNPC"/> fences the loot of every kill that falls near him into the treasury.
	/// </summary>
	public static class MerchantOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Merchant,
			Key = "Merchant",
			CostMult = MerchantCostMult,
			Aggro = MerchantAggro,
			Posture = OfficerPosture.Frontline,          // marches with the army and fights what comes in reach
			// The throwing knives: a vanilla Merchant's own knife, thrown faster and more often. A far throw is aimed above the target to meet gravity.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.ThrowingKnife,
				Speed = MerchantKnifeSpeed, GravityCorrection = MerchantKnifeGravityCorrection,
				Damage = MerchantKnifeDamage, Knockback = MerchantKnifeKnockback,
				Delay = 1, Cooldown = MerchantKnifeCooldown, RandomCooldown = MerchantKnifeRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Merchant]),
				RangeTiles = MerchantKnifeRangeTiles,
			},
			Kit = new MerchantKit(),
		};
	}

	/// <summary>
	/// The Merchant's ability, two pulses on their own cadences. THE MORALE LIFT: every <see cref="MerchantMoraleCadenceTicks"/> soldiers within
	/// <see cref="MerchantMoraleRadiusTiles"/> tiles are kept under Morale (refreshed, never stacked). THE HANDOUTS: every <see cref="MerchantSupplyCadenceTicks"/> he serves up to
	/// <see cref="MerchantServePerPulse"/> soldiers within <see cref="MerchantSupplyRadiusTiles"/> tiles from his stock, each paid from the treasury at the item's price: a soldier who would
	/// drink a potion on his own is handed one (healing if hurt, mana if spent; it shares the soldier's potion sickness, so it can never out-heal what an armory could), else a soldier whose ammo pouch
	/// is low is handed basic ammo of the kind his weapon takes. The fence on kills is <see cref="MerchantFenceGlobalNPC"/>.
	/// </summary>
	public sealed class MerchantKit : OfficerKit
	{
		private sealed class State { public int MoraleTimer, SupplyTimer; }
		private int[] _morale;

		public override object CreateState() => new State();

		// {0} fence reach (tiles), {1} loot value the treasury is credited per kill (percent of the kill's own), {2} handout reach (tiles), {3} morale reach (tiles)
		public override object[] DescriptionArgs => new object[]
		{
			MerchantFenceRadiusTiles, (int)Math.Round(MerchantFenceValueMult * 100f), MerchantSupplyRadiusTiles, MerchantMoraleRadiusTiles,
		};

		public override void Tick(OfficerContext ctx)
		{
			if (ctx.State is not State st) return;

			if (++st.MoraleTimer >= MerchantMoraleCadenceTicks)
			{
				st.MoraleTimer = 0;
				AuraKit.Pulse(ctx.Npc.Center, _morale ??= new[] { ModContent.BuffType<MoraleBuff>() }, MerchantMoraleRadiusTiles, MerchantMoraleDurationTicks, includeOfficers: false);
			}

			if (++st.SupplyTimer >= MerchantSupplyCadenceTicks)
			{
				st.SupplyTimer = 0;
				HandOut(ctx);
			}
		}

		/// <summary>Serves up to <see cref="MerchantServePerPulse"/> soldiers in reach, a potion first and else ammo, and tells clients the treasury changed.</summary>
		private static void HandOut(OfficerContext ctx)
		{
			BarracksTE payer = ctx.Barracks;
			if (payer == null) return;                                       // no settlement to pay him: he does not give stock away
			Vector2 from = ctx.Npc.Center;
			float reach = MerchantSupplyRadiusTiles * LevyConstants.TilePx;
			float reachSq = reach * reach;
			int served = 0;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (served >= MerchantServePerPulse) break;
				if (s == null || !s.IsAlive || s.Record == null) continue;
				if (s.State == SoldierState.Desert || s.State == SoldierState.Dead) continue;
				if (Vector2.DistanceSquared(from, s.NPC.Center) > reachSq) continue;
				if (HandPotion(s, payer) || HandAmmo(s, payer)) served++;
			}
			if (served > 0) RosterSystem.SyncBarracks(payer);                // the treasury changed: clients' barracks panel shows it
		}

		/// <summary>
		/// A potion for a soldier who would drink one on his own — wounded below the usual drink line, or a caster run low on mana. Healing wins when both apply. Respects and sets the soldier's potion
		/// sickness, exactly as an armory potion does. Paid at the potion's price; no sale if the treasury cannot cover it. True when something was handed over.
		/// </summary>
		private static bool HandPotion(SoldierNPC s, BarracksTE payer)
		{
			if (s.PotionSickness > 0) return false;
			NPC body = s.NPC;
			bool wantHeal = body.life * 100 < body.lifeMax * LevyConstants.PotionDrinkBelowPercent;
			bool wantMana = s.Record.MaxMana > 0 && s.Record.Mana * 100 < s.Record.MaxMana * LevyConstants.ManaPotionDrinkBelowPercent;
			if (!wantHeal && !wantMana) return false;

			int type = wantHeal ? HealingPotionFor() : ItemID.LesserManaPotion;
			if (!ContentSamples.ItemsByType.TryGetValue(type, out Item stock) || stock == null) return false;
			if (!EconomySystem.TryPay(payer, Math.Max(1, stock.value))) return false;

			if (wantHeal)
			{
				int healed = Math.Min(body.lifeMax - body.life, stock.healLife);
				body.life += healed;
				s.Record.CurrentHp = body.life;
				if (healed > 0) body.HealEffect(healed, true);
			}
			else
			{
				s.Record.Mana = Math.Min(s.Record.MaxMana, s.Record.Mana + stock.healMana);
			}
			s.PotionSickness = LevyConstants.PotionSicknessTicks;
			body.netUpdate = true;
			return true;
		}

		/// <summary>The healing potion he stocks: the one a town shop of that stage of the world would sell, so a late-game soldier is not handed a lesser one.</summary>
		private static int HealingPotionFor() =>
			NPC.downedMoonlord ? ItemID.SuperHealingPotion
			: NPC.downedPlantBoss ? ItemID.GreaterHealingPotion
			: Main.hardMode ? ItemID.HealingPotion
			: ItemID.LesserHealingPotion;

		/// <summary>
		/// Basic ammo for a soldier whose ammo pouch is low: <see cref="MerchantAmmoHandout"/> rounds of the cheap ammo his weapon takes (arrows, bullets, darts, rockets, gel, flares). He never
		/// mixes ammo: a pouch already holding something else is left alone. Paid at the item's price for what the pouch actually took. True when something was handed over.
		/// </summary>
		private static bool HandAmmo(SoldierNPC s, BarracksTE payer)
		{
			SoldierGear gear = s.Record.Gear;
			Item weapon = gear.Weapon;
			if (weapon == null || weapon.IsAir || weapon.useAmmo <= 0) return false;
			Item pouch = gear.Ammo;
			if (!pouch.IsAir && pouch.stack >= MerchantAmmoLowBelow) return false;

			int idx = Array.IndexOf(MerchantAmmoClass, weapon.useAmmo);
			if (idx < 0) return false;                                       // a class he does not stock
			int type = MerchantAmmoItem[idx];
			if (!pouch.IsAir && pouch.type != type) return false;            // the pouch holds other ammo: never mixed
			if (!ContentSamples.ItemsByType.TryGetValue(type, out Item stock) || stock == null) return false;

			long unit = Math.Max(1, stock.value);
			if (!EconomySystem.CanAfford(payer, unit * MerchantAmmoHandout)) return false;
			int taken = MerchantAmmoHandout - s.GiveAmmo(type, MerchantAmmoHandout);   // GiveAmmo returns what did not fit or did not match
			if (taken <= 0) return false;
			EconomySystem.TryPay(payer, unit * taken);
			return true;
		}
	}
}
