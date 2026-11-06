using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.Logistics;
using Levy.Common.Systems;
using Levy.Common.UI;
using Levy.Content.Buffs;
using Levy.Content.TileEntities;

namespace Levy.Common.Potions
{
	/// <summary>
	/// The Seeker potion, client side. Drinking it opens a chooser (<see cref="BeginChoose"/>); while the buff is up the tiles around the player that are, or drop, the chosen item glow like Spelunker, chests holding it are marked,
	/// and an arrow at the player's feet points at the nearest one. Nothing here touches the world; it only reads tiles and chests.
	/// </summary>
	public class SeekerSystem : ModSystem
	{
		public static readonly List<Point> Tiles = new();
		public static readonly List<Point> Chests = new();
		/// <summary>World position of the nearest match, or null.</summary>
		public static Vector2? Nearest;
		private static long _lastScan = -1000;
		private static int _scanItem;

		public static bool Running => !Main.dedServ && Main.LocalPlayer != null && Main.LocalPlayer.active && !Main.LocalPlayer.dead && Main.LocalPlayer.HasBuff<SeekerBuff>() && FindTarget.Target > 0;

		// ------------------------------------------------------------------ choosing

		/// <summary>The armory the Seeker reads: a Magic Storage-networked armory anywhere, else the nearest armory within <see cref="PotionConstants.SeekerArmoryReachTiles"/>. Nearest first.</summary>
		public static ArmoryTE FindArmory(Player p)
		{
			ArmoryTE best = null; float bestD = float.MaxValue;
			float reach = PotionConstants.SeekerArmoryReachTiles * 16f; reach *= reach;
			foreach (var te in TileEntity.ByID.Values)
			{
				if (te is not ArmoryTE a) continue;
				float d = Vector2.DistanceSquared(a.Center, p.Center);
				bool networked = false;
				if (d > reach) { try { networked = NetworkStocks.For(a) != null; } catch (Exception) { } if (!networked) continue; }
				if (d < bestD) { bestD = d; best = a; }
			}
			return best;
		}

		/// <summary>Opens the chooser: through <see cref="FindTarget.PickTarget"/> with the nearest armory's list (Magic Storage network items when linked, else its storage), or the ore/gem/treasure list when none can be reached.</summary>
		public static void BeginChoose(Player p)
		{
			if (Main.dedServ) return;
			ArmoryTE armory = FindArmory(p);
			List<int> cands = armory != null ? FindTarget.Candidates(armory) : null;
			if (armory != null && cands.Count > 0)
			{
				var prev = FindTarget.Chooser;
				bool panelOpen = UISystem.CurrentArmory != null && prev != null; // the logistics screen's chooser is only visible while the armory panel is open
				if (!panelOpen) FindTarget.Chooser = (cb, list) => SeekerChooser.Open(list, cb);
				try { FindTarget.PickTarget(armory, t => { SeekerMatcher.ClearCache(); _lastScan = -1000; }); }
				finally { if (!panelOpen) FindTarget.Chooser = prev; }
				return;
			}
			SeekerChooser.Open(SeekerMatcher.FallbackCandidates(), t => { FindTarget.Set(t); SeekerMatcher.ClearCache(); _lastScan = -1000; });
		}

		// ------------------------------------------------------------------ scan

		public override void PostUpdateEverything()
		{
			if (Main.dedServ) return;
			if (!Running) { if (Tiles.Count > 0 || Chests.Count > 0 || Nearest != null) { Tiles.Clear(); Chests.Clear(); Nearest = null; } return; }
			if (_scanItem != FindTarget.Target) { _scanItem = FindTarget.Target; _lastScan = -1000; }
			if (Main.GameUpdateCount - _lastScan < PotionConstants.SeekerScanTicks) return;
			_lastScan = Main.GameUpdateCount;
			Scan(Main.LocalPlayer.Center, FindTarget.Target, Tiles, Chests, out Nearest);
		}

		/// <summary>Collects the matching tiles around <paramref name="center"/> and the chests holding the item, and the nearest of all of them.</summary>
		public static void Scan(Vector2 center, int item, List<Point> tiles, List<Point> chests, out Vector2? nearest)
		{
			tiles.Clear(); chests.Clear(); nearest = null;
			var m = SeekerMatcher.For(item);
			int cx = (int)(center.X / 16f), cy = (int)(center.Y / 16f);
			int x0 = Math.Max(2, cx - PotionConstants.SeekerScanHalfW), x1 = Math.Min(Main.maxTilesX - 3, cx + PotionConstants.SeekerScanHalfW);
			int y0 = Math.Max(2, cy - PotionConstants.SeekerScanHalfH), y1 = Math.Min(Main.maxTilesY - 3, cy + PotionConstants.SeekerScanHalfH);
			float best = float.MaxValue;
			for (int x = x0; x <= x1; x++)
				for (int y = y0; y <= y1; y++)
				{
					if (!m.Matches(Main.tile[x, y])) continue;
					if (tiles.Count < PotionConstants.SeekerMaxMatches) tiles.Add(new Point(x, y));
					float d = Vector2.DistanceSquared(new Vector2(x * 16f + 8f, y * 16f + 8f), center);
					if (d < best) { best = d; nearest = new Vector2(x * 16f + 8f, y * 16f + 8f); }
				}
			float reach = PotionConstants.SeekerChestTiles * 16f; reach *= reach;
			for (int i = 0; i < Main.maxChests; i++)
			{
				Chest c = Main.chest[i]; if (c == null) continue;
				Vector2 pos = new Vector2(c.x * 16f + 16f, c.y * 16f + 16f);
				float d = Vector2.DistanceSquared(pos, center);
				if (d > reach || !Holds(c, item)) continue;
				chests.Add(new Point(c.x, c.y));
				if (d < best) { best = d; nearest = pos; }
			}
		}

		public static bool Holds(Chest c, int item)
		{
			if (c?.item == null) return false;
			for (int i = 0; i < c.item.Length; i++) { Item it = c.item[i]; if (it != null && !it.IsAir && it.type == item && it.stack > 0) return true; }
			return false;
		}

		// ------------------------------------------------------------------ draw

		public override void PostDrawTiles()
		{
			if (Main.dedServ || (Tiles.Count == 0 && Chests.Count == 0) || !Running) return;
			var sb = Main.spriteBatch;
			sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
			var px = TextureAssets.MagicPixel.Value;
			float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f);
			Color fill = new Color(255, 215, 90) * (0.22f + 0.22f * pulse), edge = new Color(255, 240, 160) * (0.55f + 0.35f * pulse);
			Rectangle view = new Rectangle((int)Main.screenPosition.X - 32, (int)Main.screenPosition.Y - 32, Main.screenWidth + 64, Main.screenHeight + 64);
			foreach (Point t in Tiles) Glow(sb, px, new Rectangle(t.X * 16, t.Y * 16, 16, 16), view, fill, edge);
			Color cfill = new Color(90, 220, 255) * (0.22f + 0.22f * pulse), cedge = new Color(170, 240, 255) * (0.55f + 0.35f * pulse);
			foreach (Point c in Chests) Glow(sb, px, new Rectangle(c.X * 16, c.Y * 16, 32, 32), view, cfill, cedge);
			sb.End();
		}

		private static void Glow(SpriteBatch sb, Texture2D px, Rectangle world, Rectangle view, Color fill, Color edge)
		{
			if (!world.Intersects(view)) return;
			var r = new Rectangle(world.X - (int)Main.screenPosition.X, world.Y - (int)Main.screenPosition.Y, world.Width, world.Height);
			sb.Draw(px, r, fill);
			sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 2), edge); sb.Draw(px, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), edge);
			sb.Draw(px, new Rectangle(r.X, r.Y, 2, r.Height), edge); sb.Draw(px, new Rectangle(r.Right - 2, r.Y, 2, r.Height), edge);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int i = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
			if (i < 0) return;
			layers.Insert(i, new LegacyGameInterfaceLayer("Levy: Seeker Arrow", () => { DrawArrow(Main.spriteBatch); return true; }, InterfaceScaleType.Game));
		}

		private static void DrawArrow(SpriteBatch sb)
		{
			if (!Running || Nearest == null) return;
			Player p = Main.LocalPlayer;
			Vector2 to = Nearest.Value - p.Center;
			float dist = to.Length();
			if (dist < 120f) return; // the glow is on screen next to the player
			Vector2 dir = to / dist;
			Vector2 at = p.Center - Main.screenPosition + dir * 70f;
			var px = TextureAssets.MagicPixel.Value;
			Color col = new Color(255, 225, 110);
			float rot = dir.ToRotation();
			sb.Draw(px, at, new Rectangle(0, 0, 1, 1), col, rot, new Vector2(0f, 0.5f), new Vector2(26f, 3f), SpriteEffects.None, 0f);
			Vector2 tip = at + dir * 26f;
			sb.Draw(px, tip, new Rectangle(0, 0, 1, 1), col, rot + 2.5f, new Vector2(0f, 0.5f), new Vector2(11f, 3f), SpriteEffects.None, 0f);
			sb.Draw(px, tip, new Rectangle(0, 0, 1, 1), col, rot - 2.5f, new Vector2(0f, 0.5f), new Vector2(11f, 3f), SpriteEffects.None, 0f);
			Utils.DrawBorderString(sb, Math.Round(dist / 16f) + "", at + dir * 44f, col, 0.8f, 0.5f, 0.5f);
		}

		public override void Unload() { Tiles.Clear(); Chests.Clear(); SeekerMatcher.Unload(); }
	}
}
