using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Flavor;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;
using Levy.Content.Projectiles;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE PROOF OFFICER and the template for the next one. The Nurse is a SUPPORT officer: she marches BEHIND the line, near the army's wounded, and keeps
	/// clear of enemies. She heals the most-hurt soldier in reach back to FULL — a syringe thrown to a soldier at range, or a direct top-off for one at her side —
	/// paying for it from the barracks that commissioned her (the vanilla Nurse price, capped). She throws her attack syringe only at an enemy that strays into reach
	/// (opportunistic self-defense; never a reason to advance). To add an officer: write a <c>Create()</c> like this and register it in <see cref="OfficerRegistry.RegisterAll"/>.
	/// </summary>
	public static class NurseOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Nurse,
			Key = "Nurse",
			CostMult = 1f,
			Aggro = NurseAggro,
			Posture = OfficerPosture.Support,            // stays behind the line, near the wounded, out of enemy reach
			MarchTrigger = DialogueTrigger.NURSE_MARCH,
			// NPC.AI_007_TownEntities, type 18: Nurse Syringe (projectile 583, poisons), damage 8, knockback 2 — but thrown fast enough to actually reach (velocity boost).
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.NurseSyringeHurt,
				Speed = NurseSyringeSpeed, GravityCorrection = 10f, Damage = 8, Knockback = 2f,
				Delay = 1, Cooldown = 15, RandomCooldown = 10,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Nurse]),
				RangeTiles = Math.Max(1, NPCID.Sets.DangerDetectRange[NPCID.Nurse]) / (float)LevyConstants.TilePx,
			},
			Kit = new NurseKit(),
		};
	}

	/// <summary>
	/// The Nurse's ability: every <see cref="NurseCastIntervalTicks"/> she heals the most-wounded soldier within reach to FULL — at most one per soldier per
	/// <see cref="NursePerSoldierCooldownTicks"/>. A soldier at her side is topped off directly; one further out gets a thrown healing syringe that restores on arrival.
	/// </summary>
	public sealed class NurseKit : OfficerKit
	{
		private sealed class State
		{
			public int Timer;
			/// <summary>Soldier id (record id; body index for a body without a record) -> game tick of its last heal.</summary>
			public readonly Dictionary<int, uint> Healed = new();
		}

		public override object CreateState() => new State();

		// {0} reach in tiles, {1} seconds between heals of one soldier, {2} the most one heal can cost
		public override object[] DescriptionArgs => new object[]
		{
			NurseHealRangeTiles, NursePerSoldierCooldownTicks / 60, LevyUtil.Coins(NurseMaxPriceCopper),
		};

		/// <summary>
		/// SUPPORT posture: she goes where the TROOPS are, not where the commander stands. She hovers near the most-wounded soldier within seeking range (wider than her heal
		/// reach, so she walks to a patient she cannot reach yet); with nobody hurt she keeps to the middle of the soldiers around the commander. False only when there are no
		/// soldiers at all, and she then simply keeps behind the commander.
		/// </summary>
		public override bool SupportAnchor(OfficerContext ctx, out Vector2 pos)
		{
			pos = default;
			SoldierNPC best = MostWounded(ctx.Npc.Center, NurseSeekRangeTiles * LevyConstants.TilePx, null, 0);
			if (best != null) { pos = best.NPC.Center; return true; }
			return ArmyCentre(ctx, out pos);
		}

		/// <summary>The mean position of the living soldiers within <see cref="NurseArmyRangeTiles"/> of the commander (of her own spot when no commander is near); false when there are none.</summary>
		private static bool ArmyCentre(OfficerContext ctx, out Vector2 centre)
		{
			centre = default;
			Vector2 around = ctx.Commander != null ? ctx.Commander.Center : ctx.Npc.Center;
			float reach = NurseArmyRangeTiles * LevyConstants.TilePx;
			Vector2 sum = Vector2.Zero; int n = 0;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (!s.IsAlive || s.State == SoldierState.Desert || s.State == SoldierState.Dead) continue;
				if (Vector2.Distance(around, s.NPC.Center) > reach) continue;
				sum += s.NPC.Center; n++;
			}
			if (n == 0) return false;
			centre = sum / n;
			return true;
		}

		public override void Tick(OfficerContext ctx)
		{
			if (ctx.State is not State st) return;
			if (++st.Timer < NurseCastIntervalTicks) return;
			st.Timer = 0;
			if (ctx.Barracks == null || !LevyConfig.Get.NurseHealsSoldiers) return; // no settlement to pay her (or Nurse healing switched off)

			NPC nurse = ctx.Npc;
			uint now = Main.GameUpdateCount;
			SoldierNPC best = MostWounded(nurse.Center, NurseHealRangeTiles * LevyConstants.TilePx, st, now);
			if (best == null) return;

			NPC body = best.NPC;
			int missing = body.lifeMax - body.life;       // heal to FULL (capped at missing)
			if (missing <= 0) return;
			long price = Math.Min(NurseMaxPriceCopper, FlavorSystem.NurseHealPrice(missing));
			if (!EconomySystem.TryPayNetwork(ctx.Barracks, price)) return; // the settlement cannot cover it: she does not heal on credit

			int key = best.Record != null && best.Record.Id > 0 ? best.Record.Id : -1000 - body.whoAmI;
			float dist = nurse.Distance(body.Center);
			if (dist <= NurseProximityTiles * LevyConstants.TilePx)
			{
				// PROXIMITY: top the soldier off directly
				body.life = Math.Min(body.lifeMax, body.life + missing);
				if (best.Record != null) best.Record.CurrentHp = body.life;
				body.HealEffect(missing, true);
			}
			else
			{
				// RANGED: throw a healing syringe that restores on arrival
				Vector2 v = nurse.DirectionTo(body.Center) * NurseSyringeSpeed;
				if (v.HasNaNs()) v = new Vector2(nurse.direction == 0 ? 1 : nurse.direction, -1f) * NurseSyringeSpeed;
				Projectile.NewProjectile(nurse.GetSource_FromAI(), nurse.Center, v,
					ModContent.ProjectileType<OfficerHealSyringe>(), 0, 0f, Common.Weapons.SoldierMinions.TrueMyPlayer, body.whoAmI, missing);
			}
			nurse.direction = body.Center.X < nurse.Center.X ? -1 : 1; nurse.spriteDirection = nurse.direction;
			FlavorSystem.PlayHealSound(nurse.Center);
			RosterSystem.SyncBarracks(ctx.Barracks); // the treasury changed: clients' barracks panel shows it

			st.Healed[key] = now;
			if (st.Healed.Count > 48) // forget soldiers whose cooldown is long over
			{
				var stale = new List<int>();
				foreach (var kv in st.Healed) if (now - kv.Value >= (uint)NursePerSoldierCooldownTicks) stale.Add(kv.Key);
				foreach (int k in stale) st.Healed.Remove(k);
			}
		}

		/// <summary>The most-wounded eligible soldier within <paramref name="reach"/> of <paramref name="from"/>, respecting the per-soldier cooldown when a state is given.</summary>
		private static SoldierNPC MostWounded(Vector2 from, float reach, State st, uint now)
		{
			SoldierNPC best = null; int bestMissing = 0;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (!s.IsAlive || s.State == SoldierState.Desert || s.State == SoldierState.Dead) continue;
				NPC b = s.NPC;
				int missing = b.lifeMax - b.life;
				if (missing < Math.Max(NurseMinMissingHp, (int)(b.lifeMax * NurseMinMissingFrac))) continue;
				if (Vector2.Distance(from, b.Center) > reach) continue;
				if (st != null)
				{
					int key = s.Record != null && s.Record.Id > 0 ? s.Record.Id : -1000 - b.whoAmI;
					if (st.Healed.TryGetValue(key, out uint last) && now - last < (uint)NursePerSoldierCooldownTicks) continue;
				}
				if (missing > bestMissing) { bestMissing = missing; best = s; }
			}
			return best;
		}
	}
}
