using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Summon;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE MACHINIST. The Cyborg is a SUPPORT officer who deploys <see cref="LevyConstants.Summon.CyborgDroneCount"/> combat drones: flying <see cref="SummonedUnitNPC"/>s that hover over
	/// the army and laser the enemy from range. A downed drone is rebuilt on a cadence. While he is on duty his sensors also light up the ore and treasure around his commander
	/// (<see cref="CyborgSensorPlayer"/>). He fires rockets of his own.
	/// </summary>
	public static class CyborgOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Cyborg,
			Key = "Cyborg",
			CostMult = CyborgCostMult,
			Aggro = CyborgAggro,
			Posture = OfficerPosture.Support,   // directs his drones from behind the line
			// No real item to mount: the rocket he fires IS his weapon.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.RocketI,
				Speed = CyborgRocketSpeed, GravityCorrection = CyborgRocketGravityCorrection, Damage = CyborgRocketDamage, Knockback = CyborgRocketKnockback,
				Delay = 1, Cooldown = CyborgRocketCooldown, RandomCooldown = CyborgRocketRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Cyborg]),
				RangeTiles = CyborgRocketRangeTiles,
			},
			Kit = new CyborgKit(),
		};
	}

	/// <summary>The Cyborg's kit: one flight of combat drones, managed by <see cref="SummonerKit"/>. The spelunker's eye is <see cref="CyborgSensorPlayer"/>.</summary>
	public sealed class CyborgKit : SummonerKit
	{
		protected override SummonKind Kind => SummonKinds.CyborgDrone;
		protected override int SquadCap => CyborgDroneCount;
		protected override int RespawnCadenceTicks => CyborgRespawnCadenceTicks;

		// {0} drones fielded, {1} each drone's health, {2} seconds to rebuild a lost one
		public override object[] DescriptionArgs => new object[] { CyborgDroneCount, CyborgDroneHp, CyborgRespawnCadenceTicks / 60 };
	}

	/// <summary>
	/// THE SPELUNKER'S EYE. While a commissioned Cyborg is on duty for a player, that player sees ore, gems and treasure glow around them, exactly as under Spelunker. It sets the same
	/// flag the Spelunker buff sets (<see cref="Player.findTreasure"/>) on every machine, each tick, from facts every machine already holds (the officer's synced commission and avatar), so
	/// it needs no buff icon, no packet and no timer: it holds while he marches and is gone the tick he falls, is dismissed or goes off duty. Per-commander by construction (matched on name).
	/// </summary>
	public class CyborgSensorPlayer : ModPlayer
	{
		public override void PostUpdateEquips()
		{
			var officers = OfficerSystem.Instance.ActiveBodies;
			for (int i = 0; i < officers.Count; i++)
			{
				OfficerBody b = officers[i];
				if (!b.IsAlive || b.TownNpc == null || b.TownNpc.type != NPCID.Cyborg) continue;
				OfficerGlobalNPC g = OfficerSystem.Data(b.TownNpc);
				if (g != null && string.Equals(g.Commander, Player.name, StringComparison.Ordinal)) { Player.findTreasure = true; return; }
			}
		}
	}
}
