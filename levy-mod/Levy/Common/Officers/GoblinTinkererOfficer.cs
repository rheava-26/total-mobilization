using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE FIELD REFORGER. The Goblin Tinkerer is a FRONTLINE officer who marches with the line and flings spiky balls at anything that strays into reach. His kit
	/// is what a Goblin Tinkerer is for: he reforges the army's weapons toward a quality floor WHILE IT MOVES, so nobody has to stop at an Armory to get a better prefix.
	/// Each roll is paid from the treasury at the vanilla reforge price.
	/// </summary>
	public static class GoblinTinkererOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.GoblinTinkerer,
			Key = "GoblinTinkerer",
			CostMult = GoblinCostMult,
			Aggro = GoblinAggro,
			Posture = OfficerPosture.Frontline,   // marches at the front and fights what comes in reach
			// A thrown spiky ball (affected by gravity, so a far shot is arced above the target); scales with world progress like a town NPC's shot.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.SpikyBall,
				Speed = GoblinBallSpeed, GravityCorrection = GoblinBallGravityCorrection,
				Damage = GoblinBallDamage, Knockback = GoblinBallKnockback,
				Delay = 1, Cooldown = GoblinBallCooldown, RandomCooldown = GoblinBallRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.GoblinTinkerer]),
				RangeTiles = GoblinBallRangeTiles,
			},
			Kit = new GoblinTinkererKit(),
		};
	}

	/// <summary>
	/// The Goblin Tinkerer's ability: every <see cref="GoblinReforgeCadenceTicks"/> he picks the NEAREST soldier within <see cref="GoblinReforgeRadiusTiles"/> tiles whose held weapon
	/// sits below the floor (<see cref="GoblinReforgeTopFraction"/>: the top slice of the prefixes that weapon can roll) and rerolls its prefix up to
	/// <see cref="GoblinReforgeRollsPerPulse"/> times, paying the vanilla reforge price from the treasury per roll. A roll never makes a weapon worse, and a weapon that already clears the
	/// floor (or cannot take a prefix, or is favourited) is left alone. He never takes the treasury under <see cref="GoblinReforgeReserveWages"/> Levy wages, so a reforge can never be
	/// the reason dawn payroll fails. The reroll itself is <see cref="ArmoryTE.TryReforgeOne"/>, the same step the Armory's bulk reforge uses.
	/// </summary>
	public sealed class GoblinTinkererKit : OfficerKit
	{
		private sealed class State
		{
			public int Timer;
			/// <summary>Scratch list of the soldiers in reach with a reforgeable weapon, nearest first; reused every pulse.</summary>
			public readonly List<SoldierNPC> Reach = new();
		}

		public override object CreateState() => new State();

		// {0} reach in tiles, {1} the floor: he rerolls until the prefix is in the top this-percent of what the weapon can roll, {2} rolls he spends on one weapon per visit, {3} Levy wages he leaves in the treasury
		public override object[] DescriptionArgs => new object[]
		{
			GoblinReforgeRadiusTiles, (int)Math.Round(GoblinReforgeTopFraction * 100f), GoblinReforgeRollsPerPulse, GoblinReforgeReserveWages,
		};

		public override void Tick(OfficerContext ctx)
		{
			if (ctx.State is not State st) return;
			if (++st.Timer < GoblinReforgeCadenceTicks) return;
			st.Timer = 0;

			BarracksTE payer = ctx.Barracks;
			if (payer == null) return;                                       // no settlement to pay him
			long reserve = EconomySystem.LevyWage * GoblinReforgeReserveWages;
			if (payer.Treasury <= reserve) return;                           // nothing to spare: skip the scan altogether

			Vector2 from = ctx.Npc.Center;
			float reach = GoblinReforgeRadiusTiles * LevyConstants.TilePx;
			float reachSq = reach * reach;
			st.Reach.Clear();
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (s == null || !s.IsAlive || s.Record == null) continue;
				if (Vector2.DistanceSquared(from, s.NPC.Center) > reachSq) continue;
				if (!ArmoryTE.IsReforgeable(s.Record.Gear.Weapon)) continue; // a cheap filter first: only weapons that can take a prefix at all
				st.Reach.Add(s);
			}
			if (st.Reach.Count == 0) return;
			st.Reach.Sort((a, b) => Vector2.DistanceSquared(from, a.NPC.Center).CompareTo(Vector2.DistanceSquared(from, b.NPC.Center)));

			foreach (SoldierNPC s in st.Reach)
			{
				Item weapon = s.Record.Gear.Weapon;
				// false = this weapon already clears the floor (or has nothing to roll): try the next soldier. True = he worked on it, and that is this pulse's job.
				if (!ArmoryTE.TryReforgeOne(weapon, payer, GoblinReforgeTopFraction, GoblinReforgeRollsPerPulse, reserve, out ArmoryTE.ReforgeOneResult r)) continue;
				if (r.Rolls > 0)
				{
					RosterSystem.SyncBarracks(payer);                         // the treasury changed: clients' barracks panel shows it
					PlayReforgeSound(s.NPC.Center);
					if (!ReferenceEquals(r.Best, weapon))
					{
						s.Record.Gear.Weapon = r.Best;                        // the better prefix; the old instance is dropped
						s.NotifyGearChanged();                                // re-reads his weapon profile and stats, and syncs the new prefix to clients
					}
					ctx.Npc.direction = s.NPC.Center.X < ctx.Npc.Center.X ? -1 : 1; ctx.Npc.spriteDirection = ctx.Npc.direction;
				}
				break;                                                        // one soldier per pulse (also when the treasury could not afford the first roll)
			}
		}

		/// <summary>The vanilla reforge clink, heard by everyone near <paramref name="position"/> (broadcast from a dedicated server, as the Nurse's heal sound is).</summary>
		private static void PlayReforgeSound(Vector2 position)
		{
			if (Main.netMode == NetmodeID.Server)
			{
				if (SoundID.IndexByName != null && SoundID.IndexByName.TryGetValue("Item37", out ushort index))
					NetMessage.PlayNetSound(new NetMessage.NetSoundInfo(position, index), -1, -1);
			}
			else
			{
				SoundEngine.PlaySound(SoundID.Item37, position);
			}
		}
	}
}
