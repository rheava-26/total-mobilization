using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.Officers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.UI
{
	/// <summary>Disc, Diamond and Ring carry a dark outline. The Flat shapes have none: they sit inside a bigger outlined shape, where a dark line between two colours would only muddy the rim.</summary>
	internal enum MarkerShape : byte { Disc, Diamond, Ring, DiscFlat, DiamondFlat }

	/// <summary>
	/// The maths behind the map markers, with no game state in it: projecting a tile onto the map the way vanilla does, grouping soldiers into army
	/// markers, and baking the marker pixels. Kept free of Terraria types so it can be exercised outside the game.
	/// </summary>
	internal static class MapMarkerGeometry
	{
		/// <summary>Reach of a diamond's dark outline, measured along its axes. A 1 px line on a 45 degree edge spans sqrt(2) = 1.41 of these units; the extra keeps it from looking faint.</summary>
		private const float DiamondEdge = 1.84f;

		/// <summary>Tile to map pixel, exactly as <see cref="MapOverlayDrawContext"/> does it for vanilla's own icons.</summary>
		public static Vector2 Project(Vector2 tile, Vector2 mapPosition, Vector2 mapOffset, float mapScale)
			=> (tile - mapPosition) * mapScale + mapOffset;

		/// <summary>Map pixel back to the tile under it: the inverse of <see cref="Project"/>.</summary>
		public static Vector2 Unproject(Vector2 screen, Vector2 mapPosition, Vector2 mapOffset, float mapScale)
			=> (screen - mapOffset) / mapScale + mapPosition;

		/// <summary>The mean of a group's summed positions (true component division: Vector2's own operator multiplies by a reciprocal, which is not exact).</summary>
		public static Vector2 GroupCentre(Vector2 sum, int count) => new Vector2(sum.X / count, sum.Y / count);

		/// <summary>
		/// Slides a marker out from under the heads drawn on the map. Vanilla paints player heads over every layer, and the army follows its commander, so a
		/// group marker would otherwise sit hidden beneath its own player. A marker closer than <paramref name="need"/> pixels to a head moves straight away
		/// from it (along the way it already lay, down-right when it is dead centre) until it just clears; the badge-on-an-avatar look.
		/// </summary>
		public static Vector2 ClearOfHeads(Vector2 p, float need, Vector2[] heads, int count)
		{
			for (int i = 0; i < count; i++)
			{
				Vector2 d = p - heads[i];
				float len = d.Length();
				if (len >= need) continue;
				Vector2 dir = len > 1.5f ? new Vector2(d.X / len, d.Y / len) : new Vector2(0.70711f, 0.70711f);
				p = heads[i] + dir * need;
			}
			return p;
		}

		/// <summary>
		/// Greedy grouping in tile space. Each point joins the nearest group whose centre is within <paramref name="radius"/>, else it opens a new group;
		/// once <paramref name="maxGroups"/> exist, a point outside every radius still joins the nearest, so the group count never exceeds the cap.
		/// <paramref name="sums"/> receives each group's summed position (divide by its count for the centre), <paramref name="first"/> its first member.
		/// All output arrays must hold at least <paramref name="maxGroups"/> entries and <paramref name="assign"/> at least <paramref name="n"/>.
		/// </summary>
		/// <returns>The number of groups.</returns>
		public static int Cluster(Vector2[] tiles, int n, float radius, int maxGroups, int[] assign, Vector2[] sums, int[] counts, int[] first)
		{
			if (n <= 0 || maxGroups <= 0) return 0;
			float r2 = radius * radius;
			int k = 0;
			for (int i = 0; i < n; i++)
			{
				int best = -1;
				float bestD = float.MaxValue;
				for (int c = 0; c < k; c++)
				{
					float d = Vector2.DistanceSquared(tiles[i], GroupCentre(sums[c], counts[c]));
					if (d < bestD) { bestD = d; best = c; }
				}
				if (k < maxGroups && (best < 0 || bestD > r2))
				{
					best = k++;
					sums[best] = Vector2.Zero; counts[best] = 0; first[best] = i;
				}
				else if (best < 0) best = 0; // only with unusable (NaN) positions and the cap already reached
				sums[best] += tiles[i];
				counts[best]++;
				assign[i] = best;
			}
			return k;
		}

		/// <summary>Side of the ring texture whose clear middle is <paramref name="clearRadius"/> pixels, rounded up to an even size.</summary>
		public static int RingDiameter(float clearRadius, int thickness)
		{
			int d = (int)MathF.Ceiling(2f * (clearRadius + thickness + 2f)); // two 1 px outlines around the coloured band
			if ((d & 1) != 0) d++;
			return Math.Clamp(d, 8, LevyConstants.MapMarkers.MaxRingPx);
		}

		/// <summary>
		/// Pixels of one marker, premultiplied (what SpriteBatch's AlphaBlend expects): a white body that takes whatever tint it is drawn with,
		/// inside a dark 1 px outline, anti-aliased by supersampling. A ring keeps its middle clear and is outlined on both edges.
		/// </summary>
		public static Color[] BakePixels(MarkerShape shape, int size, int thickness)
		{
			var pixels = new Color[size * size];
			bool flat = shape == MarkerShape.DiscFlat || shape == MarkerShape.DiamondFlat;
			bool diamond = shape == MarkerShape.Diamond || shape == MarkerShape.DiamondFlat;
			int ss = size > 32 ? 3 : 6;                  // samples per pixel side
			int unit = 2 * ss;                            // sample positions are whole numbers of 1/unit pixel, so mirrored pixels see exactly mirrored samples
			int centre = size * ss;                       // the middle of the texture, in the same units
			float rOuter = centre;                        // outer edge of the dark outline
			float rFill = flat ? rOuter : rOuter - (diamond ? DiamondEdge : 1f) * unit;           // where the white body ends and the dark outline begins
			float rInner = shape == MarkerShape.Ring ? Math.Max(0f, rFill - Math.Max(1, thickness) * unit) : 0f;
			float rInnerEdge = Math.Max(0f, rInner - unit);
			float outlineOpacity = LevyConstants.MapMarkers.OutlineOpacity;
			float total = ss * ss;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					int fill = 0, rim = 0;
					for (int sy = 0; sy < ss; sy++)
					{
						for (int sx = 0; sx < ss; sx++)
						{
							int dx = 2 * ss * x + 2 * sx + 1 - centre;
							int dy = 2 * ss * y + 2 * sy + 1 - centre;
							if (diamond)
							{
								int d = Math.Abs(dx) + Math.Abs(dy);
								if (d <= rFill) fill++;
								else if (d <= rOuter) rim++;
								continue;
							}
							float d2 = dx * dx + dy * dy;
							if (shape == MarkerShape.Ring)
							{
								if (d2 <= rFill * rFill && d2 >= rInner * rInner) fill++;
								else if ((d2 > rFill * rFill && d2 <= rOuter * rOuter) || (d2 < rInner * rInner && d2 >= rInnerEdge * rInnerEdge)) rim++;
							}
							else if (d2 <= rFill * rFill) fill++;
							else if (d2 <= rOuter * rOuter) rim++;
						}
					}
					float af = fill / total;
					float ar = rim / total * outlineOpacity;
					pixels[y * size + x] = new Color(af, af, af, af + ar);
				}
			}
			return pixels;
		}
	}

	/// <summary>
	/// Army, officer and boss markers on all three maps: the corner minimap, the overlay map and the fullscreen map (one ModMapLayer is drawn by vanilla on each).
	/// Soldiers and summoned units are cyan/blue dots; on the minimap and overlay map they gather into one marker per group with a head-count, on the fullscreen map
	/// each one is drawn. Vanilla paints its own head icons for town NPCs (an officer is one) and for most bosses on top of every layer, so an officer in the field is
	/// a gold ring around that icon and a boss a pulsing red ring around its icon; a boss with no icon of its own (the Moon Lord's core) gets a red disc as well.
	/// Always one marker per boss. Client draw only; it reads state the game already syncs.
	/// </summary>
	public class MapMarkersLayer : ModMapLayer
	{
		private enum Mode : byte { Minimap, Overlay, Fullscreen }
		private const byte KRegular = 0, KMilitia = 1, KSummon = 2;

		/// <summary>Where a map pixel is, what we may draw on it, and how big a UI pixel is on this map.</summary>
		private readonly struct Frame
		{
			public readonly Mode Mode;
			public readonly float Ui;
			public readonly Rectangle Clip;
			public Frame(Mode mode, float ui, Rectangle clip) { Mode = mode; Ui = ui; Clip = clip; }
			/// <summary>True when a marker of the given half-size centred on <paramref name="p"/> fits inside the drawable area.</summary>
			public bool Fits(Vector2 p, float halfSize)
				=> p.X >= Clip.Left + halfSize + 1f && p.X <= Clip.Right - halfSize - 1f && p.Y >= Clip.Top + halfSize + 1f && p.Y <= Clip.Bottom - halfSize - 1f;
		}

		private struct BossMark { public Vector2 Tile; public int HeadIndex; public NPC Npc; }
		private struct OfficerMark { public Vector2 Tile; public int HeadIndex; public string Name; }

		private static readonly SpriteFrame Whole = new SpriteFrame(1, 1, 0, 0);
		private static readonly Dictionary<int, Texture2D> Cache = new();
		private static readonly string[] CountLabels = BuildCountLabels();
		private static bool _warned;

		// per-frame scratch, draw thread only
		private static Vector2[] _tile = new Vector2[128];
		private static byte[] _kind = new byte[128];
		private static object[] _src = new object[128];
		private static int[] _assign = new int[128];
		private static int _n;
		private static readonly Vector2[] GroupSum = new Vector2[LevyConstants.MapMarkers.MaxArmyMarkers];
		private static readonly int[] GroupCount = new int[LevyConstants.MapMarkers.MaxArmyMarkers];
		private static readonly int[] GroupFirst = new int[LevyConstants.MapMarkers.MaxArmyMarkers];
		private static BossMark[] _boss = new BossMark[16];
		private static int _bossN;
		private static OfficerMark[] _officer = new OfficerMark[16];
		private static int _officerN;
		private static readonly Vector2[] Heads = new Vector2[32]; // where vanilla paints player heads this frame (map pixels)
		private static int _headN;

		/// <summary>Before the pings layer, so a map ping always draws over the markers.</summary>
		public override Position GetDefaultPosition() => new Before(IMapLayer.Pings);

		public override void Draw(ref MapOverlayDrawContext context, ref string text)
		{
			if (Main.dedServ || Main.spriteBatch == null || context.DrawScale <= 0.01f) return;
			Mode mode = Main.mapFullscreen ? Mode.Fullscreen : Main.mapStyle == 1 ? Mode.Minimap : Mode.Overlay;
			// The minimap batch already carries the UI scale in its matrix; the overlay and fullscreen maps draw in raw screen pixels (vanilla folds UIScale into their icon scale).
			float ui = mode == Mode.Minimap ? 1f : Math.Max(0.5f, Main.UIScale);
			Rectangle clip = mode == Mode.Minimap && context.ClippingRectangle.HasValue
				? context.ClippingRectangle.Value
				: new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
			var frame = new Frame(mode, ui, clip);

			// Each kind of marker is guarded on its own: a broken one must never take the map, the frame or the other markers down with it.
			// Officers go first so an army marker (and its head-count) is never painted over by an officer's ring; bosses go last, on top of everything.
			try { GatherOfficers(); DrawOfficers(ref context, in frame, ref text); }
			catch (Exception e) { Warn("officers", e); }
			try { GatherArmy(); GatherHeads(ref context, in frame); DrawArmy(ref context, in frame, ref text); }
			catch (Exception e) { Warn("army", e); }
			try { GatherBosses(); DrawBosses(ref context, in frame, ref text); }
			catch (Exception e) { Warn("bosses", e); }
		}

		private void Warn(string what, Exception e)
		{
			if (_warned) return;
			_warned = true;
			Mod.Logger.Warn("Levy: map markers (" + what + ") failed to draw; logged once: " + e);
		}

		public override void Unload() => FlushCache();

		// =====================================================================================================
		// Army: soldiers and summoned units
		// =====================================================================================================

		private static void GatherArmy()
		{
			_n = 0;
			var roster = RosterSystem.Instance;
			if (roster != null)
			{
				foreach (SoldierNPC s in roster.ActiveBodies())
					if (s != null && s.IsAlive) AddMember(s.NPC.Center, s.IsMilitia ? KMilitia : KRegular, s);
			}
			var summons = SummonedUnitSystem.Instance?.ActiveBodies;
			if (summons != null)
			{
				for (int i = 0; i < summons.Count; i++)
				{
					var body = summons[i];
					NPC npc = body?.NPC;
					if (npc != null && npc.active && body.IsAlive) AddMember(npc.Center, KSummon, npc);
				}
			}
		}

		private static void AddMember(Vector2 world, byte kind, object source)
		{
			if (_n == _tile.Length)
			{
				int cap = _n * 2;
				Array.Resize(ref _tile, cap); Array.Resize(ref _kind, cap); Array.Resize(ref _src, cap); Array.Resize(ref _assign, cap);
			}
			_tile[_n] = world / LevyConstants.TilePx;
			_kind[_n] = kind;
			_src[_n] = source;
			_n++;
		}

		private static void DrawArmy(ref MapOverlayDrawContext c, in Frame f, ref string text)
		{
			if (_n == 0) return;
			if (f.Mode == Mode.Fullscreen && _n <= LevyConstants.MapMarkers.FullscreenIndividualMax)
			{
				for (int i = 0; i < _n; i++) DrawSoldierPip(ref c, in f, i, ref text);
				return;
			}
			// Minimap and overlay map: gather the army into markers. The reach is a number of tiles (so an army reads the same at any zoom), never tighter than a few pixels.
			float mapScale = Math.Max(0.01f, c.MapScale);
			float radius = f.Mode == Mode.Fullscreen
				? LevyConstants.MapMarkers.FullscreenMergePx * f.Ui / mapScale
				: Math.Max(LevyConstants.MapMarkers.ArmyMergeTiles, LevyConstants.MapMarkers.ArmyMergeMinPx * f.Ui / mapScale);
			int groups = MapMarkerGeometry.Cluster(_tile, _n, radius, LevyConstants.MapMarkers.MaxArmyMarkers, _assign, GroupSum, GroupCount, GroupFirst);
			for (int g = 0; g < groups; g++)
			{
				int count = GroupCount[g];
				if (count == 1) { DrawSoldierPip(ref c, in f, GroupFirst[g], ref text); continue; }

				Vector2 tile = MapMarkerGeometry.GroupCentre(GroupSum[g], count);
				int basePx = count >= LevyConstants.MapMarkers.ArmyLargeCount ? LevyConstants.MapMarkers.ArmyPipLarge
					: count >= LevyConstants.MapMarkers.ArmyMediumCount ? LevyConstants.MapMarkers.ArmyPipMedium
					: LevyConstants.MapMarkers.ArmyPipSmall;
				int size = Px(basePx, f.Ui);
				Vector2 p = Project(ref c, tile);
				// The army follows its commander, so its centre is usually right under a player's head icon, which vanilla paints over this layer. Slide the marker clear of it.
				float need = LevyConstants.MapMarkers.PlayerHeadHalfPx * c.DrawScale + size * 0.5f + LevyConstants.MapMarkers.HeadClearGap * f.Ui;
				Vector2 cleared = MapMarkerGeometry.ClearOfHeads(p, need, Heads, _headN);
				if (cleared != p)
				{
					p = cleared;
					tile = MapMarkerGeometry.Unproject(p, c.MapPosition, c.MapOffset, mapScale);
				}
				if (!f.Fits(p, Math.Max(size * 0.5f, LevyConstants.MapMarkers.LabelHalfHeight * f.Ui))) continue; // the head-count is taller than a small marker
				if (Stamp(ref c, GetTexture(MarkerShape.Disc, size, 0), tile, LevyConstants.MapMarkers.ArmyAzure))
					text = Language.GetTextValue("Mods.Levy.MapMarkers.Army", count);
				DrawLabel(CountLabel(count), p, size, in f);
			}
		}

		/// <summary>
		/// Where vanilla paints each player's head icon on this map, so army markers can keep clear of them. Vanilla does not centre the head on the player: it nudges it
		/// up and left (DrawMap, "num66 -= 6f" and its siblings), by a different amount on each map, and on the overlay map it measures from the top of the player.
		/// </summary>
		private static void GatherHeads(ref MapOverlayDrawContext c, in Frame f)
		{
			_headN = 0;
			float s = c.MapScale;
			for (int i = 0; i < Main.maxPlayers && _headN < Heads.Length; i++)
			{
				Player player = Main.player[i];
				if (player == null || !player.active || player.dead || player.ghost) continue;
				Vector2 shift = f.Mode switch
				{
					Mode.Minimap => new Vector2(LevyConstants.MapMarkers.PlayerHeadShiftX, LevyConstants.MapMarkers.PlayerHeadShiftYMinimap + 0.4f * s),
					Mode.Fullscreen => new Vector2(LevyConstants.MapMarkers.PlayerHeadShiftX, LevyConstants.MapMarkers.PlayerHeadShiftYMap + 0.4f * s),
					_ => new Vector2(LevyConstants.MapMarkers.PlayerHeadShiftX, LevyConstants.MapMarkers.PlayerHeadShiftYMap + 0.4f * s - player.height * 0.5f / LevyConstants.TilePx * s),
				};
				Heads[_headN++] = Project(ref c, player.Center / LevyConstants.TilePx) + shift;
			}
		}

		private static void DrawSoldierPip(ref MapOverlayDrawContext c, in Frame f, int i, ref string text)
		{
			int size = Px(LevyConstants.MapMarkers.SoldierPip, f.Ui);
			if (!f.Fits(Project(ref c, _tile[i]), size * 0.5f)) return;
			Color color = _kind[i] == KMilitia ? LevyConstants.MapMarkers.MilitiaBlue
				: _kind[i] == KSummon ? LevyConstants.MapMarkers.SummonAqua
				: LevyConstants.MapMarkers.RegularCyan;
			if (Stamp(ref c, GetTexture(MarkerShape.Disc, size, 0), _tile[i], color)) text = NameOf(i);
		}

		private static string NameOf(int i) => _src[i] is SoldierNPC s ? s.SoldierName : _src[i] is NPC n ? n.GivenOrTypeName : "";

		/// <summary>The head-count beside an army marker, on the side that stays inside the map.</summary>
		private static void DrawLabel(string label, Vector2 p, int pipSize, in Frame f)
		{
			var font = FontAssets.MouseText?.Value;
			if (font == null) return;
			float scale = LevyConstants.MapMarkers.LabelScale * f.Ui;
			float width = font.MeasureString(label).X * scale;
			float gap = 2f * f.Ui;
			bool right = p.X + pipSize * 0.5f + gap + width <= f.Clip.Right - 1f;
			var pos = new Vector2(right ? p.X + pipSize * 0.5f + gap : p.X - pipSize * 0.5f - gap, p.Y);
			Utils.DrawBorderString(Main.spriteBatch, label, pos, LevyConstants.MapMarkers.LabelColor, scale, right ? 0f : 1f, 0.5f);
		}

		// =====================================================================================================
		// Officers: the avatar in the field, with a ring around the parked town NPC's vanilla head icon
		// =====================================================================================================

		/// <summary>Officers with a live avatar in the field (the others are parked at home, where vanilla already shows their town NPC).</summary>
		private static void GatherOfficers()
		{
			_officerN = 0;
			var officers = OfficerSystem.Instance?.ActiveBodies;
			if (officers == null) return;
			for (int i = 0; i < officers.Count; i++)
			{
				OfficerBody body = officers[i];
				NPC npc = body?.NPC;
				if (npc == null || !npc.active || !body.IsAlive) continue;
				// The town NPC is pinned to the avatar, and vanilla draws its head icon (only when its head index is above 0) over this layer.
				NPC town = body.TownNpc;
				int head = town != null && town.active ? TownNPCProfiles.GetHeadIndexSafe(town) : -1;
				if (_officerN == _officer.Length) Array.Resize(ref _officer, _officer.Length * 2);
				_officer[_officerN++] = new OfficerMark { Tile = npc.Center / LevyConstants.TilePx, HeadIndex = head, Name = town != null ? town.FullName : npc.GivenOrTypeName };
			}
		}

		private static void DrawOfficers(ref MapOverlayDrawContext c, in Frame f, ref string text)
		{
			for (int i = 0; i < _officerN; i++)
			{
				OfficerMark o = _officer[i];
				float iconPx = o.HeadIndex > 0 ? HeadPx(TextureAssets.NpcHead, o.HeadIndex, c.DrawScale) : 0f;
				if (iconPx > 0f)
				{
					// Vanilla paints this town NPC's head over this layer (with its name on hover): the ring around it is the marker.
					float clear = IconClear(ref c, in f, iconPx);
					if (f.Fits(Project(ref c, o.Tile), RingDiameter(in f, clear, out _) * 0.5f)) DrawRing(ref c, in f, o.Tile, clear, LevyConstants.MapMarkers.OfficerGold, 1f);
					continue;
				}
				// No head icon to ring (not expected of a town NPC): a gold diamond with a white rim stands in.
				int outer = Px(LevyConstants.MapMarkers.OfficerPipOuter, f.Ui), inner = Px(LevyConstants.MapMarkers.OfficerPipInner, f.Ui);
				if (!f.Fits(Project(ref c, o.Tile), outer * 0.5f)) continue;
				Stamp(ref c, GetTexture(MarkerShape.Diamond, outer, 0), o.Tile, LevyConstants.MapMarkers.OfficerWhite, hoverGrow: false);
				if (Stamp(ref c, GetTexture(MarkerShape.DiamondFlat, inner, 0), o.Tile, LevyConstants.MapMarkers.OfficerGold)) text = o.Name;
			}
		}

		// =====================================================================================================
		// Bosses: one marker per boss, ring pulsing around vanilla's icon
		// =====================================================================================================

		/// <summary>
		/// Collects the bosses to mark. A boss is an NPC with <c>boss</c> set, one vanilla sorts with bosses without setting it (Lunar Pillars, the Eater of Worlds
		/// head, the Torch God), or a hostile one vanilla currently gives a boss head icon (event bosses). The parts of a boss are left to the boss: the Moon Lord's
		/// head and hands (all flagged <c>boss</c>) and the Golem's head and fists draw nothing while their core is alive, and any segment that shares another NPC's
		/// life (realLife) is skipped. The Eater of Worlds is many heads: they become one marker at their centre (ringing its icon while there is a single head). The Destroyer's marker sits where vanilla puts its icon.
		/// </summary>
		private static void GatherBosses()
		{
			_bossN = 0;
			var counted = NPCID.Sets.ShouldBeCountedAsBoss;
			var headSet = NPCID.Sets.BossHeadTextures;
			bool core = false, golem = false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.life <= 0) continue;
				if (n.type == NPCID.MoonLordCore) core = true;
				else if (n.type == NPCID.Golem) golem = true;
			}

			Vector2 wormSum = default;
			int wormHeads = 0;
			NPC wormFirst = null;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.life <= 0 || n.friendly || n.townNPC) continue;
				int t = n.type;
				bool isBoss = n.boss || (t >= 0 && t < counted.Length && counted[t]);
				int head = -1;
				if (isBoss || (t >= 0 && t < headSet.Length && headSet[t] >= 0)) head = HeadIndexOf(n); // what vanilla paints for it right now; -1 = nothing
				if (!isBoss && head < 0) continue;
				if (t == NPCID.CultistBoss && head < 0) continue; // the Lunatic Cultist hides which copy is the real one; so does the map
				if (IsBossPart(n, core, golem)) continue;
				if (t == NPCID.EaterofWorldsHead) { wormSum += n.Center; wormHeads++; wormFirst ??= n; continue; }
				AddBoss(n, BossCenter(n), head);
			}
			// One head: its own vanilla icon sits right here, so the ring goes around it. Several: vanilla paints an icon on each head, none at their centre, so the
			// merged marker stands alone (red disc and ring) rather than ringing empty ground.
			if (wormHeads > 0) AddBoss(wormFirst, wormSum / wormHeads, wormHeads == 1 ? HeadIndexOf(wormFirst) : -1);
		}

		/// <summary>The boss head icon vanilla paints for this NPC right now (-1 = none). Another mod's hook runs inside, so a failure there costs this one NPC its icon, not every boss its marker.</summary>
		private static int HeadIndexOf(NPC n)
		{
			try { return n.GetBossHeadTextureIndex(); }
			catch { return -1; }
		}

		private static bool IsBossPart(NPC n, bool moonLordCore, bool golem)
		{
			int life = n.realLife;
			if (life >= 0 && life < Main.maxNPCs && life != n.whoAmI && Main.npc[life].active) return true; // a segment of another NPC (worm bodies, Wall of Flesh eyes, modded segmented bosses)
			switch (n.type)
			{
				case NPCID.MoonLordHead:
				case NPCID.MoonLordHand:
					return moonLordCore;
				case NPCID.GolemHead:
				case NPCID.GolemHeadFree:
				case NPCID.GolemFistLeft:
				case NPCID.GolemFistRight:
					return golem;
				default:
					return false;
			}
		}

		/// <summary>The boss's centre; the Destroyer's is the mean of its first 15 segments, which is where vanilla paints its head icon.</summary>
		private static Vector2 BossCenter(NPC n)
		{
			if (n.type != NPCID.TheDestroyer) return n.Center + new Vector2(0f, n.gfxOffY);
			Vector2 sum = n.Center;
			int count = 1, next = (int)n.ai[0];
			while (count < 15 && next >= 0 && next < Main.maxNPCs)
			{
				NPC seg = Main.npc[next];
				if (!seg.active || seg.type < NPCID.TheDestroyer || seg.type > NPCID.TheDestroyerTail) break;
				count++; sum += seg.Center; next = (int)seg.ai[0];
			}
			return sum / count;
		}

		private static void AddBoss(NPC npc, Vector2 world, int headIndex)
		{
			if (_bossN == _boss.Length) Array.Resize(ref _boss, _boss.Length * 2);
			_boss[_bossN++] = new BossMark { Tile = world / LevyConstants.TilePx, HeadIndex = headIndex, Npc = npc };
		}

		private static void DrawBosses(ref MapOverlayDrawContext c, in Frame f, ref string text)
		{
			float pulse = LevyConstants.MapMarkers.PulseMin
				+ (1f - LevyConstants.MapMarkers.PulseMin) * (0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * LevyConstants.MapMarkers.PulseSpeed));
			for (int i = 0; i < _bossN; i++)
			{
				BossMark b = _boss[i];
				int outer = Px(LevyConstants.MapMarkers.BossPipOuter, f.Ui), inner = Px(LevyConstants.MapMarkers.BossPipInner, f.Ui);
				// With a vanilla icon (painted over this layer, name on hover) the pulsing ring around it is the marker. Without one (the Moon Lord's core, the Golem's
				// body, the Torch God) a red disc with a white rim and a ring hugging it stands in.
				float iconPx = b.HeadIndex >= 0 ? HeadPx(TextureAssets.NpcHeadBoss, b.HeadIndex, c.DrawScale) : 0f;
				bool hasIcon = iconPx > 0f;
				float clear = hasIcon ? IconClear(ref c, in f, iconPx) : outer * 0.5f + LevyConstants.MapMarkers.RingGap * f.Ui;
				if (!f.Fits(Project(ref c, b.Tile), Math.Max(hasIcon ? 0 : outer, RingDiameter(in f, clear, out _)) * 0.5f)) continue;

				DrawRing(ref c, in f, b.Tile, clear, LevyConstants.MapMarkers.BossRing, pulse);
				if (hasIcon) continue;
				Stamp(ref c, GetTexture(MarkerShape.Disc, outer, 0), b.Tile, LevyConstants.MapMarkers.BossWhite, hoverGrow: false);
				if (Stamp(ref c, GetTexture(MarkerShape.DiscFlat, inner, 0), b.Tile, LevyConstants.MapMarkers.BossRed)) text = b.Npc.GivenOrTypeName;
			}
		}

		// =====================================================================================================
		// Drawing helpers
		// =====================================================================================================

		/// <summary>Map pixel of a tile, by the same rule vanilla's own icons use.</summary>
		private static Vector2 Project(ref MapOverlayDrawContext c, Vector2 tile)
			=> MapMarkerGeometry.Project(tile, c.MapPosition, c.MapOffset, c.MapScale);

		/// <summary>Draws a marker texture centred on a tile through vanilla's own path (projection, minimap clip, hover test). Returns true while the mouse is over it.</summary>
		private static bool Stamp(ref MapOverlayDrawContext c, Texture2D texture, Vector2 tile, Color color, bool hoverGrow = true)
		{
			if (texture == null) return false;
			float s = 1f / c.DrawScale; // cancels the context's icon scale, so the texture lands 1:1 in UI pixels
			return c.Draw(texture, tile, color, Whole, s, hoverGrow ? s * LevyConstants.MapMarkers.HoverGrow : s, Alignment.Center).IsMouseOver;
		}

		/// <summary>Radius (px) of the clear middle of a ring that sits just outside a vanilla head icon of the given drawn size, past the icon's own outline.</summary>
		private static float IconClear(ref MapOverlayDrawContext c, in Frame f, float iconPx)
			=> iconPx * 0.5f + LevyConstants.MapMarkers.VanillaIconOutline * c.DrawScale + LevyConstants.MapMarkers.RingGap * f.Ui;

		/// <summary>Side (px) of the ring texture for a clear middle of the given radius, and the band thickness it uses.</summary>
		private static int RingDiameter(in Frame f, float clearRadius, out int thickness)
		{
			thickness = Math.Max(2, (int)MathF.Round(LevyConstants.MapMarkers.RingThickness * f.Ui));
			return MapMarkerGeometry.RingDiameter(clearRadius, thickness);
		}

		/// <summary>A ring centred on a tile, its clear middle <paramref name="clearRadius"/> px across from the centre.</summary>
		private static void DrawRing(ref MapOverlayDrawContext c, in Frame f, Vector2 tile, float clearRadius, Color color, float alpha)
		{
			int diameter = RingDiameter(in f, clearRadius, out int thickness);
			Stamp(ref c, GetTexture(MarkerShape.Ring, diameter, thickness), tile, color * alpha, hoverGrow: false);
		}

		/// <summary>Drawn size (px) of a vanilla head icon: its texture times the scale vanilla draws it at. Zero when there is no such icon.</summary>
		private static float HeadPx(Asset<Texture2D>[] sheet, int index, float drawScale)
		{
			if (sheet == null || index < 0 || index >= sheet.Length || sheet[index] == null) return 0f;
			int side = Math.Max(sheet[index].Width(), sheet[index].Height()); // 0 until the texture has loaded
			if (side <= 0) side = LevyConstants.MapMarkers.FallbackIconPx;
			return side * drawScale;
		}

		/// <summary>A marker size for this map: scaled by the UI, never under 4 px, and even so it sits centred on a pixel boundary.</summary>
		private static int Px(int basePx, float ui)
		{
			int p = Math.Max(4, (int)MathF.Round(basePx * ui));
			return (p & 1) == 0 ? p : p + 1;
		}

		private static string CountLabel(int n) => n >= 0 && n < CountLabels.Length ? CountLabels[n] : n.ToString();

		private static string[] BuildCountLabels()
		{
			var labels = new string[100];
			for (int i = 0; i < labels.Length; i++) labels[i] = i.ToString();
			return labels;
		}

		// ---- baked textures: made on first use at exactly the pixel size they are drawn at, so no resampling blurs them ----

		private static Texture2D GetTexture(MarkerShape shape, int size, int thickness)
		{
			int key = ((int)shape << 16) | (size << 6) | Math.Min(thickness, 63);
			if (Cache.TryGetValue(key, out Texture2D texture) && texture != null && !texture.IsDisposed) return texture;
			GraphicsDevice device = Main.graphics?.GraphicsDevice;
			if (device == null) return null;
			// Sizes change as the map zooms; a flush is rare and keeps the cache bounded.
			if (Cache.Count >= LevyConstants.MapMarkers.TextureCacheMax) FlushCache();
			texture = new Texture2D(device, size, size);
			texture.SetData(MapMarkerGeometry.BakePixels(shape, size, thickness));
			Cache[key] = texture;
			return texture;
		}

		/// <summary>Drops every baked texture. Disposal is queued for the main thread: a texture may already be waiting in the current SpriteBatch.</summary>
		private static void FlushCache()
		{
			if (Cache.Count == 0) return;
			var dead = new List<Texture2D>(Cache.Values);
			Cache.Clear();
			if (!Main.dedServ) Main.QueueMainThreadAction(() => { foreach (Texture2D t in dead) t?.Dispose(); });
		}
	}

	/// <summary>
	/// Retires the older one-dot-per-soldier layer (<c>SoldierMapLayer</c>): it paints a gold or blue dot for every soldier on every map, which would sit under the new
	/// army markers as a cloud of dots and clash with the officer pips. Hidden by name each frame through the supported hook, so deleting that file later is safe.
	/// </summary>
	public class MapMarkersSystem : ModSystem
	{
		private const string RetiredLayer = "SoldierMapLayer";

		public override void PreDrawMapIconOverlay(IReadOnlyList<IMapLayer> layers, MapOverlayDrawContext mapOverlayDrawContext)
		{
			for (int i = 0; i < layers.Count; i++)
				if (layers[i] is ModMapLayer layer && layer.Mod == Mod && layer.Name == RetiredLayer) layer.Visible = false;
		}
	}
}
