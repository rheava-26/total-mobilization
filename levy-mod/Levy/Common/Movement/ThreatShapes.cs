using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Movement
{
	/// <summary>A hostile projectile that warns of an explosion it has not made yet (a telegraphed area). A ModProjectile implements it; other mods' are read by <see cref="ThreatShapes.AreaOf"/>.</summary>
	public interface IThreatArea
	{
		/// <summary>Radius (px) around the projectile's centre the coming explosion covers; 0 = none.</summary>
		float AreaRadius(Projectile p);
		/// <summary>Ticks until it goes off (0 = it is going off now).</summary>
		int AreaFuse(Projectile p);
	}

	/// <summary>
	/// The SHAPE of a modded hostile projectile, read from the projectile itself, never from its name where it can be avoided. Each answer is cached per projectile type.
	///  * BEAMS: a deathray is a line, not a point. Two shapes are recognised: a laserbeam base class with a <c>LaserLength</c> (Calamity's <c>BaseLaserbeamProjectile</c>: Thanatos's beam, Ares's Blenderbeam,
	///    Artemis's Ohio Beam, every Calamity boss's beam built on it; its line width is <c>Size.Length() * scale</c>, what the base Colliding passes), and the vanilla deathray pattern, a Colliding override
	///    that tests a line of length <c>localAI[1]</c> (Ares's Exothermal Laser, Providence's and the Brimstone rays): its IL is read once for the call to Collision.CheckAABBvLineCollision and the width constant.
	///  * AREAS: a projectile that telegraphs an explosion: <see cref="IThreatArea"/>, a small table of known telegraphs (the radius the game draws), and a growing blast built on a massive-explosion base class
	///    (<c>CurrentRadius</c> = ai[0], <c>MaxRadius</c> = ai[1], hitbox <c>CurrentRadius * scale</c> wide).
	/// </summary>
	public static class ThreatShapes
	{
		/// <summary>Harness switches: LEVY_MODBEAMS=0 (modded beams are points again), LEVY_AREAS=0 (no telegraphed areas).</summary>
		public static bool BeamsOn = Environment.GetEnvironmentVariable("LEVY_MODBEAMS") != "0", AreasOn = Environment.GetEnvironmentVariable("LEVY_AREAS") != "0";

		// ---- beams ----
		private sealed class BeamKind { public int Kind; public PropertyInfo Length; public float Width; }   // Kind 0 none, 1 laserbeam base (LaserLength), 2 deathray pattern (localAI[1])
		private static BeamKind[] _beam = new BeamKind[0];

		/// <summary>True (with the line's current length and width) when this modded projectile is a beam.</summary>
		public static bool BeamOf(Projectile p, out float len, out float width)
		{
			len = width = 0f;
			if (!BeamsOn || p.ModProjectile == null) return false;
			if (p.type >= _beam.Length) Array.Resize(ref _beam, Math.Max(p.type + 1, ProjectileLoader.ProjectileCount));
			var k = _beam[p.type] ??= ClassifyBeam(p.ModProjectile.GetType());
			switch (k.Kind)
			{
				case 1:
					try { len = (float)k.Length.GetValue(p.ModProjectile); } catch (Exception) { return false; }
					width = p.Size.Length() * p.scale;
					return len > 0f;
				case 2:
					len = p.localAI[1]; width = k.Width * p.scale;
					return len > 0f;
			}
			return false;
		}

		private static BeamKind ClassifyBeam(Type t)
		{
			try
			{
				for (Type b = t; b != null && b != typeof(ModProjectile); b = b.BaseType)
					if (b.Name == "BaseLaserbeamProjectile")
					{
						var lp = b.GetProperty("LaserLength", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
						if (lp != null && lp.PropertyType == typeof(float)) return new BeamKind { Kind = 1, Length = lp };
					}
				if (DeathrayPattern(t, out float w)) return new BeamKind { Kind = 2, Width = w > 0f ? w : 22f };
			}
			catch (Exception) { }
			return new BeamKind();
		}

		private static Dictionary<short, OpCode> _ops;
		/// <summary>The type's Colliding calls Collision.CheckAABBvLineCollision and reads Projectile.localAI[1] (the vanilla deathray shape); <paramref name="width"/> is the last float constant before that call.</summary>
		private static bool DeathrayPattern(Type t, out float width)
		{
			width = 0f;
			MethodInfo m = t.GetMethod("Colliding", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Rectangle), typeof(Rectangle) }, null);
			if (m == null || m.DeclaringType == typeof(ModProjectile)) return false;
			byte[] il = m.GetMethodBody()?.GetILAsByteArray();
			if (il == null) return false;
			if (_ops == null)
			{
				_ops = new Dictionary<short, OpCode>();
				foreach (FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)) if (f.GetValue(null) is OpCode o) _ops[o.Value] = o;
			}
			Module mod = m.Module;
			bool calls = false, la1 = false, afterLocalAI = false; float lastR4 = 0f;
			int i = 0;
			while (i < il.Length)
			{
				short v = il[i++];
				if (v == 0xFE && i < il.Length) v = unchecked((short)(0xFE00 | il[i++]));
				if (!_ops.TryGetValue(v, out OpCode op)) return false;
				int at = i, size;
				switch (op.OperandType)
				{
					case OperandType.InlineNone: size = 0; break;
					case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: size = 1; break;
					case OperandType.InlineVar: size = 2; break;
					case OperandType.InlineI8: case OperandType.InlineR: size = 8; break;
					case OperandType.InlineSwitch: size = 4 + 4 * BitConverter.ToInt32(il, at); break;
					default: size = 4; break;
				}
				if (op == OpCodes.Ldc_R4) lastR4 = BitConverter.ToSingle(il, at);
				bool wasLocalAI = afterLocalAI; afterLocalAI = false;
				if (op.OperandType == OperandType.InlineField)
				{
					try { FieldInfo f = mod.ResolveField(BitConverter.ToInt32(il, at)); afterLocalAI = f != null && f.Name == "localAI" && f.DeclaringType == typeof(Projectile); } catch (Exception) { }
				}
				else if (wasLocalAI && op == OpCodes.Ldc_I4_1) la1 = true;
				if (op == OpCodes.Call || op == OpCodes.Callvirt)
				{
					try { MethodBase mb = mod.ResolveMethod(BitConverter.ToInt32(il, at)); if (mb != null && mb.Name == "CheckAABBvLineCollision" && mb.DeclaringType == typeof(Collision)) { calls = true; width = lastR4; } } catch (Exception) { }
				}
				i = at + size;
			}
			return calls && la1;
		}

		// ---- areas ----
		/// <summary>Known telegraphs (mod/name: the radius the game draws in px, or the hit-radius factor of a growing blast). Data: a new telegraph is one more line.</summary>
		private static readonly Dictionary<string, float> KnownRadius = new()
		{
			// draws a 1480 px telegraph circle (radius 740) for its whole 180-tick flight; its damaging wave then grows to hit a player within 0.4 x 740 x scale, scale reaching 4.56 by its last hitting tick:
			// 1350 px. Lab recordings (exofixes) saw levies die 900 to 1350 px out, 20 to 50 ticks after the blast, so the area is where it reaches, not the circle it draws.
			["CalamityMod/AresGaussNukeProjectile"] = 1350f,
			["CalamityMod/CrimsonEye"] = 124f,                // Calamity's Brain of Cthulhu eye: a 248 px telegraph circle
		};
		/// <summary>Growing blasts whose player hit test is a circle smaller than the hitbox: the fraction of CurrentRadius * scale it reaches (the base hitbox is 0.5 of it).</summary>
		private static readonly Dictionary<string, float> BlastFactor = new() { ["CalamityMod/AresGaussNukeProjectileBoom"] = 0.4f };

		private sealed class AreaKind { public int Kind; public float R; }   // 0 none, 1 interface, 2 known telegraph, 3 growing blast (R = hit factor)
		private static AreaKind[] _area = new AreaKind[0];
		/// <summary>Ticks of growth a fleeing levy is given against a blast that is still expanding.</summary>
		private const float BlastLeadScale = 1.3f;

		/// <summary>True (with the radius and the ticks until it goes off) when this projectile marks an area that will hurt.</summary>
		public static bool AreaOf(Projectile p, out float radius, out int fuse)
		{
			radius = 0f; fuse = 0;
			if (!AreasOn || p.ModProjectile == null) return false;
			if (p.type >= _area.Length) Array.Resize(ref _area, Math.Max(p.type + 1, ProjectileLoader.ProjectileCount));
			var k = _area[p.type] ??= ClassifyArea(p.ModProjectile);
			switch (k.Kind)
			{
				case 1:
					var a = (IThreatArea)p.ModProjectile; radius = a.AreaRadius(p); fuse = Math.Max(0, a.AreaFuse(p)); return radius > 0f;
				case 2:
					radius = k.R; fuse = p.timeLeft * (1 + p.extraUpdates); return true;
				case 3:
					if (p.damage <= 0) return false; // a wave drawn for show (the Gauss Nuke's first two) hurts nobody
					// a blast that is still growing: where its edge will be a few ticks on (its scale climbs 1.2 -> 5 over its life), so a levy near the edge keeps running
					radius = k.R * Math.Max(p.ai[0], p.ai[1] * 0.75f) * Math.Min(5f, p.scale + BlastLeadScale);
					return radius > 16f;
			}
			return false;
		}

		private static AreaKind ClassifyArea(ModProjectile mp)
		{
			try
			{
				if (mp is IThreatArea) return new AreaKind { Kind = 1 };
				string key = mp.Mod.Name + "/" + mp.Name;
				if (KnownRadius.TryGetValue(key, out float r)) return new AreaKind { Kind = 2, R = r };
				for (Type b = mp.GetType(); b != null && b != typeof(ModProjectile); b = b.BaseType)
					if (b.Name == "BaseMassiveExplosionProjectile") return new AreaKind { Kind = 3, R = BlastFactor.TryGetValue(key, out float f) ? f : 0.5f };
			}
			catch (Exception) { }
			return new AreaKind();
		}
	}
}
