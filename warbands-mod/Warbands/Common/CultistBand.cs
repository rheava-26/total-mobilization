using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Targeting;
using Warbands.Content.NPCs;
using Warbands.Content.Projectiles;

namespace Warbands.Common
{
	/// <summary>
	/// The Lunatic Cultist's circle.
	///  * Lunatic Devotees keep chanting (vanilla AI, untouched) and cast while they do: every 1.5 to 2.5 seconds, with a player or levy within 45 tiles and a clear line, a slow homing stardust orb (DevoteOrb) with damage from BandStats at the world's stage.
	///  * Cultist Archers fight as a band that runs Levy's soldier brain: the first tick a vanilla archer exists near a player or levy within 70 tiles, it is replaced by a BandLevyNPC cultist archer of the "Cultist Archers" type, joining the circle's band.
	/// Switch: WarbandsConfig.CultistBand.
	/// </summary>
	public class CultistBandGlobal : GlobalNPC
	{
		public override bool InstancePerEntity => true;
		private int _timer = 1;

		public static bool IsArcher(int type) => type == NPCID.CultistArcherBlue || type == NPCID.CultistArcherWhite;
		public static int Converted;

		public override void PostAI(NPC npc)
		{
			var cfg = WarbandsConfig.Get; if (cfg == null || !cfg.CultistBand || Main.netMode == NetmodeID.MultiplayerClient) return;
			if (npc.type == NPCID.CultistDevote) DevoteCast(npc);
			else if (IsArcher(npc.type) && cfg.LevyBrain) ConvertArcher(npc);
		}

		private void DevoteCast(NPC npc)
		{
			if (--_timer > 0) return;
			_timer = 90 + Main.rand.Next(60);
			Vector2 aimAt = default; float best = 45f * 16f; best *= best; bool found = false;
			for (int i = 0; i < Main.maxPlayers; i++) { var p = Main.player[i]; if (!p.active || p.dead) continue; float d = Vector2.DistanceSquared(p.Center, npc.Center); if (d < best) { best = d; aimAt = p.Center; found = true; } }
			var bodies = EnemyTargeting.Bodies;
			for (int i = 0; i < bodies.Count; i++) { var b = bodies[i]; if (b == null || !b.IsAlive || b.NPC == null || !b.NPC.active) continue; float d = Vector2.DistanceSquared(b.NPC.Center, npc.Center); if (d < best) { best = d; aimAt = b.NPC.Center; found = true; } }
			if (!found || !Collision.CanHitLine(npc.position, npc.width, npc.height, aimAt - new Vector2(4, 4), 8, 8)) return;
			int hit = BandStats.Hit(Math.Max(Stage.Current, 5), 'M', 55f, 0.45f);
			int dmg = Math.Max(1, hit / (int)Levy.Common.LevyConstants.HostileProjectileDamageMult);
			Vector2 vel = Vector2.Normalize(aimAt - npc.Center) * 2.6f;
			Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel, ModContent.ProjectileType<DevoteOrb>(), dmg, 1f, Main.myPlayer);
			Casts++;
		}
		public static int Casts;

		private void ConvertArcher(NPC npc)
		{
			bool near = false;
			for (int i = 0; i < Main.maxPlayers && !near; i++) { var p = Main.player[i]; if (p.active && !p.dead && Vector2.DistanceSquared(p.Center, npc.Center) < 70f * 16f * 70f * 16f) near = true; }
			if (!near) { var bodies = EnemyTargeting.Bodies; for (int i = 0; i < bodies.Count && !near; i++) { var b = bodies[i]; if (b != null && b.IsAlive && b.NPC != null && Vector2.DistanceSquared(b.NPC.Center, npc.Center) < 70f * 16f * 70f * 16f) near = true; } }
			if (!near) return;
			var type = BandTypes.ById("CultistArchers");
			BandBrain circle = null;
			foreach (var b in BandBrain.All.Values) if (b.Type == type && Vector2.DistanceSquared(b.Anchor, npc.Center) < 90f * 16f * 90f * 16f) { circle = b; break; }
			bool fresh = circle == null;
			if (fresh) circle = BandBrain.Create(type, type.Stage, 0, npc.Center);
			int member = circle.StartSize;
			int id = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Bottom.Y, ModContent.NPCType<BandLevyNPC>());
			if (id < 0 || id >= Main.maxNPCs) { if (fresh) BandBrain.All.Remove(circle.Id); return; }
			circle.StartSize++;
			(Main.npc[id].ModNPC as BandLevyNPC)?.Setup(type, member, type.Stage, circle.Id, circle.StartSize);
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, id);
			npc.active = false; npc.netUpdate = true; Converted++;
		}
	}
}
