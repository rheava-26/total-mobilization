using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Potions;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	public partial class BossBugsWorldCheck
	{
		// =====================================================================================================
		// LOST BET: a Spectator whose whole army dies with no manpower left, in a boss fight, dies too
		// =====================================================================================================
		private const int BetBarracksId = 900101;
		private static int _betKills; private static string _betText; private static bool _betHooked;

		private static void BetKillMeDetour(On_Player.orig_KillMe orig, Player self, PlayerDeathReason reason, double dmg, int hitDirection, bool pvp)
		{
			if (self.whoAmI != 0 || self.name != Commander) { orig(self, reason, dmg, hitDirection, pvp); return; }
			_betKills++; _betText = reason.GetDeathText(self.name).ToString(); // recorded, not carried out: the stand-in commander is not killed
		}

		/// <summary>One world tick with the stand-in commander answering as the local player, so the Spectator's own once-a-tick check is what runs.</summary>
		private static void BetStep(int n)
		{
			try { SpectatorPlayer.BetHarness = true; Step(n); } finally { SpectatorPlayer.BetHarness = false; }
		}

		private static void LostBet()
		{
			Arena(FloorY, Half);
			SetRevenge(false, false); Main.GameMode = GameModeID.Expert; Main.hardMode = true;
			global::Levy.Common.Boss.BossDownedSystem.ClearForTest();
			global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(NPCID.KingSlime, true); // a beaten boss: a Spectator may stand in its fight
			SetBossesTarget(true);
			On_Player.KillMe += BetKillMeDetour; _betHooked = true;
			var bar = TestTileEntities.Add<BarracksTE>(BetBarracksId, new Point16(Cx - 30, FloorY - 3)); bar.TownName = "BetCheckTown";
			SaveHoldCheck.ProbeMidCheck("BossBugs bet barracks");
			Player me = Main.player[0];
			int bt = ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>();
			try
			{
				// (name, spectator, boss, manpower left, expected death)
				var cases = new (string name, bool spec, bool boss, int manpower, bool dies)[]
				{
					("Spectator, boss, army wiped, no manpower", true, true, 0, true),
					("Spectator, boss, army wiped, manpower left", true, true, 12, false),
					("no Spectator, boss, army wiped, no manpower", false, true, 0, false),
					("Spectator, no boss, army wiped, no manpower", true, false, 0, false),
				};
				var seen = new HashSet<string>();
				foreach (var c in cases)
				{
					for (int rep = 0; rep < (c.dies ? 10 : 1); rep++)
					{
						NewBetScene(bar, c.manpower);
						_spectator = c.spec; if (!c.spec && me.HasBuff(bt)) me.ClearBuff(bt);
						PlaceCommander(-20);
						var levies = Levies(4, 3f, 10f); foreach (var l in levies) l.Record.HomeBarracksId = BetBarracksId;
						Step(4);
						NPC boss = c.boss ? SpawnNpc(NPCID.KingSlime, new Vector2((Cx + 190) * 16f, (_floorY - 30) * 16f)) : null;
						if (c.boss && boss != null) _perTick = () => { if (boss.active) { boss.life = boss.lifeMax; boss.timeLeft = 99999; } };
						BetStep(30);
						if (rep == 0) Check($"({c.name}) four levies stand on the field before the wipe", levies.Count == 4 && levies.All(l => l.NPC.active && l.IsAlive));
						int kills0 = _betKills; _betText = null;
						WipeArmy(levies);
						Check($"({c.name}) the army is down and the pool lost one man per levy (pool {bar.Manpower})", levies.All(l => !l.NPC.active || !l.IsAlive) && bar.Manpower == Math.Max(0, c.manpower - 4));
						BetStep(3);
						_perTick = null;
						bool died = _betKills > kills0;
						if (rep == 0) Check($"({c.name}) {(c.dies ? "the player dies" : "no death")}", died == c.dies);
						else if (died != c.dies) Check($"({c.name}) repeat {rep}: {(c.dies ? "the player dies" : "no death")}", false);
						if (died) seen.Add(_betText);
						if (died && c.dies) Check($"({c.name}) the death message is one of the five lost-bet lines: \"{_betText}\"", Enumerable.Range(0, SpectatorBet.MessageCount).Any(k => SpectatorBet.Message(k, Commander) == _betText));
						if (boss != null) boss.active = false;
					}
				}
				Check($"over ten lost bets the random pick used {seen.Count} of the five lines", seen.Count >= 2);
				Check("each of the five lines is real text with the player's name filled in", Enumerable.Range(0, SpectatorBet.MessageCount).All(k => { string m = SpectatorBet.Message(k, "Eve"); return m.Contains("Eve") && !m.Contains("Mods.Levy") && !m.Contains("{0}"); }));

				// ---- the Spectator dropped after the wipe must not dodge it, and one dropped before it does not owe it
				foreach (bool dropAfterWipe in new[] { true, false })
				{
					NewBetScene(bar, 0);
					_spectator = true; PlaceCommander(-20);
					var levies = Levies(4, 3f, 10f); foreach (var l in levies) l.Record.HomeBarracksId = BetBarracksId;
					Step(4);
					NPC boss = SpawnNpc(NPCID.KingSlime, new Vector2((Cx + 190) * 16f, (_floorY - 30) * 16f));
					_perTick = () => { if (boss != null && boss.active) { boss.life = boss.lifeMax; boss.timeLeft = 99999; } };
					BetStep(30);
					int kills0 = _betKills;
					string who = dropAfterWipe ? "the Spectator is cancelled right after the wipe" : "the Spectator runs out before the wipe";
					if (dropAfterWipe) { WipeArmy(levies); _spectator = false; me.ClearBuff(bt); BetStep(3); Check($"({who}) the death still fires", _betKills > kills0); }
					else { _spectator = false; int ix = me.FindBuffIndex(bt); if (ix >= 0) me.buffTime[ix] = 1; BetStep(1); me.ClearBuff(bt); BetStep(3); WipeArmy(levies); BetStep(3); Check($"({who}) no death", _betKills == kills0); } // (a cancel during the fight is a forfeit: see Forfeit())
					_perTick = null; if (boss != null) boss.active = false;
				}

				// ---- an army that was never fielded is no lost bet
				{
					NewBetScene(bar, 0);
					_spectator = true; PlaceCommander(-20);
					Step(4);
					NPC boss = SpawnNpc(NPCID.KingSlime, new Vector2((Cx + 190) * 16f, (_floorY - 30) * 16f));
					int kills0 = _betKills;
					BetStep(60);
					Check("(Spectator, boss, no levy was ever fielded) no death", _betKills == kills0);
					if (boss != null) boss.active = false;
				}

				// ---- a boss that leaves is a lost bet, unless it was killed, it left for want of a target, no army was ever fielded, or it was back within the grace
				{
					int Grace = SpectatorBet.LeaveGraceTicks + 20;
					var leaves = new (string name, int levies, Action<NPC> end, int wait, bool dies)[]
					{
						("the boss despawns with the army standing", 4, b => b.active = false, Grace, true),
						("the boss is killed", 4, b => b.StrikeInstantKill(), Grace, false),
						("the boss flees from a spectator it could not see", 4, b => { SpectatorBet.NoteBossBlind(); b.active = false; }, Grace, false),
						("the boss despawns and no army was ever fielded", 0, b => b.active = false, Grace, false),
						("the boss is gone for a second only", 4, null, Grace, false),
					};
					foreach (var c in leaves)
					{
						NewBetScene(bar, 12);
						me.GetModPlayer<SpectatorPlayer>().OnEnterWorld(); // a new world: no fight carries over from the case before
						_spectator = true; PlaceCommander(-20);
						var levies = Levies(c.levies, 3f, 10f); foreach (var l in levies) l.Record.HomeBarracksId = BetBarracksId;
						Step(4);
						NPC boss = SpawnNpc(NPCID.KingSlime, new Vector2((Cx + 190) * 16f, (_floorY - 30) * 16f));
						_perTick = () => { if (boss != null && boss.active) { boss.life = boss.lifeMax; boss.timeLeft = 99999; } };
						BetStep(30);
						int kills0 = _betKills; _betText = null; _perTick = null;
						if (c.end != null) c.end(boss);
						else { boss.active = false; BetStep(60); boss = SpawnNpc(NPCID.KingSlime, new Vector2((Cx + 190) * 16f, (_floorY - 30) * 16f)); _perTick = () => { if (boss != null && boss.active) { boss.life = boss.lifeMax; boss.timeLeft = 99999; } }; }
						BetStep(c.wait);
						bool died = _betKills > kills0;
						Note($"({c.name}) verdict {me.GetModPlayer<SpectatorPlayer>().LastLeave?.ToString() ?? "none"}, kill tick {SpectatorBet.BossKillTick}, blind tick {SpectatorBet.BossBlindTick}");
						Check($"({c.name}) {(c.dies ? "the player dies" : "no death")}", died == c.dies);
						if (died && c.dies) Check($"({c.name}) the death message is one of the boss-left lines: \"{_betText}\"", Enumerable.Range(0, SpectatorBet.LeftMessageCount).Any(k => SpectatorBet.LeftMessage(k, Commander) == _betText));
						_perTick = null; if (boss != null) boss.active = false;
					}
					Check("each of the boss-left lines is real text with the player's name filled in", Enumerable.Range(0, SpectatorBet.LeftMessageCount).All(k => { string m = SpectatorBet.LeftMessage(k, "Eve"); return m.Contains("Eve") && !m.Contains("Mods.Levy") && !m.Contains("{0}"); }));
					Check("the verdicts: a kill wins, a blind boss is not a loss, no army is no bet, the rest is lost",
						SpectatorBet.LeaveVerdict(100, 500, 400, 0, true) == SpectatorBet.LeaveResult.Won && SpectatorBet.LeaveVerdict(100, 500, 0, 450, true) == SpectatorBet.LeaveResult.Blinded
						&& SpectatorBet.LeaveVerdict(100, 500, 0, 0, false) == SpectatorBet.LeaveResult.NoArmy && SpectatorBet.LeaveVerdict(100, 500, 0, 0, true) == SpectatorBet.LeaveResult.Lost
						&& SpectatorBet.LeaveVerdict(100, 900, 0, 450, true) == SpectatorBet.LeaveResult.Lost && SpectatorBet.LeaveVerdict(100, 500, 50, 0, true) == SpectatorBet.LeaveResult.Lost);
				}
			}
			finally
			{
				if (_betHooked) { On_Player.KillMe -= BetKillMeDetour; _betHooked = false; }
				if (TileEntity.ByID.TryGetValue(BetBarracksId, out var betBar)) TestTileEntities.Remove(betBar);
				global::Levy.Common.Boss.BossDownedSystem.ClearForTest();
			}
		}

		// =====================================================================================================
		// BRIMSTONE MONSTER: Supreme Calamitas's vortex takes life inside its own CanHitPlayer and homes on Main.player[ai0]; a Spectator must be neither chased nor drained
		// =====================================================================================================
		private static void BrimstoneGuard()
		{
			if (!ModLoader.TryGetMod("CalamityMod", out Mod cal) || !cal.TryFind("BrimstoneMonster", out ModProjectile bm)) { Note("Calamity is not loaded: the Brimstone Monster guard is checked in the Calamity half"); return; }
			Arena(FloorY, Half);
			SetRevenge(true, false); Main.GameMode = GameModeID.Expert; Main.hardMode = true;
			global::Levy.Common.Boss.BossDownedSystem.ClearForTest();
			global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(NPCID.KingSlime, true);
			System.Reflection.FieldInfo anyBoss = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer")?.GetField("areThereAnyDamnBosses", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			Player me = Main.player[0];
			int bt = ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>();
			try
			{
				foreach (bool spec in new[] { false, true })
				{
					string arm = spec ? "Spectator" : "no Spectator";
					ClearHostiles(); me.dead = false; if (me.HasBuff(bt)) me.ClearBuff(bt);
					_spectator = spec; PlaceCommander(-50);
					Step(4);
					NPC boss = SpawnNpc(NPCID.KingSlime, new Vector2((Cx + 150) * 16f, (_floorY - 30) * 16f));
					_perTick = () => { if (boss != null && boss.active) { boss.life = boss.lifeMax; boss.timeLeft = 99999; boss.Center = new Vector2((Cx + 150) * 16f, (_floorY - 30) * 16f); } anyBoss?.SetValue(null, true); };
					Vector2 start = _cmdAt + new Vector2(900f, -40f);
					int pi = Projectile.NewProjectile(new EntitySource_WorldEvent(), start, Vector2.Zero, bm.Type, 100, 0f, Main.myPlayer, 0f, 0f);
					Projectile mon = Main.projectile[pi];
					BetStep(280); // the vortex grows for 180 ticks and sits for 90 more
					// the drain: put the vortex on the commander and ask the question vanilla asks first
					mon.Center = me.Center; mon.Opacity = 1f; mon.scale = 1.9f; me.statLife = me.statLifeMax2 = 500; me.immune = false; me.immuneTime = 0;
					int before = me.statLife; bool hit = Terraria.ModLoader.ProjectileLoader.CanHitPlayer(mon, me);
					int drained = before - me.statLife;
					Check($"(Brimstone Monster, {arm}) {(spec ? "it takes no life off the player" : "it drains life (the control: the check can see the bug)")} ({drained})", spec ? drained == 0 : drained > 0);
					Check($"(Brimstone Monster, {arm}) no standard hit is made", !hit);
					// the chase
					mon.Center = start; mon.velocity = Vector2.Zero; mon.ai[0] = 0f; mon.timeLeft = 36000; mon.Opacity = 1f; mon.scale = 1.9f;
					float d0 = Vector2.Distance(mon.Center, me.Center);
					BetStep(240);
					float d1 = mon.active ? Vector2.Distance(mon.Center, me.Center) : d0;
					Check($"(Brimstone Monster, {arm}) {(spec ? "it does not chase: the gap stays" : "it chases: the gap closes (the control)")} ({d0:0} -> {d1:0} px)", spec ? d1 > d0 - 60f : d1 < d0 - 200f);
					_perTick = null; if (boss != null) boss.active = false; if (mon.active) mon.active = false;
				}
			}
			finally { _spectator = false; _perTick = null; if (me.HasBuff(bt)) me.ClearBuff(bt); global::Levy.Common.Boss.BossDownedSystem.ClearForTest(); SetRevenge(false, false); }
		}

		private static void NewBetScene(BarracksTE bar, int manpower)
		{
			ClearHostiles(); RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) Main.npc[i].active = false;
			bar.Manpower = manpower; bar.Mobilized = false;
			Main.player[0].dead = false;
		}

		/// <summary>Every levy dies the way a real one does (the death path that spends the pool).</summary>
		private static void WipeArmy(List<SoldierNPC> levies)
		{
			foreach (var l in levies) if (l.NPC.active) { l.NPC.life = 1; l.NPC.SimpleStrikeNPC(99999, 0, false, 0f, null, false, 0f, false); }
		}
	}
}
