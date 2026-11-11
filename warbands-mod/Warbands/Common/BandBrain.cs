using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Targeting;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>The class of what a band is fighting, read from the player's hand (or a levy's weapon).</summary>
	public enum FoeClass : byte { Unknown, Melee, Gunner, Caster, Summoner }

	/// <summary>
	/// What a whole band shares: who is in it, who may attack this window (the attack token and the rotating aggro of ranged bands), whether it has been warned off, who fled and who fell, and the payout when it is beaten.
	/// Rebuilt from the live members every tick by <see cref="Update"/> (WarbandsSystem.PreUpdateNPCs), server and single player only.
	/// </summary>
	public class BandBrain
	{
		public static readonly Dictionary<int, BandBrain> All = new();
		private static int _nextId = 1;
		public static int NextId() => _nextId++;
		/// <summary>Ticks a band may stand with no player within 220 tiles before it is cleaned up (config minutes; 30 by default). Bands are rare enemies: nothing else despawns them.</summary>
		public static int FarCleanupTicks => 3600 * Math.Max(5, WarbandsConfig.Get?.BandCleanupMinutes ?? 30);
		public static int PeekNextId() => _nextId;
		public static BandBrain Get(int id) => All.TryGetValue(id, out var b) ? b : null;
		public static int Counted;           // bands formed this session
		public static int BountiesPaid;      // payouts handed out this session

		public int Id, Stage, StartSize;
		public BandRoster Type;
		public Vector2 Anchor;
		public readonly List<BandLevyNPC> Members = new();
		public string AggroWhy; public bool Aggro, Warns, Neutral, Provoked, WonSaid, LastSaid; public int WarnLevel; public uint WarnTick; public int StayTicks;
		public int Killed, Fled; public Vector2 LastDeathPos; public bool Paid; public uint Born;
		public int ActiveGroup, RetreatGroup = -1; public uint RetreatUntil, FlipAt;
		public uint LastOneTapTick, LastDeathTick; public int FarTicks, FarAggroless;
		public uint LastHitTick;
		public bool RaisedAlarm; public readonly HashSet<FoeClass> ClassRemarked = new();

		public static BandBrain Create(BandRoster type, int stage, int startSize, Vector2 anchor, int id = 0, bool neutral = false)
		{
			var b = new BandBrain { Id = id > 0 ? id : NextId(), Type = type, Stage = stage, StartSize = startSize, Anchor = anchor, Neutral = neutral, Warns = type.Warns && !neutral, Aggro = !type.Warns && !neutral, Born = Main.GameUpdateCount };
			All[b.Id] = b; Counted++;
			return b;
		}

		// ---------------------------------------------------------------- the attack token (at most EngageCap attackers per window) and the rotating aggro
		public bool TryToken(BandLevyNPC m)
		{
			uint now = Main.GameUpdateCount;
			if (!Aggro) return false;
			if (RetreatGroup >= 0 && m.Group == RetreatGroup && now < RetreatUntil && m.Role != 'E') return false; // the attacked side falls back and holds fire; the others shoot
			if (m.EngagedUntil > now) { m.EngagedUntil = now + BandStats.EngageWindowTicks; return true; }
			int engaged = 0;
			for (int i = 0; i < Members.Count; i++) if (Members[i].EngagedUntil > now) engaged++;
			if (engaged >= BandStats.EngageCap(Math.Max(StartSize, Members.Count))) return false;
			m.EngagedUntil = now + BandStats.EngageWindowTicks;
			return true;
		}

		/// <summary>A member took damage: the side it is on is the attacked side for a moment (ranged bands rotate their aggro).</summary>
		public void OnMemberHit(BandLevyNPC m, int damage)
		{
			LastHitTick = Main.GameUpdateCount;
			if (!Aggro) { Aggro = true; AggroWhy = "member hit"; if (Neutral) Provoked = true; Chat(m, Neutral ? "PROVOKED" : "WARNING_BROKEN", force: true); }
			if (m.Role == 'E' || damage < Math.Max(3, m.NPC.lifeMax / 40)) return;
			// the retreat rotation only makes sense when the other side has shooters to keep up the fire
			bool otherHasShooter = false;
			for (int i = 0; i < Members.Count; i++) if (Members[i].Group != m.Group && Members[i].Role != 'E') otherHasShooter = true;
			if (!otherHasShooter) return;
			RetreatGroup = m.Group; RetreatUntil = Main.GameUpdateCount + 150; ActiveGroup = 1 - m.Group; Rotations++;
		}
		public int Rotations;

		public void OnMemberDied(BandLevyNPC m, bool oneTap)
		{
			Killed++; LastDeathPos = m.NPC.Center; LastDeathTick = Main.GameUpdateCount; if (oneTap) { LastOneTapTick = Main.GameUpdateCount; OneTaps++; }
			if (!Aggro) { Aggro = true; if (Neutral) Provoked = true; }
			foreach (var o in Members)
			{
				if (o == m || !o.NPC.active) continue;
				o.Morale -= oneTap ? 30f : 12f;
			}
			var talker = PickTalker(m);
			if (talker != null) Chat(talker, oneTap ? "ONE_HIT" : "MATE_DIED");
		}
		public int OneTaps;

		public void OnMemberFled(BandLevyNPC m) { Fled++; }

		public BandLevyNPC PickTalker(BandLevyNPC except)
		{
			BandLevyNPC best = null; int seen = 0;
			foreach (var o in Members) { if (o == except || !o.NPC.active || o.NPC.life <= 0) continue; if (Main.rand.Next(++seen) == 0) best = o; }
			return best;
		}

		public void Chat(BandLevyNPC speaker, string key, Player who = null, string armor = null, int n = 0, bool force = false)
			=> BandChatter.Say(speaker, key, who?.name, armor, n, force: force);

		/// <summary>Distance in tiles from the band's centre to the nearest enemy of the band, and that enemy.</summary>
		public NPC Nearest(out float tiles)
		{
			tiles = float.MaxValue; if (Members.Count == 0) return null;
			Vector2 c = Vector2.Zero; foreach (var m in Members) c += m.NPC.Center; c /= Members.Count;
			NPC best = null; float bestSq = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i]; if (!n.active || !HostileSide.IsEnemy(n)) continue;
				float d = Vector2.DistanceSquared(c, n.Center); if (d < bestSq) { bestSq = d; best = n; }
			}
			if (best != null) tiles = (float)Math.Sqrt(bestSq) / 16f;
			return best;
		}

		/// <summary>The class of what the band is fighting: a player's held item and minions, or a levy's weapon.</summary>
		public static FoeClass ClassOf(NPC target)
		{
			if (target == null) return FoeClass.Unknown;
			if (target.ModNPC is PlayerStandIn si) return ClassOf(si.Who);
			if (target.ModNPC is Levy.Content.NPCs.SoldierNPC s && s.Weapon != null)
			{
				if (s.Weapon.IsMelee) return FoeClass.Melee;
				if (s.Weapon.Family == Levy.Common.WeaponFamily.Magic) return FoeClass.Caster;
				return FoeClass.Gunner;
			}
			return FoeClass.Unknown;
		}

		public static FoeClass ClassOf(Player p)
		{
			if (p == null || !p.active) return FoeClass.Unknown;
			if (p.numMinions > 0 || p.maxMinions > 1 && p.slotsMinions > 0f) return FoeClass.Summoner;
			Item it = p.HeldItem;
			if (it == null || it.IsAir) return FoeClass.Unknown;
			if (it.CountsAsClass(DamageClass.Summon)) return FoeClass.Summoner;
			if (it.CountsAsClass(DamageClass.Magic)) return FoeClass.Caster;
			if (it.CountsAsClass(DamageClass.Ranged) || it.CountsAsClass(DamageClass.Throwing)) return FoeClass.Gunner;
			if (it.CountsAsClass(DamageClass.Melee)) return FoeClass.Melee;
			return FoeClass.Unknown;
		}

		// ---------------------------------------------------------------- per tick
		public static void Update()
		{
			if (All.Count == 0) return;
			foreach (var b in All.Values) b.Members.Clear();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is BandLevyNPC m && All.TryGetValue(m.BandId, out var b)) b.Members.Add(m);
			}
			List<int> done = null;
			foreach (var b in All.Values)
			{
				if (b.Members.Count == 0 && Main.GameUpdateCount - b.Born > 5) { (done ??= new()).Add(b.Id); b.Finish(); continue; }
				b.Think();
			}
			if (done != null) foreach (int id in done) All.Remove(id);
		}

		private void Think()
		{
			uint now = Main.GameUpdateCount;
			if (Members.Count == 0) return;
			// despawn after a long time with no player anywhere near
			bool near = false; Vector2 c = Members[0].NPC.Center;
			for (int i = 0; i < Main.maxPlayers; i++) { var p = Main.player[i]; if (p.active && !p.dead && Vector2.DistanceSquared(p.Center, c) < 220f * 16f * 220f * 16f) { near = true; break; } }
			FarTicks = near ? 0 : FarTicks + 1;
			if (FarTicks > FarCleanupTicks) { foreach (var m in Members) { m.NPC.active = false; m.NPC.netUpdate = true; } Paid = true; return; } // the only despawn: a band left alone for a very long time
			if (now >= FlipAt) { FlipAt = now + 60 * 6; if (RetreatGroup < 0) ActiveGroup = 1 - ActiveGroup; }
			if (RetreatGroup >= 0 && now >= RetreatUntil) RetreatGroup = -1;

			NPC foe = Nearest(out float tiles);
			// a player who has just walked in: remark on what they wear and what they brought
			if (foe != null && tiles < 48f && !RaisedAlarm)
			{
				RaisedAlarm = true;
				var talker = Members[Main.rand.Next(Members.Count)];
				Player who = foe.ModNPC is PlayerStandIn si ? si.Who : null;
				string armor = BandChatter.ArmorSetName(who);
				int levies = CountLevies();
				if (Neutral && !Provoked) Chat(talker, "NEUTRAL", who, force: true);
				else if (Type.Warns && !Aggro) { }
				else if (levies >= 6) Chat(talker, "SPOTTED_LEVIES", who, armor, levies);
				else if (armor != null && who != null && who.statDefense >= 40) Chat(talker, "SPOTTED_ARMOR", who, armor);
				else Chat(talker, "SPOTTED", who);
			}
			if (Warns && !Aggro) WarnTick_(foe, tiles, now);
			if (Aggro && !WonSaid)
			{
				for (int i = 0; i < Main.maxPlayers; i++) { var p = Main.player[i]; if (p.active && p.dead && Vector2.DistanceSquared(p.Center, c) < 70f * 16f * 70f * 16f) { WonSaid = true; Chat(Members[Main.rand.Next(Members.Count)], "VICTORY", p, force: true); break; } }
			}
			if (Aggro && !LastSaid && Members.Count == 1 && Killed >= 2) { LastSaid = true; Chat(Members[0], "DEFEAT", force: true); }
		}

		private void WarnTick_(NPC foe, float tiles, uint now)
		{
			if (foe == null) return;
			var talker = Members[0];
			Player who = foe.ModNPC is PlayerStandIn si ? si.Who : null;
			if (tiles < 55f && WarnLevel == 0) { WarnLevel = 1; WarnTick = now; BandChatter.Say(talker, "WARNING", force: true); }
			else if (tiles < 34f && WarnLevel == 1 && now - WarnTick > 150) { WarnLevel = 2; WarnTick = now; BandChatter.Say(talker, "WARNING_FINAL", who?.name, force: true); }
			if (WarnLevel >= 1 && tiles < 30f) StayTicks++; else StayTicks = Math.Max(0, StayTicks - 2);
			bool hurt = false; foreach (var m in Members) if (m.NPC.life < m.NPC.lifeMax) hurt = true;
			float closest = float.MaxValue; foreach (var m in Members) closest = Math.Min(closest, Vector2.Distance(m.NPC.Center, foe.Center) / 16f);
			// the band spreads out around the player while it warns, so the trigger is the nearest member, not the middle of the band
			if (closest < 11f || StayTicks > 60 * 9 || hurt) { Aggro = true; AggroWhy = $"closest {closest:0.0} stay {StayTicks} hurt {hurt}"; BandChatter.Say(talker, "WARNING_BROKEN", who?.name, force: true); }
		}

		public static int CountLevies()
		{
			int c = 0;
			for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.ModNPC is Levy.Content.NPCs.SoldierNPC s && !s.Hostile && s.Record != null && s.IsAlive && !s.OfficerMode) c++; }
			return c;
		}

		/// <summary>The band is over (every member dead, fled or despawned): pay the bounty for those beaten.</summary>
		private void Finish()
		{
			if (Paid || Killed == 0 || Type?.Bounty == null || Neutral) return; // a neutral band is no outlaw band: nothing to claim
			Paid = true; BountiesPaid++;
			int total = Math.Max(1, StartSize);
			float share = Math.Min(1f, Killed / (float)total);
			var src = new EntitySource_Misc("Warbands:Bounty");
			int idx = global::Warbands.Common.BandTypes.IndexOf(Type);
			int it = Item.NewItem(src, LastDeathPos, ModContent.ItemType<global::Warbands.Content.Items.BountyNotice>(), 1, true);
			if (it >= 0 && it < Main.maxItems && Main.item[it].ModItem is global::Warbands.Content.Items.BountyNotice bn)
			{
				bn.SetBand(idx, Math.Max(1, (int)Math.Round(share * 100f)));
				if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncItem, -1, -1, null, it);
			}
			string line = Terraria.Localization.Language.GetTextValue("Mods.Warbands.Chat.Beaten", Type.DisplayName);
			if (Main.netMode == NetmodeID.Server) Terraria.Chat.ChatHelper.BroadcastChatMessage(Terraria.Localization.NetworkText.FromLiteral(line), new Color(230, 190, 90));
			else Main.NewText(line, 230, 190, 90);
		}

		public static void Reset() { All.Clear(); Counted = 0; BountiesPaid = 0; }
	}
}
