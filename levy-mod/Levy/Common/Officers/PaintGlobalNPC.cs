using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Mark;
using Levy.Common.Util;
using Levy.Content.Buffs;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>The paint, enemy side. A <see cref="PaintedBuff"/> enemy loses defense, deals less damage and is tinted one of the Painter's colors (chosen by its index, so every machine agrees with no packet).</summary>
	public class PaintGlobalNPC : GlobalNPC
	{
		public static int BuffType => ModContent.BuffType<PaintedBuff>();
		public static bool IsPainted(NPC npc) => npc != null && npc.active && npc.HasBuff(BuffType);

		/// <summary>The fraction of its damage a painted enemy still deals (1 when unpainted).</summary>
		public static float DamageMult(NPC npc) => IsPainted(npc) ? 1f - PaintDamageCut : 1f;

		private static readonly Color[] Palette =
		{
			new Color(255, 60, 60), new Color(255, 160, 40), new Color(250, 230, 50), new Color(70, 220, 90),
			new Color(60, 200, 240), new Color(90, 110, 255), new Color(210, 90, 250), new Color(255, 110, 190),
		};
		public static Color ColorOf(NPC npc) => Palette[(npc.whoAmI + npc.type) & 7];

		/// <summary>Soaks an enemy in paint (server / single player); overrides debuff immunity so bosses are painted too, and syncs.</summary>
		public static void Paint(NPC npc)
		{
			if (npc == null || !npc.active || !LevyUtil.IsServerOrSingle || npc.friendly || npc.townNPC) return;
			int t = BuffType;
			npc.buffImmune[t] = false;
			npc.AddBuff(t, PaintDurationTicks);
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.NPCBuffs, -1, -1, null, npc.whoAmI);
		}

		public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
		{
			if (!npc.HasBuff(BuffType)) return;
			modifiers.Defense.Flat -= PaintDefenseLoss;
		}

		public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
		{
			if (!npc.HasBuff(BuffType)) return;
			modifiers.SourceDamage *= 1f - PaintDamageCut;
		}

		public override void DrawEffects(NPC npc, ref Color drawColor)
		{
			if (!npc.HasBuff(BuffType)) return;
			Color c = ColorOf(npc);
			drawColor = Color.Lerp(drawColor, new Color(c.R, c.G, c.B, drawColor.A), 0.65f);
			if (Main.rand.NextBool(6)) { int d = Dust.NewDust(npc.position, npc.width, npc.height, DustID.WhiteTorch, 0f, 0f, 100, c, 1.1f); Main.dust[d].noGravity = true; Main.dust[d].velocity *= 0.3f; }
		}
	}

	/// <summary>A paintball from the army paints what it hits.</summary>
	public class PaintProjectile : GlobalProjectile
	{
		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ProjectileID.PainterPaintball;

		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!LevyUtil.IsServerOrSingle || !MarkSystem.IsArmyProjectile(projectile)) return;
			PaintGlobalNPC.Paint(target);
		}
	}
}
