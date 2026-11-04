using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Util;
using Levy.Content.Buffs;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Mark
{
	/// <summary>
	/// THE SHRED AMP (enemy side). While an enemy carries the <see cref="MarkedBuff"/>, every hit from the player's army lands for
	/// <see cref="LevyConstants.Officer.MarkDamagePct"/>% more. The amp is gated to army shots only (soldiers, their summons, an officer's
	/// bolt) through <see cref="MarkSystem.IsArmyProjectile"/>, so the mark helps the army and nothing else — never enemy-vs-enemy, never
	/// the commander's own weapon. It runs in the same hit pipeline that already resolves soldier damage (the server for the army's npcProj
	/// shots), so the amped number is the one that syncs to every client. A percent on final damage, so it stays relevant on bosses.
	/// </summary>
	public class MarkAmpGlobalNPC : GlobalNPC
	{
		public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
		{
			if (!npc.HasBuff(ModContent.BuffType<MarkedBuff>())) return;   // fast reject: almost no enemy carries the mark
			if (!MarkSystem.IsArmyProjectile(projectile)) return;         // only the player's army benefits from the mark
			modifiers.FinalDamage *= 1f + MarkDamageFrac;
		}
	}

	/// <summary>
	/// Applies, clears and recognises the army's mark. Application and removal are server authority and are synced to clients, so the buff icon
	/// and any client-side read agree with the server. The amp that reads the mark lives on <see cref="MarkAmpGlobalNPC"/>.
	/// </summary>
	public static class MarkSystem
	{
		public static int BuffType => ModContent.BuffType<MarkedBuff>();

		/// <summary>
		/// True when a projectile is one of the player's army's shots: a tagged soldier/summon shot (<see cref="SoldierProjectileTag.IsSoldierShot"/>,
		/// the mod's own army-damage signal — soldiers, their real minions, and anything those spawn), or an officer avatar's bolt (fired by a
		/// <see cref="SoldierNPC"/> but not run through the soldier weapon tagger, so it carries only the source-NPC tag). Enemy shots are hostile
		/// and the commander's own projectiles are neither, so neither is ever amped.
		/// </summary>
		public static bool IsArmyProjectile(Projectile p)
		{
			if (p == null || !p.active || p.hostile) return false;
			if (!p.TryGetGlobalProjectile<SoldierProjectileTag>(out var tag)) return false;
			if (tag.IsSoldierShot) return true;                                   // soldiers, their real minions and the shots those spawn
			if (tag.SourceNpcIndex >= 0 && tag.SourceNpcIndex < Main.maxNPCs)     // officer avatar bolts: fired BY a soldier-family NPC
			{
				NPC src = Main.npc[tag.SourceNpcIndex];
				if (src.active && (src.ModNPC is SoldierNPC || src.ModNPC is SummonedUnitNPC)) return true; // a summoned unit's shot is the army's too
			}
			return false;
		}

		/// <summary>
		/// A hit of <paramref name="damage"/> by one of the army's bodies that strikes directly instead of through a projectile (a summoned charger's blow), amped the way
		/// <see cref="MarkAmpGlobalNPC"/> amps a shot: the same percent on a marked enemy, the damage untouched on any other.
		/// </summary>
		public static int Amp(NPC target, int damage) =>
			target != null && target.HasBuff(BuffType) ? (int)(damage * (1f + MarkDamageFrac)) : damage;

		/// <summary>Hang the mark on an enemy (server). Overrides debuff immunity so the call for fire always lands — bosses included — and syncs.</summary>
		public static void Mark(NPC npc, int durationTicks)
		{
			if (npc == null || !npc.active || !LevyUtil.IsServerOrSingle) return;
			int type = BuffType;
			npc.buffImmune[type] = false;         // the shred ignores immunity: it is the army aiming, not a status effect the enemy can resist
			npc.AddBuff(type, durationTicks);
			Sync(npc);
		}

		/// <summary>Pull the mark off an enemy early (server), so only one enemy ever carries it at a time; syncs.</summary>
		public static void Unmark(NPC npc)
		{
			if (npc == null || !npc.active || !LevyUtil.IsServerOrSingle) return;
			int idx = npc.FindBuffIndex(BuffType);
			if (idx < 0) return;
			npc.DelBuff(idx);
			Sync(npc);
		}

		private static void Sync(NPC npc)
		{
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.NPCBuffs, -1, -1, null, npc.whoAmI);
		}
	}
}
