using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Recording
{
	/// <summary>
	/// Format 15 frame key z: the LINES and LINKS a hostile projectile is, which its width and height cannot say. A deathray's hitbox is a small box at its root while the beam reaches 9,000 px; a linked electric field is a segment between
	/// two orbs. Each row is [x0, y0, x1, y1, width, kind, type, slot] in whole pixels, the same slot as the projectile's row in j.
	/// kind 1 a damaging beam or line, 2 a warning (the line is drawn but not yet dangerous), 3 a link (a field joining two projectiles), 4 a short strike segment.
	/// Read from the projectile itself, by class name where the mod's own geometry must be copied (Wrath of the Gods' laser bases, screen slices, the Avatar's torrent and arm strikes, Calamity's Ares tesla orbs); Calamity's laserbeam
	/// base and the vanilla deathray pattern come from <see cref="Levy.Common.Movement.ThreatShapes.BeamOf"/>. The resolver of each projectile type is chosen once and cached; a type that is none of these costs one array read.
	/// Reads public state only; nothing here changes the game.
	/// </summary>
	internal static class RecLines
	{
		internal const int KBeam = 1, KWarn = 2, KLink = 3, KStrike = 4, MaxRows = 80;
		private enum H { None, Threat, Prim, PrimTele, Slice, Slash, ArmStrike, Torrent, Thorn, TeslaLink }
		private sealed class Kind { public H H; public PropertyInfo Max, Source; public MethodInfo Method; }
		private static Kind[] _k = new Kind[0];
		/// <summary>Types seen with a hostile hitbox of 600 px or more or a line collision that nothing here resolved (log aid for the lab, once per type).</summary>
		internal static readonly HashSet<string> Unresolved = new();

		private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

		private static Kind Classify(Projectile pr)
		{
			var mp = pr.ModProjectile; var k = new Kind();
			if (mp == null) return k;
			try
			{
				for (Type t = mp.GetType(); t != null && t != typeof(ModProjectile); t = t.BaseType)
				{
					switch (t.Name)
					{
						case "BaseTelegraphedPrimitiveLaserbeam": k.H = H.PrimTele; k.Max = FindProp(mp.GetType(), "MaxLaserLength"); return k.Max != null ? k : new Kind();
						case "BasePrimitiveLaserbeam": k.H = H.Prim; k.Max = FindProp(mp.GetType(), "MaxLaserLength"); return k.Max != null ? k : new Kind();
						case "TelegraphedScreenSlice": case "VergilScreenSlice": k.H = H.Slice; return k;
						case "LightSlash": k.H = H.Slash; return k;
						case "PortalArmStrike": k.H = H.ArmStrike; k.Source = FindProp(t, "PortalSource"); return k.Source != null ? k : new Kind();
						case "BloodTorrent": k.H = H.Torrent; k.Source = FindProp(t, "Start"); k.Method = t.GetMethod("BloodWidthFunction", Any); return k.Source != null ? k : new Kind();
						case "OtherworldlyThorn": k.H = H.Thorn; return k;
						case "AresTeslaOrb": k.H = H.TeslaLink; k.Method = t.GetMethod("GetOrbToAttachTo", Any); return k.Method != null ? k : new Kind();
					}
				}
				k.H = H.Threat; // BaseLaserbeamProjectile and the vanilla deathray pattern: asked per projectile, ThreatShapes says whether it is a beam
			}
			catch (Exception) { return new Kind(); }
			return k;
		}
		private static PropertyInfo FindProp(Type t, string name) { for (; t != null; t = t.BaseType) { var p = t.GetProperty(name, Any | BindingFlags.DeclaredOnly); if (p != null) return p; } return null; }

		/// <summary>Adds the line rows of this projectile (if it is one) to <paramref name="rows"/>; true when it added any. <paramref name="slot"/> is its Main.projectile index.</summary>
		internal static bool Collect(Projectile pr, int slot, List<double[]> rows)
		{
			if (rows.Count >= MaxRows) return false;
			if (pr.type >= _k.Length) Array.Resize(ref _k, Math.Max(pr.type + 1, ProjectileLoader.ProjectileCount));
			var k = _k[pr.type] ??= Classify(pr);
			if (k.H == H.None) return false;
			try
			{
				Vector2 c = pr.Center, dir; float len, w;
				switch (k.H)
				{
					case H.Threat:
						if (!global::Levy.Common.Movement.ThreatShapes.BeamOf(pr, out len, out w)) return false;
						dir = pr.velocity; if (dir.LengthSquared() < 1e-6f) return false; dir.Normalize();
						return Add(rows, c, c + dir * len, w, KBeam, pr, slot);
					case H.Prim:
						len = pr.localAI[1] * Convert.ToSingle(k.Max.GetValue(pr.ModProjectile)); dir = pr.velocity; if (dir.LengthSquared() < 1e-6f || len <= 1f) return false; dir.Normalize();
						return Add(rows, c, c + dir * len, pr.scale * pr.width * 0.9f, KBeam, pr, slot);
					case H.PrimTele:
					{
						dir = pr.velocity; if (dir.LengthSquared() < 1e-6f) return false; dir.Normalize();
						float max = Convert.ToSingle(k.Max.GetValue(pr.ModProjectile));
						bool tele = pr.localAI[0] <= pr.ai[0];
						len = tele ? max : pr.localAI[1] * max; if (len <= 1f) return false;
						return Add(rows, c, c + dir * len, pr.scale * pr.width * 0.9f, tele ? KWarn : KBeam, pr, slot);
					}
					case H.Slice:
						len = pr.ai[1]; if (len <= 1f || pr.velocity.LengthSquared() < 1e-6f) return false;
						return Add(rows, c, c + pr.velocity * len, pr.scale * pr.width * 0.9f, pr.localAI[0] <= pr.ai[0] ? KWarn : KBeam, pr, slot);
					case H.Slash:
					{
						Vector2 d = pr.ai[0].ToRotationVector2() * pr.width * pr.scale * 0.5f;
						return Add(rows, c - d, c + d, pr.height * 0.5f, pr.ai[1] < 8f ? KWarn : KStrike, pr, slot);
					}
					case H.ArmStrike:
					{
						Vector2 s = (Vector2)k.Source.GetValue(pr.ModProjectile); if (s == Vector2.Zero) return false;
						return Add(rows, s, c, pr.scale * 40f, KStrike, pr, slot);
					}
					case H.Torrent:
					{
						Vector2 s = (Vector2)k.Source.GetValue(null); if (s == Vector2.Zero) return false;
						float bw = k.Method != null ? Convert.ToSingle(k.Method.Invoke(pr.ModProjectile, new object[] { 0.5f })) : 120f;
						return Add(rows, s, c, Math.Max(32f, bw * 2f), KBeam, pr, slot);
					}
					case H.Thorn:
					{
						float reach = pr.ai[1]; if (reach <= 1f) return false;
						float width = pr.width * reach / Math.Max(1f, pr.ai[0]);
						return Add(rows, c, c - Vector2.UnitY * reach * 0.85f, width * 0.5f, pr.ai[2] <= 0f ? KWarn : KBeam, pr, slot);
					}
					case H.TeslaLink:
					{
						var other = k.Method.Invoke(pr.ModProjectile, null) as Projectile; if (other == null || !other.active) return false;
						return Add(rows, c, other.Center, 8f, KLink, pr, slot);
					}
				}
			}
			catch (Exception) { _k[pr.type] = new Kind(); }
			return false;
		}

		private static bool Add(List<double[]> rows, Vector2 a, Vector2 b, float width, int kind, Projectile pr, int slot)
		{
			if (float.IsNaN(a.X) || float.IsNaN(b.X) || float.IsInfinity(a.X) || float.IsInfinity(b.X)) return false;
			rows.Add(new double[] { Math.Round(a.X), Math.Round(a.Y), Math.Round(b.X), Math.Round(b.Y), Math.Round(width), kind, pr.type, slot });
			return true;
		}
	}
}
