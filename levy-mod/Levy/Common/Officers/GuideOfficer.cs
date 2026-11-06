using System;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE GUIDE. A FRONTLINE officer that marches with the line and steadies the soldiers around him: they shoot straighter, and fire together on his call. He also
	/// shoots anything that strays into reach with his own wooden bow (the real item, <see cref="OfficerDef.HeldWeapon"/>); the guiding bolt is only the fallback. The accuracy is passive and read shot by shot (<see cref="OfficerSupport"/>); the volley pulls ready soldiers' shots forward in step.
	/// </summary>
	public static class GuideOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Guide,
			Key = "Guide",
			CostMult = GuideCostMult,
			Aggro = GuideAggro,
			Posture = OfficerPosture.Frontline,          // marches with the army and fights what comes in reach
			// His own wooden bow and the arrows he carries: the Guide shoots the item's real arrow at the bow's real cadence.
			HeldWeapon = new OfficerHeldWeapon { Item = ItemID.WoodenBow, Ammo = ItemID.WoodenArrow },
			// The guiding bolt: the fallback when he holds no usable weapon (and the reach and damage floor the bow fights with).
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.SapphireBolt,
				Speed = GuideBoltSpeed, GravityCorrection = GuideBoltGravityCorrection, Damage = GuideBoltDamage, Knockback = GuideBoltKnockback,
				Delay = 1, Cooldown = GuideBoltCooldown, RandomCooldown = GuideBoltRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Guide]),
				RangeTiles = GuideBoltRangeTiles,
			},
			Kit = new GuideKit(),
		};
	}

	/// <summary>
	/// The Guide's ability: every soldier within <see cref="GuideAuraRadiusTiles"/> tiles has his aim error multiplied by <see cref="GuideAimErrorMult"/> (the accuracy, via <see cref="AimSupport"/>),
	/// and every <see cref="GuideVolleyCadenceTicks"/> he calls a VOLLEY: each fighting soldier in reach whose weapon is almost ready fires at once, so the line fires together.
	/// </summary>
	public sealed class GuideKit : OfficerKit
	{
		private sealed class State { public int Timer; }
		public override object CreateState() => new State();

		public override bool AimSupport(out int radiusTiles, out float errorMult) { radiusTiles = GuideAuraRadiusTiles; errorMult = GuideAimErrorMult; return true; }

		// {0} reach in tiles, {1} how much less aim error a soldier in reach has (percent), {2} seconds between volleys
		public override object[] DescriptionArgs => new object[] { GuideAuraRadiusTiles, (int)Math.Round((1f - GuideAimErrorMult) * 100f), GuideVolleyCadenceTicks / 60 };

		public override void Tick(OfficerContext ctx)
		{
			if (ctx.State is not State st || ++st.Timer < GuideVolleyCadenceTicks) return;
			st.Timer = 0;
			CallVolley(ctx.Npc.Center);
		}

		/// <summary>Brings forward the next shot of every fighting soldier in reach whose weapon is within the pull of ready. Returns how many were called. Server.</summary>
		public static int CallVolley(Vector2 from)
		{
			float reach = GuideAuraRadiusTiles * LevyConstants.TilePx; reach *= reach;
			int n = 0;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (s == null || !s.IsAlive || s.Record == null || s.Record.HoldFire || s.State != SoldierState.Engage) continue;
				if (Vector2.DistanceSquared(from, s.NPC.Center) > reach) continue;
				if (s.AttackCooldown <= 0 || s.AttackCooldown > GuideVolleyPullTicks) continue;
				s.AttackCooldown = 0; n++;
			}
			return n;
		}
	}
}
