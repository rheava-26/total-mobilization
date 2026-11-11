using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Util;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// THE PROJECTILE/NPC PAIR LOOP. Terraria's Projectile.Damage walks every NPC slot for every damaging projectile, every tick, and for each live enemy asks the mods (CombinedHooks.CanHitNPCWithProj: every
	/// GlobalProjectile's CanHitNPC, every GlobalNPC's CanBeHitByProjectile) before it tests the hitboxes. An army of 36 levies with their minions keeps 200 to 300 projectiles in the air, and the Destroyer is 80 NPCs
	/// long: 35 000 pairs a tick, a third of the whole tick, of which about nine are hits. Almost all of them are a shot on one side of the screen asked about a segment on the other.
	///
	/// This answers those pairs "no" before the mods are asked: for an ARMY projectile (a soldier's shot, minion or tower) whose collision is the plain hitbox, an NPC farther away than any hitbox could reach is refused
	/// exactly as a hook veto would refuse it (CanHitNPCWithProj returns false: the loop moves on). Nothing is changed in the world and nothing is skipped that could have hit: "plain" means a modded projectile that
	/// overrides neither Colliding nor ModifyDamageHitbox, or a vanilla projectile of an ordinary shot AI style, and the cut-off distance is the hitbox plus <see cref="ModMarginPx"/> / <see cref="VanillaMarginPx"/>
	/// (hundreds of pixels) of margin plus the distance the projectile moves in a tick. LEVY_PAIRCHK=1 (lab) asks the real hit test of every refused pair as well and counts the ones that would have hit: it must stay 0.
	/// LEVY_PAIRCULL=0 / LEVY_OPT=0 turn it off.
	/// </summary>
	public class PairCull : ModSystem
	{
		public static readonly bool Enabled = LevyProf.Opt && Environment.GetEnvironmentVariable("LEVY_PAIRCULL") != "0";
		internal static readonly bool Audit = Environment.GetEnvironmentVariable("LEVY_PAIRCHK") == "1";
		/// <summary>Margin beyond the hitbox, px: a modded projectile with the stock hit test reaches no further than its hitbox, so this is generous; vanilla special shapes (a whip, a spear, a flail) are not in the plain set at all, the larger margin covers the global hooks that stretch an ordinary shot.</summary>
		public const float ModMarginPx = 480f, VanillaMarginPx = 960f;
		/// <summary>Vanilla AI styles whose collision is the plain hitbox: arrows, bullets, bolts, grenades, rockets, ordinary magic shots.</summary>
		private static readonly bool[] PlainVanillaStyle = MakePlain();
		private static bool[] MakePlain() { var a = new bool[256]; foreach (int s in new[] { 0, ProjAIStyleID.Arrow, ProjAIStyleID.ThrownProjectile, ProjAIStyleID.Explosive }) a[s] = true; return a; }

		public static long Refused, Asked, Violations, Eligible, Ineligible;

		private static Projectile _cur; private static float _cx, _cy, _reach;
		private static sbyte[] _plain = new sbyte[0]; // per type: 0 unknown, 1 plain mod projectile, 2 plain vanilla, -1 not plain

		private delegate bool? OrigCanHit(Projectile projectile, NPC target);
		private static bool _hooked;

		public override void Load()
		{
			if (!Enabled) return;
			try
			{
				MethodInfo m = typeof(CombinedHooks).GetMethod("CanHitNPCWithProj", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Projectile), typeof(NPC) }, null);
				if (m == null) { Mod.Logger.Warn("PairCull: CombinedHooks.CanHitNPCWithProj not found; the pair loop is left alone."); return; }
				On_Projectile.Damage += DamageDetour;
				MonoModHooks.Add(m, (Func<OrigCanHit, Projectile, NPC, bool?>)CanHitDetour);
				_hooked = true;
			}
			catch (Exception e) { Mod.Logger.Warn("PairCull: could not hook the pair loop: " + e.Message); }
		}

		public override void Unload()
		{
			try { On_Projectile.Damage -= DamageDetour; } catch (Exception) { }
			_cur = null;
		}

		private static void DamageDetour(On_Projectile.orig_Damage orig, Projectile self)
		{
			Projectile prev = _cur;
			_cur = null;
			if (self.damage > 0 && self.friendly && Eligibility(self, out float margin))
			{
				_cur = self; _cx = self.Center.X; _cy = self.Center.Y;
				float vel = self.velocity.Length() * (1 + self.extraUpdates);
				_reach = 0.5f * Math.Max(self.width, self.height) + vel + margin;
			}
			try { orig(self); }
			finally { _cur = prev; }
		}

		private static bool? CanHitDetour(OrigCanHit orig, Projectile projectile, NPC target)
		{
			if (!ReferenceEquals(projectile, _cur)) return orig(projectile, target);
			float dx = Math.Abs(target.Center.X - _cx) - 0.5f * target.width, dy = Math.Abs(target.Center.Y - _cy) - 0.5f * target.height;
			if (dx <= _reach && dy <= _reach) return orig(projectile, target);
			if (LevyProf.On) LevyProf.PairsCulled++;
			if (Audit)
			{
				Asked++;
				bool? r = orig(projectile, target);
				if (!(r.HasValue && !r.Value)) { try { if (projectile.Colliding(projectile.Hitbox, target.Hitbox)) Violations++; } catch (Exception) { } }
				return r;
			}
			Refused++;
			return false;
		}

		/// <summary>True for an army projectile with the plain hit test; <paramref name="margin"/> is the distance to add to its hitbox.</summary>
		private static bool Eligibility(Projectile p, out float margin)
		{
			margin = ModMarginPx;
			if (!p.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex < 0 || p.hostile) { Ineligible++; return false; }
			int t = p.type;
			if (t < 0) return false;
			if (_plain.Length <= t) Array.Resize(ref _plain, Math.Max(t + 1, ProjectileLoader.ProjectileCount));
			sbyte c = _plain[t];
			if (c == 0)
			{
				if (t >= ProjectileID.Count)
				{
					ModProjectile mp = p.ModProjectile;
					c = mp != null && !Overrides(mp.GetType(), "Colliding") && !Overrides(mp.GetType(), "ModifyDamageHitbox") ? (sbyte)1 : (sbyte)-1;
				}
				else c = p.aiStyle >= 0 && p.aiStyle < PlainVanillaStyle.Length && PlainVanillaStyle[p.aiStyle] ? (sbyte)2 : (sbyte)-1;
				_plain[t] = c;
			}
			if (c < 0) { Ineligible++; return false; }
			margin = c == 1 ? ModMarginPx : VanillaMarginPx;
			Eligible++;
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
