using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;
using Levy.Content.Projectiles;

namespace Levy.Common.Events
{
	/// <summary>
	/// Old One's Army levy sentries. While the event runs (and "Old One's Army uses levy sentries" is on) regular levies stand down: their bodies are taken off the
	/// field and the roster will not field new ones (RosterSystem.SpawnBody asks <see cref="BlocksSpawn"/>). Each placed <see cref="LevySentryBanner"/> then calls up
	/// 1 / 2 / 3 levies (by tier) from its owner's roster and keeps topping them up as they fall. Sentries and the stand-down both end with the event; levies a sentry
	/// called simply carry on as the normal army. Militia are town defenders, not regulars, and are not affected. Server / single player only.
	/// </summary>
	public class OldOnesArmySystem : ModSystem
	{
		public static OldOnesArmySystem Instance => ModContent.GetInstance<OldOnesArmySystem>();

		public static bool ConfigOn => LevyConfig.Get?.OldOnesArmyUsesLevySentries ?? false;
		/// <summary>The mode is in force: option on and the event running.</summary>
		public static bool Active => ActiveFor(ConfigOn, DD2Event.Ongoing);
		public static bool ActiveFor(bool optionOn, bool eventOngoing) => optionOn && eventOngoing;

		/// <summary>True only while a sentry itself spawns a levy, so the stand-down lets that one through.</summary>
		internal static bool Calling;

		/// <summary>Regular levies are refused a body while the mode is active, except the ones a sentry is calling.</summary>
		public static bool BlocksSpawn(SoldierRecord rec) => rec != null && rec.Kind == SoldierKind.Regular && Active && !Calling;

		public static int LeviesFor(int tier) => LevyConstants.OoaLeviesPerTier[Math.Clamp(tier, 1, 3) - 1];
		public static int ManaFor(int tier) => LevyConstants.OoaEtherianManaPerTier[Math.Clamp(tier, 1, 3) - 1];
		/// <summary>Crystals left after paying for a sentry of this tier, or -1 if the player cannot afford it.</summary>
		public static int Pay(int crystalsHeld, int tier) => crystalsHeld >= ManaFor(tier) ? crystalsHeld - ManaFor(tier) : -1;

		public enum Edge { None, Started, Ended }
		public static Edge EdgeOf(bool wasActive, bool nowActive) => !wasActive && nowActive ? Edge.Started : wasActive && !nowActive ? Edge.Ended : Edge.None;

		/// <summary>Candidates (in roster order) a sentry may call: this commander's living regulars with no body, off reserve, not already called. At most <paramref name="need"/>.</summary>
		public static List<SoldierRecord> PickCallups(IEnumerable<SoldierRecord> roster, string commander, int need, HashSet<int> alreadyCalled, Func<SoldierRecord, bool> hasBody)
		{
			var list = new List<SoldierRecord>();
			foreach (var r in roster)
			{
				if (list.Count >= need) break;
				if (r == null || r.Kind != SoldierKind.Regular || r.CommanderName != commander || r.Dead || r.Reserve || hasBody(r) || alreadyCalled.Contains(r.Id)) continue;
				list.Add(r);
			}
			return list;
		}

		/// <summary>levy record id -> the sentry projectile (whoAmI, identity) that called it.</summary>
		private readonly Dictionary<int, (int index, int identity)> _called = new();
		private bool _wasActive;
		private int _timer;
		public int CalledCount => _called.Count;

		public override void ClearWorld() { _called.Clear(); _wasActive = false; _timer = 0; }

		private static bool SentryAlive((int index, int identity) s) =>
			s.index >= 0 && s.index < Main.maxProjectiles && Main.projectile[s.index].active && Main.projectile[s.index].identity == s.identity
			&& Main.projectile[s.index].type == ModContent.ProjectileType<LevySentryBanner>();

		public override void PostUpdateNPCs()
		{
			if (LevyUtil.IsClient) return;
			bool now = Active;
			var edge = EdgeOf(_wasActive, now);
			_wasActive = now;
			if (edge == Edge.Ended) { _called.Clear(); _timer = 0; return; }
			if (!now) return;
			if (edge == Edge.Started) _timer = LevyConstants.OoaMaintainTicks;
			if (++_timer < LevyConstants.OoaMaintainTicks) return;
			_timer = 0;
			Maintain();
		}

		private void Maintain()
		{
			var roster = RosterSystem.Instance;
			// 1. Forget levies that lost their body or whose sentry is gone (a gone sentry sends its levies home).
			var drop = new List<int>();
			foreach (var kv in _called)
			{
				var rec = roster.FindById(kv.Key);
				bool body = rec != null && rec.HasBody;
				if (!body) { drop.Add(kv.Key); continue; }
				if (!SentryAlive(kv.Value)) { Retire(rec); drop.Add(kv.Key); }
			}
			foreach (var id in drop) _called.Remove(id);

			// 2. Any other regular body in the field stands down.
			foreach (var s in new List<SoldierNPC>(roster.ActiveBodies()))
				if (s.Record != null && s.Record.Kind == SoldierKind.Regular && !_called.ContainsKey(s.Record.Id)) Retire(s.Record);

			// 3. Every sentry tops up to its tier.
			int bannerType = ModContent.ProjectileType<LevySentryBanner>();
			var calledSet = new HashSet<int>(_called.Keys);
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				var p = Main.projectile[i];
				if (!p.active || p.type != bannerType || p.owner < 0 || p.owner >= Main.maxPlayers || !Main.player[p.owner].active) continue;
				int tier = Math.Clamp((int)p.ai[0], 1, 3);
				int have = 0;
				foreach (var kv in _called) if (kv.Value.index == i && kv.Value.identity == p.identity) have++;
				int need = LeviesFor(tier) - have;
				if (need <= 0) continue;
				foreach (var rec in PickCallups(roster.Regulars, Main.player[p.owner].name, need, calledSet, r => r.HasBody))
				{
					Calling = true;
					int idx;
					try { idx = roster.SpawnBody(rec, p.Bottom); } finally { Calling = false; }
					if (idx < 0) break; // render cap or no NPC slot
					_called[rec.Id] = (i, p.identity);
					calledSet.Add(rec.Id);
				}
			}
		}

		/// <summary>Takes a regular's body off the field without touching its record (hp is kept).</summary>
		private static void Retire(SoldierRecord rec)
		{
			if (rec == null || !rec.HasBody) return;
			if (Main.npc[rec.BodyIndex].ModNPC is not SoldierNPC body) return;
			rec.CurrentHp = Math.Clamp(body.NPC.life, 1, Math.Max(1, rec.MaxHp));
			RosterSystem.RetireBody(body);
			rec.BodyIndex = -1;
		}

		/// <summary>The ground point under a cursor: first solid tile at or below it within the scan range. Null if none.</summary>
		public static Vector2? GroundBelow(Vector2 world)
		{
			int tx = (int)(world.X / 16f), ty = (int)(world.Y / 16f);
			for (int k = 0; k <= LevyConstants.OoaGroundScanTiles; k++)
			{
				int y = ty + k;
				if (!WorldGen.InWorld(tx, y, 5)) return null;
				Tile t = Main.tile[tx, y];
				if (t.HasTile && Main.tileSolid[t.TileType]) return new Vector2(tx * 16f + 8f, y * 16f);
			}
			return null;
		}
	}
}
