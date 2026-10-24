using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.NPCs;
using SkyDropSpec = Levy.Common.LevyConstants.SkyDropSpec;
using SkyDropAi1 = Levy.Common.LevyConstants.SkyDropAi1;
using SkyDropAnchor = Levy.Common.LevyConstants.SkyDropAnchor;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Sky-drop weapons (Starfury, Star Wrath, Blizzard Staff, Meteor Staff, Lunar Flare, Daedalus Stormbow, Blood Rain Bow): the shot is called down from the sky onto the target instead of
	/// leaving the soldier. Vanilla hard-codes each in Player.ItemCheck_Shoot with the cursor as the aim point; the soldier's target stands in for it. The rows live in
	/// <see cref="LevyConstants.SkyDropWeapons"/>; this class is the one spawn routine they all share. Called from WeaponUse before any other default shot.
	///
	/// Per projectile: roll the start (600 px above the higher of the soldier and the target, X spread as the row says), lead the target for the shot's real speed (extra updates included),
	/// aim down at it with vanilla's scatter, then give it the soldier's grade / veteran / shaken aim error. A shot that collides with terrain in flight (arrows, hail, meteors) needs an open path
	/// from its start: when vanilla's start is walled in (the soldier under a roof, a hill between) it starts straight above the target instead, and only when even that is shut does the
	/// soldier fire the weapon's plain shot from the muzzle, still with the row's ai[1] (so a meteor is never the invisible scale-0 one). Stars and flares fly through the ground until they reach the
	/// target's height, so they always fall.
	///
	/// MP: runs where the soldier's AI runs (server / single player) and spawns through <see cref="WeaponUse.Spawn"/>, so every projectile is owned by Main.myPlayer (ai[] assigned, the spawn packet
	/// sent), tagged to the soldier for loot credit and set bonuses, and carries its ai[1] in the spawn packet. All dice are rolled at the spawn site; clients only draw the synced projectile.
	/// </summary>
	public static class SkyDrop
	{
		/// <summary>Everything one cast shares, so the per-projectile routine does not take a dozen parameters.</summary>
		private struct Cast
		{
			public SoldierNPC Soldier;
			public SkyDropSpec Spec;
			public DamageClass Class;
			public Vector2 Center, Aim, AimVel;
			public float HalfWidth, RefY, Speed, JitterDeg, LeadError, Knockback;
			public int Facing, Type, Damage;
		}

		/// <summary>
		/// Rains the weapon's shot onto <paramref name="target"/>. <paramref name="muzzle"/>, <paramref name="vel"/>, <paramref name="type"/>, <paramref name="damage"/> and <paramref name="knockback"/> are the
		/// shot the weapon would otherwise fire (after the item's Shoot hooks): its speed is the launch speed, and the plain muzzle shot is the last resort. Returns false when the item is not a sky-drop
		/// weapon or nothing could be spawned, and the caller fires its usual shot.
		/// </summary>
		public static bool TryFire(SoldierNPC s, Item item, WeaponProfile profile, NPC target, Vector2 muzzle, Vector2 vel, int type, int damage, float knockback)
		{
			if (s == null || item == null || target == null || type <= 0 || !LevyConstants.SkyDropWeapons.TryGetValue(item.type, out SkyDropSpec spec)) return false;
			Vector2 center = s.NPC.Center, aim = target.Center;
			if (center.HasNaNs() || aim.HasNaNs()) return false;
			float speed = vel.HasNaNs() ? 0f : vel.Length();
			var cast = new Cast
			{
				Soldier = s, Spec = spec, Class = item.DamageType ?? profile?.DamageType ?? DamageClass.Default,
				Center = center, Aim = aim, AimVel = target.velocity.HasNaNs() ? Vector2.Zero : target.velocity,
				HalfWidth = s.NPC.width * 0.5f, RefY = Math.Min(center.Y, aim.Y), Speed = Math.Max(1f, speed),
				Facing = s.NPC.direction == 0 ? 1 : s.NPC.direction, Type = type,
				Damage = Math.Max(1, (int)(damage * spec.DamageFactor)), Knockback = knockback * spec.KnockbackFactor,
			};
			WeaponUse.GradeError(s, WeaponUse.Muzzle(s), aim, out cast.JitterDeg, out cast.LeadError);

			int count = RollCount(spec, type), spawned = 0;
			for (int k = 0; k < count; k++)
				if (DropOne(in cast, k)) spawned++;
			if (spawned > 0) return true;
			return spec.TerrainBlocks && FromMuzzle(in cast, muzzle, vel);
		}

		/// <summary>The direction the weapon is held while it fires: toward the target, except the two bows that point at the sky their arrows fall from (vanilla aims them 1000 px above the cursor).</summary>
		public static Vector2 HeldAim(Item item, Vector2 from, NPC target, Vector2 shotVel)
		{
			if (item == null || target == null || !LevyConstants.SkyDropWeapons.TryGetValue(item.type, out SkyDropSpec spec) || spec.HeldAimLiftPx <= 0f) return shotVel;
			Vector2 aim = target.Center - from - new Vector2(0f, spec.HeldAimLiftPx);
			return aim.HasNaNs() || aim == Vector2.Zero ? shotVel : aim;
		}

		/// <summary>One planned projectile: where it spawns, how it flies, its type and its ai[1].</summary>
		private struct Drop
		{
			public Vector2 Spawn, Velocity;
			public int Type;
			public float Ai1;
		}

		/// <summary>One projectile of the cast. Returns false when it found no start or the projectile array was full.</summary>
		private static bool DropOne(in Cast c, int k) =>
			Plan(in c, k, out Drop d) && WeaponUse.Spawn(c.Soldier, d.Spawn, d.Velocity, d.Type, c.Damage, c.Knockback, c.Class, 0f, d.Ai1) != null;

		/// <summary>Works out projectile <paramref name="k"/> of the cast: the start, the lead, vanilla's aim and scatter, the soldier's aim error and the row's ai[1]. False when no start has an open path.</summary>
		private static bool Plan(in Cast c, int k, out Drop drop)
		{
			drop = default;
			SkyDropSpec spec = c.Spec;
			int type = Variant(spec, c.Type);
			// A shot with extra updates (Star Wrath 1, hail 1, meteors 2, flares 5) covers that many times its launch speed each tick, which is what the lead and the fall time must use.
			float reach = c.Speed * spec.SpeedFactor * (1f + Math.Max(0, WeaponClassifier.ProjectileSample(type)?.extraUpdates ?? 0));

			Vector2 start = RollStart(in c, k);
			Vector2 landing = Predict(start, reach, c.Aim, c.AimVel, out Vector2 lead);
			Vector2 spawn = spec.ShiftPx > 0 ? ClampToWorld(start + new Vector2(Main.rand.Next(-spec.ShiftPx, spec.ShiftPx + 1), 0f)) : start;
			if (spec.TerrainBlocks && !PathClear(spawn, landing))
			{
				// Vanilla's start is walled in (the soldier under a roof, a hill between): rain from straight above the target while there is open air for it.
				if (!OpenStart(spec, landing, k, out start)) return false;
				landing = Predict(start, reach, c.Aim, c.AimVel, out lead);
				spawn = start;
			}

			// Vanilla's aim: down at the target, never flatter than the minimum fall, normalised to the shot's speed, then its own scatter.
			Vector2 d = landing - start;
			float dx = d.X + (spec.AimJitter > 0 ? Main.rand.Next(-spec.AimJitter, spec.AimJitter + 1) * spec.AimJitterStep : 0f);
			float dy = Math.Max(Math.Abs(d.Y), LevyConstants.SkyDropMinFallPx);
			float norm = c.Speed / (float)Math.Sqrt(dx * dx + dy * dy);
			Vector2 v = new Vector2(dx * norm, dy * norm);
			if (spec.JitterX > 0) v.X += Main.rand.Next(-spec.JitterX, spec.JitterX + 1) * spec.JitterXStep;
			if (spec.JitterY > 0) v.Y += Main.rand.Next(-spec.JitterY, spec.JitterY + 1) * spec.JitterYStep;
			if (spec.XMulMaxPct > spec.XMulMinPct) v.X *= Main.rand.Next(spec.XMulMinPct, spec.XMulMaxPct) * 0.01f;
			v *= spec.SpeedFactor;
			// The soldier's own aim error on top: a green recruit's rain lands wide, a veteran's on the mark.
			v = Aiming.ApplyError(v, c.JitterDeg, c.LeadError, lead, start, c.Aim);

			drop = new Drop { Spawn = spawn, Velocity = v, Type = type, Ai1 = Ai1For(in c, start, landing) };
			return true;
		}

		/// <summary>The weapon's plain shot, last resort when no start above the target was open (a cave, a sealed room): fired from the muzzle but with the row's ai[1].</summary>
		private static bool FromMuzzle(in Cast c, Vector2 muzzle, Vector2 vel)
		{
			int type = Variant(c.Spec, c.Type);
			return WeaponUse.Spawn(c.Soldier, muzzle, vel, type, c.Damage, c.Knockback, c.Class, 0f, Ai1For(in c, muzzle, c.Aim)) != null;
		}

		/// <summary>How many projectiles this cast drops: the row's base, its random extra, and the 1-in-N bonus (or, for a heavy arrow, the 1-in-N loss).</summary>
		private static int RollCount(SkyDropSpec spec, int type)
		{
			int n = spec.Count + (spec.CountExtra > 0 ? Main.rand.Next(spec.CountExtra + 1) : 0);
			if (spec.BonusOneIn > 0 && Main.rand.NextBool(spec.BonusOneIn))
				n += spec.FewerWhenHeavy && IsHeavy(type) ? -1 : 1;
			return Math.Max(1, n);
		}

		private static bool IsHeavy(int type)
		{
			bool[] set = ProjectileID.Sets.FiresFewerFromDaedalusStormbow;
			return set != null && type >= 0 && type < set.Length && set[type];
		}

		/// <summary>The Meteor Staff's meteor is one of three sizes of one projectile; every other row fires the type it is given.</summary>
		private static int Variant(SkyDropSpec spec, int type) => spec.Variants > 1 && type == spec.VariantBase ? type + Main.rand.Next(spec.Variants) : type;

		/// <summary>
		/// Where projectile <paramref name="k"/> starts: the row's drop height above the higher of the soldier and the target (stacked higher for each further projectile), at an X rolled from the row's
		/// anchor toward the soldier's back, averaged with the soldier's X by the row's weights, then scattered.
		/// </summary>
		private static Vector2 RollStart(in Cast c, int k)
		{
			SkyDropSpec spec = c.Spec;
			float anchor = spec.Anchor == SkyDropAnchor.Wielder ? c.Center.X : c.Aim.X + c.HalfWidth;
			float x = anchor - Main.rand.Next(spec.SpreadPx + 1) * c.Facing;
			if (spec.WielderParts > 0) x = (x * spec.AimParts + c.Center.X * spec.WielderParts) / (spec.AimParts + spec.WielderParts);
			if (spec.ScatterPx > 0) x += Main.rand.Next(-spec.ScatterPx, spec.ScatterPx + 1);
			return ClampToWorld(new Vector2(x, c.RefY - spec.DropHeightPx - Stack(spec, k)));
		}

		/// <summary>How much higher projectile <paramref name="k"/> starts than the first.</summary>
		private static float Stack(SkyDropSpec spec, int k) => spec.StackRandom ? spec.StackStepPx * Main.rand.NextFloat() : spec.StackStepPx * k;

		/// <summary>
		/// A start straight above the landing point, as high as the open air allows (the row's full drop height and stack first, then a shrinking fraction of both so a cramped sky keeps the
		/// projectiles in order), or false when no start with a clear path to it exists.
		/// </summary>
		private static bool OpenStart(SkyDropSpec spec, Vector2 landing, int k, out Vector2 start)
		{
			float full = spec.DropHeightPx + Stack(spec, k);
			foreach (float fraction in LevyConstants.SkyDropOpenHeightFractions)
			{
				float height = full * fraction;
				if (height < LevyConstants.SkyDropMinOpenHeightPx) break;
				// scattered sideways for spread, then dead above (the one column the target's own tile guarantees) before giving up this height
				start = ClampToWorld(new Vector2(landing.X + Main.rand.Next(-LevyConstants.SkyDropOpenScatterPx, LevyConstants.SkyDropOpenScatterPx + 1), landing.Y - height));
				if (PathClear(start, landing)) return true;
				start = ClampToWorld(new Vector2(landing.X, landing.Y - height));
				if (PathClear(start, landing)) return true;
			}
			start = default;
			return false;
		}

		/// <summary>
		/// Whether a thin ray from <paramref name="from"/> to <paramref name="to"/> crosses no solid tile, sampled every <see cref="LevyConstants.SkyDropRayStepPx"/>. Not Collision.CanHitLine: that checks a band
		/// three tiles wide, so a target standing against a wall or a cliff foot (where sieges happen) would have no open path from anywhere. A tile is solid as it is to a flying projectile
		/// (active, not actuated, solid and not a platform): slopes and half blocks count, a closed door counts, the world outside the map does not.
		/// </summary>
		private static bool PathClear(Vector2 from, Vector2 to)
		{
			Vector2 d = to - from;
			float len = d.Length();
			if (len < 1f || d.HasNaNs()) return true;
			Vector2 step = d / len * LevyConstants.SkyDropRayStepPx;
			int n = (int)(len / LevyConstants.SkyDropRayStepPx);
			Vector2 p = from;
			for (int i = 0; i <= n; i++, p += step)
			{
				int x = (int)(p.X / 16f), y = (int)(p.Y / 16f);
				if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) continue;
				Tile t = Main.tile[x, y];
				if (t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) return false;
			}
			return true;
		}

		private static Vector2 ClampToWorld(Vector2 p)
		{
			float margin = LevyConstants.SkyDropWorldMarginPx;
			return new Vector2(MathHelper.Clamp(p.X, margin, Main.maxTilesX * 16f - margin), Math.Max(p.Y, margin));
		}

		/// <summary>
		/// Where the target will be when a shot launched from <paramref name="from"/> at <paramref name="reach"/> px per tick arrives, and how far it moves meanwhile (the same two passes as
		/// <see cref="Aiming.SolveIntercept"/>, which returns only a heading). A falling shot has no gravity to allow for: vanilla's don't.
		/// </summary>
		private static Vector2 Predict(Vector2 from, float reach, Vector2 pos, Vector2 vel, out Vector2 lead)
		{
			lead = Vector2.Zero;
			if (vel == Vector2.Zero || reach < 0.01f) return pos;
			Vector2 at = pos;
			float t = 0f;
			for (int i = 0; i < LevyConstants.AimRefineIterations; i++)
			{
				t = MathHelper.Clamp(Vector2.Distance(from, at) / reach, 0f, LevyConstants.AimMaxLeadTicks);
				at = pos + vel * t;
			}
			lead = vel * t;
			return at;
		}

		/// <summary>The projectile's ai[1] for this row (ai[0] is always 0): a frame, a scale, or the height at which the shot starts to collide with terrain.</summary>
		private static float Ai1For(in Cast c, Vector2 start, Vector2 landing)
		{
			SkyDropSpec spec = c.Spec;
			switch (spec.Ai1)
			{
				case SkyDropAi1.Frame: return Main.rand.Next(Math.Max(1, spec.Ai1Frames));
				case SkyDropAi1.Scale: return spec.Ai1ScaleMin + (float)Main.rand.NextDouble() * spec.Ai1ScaleRange;
				case SkyDropAi1.TargetY: return landing.Y;
				case SkyDropAi1.TargetYAboveWielder:
				{
					// Vanilla: solid from 200 px above the caster down. If the fall from there is walled off (a cave roof, a hill) the star would burst in the rock, so it runs on to the target's height.
					float y = Math.Min(landing.Y, c.Center.Y - spec.Ai1CapAbovePx);
					return y < landing.Y && !FallIsOpen(start, landing, y) ? landing.Y : y;
				}
			}
			return 0f;
		}

		/// <summary>Whether the fall from the height <paramref name="y"/> on the line start -> landing down to the landing point crosses no terrain.</summary>
		private static bool FallIsOpen(Vector2 start, Vector2 landing, float y)
		{
			float span = landing.Y - start.Y;
			float t = span > 1f ? MathHelper.Clamp((y - start.Y) / span, 0f, 1f) : 0f;
			return PathClear(Vector2.Lerp(start, landing, t), landing);
		}
	}
}
