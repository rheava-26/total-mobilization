using System;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Util;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// THE PROJECTILE/NPC PAIR LOOP. Terraria's Projectile.Damage walks every NPC slot for every damaging projectile, every tick, and for each live enemy asks the mods (CombinedHooks.CanHitNPCWithProj: every
	/// GlobalProjectile's CanHitNPC, every GlobalNPC's CanBeHitByProjectile) before it tests the hitboxes. An army of 36 levies with their minions keeps 200 to 300 projectiles in the air and the Destroyer is 80 NPCs
	/// long: 35 000 pairs a tick, a third of the whole tick, of which about nine are hits. Nearly all of them are a shot on one side of the arena asked about a segment on the other.
	///
	/// <see cref="SoldierProjectileTag.CanHitNPC"/> (Levy's own hook, already part of that dispatch) answers those pairs "no" with <see cref="Far"/> before the mods after it are asked, which is exactly what a hook veto does:
	/// the NPC is refused and the loop moves on. Only an ARMY projectile with the plain hit test is ever refused: a modded projectile that overrides neither Colliding nor ModifyDamageHitbox, or a vanilla one of an ordinary
	/// shot AI style (arrows, bullets, thrown, explosive). The cut-off is the hitbox plus a margin (<see cref="ModMarginPx"/> / <see cref="VanillaMarginPx"/>, several times what any stock collision reaches) plus the
	/// distance the projectile moves in a tick. Nothing in the world is touched. LEVY_PAIRCHK=1 (lab) refuses nothing and instead asks the real hit test of every pair the cull would have refused and records how far
	/// beyond its plain reach any hit ever landed: violations must stay 0. OFF BY DEFAULT: LEVY_PAIRCULL=1 turns it on. Measured on the Destroyer lab it refuses 35% of the pairs (the rest belong to special-shape projectiles or are near enough) and saves 2% of the tick (the dispatch up to this hook and the vanilla loop are the cost), with 0 violations over 100 million audited pairs and no hit ever landing beyond the plain reach (maxHitExcessPx 0). Left in for a build where Levy's hook comes first in the dispatch.
	/// </summary>
	public static class PairCull
	{
		public static readonly bool Enabled = LevyProf.Opt && Environment.GetEnvironmentVariable("LEVY_PAIRCULL") == "1";
		internal static readonly bool Audit = Enabled && Environment.GetEnvironmentVariable("LEVY_PAIRCHK") == "1";
		/// <summary>Margin beyond the hitbox, px. A stock hit test reaches no further than the hitbox; the margin is for global hooks that stretch an ordinary shot.</summary>
		public const float ModMarginPx = 160f, VanillaMarginPx = 256f;
		/// <summary>Vanilla AI styles whose collision is the plain hitbox: bullets, arrows, thrown things, explosives.</summary>
		private static readonly bool[] PlainVanillaStyle = MakePlain();
		private static bool[] MakePlain() { var a = new bool[256]; foreach (int s in new[] { 0, ProjAIStyleID.Arrow, ProjAIStyleID.ThrownProjectile, ProjAIStyleID.Explosive }) a[s] = true; return a; }

		/// <summary>Lab counters.</summary>
		public static long Refused, Audited, Violations, Eligible, Ineligible; public static float MaxHitExcessPx;
		private static sbyte[] _kind = new sbyte[0]; // per projectile type: 0 unknown, 1 plain mod projectile, 2 plain vanilla, -1 not plain

		/// <summary>0 = this projectile is never culled; otherwise the margin to add to its hitbox (px). Decided once per projectile.</summary>
		public static float Classify(Projectile p)
		{
			if (!Enabled || p.hostile) { Ineligible++; return 0f; }
			int t = p.type;
			if (t < 0) return 0f;
			if (_kind.Length <= t) Array.Resize(ref _kind, Math.Max(t + 1, ProjectileLoader.ProjectileCount));
			sbyte c = _kind[t];
			if (c == 0)
			{
				if (t >= ProjectileID.Count)
				{
					ModProjectile mp = p.ModProjectile;
					c = mp != null && !Overrides(mp.GetType(), "Colliding") && !Overrides(mp.GetType(), "ModifyDamageHitbox") ? (sbyte)1 : (sbyte)-1;
				}
				else c = p.aiStyle >= 0 && p.aiStyle < PlainVanillaStyle.Length && PlainVanillaStyle[p.aiStyle] ? (sbyte)2 : (sbyte)-1;
				_kind[t] = c;
			}
			if (c < 0) { Ineligible++; return 0f; }
			Eligible++;
			return c == 1 ? ModMarginPx : VanillaMarginPx;
		}

		/// <summary>True when <paramref name="target"/> is farther from the projectile than anything it could hit (in audit mode never: it only records).</summary>
		public static bool Far(Projectile p, NPC target, float margin)
		{
			float reach = 0.5f * Math.Max(p.width, p.height) + (Math.Abs(p.velocity.X) + Math.Abs(p.velocity.Y)) * (1 + p.extraUpdates);
			float dx = Math.Abs(target.Center.X - p.Center.X) - 0.5f * target.width, dy = Math.Abs(target.Center.Y - p.Center.Y) - 0.5f * target.height;
			if (Audit)
			{
				bool hit = false;
				try { hit = p.Colliding(p.Hitbox, target.Hitbox); } catch (Exception) { }
				if (hit) { float excess = Math.Max(dx, dy) - reach; if (excess > MaxHitExcessPx) MaxHitExcessPx = excess; }
				if (dx > reach + margin || dy > reach + margin) { Audited++; if (hit) Violations++; }
				return false;
			}
			if (dx <= reach + margin && dy <= reach + margin) return false;
			Refused++;
			if (LevyProf.On) LevyProf.PairsCulled++;
			return true;
		}

		/// <summary>Does <paramref name="type"/> (or a base class below ModProjectile) declare its own <paramref name="method"/>?</summary>
		private static bool Overrides(Type type, string method)
		{
			for (Type t = type; t != null && t != typeof(ModProjectile); t = t.BaseType)
				if (t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null) return true;
			return false;
		}
	}
}
