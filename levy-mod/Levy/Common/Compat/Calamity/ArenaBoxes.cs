using System;
using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Compat.Calamity
{
	/// <summary>
	/// Calamity's arena boxes (<c>ArenaWallSystem.ActiveBoxes</c>: Calamitas Clone in Revengeance and above, Supreme Calamitas) confine PLAYERS only: <c>ArenaWallPlayer.PreUpdateMovement</c> asks each box
	/// <c>ShouldEffectPlayer(player)</c> and <c>Contains(player.position, player.Size)</c>, and a player the box overlaps is snapped inside it and has its velocity trimmed so the next step cannot carry it out.
	/// A levy is an NPC, so the box never saw it. This runs the same two tests for a levy's body and the same snap and velocity trim, so a levy is held exactly like a player whose hitbox is there.
	/// Cheap by design: with no Calamity, or no active box, it is one static check. Everything is by reflection (Levy has no compile-time reference to CalamityMod); a missing member turns it off.
	/// <c>LEVY_ARENABOX=0</c> switches it off (A/B for the lab and the world check).
	/// </summary>
	public static class ArenaBoxes
	{
		public static readonly bool On = Environment.GetEnvironmentVariable("LEVY_ARENABOX") != "0";
		/// <summary>Harness telemetry: bodies a box held, and the ticks a body was inside the effect range.</summary>
		public static long Held, Checked;
		private static readonly bool Dbg = Environment.GetEnvironmentVariable("LEVY_ARENABOX_DBG") == "1"; private static int _dbgN;

		private static bool _init, _ok;
		private static FieldInfo _boxes;
		private static MethodInfo _should, _contains;
		private static PropertyInfo _topLeft, _bottomRight;
		private static uint _tickStamp = uint.MaxValue; private static IList _list;
		private static Player _stand;
		private static bool _warned;

		private static void Init()
		{
			if (_init) return; _init = true;
			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
				Type sys = cal.Code.GetType("CalamityMod.Systems.Mechanic.ArenaWallSystem");
				Type box = sys?.GetNestedType("Box");
				if (sys == null || box == null) return;
				_boxes = sys.GetField("ActiveBoxes", BindingFlags.Public | BindingFlags.Static);
				_should = box.GetMethod("ShouldEffectPlayer", new[] { typeof(Player) });
				_contains = box.GetMethod("Contains", new[] { typeof(Vector2), typeof(Vector2) });
				_topLeft = box.GetProperty("TopLeft"); _bottomRight = box.GetProperty("BottomRight");
				_ok = _boxes != null && _should != null && _contains != null && _topLeft != null && _bottomRight != null;
			}
			catch (Exception) { _ok = false; }
		}

		/// <summary>The live box list once per game tick (null when there is none), so a whole army costs one reflection read.</summary>
		private static IList Boxes()
		{
			if (_tickStamp == Main.GameUpdateCount) return _list != null && _list.Count > 0 ? _list : null;
			_tickStamp = Main.GameUpdateCount;
			try { _list = _boxes.GetValue(null) as IList; }
			catch (Exception) { _list = null; }
			return _list != null && _list.Count > 0 ? _list : null;
		}

		/// <summary>True while a Calamity arena box is up.</summary>
		public static bool Active { get { if (!On) return false; Init(); return _ok && Boxes() != null; } }

		/// <summary>Every active box's walls (top-left, bottom-right) into <paramref name="into"/>.</summary>
		public static void AllBounds(System.Collections.Generic.List<(Vector2 tl, Vector2 br)> into)
		{
			into.Clear();
			if (!On) return;
			Init();
			if (!_ok) return;
			IList boxes = Boxes();
			if (boxes == null) return;
			try { for (int i = 0; i < boxes.Count; i++) if (boxes[i] != null) into.Add(((Vector2)_topLeft.GetValue(boxes[i]), (Vector2)_bottomRight.GetValue(boxes[i]))); }
			catch (Exception) { into.Clear(); }
		}

		/// <summary>The walls of the active box that holds <paramref name="npc"/> (the first one that would hold a player standing there), so a goal can be chosen inside them. False with no box up.</summary>
		public static bool TryBounds(NPC npc, out Vector2 topLeft, out Vector2 bottomRight)
		{
			topLeft = bottomRight = default;
			if (!On || npc == null) return false;
			Init();
			if (!_ok) return false;
			IList boxes = Boxes();
			if (boxes == null) return false;
			try
			{
				_stand ??= new Player { active = true };
				for (int i = 0; i < boxes.Count; i++)
				{
					object box = boxes[i];
					if (box == null) continue;
					_stand.width = npc.width; _stand.height = npc.height; _stand.position = npc.position;
					if (!(bool)_should.Invoke(box, new object[] { _stand })) continue;
					topLeft = (Vector2)_topLeft.GetValue(box); bottomRight = (Vector2)_bottomRight.GetValue(box);
					return true;
				}
			}
			catch (Exception) { }
			return false;
		}

		/// <summary>
		/// Holds <paramref name="npc"/> inside every active box that would hold a player standing there (ArenaWallPlayer.ContainPlayerLogic). Call after the body's movement for the tick is decided, before it is applied.
		/// True when the body rests on the box's bottom wall (its feet at the wall and no upward motion): a wall that stops a fall holds a body up as a floor does, and the caller treats it as standing.
		/// </summary>
		public static bool Contain(NPC npc)
		{
			bool floor = false;
			if (!On || npc == null) return false;
			Init();
			if (!_ok) return false;
			IList boxes = Boxes();
			if (boxes == null) return false;
			try
			{
				_stand ??= new Player { active = true };
				for (int i = 0; i < boxes.Count; i++)
				{
					object box = boxes[i];
					if (box == null) continue;
					_stand.width = npc.width; _stand.height = npc.height; _stand.position = npc.position; // the box reads a player's TopLeft and Size
					if (!(bool)_should.Invoke(box, new object[] { _stand })) continue;
					Checked++;
					if (!(bool)_contains.Invoke(box, new object[] { npc.position, npc.Size })) continue;
					Vector2 tl = (Vector2)_topLeft.GetValue(box), br = (Vector2)_bottomRight.GetValue(box);
					Vector2 dbgP = npc.position, dbgV = npc.velocity;
					if (Snap(npc, tl, br)) { Held++; npc.netUpdate = true; if (Dbg && Math.Abs(dbgP.Y - npc.position.Y) > 0.01f && _dbgN++ < 60) Levy.Instance?.Logger.Info($"ARENABOX-DBG t={Main.GameUpdateCount} who={npc.whoAmI} pos {dbgP.Y - tl.Y:0.0}->{npc.position.Y - tl.Y:0.0} vel {dbgV.X:0.00},{dbgV.Y:0.00} -> {npc.velocity.X:0.00},{npc.velocity.Y:0.00} wallBottom={br.Y - tl.Y:0.0} h={npc.height}"); }
					if (npc.velocity.Y >= 0f && br.Y - npc.BottomRight.Y - npc.velocity.Y <= FloorSlackPx) floor = true;
				}
			}
			catch (Exception e) { if (!_warned) { _warned = true; Levy.Instance?.Logger.Warn("Levy: Calamity arena box hold failed (" + e.Message + ")"); } }
			return floor;
		}

		/// <summary>How close (px) a falling body's feet must be to a box's bottom wall, once the wall has held it, to count as resting on it.</summary>
		private const float FloorSlackPx = 0.75f;

		/// <summary>The snap and velocity trim of ArenaWallPlayer.ContainPlayerLogic, on an NPC. True when the body was outside the walls and was moved or slowed.</summary>
		public static bool Snap(NPC npc, Vector2 tl, Vector2 br)
		{
			bool moved = false;
			if (npc.Left.X < tl.X) { npc.position.X = tl.X; moved = true; }
			if (npc.Right.X > br.X) { npc.position.X = br.X - npc.width; moved = true; }
			if (npc.TopLeft.Y < tl.Y) { npc.position.Y = tl.Y; moved = true; }
			if (npc.BottomRight.Y > br.Y) { npc.position.Y = br.Y - npc.height; moved = true; }
			Vector2 ov = npc.velocity, otl = npc.TopLeft, obr = npc.BottomRight;
			npc.position += ov;
			if (npc.Left.X < tl.X) { npc.velocity.X = tl.X - otl.X; moved = true; }
			if (npc.Right.X > br.X) { npc.velocity.X = br.X - obr.X; moved = true; }
			if (npc.TopLeft.Y < tl.Y) { npc.velocity.Y = tl.Y - otl.Y; if (npc.velocity.Y == 0f) npc.velocity.Y += 0.001f; moved = true; }
			if (npc.BottomRight.Y > br.Y) { npc.velocity.Y = br.Y - obr.Y; moved = true; }
			npc.position -= ov;
			return moved;
		}
	}
}
