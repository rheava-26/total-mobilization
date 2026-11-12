using System;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Banners;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;
using Levy.Content.Tiles;

namespace Levy.Common.Testing
{
	/// <summary>
	/// TRAVEL LAB, Hold the Banners mode (LEVY_TRAVEL_ORDER=banner). Measurement only. At tick 120 a real Holding Banner is placed with WorldGen.PlaceObject on the floor of the goal (the tile entity placed as the player's placement hook does),
	/// the whole army is given the Hold the banners order through RosterSystem.IssueOrder, and a levy arrives when it stands at its own stand inside the banner's aura. With LEVY_TRAVEL_HOLDSEC=N foes then come at the banner for N seconds and the stands are measured. The banner is taken down when the run ends.
	/// World edit (LEVY_TRAVEL_EDIT): tower:DX[:H[:W[:STAIRS[:TOP]]]] builds a tower DX tiles east of the start: H rows tall (default 35), W wide (default 9), a zigzag of platforms every STAIRS rows (default 3; 0 = a sheer stone pillar),
	/// TOP = platform (a platform floor on top, default) or brick (a stone floor, pillars only). The goal token "tower" is the middle of its top.
	/// </summary>
	public partial class CombatHarness
	{
		private bool _tvBannerMode;
		private int _tvTowerX = -1, _tvTowerFloor = -1;
		private Point16 _tvBannerTop = new Point16(-1, -1);

		private void TvBannerInit() => _tvBannerMode = (Environment.GetEnvironmentVariable("LEVY_TRAVEL_ORDER") ?? "").ToLowerInvariant() == "banner";

		private void TvTowerEdit(string[] q, int baseX)
		{
			int P(int i, int d) => q.Length > i && int.TryParse(q[i], out int v) ? v : d;
			int dx = P(1, 60), h = P(2, 35), w = Math.Max(5, P(3, 9)), stairs = P(4, 3);
			string top = q.Length > 5 ? q[5] : "platform";
			int x = Math.Clamp(baseX + dx, 40, Main.maxTilesX - 40 - w);
			int s = TvTopSolid(x + w / 2);
			// a flat apron: stone to the same level 8 columns each side, clear air above it all the way up
			for (int xx = x - 9; xx <= x + w + 8; xx++)
			{
				for (int y = s - h - 8; y < s; y++) TvAir(xx, y);
				for (int y = s; y <= s + 5; y++) { Tile t = Main.tile[xx, y]; if (!t.HasTile || !Main.tileSolid[t.TileType]) { TvSet(xx, y, TileID.Stone); t = Main.tile[xx, y]; } t.LiquidAmount = 0; }
			}
			int floor = s - h;                                     // the tile row the banner stands on
			if (stairs <= 0)
			{
				for (int xx = x; xx < x + w; xx++) for (int y = floor; y < s; y++) TvSet(xx, y, TileID.GrayBrick);   // a sheer pillar
			}
			else
			{
				int k = 1;
				for (int r = s - stairs; r > floor; r -= stairs, k++)
					for (int xx = x; xx < x + w; xx++)
					{
						bool gap = (k % 2 == 1) ? xx >= x + w - 2 : xx < x + 2;
						if (!gap) TvPlat(xx, r);
					}
			}
			// the top floor: a platform floor, or stone on a sheer pillar (a tower with stairs always ends in a platform floor so the stairs can be climbed through it)
			for (int xx = x; xx < x + w; xx++)
			{
				if (top == "brick" && stairs <= 0) TvSet(xx, floor, TileID.GrayBrick);
				else TvPlat(xx, floor);
			}
			_tvTowerX = x + w / 2; _tvTowerFloor = floor;
			_tvEvents.AppendLine($"EDIT tower x={x} w={w} h={h} stairs={stairs} top={top} surface={s} floorRow={floor} goalX={_tvTowerX}");
		}

		/// <summary>Places the banner on the goal's floor (tile row <c>_tvGy + 3</c>), the middle column of it, and registers its tile entity.</summary>
		private void TvBannerPlace()
		{
			int floorRow = _tvGy + 3, x0 = _tvGx - 1, topY = floorRow - LevyConstants.Banner.Height;
			for (int xx = x0; xx < x0 + LevyConstants.Banner.Width; xx++) for (int y = topY; y < floorRow; y++) { Tile t = Main.tile[xx, y]; if (t.HasTile && !Main.tileSolid[t.TileType]) TvAir(xx, y); }
			bool ok = WorldGen.PlaceObject(x0, floorRow - 1, ModContent.TileType<HoldingBannerTile>(), true, 0);
			Tile tl = Main.tile[x0, topY];
			bool tile = tl.HasTile && tl.TileType == ModContent.TileType<HoldingBannerTile>() && tl.TileFrameX == 0 && tl.TileFrameY == 0;
			int te = -1;
			if (tile) { te = ModContent.GetInstance<HoldingBannerTE>().Place(x0, topY); BannerSystem.Changed(); _tvBannerTop = new Point16(x0, topY); }
			_tvEvents.AppendLine($"BANNER placed={ok} tile={tile} te={te} top=({x0},{topY}) floorRow={floorRow}");
			Mod.Logger.Info($"HARNESS-TRAVEL: banner placed={ok} tile={tile} te={te} at ({x0},{topY})");
		}

		private void TvBannerClear()
		{
			if (_tvBannerTop.X < 0) return;
			for (int xx = _tvBannerTop.X; xx < _tvBannerTop.X + LevyConstants.Banner.Width; xx++) for (int y = _tvBannerTop.Y; y < _tvBannerTop.Y + LevyConstants.Banner.Height; y++) TvAir(xx, y);
			ModContent.GetInstance<HoldingBannerTE>().Kill(_tvBannerTop.X, _tvBannerTop.Y);
			BannerSystem.Changed();
			_tvBannerTop = new Point16(-1, -1);
		}

		/// <summary>A levy has arrived when it stands at its own stand (within 2.5 tiles, same level) and so inside the banner's aura.</summary>
		private bool TvBannerArrived(SoldierNPC sn, NPC n)
		{
			if (_tvBannerTop.X < 0 || !sn.BannerStand(out Point stand)) return false;
			Vector2 sb = global::Levy.Common.Movement.TileNav.BottomCentre(stand.X, stand.Y);
			return Math.Abs(n.Center.X - sb.X) <= 2.5f * 16f && Math.Abs(n.Bottom.Y - sb.Y) <= 2f * 16f && BannerSystem.InAura(_tvBannerTop, n.Center, n.Bottom.Y);
		}

		/// <summary>End of a run: how many levies stand at a stand, carry the buff and how many distinct stands are in use (a line in events.txt).</summary>
		private void TvBannerReport()
		{
			int bodies = 0, buffed = 0, atStand = 0, type = ModContent.BuffType<global::Levy.Content.Buffs.HoldingBannerBuff>();
			var stands = new System.Collections.Generic.HashSet<Point>();
			foreach (var l in _tv)
			{
				if (!l.Rec.HasBody || Main.npc[l.Rec.BodyIndex].ModNPC is not SoldierNPC sn) continue;
				bodies++; NPC n = Main.npc[l.Rec.BodyIndex];
				if (n.HasBuff(type)) buffed++;
				if (TvBannerArrived(sn, n)) { atStand++; sn.BannerStand(out Point st); stands.Add(st); }
			}
			_tvEvents.AppendLine($"BANNERREPORT bodies={bodies} atStand={atStand} buffed={buffed} distinctStands={stands.Count} banners={BannerSystem.BannerPositions().Count} rebuilds={BannerSystem.Rebuilds}");
		}

		// ---- the hold phase (LEVY_TRAVEL_HOLDSEC): once every levy that can has arrived, foes come at the banner for N seconds; how well the army stays on its stands is measured ----
		private int _tvHoldStart = -1, _tvFoesSpawned;
		private sealed class BanHold { public int Ticks, InAura, AtStand, Out6, Hits0; public float MaxDrift; public bool WasOut; public int Episodes; }
		private readonly System.Collections.Generic.Dictionary<int, BanHold> _tvHold = new();

		/// <summary>Called when every levy is done. True = keep running (the hold phase is on); false = finish the run (after the hold report).</summary>
		private bool TvHoldPhase()
		{
			int hold = int.TryParse(Environment.GetEnvironmentVariable("LEVY_TRAVEL_HOLDSEC"), out int hs) ? hs : 0;
			if (!_tvBannerMode || hold <= 0) return false;
			if (_tvHoldStart < 0)
			{
				_tvHoldStart = _tvTick; _tvHold.Clear();
				foreach (var l in _tv) if (l.Rec.HasBody && l.ArriveTick >= 0 && Main.npc[l.Rec.BodyIndex].ModNPC is SoldierNPC sn0) _tvHold[l.Rec.Id] = new BanHold { Hits0 = sn0.HitsTaken };
				_tvEvents.AppendLine($"HOLD start t={_tvTick} levies={_tvHold.Count} for {hold}s");
			}
			if ((_tvTick - _tvHoldStart) % 120 == 0) TvSpawnFoeNearBanner();
			foreach (var l in _tv)
			{
				if (!_tvHold.TryGetValue(l.Rec.Id, out var h) || !l.Rec.HasBody || Main.npc[l.Rec.BodyIndex].ModNPC is not SoldierNPC sn) continue;
				NPC n = Main.npc[l.Rec.BodyIndex];
				if (!sn.BannerStand(out Point stand)) continue;
				Vector2 sb = global::Levy.Common.Movement.TileNav.BottomCentre(stand.X, stand.Y);
				float drift = Vector2.Distance(n.Bottom, sb) / 16f;
				h.Ticks++; if (_tvBannerTop.X >= 0 && BannerSystem.InAura(_tvBannerTop, n.Center, n.Bottom.Y)) h.InAura++;
				if (drift <= 2.5f) h.AtStand++;
				if (drift > h.MaxDrift) h.MaxDrift = drift;
				bool far = drift > 8f; if (far && !h.WasOut) h.Episodes++; h.WasOut = far; if (far) h.Out6++;
			}
			if (_tvTick - _tvHoldStart < hold * 60) return true;
			var sb2 = new StringBuilder(); int n2 = 0; long tk = 0, ia = 0, ast = 0; float worst = 0; int eps = 0, hits = 0, endAt = 0;
			foreach (var l in _tv)
			{
				if (!_tvHold.TryGetValue(l.Rec.Id, out var h)) continue;
				n2++; tk += h.Ticks; ia += h.InAura; ast += h.AtStand; worst = Math.Max(worst, h.MaxDrift); eps += h.Episodes;
				if (l.Rec.HasBody && Main.npc[l.Rec.BodyIndex].ModNPC is SoldierNPC sn) { hits += sn.HitsTaken - h.Hits0; if (sn.BannerStand(out Point st) && Math.Abs(Main.npc[l.Rec.BodyIndex].Bottom.X - global::Levy.Common.Movement.TileNav.BottomCentre(st.X, st.Y).X) <= 2.5f * 16f) endAt++; }
			}
			_tvEvents.AppendLine($"HOLDREPORT levies={n2} pctInAura={(tk > 0 ? 100.0 * ia / tk : 0):0.0} pctAtStand={(tk > 0 ? 100.0 * ast / tk : 0):0.0} maxDriftTiles={worst:0.0} chaseEpisodes(>8 tiles)={eps} hitsTaken={hits} atStandAtEnd={endAt}/{n2} foesSpawned={_tvFoesSpawned}");
			_tvHoldStart = -1;
			return false;
		}

		private void TvSpawnFoeNearBanner()
		{
			if (_tvBannerTop.X < 0) return;
			int hostile = 0; for (int i = 0; i < Main.maxNPCs; i++) { NPC nn = Main.npc[i]; if (nn.active && !nn.friendly && !nn.townNPC && nn.damage > 0) hostile++; }
			if (hostile >= 6) return;
			int side = (_tvTick / 120) % 2 == 0 ? 1 : -1;
			int tx = Math.Clamp(_tvBannerTop.X + side * 15, 20, Main.maxTilesX - 20); _tvFoesSpawned++;
			int ty = TvSurf[tx] > 0 ? TvSurf[tx] : _tvBannerTop.Y + LevyConstants.Banner.Height;
			int[] kinds = { NPCID.Zombie, NPCID.GreenSlime, NPCID.Zombie, NPCID.DemonEye, NPCID.BlueSlime };
			NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("LevyTravel"), tx * 16 + 8, (ty - 2) * 16, kinds[_tvTick / 120 % kinds.Length]);
		}

		private void TvBannerOrder()
		{
			TvBannerPlace();
			RosterSystem.Instance.IssueOrder(Main.player[0], SoldierOrder.HoldBanners, true);
			Mod.Logger.Info("HARNESS-TRAVEL: Hold the banners issued");
		}
	}
}
