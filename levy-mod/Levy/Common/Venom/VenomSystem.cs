using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Mark;
using Levy.Common.Officers;
using Levy.Common.Util;
using Levy.Content.Buffs;
using static Levy.Common.LevyConstants.Summon;

namespace Levy.Common.Venom
{
	/// <summary>
	/// THE WITCH DOCTOR'S VENOM (the army side). While a commissioned Witch Doctor is on duty, a hit from the army — a soldier's shot or blow, a summoned unit's, an officer's bolt —
	/// landed by a body within <see cref="WitchDoctorVenomRadiusTiles"/> of him envenoms its target with <see cref="WitchDoctorVenomChance"/>. Venom is <see cref="VenomBuff"/>:
	/// a damage-over-time that ignores defense and grows with the world's progress (<see cref="VenomGlobalNPC"/>). It overrides debuff immunity, as the shred mark does, so it lands on bosses.
	/// A fresh application only restarts the timer; it never stacks. All state is the debuff on the enemy: nothing here is saved or kept between ticks.
	/// </summary>
	public static class VenomSystem
	{
		public static int BuffType => ModContent.BuffType<VenomBuff>();

		/// <summary>
		/// Called once for every hit the army lands, with the target and where the attacker stood. Rolls the chance and envenoms when an on-duty Witch Doctor covers the attacker.
		/// Server / single player only; with no Witch Doctor fielded it returns at once.
		/// </summary>
		public static void OnArmyHit(NPC target, Vector2 attackerAt)
		{
			if (target == null || !target.active || !LevyUtil.IsServerOrSingle || target.friendly || target.townNPC) return;
			var officers = OfficerSystem.Instance.ActiveBodies;
			if (officers.Count == 0) return;                          // no officer fielded: nothing to do
			float reach = WitchDoctorVenomRadiusTiles * LevyConstants.TilePx;
			float reachSq = reach * reach;
			bool covered = false;
			for (int i = 0; i < officers.Count; i++)
			{
				OfficerBody b = officers[i];
				if (!b.IsAlive || b.TownNpc == null || b.TownNpc.type != NPCID.WitchDoctor) continue;
				if (Vector2.DistanceSquared(attackerAt, b.NPC.Center) <= reachSq) { covered = true; break; }
			}
			if (!covered || Main.rand.NextFloat() >= WitchDoctorVenomChance) return;
			Envenom(target, WitchDoctorVenomDurationTicks);
		}

		/// <summary>Hang venom on an enemy (server). Overrides debuff immunity so it always lands — bosses included — and syncs.</summary>
		public static void Envenom(NPC npc, int durationTicks)
		{
			if (npc == null || !npc.active || !LevyUtil.IsServerOrSingle) return;
			int type = BuffType;
			npc.buffImmune[type] = false;
			npc.AddBuff(type, durationTicks);
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.NPCBuffs, -1, -1, null, npc.whoAmI);
		}
	}

	/// <summary>
	/// Venom's two enemy-side parts. The burn: while an enemy carries <see cref="VenomBuff"/> it loses <see cref="WitchDoctorVenomDps"/> life a second, times the world-progress scale, straight
	/// off its life regeneration so defense does not blunt it (the standard damage-over-time hook, run on server and clients alike). The delivery for the shots that are not soldier hits: a summoned
	/// unit's or an officer's projectile envenoms on impact; a soldier's own shots and blows reach <see cref="VenomSystem.OnArmyHit"/> from where the soldier's hit is booked, and a summoned
	/// unit's blow from the unit itself.
	/// </summary>
	public class VenomGlobalNPC : GlobalNPC
	{
		public override void UpdateLifeRegen(NPC npc, ref int damage)
		{
			if (!npc.HasBuff(VenomSystem.BuffType)) return;   // fast reject: almost no enemy carries venom
			if (npc.lifeRegen > 0) npc.lifeRegen = 0;
			int dps = (int)(WitchDoctorVenomDps * OfficerBrain.TownDamageScale());
			npc.lifeRegen -= dps * 2;                          // lifeRegen is in half-hit points per second
			if (damage < dps / 5) damage = dps / 5;            // the number that pops over the enemy's head
		}

		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			if (!LevyUtil.IsServerOrSingle || !MarkSystem.IsArmyProjectile(projectile)) return;
			if (!projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.IsSoldierShot) return; // a soldier's shot is rolled once, where the soldier's hit is booked
			Vector2 from = projectile.Center;
			if (tag.SourceNpcIndex >= 0 && tag.SourceNpcIndex < Main.maxNPCs && Main.npc[tag.SourceNpcIndex].active) from = Main.npc[tag.SourceNpcIndex].Center;
			VenomSystem.OnArmyHit(npc, from);
		}
	}
}
