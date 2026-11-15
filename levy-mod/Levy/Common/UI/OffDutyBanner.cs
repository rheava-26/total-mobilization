using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ModLoader.UI;
using Terraria.UI;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.UI
{
	/// <summary>
	/// OFF-DUTY BANNER. A levy, a militiaman or an officer who is away from home on duty leaves a small banner standing where they left from: over their barracks for a levy or militiaman, at the front door of the house for a
	/// commissioned town NPC. Hover it for "Off on duty." and the names. It is DRAWN ONLY: no tile is placed, the world never changes. It goes when the last of them is back (inside <see cref="LevyConstants.NearHomeTiles"/> of home) or gone.
	/// Cheap by construction: the spots are rebuilt every <see cref="RefreshTicks"/> ticks into fixed arrays (no allocation), a spot off screen is skipped, and the number strings are made once.
	/// </summary>
	public class OffDutyBanner : ModSystem
	{
		public const int MaxSpots = 48, MaxNames = 5, RefreshTicks = 20;
		private const int PoleH = 30, ClothW = 14, ClothH = 11;

		public struct Spot { public int Key; public Vector2 Base; public int Count; public int Shown; public bool Officer; public string Text; }

		private static readonly Spot[] _spots = new Spot[MaxSpots];
		private static readonly string[] _names = new string[MaxSpots * MaxNames];
		private static readonly string[] _num = MakeNumbers();
		private static int _n, _hover = -1;
		private static uint _lastRefresh = uint.MaxValue;

		/// <summary>Spots standing right now (read by the world check).</summary>
		public static int SpotCount => _n;
		public static Spot SpotAt(int i) => _spots[i];
		public static string NameAt(int spot, int k) => _names[spot * MaxNames + k];

		private static string[] MakeNumbers() { var a = new string[100]; for (int i = 0; i < a.Length; i++) a[i] = i.ToString(); return a; }

		/// <summary>True when a body at <paramref name="at"/> is away from the home banner spot (farther than the "near home" radius).</summary>
		public static bool IsAway(Vector2 home, Vector2 at) => Vector2.DistanceSquared(home, at) > LevyConstants.NearHomeTiles * LevyConstants.TilePx * LevyConstants.NearHomeTiles * LevyConstants.TilePx;

		/// <summary>One away body: counted at its home's spot (made when it is the first), its name kept while the spot has room for it.</summary>
		public static void Add(int key, Vector2 homeBase, string name, bool officer)
		{
			int s = -1;
			for (int i = 0; i < _n; i++) if (_spots[i].Key == key) { s = i; break; }
			if (s < 0)
			{
				if (_n >= MaxSpots) return;
				s = _n++;
				_spots[s] = new Spot { Key = key, Base = homeBase, Officer = officer };
			}
			ref Spot sp = ref _spots[s];
			sp.Count++;
			if (sp.Shown < MaxNames) _names[s * MaxNames + sp.Shown++] = name;
		}

		/// <summary>Forget every spot (before a rebuild, and at the end of a check).</summary>
		public static void Clear()
		{
			for (int i = 0; i < _n; i++) { _spots[i].Text = null; for (int k = 0; k < MaxNames; k++) _names[i * MaxNames + k] = null; }
			_n = 0; _hover = -1;
		}

		/// <summary>Walks the bodies once and rebuilds the spots. Server-free: it reads only what a client already has (the soldier records and the town NPCs' own homes).</summary>
		public static void Refresh()
		{
			Clear();
			var settlement = SettlementSystem.Instance;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.ModNPC is not SoldierNPC s || s.Record == null || s.Hostile || !s.IsAlive) continue;
				if (s.OfficerMode)
				{
					NPC town = s.OfficerTownNpc;
					if (town == null || town.homeTileX <= 0 || town.homeTileY <= 0) continue; // a homeless town NPC has nowhere to leave a banner
					Vector2 door = new Vector2(town.homeTileX * 16f + 8f, town.homeTileY * 16f);
					if (IsAway(door, npc.Center)) Add(-1 - town.homeTileX * 20000 - town.homeTileY, door, s.Record.Name, true);
				}
				else
				{
					if (settlement == null || s.Record.HomeBarracksId < 0) continue;
					var b = settlement.GetBarracks(s.Record.HomeBarracksId);
					if (b == null) continue;
					Vector2 roof = new Vector2((b.Position.X + 1.5f) * 16f, b.Position.Y * 16f);
					if (IsAway(b.Center, npc.Center)) Add(b.ID + 1, roof, s.Record.Name, false);
				}
			}
			_lastRefresh = Main.GameUpdateCount;
		}

		/// <summary>The hover text of spot <paramref name="i"/>: "Off on duty." then the names, then "and N more". Built once per refresh, only for a spot the mouse is on.</summary>
		public static string TextOf(int i)
		{
			if (i < 0 || i >= _n) return "";
			ref Spot sp = ref _spots[i];
			if (sp.Text != null) return sp.Text;
			var sb = new System.Text.StringBuilder(LevyUtil.T("UI2.OffDuty"));
			for (int k = 0; k < sp.Shown; k++) sb.Append('\n').Append(_names[i * MaxNames + k]);
			if (sp.Count > sp.Shown) sb.Append('\n').Append(LevyUtil.T("UI2.OffDutyMore", sp.Count - sp.Shown));
			return sp.Text = sb.ToString();
		}

		public override void OnWorldUnload() { Clear(); _lastRefresh = uint.MaxValue; }

		public override void PostUpdateEverything()
		{
			if (Main.dedServ || Main.gameMenu) return;
			if (_lastRefresh == uint.MaxValue || Main.GameUpdateCount - _lastRefresh >= RefreshTicks) Refresh();
			_hover = -1;
			if (_n == 0) return;
			Vector2 m = Main.MouseWorld;
			for (int i = 0; i < _n; i++)
			{
				Vector2 b = _spots[i].Base;
				if (m.X >= b.X - 10f && m.X <= b.X + ClothW + 4f && m.Y >= b.Y - PoleH - 2f && m.Y <= b.Y + 2f) { _hover = i; break; }
			}
		}

		public override void PostDrawTiles()
		{
			if (Main.dedServ || _n == 0) return;
			Rectangle view = new Rectangle((int)Main.screenPosition.X - 48, (int)Main.screenPosition.Y - 64, Main.screenWidth + 96, Main.screenHeight + 128);
			bool any = false;
			for (int i = 0; i < _n; i++) if (view.Contains((int)_spots[i].Base.X, (int)_spots[i].Base.Y)) { any = true; break; }
			if (!any) return; // nothing on screen: no batch is even started
			var sb = Main.spriteBatch;
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
			var px = TextureAssets.MagicPixel.Value;
			float t = Main.GlobalTimeWrappedHourly;
			for (int i = 0; i < _n; i++)
			{
				ref Spot sp = ref _spots[i];
				if (!view.Contains((int)sp.Base.X, (int)sp.Base.Y)) continue;
				int bx = (int)(sp.Base.X - Main.screenPosition.X), by = (int)(sp.Base.Y - Main.screenPosition.Y);
				sb.Draw(px, new Rectangle(bx, by - PoleH, 2, PoleH), new Color(92, 62, 34));                       // pole
				sb.Draw(px, new Rectangle(bx - 1, by - PoleH - 2, 4, 2), new Color(224, 190, 90));                 // finial
				Color cloth = sp.Officer ? new Color(60, 110, 170) : new Color(168, 40, 40), edge = new Color(224, 190, 90);
				for (int c = 0; c < ClothW / 2; c++)                                                               // the cloth, a strip at a time so it ripples
				{
					int wave = (int)Math.Round(Math.Sin(t * 4f + c * 0.9f + i) * 1.2f * (c + 1) / 3f);
					sb.Draw(px, new Rectangle(bx + 2 + c * 2, by - PoleH + wave, 2, ClothH - c), cloth);
					sb.Draw(px, new Rectangle(bx + 2 + c * 2, by - PoleH + wave, 2, 1), edge);
				}
				if (sp.Count > 1) Utils.DrawBorderString(sb, _num[Math.Min(sp.Count, 99)], new Vector2(bx + 3, by - PoleH + ClothH + 1), Color.White, 0.6f);
			}
			sb.End();
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int i = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
			if (i < 0) return;
			layers.Insert(i, new LegacyGameInterfaceLayer("Levy: Off Duty Banner", () =>
			{
				if (_hover >= 0 && _hover < _n && !Main.LocalPlayer.mouseInterface) UICommon.TooltipMouseText(TextOf(_hover));
				return true;
			}, InterfaceScaleType.Game));
		}
	}
}
