using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Util;                 // Levy.Common.Util.LevyUtil.IsHostileTarget  (cross-mod reference in content)
using T = Mechanization.Common.MechConstants.Turret;

namespace Mechanization.Content.NPCs
{
	/// <summary>
	/// A deployable automatic turret: a friendly, non-town, <c>aiStyle = -1</c> NPC that does NOT move. It falls under
	/// gravity until it rests on terrain, then sits still, acquires the nearest hostile in range and fires a fixed
	/// vanilla round on a cadence. It carries its own HP and a drawn healthbar, takes contact damage from melee enemies
	/// (vanilla applies no NPC-vs-NPC contact, so the turret applies it itself, exactly as Levy's DroneNPC does) and is
	/// damaged by hostile projectiles, and it is destroyed when its HP reaches 0.
	///
	/// Modeled on <see cref="Levy.Content.NPCs"/>' combat drone, minus the Barracks/Armory garrison: the turret owns no
	/// loan, so its whole lifecycle is simply spawn → fight → die. The hostile test reuses Levy's own
	/// <see cref="LevyUtil.IsHostileTarget"/> — a live cross-mod call, so the turret genuinely depends on Levy at runtime.
	///
	/// Server / single player drives the brain and fires; clients draw the borrowed sprite (turned to the aim synced in
	/// ai[0]) and the healthbar. Transient in v0: not saved, so a turret is lost on world reload (a later phase gives it
	/// a tile entity / save support and, with it, Levy-Armory weapons and enemy aggro).
	/// </summary>
	public class TurretNPC : ModNPC
	{
		// ---- brain state (server / single player) ----
		private int _age, _immuneTime, _scan, _target = -1, _cooldown;
		/// <summary>The drawn aim of the barrel (radians), eased toward the wanted one; a client eases it toward the synced ai[0].</summary>
		private float _aim;

		// A valid vanilla asset so content loads; the real body is drawn in PreDraw and this is never actually blitted.
		public override string Texture => "Terraria/Images/Item_" + T.BodyDrawItemType;

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 1;
			NPCID.Sets.ActsLikeTownNPC[Type] = true;        // keeps it out of hostile spawn logic, as Levy's drone/soldier do
			NPCID.Sets.NoTownNPCHappiness[Type] = true;
			NPCID.Sets.CantTakeLunchMoney[Type] = true;
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
		}

		public override void SetDefaults()
		{
			NPC.width = T.BodyWidth; NPC.height = T.BodyHeight;
			NPC.friendly = true; NPC.townNPC = false;
			NPC.aiStyle = -1;
			NPC.damage = 0; NPC.defense = T.DefenseBase;
			NPC.lifeMax = T.HpBase; NPC.life = NPC.lifeMax;
			NPC.knockBackResist = 0f;                        // immovable
			NPC.npcSlots = 0f;                               // never thins the hostile spawn pressure
			NPC.HitSound = SoundID.NPCHit4;                  // metallic
			NPC.DeathSound = SoundID.NPCDeath14;             // small explosion
			NPC.lavaImmune = false;
			NPC.noGravity = true;                            // core adds no gravity for aiStyle -1; the turret applies its own in AI
			NPC.noTileCollide = false;                       // so it rests on solid ground
			NPC.rarity = 0; NPC.value = 0f;
			NPC.netAlways = true;                            // a stationary combat object is always relevant to nearby clients
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) { }

		// =====================================================================================================
		// AI
		// =====================================================================================================
		public override void AI()
		{
			if (LevyUtil.IsClient) { _aim = EaseAngle(_aim, NPC.ai[0], T.AimTurnLerp); return; }

			_age++;
			if (_immuneTime > 0) _immuneTime--;

			ApplyGravity();
			if (_immuneTime == 0) ContactDamage();
			if (!NPC.active || NPC.life <= 0) return;

			Combat();
			bool engaging = _target >= 0 && _target < Main.maxNPCs && Shootable(Main.npc[_target]);
			float wantAim = _aim;
			if (engaging)
			{
				NPC t = Main.npc[_target];
				Vector2 to = t.Center - NPC.Center;
				if (to != Vector2.Zero) wantAim = to.ToRotation();
				if (Math.Abs(to.X) > 4f) { NPC.direction = to.X < 0f ? -1 : 1; NPC.spriteDirection = NPC.direction; }
				if (_cooldown <= 0) { Fire(t); _cooldown = T.FireIntervalTicks; }
			}
			else wantAim = NPC.direction < 0 ? MathHelper.Pi : 0f; // idle: barrel level, facing its last way

			_aim = EaseAngle(_aim, wantAim, T.AimTurnLerp);
			NPC.ai[0] = _aim; // rides the NPC sync so clients draw the barrel where the server aims it

			if ((Main.GameUpdateCount + (uint)NPC.whoAmI) % 30u == 0) NPC.netUpdate = true;
		}

		private static float EaseAngle(float from, float to, float t) => MathHelper.WrapAngle(from + MathHelper.WrapAngle(to - from) * t);

		/// <summary>Falls until it rests on terrain, then stays put. The core applies this velocity with tile collision.</summary>
		private void ApplyGravity()
		{
			NPC.velocity.X = 0f;
			NPC.velocity.Y += T.GravityPerTick;
			if (NPC.velocity.Y > T.MaxFallSpeed) NPC.velocity.Y = T.MaxFallSpeed;
			NPC.rotation = 0f;
		}

		// ---- combat ----
		private void Combat()
		{
			if (_cooldown > 0) _cooldown--;
			if (++_scan >= T.ScanIntervalTicks) { _scan = 0; _target = FindTarget(); }
			else if (_target >= 0 && (_target >= Main.maxNPCs || !Shootable(Main.npc[_target]))) _target = -1;
		}

		/// <summary>Nearest hostile in reach with a clear line. Range cut runs first so the hostile test and line test are only paid for the slots actually in reach.</summary>
		private int FindTarget()
		{
			float rangeSq = T.RangePx * T.RangePx;
			Vector2 from = NPC.Center;
			int best = -1; float bestSq = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active) continue;
				float dSq = Vector2.DistanceSquared(from, t.Center);
				if (dSq >= bestSq || dSq > rangeSq) continue;
				if (!LevyUtil.IsHostileTarget(t)) continue;   // Levy's proven hostile filter (cross-mod)
				if (!Collision.CanHitLine(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height)) continue;
				bestSq = dSq; best = i;
			}
			return best;
		}

		private bool Shootable(NPC t)
		{
			if (!LevyUtil.IsHostileTarget(t)) return false;
			if (Vector2.DistanceSquared(NPC.Center, t.Center) > T.RangePx * T.RangePx) return false;
			return Collision.CanHitLine(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height);
		}

		/// <summary>One shot: a fixed vanilla round aimed straight at the target, friendly and owned by the real player so it behaves as a friendly projectile and syncs.</summary>
		private void Fire(NPC target)
		{
			Vector2 from = NPC.Center;
			Vector2 to = target.Center - from;
			Vector2 dir = to.SafeNormalize(Vector2.UnitX * (NPC.direction == 0 ? 1 : NPC.direction));
			Vector2 vel = dir * T.ShotSpeed;
			Vector2 muzzle = from + dir * T.MuzzleOffsetPx;
			if (Collision.SolidCollision(muzzle - new Vector2(2f, 2f), 4, 4)) muzzle = from; // a muzzle buried in a tile would kill the round on spawn

			int owner = Main.myPlayer; // 255 on a dedicated server: the correct owner for a server-spawned friendly projectile
			int idx = Projectile.NewProjectile(NPC.GetSource_FromAI(), muzzle.X, muzzle.Y, vel.X, vel.Y, T.ProjectileType, T.ShotDamage, T.ShotKnockback, owner);
			if (idx >= 0 && idx < Main.maxProjectiles)
			{
				Projectile p = Main.projectile[idx];
				p.friendly = true; p.hostile = false;
				p.netUpdate = true;
			}
		}

		// ---- taking contact hits (vanilla applies no NPC-vs-NPC contact, so the turret applies it itself) ----
		private void ContactDamage()
		{
			Rectangle mine = NPC.Hitbox;
			float cx = NPC.Center.X, cy = NPC.Center.Y;
			float halfW = NPC.width * 0.5f + T.ContactBroadPhasePx, halfH = NPC.height * 0.5f + T.ContactBroadPhasePx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC a = Main.npc[i];
				if (!a.active || a.friendly || a.damage <= 0 || i == NPC.whoAmI) continue; // only hostiles that deal contact damage
				if (Math.Abs(a.Center.X - cx) > a.width * 0.5f + halfW || Math.Abs(a.Center.Y - cy) > a.height * 0.5f + halfH) continue;
				if (!NPCLoader.CanHitNPC(a, NPC)) continue;
				int slot = -1; float mult = 1f;
				Rectangle rect = a.Hitbox;
				NPC.GetMeleeCollisionData(mine, i, ref slot, ref mult, ref rect);
				if (!mine.Intersects(rect)) continue;

				int dir = a.Center.X < cx ? 1 : -1;
				int raw = Math.Max(1, Main.DamageVar(a.damage * mult));
				NPC.SimpleStrikeNPC(raw, dir, false, 0f, null, false, 0f, true); // the server broadcasts the strike
				_immuneTime = T.ImmuneTicksOnHit;
				if (NPC.active && NPC.life > 0) NPC.netUpdate = true;
				return; // one contact hit per tick
			}
		}

		// =====================================================================================================
		// Vanilla NPC hooks
		// =====================================================================================================
		public override bool CheckDead() => true;
		public override bool PreKill() => false;              // no vanilla loot
		public override bool CheckActive() => false;          // a placed turret does not despawn by distance
		public override bool NeedSaving() => false;           // transient in v0: lost on world reload
		public override bool UsesPartyHat() => false;
		public override bool CanChat() => false;
		public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
		public override bool? CanBeHitByItem(Player player, Item item) => false;                 // no friendly fire from the owner's tools
		public override bool? CanBeHitByProjectile(Projectile projectile) => projectile.friendly && !projectile.hostile ? false : null; // hostile projectiles hit; friendly ones do not
		public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false; // we draw our own bar

		public override void HitEffect(NPC.HitInfo hit)
		{
			if (Main.netMode == NetmodeID.Server) return;
			int sparks = NPC.life > 0 ? 3 : 18;
			for (int i = 0; i < sparks; i++)
			{
				int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Iron, hit.HitDirection * 1.8f, -1.5f);
				Main.dust[d].scale = 0.9f;
			}
			if (NPC.life <= 0)
			{
				for (int i = 0; i < 8; i++)
					Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Smoke, hit.HitDirection, -2f, 100, default, 1.3f);
			}
		}

		// =====================================================================================================
		// Draw: borrow a vanilla item sprite (no new art) turned to the aim, plus a healthbar.
		// =====================================================================================================
		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (Main.dedServ) return false;
			try
			{
				int item = T.BodyDrawItemType;
				Main.instance.LoadItem(item);
				Texture2D tex = TextureAssets.Item[item]?.Value;
				if (tex == null || tex.Width <= 0 || tex.Height <= 0) return false;
				float scale = Math.Min(1f, T.BodyDrawMaxPx / Math.Max(tex.Width, tex.Height));
				Vector2 origin = new Vector2(tex.Width, tex.Height) * 0.5f;
				Vector2 pos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);
				Color c = Lighting.GetColor((int)(NPC.Center.X / 16f), (int)(NPC.Center.Y / 16f), drawColor);
				// Flip the sprite upright when the barrel points left, so the cannon never draws upside-down.
				SpriteEffects fx = Math.Cos(_aim) < 0.0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
				spriteBatch.Draw(tex, pos, null, c, _aim, origin, scale, fx, 0f);
				return false;
			}
			catch (Exception e)
			{
				Mechanization.Instance.Logger.Warn("Mechanization: drawing a turret failed: " + e.Message);
				return false;
			}
		}

		public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (Main.dedServ) return;
			int w = T.HpBarWidthPx, h = T.HpBarHeightPx;
			Vector2 top = NPC.Top - screenPos;
			float frac = MathHelper.Clamp(NPC.life / (float)Math.Max(1, NPC.lifeMax), 0f, 1f);
			var pixel = TextureAssets.MagicPixel.Value;
			int bx = (int)(top.X - w * 0.5f), by = (int)(top.Y - T.HpBarOffsetY);
			spriteBatch.Draw(pixel, new Rectangle(bx - 1, by - 1, w + 2, h + 2), new Color(20, 20, 20, 200));
			spriteBatch.Draw(pixel, new Rectangle(bx, by, (int)(w * frac), h), Color.Lerp(Color.Red, Color.LimeGreen, frac));
		}
	}
}
