using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;

namespace Warbands.Common
{
	/// <summary>
	/// TIME-TO-KILL lab (env WARBANDS_TTK=1, headless boot). For four progression stages it reads the REAL game numbers (Item.SetDefaults damage and use time, NPC.SetDefaults life, defense and contact damage with the
	/// world mode forced to Master and to Classic, the armor pieces' defense) and Levy's real damage rules (LevyConstants.GradeDamageMult, HostileProjectileDamageMult, the defense formula Levy's soldiers use through
	/// Player.DefenseEffectiveness: Classic 0.5 / Expert 0.75 / Master 1.0, and the NPC rule damage - defense x 0.5), then prints one TSV row per matchup to the log and to $WARBANDS_OUT.
	/// Pure arithmetic: no fight is run. The hostile band does not exist yet, so a "levy" here is the stage's typical Levy kit as a target and as an attacker.
	/// </summary>
	public class TtkLab : ModSystem
	{
		// ---- the stage kits: what a normal player (and so a Levy in typical okish gear, see levy-mod/lab/presets/gear.yaml) carries ----
		private class Wpn { public char Cls; public string Item, Ammo; public int Shots = 1; public Wpn(char c, string i, string a = null, int shots = 1) { Cls = c; Item = i; Ammo = a; Shots = shots; } }
		private class Foe { public string Npc; public float Hz; public string Kind; public Foe(string n, float hz, string kind) { Npc = n; Hz = hz; Kind = kind; } }
		private class Kit
		{
			public int Stage; public string Label; public int PlayerHp, LevyHp, AccDef; public string[] Armor; public Wpn[] Weapons; public Foe[] Foes;
		}

		// Accuracy against a moving target and the share of the time a Levy is actually firing (levy-mod/docs/boss_lab/biomeblades/NOTES.md: 20-300 damage per levy-second against raw weapon figures well above that).
		private static readonly Dictionary<char, float> PlayerAcc = new() { ['R'] = 0.70f, ['M'] = 0.75f, ['E'] = 0.80f };
		private static readonly Dictionary<char, float> LevyDuty = new() { ['R'] = 0.55f, ['M'] = 0.60f, ['E'] = 0.35f };

		private static readonly Kit[] Kits =
		{
			new Kit { Stage = 0, Label = "pre-boss", PlayerHp = 200, LevyHp = 200, AccDef = 0,
				Armor = new[] { "GoldHelmet", "GoldChainmail", "GoldGreaves" },
				Weapons = new[] { new Wpn('R', "DemonBow", "FlamingArrow"), new Wpn('M', "Vilethorn"), new Wpn('E', "BladeofGrass") },
				Foes = new[] { new Foe("Zombie", 1.5f, "contact"), new Foe("DemonEye", 1.5f, "contact"), new Foe("Skeleton", 1.5f, "contact") } },
			new Kit { Stage = 1, Label = "post-EoW", PlayerHp = 300, LevyHp = 260, AccDef = 2,
				Armor = new[] { "ShadowHelmet", "ShadowScalemail", "ShadowGreaves" },
				Weapons = new[] { new Wpn('R', "PhoenixBlaster", "MeteorShot"), new Wpn('M', "Flamelash"), new Wpn('E', "LightsBane") },
				Foes = new[] { new Foe("Demon", 0.75f, "ranged"), new Foe("FireImp", 0.75f, "ranged"), new Foe("Hornet", 1.0f, "contact") } },
			new Kit { Stage = 3, Label = "early Hardmode", PlayerHp = 400, LevyHp = 360, AccDef = 5,
				Armor = new[] { "MythrilHelmet", "MythrilChainmail", "MythrilGreaves" },
				Weapons = new[] { new Wpn('R', "MythrilRepeater", "HellfireArrow"), new Wpn('M', "CrystalStorm"), new Wpn('E', "Excalibur") },
				Foes = new[] { new Foe("Pixie", 1.5f, "contact"), new Foe("ChaosElemental", 1.5f, "contact"), new Foe("Corruptor", 0.8f, "ranged") } },
			new Kit { Stage = 5, Label = "post-Plantera", PlayerHp = 500, LevyHp = 480, AccDef = 10,
				Armor = new[] { "TurtleHelmet", "TurtleScaleMail", "TurtleLeggings" },
				Weapons = new[] { new Wpn('R', "Megashark", "ChlorophyteBullet"), new Wpn('M', "NettleBurst"), new Wpn('E', "TerraBlade", null, 2) },
				Foes = new[] { new Foe("BlueArmoredBones", 1.5f, "contact"), new Foe("Necromancer", 0.5f, "ranged"), new Foe("SkeletonSniper", 0.4f, "ranged") } },
		};

		public override void OnWorldLoad()
		{
			if (Environment.GetEnvironmentVariable("WARBANDS_TTK") != "1") return;
			try { Run(); }
			catch (Exception e) { Mod.Logger.Error("[Warbands] TTK lab failed: " + e); }
		}

		private static int ItemType(string name) => ItemID.Search.TryGetId(name, out int id) ? id : -1;
		private static int NpcType(string name) => NPCID.Search.TryGetId(name, out int id) ? id : -1;

		private void Run()
		{
			var sb = new StringBuilder();
			void Row(params object[] f) { string line = string.Join("\t", f); sb.AppendLine(line); Mod.Logger.Info("[Warbands] TTK|" + line); }
			int savedMode = Main.GameMode;
			var missing = new List<string>();
			try
			{
				foreach (int mode in new[] { 2, 0 })
				{
					Main.GameMode = mode;
					string mname = mode == 2 ? "Master" : "Classic";
					float eff = mode == 2 ? 1f : 0.5f;
					float lifeMul = Main.GameModeInfo.EnemyMaxLifeMultiplier, dmgMul = Main.GameModeInfo.EnemyDamageMultiplier;
					Row("#", "MODE", mname, "defenseEffectiveness(player/levy)=" + eff, "npcDefenseEffectiveness=0.5", "levyGradeDamage=" + LevyConstants.GradeDamageMult[0], "hostileProjMult=" + LevyConstants.HostileProjectileDamageMult, "enemyLifeMult=" + lifeMul, "enemyDamageMult=" + dmgMul);
					foreach (var k in Kits)
					{
						int armor = 0;
						foreach (var a in k.Armor) { int t = ItemType(a); if (t < 0) { missing.Add(a); continue; } var it = new Item(); it.SetDefaults(t); armor += it.defense; }
						int def = armor + k.AccDef;
						Row("KIT", k.Label, "playerHp=" + k.PlayerHp, "levyHp=" + k.LevyHp, "defense=" + def + " (armor " + armor + " + acc " + k.AccDef + ")");

						// weapons: damage per second against a given defense, player accuracy vs levy duty
						var wInfo = new List<(char cls, string name, float dmg, float rate, int shots)>();
						foreach (var w in k.Weapons)
						{
							int t = ItemType(w.Item); if (t < 0) { missing.Add(w.Item); continue; }
							var it = new Item(); it.SetDefaults(t);
							float dmg = it.damage;
							if (w.Ammo != null) { int at = ItemType(w.Ammo); if (at < 0) missing.Add(w.Ammo); else { var am = new Item(); am.SetDefaults(at); dmg += am.damage; } }
							int ut = w.Cls == 'E' ? Math.Max(1, it.useAnimation) : Math.Max(1, it.useTime);
							wInfo.Add((w.Cls, it.Name, dmg, 60f / ut, w.Shots));
							Row("WEAPON", k.Label, w.Cls, it.Name, "hit=" + dmg, "perSec=" + (60f / ut).ToString("F2"), "raw dps=" + (dmg * 60f / ut * w.Shots).ToString("F0"));
						}
						float PlayerDps(float defense) { float s = 0; foreach (var w in wInfo) s += Math.Max(1f, w.dmg - defense * 0.5f) * w.rate * w.shots * PlayerAcc[w.cls]; return s / Math.Max(1, wInfo.Count); }
						float PlayerDpsCls(char c, float defense) { foreach (var w in wInfo) if (w.cls == c) return Math.Max(1f, w.dmg - defense * 0.5f) * w.rate * w.shots * PlayerAcc[c]; return 0; }
						float LevyDpsOn(float defense, float defEff) { float s = 0; foreach (var w in wInfo) s += Math.Max(1f, w.dmg * LevyConstants.GradeDamageMult[0] - defense * defEff) * w.rate * w.shots * LevyDuty[w.cls]; return s / Math.Max(1, wInfo.Count); }

						// Targets: three ordinary enemies and the Levy
						foreach (var f in k.Foes)
						{
							int nt = NpcType(f.Npc); if (nt < 0) { missing.Add(f.Npc); continue; }
							var n = new NPC(); n.SetDefaults(nt);
							float life = n.lifeMax, nd = n.defense;
							float dpsAvg = PlayerDps(nd);
							float hitToPlayer = Math.Max(1f, n.damage - def * eff);
							float foeDps = hitToPlayer * f.Hz;
							Row("FOE", k.Label, n.TypeName, "life=" + life, "defense=" + nd, "damage=" + n.damage,
								"playerTTK R/M/E=" + Ttk(life, PlayerDpsCls('R', nd)) + "/" + Ttk(life, PlayerDpsCls('M', nd)) + "/" + Ttk(life, PlayerDpsCls('E', nd)),
								"playerTTKavg=" + Ttk(life, dpsAvg),
								"hitOnPlayer=" + hitToPlayer.ToString("F0"), "foeDps=" + foeDps.ToString("F0"), "timeToKillPlayer=" + Ttk(k.PlayerHp, foeDps), "kind=" + f.Kind);
						}
						{
							float ldef = def; float life = k.LevyHp;
							float dpsAvg = PlayerDps(ldef);
							float levyDps = LevyDpsOn(def, eff);
							// the levy firing a hostile projectile at the player takes the x2 rule if left alone
							float levyDpsProj = Math.Max(1f, 0f);
							float rawLevyDps = 0; foreach (var w in wInfo) rawLevyDps += w.dmg * LevyConstants.GradeDamageMult[0] * w.rate * w.shots * LevyDuty[w.cls]; rawLevyDps /= Math.Max(1, wInfo.Count);
							float sLife = life * lifeMul; float sRaw = 0; foreach (var w in wInfo) sRaw += Math.Max(1f, w.dmg * LevyConstants.GradeDamageMult[0] * dmgMul - def * eff) * w.rate * w.shots * LevyDuty[w.cls]; sRaw /= Math.Max(1, wInfo.Count);
							Row("LEVY*", k.Label, "mode-scaled levy", "life=" + sLife, "defense=" + ldef, "damage=weapon x" + dmgMul,
								"playerTTK R/M/E=" + Ttk(sLife, PlayerDpsCls('R', ldef)) + "/" + Ttk(sLife, PlayerDpsCls('M', ldef)) + "/" + Ttk(sLife, PlayerDpsCls('E', ldef)),
								"playerTTKavg=" + Ttk(sLife, dpsAvg), "levyDpsOnPlayer=" + sRaw.ToString("F0"), "timeToKillPlayer=" + Ttk(k.PlayerHp, sRaw));
							Row("LEVY", k.Label, "typical levy", "life=" + life, "defense=" + ldef, "damage=weapon",
								"playerTTK R/M/E=" + Ttk(life, PlayerDpsCls('R', ldef)) + "/" + Ttk(life, PlayerDpsCls('M', ldef)) + "/" + Ttk(life, PlayerDpsCls('E', ldef)),
								"playerTTKavg=" + Ttk(life, dpsAvg),
								"levyDpsOnPlayer=" + levyDps.ToString("F0"), "levyRawDps=" + rawLevyDps.ToString("F0"), "timeToKillPlayer=" + Ttk(k.PlayerHp, levyDps), "kind=weapon");
						}
					}
				}
			}
			finally { Main.GameMode = savedMode; }
			if (missing.Count > 0) Row("#", "MISSING", string.Join(",", missing));
			string path = Environment.GetEnvironmentVariable("WARBANDS_OUT");
			if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
			Mod.Logger.Info("[Warbands] TTK lab done");
		}

		private static string Ttk(float hp, float dps) => dps <= 0f ? "inf" : (hp / dps).ToString("F1") + "s";
	}
}
