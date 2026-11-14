using System;
using Microsoft.Xna.Framework;
using Terraria;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>
	/// SPAWN LAB (env WARBANDS_LIVE=spawn; WARBANDS_SEEDS = number of spawns, default 400; WARBANDS_SPAWNSIZE band size, default 4; WARBANDS_OUT the TSV). The self-check's spawner case, repeated: a stand-in player at the world spawn asks for a band of 4 (stage 1) over and over on the loaded world.
	/// One TSV row per spawn: members made and the spawner's own report (spots found, spots missing, NPC slots full, soldiers dropped for starting inside a tile). The summary line counts how many spawns fell short and why.
	/// </summary>
	public partial class WarbandsLab
	{
		private void AddSpawnTrials()
		{
			int n = int.TryParse(Environment.GetEnvironmentVariable("WARBANDS_SEEDS"), out int sv) ? sv : 400;
			int size = int.TryParse(Environment.GetEnvironmentVariable("WARBANDS_SPAWNSIZE"), out int zv) ? zv : 4;
			_tsv.AppendLine("seed\tformed\tmade\twanted\tspots\tnoSpot\tslotFull\tembedded");
			_trials.Add(new Trial
			{
				Name = $"spawn lab: {n} bands of {size} at the world spawn", Max = 20,
				Setup = () => { },
				Tick = t =>
				{
					int short4 = 0, noSpotN = 0, slotN = 0, embN = 0, failed = 0;
					for (int seed = 0; seed < n; seed++)
					{
						Main.rand = new Terraria.Utilities.UnifiedRandom(900 + seed);
						ClearArena();
						var fake = new Player(); fake.position = new Vector2(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16); fake.width = 20; fake.height = 42;
						BandSpawner.Report = default;
						bool formed = BandSpawner.SpawnBand(fake, 1, size);
						int count = 0; for (int i = 0; i < Main.maxNPCs; i++) { var nn = Main.npc[i]; if (nn.active && nn.ModNPC is BandLevyNPC) count++; }
						var r = BandSpawner.Report;
						_tsv.AppendLine($"{seed}\t{(formed ? 1 : 0)}\t{count}\t{r.Wanted}\t{r.Spots}\t{r.NoSpot}\t{r.SlotFull}\t{r.Embedded}");
						if (!formed) failed++;
						else if (count < size) { short4++; if (r.NoSpot > 0) noSpotN++; if (r.SlotFull > 0) slotN++; if (r.Embedded > 0) embN++; }
					}
					ClearArena();
					Mod.Logger.Info($"[Warbands] spawn lab: {n} spawns of {size}: short={short4} ({100.0 * short4 / n:0.0}%) failedToForm={failed} short because: noStandingRoom={noSpotN} npcSlotsFull={slotN} embedded={embN}");
					return true;
				},
				Result = () => (true, "ran"),
			});
		}
	}
}
