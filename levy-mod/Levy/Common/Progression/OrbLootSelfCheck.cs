using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Progression
{
	/// <summary>Boot check (server with a world, LEVY_ORBCHECK=1): with the toggle on and 20 levies, one smash drops 1 vanilla roll plus 4 extra rolls from the orb's own pool, and the orb counter advances by exactly one.</summary>
	public class OrbLootSelfCheck : ModSystem
	{
		int _tick; bool _done;
		static readonly HashSet<int> Pool = new()
		{
			ItemID.Musket, ItemID.MusketBall, ItemID.Vilethorn, ItemID.BallOHurt, ItemID.BandofStarpower, ItemID.ShadowOrb,
			ItemID.TheUndertaker, ItemID.CrimsonRod, ItemID.TheRottedFork, ItemID.PanicNecklace, ItemID.CrimsonHeart
		};

		public override void OnWorldLoad()
		{
			if (_done || Environment.GetEnvironmentVariable("LEVY_ORBCHECK") != "1" || Main.netMode == NetmodeID.MultiplayerClient) return;
			
			_done = true;
			int pass = 0, fail = 0;
			void Check(string n, bool ok, string d = "") { if (ok) pass++; else fail++; Mod.Logger.Info($"[OrbCheck] {(ok ? "PASS" : "FAIL")} {n} :: {d}"); }
			try
			{
				var cfg = LevyProgressionConfig.Get;
				Check("config default off, 5 levies per roll", !cfg.OrbLootScaling && cfg.LeviesPerExtraOrbRoll == 5, $"{cfg.OrbLootScaling} {cfg.LeviesPerExtraOrbRoll}");
				Check("20 levies / 5 = 4 extra", OrbLootSystem.ExtraRolls(20, 5) == 4 && OrbLootSystem.ExtraRolls(4, 5) == 0);
				cfg.OrbLootScaling = true; cfg.LeviesPerExtraOrbRoll = 5; OrbLootSystem.LeviesOverride = 20;
				int px = Main.maxTilesX / 2, py = (int)Main.worldSurface + 40;
				foreach (bool crim in new[] { false, true })
					for (int trial = 0; trial < 3; trial++)
					{
						bool oldCrim = WorldGen.crimson; WorldGen.crimson = crim;
						int x = px + trial * 10, y = py;
						for (int a = -3; a <= 3; a++) for (int b = -4; b <= 2; b++) { var t = Main.tile[x + a, y + b]; t.ClearTile(); }
						for (int a = -2; a <= 3; a++) { var t = Main.tile[x + a, y + 2]; t.HasTile = true; t.TileType = TileID.Stone; }
						WorldGen.AddShadowOrb(x, y);
						var pieces = new List<(int, int)>();
						for (int a = -1; a <= 2; a++) for (int b = -1; b <= 2; b++) if (Main.tile[x + a, y + b].HasTile && Main.tile[x + a, y + b].TileType == TileID.ShadowOrbs) pieces.Add((x + a, y + b));
						int before = WorldGen.shadowOrbCount; bool smashed = WorldGen.shadowOrbSmashed;
						var seen = new HashSet<int>(); for (int k = 0; k < Main.maxItems; k++) if (Main.item[k].active) seen.Add(k);
						OrbLootSystem.LastBankRolls = -1;
						if (pieces.Count > 1)
						{
							// vanilla smash path: one piece is removed, the frame check of a remaining piece destroys the orb and drops its loot
							var (ox, oy) = pieces[0]; var tt = Main.tile[ox, oy]; tt.HasTile = false;
							var (cx, cy) = pieces[1]; WorldGen.CheckOrb(cx, cy, TileID.ShadowOrbs);
						}
						var drops = new List<int>(); for (int k = 0; k < Main.maxItems; k++) if (Main.item[k].active && !seen.Contains(k)) drops.Add(Main.item[k].type);
						string kind = crim ? "heart" : "orb";
						Check($"{kind}#{trial} placed", pieces.Count > 1, $"pieces={pieces.Count}");
						Check($"{kind}#{trial} drops 1+4 rolls", OrbLootSystem.LastBankRolls == 4 && OrbLootSystem.LastBankItems >= 4, $"bankRolls={OrbLootSystem.LastBankRolls} bankItems={OrbLootSystem.LastBankItems} total={drops.Count} [{string.Join(",", drops.Select(d => ItemID.Search.GetName(d)))}]");
						Check($"{kind}#{trial} right pool", drops.Count > 0 && drops.All(Pool.Contains));
						Check($"{kind}#{trial} orb counter +1 only", WorldGen.shadowOrbCount == before + 1, $"{before}->{WorldGen.shadowOrbCount}");
						WorldGen.crimson = oldCrim; WorldGen.shadowOrbCount = before; WorldGen.shadowOrbSmashed = smashed;
						for (int k = 0; k < Main.maxItems; k++) if (!seen.Contains(k)) Main.item[k].active = false;
					}
			}
			catch (Exception e) { Check("no exception", false, e.ToString()); }
			finally { OrbLootSystem.LeviesOverride = -1; LevyProgressionConfig.Get.OrbLootScaling = false; }
			Mod.Logger.Info($"[OrbCheck] SUMMARY pass={pass} fail={fail}");
		}
	}
}
