using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// HOOKS IN NATURAL TERRAIN (LEVY_FIELD_SPEC=hookterrain). A levy on a Follow order, with and without a hook in its gear, goes after a commander who stands where only a climb, a swing or a long way round reaches:
	/// cliff (a tall wall with a ramp 70 tiles round), shaft (a 32-tile vertical shaft), island (a floating island), cave (an overhang ledge in a tunnel), hills (rolling ground, the commander jogging: the control).
	/// Prints one "HOOKT|..." line per trial. Env: LEVY_HOOKT=cliff,shaft,island,cave,hills  LEVY_HOOKT_BEFORE=1 (the old trigger rules)
	/// </summary>
	public partial class CombatHarness
	{
		private void FaAddHookTerrain()
		{
			var want = new HashSet<string>((Environment.GetEnvironmentVariable("LEVY_HOOKT") ?? "cliff,shaft,island,cave,hills").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
			bool before = Environment.GetEnvironmentVariable("LEVY_HOOKT_BEFORE") == "1";
			foreach (string kind in new[] { "cliff", "shaft", "island", "cave", "hills" })
			{
				if (!want.Contains(kind)) continue;
				foreach (int hook in new[] { 0, ItemID.GrapplingHook }) _fa.Add(FaHookT(kind, hook, before));
			}
		}

		private FaTrial FaHookT(string kind, int hookItem, bool before)
		{
			const int Ticks = 2400;
			int arrive = -1, casts0 = 0, teleports = 0, pitTicks = 0; float bestD = float.MaxValue, sumD = 0f; int nD = 0; Vector2 last = Vector2.Zero; int minLife = int.MaxValue; float startD = 0f;
			return new FaTrial
			{
				Name = "HOOKT", Ticks = Ticks,
				Setup = () =>
				{
					SoldierNPC.DodgeMode = 2; SoldierNPC.FallDamageOverride = 0; SoldierNPC.HookShortcutMode = before ? 0 : 1;
					int cx = _cx, fy = _faFy;
					FaBuildArena(kind == "hills" ? "natural" : "flat");
					Vector2 cmd;
					switch (kind)
					{
						case "cliff":
							FaFill(cx + 8, fy - 30, cx + 60, fy - 1, true, TileID.Stone);                    // a 30-tile wall, flat top
							for (int i = 0; i < 30; i++) FaFill(cx + 61 + i * 2, fy - 30 + i, cx + 62 + i * 2, fy - 1, true, TileID.Stone);   // a ramp down the far side, 60 tiles round
							cmd = new Vector2((cx + 30) * 16f, (fy - 30) * 16f); break;
						case "shaft":
							FaFill(cx + 10, fy - 36, cx + 40, fy - 1, true, TileID.Stone);
							FaFill(cx + 20, fy - 33, cx + 23, fy - 1, false);                                // a 4-wide shaft, no ledges
							FaFill(cx + 20, fy - 34, cx + 40, fy - 33, false);                               // a tunnel at the top
							cmd = new Vector2((cx + 30) * 16f, (fy - 33) * 16f + 16f); break;
						case "island":
							FaFill(cx + 6, fy - 24, cx + 30, fy - 22, true, TileID.Stone);                   // a floating island 22 tiles up
							cmd = new Vector2((cx + 18) * 16f, (fy - 24) * 16f); break;
						case "cave":
							FaFill(cx + 4, fy - 40, cx + 50, fy - 1, true, TileID.Stone);
							FaFill(cx + 4, fy - 12, cx + 16, fy - 1, false);                                  // a cavern 12 tall open to the left
							FaFill(cx + 16, fy - 30, cx + 30, fy - 12, false);                                // a chimney up to a ledge cave
							FaFill(cx + 16, fy - 30, cx + 44, fy - 24, false);
							cmd = new Vector2((cx + 36) * 16f, (fy - 24) * 16f); break;
						default:
							cmd = new Vector2((cx - 100) * 16f, 0f); break;
					}
					if (kind == "hills") { _faCmdPos = new Vector2((cx - 90) * 16f, FaGroundRow((cx - 90) * 16f) * 16f - Main.player[0].height); }
					else _faCmdPos = new Vector2(cmd.X, cmd.Y - Main.player[0].height);
					float sx = kind == "hills" ? (cx - 100) * 16f : (cx - 8) * 16f;
					var b = FaSpawnSlot(0, sx, FaGroundRow(sx) * 16f, r =>
					{
						r.Order = SoldierOrder.Follow; r.HoldFire = true; r.LifeCrystals = 15;
						if (hookItem != 0) { r.Gear.Hook = RItem(hookItem); r.Gear.SetLocked(SoldierGear.SlotHook, true); }
					});
					b.RefreshStats(); casts0 = b.HkCasts; last = b.NPC.Center; startD = Vector2.Distance(b.NPC.Bottom, _faCmdPos.Value + new Vector2(10f, Main.player[0].height)) / 16f;
				},
				Step = t =>
				{
					var b = _faSol[0]; NPC n = b.NPC; if (!n.active) return;
					if (kind == "hills")
					{
						float mx = (_cx - 90) * 16f + t * 1.2f; _faCmdPos = new Vector2(mx, FaGroundRow(mx) * 16f - Main.player[0].height); _faCmdGrap = (t / 240) % 3 == 2 ? 1 : 0;
					}
					minLife = Math.Min(minLife, n.life);
					if (Vector2.Distance(n.Center, last) > 20 * 16f) teleports++;
					last = n.Center;
					float d = Vector2.Distance(n.Bottom, Main.player[0].Bottom) / 16f; bestD = Math.Min(bestD, d); sumD += d; nD++;
					if (arrive < 0 && d < 6f && n.velocity.Y == 0f) arrive = t;
					if (n.Bottom.Y > _faFy * 16f + 16f) pitTicks++;
				},
				Report = () =>
				{
					var b = _faSol[0]; NPC n = b.NPC; bool alive = n.active && n.life > 0;
					float d = alive ? Vector2.Distance(n.Bottom, Main.player[0].Bottom) / 16f : -1f;
					string line = $"HOOKT|terrain={kind}|hook={(hookItem == 0 ? "none" : "grappling")}|rules={(before ? "before" : "after")}|startDist={startD:0}T|reachedAtTick={(arrive >= 0 ? arrive.ToString() : "never")}|finalDist={d:0.0}T|closest={bestD:0.0}T|meanDist={(nD > 0 ? sumD / nD : 0):0.0}T|teleports={teleports}|hookCasts={b.HkCasts - casts0}|latches={b.HkLatches}|landings={b.HkLandings}|aborts={b.HkAborts}|alive={alive}";
					Mod.Logger.Info(line);
					return line;
				},
			};
		}
	}
}
