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
			}
			finally
			{
				if (_betHooked) { On_Player.KillMe -= BetKillMeDetour; _betHooked = false; }
				if (TileEntity.ByID.TryGetValue(BetBarracksId, out var betBar)) TestTileEntities.Remove(betBar);
				global::Levy.Common.Boss.BossDownedSystem.ClearForTest();
			}
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
