using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>
	/// DODGE LAB (env WARBANDS_LIVE=dodge; WARBANDS_SEEDS per band type, default 6; WARBANDS_OUT the TSV). A hostile band fights the stand-in player in the open arena while the stand-in fires real friendly bullets (player-owned, aimed at a member's
	/// centre at the moment of firing, no lead, as a player aims) at one living member after another, one every 40 ticks, 40 shots per trial from 14 to 34 tiles. A shot is "hit" when its swept path crosses any band member's hitbox before it dies
	/// (a dedicated server runs no player-side projectile hits, so the lab counts them), "dodged" when it dies without crossing one. Members keep their own stats: the stand-in's own hits are switched off (accuracy 0) so the band is never thinned.
	/// A control row fires the same shots at a target dummy 24 tiles away (every shot should cross it). One TSV row per trial: shots, hits, dodged share, and the dodge manoeuvres the band's brains made.
	/// </summary>
	public partial class WarbandsLab
	{
		private class Shot { public int Id, Identity, Born; public Vector2 Prev; public float Dist; public char Role; public bool Done, Hit, TargetHit; public int TargetNpc; }
		private static int DodgeFld(object o, string name) { try { var f = typeof(Levy.Content.NPCs.SoldierNPC).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance); return f == null ? 0 : (int)f.GetValue(o); } catch { return 0; } }

		private void AddDodgeTrials()
		{
			int seeds = int.TryParse(Environment.GetEnvironmentVariable("WARBANDS_SEEDS"), out int sv) ? sv : 6;
			var types = new[] { BandTypes.ById("ForestDeserters"), BandTypes.ById("MeteorMarauders"), BandTypes.ById("MythrilMercenaries") };
			_tsv.AppendLine("kind\ttype\tseed\tmembers\tshots\thits\tdodged_pct\ttarget_evaded_pct\tmaneuvers\tsteps\tjumps\tdashes\thooks");
			const int shots = 40, every = 40, warm = 180;
			for (int seed = -1; seed < seeds; seed++)
			{
				bool control = seed < 0; int sd = Math.Max(0, seed);
				var type = types[sd % types.Length];
				var list = new List<Shot>(); int fired = 0; int dummy = -1;
				_trials.Add(new Trial
				{
					Name = control ? "dodge control: target dummy at 24 tiles" : $"dodge {type.Id} / seed {sd}", Max = warm + shots * every + 240,
					Setup = () =>
					{
						Main.rand = new Terraria.Utilities.UnifiedRandom(52000 + sd); list.Clear(); fired = 0; dummy = -1;
						ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; DamageTaken = 0; _shotIds.Clear(); _shotsSeen = 0;
						if (control) { dummy = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent(), (int)(PlayerPos.X + 24 * 16), (int)(ArenaFloor * 16f - 2f), NPCID.TargetDummy); if (dummy >= 0 && dummy < Main.maxNPCs) { _labNpcs.Add(dummy); Main.npc[dummy].dontTakeDamage = true; } }
						else { SpawnArmBand(type, 4 + sd % 2, false); BandSpawner.LastBrain.Aggro = true; BandSpawner.LastBrain.Warns = false; BandSpawner.LastBrain.Neutral = false; }
					},
					Tick = t =>
					{
						// outcome of the shots in flight
						foreach (var s in list)
						{
							if (s.Done) continue;
							var pr = Main.projectile[s.Id];
							bool alive = pr.active && pr.identity == s.Identity && pr.friendly && pr.type == ProjectileID.Bullet;
							Vector2 cur = pr.Center;
							var bodies = control ? (dummy >= 0 ? new List<NPC> { Main.npc[dummy] } : new List<NPC>()) : Levies().Where(b => b.NPC.life > 0).Select(b => b.NPC).ToList();
							foreach (var n in bodies)
							{
								var r = n.Hitbox;
								if (Collision.CheckAABBvLineCollision(r.TopLeft(), r.Size(), s.Prev, cur)) { s.Hit = true; if (n.whoAmI == s.TargetNpc) s.TargetHit = true; }
							}
							s.Prev = cur;
							if (!alive || t - s.Born > 150) s.Done = true;
						}
						if (t >= warm && (t - warm) % every == 0 && fired < shots)
						{
							var pool = control ? (dummy >= 0 ? new List<NPC> { Main.npc[dummy] } : new List<NPC>()) : Levies().Where(b => b.NPC.life > 0).Select(b => b.NPC).ToList();
							if (pool.Count > 0)
							{
								var tgt = pool[fired % pool.Count];
								Vector2 from = Main.player[0].Center, to = tgt.Center; float d = Vector2.Distance(from, to) / 16f;
								Vector2 v = Vector2.Normalize(to - from) * 8f;
								int id = Projectile.NewProjectile(new Terraria.DataStructures.EntitySource_Misc("Warbands:DodgeLab"), from, v, ProjectileID.Bullet, 40, 1f, 0);
								if (id >= 0 && id < Main.maxProjectiles)
								{
									var pr = Main.projectile[id]; pr.friendly = true; pr.hostile = false; pr.owner = 0; pr.timeLeft = 120; pr.tileCollide = true;
									list.Add(new Shot { Id = id, Identity = pr.identity, Prev = from, Born = t, Dist = d, TargetNpc = tgt.whoAmI, Role = (tgt.ModNPC as BandLevyNPC)?.Role ?? 'D' });
									fired++;
								}
							}
						}
						return fired >= shots && list.All(s => s.Done);
					},
					Result = () =>
					{
						int total = list.Count, hits = list.Count(s => s.Hit), thits = list.Count(s => s.TargetHit);
						double dodged = total == 0 ? -1 : 100.0 * (total - hits) / total, evaded = total == 0 ? -1 : 100.0 * (total - thits) / total;
						string byRole = string.Join(" ", list.GroupBy(x => x.Role).OrderBy(g => g.Key).Select(g => $"{g.Key}:{100 * g.Count(x => !x.TargetHit) / g.Count()}%"));
						int man = 0, st = 0, jp = 0, ds = 0, hk = 0;
						foreach (var b in Levies()) { man += DodgeFld(b, "DodgeManeuvers"); st += DodgeFld(b, "DodgeSteps"); jp += DodgeFld(b, "DodgeJumps"); ds += DodgeFld(b, "DodgeDashes"); hk += DodgeFld(b, "DodgeHooks"); }
						_tsv.AppendLine($"{(control ? "control" : "band")}\t{type.Id}\t{sd}\t{Levies().Count}\t{total}\t{hits}\t{dodged:0.0}\t{evaded:0.0}\t{man}\t{st}\t{jp}\t{ds}\t{hk}");
						return (total >= shots - 2, $"{total} shots, {hits} crossed a member, {dodged:0.0}% dodged by the band, the member aimed at evaded {evaded:0.0}% (by role {byRole}); manoeuvres {man} (steps {st}, jumps {jp}, dashes {ds}, hooks {hk}); members {Levies().Count}");
					},
				});
			}
		}
	}
}
