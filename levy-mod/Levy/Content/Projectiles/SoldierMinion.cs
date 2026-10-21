using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Content.Projectiles
{
	/// <summary>
	/// STAND-IN minion, used only when a summon staff's minion cannot be run as the real vanilla projectile (see <see cref="Common.Weapons.SoldierMinions"/>): modded minions (their AI
	/// needs a real owner's buffs), the Stardust Dragon's linked segments, and any vanilla type that threw while driven. Everything else is the real projectile.
	/// One minion of a summoner soldier: ai[0] = soldier whoAmI, ai[1] = the summon weapon's minion projectile type (drawn as that sprite),
	/// ai[2] = chased NPC index + 1 (0 = none). Trails behind its soldier, dives at the nearest hostile (tagged ones first), backs off after
	/// each hit (or, for shooter minions in <see cref="RangedMinions"/>, holds a standoff range and fires instead), and goes home when it has nothing to chase. Owned by Main.myPlayer like every soldier projectile; it vanishes with its soldier.
	/// </summary>
	public class SoldierMinion : ModProjectile
	{
		/// <summary>SoldierMinion.png is a placeholder; PreDraw draws the real minion's texture when it resolves.</summary>
		public override void SetStaticDefaults()
		{
			Main.projFrames[Type] = 1;
		}

		public override void SetDefaults()
		{
			Projectile.width = LevyConstants.MinionHitboxPx;
			Projectile.height = LevyConstants.MinionHitboxPx;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.aiStyle = -1;
			Projectile.timeLeft = LevyConstants.MinionLifeTicks;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = LevyConstants.MinionLocalHitCooldownTicks;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
			Projectile.netImportant = true;
			Projectile.DamageType = DamageClass.Summon;
		}

		private int SoldierIndex => (int)Projectile.ai[0];
		private int VisualType => (int)Projectile.ai[1];

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

		public override bool? CanCutTiles() => false;
		public override bool MinionContactDamage() => false;

		public override void AI()
		{
			SoldierNPC s = Soldier;
			if (s == null || !s.IsAlive)
			{
				// A client may see the minion before the body's record sync: let the server decide when it is really gone.
				if (Main.netMode == NetmodeID.MultiplayerClient) return;
				Projectile.Kill(); return;
			}

			Projectile.timeLeft = LevyConstants.MinionLifeTicks; // refreshed while the soldier lives
			float tick = ++Projectile.localAI[0];
			bool authoritative = Main.netMode != NetmodeID.MultiplayerClient;

			// ---- target ----
			if (authoritative)
			{
				if (s.Record.HoldFire)
				{
					if (((int)tick + Projectile.whoAmI) % LevyConstants.MinionRetargetTicks == 0 && Projectile.ai[2] != 0f) { Projectile.ai[2] = 0f; Projectile.netUpdate = true; }
				}
				else
				{
					// the soldier's own target (which already folds in the commander's focus designation) pulls the flock the moment it is chosen
					int pref = SoldierTargetIfValid(s);
					int cur = (int)Projectile.ai[2] - 1;
					if (pref >= 0 && pref != cur) { Projectile.ai[2] = pref + 1; Projectile.netUpdate = true; }
					else if (pref < 0 && ((int)tick + Projectile.whoAmI) % LevyConstants.MinionRetargetTicks == 0)
					{
						int enc = FindTarget(s) + 1;
						if (enc != (int)Projectile.ai[2]) { Projectile.ai[2] = enc; Projectile.netUpdate = true; }
					}
				}
			}
			NPC target = CurrentTarget(s);

			// ---- movement ----
			if (Projectile.localAI[1] > 0f)
			{
				// recoil after a hit: keep moving away from the victim, slowing, before diving again
				Projectile.localAI[1] -= 1f;
				Projectile.velocity *= 0.94f;
			}
			else if (target != null && RangedMinions.TryGetValue(VisualType, out RangedShot ranged))
			{
				RangedAttack(s, target, in ranged, authoritative);
			}
			else if (target != null)
			{
				Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * LevyConstants.MinionDashSpeed;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, LevyConstants.MinionTurnLerp);
			}
			else
			{
				Vector2 home = TrailPoint(s, tick);
				Vector2 to = home - Projectile.Center;
				float dist = to.Length();
				if (dist > LevyConstants.MinionTeleportDistPx)
				{
					Projectile.Center = home;
					Projectile.velocity = Vector2.Zero;
					if (authoritative) Projectile.netUpdate = true;
				}
				else
				{
					Vector2 want = dist < 1f ? Vector2.Zero : to / dist * Math.Min(LevyConstants.MinionIdleSpeed, dist * 0.18f + 0.5f);
					Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.18f);
				}
			}

			// keep the shared state of the flock fresh on remote screens
			if (Main.netMode == NetmodeID.Server && ((int)tick + Projectile.whoAmI) % 10 == 0) Projectile.netUpdate = true;

			// ---- look ----
			if (Math.Abs(Projectile.velocity.X) > 0.4f) Projectile.spriteDirection = Projectile.direction = Projectile.velocity.X < 0f ? -1 : 1;
			Projectile.rotation = Projectile.velocity.X * 0.04f;
		}

		/// <summary>How a ranged minion shoots: the vanilla projectile it fires, ticks between shots, launch speed, the range it holds, and the effective px/tick used for lead.</summary>
		public readonly struct RangedShot
		{
			public readonly int Proj, CooldownTicks; public readonly float Speed, StandoffPx, LeadSpeed, DamageMult; public readonly int TimeLeft;
			public RangedShot(int proj, int cooldownTicks, float speed, float standoffPx, float leadSpeed, float damageMult = 1f, int timeLeft = 300)
			{ Proj = proj; CooldownTicks = cooldownTicks; Speed = speed; StandoffPx = standoffPx; LeadSpeed = leadSpeed; DamageMult = damageMult; TimeLeft = timeLeft; }
		}

		/// <summary>
		/// Vanilla minions that shoot instead of ramming, keyed by the minion projectile type. Fired ids, speeds and cadence are read from vanilla
		/// Projectile.AI_062 / AI_066 / AI_054 (cooldown = threshold / average counter gain per tick). LeadSpeed accounts for extraUpdates. Anything
		/// not listed keeps the dive-and-recoil melee behaviour.
		/// </summary>
		private static readonly System.Collections.Generic.Dictionary<int, RangedShot> RangedMinions = new System.Collections.Generic.Dictionary<int, RangedShot>
		{
			{ ProjectileID.Hornet,       new RangedShot(ProjectileID.HornetStinger,       36, 10f, 240f, 10f) },
			{ ProjectileID.FlyingImp,    new RangedShot(ProjectileID.ImpFireball,         68, 11f, 280f, 22f) },
			{ ProjectileID.Tempest,      new RangedShot(ProjectileID.MiniSharkron,        30, 20f, 300f, 20f) },
			{ ProjectileID.UFOMinion,    new RangedShot(ProjectileID.UFOLaser,            24,  4f, 380f, 0f,  1f, 100) },
			{ ProjectileID.Retanimini,   new RangedShot(ProjectileID.MiniRetinaLaser,     45,  8f, 320f, 24f, 1.15f) },
			{ ProjectileID.Pygmy,        new RangedShot(ProjectileID.PygmySpear,          56, 11f, 280f, 22f) },
			{ ProjectileID.Pygmy2,       new RangedShot(ProjectileID.PygmySpear,          56, 11f, 280f, 22f) },
			{ ProjectileID.Pygmy3,       new RangedShot(ProjectileID.PygmySpear,          56, 11f, 280f, 22f) },
			{ ProjectileID.Pygmy4,       new RangedShot(ProjectileID.PygmySpear,          56, 11f, 280f, 22f) },
		};

		/// <summary>Keys added at runtime by other mods' bridges, so they can be removed again on unload / re-registration without touching the vanilla entries.</summary>
		private static readonly System.Collections.Generic.HashSet<int> RegisteredKeys = new System.Collections.Generic.HashSet<int>();

		/// <summary>
		/// Registers (or replaces) a shooter minion: any soldier stand-in drawing <paramref name="minionProjType"/> will hold a standoff range and fire
		/// <see cref="RangedShot.Proj"/> instead of ramming. Intended for weak-reference bridges to other mods; call during load (PostSetupContent).
		/// </summary>
		public static void RegisterRangedMinion(int minionProjType, RangedShot shot)
		{
			if (minionProjType <= 0 || shot.Proj <= 0) return;
			lock (RangedMinions)
			{
				RangedMinions[minionProjType] = shot;
				RegisteredKeys.Add(minionProjType);
			}
		}

		/// <summary>Raw-field overload of <see cref="RegisterRangedMinion(int, RangedShot)"/> (ticks between shots, launch speed, held range px, effective px/tick for lead, damage multiplier, shot lifetime; 0 keeps the projectile's own).</summary>
		public static void RegisterRangedMinion(int minionProjType, int proj, int cooldownTicks, float speed, float standoffPx, float leadSpeed, float damageMult = 1f, int timeLeft = 300)
			=> RegisterRangedMinion(minionProjType, new RangedShot(proj, cooldownTicks, speed, standoffPx, leadSpeed, damageMult, timeLeft));

		/// <summary>Removes every entry added through <see cref="RegisterRangedMinion(int, RangedShot)"/>; the vanilla table is untouched.</summary>
		public static void ClearRegisteredRangedMinions()
		{
			lock (RangedMinions)
			{
				foreach (int k in RegisteredKeys) RangedMinions.Remove(k);
				RegisteredKeys.Clear();
			}
		}

		private int _fireCd;

		/// <summary>One ranged minion tick with a target: close to its standoff range with a clear line, hold there, shoot on cooldown.</summary>
		private void RangedAttack(SoldierNPC s, NPC target, in RangedShot shot, bool authoritative)
		{
			if (_fireCd > 0) _fireCd--;
			Vector2 to = target.Center - Projectile.Center;
			float dist = to.Length();
			Vector2 dir = dist < 1f ? Vector2.UnitX : to / dist;
			bool clear = Collision.CanHitLine(Projectile.Center, 1, 1, target.Center, 1, 1);
			Vector2 want;
			if (dist > shot.StandoffPx || !clear) want = dir * LevyConstants.MinionDashSpeed;
			else if (dist < shot.StandoffPx * LevyConstants.MinionRangedBackoffFraction) want = -dir * LevyConstants.MinionIdleSpeed * 0.6f;
			else want = Vector2.Zero;
			Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, LevyConstants.MinionTurnLerp);
			if (Math.Abs(to.X) > 8f) Projectile.spriteDirection = Projectile.direction = to.X < 0f ? -1 : 1;

			if (!authoritative || _fireCd > 0 || !clear || dist > shot.StandoffPx * 1.1f) return;
			try
			{
				Vector2 v = shot.LeadSpeed > 0.01f
					? Aiming.SolveIntercept(Projectile.Center, shot.LeadSpeed, target.Center, target.velocity, false) * (shot.Speed / shot.LeadSpeed)
					: dir * shot.Speed;
				int dmg = Math.Max(1, (int)Math.Round(Projectile.damage * shot.DamageMult));
				// a vanilla MinionShot already gets the tag bonus from SoldierProjectileTag.ModifyHitNPC; other (modded) shots get it here
				bool tagBonusAlready = shot.Proj > 0 && shot.Proj < ProjectileID.Sets.MinionShot.Length && ProjectileID.Sets.MinionShot[shot.Proj];
				if (!tagBonusAlready && LevyNpcData.IsNpcTagged(target)) dmg = Math.Max(1, (int)Math.Round(dmg * (1f + LevyConstants.TagMinionDamageBonus)));
				Projectile p = WeaponUse.Spawn(s, Projectile.Center, v, shot.Proj, dmg, Projectile.knockBack, DamageClass.Summon);
				_fireCd = shot.CooldownTicks;
				if (p == null) return;
				if (shot.TimeLeft > 0) p.timeLeft = shot.TimeLeft;
				p.netUpdate = true;
				Projectile.velocity -= dir * LevyConstants.MinionRangedKickback;
			}
			catch (Exception)
			{
				_fireCd = shot.CooldownTicks; // never retry every tick
			}
		}

		/// <summary>
		/// Where this minion rests while idle: a loose trailing line behind the soldier (opposite its facing) and a little above it, each minion
		/// one step further back and alternately higher/lower so the flock fans out, with a gentle bob. Same idea as vanilla minionPos spacing.
		/// </summary>
		private Vector2 TrailPoint(SoldierNPC s, float tick)
		{
			int slot = FlockSlot(s, tick);
			int back = s.NPC.direction == 0 ? -1 : -s.NPC.direction;
			float phase = slot * 1.7f + Projectile.whoAmI * 0.9f + tick * LevyConstants.MinionBobSpeed;
			float x = back * (LevyConstants.MinionTrailBackPx + slot * LevyConstants.MinionTrailSpacingPx) + (float)Math.Cos(phase * 0.7f) * LevyConstants.MinionBobAmpPx * 0.5f;
			float y = -LevyConstants.MinionTrailLiftPx - (slot % 2) * LevyConstants.MinionTrailStaggerPx + (float)Math.Sin(phase) * LevyConstants.MinionBobAmpPx;
			return s.NPC.Center + new Vector2(x, y);
		}

		/// <summary>This minion's stable place in its soldier's flock (0 = nearest the soldier): its rank by projectile index among that soldier's minions, refreshed a few times a second.</summary>
		private int FlockSlot(SoldierNPC s, float tick)
		{
			if (Projectile.localAI[2] <= 0f || ((int)tick + Projectile.whoAmI) % LevyConstants.MinionMaintainIntervalTicks == 0)
			{
				int rank = 0;
				for (int i = 0; i < Projectile.whoAmI && i < Main.maxProjectiles; i++)
				{
					Projectile o = Main.projectile[i];
					if (o.active && o.type == Projectile.type && (int)o.ai[0] == SoldierIndex) rank++;
				}
				Projectile.localAI[2] = Math.Min(rank, LevyConstants.MinionMaxPerSoldier - 1) + 1f;
			}
			return (int)Projectile.localAI[2] - 1;
		}

		/// <summary>The chased NPC if still valid (alive, hostile, near the soldier), else null.</summary>
		private NPC CurrentTarget(SoldierNPC s)
		{
			int idx = (int)Projectile.ai[2] - 1;
			if (idx < 0 || idx >= Main.maxNPCs) return null;
			NPC n = Main.npc[idx];
			if (!n.active || n.life <= 0 || !LevyUtil.IsHostileTarget(n) || SoldierTargeting.IsNeverTarget(n)) return null;
			if (Vector2.DistanceSquared(n.Center, s.NPC.Center) > LevyConstants.MinionLeashPx * LevyConstants.MinionLeashPx) return null;
			return n;
		}

		/// <summary>The soldier's current target (<c>TargetIndex</c>, which includes the commander's focus designation) if it is a live hostile inside the leash, else -1. Seek range does not apply: minions fly to wherever the soldier is fighting.</summary>
		private static int SoldierTargetIfValid(SoldierNPC s)
		{
			int idx = s.TargetIndex;
			if (idx < 0 || idx >= Main.maxNPCs) return -1;
			NPC n = Main.npc[idx];
			if (!n.active || n.life <= 0 || !LevyUtil.IsHostileTarget(n) || SoldierTargeting.IsNeverTarget(n)) return -1;
			if (Vector2.DistanceSquared(n.Center, s.NPC.Center) > LevyConstants.MinionLeashPx * LevyConstants.MinionLeashPx) return -1;
			return idx;
		}

		/// <summary>Nearest hostile to this minion inside the seek range and the soldier's leash; tagged enemies and the soldier's own target count as closer.</summary>
		private int FindTarget(SoldierNPC s)
		{
			float seekSq = LevyConstants.MinionSeekRangePx * LevyConstants.MinionSeekRangePx;
			float leashSq = LevyConstants.MinionLeashPx * LevyConstants.MinionLeashPx;
			float tagFactor = LevyConstants.MinionTaggedPreference * LevyConstants.MinionTaggedPreference;
			int best = -1; float bestScore = float.MaxValue;
			Vector2 me = Projectile.Center, home = s.NPC.Center;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.life <= 0 || !LevyUtil.IsHostileTarget(n) || SoldierTargeting.IsNeverTarget(n)) continue;
				Vector2 c = n.Center;
				float dSq = Vector2.DistanceSquared(me, c);
				if (dSq > seekSq || Vector2.DistanceSquared(home, c) > leashSq) continue;
				float score = dSq;
				if (LevyNpcData.IsNpcTagged(n)) score *= tagFactor;
				if (i == s.TargetIndex) score *= 0.5f;
				if (score < bestScore) { bestScore = score; best = i; }
			}
			return best;
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (!LevyUtil.IsHostileTarget(target) || SoldierTargeting.IsNeverTarget(target)) return false;
			return null;
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (LevyNpcData.IsNpcTagged(target)) modifiers.SourceDamage += LevyConstants.TagMinionDamageBonus;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Credit, boss-rule accounting, kill count and the KILL bark are applied by LootCreditGlobalProjectile for every tagged soldier projectile.
			Vector2 away = (Projectile.Center - target.Center).SafeNormalize(-Vector2.UnitY);
			Projectile.velocity = away * (LevyConstants.MinionDashSpeed * 0.6f);
			Projectile.localAI[1] = LevyConstants.MinionRecoilTicks;
			Projectile.netUpdate = true;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ) return false;
			try
			{
				int t = VisualType;
				if (t <= 0 || t >= TextureAssets.Projectile.Length || t == Type) return true;
				Main.instance.LoadProjectile(t);
				var asset = TextureAssets.Projectile[t];
				if (asset == null || !asset.IsLoaded) return true;
				Texture2D tex = asset.Value;
				int frames = Math.Max(1, Main.projFrames[t]);
				int frame = (int)(Projectile.localAI[0] / Math.Max(1, LevyConstants.MinionFrameTicks)) % frames;
				Rectangle src = tex.Frame(1, frames, 0, frame);
				Vector2 origin = src.Size() * 0.5f;
				SpriteEffects fx = Projectile.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
				// Vanilla minion sprites face left by default; flipped when travelling right.
				Color c = Lighting.GetColor((int)(Projectile.Center.X / 16f), (int)(Projectile.Center.Y / 16f));
				Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src, c, Projectile.rotation, origin, 1f, fx);
				return false;
			}
			catch (Exception) { return true; }
		}
	}
}
