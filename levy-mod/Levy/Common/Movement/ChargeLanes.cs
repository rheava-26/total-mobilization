using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Movement
{
	/// <summary>
	/// A CHARGE LANE: the band a big boss body is about to sweep, known from its wind-up instead of from the first tick of the dash. A charge is a straight line, so the way out is across it, not along it.
	/// Calamity's Leviathan charges in NPC.ai[0] == 2: ai[1] even is the line-up (damage 0, it flies to 900 px beside its target and level with it), ai[1] odd is the dash (velocity aimed once, then constant) and the brake
	/// (damage 0 again) that ends in the next line-up, so the whole state is one lane at the boss's own height. Any other very large body that is already moving fast along a line is a lane from its velocity.
	/// All pure geometry on a snapshot built once per tick for the whole army; a soldier only asks "am I in a lane, and which way out".
	/// </summary>
	public struct ChargeLane
	{
		public int Npc;
		/// <summary>0 a charger hovering level with its target (the line-up can be a single tick long), 1 line-up, 2 dash (or brake: the next line-up is a tick away), 3 a fast body seen by its velocity.</summary>
		public byte Kind;
		/// <summary>Centre line (world Y, px) and half height (px) of the band the body sweeps, margin included.</summary>
		public float Cy, Half;
		/// <summary>Heading along X (-1 / +1), the boss's centre X, and how far along the heading the band reaches (px).</summary>
		public int Dir; public float OriginX, Reach, Speed;
	}

	public static class ChargeLanes
	{
		/// <summary>Harness switch (LEVY_CHARGELANE=0): no lane reading, soldiers dodge a charge the old way (only once its dash is under way and by the threat field alone).</summary>
		public static bool On = Environment.GetEnvironmentVariable("LEVY_CHARGELANE") != "0";
		public const float MarginPx = 18f, ClearPx = 8f, FastBodyMinSpeed = 14f, FastBodyMinHalf = 80f, FastBodyMaxSlope = 0.5f, LeviathanReachPx = 3600f;
		private static readonly List<ChargeLane> Cur = new(2);
		private static uint _tick = uint.MaxValue; private static int _leviType = -2;
		public static int LanesSeen, Builds;

		/// <summary>The lanes of this tick (empty almost always). Built once per tick.</summary>
		public static List<ChargeLane> Get()
		{
			uint now = Main.GameUpdateCount;
			if (_tick == now) return Cur;
			_tick = now; Cur.Clear();
			if (!On) return Cur;
			Builds++;
			if (_leviType == -2) _leviType = ModContent.TryFind("CalamityMod/Leviathan", out ModNPC m) ? m.Type : -1;
			float armyX = float.NaN;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.townNPC || n.ModNPC is Content.NPCs.SoldierNPC) continue;
				ChargeLane lane;
				if (n.type == _leviType && _leviType > 0)
				{
					if (float.IsNaN(armyX)) armyX = ArmyX(n.Center.X);
					if (!LeviathanLane(n.ai[0], n.ai[1], n.Opacity, n.Center, n.width * n.scale, n.height * n.scale, n.velocity, armyX, CanCharge(n), out lane)) continue;
					lane.Npc = i; Cur.Add(lane); LanesSeen++;
				}
				else if (FastBodyLane(n.Center, n.velocity, n.width, n.height, n.damage, out lane)) { lane.Npc = i; Cur.Add(lane); LanesSeen++; }
			}
			return Cur;
		}

		/// <summary>Mean X of the soldiers on the player's side (where a charge is aimed); the boss's own X when there are none.</summary>
		private static float ArmyX(float fallback)
		{
			float sum = 0f; int c = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is Content.NPCs.SoldierNPC sn && !sn.Hostile && n.life > 0) { sum += n.Center.X; c++; }
			}
			return c > 0 ? sum / c : fallback;
		}

		private static System.Reflection.PropertyInfo _deathP; private static System.Reflection.FieldInfo _deathF; private static int _anaType = -2;
		/// <summary>Whether the Leviathan's AI may pick a charge now (Calamity: death mode, or life under 20%, or under 70% / 40% with Anahita not fighting). A line-up can start the same tick the hover ends, so a body that may charge keeps a lane while it hovers.</summary>
		private static bool CanCharge(NPC n)
		{
			try
			{
				if (_deathP == null && _deathF == null && ModLoader.TryGetMod("CalamityMod", out Mod cal))
				{
					Type t = cal.Code.GetType("CalamityMod.World.CalamityWorld");
					_deathP = t?.GetProperty("death", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static); _deathF = t?.GetField("death", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
				}
				bool death = _deathP != null ? (bool)_deathP.GetValue(null) : _deathF != null && (bool)_deathF.GetValue(null);
				if (death) return true;
				float r = n.life / (float)Math.Max(1, n.lifeMax);
				if (r < 0.2f) return true;
				if (_anaType == -2) _anaType = ModContent.TryFind("CalamityMod/Anahita", out ModNPC a) ? a.Type : -1;
				bool siren = _anaType > 0 && NPC.AnyNPCs(_anaType);
				return Main.expertMode && r < (siren ? 0f : 0.7f);
			}
			catch (Exception) { return false; }
		}

		/// <summary>The Leviathan's charge state as a lane: ai0 == 2 (ai1 even: line-up, odd: dash and brake), and while it may charge, the hover (ai0 0 or 1) before it. Not charging, or still fading in from its spawn: no lane.</summary>
		public static bool LeviathanLane(float ai0, float ai1, float opacity, Vector2 center, float width, float height, Vector2 vel, float armyX, bool canCharge, out ChargeLane lane)
		{
			lane = default;
			if (opacity < 0.9f) return false;
			bool hover = (int)ai0 == 0 || (int)ai0 == 1;
			if ((int)ai0 != 2 && !(hover && canCharge)) return false;
			bool dash = ((int)ai1 & 1) == 1;
			int dir = Math.Abs(vel.X) > 6f ? Math.Sign(vel.X) : Math.Sign(armyX - center.X);   // moving: its heading (a brake still points the way it came); idle: toward the army
			if (dir == 0) dir = 1;
			lane = new ChargeLane { Kind = (byte)(hover ? 0 : dash ? 2 : 1), Cy = center.Y, Half = height * 0.5f + MarginPx, Dir = dir, OriginX = center.X, Reach = LeviathanReachPx, Speed = Math.Max(20f, vel.Length()) };
			return true;
		}

		/// <summary>A very large hostile body already moving fast and nearly level: the lane is its own height along its heading.</summary>
		public static bool FastBodyLane(Vector2 center, Vector2 vel, float width, float height, int damage, out ChargeLane lane)
		{
			lane = default;
			float sp = vel.Length();
			if (damage <= 0 || sp < FastBodyMinSpeed || height * 0.5f < FastBodyMinHalf || Math.Abs(vel.Y) > FastBodyMaxSlope * sp) return false;
			lane = new ChargeLane { Kind = 3, Cy = center.Y + vel.Y / Math.Max(1f, Math.Abs(vel.X)) * 0f, Half = height * 0.5f + MarginPx, Dir = Math.Sign(vel.X), OriginX = center.X, Reach = Math.Max(1600f, sp * 90f), Speed = sp };
			return lane.Dir != 0;
		}

		/// <summary>Is a body (centre c, half height) inside the band, from a body length behind the boss to the end of the reach?</summary>
		public static bool InLane(in ChargeLane l, Vector2 c, float halfH, float bossHalfW = 450f)
		{
			if (Math.Abs(c.Y - l.Cy) > l.Half + halfH) return false;
			float along = (c.X - l.OriginX) * l.Dir;
			return along >= -bossHalfW && along <= l.Reach;
		}

		/// <summary>Top speed (px/tick) of a fall out of a lane; a body falls faster than it flies, so a drop clears a band in about two thirds of the time a climb does.</summary>
		public const float FallPx = 9f;

		/// <summary>
		/// The way out of a lane: across it. Up needs flight (wings) and fuel for the climb. Down needs a floor far enough below to land beyond the band (a fall: from the air, or by dropping through the platform it stands on;
		/// no fuel needed) or flight. Both open: the soldier's own parity picks (so a charge cannot line the whole army up on one side) unless the other way is much quicker, and a spread (0..1) adds clearance so the escaped
		/// soldiers do not stack where the next line-up will look. Returns the Y (px, body centre) to reach.
		/// </summary>
		public static bool Plan(in ChargeLane l, float y, float halfH, bool canFly, float flyPxPerTick, int wingTicks, bool canDrop, float floorY, int parity, float spread01, out int dir, out float targetY)
		{
			dir = 0; targetY = y;
			float extra = spread01 * 6f * 16f;   // the climb is dear (fuel) so it takes a third of the spread; a fall is cheap
			float upY = l.Cy - l.Half - halfH - ClearPx - extra * 0.33f, downY = l.Cy + l.Half + halfH + ClearPx + extra;
			bool up = canFly && (y - upY) / Math.Max(1f, flyPxPerTick) <= wingTicks;
			bool floorOk = downY <= floorY;
			bool down = floorOk && (canDrop || canFly && (downY - y) / Math.Max(1f, flyPxPerTick) <= wingTicks);
			if (!up && !down) return false;
			if (up && down)
			{
				float cu = (y - upY) / Math.Max(1f, flyPxPerTick), cd = (downY - y) / FallPx;
				bool pickUp = parity == 0 ? cu <= cd * 1.6f : cu <= cd * 0.6f;   // each side takes the quicker way unless the other is clearly further
				dir = pickUp ? -1 : 1;
			}
			else dir = up ? -1 : 1;
			targetY = dir < 0 ? upY : downY;
			return true;
		}
	}
}
