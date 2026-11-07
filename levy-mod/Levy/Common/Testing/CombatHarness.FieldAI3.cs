using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>FIELD AI 3 trials (LEVY_FIELD_SPEC): gravflip. Measurement only.</summary>
	public partial class CombatHarness
	{
		// ---- the commander flips gravity (Gravitation Potion) and hangs from a ceiling far above the army ----
		private FaTrial FaGravFlip(int fallDamage, bool lavaFloor)
		{
			const int Ticks = 1500; const int CeilUp = 45;
			int n = 3; Vector2[] last = new Vector2[n]; int teleports = 0; int[] minLife = { int.MaxValue, int.MaxValue, int.MaxValue }; int midAir = 0;
			return new FaTrial
			{
				Name = "GRAVFLIP", Ticks = Ticks,
				Setup = () =>
				{
					SoldierNPC.DodgeMode = 2; SoldierNPC.FallDamageOverride = fallDamage;
					int cx = _cx, fy = _faFy, ceil = fy - CeilUp;
					FaFill(cx + 10, ceil - 3, cx + 90, ceil - 1, true);                         // the ceiling slab; its lower edge is row `ceil`
					if (lavaFloor) for (int x = cx + 10; x <= cx + 90; x++) for (int y = fy - 3; y < fy; y++) { Tile t = Main.tile[x, y]; t.LiquidAmount = 255; t.LiquidType = Terraria.ID.LiquidID.Lava; }
					_faCmdGrav = -1f; _faCmdPos = new Vector2((cx + 50) * 16f, ceil * 16f);       // hanging under the slab
					for (int k = 0; k < n; k++)
					{
						var b = FaSpawn((cx - 100 + k * 4) * 16f, fy * 16f, r => { r.Order = SoldierOrder.Follow; });
						b.RefreshStats(); last[k] = b.NPC.Center;
					}
				},
				Step = t =>
				{
					for (int k = 0; k < n; k++)
					{
						NPC nn = _faSol[k].NPC; if (!nn.active) continue;
						if (Vector2.Distance(nn.Center, last[k]) > 20 * 16f) teleports++;
						if (nn.Bottom.Y < (_faFy - 3) * 16f - 16f && nn.velocity.Y != 0f && Math.Abs(nn.Center.X - Main.player[0].Center.X) < 200f) midAir++;
						last[k] = nn.Center; minLife[k] = Math.Min(minLife[k], nn.life);
					}
				},
				Report = () =>
				{
					var sb = new StringBuilder($"fallDamageConfig={(fallDamage == 1 ? "on" : "off")} floor={(lavaFloor ? "lava under the ceiling" : "stone")} commander=gravity-flipped, ceiling {CeilUp} tiles over the floor, army 150 tiles away (catch-up teleport range). teleports={teleports} ticksAirborneNearCommander={midAir}");
					int dead = 0;
					for (int k = 0; k < n; k++)
					{
						var b = _faSol[k]; NPC nn = b.NPC; bool alive = nn.active && nn.life > 0; if (!alive) dead++;
						float drop = alive ? (nn.Bottom.Y / 16f - (_faFy - CeilUp)) : -1f;
						sb.Append($" | levy{k}: alive={alive} hp={(alive ? nn.life : 0)}/{nn.lifeMax} lowest={minLife[k]} fallHits={b.FallHits} fallDmg={b.FallDamageTotal} lastFallTiles={b.LastFallTiles} bottomRowsBelowCeiling={(alive ? drop.ToString("0") : "-")} dxToCommander={(alive ? ((nn.Center.X - Main.player[0].Center.X) / 16f).ToString("0") : "-")}");
					}
					return $"DEAD={dead}/{n} " + sb;
				},
			};
		}
	}
}
