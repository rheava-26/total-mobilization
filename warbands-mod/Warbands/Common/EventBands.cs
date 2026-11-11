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
	/// Event bands: a band that joins a vanilla event and fills the gap the event lacks.
	///   Goblin Army   aerial skirmishers: rocket boots, hooks, spiky balls and the stage's guns; they hop to new high ground.
	///   Pirate Invasion  ranged boarders: the stage's guns and bows, cutlass-and-hook boarders; they rally on the Flying Dutchman.
	///   Sandstorm     mages: the stage's staves, a Sandstorm in a Bottle; desert vanity, Forbidden look from Hardmode.
	///   Slime Rain    a ninja clan: katanas, shurikens, spiky balls, hooks; blades dart in and out.
	/// The event sets the LOOK (vanity) and the weapon STYLE (a filter on the world stage's own table, <see cref="BandTypes"/>); the items are the ones a player could already have at that stage.
	/// A roster is built once per (event, stage): slot 0 is the captain, the rest are the members. Always hostile, one band at a time per event, gone when the event ends.
	/// </summary>
	public static class EventBands
	{
		public const int IndexBase = 100;
		private static readonly BandRoster[] _cache = new BandRoster[4 * Stage.Count];
		public static int Credited, Departed, Spawned, CaptainDrops, Kills, CaptainKills;
		/// <summary>Headless lab / self-check: pretend the event is on and a player qualifies (a dedicated server with nobody online runs no zone code).</summary>
		public static bool LabAlwaysQualify;

		public static string Name(EventKind k) => k switch { EventKind.Goblins => "Goblin Skirmishers", EventKind.Pirates => "Pirate Boarders", EventKind.Sandstorm => "Sandstorm Mages", EventKind.SlimeRain => "Ninja Clan", _ => "" };
		public static string IdOf(EventKind k) => "Event" + k;

		// ---------------------------------------------------------------- roster index (BandLevyNPC.TypeIdx) and cache
		public static int IndexOf(BandRoster r)
		{
			if (r == null || r.Event == EventKind.None) return -1;
			return IndexBase + ((int)r.Event - 1) * Stage.Count + Math.Clamp(r.Stage, 0, Stage.Count - 1);
		}

		public static BandRoster FromIndex(int i)
		{
			if (i < IndexBase || i >= IndexBase + 4 * Stage.Count) return null;
			int k = (i - IndexBase) / Stage.Count, s = (i - IndexBase) % Stage.Count;
			return Roster((EventKind)(k + 1), s);
		}

		public static BandRoster Roster(EventKind kind, int stage)
		{
			if (kind == EventKind.None) return null;
			stage = Math.Clamp(stage, 0, Stage.Count - 1);
			int slot = ((int)kind - 1) * Stage.Count + stage;
			return _cache[slot] ??= Build(kind, stage);
		}

		// ---------------------------------------------------------------- the stage table, filtered by weapon style
		private static Item Of(string name) { var it = new Item(); int t = BandData.ItemType(name); if (t > 0) it.SetDefaults(t); return it; }

		/// <summary>The world stage's own table: the members of every named band type of the highest stage at or below <paramref name="stage"/>.</summary>
		public static List<BandMember> Pool(int stage)
		{
			int best = -1;
			foreach (var t in BandTypes.All) if (t.Id != "CultistArchers" && t.Stage <= stage && t.Stage > best) best = t.Stage;
			var list = new List<BandMember>();
			foreach (var t in BandTypes.All) if (t.Id != "CultistArchers" && t.Stage == best) list.AddRange(t.Members);
			return list;
		}

		private static bool IsGun(BandMember m) => m.Role == 'R' && m.Ammo != null && Of(m.Weapon).useAmmo == AmmoID.Bullet;
		private static bool IsBow(BandMember m) => m.Role == 'R' && m.Ammo != null && Of(m.Weapon).useAmmo == AmmoID.Arrow;

		private static BandMember Copy(BandMember b, bool captain, string[] kit, string hook) => new BandMember(b.Role, b.Weapon, b.Ammo, b.Armor) { Captain = captain, Kit = kit, Hook = hook };
		private static BandMember Own(char role, string weapon, string ammo, string[] armor, bool captain, string[] kit, string hook) => new BandMember(role, weapon, ammo, armor) { Captain = captain, Kit = kit, Hook = hook };

		/// <summary>Deterministic shuffle: the same stage always gives the same band (the client and the server build the same roster).</summary>
		private static List<T> Shuffle<T>(IEnumerable<T> src, int seed)
		{
			var l = src.ToList(); var r = new Random(seed);
			for (int i = l.Count - 1; i > 0; i--) { int j = r.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
			return l;
		}

		private static BandRoster Build(EventKind kind, int stage)
		{
			var pool = Pool(stage); int seed = (int)kind * 101 + stage;
			var members = new List<BandMember>();
			var guns = Shuffle(pool.Where(IsGun), seed); var bows = Shuffle(pool.Where(IsBow), seed + 1);
			var shooters = Shuffle(pool.Where(m => m.Role == 'R'), seed + 2);
			var mages = Shuffle(pool.Where(m => m.Role == 'M'), seed + 3);
			var blades = Shuffle(pool.Where(m => m.Role == 'E'), seed + 4);
			var armors = Shuffle(pool.Select(m => m.Armor), seed + 5);
			string[] Arm(int i) => armors[i % armors.Count];
			BandMember G(int i) => (guns.Count > 0 ? guns : shooters.Count > 0 ? shooters : pool)[i % (guns.Count > 0 ? guns : shooters.Count > 0 ? shooters : pool).Count];
			BandMember B(int i) => (bows.Count > 0 ? bows : guns.Count > 0 ? guns : shooters.Count > 0 ? shooters : pool)[i % (bows.Count > 0 ? bows : guns.Count > 0 ? guns : shooters.Count > 0 ? shooters : pool).Count];
			var r = new BandRoster { Biome = BandBiome.Forest, Stage = stage, Event = kind, Id = IdOf(kind), Name = Name(kind), Flavor = null, Warns = false, AlwaysNeutral = false, Mats = new (string, int)[0], Bounty = null, BountyCopper = 0 };
			string[] none = new string[0];
			switch (kind)
			{
				case EventKind.Goblins:
					r.Title = "Skirmisher"; r.CaptainTitle = "Warchief"; r.Skirmish = true; r.Skin = new Color(112, 128, 176); r.TintR = 110; r.TintG = 130; r.TintB = 200;
					r.Vanity = new[] { "Goggles", null, null };
					for (int i = 0; i < 8; i++)
					{
						var kit = new[] { stage >= 1 ? "RocketBoots" : "HermesBoots" };
						if (i % 3 == 1) members.Add(Own('R', "SpikyBall", null, Arm(i), i == 0, kit, "GrapplingHook"));
						else members.Add(Copy(G(i), i == 0, kit, "GrapplingHook"));
					}
					break;
				case EventKind.Pirates:
					r.Title = "Boarder"; r.CaptainTitle = "Captain"; r.TintR = 200; r.TintG = 160; r.TintB = 60;
					r.Vanity = new[] { "PirateHat", "PirateShirt", "PiratePants" };
					for (int i = 0; i < 8; i++)
					{
						bool blade = i == 3 || i == 6;
						if (blade)
						{
							var hook = "GrapplingHook"; var kit = new[] { "HermesBoots" };
							if (stage >= 3 && stage <= 4) members.Add(Own('E', "Cutlass", null, Arm(i), false, kit, hook));
							else if (blades.Count > 0) members.Add(Copy(blades[i % blades.Count], false, kit, hook));
							else members.Add(Copy(G(i), false, kit, hook));
						}
						else
						{
							bool bow = i == 1 || i == 5;
							members.Add(Copy(bow ? B(i) : G(i), i == 0, new[] { "CloudinaBottle" }, null));
						}
					}
					break;
				case EventKind.Sandstorm:
					r.Title = "Dunecaster"; r.CaptainTitle = "Sandcaller"; r.Skin = null; r.TintR = 230; r.TintG = 200; r.TintB = 120;
					r.Vanity = stage >= 3 ? new[] { "AncientBattleArmorHat", "AncientBattleArmorShirt", "AncientBattleArmorPants" } : new[] { "AncientArmorHat", "AncientArmorShirt", "AncientArmorPants" };
					for (int i = 0; i < 8; i++)
					{
						var src = mages.Count > 0 ? mages[i % mages.Count] : G(i);
						members.Add(Copy(src, i == 0, stage >= 3 ? new[] { "SandstorminaBottle" } : none, null));
					}
					break;
				case EventKind.SlimeRain:
					r.Title = "Ninja"; r.CaptainTitle = "Sensei"; r.Skirmish = true; r.TintR = 70; r.TintG = 70; r.TintB = 100;
					r.Vanity = new[] { "NinjaHood", "NinjaShirt", "NinjaPants" };
					for (int i = 0; i < 8; i++)
					{
						var kit = stage >= 1 ? new[] { "HermesBoots" } : none; var hook = "GrapplingHook";
						if (i % 2 == 0)
						{
							if (stage <= 1) members.Add(Own('E', "Katana", null, Arm(i), i == 0, kit, hook));
							else members.Add(Copy(blades.Count > 0 ? blades[(i / 2) % blades.Count] : G(i), i == 0, kit, hook));
						}
						else members.Add(Own('R', i % 4 == 1 ? "Shuriken" : "SpikyBall", null, Arm(i), false, kit, hook));
					}
					break;
			}
			r.Members = members.ToArray();
			return r;
		}

		// ---------------------------------------------------------------- the captain's name
		private static readonly string[][] Epithets =
		{
			null,
			new[] { "Rockethead", "Hookhand", "Spikeback", "Sootfoot", "Bluefinger" },
			new[] { "One-Eye", "Saltbeard", "Hardtack", "Powder-Keg", "Tidewalker" },
			new[] { "Dune-Caller", "Dustveil", "Sunbleached", "Grainwalker", "Mirage" },
			new[] { "Quiet", "Gelfoot", "Stickyhand", "Slicksteps", "Dripping" },
		};
		public static string Epithet(EventKind k, Random rng) { var l = Epithets[(int)k]; return l == null ? "" : "\"" + l[rng.Next(l.Length)] + "\""; }

		// ---------------------------------------------------------------- loot (captain: the event's own drops; crew: a small vanity chance)
		public struct Drop { public string Item; public int Min, Max; public float Chance; public int MinStage; public Drop(string i, int min, int max, float c, int ms = 0) { Item = i; Min = min; Max = max; Chance = c; MinStage = ms; } }

		public static Drop[] CaptainLoot(EventKind k) => k switch
		{
			EventKind.Goblins => new[]
			{
				new Drop("SpikyBall", 20, 40, 1f), new Drop("Harpoon", 1, 1, 0.30f), new Drop("RocketBoots", 1, 1, 0.12f), new Drop("ShadowFlameBow", 1, 1, 0.10f), new Drop("ShadowFlameKnife", 1, 1, 0.10f),
				new Drop("ShadowFlameHexDoll", 1, 1, 0.10f), new Drop("GoblinTech", 1, 1, 0.10f), new Drop("Goggles", 1, 1, 0.35f),
			},
			EventKind.Pirates => new[]
			{
				new Drop("Cutlass", 1, 1, 0.25f), new Drop("CoinGun", 1, 1, 0.12f), new Drop("LuckyCoin", 1, 1, 0.12f), new Drop("DiscountCard", 1, 1, 0.12f), new Drop("GoldRing", 1, 1, 0.12f),
				new Drop("PirateStaff", 1, 1, 0.08f), new Drop("PirateHat", 1, 1, 0.40f), new Drop("PirateShirt", 1, 1, 0.40f), new Drop("PiratePants", 1, 1, 0.40f), new Drop("EyePatch", 1, 1, 0.40f),
			},
			EventKind.Sandstorm => new[]
			{
				new Drop("SandstorminaBottle", 1, 1, 0.15f, 3), new Drop("AncientArmorHat", 1, 1, 0.30f), new Drop("AncientArmorShirt", 1, 1, 0.30f), new Drop("AncientArmorPants", 1, 1, 0.30f),
				new Drop("AncientCloth", 2, 5, 0.50f, 3), new Drop("AncientBattleArmorHat", 1, 1, 0.20f, 3), new Drop("AncientBattleArmorShirt", 1, 1, 0.20f, 3), new Drop("AncientBattleArmorPants", 1, 1, 0.20f, 3),
				new Drop("FlyingCarpet", 1, 1, 0.10f, 3), new Drop("StormTigerStaff", 1, 1, 0.12f, 5),
			},
			EventKind.SlimeRain => new[]
			{
				new Drop("Shuriken", 30, 60, 1f), new Drop("SpikyBall", 10, 20, 1f), new Drop("Katana", 1, 1, 0.25f), new Drop("NinjaHood", 1, 1, 0.25f), new Drop("NinjaShirt", 1, 1, 0.25f),
				new Drop("NinjaPants", 1, 1, 0.25f), new Drop("SlimeStaff", 1, 1, 0.08f), new Drop("MasterNinjaGear", 1, 1, 0.05f, 5),
			},
			_ => new Drop[0],
		};

		public static Drop[] CrewLoot(EventKind k) => k switch
		{
			EventKind.Goblins => new[] { new Drop("SpikyBall", 3, 8, 0.20f), new Drop("Goggles", 1, 1, 0.04f) },
			EventKind.Pirates => new[] { new Drop("PirateHat", 1, 1, 0.05f), new Drop("PirateShirt", 1, 1, 0.05f), new Drop("PiratePants", 1, 1, 0.05f), new Drop("EyePatch", 1, 1, 0.05f) },
			EventKind.Sandstorm => new[] { new Drop("AncientArmorHat", 1, 1, 0.05f), new Drop("AncientArmorShirt", 1, 1, 0.05f), new Drop("AncientArmorPants", 1, 1, 0.05f) },
			EventKind.SlimeRain => new[] { new Drop("Shuriken", 4, 10, 0.25f), new Drop("NinjaHood", 1, 1, 0.05f), new Drop("NinjaShirt", 1, 1, 0.05f), new Drop("NinjaPants", 1, 1, 0.05f) },
			_ => new Drop[0],
		};

		/// <summary>What a drop table pays at <paramref name="stage"/> for one set of rolls: (item type, stack) for each entry that passes its stage gate and its chance.</summary>
		public static List<(int type, int stack)> RollDrops(Drop[] table, int stage, Random rng = null)
		{
			var got = new List<(int, int)>();
			foreach (var d in table)
			{
				if (d.MinStage > stage) continue;
				int t = BandData.ItemType(d.Item); if (t <= 0) continue;
				float roll = rng != null ? (float)rng.NextDouble() : Main.rand.NextFloat();
				if (roll >= d.Chance) continue;
				int stack = d.Min >= d.Max ? d.Min : (rng != null ? rng.Next(d.Min, d.Max + 1) : Main.rand.Next(d.Min, d.Max + 1));
				got.Add((t, Math.Max(1, stack)));
			}
			return got;
		}

		/// <summary>A member of an event band died: credit the invasion (Goblin Army and Pirate Invasion count the kill like their own invaders), then the loot.</summary>
		public static void OnMemberKilled(BandLevyNPC m, BandBrain band)
		{
			var kind = band.Type.Event;
			Kills++; if (m.IsCaptain) CaptainKills++;
			if (CreditProgress(kind, m.IsCaptain ? 2 : 1)) Credited += m.IsCaptain ? 2 : 1;
			var table = m.IsCaptain ? CaptainLoot(kind) : CrewLoot(kind);
			foreach (var (type, stack) in RollDrops(table, band.Stage))
				Item.NewItem(m.NPC.GetSource_Loot(), m.NPC.Hitbox, type, stack);
			if (m.IsCaptain) CaptainDrops++;
		}

		/// <summary>Takes <paramref name="slots"/> off the invasion's remaining size, as vanilla does when one of its own invaders dies (NPC.checkDead). False when the kind has no progress counter or its invasion is not the one running.</summary>
		public static bool CreditProgress(EventKind kind, int slots)
		{
			int inv = kind == EventKind.Goblins ? InvasionID.GoblinArmy : kind == EventKind.Pirates ? InvasionID.PirateInvasion : 0;
			if (inv == 0 || Main.invasionType != inv || Main.invasionSize <= 0) return false;
			Main.invasionSize = Math.Max(0, Main.invasionSize - slots);
			if (Main.netMode != NetmodeID.MultiplayerClient) Main.ReportInvasionProgress(Main.invasionSizeStart - Main.invasionSize, Main.invasionSizeStart, inv + 3, 0);
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(78, -1, -1, null, Main.invasionProgress, Main.invasionProgressMax, Main.invasionProgressIcon);
			return true;
		}

		// ---------------------------------------------------------------- events: is it on, may it have a band, does a player qualify
		public static bool Enabled(EventKind k)
		{
			var c = WarbandsConfig.Get; if (c == null || !c.Enabled || !c.EventBands || !c.LevyBrain) return false;
			return k switch { EventKind.Goblins => c.EventGoblins, EventKind.Pirates => c.EventPirates, EventKind.Sandstorm => c.EventSandstorm, EventKind.SlimeRain => c.EventSlimeRain, _ => false };
		}

		public static bool Active(EventKind k) => k switch
		{
			EventKind.Goblins => Main.invasionType == InvasionID.GoblinArmy && Main.invasionSize > 0,
			EventKind.Pirates => Main.invasionType == InvasionID.PirateInvasion && Main.invasionSize > 0,
			EventKind.Sandstorm => Terraria.GameContent.Events.Sandstorm.Happening,
			EventKind.SlimeRain => Main.slimeRain,
			_ => false,
		};

		/// <summary>A player a band of this event may form near: on the surface, and inside the event (the invasion's own spawn window of 3000 px around its front; in the sandstorm's desert; under the slime rain).</summary>
		public static bool Qualifies(EventKind k, Player p)
		{
			if (!p.active || p.dead) return false;
			if (LabAlwaysQualify) return true;
			bool surface = p.Center.Y / 16f < (float)Main.worldSurface;
			switch (k)
			{
				case EventKind.Goblins:
				case EventKind.Pirates: return surface && Main.invasionDelay == 0 && Math.Abs(p.Center.X - Main.invasionX * 16.0) < 3000.0;
				case EventKind.Sandstorm: return p.ZoneSandstorm && surface;
				case EventKind.SlimeRain: return surface && p.ZoneOverworldHeight;
			}
			return false;
		}

		/// <summary>The side of the player an invasion band forms on: toward the invasion's front when it is far off, else either.</summary>
		public static int SideFor(EventKind k, Player p)
		{
			if (k != EventKind.Goblins && k != EventKind.Pirates) return 0;
			double dx = Main.invasionX * 16.0 - p.Center.X;
			return Math.Abs(dx) > 40 * 16 ? (dx < 0 ? -1 : 1) : 0;
		}

		/// <summary>A footing for an event band: open sky above the standing spot, no house walls beside it and no town NPC within 30 tiles (so the band never forms in the player's base).</summary>
		public static bool OpenFooting(Point pt)
		{
			int x = pt.X, fy = pt.Y;
			for (int y = fy - 2; y >= Math.Max(10, fy - 45); y--)
			{
				Tile t = Main.tile[x, y];
				if (t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) return false;
			}
			for (int dx = -3; dx <= 3; dx++) if (Main.wallHouse[Main.tile[Math.Clamp(x + dx, 10, Main.maxTilesX - 10), fy - 2].WallType]) return false;
			float r2 = 30f * 16f * 30f * 16f; Vector2 c = new Vector2(x * 16f + 8f, fy * 16f);
			for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.townNPC && Vector2.DistanceSquared(n.Center, c) < r2) return false; }
			return true;
		}

		/// <summary>Forms the event's band near <paramref name="p"/>. True when it formed (see <see cref="BandSpawner.LastBrain"/>).</summary>
		public static bool TrySpawn(EventKind k, Player p, int members = 0)
		{
			if (members <= 0) members = Main.rand.Next(3, 6);
			var r = Roster(k, Stage.Current);
			bool invasion = k == EventKind.Goblins || k == EventKind.Pirates;
			bool ok = BandSpawner.SpawnBand(p, r, members, invasion ? 60 : 45, invasion ? 90 : 70, false, true, false, SideFor(k, p), true);
			if (!ok) return false;
			Spawned++;
			var br = BandSpawner.LastBrain;
			if (br != null) foreach (var n in Main.npc) if (n.active && n.ModNPC is BandLevyNPC b && b.BandId == br.Id && b.IsCaptain) { BandChatter.Say(b, "EVENT_ARRIVE", force: true); break; }
			return true;
		}
	}

	/// <summary>The event-band ticker (server and single player): forms a band while an event is on, removes it when the event ends, rallies the Flying Dutchman's boarders.</summary>
	public class EventBandSystem : ModSystem
	{
		private class State { public bool WasActive; public uint NextAt; public int Spawned; public int BandId; }
		private readonly State[] _st = { null, new State(), new State(), new State(), new State() };
		public const int SightTiles = 80;

		public override void OnWorldLoad() { for (int i = 1; i < _st.Length; i++) _st[i] = new State(); }

		public override void PostUpdateWorld()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			uint now = Main.GameUpdateCount; if ((now & 15) != 0) return;
			var cfg = WarbandsConfig.Get; if (cfg == null) return;
			for (int ki = 1; ki <= 4; ki++)
			{
				var kind = (EventKind)ki; var s = _st[ki];
				bool active = EventBands.Enabled(kind) && EventBands.Active(kind);
				if (!active) { if (s.WasActive) Mod.Logger.Info($"[Warbands] event {kind} over"); s.WasActive = false; s.BandId = 0; continue; }
				if (!s.WasActive) { Mod.Logger.Info($"[Warbands] event {kind} on (stage {Stage.Current})"); s.WasActive = true; s.Spawned = 0; s.NextAt = now + 60u * 10; s.BandId = 0; } // the first band comes ten seconds into the event
				if (s.BandId != 0)
				{
					if (BandBrain.All.ContainsKey(s.BandId)) continue; // one band at a time per event
					s.BandId = 0; s.NextAt = now + 60u * (uint)Math.Max(10, cfg.EventBandCooldownSeconds);
				}
				if (BandOut(kind) || s.Spawned >= cfg.EventBandMaxPerEvent || now < s.NextAt) continue;
				var who = new List<Player>();
				for (int i = 0; i < Main.maxPlayers; i++) if (EventBands.Qualifies(kind, Main.player[i])) who.Add(Main.player[i]);
				if (who.Count == 0) continue;
				var p = who[Main.rand.Next(who.Count)];
				if (EventBands.TrySpawn(kind, p) && BandSpawner.LastBrain != null) { s.BandId = BandSpawner.LastBrain.Id; s.Spawned++; }
				else { s.NextAt = now + 60u * 4; Mod.Logger.Info($"[Warbands] event {kind}: no band formed this try (no footing near the player)"); } // try again shortly
			}
			Tend(now);
		}

		private static bool BandOut(EventKind k)
		{
			foreach (var b in BandBrain.All.Values) if (b.Type != null && b.Type.Event == k) return true;
			return false;
		}

		/// <summary>Event bands in the world: leave when their event ends, are cleaned up when no player is near, follow the Flying Dutchman.</summary>
		public void Tend(uint now)
		{
			foreach (var b in BandBrain.All.Values)
			{
				if (!b.IsEvent) continue;
				var kind = b.Type.Event;
				bool active = EventBands.Enabled(kind) && EventBands.Active(kind);
				if (!active && !b.Leaving) { b.Leaving = true; b.LeaveTick = now; Announce(b); }
				if (b.Members.Count == 0) continue;
				if (b.FarTicks > 60 * 30) { foreach (var m in b.Members) { m.NPC.active = false; m.NPC.netUpdate = true; EventBands.Departed++; } b.Paid = true; continue; } // no player near for half a minute: gone
				if (b.Leaving)
				{
					foreach (var m in b.Members)
					{
						if (!m.NPC.active) continue;
						if (!NearAnyPlayer(m.NPC.Center, SightTiles)) { m.NPC.active = false; m.NPC.netUpdate = true; EventBands.Departed++; }
						else if (now - b.LeaveTick > 60 * 25) m.BeginRecall();
					}
					continue;
				}
				if (kind == EventKind.Pirates) Rally(b);
			}
		}

		private static void Announce(BandBrain b)
		{
			BandLevyNPC cap = null;
			foreach (var m in b.Members) { if (m.IsCaptain) { cap = m; break; } if (cap == null) cap = m; }
			if (cap != null) BandChatter.Say(cap, "EVENT_END", force: true);
		}

		private static bool NearAnyPlayer(Vector2 c, float tiles)
		{
			float r2 = tiles * 16f * tiles * 16f;
			for (int i = 0; i < Main.maxPlayers; i++) { var p = Main.player[i]; if (p.active && !p.dead && Vector2.DistanceSquared(p.Center, c) < r2) return true; }
			return false;
		}

		/// <summary>The Flying Dutchman's boarders gather under the ship: the band's anchor follows the ground beneath it.</summary>
		private static void Rally(BandBrain b)
		{
			NPC ship = null;
			for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.type == NPCID.PirateShip) { ship = n; break; } }
			b.Rally = ship != null;
			if (ship == null) return;
			int x = (int)(ship.Center.X / 16f), sy = (int)(ship.Center.Y / 16f);
			int fy = BandSpawner.FloorAt(Math.Clamp(x, 10, Main.maxTilesX - 10), sy + 12, 14, 70);
			if (fy >= 0) b.Anchor = new Vector2(x * 16f + 8f, fy * 16f - BandLevyNPC.BodyHeight * 0.5f);
		}
	}
}
