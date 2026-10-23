using System;
using Terraria;
using Terraria.ID;
using Levy.Common.Systems;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE QUARTERMASTER. The Merchant is a FRONTLINE officer that marches with the line and fires a coin gun at any enemy in reach, and whose kit
	/// banks a modest trickle of coin into the settlement that commissioned him. The shot is his <see cref="OfficerWeapon"/>; the income is the kit.
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
			// The coin gun: a platinum round, fast and heavy (spawned with noDropItem, so nothing is minted from a shot).
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.PlatinumCoin,
				Speed = MerchantCoinSpeed, GravityCorrection = MerchantCoinGravityCorrection,
				Damage = MerchantCoinDamage, Knockback = MerchantCoinKnockback,
				Delay = 1, Cooldown = MerchantCoinCooldown, RandomCooldown = MerchantCoinRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Merchant]),
				RangeTiles = MerchantCoinRangeTiles,
			},
			Kit = new MerchantKit(),
		};
	}

	/// <summary>
	/// The Merchant's ability: every <see cref="LevyConstants.Officer.MerchantCoinIntervalTicks"/> he banks a modest sum into the commissioning
	/// barracks' treasury — <c>LevyWage / <see cref="LevyConstants.Officer.MerchantCoinWageDivisor"/></c>, so it scales with the world without
	/// paying for itself. No barracks to bank into, no deposit.
	/// </summary>
	public sealed class MerchantKit : OfficerKit
	{
		private sealed class State { public int Timer; }

		public override object CreateState() => new State();

		// {0} seconds between treasury deposits
		public override object[] DescriptionArgs => new object[] { MerchantCoinIntervalTicks / 60 };

		public override void Tick(OfficerContext ctx)
		{
			if (ctx.State is not State st) return;
			if (++st.Timer < MerchantCoinIntervalTicks) return;
			st.Timer = 0;
			if (ctx.Barracks == null) return;                 // no settlement to bank into

			long amount = Math.Max(1, EconomySystem.LevyWage / MerchantCoinWageDivisor);
			EconomySystem.Deposit(ctx.Barracks, amount);
			RosterSystem.SyncBarracks(ctx.Barracks);          // the treasury changed: clients' barracks panel shows it
		}
	}
}
