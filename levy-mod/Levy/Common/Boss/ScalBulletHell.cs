using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Common.Compat.Calamity;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Boss
{
	/// <summary>
	/// SUPREME CALAMITAS'S BULLET HELL ON THE WHOLE ARENA, while an army fights her. Calamity writes every bullet-hell shot relative to her target: <c>target.position + (rand(-1000, 1000), -1000)</c> from above, <c>target.position + (+-1000, rand(-1000, 1000))</c>
	/// from the sides, aimed along an axis (SupremeCalamitas.AI, every phase's <c>bulletHellCounter2</c> branch; eleven call sites a phase). For a player that is a ring of shots that arrive from off-screen. When the target is a levy
	/// (the army swap puts a levy's stand-in where the player stood for her AI call) the ring is centred on that levy, which is anywhere in the arena: the shots are born in the middle of the commander's screen and hit no one else.
	/// <para/>While at least one levy stands in her arena box, each such shot is moved by the offsets it was given: the arena's centre replaces the target, and the +-1000 that was the ring's reach becomes the arena's own half size plus a margin
	/// (<see cref="LevyConstants.ScalBhMarginPx"/>), so the shots are born just outside the walls on all four sides along their whole length and fly in across the whole arena, through every levy. Direction, speed, damage and timing are hers; only where a shot is born changes.
	/// Solo play (no levy in her box) is untouched: every shot is born where Calamity put it. A shot is recognised by its shape (one offset exactly +-1000 along an axis, the other inside +-1000), which no other attack of hers has.
	/// On by default when an army is present (the designer's ask); <c>LEVY_SCALBH=0</c> turns it off (lab A/B).
	/// </summary>
	public class ScalBulletHell : ModSystem
	{
		public static bool On = Environment.GetEnvironmentVariable("LEVY_SCALBH") != "0";
		/// <summary>Harness telemetry: bullet-hell shaped shots seen, shots moved to the arena, shots seen with no army (left alone).</summary>
		public static long Seen, Moved, SoloKept;
		/// <summary>Lab: when set, every bullet-hell shot's birth (as Calamity asked for it, as it was born, moved or not) is appended here.</summary>
		public static List<(Vector2 asked, Vector2 born, bool moved)> Log;
		public static bool Installed { get; private set; }

		private delegate void OrigAI(ModNPC self);
		private delegate int OrigNewProj(IEntitySource source, float x, float y, float sx, float sy, int type, int damage, float knockback, int owner, float ai0, float ai1, float ai2);

		/// <summary>The Supreme Calamitas whose AI is running now (null outside it) and whether an army is in her arena.</summary>
		private static NPC _scal; private static bool _army; private static Vector2 _centre, _half;

		public override void PostSetupContent()
		{
			try
			{
				if (!On || !ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
				Type t = cal.Code.GetType("CalamityMod.NPCs.SupremeCalamitas.SupremeCalamitas");
				MethodInfo ai = t?.GetMethod("AI", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
				MethodInfo np = typeof(Projectile).GetMethod("NewProjectile", new[] { typeof(IEntitySource), typeof(float), typeof(float), typeof(float), typeof(float), typeof(int), typeof(int), typeof(float), typeof(int), typeof(float), typeof(float), typeof(float) });
				if (ai == null || np == null || ai.DeclaringType != t) { Mod.Logger.Warn("Levy: Supreme Calamitas bullet-hell hook not installed (AI or NewProjectile not found)"); return; }
				MonoModHooks.Add(ai, (Action<OrigAI, ModNPC>)AiDetour);
				MonoModHooks.Add(np, (Func<OrigNewProj, IEntitySource, float, float, float, float, int, int, float, int, float, float, float, int>)NewProjDetour);
				Installed = true;
				Mod.Logger.Info("Levy: Supreme Calamitas bullet hell on the arena: hooks installed");
			}
			catch (Exception e) { Mod.Logger.Warn("Levy: Supreme Calamitas bullet-hell hook failed to install: " + e.Message); }
		}

		private static void AiDetour(OrigAI orig, ModNPC self)
		{
			NPC n = self.NPC;
			_scal = null; _army = false;
			try
			{
				if (On && n != null && ArenaOf(self, out Vector2 tl, out Vector2 br) && ArmyIn(tl, br))
				{
					_army = true; _centre = (tl + br) * 0.5f; _half = (br - tl) * 0.5f + new Vector2(LevyConstants.ScalBhMarginPx);
				}
				_scal = n;
				orig(self);
			}
			finally { _scal = null; _army = false; }
		}

		private static FieldInfo _boxField; private static Type _boxType;
		/// <summary>Her arena box (her own <c>ArenaBox</c>), else the box that holds her.</summary>
		private static bool ArenaOf(ModNPC self, out Vector2 tl, out Vector2 br)
		{
			tl = br = default;
			try
			{
				if (_boxField == null || _boxField.DeclaringType != self.GetType()) { _boxField = self.GetType().GetField("ArenaBox", BindingFlags.Public | BindingFlags.Instance); _boxType = _boxField?.FieldType; }
				object box = _boxField?.GetValue(self);
				if (box != null && _boxType != null)
				{
					PropertyInfo ptl = _boxType.GetProperty("TopLeft"), pbr = _boxType.GetProperty("BottomRight");
					if (ptl != null && pbr != null) { tl = (Vector2)ptl.GetValue(box); br = (Vector2)pbr.GetValue(box); if (br.X > tl.X && br.Y > tl.Y) return true; }
				}
			}
			catch (Exception) { }
			return ArenaBoxes.TryBounds(self.NPC, out tl, out br);
		}

		/// <summary>True when a soldier of the army stands inside the arena's walls.</summary>
		private static bool ArmyIn(Vector2 tl, Vector2 br)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC s = Main.npc[i];
				if (!s.active || !(s.ModNPC is SoldierNPC sn) || !sn.IsAlive || sn.Hostile) continue;
				Vector2 c = s.Center;
				if (c.X >= tl.X && c.X <= br.X && c.Y >= tl.Y && c.Y <= br.Y) return true;
			}
			return false;
		}

		private static int NewProjDetour(OrigNewProj orig, IEntitySource source, float x, float y, float sx, float sy, int type, int damage, float kb, int owner, float a0, float a1, float a2)
		{
			NPC scal = _scal;
			if (scal != null && scal.active && source is EntitySource_Parent ps && ReferenceEquals(ps.Entity, scal))
			{
				int ti = scal.target;
				if (ti >= 0 && ti < Main.maxPlayers + 1 && Main.player[ti] != null)
				{
					Vector2 refPos = Main.player[ti].position;
					float dx = x - refPos.X, dy = y - refPos.Y;
					const float R = LevyConstants.ScalBhVanillaHalfPx, tol = 0.6f;
					bool bh = (Math.Abs(Math.Abs(dx) - R) < tol && Math.Abs(dy) <= R + tol) || (Math.Abs(Math.Abs(dy) - R) < tol && Math.Abs(dx) <= R + tol);
					if (bh)
					{
						Seen++;
						Vector2 asked = new Vector2(x, y);
						if (_army)
						{
							x = _centre.X + dx / R * _half.X; y = _centre.Y + dy / R * _half.Y; Moved++;
							Log?.Add((asked, new Vector2(x, y), true));
						}
						else { SoloKept++; Log?.Add((asked, asked, false)); }
					}
				}
			}
			return orig(source, x, y, sx, sy, type, damage, kb, owner, a0, a1, a2);
		}
	}
}
