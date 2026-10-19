using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Content.Projectiles
{
	/// <summary>
	/// A soldier's yoyo: ai[0] = soldier whoAmI, ai[1] = yoyo item type (its own projectile sprite is drawn), ai[2] = phase (0 out and loitering, 1 returning).
	/// Levy-owned and driven entirely by this AI: it never reads the owning player and never touches the commander's held-projectile state.
	/// It flies at the soldier's target (held within the yoyo's string reach of the soldier), orbits it dealing damage on contact for the yoyo's flight time,
	/// then hauls back to the soldier and is caught. One out per soldier.
	/// </summary>
	public class SoldierYoyo : ModProjectile
	{
		/// <summary>No asset of its own: PreDraw draws the yoyo item's projectile sprite. A vanilla yoyo texture only satisfies the loader.</summary>
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodYoyo;

		private bool _init;
		private float _reach;
		private int _loiter;
		private int _target = -1;
		private int _lost;
		private float _angle;

		public override void SetDefaults()
		{
			Projectile.width = LevyConstants.YoyoHitboxPx;
			Projectile.height = LevyConstants.YoyoHitboxPx;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.aiStyle = -1;
			Projectile.timeLeft = LevyConstants.YoyoLoiterMaxTicks + LevyConstants.YoyoHardCapTicks;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = LevyConstants.YoyoLocalHitCooldownTicks;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
			Projectile.ownerHitCheck = false;
			Projectile.DamageType = DamageClass.Melee;
		}

		private int SoldierIndex => (int)Projectile.ai[0];
		private int ItemType => (int)Projectile.ai[1];
		private bool Returning => Projectile.ai[2] >= 1f;

		public SoldierNPC Soldier
		{
			get
			{
				int i = SoldierIndex;
				if (i < 0 || i >= Main.maxNPCs) return null;
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC s || s.Record == null) return null;
				return s;
			}
		}

		private int ShotType()
		{
			int item = ItemType;
			return item > 0 && ContentSamples.ItemsByType.TryGetValue(item, out var it) && it != null ? it.shoot : 0;
		}

		private static bool Valid(NPC n) => n != null && n.active && LevyUtil.IsHostileTarget(n) && !SoldierTargeting.IsNeverTarget(n);

		/// <summary>The soldier's own target if it is within string reach, else the nearest enemy near the yoyo that the soldier can see and the string can reach.</summary>
		private int Acquire(SoldierNPC s)
		{
			Vector2 from = s.NPC.Center;
			float reach = _reach + LevyConstants.YoyoOrbitRadiusPx;
			int pref = s.TargetIndex;
			if (pref >= 0 && pref < Main.maxNPCs)
			{
				NPC p = Main.npc[pref];
				if (Valid(p) && Vector2.DistanceSquared(from, p.Center) <= reach * reach) return pref;
			}
			int best = -1;
			float bestSq = float.MaxValue, seek = LevyConstants.YoyoSeekRadiusPx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!Valid(n)) continue;
				if (Vector2.DistanceSquared(from, n.Center) > reach * reach) continue;
				float dsq = Vector2.DistanceSquared(Projectile.Center, n.Center);
				if (dsq > seek * seek || dsq >= bestSq) continue;
				if (!Collision.CanHitLine(from, 1, 1, n.Center, 1, 1)) continue;
				best = i; bestSq = dsq;
			}
			return best;
		}

		public override void AI()
		{
			SoldierNPC s = Soldier;
			if (s == null || !s.IsAlive)
			{
				if (Main.netMode == NetmodeID.MultiplayerClient) return;
				Projectile.Kill(); return;
			}
			Projectile.rotation += LevyConstants.YoyoSpinPerTick;
			float tick = ++Projectile.localAI[0];
			if (Main.netMode == NetmodeID.MultiplayerClient) return; // the server steers; clients coast on the synced velocity until the next update

			if (!_init)
			{
				int shot = ShotType();
				_reach = WeaponClassifier.YoyoReach(shot);
				_loiter = WeaponClassifier.YoyoLoiterTicks(shot);
				_angle = Main.rand.NextFloat(MathHelper.TwoPi);
				_init = true;
			}

			Vector2 home = s.NPC.Center;
			if (!Returning && tick > _loiter)
			{
				Projectile.ai[2] = 1f;
				Projectile.netUpdate = true;
			}

			if (Returning)
			{
				Vector2 to = home - Projectile.Center;
				float d = to.Length();
				if (d <= LevyConstants.YoyoCatchPx || tick > _loiter + LevyConstants.YoyoHardCapTicks) { Projectile.Kill(); return; }
				Vector2 want = to / Math.Max(1f, d) * Math.Min(LevyConstants.YoyoReturnSpeed, Math.Max(4f, d * 0.5f));
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.35f);
			}
			else
			{
				if (_target < 0 || _target >= Main.maxNPCs || !Valid(Main.npc[_target])) _target = -1;
				if (_target < 0 || ((int)tick % 15) == 0)
				{
					int found = Acquire(s);
					if (found >= 0) _target = found;
				}
				if (_target < 0)
				{
					if (++_lost > LevyConstants.YoyoLoseTargetTicks) { Projectile.ai[2] = 1f; Projectile.netUpdate = true; }
					Projectile.velocity *= 0.88f;
				}
				else
				{
					_lost = 0;
					NPC t = Main.npc[_target];
					_angle += LevyConstants.YoyoOrbitSpeed;
					float radius = Math.Min(LevyConstants.YoyoOrbitRadiusPx, Math.Max(10f, Math.Min(t.width, t.height) * 0.5f));
					Vector2 desired = t.Center + _angle.ToRotationVector2() * radius;
					Vector2 off = desired - home;
					if (off.LengthSquared() > _reach * _reach) desired = home + off.SafeNormalize(Vector2.UnitX) * _reach;
					Vector2 to = desired - Projectile.Center;
					Vector2 want = to * 0.4f;
					float max = LevyConstants.YoyoMaxSpeed;
					if (want.LengthSquared() > max * max) want = want.SafeNormalize(Vector2.UnitX) * max;
					Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.5f);
				}
				// the string: never stray beyond reach of the soldier
				Vector2 next = Projectile.Center + Projectile.velocity;
				Vector2 nOff = next - home;
				if (nOff.LengthSquared() > _reach * _reach)
				{
					next = home + nOff.SafeNormalize(Vector2.UnitX) * _reach;
					Projectile.velocity = next - Projectile.Center;
				}
			}
			if (((int)tick & 3) == 0) Projectile.netUpdate = true;
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (!LevyUtil.IsHostileTarget(target) || SoldierTargeting.IsNeverTarget(target)) return false;
			return null;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Credit, boss-rule accounting, kill count and the KILL bark come from LootCreditGlobalProjectile.
			SoldierNPC s = Soldier;
			if (s != null) s.LastMeleeHitTick = (int)Main.GameUpdateCount;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ) return false;
			try
			{
				SoldierNPC s = Soldier;
				if (s != null) DrawString(s.NPC.Center + new Vector2(0f, -6f), Projectile.Center);
				int t = ShotType();
				if (t <= 0 || t >= TextureAssets.Projectile.Length || t == Type) return true;
				Main.instance.LoadProjectile(t);
				var asset = TextureAssets.Projectile[t];
				if (asset == null || !asset.IsLoaded) return true;
				Texture2D tex = asset.Value;
				int frames = Math.Max(1, Main.projFrames[t]);
				int frame = (int)(Projectile.localAI[0] / 4f) % frames;
				Rectangle src = tex.Frame(1, frames, 0, frame);
				Color c = Lighting.GetColor((int)(Projectile.Center.X / 16f), (int)(Projectile.Center.Y / 16f));
				Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src, c, Projectile.rotation, src.Size() * 0.5f, 1f, SpriteEffects.None);
				return false;
			}
			catch (Exception) { return true; }
		}

		/// <summary>A thin cord from the soldier's hand to the yoyo.</summary>
		internal static void DrawString(Vector2 a, Vector2 b)
		{
			Vector2 d = b - a;
			float len = d.Length();
			if (len < 2f) return;
			Texture2D px = TextureAssets.MagicPixel.Value;
			Color lit = Lighting.GetColor((int)(b.X / 16f), (int)(b.Y / 16f));
			Color c = new Color(225, 225, 225).MultiplyRGBA(lit);
			Main.EntitySpriteDraw(px, a - Main.screenPosition, new Rectangle(0, 0, 1, 1), c, d.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len, 1.4f), SpriteEffects.None);
		}
	}
}
