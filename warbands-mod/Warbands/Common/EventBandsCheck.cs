using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Weapons;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>Warbands 0.4.0 self-check cases for event bands (called from WarbandsSelfCheck): roster data (look, style filter, stage), always hostile, progress credit, captain drops, leaving when the event ends.</summary>
	public static class EventBandsCheck
	{
		public static void Run(Action<string, bool, string> Check, bool tiers)
		{
			void Chk(string name, bool ok, string detail = "") => Check(name, ok, detail);
			var c = WarbandsConfig.Get;
			Chk("event band config: master and four per-event switches on, 90 second cooldown, 4 bands per event", c != null && c.EventBands && c.EventGoblins && c.EventPirates && c.EventSandstorm && c.EventSlimeRain && c.EventBandCooldownSeconds == 90 && c.EventBandMaxPerEvent == 4);

			var kinds = new[] { EventKind.Goblins, EventKind.Pirates, EventKind.Sandstorm, EventKind.SlimeRain };
			var signature = new HashSet<string> { "SpikyBall", "Shuriken", "Katana", "Cutlass" };
			var bad = new List<string>(); int members = 0;
			foreach (var k in kinds)
				for (int s = 0; s < Stage.Count; s++)
				{
					var r = EventBands.Roster(k, s);
					if (r == null || r.Event != k || r.Stage != s || r.Members.Length != 8) { bad.Add($"{k}@{s} roster"); continue; }
					if (EventBands.FromIndex(EventBands.IndexOf(r)) != r || BandTypes.FromIndex(BandTypes.IndexOf(r)) != r) bad.Add($"{k}@{s} index");
					if (r.Bounty != null || r.Warns || r.AlwaysNeutral) bad.Add($"{k}@{s} flags");
					if (r.Members.Count(m => m.Captain) != 1 || !r.Members[0].Captain || string.IsNullOrEmpty(r.CaptainTitle) || string.IsNullOrEmpty(r.Title)) bad.Add($"{k}@{s} captain");
					var pool = EventBands.Pool(s); var poolWeapons = new HashSet<string>(pool.Select(m => m.Weapon));
					for (int i = 0; i < r.Members.Length; i++)
					{
						var m = r.Members[i]; members++;
						int w = BandData.ItemType(m.Weapon); if (w <= 0) { bad.Add($"{k}@{s} weapon {m.Weapon}"); continue; }
						var wi = new Item(); wi.SetDefaults(w);
						var fam = WeaponClassifier.Classify(wi);
						if (fam == Levy.Common.WeaponFamily.Unsupported || fam == Levy.Common.WeaponFamily.None) bad.Add($"{k}@{s} {m.Weapon} is {fam} (a band could not fire it)");
						// the item comes from the stage's own table, or is one of the event's signature items
						if (!poolWeapons.Contains(m.Weapon) && !signature.Contains(m.Weapon)) bad.Add($"{k}@{s} {m.Weapon} is not in the stage table");
						if (m.Weapon == "StormTigerStaff") bad.Add($"{k}@{s} carries the Desert Tiger Staff");
						if (tiers && ProgressionTier.Of(w) > Stage.ItemCeiling[s]) bad.Add($"{k}@{s} {m.Weapon} tier {ProgressionTier.Of(w):0.#}");
						if (m.Ammo != null) { int a = BandData.ItemType(m.Ammo); var ai = new Item(); if (a > 0) ai.SetDefaults(a); if (a <= 0 || ai.ammo != wi.useAmmo) bad.Add($"{k}@{s} ammo {m.Ammo}"); }
						// weapon style filter
						bool styleOk = k switch
						{
							EventKind.Goblins => m.Weapon == "SpikyBall" || (m.Role == 'R' && poolWeapons.Contains(m.Weapon)),
							EventKind.Pirates => (m.Role == 'R' && poolWeapons.Contains(m.Weapon)) || (m.Role == 'E' && (m.Weapon == "Cutlass" || poolWeapons.Contains(m.Weapon))),
							EventKind.Sandstorm => (m.Role == 'M' && poolWeapons.Contains(m.Weapon)) || !pool.Any(x => x.Role == 'M'),
							EventKind.SlimeRain => m.Weapon == "Shuriken" || m.Weapon == "SpikyBall" || m.Weapon == "Katana" || (m.Role == 'E' && poolWeapons.Contains(m.Weapon)),
							_ => false,
						};
						if (!styleOk) bad.Add($"{k}@{s} slot {i} {m.Weapon} breaks the style");
						if (m.Role == 'M' && k != EventKind.Sandstorm) bad.Add($"{k}@{s} unexpected mage");
						if (m.Armor == null || m.Armor.Length != 3) bad.Add($"{k}@{s} armor");
						// mobility kit: resolves, within the stage's tier ceiling
						var kit = new List<string>(); if (m.Kit != null) kit.AddRange(m.Kit); if (m.Hook != null) kit.Add(m.Hook);
						foreach (var name in kit)
						{
							int ti = BandData.ItemType(name); if (ti <= 0) { bad.Add($"{k}@{s} kit {name}"); continue; }
							if (tiers && ProgressionTier.Of(ti) > Stage.ItemCeiling[s]) bad.Add($"{k}@{s} kit {name} tier {ProgressionTier.Of(ti):0.#}");
						}
					}
					// the look: vanity resolves and fits its slot
					if (r.Vanity == null || r.Vanity.Length != 3 || r.Vanity.All(v => v == null)) bad.Add($"{k}@{s} no look");
					else for (int i = 0; i < 3; i++)
					{
						if (r.Vanity[i] == null) continue;
						int vt = BandData.ItemType(r.Vanity[i]); if (vt <= 0) { bad.Add($"{k}@{s} vanity {r.Vanity[i]}"); continue; }
						var vi = new Item(); vi.SetDefaults(vt);
						if (!(i == 0 ? vi.headSlot >= 0 : i == 1 ? vi.bodySlot >= 0 : vi.legSlot >= 0)) bad.Add($"{k}@{s} vanity {r.Vanity[i]} slot");
					}
				}
			Chk($"event rosters: 4 events x 7 stages ({members} slots) have a captain in slot 0, resolve, are fireable by Levy's brain, come from the stage table (or the event's signature items), match the weapon style, and stay within the stage tier ceiling", bad.Count == 0, string.Join("; ", bad.Distinct().Take(14)));

			// the look per event and stage
			{
				var g = EventBands.Roster(EventKind.Goblins, 1); var pi = EventBands.Roster(EventKind.Pirates, 3); var nj = EventBands.Roster(EventKind.SlimeRain, 0);
				var s0 = EventBands.Roster(EventKind.Sandstorm, 0); var s3 = EventBands.Roster(EventKind.Sandstorm, 3); var s5 = EventBands.Roster(EventKind.Sandstorm, 5);
				Chk("look: goblins wear goggles and blue skin, pirates the pirate set, ninjas the ninja set, sandstorm mages desert vanity before Hardmode and the Forbidden set after",
					g.Vanity[0] == "Goggles" && g.Skin.HasValue && pi.Vanity[0] == "PirateHat" && pi.Vanity[2] == "PiratePants" && nj.Vanity[0] == "NinjaHood" && nj.Vanity[1] == "NinjaShirt"
					&& s0.Vanity[0] == "AncientArmorHat" && s3.Vanity[0] == "AncientBattleArmorHat" && s5.Vanity[2] == "AncientBattleArmorPants");
			}

			// stage rule for loot: the Desert Tiger Staff only after Plantera; every gated drop's tier fits its gate stage
			{
				var table = EventBands.CaptainLoot(EventKind.Sandstorm); var badL = new List<string>();
				var rng = new Random(5); int tiger4 = 0, tiger5 = 0;
				int tiger = BandData.ItemType("StormTigerStaff");
				for (int i = 0; i < 4000; i++) foreach (var (t, _) in EventBands.RollDrops(table, 4, rng)) if (t == tiger) tiger4++;
				for (int i = 0; i < 4000; i++) foreach (var (t, _) in EventBands.RollDrops(table, 5, rng)) if (t == tiger) tiger5++;
				Chk("no Desert Tiger Staff before Plantera: 0 in 4000 captain rolls at the post-Mechs stage, some after Plantera", tiger > 0 && tiger4 == 0 && tiger5 > 100, $"stage4={tiger4} stage5={tiger5}");
				foreach (var k in kinds)
					foreach (var d in EventBands.CaptainLoot(k).Concat(EventBands.CrewLoot(k)))
					{
						int t = BandData.ItemType(d.Item); if (t <= 0) { badL.Add(k + " drop " + d.Item); continue; }
						if (d.Chance <= 0f || d.Chance > 1f || d.Min < 1 || d.Max < d.Min) badL.Add(k + " odds " + d.Item);
						if (tiers && d.MinStage > 0 && ProgressionTier.Of(t) > Stage.ItemCeiling[d.MinStage] + 3f) badL.Add($"{k} {d.Item} gated at {d.MinStage} but tier {ProgressionTier.Of(t):0.#}");
					}
				Chk("event drop tables: every item resolves, odds are sane, stage-gated items sit within their gate's tier", badL.Count == 0, string.Join("; ", badL));
			}

			// dialogue
			{
				var badD = new List<string>();
				foreach (var k in kinds) foreach (var key in new[] { "EVENT_ARRIVE", "EVENT_END", "SPOTTED", "HURT", "PANIC", "MATE_DIED", "VICTORY" })
					if (!BandChatter.Lines.TryGetValue(key + " " + EventBands.IdOf(k), out var ls) || ls.Count == 0) badD.Add(key + " " + k);
				if (!BandChatter.Lines.ContainsKey("EVENT_ARRIVE") || !BandChatter.Lines.ContainsKey("EVENT_END")) badD.Add("generic event lines");
				Chk("Dialogue.txt: every event has arrival, ending, spotted, hurt, panic, mate-down and victory lines", badD.Count == 0, string.Join(",", badD));
			}

			// live: one band per event: hostile whatever the neutral percent, a captain, progress credit, leaves when the event ends
			float keepNeutral = c.NeutralBandPercent; c.NeutralBandPercent = 100f;
			int inv0 = Main.invasionType, invSize0 = Main.invasionSize, invStart0 = Main.invasionSizeStart; double invX0 = Main.invasionX; bool slime0 = Main.slimeRain; bool sand0 = Terraria.GameContent.Events.Sandstorm.Happening;
			EventBands.LabAlwaysQualify = true;
			try
			{
				foreach (var k in kinds)
				{
					if (k == EventKind.Goblins) { Main.invasionType = InvasionID.GoblinArmy; Main.invasionSize = Main.invasionSizeStart = 80; }
					else if (k == EventKind.Pirates) { Main.invasionType = InvasionID.PirateInvasion; Main.invasionSize = Main.invasionSizeStart = 120; }
					else if (k == EventKind.Sandstorm) Terraria.GameContent.Events.Sandstorm.Happening = true;
					else Main.slimeRain = true;
					Chk($"{k}: the event reads as active", EventBands.Active(k));
					bool formed = false; BandBrain brain = null; var live = new List<BandLevyNPC>();
					var rng = new Random(900 + (int)k);
					for (int a = 0; a < 60 && !formed; a++)
					{
						int x = rng.Next(200, Main.maxTilesX - 200); int fy = BandSpawner.FloorAt(x, (int)Main.worldSurface - 20, 120, 160); if (fy < 0) continue;
						var p = new Player(); p.width = 20; p.height = 42; p.position = new Microsoft.Xna.Framework.Vector2(x * 16f - 10f, fy * 16f - 42f); p.active = true;
						Main.invasionX = x;
						formed = EventBands.TrySpawn(k, p, 4); brain = BandSpawner.LastBrain;
					}
					if (!formed || brain == null) { Chk($"{k}: a band forms", false, "no footing found in 60 tries"); continue; }
					for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.ModNPC is BandLevyNPC l && l.BandId == brain.Id) live.Add(l); }
					brain.Members.Clear(); brain.Members.AddRange(live);
					var cap = live.FirstOrDefault(l => l.IsCaptain);
					var crew = live.Where(l => !l.IsCaptain).ToList();
					bool hostile = !brain.Neutral && brain.Aggro && !brain.Warns && !brain.Type.Warns && live.All(l => !l.HostilePassive);
					Chk($"{k}: a band of {live.Count} forms (captain + {crew.Count}), always hostile with the neutral percent at 100", live.Count >= 3 && live.Count <= 8 && cap != null && crew.Count >= 2 && hostile && brain.IsEvent, $"neutral={brain.Neutral} aggro={brain.Aggro} warns={brain.Warns}");
					bool capOk = cap != null && cap.NPC.GivenName.StartsWith(brain.Type.CaptainTitle) && cap.NPC.GivenName.Contains('"') && crew.All(l => !l.NPC.GivenName.StartsWith(brain.Type.CaptainTitle) && l.NPC.GivenName.StartsWith(brain.Type.Title))
						&& cap.NPC.lifeMax >= (int)(BandStats.Life(brain.Stage, cap.Role) * BandLevyNPC.CaptainLifeMult) - 1 && crew.All(l => l.NPC.lifeMax == BandStats.Life(brain.Stage, l.Role));
					Chk($"{k}: the captain has its own title and name and {BandLevyNPC.CaptainLifeMult:0.#}x the life; members keep the normal band life", capOk, cap == null ? "no captain" : $"{cap.NPC.GivenName} life {cap.NPC.lifeMax} (member base {BandStats.Life(brain.Stage, cap.Role)})");

					if (k == EventKind.Goblins || k == EventKind.Pirates)
					{
						int before = Main.invasionSize; int cr0 = EventBands.Credited;
						crew[0].NPC.StrikeInstantKill();
						int afterOne = Main.invasionSize;
						cap.NPC.StrikeInstantKill();
						Chk($"{k}: killing a member takes 1 off the invasion and the captain 2 (size {before} to {afterOne} to {Main.invasionSize})", afterOne == before - 1 && Main.invasionSize == before - 3 && EventBands.Credited == cr0 + 3 && Main.invasionProgress == Main.invasionSizeStart - Main.invasionSize, $"progress {Main.invasionProgress}/{Main.invasionProgressMax}");
					}
					else
					{
						int before = Main.invasionSize; crew[0].NPC.StrikeInstantKill();
						Chk($"{k}: no invasion counter, killing a member changes no invasion size", Main.invasionSize == before);
						cap.NPC.StrikeInstantKill();
					}

					// the event ends: the band leaves (no player near, so it is off screen and removed at once)
					var left = live.Where(l => l.NPC.active).ToList();
					if (k == EventKind.Goblins || k == EventKind.Pirates) Main.invasionType = 0; else if (k == EventKind.Sandstorm) Terraria.GameContent.Events.Sandstorm.Happening = false; else Main.slimeRain = false;
					int dep0 = EventBands.Departed;
					ModContent.GetInstance<EventBandSystem>().Tend(Main.GameUpdateCount);
					bool allGone = left.All(l => !l.NPC.active);
					Chk($"{k}: when the event ends the band leaves ({left.Count} members removed off screen), flagged Leaving", brain.Leaving && allGone && EventBands.Departed == dep0 + left.Count, $"leaving={brain.Leaving} gone={allGone}");
					foreach (var l in live) l.NPC.active = false;
					BandBrain.All.Remove(brain.Id);
				}
			}
			finally
			{
				EventBands.LabAlwaysQualify = false; c.NeutralBandPercent = keepNeutral;
				Main.invasionType = inv0; Main.invasionSize = invSize0; Main.invasionSizeStart = invStart0; Main.invasionX = invX0; Main.slimeRain = slime0; Terraria.GameContent.Events.Sandstorm.Happening = sand0;
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is BandLevyNPC) Main.npc[i].active = false;
				BandBrain.All.Clear();
			}
		}
	}
}
