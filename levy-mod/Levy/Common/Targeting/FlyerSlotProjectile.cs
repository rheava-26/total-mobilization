using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Targeting
{
	/// <summary>
	/// An enemy that takes the flyer slot (Empress of Light) writes <c>NPC.target</c> = 254 into the ai[0] of the projectiles she fires (her Prismatic Bolts read <c>Main.player[(int)ai[0]]</c> every tick to
	/// steer). Outside her own AI call slot 254 holds the empty vanilla player, so those bolts flew at the world origin instead of the soldier they were fired at. For the length of such a projectile's own
	/// <c>Projectile.AI</c> call (a detour, because Calamity runs its own projectile AI from its GlobalProjectile.PreAI, ahead of any mod loaded after it) the soldier her fight is locked on, else the nearest
	/// soldier, stands in slot 254, exactly as for the enemy's AI call. Hostile projectiles with any other ai[0] are untouched.
	/// </summary>
	public class FlyerSlotProjectile : ModSystem
	{
		internal static int DbgSeen, DbgSwapped, DbgNoBody, DbgBlocked;

		public override void Load() => On_Projectile.AI += AiDetour;
		public override void Unload() => On_Projectile.AI -= AiDetour;

		private static void AiDetour(On_Projectile.orig_AI orig, Projectile self)
		{
			if (!self.active || !self.hostile || (int)self.ai[0] != EnemyTargeting.FlyerSlot) { orig(self); return; }
			DbgSeen++;
			if (!EnemyTargeting.Enabled || !EnemyTargeting.BossFlyerOn || EnemyTargeting.IsSwapped || !EnemyTargeting.FlyerSlotUsable) { DbgBlocked++; orig(self); return; }
			ITargetBody body = BodyFor(self);
			if (body == null) { DbgNoBody++; orig(self); return; }
			DbgSwapped++;
			EnemyTargeting.Swap(body, EnemyTargeting.FlyerSlot);
			try { orig(self); }
			finally { EnemyTargeting.Unswap(); }
		}

		/// <summary>The soldier a flyer-slot enemy is locked on (the live one nearest the projectile), else the nearest live soldier to the projectile.</summary>
		private static ITargetBody BodyFor(Projectile p)
		{
			ITargetBody best = null; float bestD = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || !EnemyTargeting.UsesFlyerSlot(n) || !EnemyTargeting.IsBossClass(n)) continue;
				ITargetBody b = EnemyTargeting.LiveTarget(global::Levy.Common.Globals.LevyNpcData.Of(n).SoldierTarget);
				if (b == null || !b.IsAlive) continue;
				float d = Vector2.DistanceSquared(n.Center, p.Center);
				if (d < bestD) { bestD = d; best = b; }
			}
			if (best != null) return best;
			for (int i = 0; i < EnemyTargeting.Bodies.Count; i++)
			{
				ITargetBody b = EnemyTargeting.Bodies[i]; if (!b.IsAlive) continue;
				float d = Vector2.DistanceSquared(b.NPC.Center, p.Center);
				if (d < bestD) { bestD = d; best = b; }
			}
			return best;
		}
	}
}
