using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Levy.Common.Config;
using Levy.Common.Flavor;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE PROOF OFFICER and the template for the next one. The Nurse marches with the army, shoots a hostile that wanders into her reach with her own syringe
	/// (the stock town-NPC attack, so she is a weak fighter), and heals the nearest wounded soldier on a cooldown, paying for it from the treasury of the barracks
	/// that commissioned her (the vanilla Nurse price, capped). To add an officer: write a <c>Create()</c> like this one and register it in
	/// <see cref="OfficerRegistry.RegisterAll"/>; add its Name / Desc strings under Mods.Levy.Officers.Kit.{Key} in the localization file.
	/// </summary>
	public static class NurseOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Nurse,
			Key = "Nurse",
			CostMult = 1f,
			Aggro = NurseAggro,
			// NPC.AI_007_TownEntities, type 18: Nurse Syringe (projectile 583, poisons), speed 8, damage 8, shot on the first wind-up tick, 15 + rand(10) cooldown, knockback 2.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.NurseSyringeHurt,
				Speed = 8f, GravityCorrection = 10f, Damage = 8, Knockback = 2f,
				Delay = 1, Cooldown = 15, RandomCooldown = 10,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Nurse]),
				RangeTiles = Math.Max(1, NPCID.Sets.DangerDetectRange[NPCID.Nurse]) / (float)LevyConstants.TilePx,
			},
			Kit = new NurseKit(),
		};
	}

	/// <summary>The Nurse's ability: every <see cref="NurseCastIntervalTicks"/> she heals the nearest wounded soldier within reach, at most once per soldier per <see cref="NursePerSoldierCooldownTicks"/>.</summary>
	public sealed class NurseKit : OfficerKit
	{
		private sealed class State
		{
			public int Timer;
			/// <summary>Soldier id (record id; body index for a body without a record) -> game tick of its last heal.</summary>
			public readonly Dictionary<int, uint> Healed = new();
		}

		public override object CreateState() => new State();

		// {0} reach in tiles, {1} percent of max life per heal, {2} seconds between heals of one soldier, {3} the most one heal can cost
		public override object[] DescriptionArgs => new object[]
		{
			NurseHealRangeTiles, (int)Math.Round(NurseHealFrac * 100f), NursePerSoldierCooldownTicks / 60, LevyUtil.Coins(NurseMaxPriceCopper),
		};

		public override void Tick(OfficerContext ctx)
		{
			if (ctx.State is not State st) return;
			if (++st.Timer < NurseCastIntervalTicks) return;
			st.Timer = 0;
			if (ctx.Barracks == null || !LevyConfig.Get.NurseHealsSoldiers) return; // no settlement to pay her (or the world has Nurse healing switched off)

			NPC nurse = ctx.Npc;
			uint now = Main.GameUpdateCount;
			float reach = NurseHealRangeTiles * LevyConstants.TilePx;
			SoldierNPC best = null; float bestD = reach; int bestMissing = 0, bestKey = 0;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (!s.IsAlive || s.State == SoldierState.Desert || s.State == SoldierState.Dead) continue;
				NPC b = s.NPC;
				int missing = b.lifeMax - b.life;
				if (missing < Math.Max(NurseMinMissingHp, (int)(b.lifeMax * NurseMinMissingFrac))) continue;
				float d = nurse.Distance(b.Center);
				if (d >= bestD) continue;
				int key = s.Record != null ? s.Record.Id : -1000 - b.whoAmI;
				if (st.Healed.TryGetValue(key, out uint last) && now - last < (uint)NursePerSoldierCooldownTicks) continue;
				best = s; bestD = d; bestMissing = missing; bestKey = key;
			}
			if (best == null) return;

			NPC body = best.NPC;
			int heal = Math.Min(bestMissing, Math.Max(1, (int)(body.lifeMax * NurseHealFrac)));
			long price = Math.Min(NurseMaxPriceCopper, FlavorSystem.NurseHealPrice(heal));
			if (!EconomySystem.TryPay(ctx.Barracks, price)) return; // the treasury cannot cover it: she does not heal on credit

			body.life = Math.Min(body.lifeMax, body.life + heal);
			if (best.Record != null) best.Record.CurrentHp = body.life;
			body.HealEffect(heal);   // the green number; broadcast from a server
			body.netUpdate = true;
			nurse.direction = body.Center.X < nurse.Center.X ? -1 : 1; nurse.spriteDirection = nurse.direction;
			FlavorSystem.PlayHealSound(nurse.Center);
			RosterSystem.SyncBarracks(ctx.Barracks); // the treasury changed: clients' barracks panel shows it

			st.Healed[bestKey] = now;
			if (st.Healed.Count > 48) // forget soldiers whose cooldown is long over
			{
				var stale = new List<int>();
				foreach (var kv in st.Healed) if (now - kv.Value >= (uint)NursePerSoldierCooldownTicks) stale.Add(kv.Key);
				foreach (int k in stale) st.Healed.Remove(k);
			}
		}
	}
}
