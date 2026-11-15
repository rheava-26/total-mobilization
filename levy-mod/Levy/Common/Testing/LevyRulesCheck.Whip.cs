using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WHIP part of the rules check: a summoner levy (whip in hand, minion staff beside it, as the armory issues them) fights three ways for 30 s each and its lashes a minute are read against the best rate a Player has with the
	/// same whip (one lash per use animation, the whip's own use rate with the dummy's speed bonuses). Three fights: an enemy standing in the lash's reach, a hopping enemy that walks up to the levy (real slime AI, a lot of life),
	/// and a boss (King Slime, real AI). For each, the ticks the nearest enemy was inside the lash's reach are split into: lash out, waiting on the use time, and READY with nothing thrown (the number the AI owes).
	/// </summary>
	public partial class LevyRulesCheck
	{
		private sealed class WhipRow
		{
			public string Fight, Whip; public int Window, Swings, Ticks, InReach, LashOut, Waiting, Ready, ReadyNotEngaged, MinionsPeak, ReadyNoTarget; public Dictionary<string, int> States = new(); public int Anim; public float ReachPx; public double PerMin, PlayerMax, Duty;
		}

		private static readonly List<WhipRow> _whipRows = new();

		private static void WhipPart()
		{
			Arena(FloorY, Half); PlaceCommander(-8);
			foreach (int whip in new[] { ItemID.BlandWhip, ItemID.SwordWhip })
			{
				string wn = Lang.GetItemNameValue(whip);
				var a = WhipFight("enemy in reach", whip, 0); var b = WhipFight("walking enemy", whip, 1); var c = WhipFight("boss", whip, 2);
				foreach (var r in new[] { a, b, c })
				{
					if (r == null) continue;
					Note($"whip [{wn}] {r.Fight}: {r.Swings} lashes in {r.Window / 60}s = {r.PerMin:0.0}/min (a Player's best {r.PlayerMax:0.0}/min, use animation {r.Anim}t, reach {r.ReachPx:0}px); enemy in reach {r.InReach} ticks: lash out {r.LashOut}, waiting {r.Waiting}, READY and idle {r.Ready} ({r.ReadyNotEngaged} of them not Engage [{string.Join(", ", r.States.Select(k => k.Key + " " + k.Value))}], {r.ReadyNoTarget} Engaged on another target); duty in reach {r.Duty:P0}; minions peak {r.MinionsPeak}");
					WhipSummary(r);
				}
				if (a != null) Check($"whip [{wn}] enemy in reach: the levy lashes at least 85% of a Player's best rate ({a.PerMin:0.0} of {a.PlayerMax:0.0}/min)", a.PerMin >= 0.85 * a.PlayerMax);
				if (b != null) Check($"whip [{wn}] walking enemy: while it is in reach the levy lashes as often as a Player could ({b.Duty:P0} of the ticks in reach, floor 80%)", b.InReach >= 120 && b.Duty >= 0.80);
				if (c != null) Check($"whip [{wn}] boss: while the boss is in reach the levy lashes as often as a Player could ({c.Duty:P0} of the ticks in reach, floor 80%)", c.InReach < 120 || c.Duty >= 0.80);
			}
		}

		private static void WhipSummary(WhipRow r) => _whipRows.Add(r);

		private static WhipRow WhipFight(string fight, int whipItem, int kind)
		{
			Arena(FloorY, Half); PlaceCommander(-8);
			Vector2 at = new Vector2((Cx - 4) * 16f, _floorY * 16f);
			var s = Levy(at, SoldierOrder.Follow); if (s == null) { Check($"whip {fight}: the levy spawns", false); return null; }
			NPC enemy = null; var enemies = new List<NPC>();
			try
			{
				var g = s.Record.Gear;
				g.Weapon = Make(whipItem); g.Sidearm = Make(ItemID.SlimeStaff);
				s.Record.LoadoutLocked = true; s.StatsDirty = true;
				Step(30); s.RefreshStats();
				Item item = g.Weapon;
				int anim = RealWhips.TotalAnimation(s.Dummy, item), use = Math.Max(1, CombinedHooks.TotalUseTime(Math.Max(1, item.useTime), s.Dummy, item));
				// the enemy
				Vector2 spawn = new Vector2((Cx + 14) * 16f, (_floorY - 4) * 16f);
				if (kind == 0)
				{
					enemy = SpawnEnemy(NPCID.GreenSlime, new Vector2((Cx - 4) * 16f + 90f, _floorY * 16f), true);
				}
				else if (kind == 1) enemy = SpawnEnemy(NPCID.GreenSlime, spawn, false);
				else enemy = SpawnEnemy(NPCID.KingSlime, spawn, false);
				if (enemy == null) { Check($"whip {fight}: the enemy spawns", false); return null; }
				enemies.Add(enemy); _keep.Add(enemy.whoAmI);
				var row = new WhipRow { Fight = fight, Whip = Lang.GetItemNameValue(whipItem), Window = 1800, Anim = anim };
				int real0 = RealWhips.Real, fb0 = RealWhips.Fallbacks;
				Vector2 stand = s.NPC.position;
				_perTick = () =>
				{
					// the levy is kept alive (the fight is about its whip, not about surviving the enemy)
					s.NPC.life = s.NPC.lifeMax; s.Record.CurrentHp = s.NPC.life;
					if (!enemy.active) return;
					enemy.life = enemy.lifeMax; enemy.timeLeft = 9999;
					if (s.Weapon == null || !s.Weapon.IsWhip) return;
					float reach = RealWhips.MeasuredReach(whipItem); if (reach <= 0f) reach = s.Weapon.ThrustReachPx;
					float whipMult = s.Dummy != null && s.Dummy.whipRangeMultiplier > 0.5f ? s.Dummy.whipRangeMultiplier : 1f;
					row.ReachPx = reach * whipMult;
					NPC seg = Aiming.NearestSegment(enemy, s.NPC.Center) ?? enemy;
					float tiles = Aiming.TilesToHitbox(s.NPC.Center, seg);
					row.Ticks++;
					if (tiles * 16f > reach * whipMult * 0.97f) return; // the AI's own test is the reach it knows; a margin keeps the count honest
					row.InReach++;
					if (RealWhips.LiveFor(s) != null) row.LashOut++;
					else if (s.AttackCooldown > 0) row.Waiting++;
					else { row.Ready++; if (s.State != SoldierState.Engage) { row.ReadyNotEngaged++; string k = s.State + (s.TargetIndex < 0 ? "/no target" : s.TargetIndex == enemy.whoAmI ? "/target=enemy" : "/target=other"); row.States[k] = row.States.GetValueOrDefault(k) + 1; } else if (s.TargetIndex != enemy.whoAmI) row.ReadyNoTarget++; }
					int minions = 0; for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && p.minion && (SoldierMinions.OwnedBy(p, s.NPC.whoAmI) || (p.type == ModContent.ProjectileType<global::Levy.Content.Projectiles.SoldierMinion>() && (int)p.ai[0] == s.NPC.whoAmI))) minions++; }
					row.MinionsPeak = Math.Max(row.MinionsPeak, minions);
				};
				Step(row.Window);
				_perTick = null;
				row.Swings = (RealWhips.Real - real0) + (RealWhips.Fallbacks - fb0 > 0 ? 0 : 0);
				if (row.Swings == 0 && RealWhips.Disabled) row.Swings = 0;
				row.PerMin = row.Swings * 3600.0 / row.Window;
				row.PlayerMax = 3600.0 / Math.Max(1, anim);
				row.Duty = row.InReach > 0 ? Math.Min(1.0, row.Swings * (double)anim / row.InReach) : 0;
				return row;
			}
			finally { _perTick = null; foreach (var e in enemies) { _keep.Remove(e.whoAmI); e.active = false; } Remove(s); for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false; }
		}

		private static NPC SpawnEnemy(int type, Vector2 at, bool pinned)
		{
			int i = NPC.NewNPC(new EntitySource_WorldEvent("LevyRulesCheck"), (int)at.X, (int)at.Y, type);
			if (i < 0 || i >= Main.maxNPCs) return null;
			NPC n = Main.npc[i]; n.timeLeft = 99999; n.netOffset = Vector2.Zero;
			n.lifeMax = n.life = 1_000_000; n.defense = 0; n.knockBackResist = 0f;
			if (type == NPCID.GreenSlime) { n.damage = 20; n.width = 64; n.height = 48; }
			if (pinned) { n.noGravity = true; n.noTileCollide = true; n.aiStyle = -1; n.damage = 0; n.Bottom = at; var ht = n.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = n.position; }
			return n;
		}
	}
}
