using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Levy.Common.Globals;

namespace Levy.Common.Testing
{
	/// <summary>
	/// BOSS LAB additions to the calrev harness (measurement only): tier-appropriate modded gear tables, the real levy count per progression point, worm bosses (Eater of Worlds, Desert Scourge) with head/body/tail/add
	/// damage split, an underground arena, Death mode, per-tick boss-target logging, enrage / movement sampling and a 30-tick debug trace.
	/// Extra spec tokens on a fight line:  n=cp|N  arena=surf|ug  gear=van|mod  grade=lv|mix  bhp=boss-life-multiplier  hpscale=percent-per-command-point  dbg=1.
	/// Modes: exp | rev | revoff | death.   Bosses: kslime eye brain eow ds qb skel.
	/// </summary>
	public partial class CombatHarness
	{
		private static CrRun _crRunCur;

		// ------------------------------------------------------------------------------------------------------------------ the real levy count
		/// <summary>Levies the player can field when this boss is fought: Levy's own tier math with the default settings (10 at the start, 20 at Wall of Flesh) and the Boss Checklist positions of the bosses
		/// beaten before it (King Slime 1, Desert Scourge 1.6, Eye 2, Crabulon 2.7, Eater/Brain 3, Hive Mind 3.98, Perforators 3.99, Queen Bee 4, Skeletron 5).</summary>
		internal static int CrRealMax(string boss)
		{
			float furthest = boss switch { "kslime" => 0f, "ds" => 1f, "eye" => 1.6f, "eow" => 2.7f, "brain" => 2.7f, "qb" => 3.99f, "skel" => 4f, _ => 0f };
			float[] prog = { 1, 2, 3, 4, 5, 6, 7 }; int[] w = { 1, 2, 2, 1, 2, 1, 7 };
			var l = new List<TierBoss>();
			for (int i = 0; i < 7; i++) l.Add(new TierBoss(0, w[i], prog[i] <= furthest, prog[i]));
			foreach (float mp in new[] { 1.6f, 2.7f, 3.98f, 3.99f, 6.7f }) l.Add(new TierBoss(0, 0f, mp <= furthest, mp));
			return LevyTierMath.Compute(l, 10, 20, 40, 60, 60);
		}

		// ------------------------------------------------------------------------------------------------------------------ boss setup / misc switches
		private static int CrCalNpc(string name) => ModContent.TryFind<ModNPC>("CalamityMod/" + name, out var m) ? m.Type : -1;

		private static void CrSetupBoss(CrState s, CrRun run)
		{
			switch (run.Boss)
			{
				case "kslime": foreach (int t in new[] { NPCID.BlueSlime, NPCID.SlimeSpiked, NPCID.GreenSlime, NPCID.PurpleSlime, NPCID.RedSlime, NPCID.YellowSlime, NPCID.IceSlime, NPCID.JungleSlime, NPCID.BlackSlime }) s.AddTypes.Add(t); break;
				case "brain": s.AddTypes.Add(NPCID.Creeper); break;
				case "eow": s.BodyType = NPCID.EaterofWorldsBody; s.TailType = NPCID.EaterofWorldsTail; s.PartTypes.Add(NPCID.EaterofWorldsHead); s.PartTypes.Add(NPCID.EaterofWorldsBody); s.PartTypes.Add(NPCID.EaterofWorldsTail); break;
				case "ds":
					s.BodyType = CrCalNpc("DesertScourgeBody"); s.TailType = CrCalNpc("DesertScourgeTail");
					foreach (int t in new[] { s.BossType, s.BodyType, s.TailType }) if (t > 0) s.PartTypes.Add(t);
					foreach (string n in new[] { "DesertNuisanceHead", "DesertNuisanceBody", "DesertNuisanceTail", "DesertNuisanceHeadYoung", "DesertNuisanceBodyYoung", "DesertNuisanceTailYoung" }) { int t = CrCalNpc(n); if (t > 0) s.AddTypes.Add(t); else global::Levy.Levy.Instance.Logger.Warn("HARNESS-CR: unknown Calamity NPC " + n); }
					break;
				case "qb": s.AddTypes.Add(NPCID.Bee); s.AddTypes.Add(NPCID.BeeSmall); break;
				case "skel": s.AddTypes.Add(NPCID.SkeletronHand); break;
				default: s.AddTypes.Add(NPCID.ServantofCthulhu); break;
			}
		}

		private static int _crDefHpScale = int.MinValue;
		private static void CrSetHpScale(CrRun run)
		{
			var c = global::Levy.Common.Config.LevyConfig.Get;
			if (_crDefHpScale == int.MinValue) _crDefHpScale = c.BossHpScalingPercentPerCp;
			c.BossHpScalingPercentPerCp = run != null && run.HpScale >= 0f ? (int)run.HpScale : _crDefHpScale;
		}

		private static Type _crDownedType;
		private static void CrResetDowned()
		{
			NPC.downedBoss1 = false; NPC.downedBoss2 = false; NPC.downedBoss3 = false; NPC.downedSlimeKing = false; NPC.downedQueenBee = false;
			CrSetCalDowned(false);
		}
		private static void CrSetCalDowned(bool v)
		{
			try
			{
				if (_crDownedType == null && ModLoader.TryGetMod("CalamityMod", out Mod cal)) _crDownedType = cal.Code.GetType("CalamityMod.DownedBossSystem");
				var p = _crDownedType?.GetProperty("downedDesertScourge", BindingFlags.Public | BindingFlags.Static);
				if (p != null) p.SetValue(null, v); else _crDownedType?.GetField("downedDesertScourge", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, v);
			}
			catch { }
		}
		private static bool CrCalDowned()
		{
			try { var p = _crDownedType?.GetProperty("downedDesertScourge", BindingFlags.Public | BindingFlags.Static); return p != null && (bool)p.GetValue(null); } catch { return false; }
		}
		private static bool CrDeathOn() { try { return _crDeathField != null && (bool)_crDeathField.GetValue(null); } catch { return false; } }

		private static void CrApplyZones(Player p)
		{
			var r = _crRunCur; if (r == null) return;
			p.ZoneCrimson = r.Boss == "brain"; p.ZoneCorrupt = r.Boss == "eow"; p.ZoneJungle = r.Jungle; p.ZoneDesert = r.Boss == "ds"; p.ZoneUndergroundDesert = r.Boss == "ds" && r.Arena == "ug";
			p.ZoneOverworldHeight = r.Arena != "ug"; p.ZoneRockLayerHeight = r.Arena == "ug"; p.ZoneDirtLayerHeight = false; p.ZoneSkyHeight = false; p.ZoneUnderworldHeight = false;
		}

		private static string CrPct(long a, long b) => b <= 0 ? "-" : (100.0 * a / b).ToString("0", CultureInfo.InvariantCulture);

		// ------------------------------------------------------------------------------------------------------------------ worms
		private bool CrWormTick(CrState st, ref NPC e)
		{
			int parts = 0; long life = 0, lmax = 0; NPC head = null;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !(n.type == st.BossType || st.PartTypes.Contains(n.type))) continue;
				parts++; n.timeLeft = 99999;
				bool linked = n.realLife >= 0 && n.realLife != n.whoAmI && n.realLife < Main.maxNPCs && Main.npc[n.realLife].active; // Calamity worms: segments share the head's life (realLife); counting them would multiply it
				if (!linked) { life += n.life; lmax += n.lifeMax; }
				if (n.type == st.BossType && head == null) head = n;
			}
			st.MaxParts = Math.Max(st.MaxParts, parts);
			if (parts > 0)
			{
				st.BossLife = (int)Math.Min(life, int.MaxValue); st.BossLifeMaxSum = Math.Max(st.BossLifeMaxSum, lmax); st.BossLifeMax = (int)Math.Min(st.BossLifeMaxSum, int.MaxValue);
				if (st.HalfTick < 0 && st.Tick > 40 && life * 2 <= st.BossLifeMaxSum) st.HalfTick = st.Tick;
			}
			if (head != null) e = head;
			bool alive = parts > 0 || st.Tick < 60;
			if (!alive && !st.BossGone)
			{
				st.BossGone = true; st.EndTick = st.Tick; st.BossLife = 0;
				long dmg = st.Levies.Sum(l => l.DmgBoss);
				bool flag = st.Run.Boss == "eow" ? NPC.downedBoss2 : CrCalDowned();
				st.Result = flag || (st.BossLifeMaxSum > 0 && dmg >= 0.9 * st.BossLifeMaxSum) ? "WIN" : "DESPAWN";
			}
			return alive;
		}

		// ------------------------------------------------------------------------------------------------------------------ per-tick sampling
		private static GlobalNPC _crCalGlobal; private static FieldInfo _crEnrF; private static MethodInfo _crInstM;
		private static bool CrEnraged(NPC n)
		{
			try
			{
				if (_crCalGlobal == null)
				{
					if (!ModContent.TryFind("CalamityMod/CalamityGlobalNPC", out GlobalNPC g)) return false;
					_crCalGlobal = g; _crEnrF = g.GetType().GetField("CurrentlyEnraged", BindingFlags.Public | BindingFlags.Instance);
					_crInstM = g.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m => m.Name == "Instance" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(NPC));
				}
				if (_crEnrF == null || _crInstM == null) return false;
				object inst = _crInstM.Invoke(_crCalGlobal, new object[] { n });
				return inst != null && (bool)_crEnrF.GetValue(inst);
			}
			catch { return false; }
		}

		private static string CrNpcName(int type) => type < 0 ? "none(" + type + ")" : type < NPCID.Count ? Lang.GetNPCNameValue(type).Replace(" ", "_") : (NPCLoader.GetNPC(type)?.Name ?? ("t" + type));

		private static int CrTgtClass(NPC n) => n.target == SoldierNPC.DummySlot || n.target >= 300 ? 0 : n.target >= 0 && n.target < Main.maxPlayers ? 1 : 2;

		private void CrSampleTick(CrState st, NPC e, bool alive, int adds)
		{
			if (alive)
			{
				st.BossAliveTicks++;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active) continue;
					if (n.type == st.BossType)
					{
						int c = CrTgtClass(n); if (c == 0) st.TgtLevy++; else if (c == 1) st.TgtPlayer++; else st.TgtNone++;
					}
					else if (st.AddTypes.Contains(n.type))
					{
						int c = CrTgtClass(n); if (c == 0) st.AddTgtLevy++; else if (c == 1) st.AddTgtPlayer++; else st.AddTgtNone++;
					}
				}
				if (st.HavePos) st.Moved += Vector2.Distance(st.LastPos, e.Center);
				st.LastPos = e.Center; st.HavePos = true;
				if (CrEnraged(e)) st.EnragedTicks++;
				if (e.dontTakeDamage) st.DtdTicks++;
				long sum = st.Levies.Sum(l => l.DmgBoss); int ph = (int)e.ai[0];
				st.Phase.TryGetValue(ph, out var pv); st.Phase[ph] = (pv.ticks + 1, pv.dmg + (sum - st.LastDmgSum)); st.LastDmgSum = sum;
				if (adds < st.PrevAdds) st.AddKills += st.PrevAdds - adds;
				st.PrevAdds = adds;
			}
			if (Environment.GetEnvironmentVariable("LEVY_CR_PROJTRACE") == "1" && st.Tick <= 900)
			{
				// projectile trace (measurement only): every live friendly modded projectile, with its AI state and where it is relative to the nearest levy / the boss
				int shown = 0;
				for (int pi = 0; pi < Main.maxProjectiles && shown < 6; pi++)
				{
					Projectile pr = Main.projectile[pi];
					if (!pr.active || pr.hostile || pr.type == 720) continue;
					float dl = 1e9f; foreach (var l in st.Levies) if (!l.Dead) { var n = Main.npc[l.Idx]; if (n.active) dl = Math.Min(dl, Vector2.Distance(n.Center, pr.Center)); }
					string ln = string.Format(CultureInfo.InvariantCulture, "PT\t{0}\t{1}\t{2}\towner={3}\tai={4:0.##},{5:0.##},{6:0.##}\tlai={7:0.##},{8:0.##}\tdmg={9}\tvel={10:0.0},{11:0.0}\ttl={12}\tdLevy={13:0}px\tdBoss={14:0}px\tpen={15}\ttile={16}\toHit={17}\tmouse={18:0},{19:0}\town={20}",
						st.Run.Key.Substring(0, Math.Min(40, st.Run.Key.Length)), st.Tick, (pr.ModProjectile?.Name ?? ("v" + pr.type)), pr.owner, pr.ai[0], pr.ai[1], pr.ai[2], pr.localAI[0], pr.localAI[1], pr.damage, pr.velocity.X, pr.velocity.Y, pr.timeLeft, dl, Vector2.Distance(pr.Center, e.Center), pr.penetrate, pr.tileCollide ? 1 : 0, pr.ownerHitCheck ? 1 : 0, Main.MouseWorld.X, Main.MouseWorld.Y, (pr.owner < 255 ? $"act{(Main.player[pr.owner].active?1:0)}dead{(Main.player[pr.owner].dead?1:0)}noI{(Main.player[pr.owner].noItems?1:0)}cc{(Main.player[pr.owner].CCed?1:0)}ch{(Main.player[pr.owner].channel?1:0)}ctr{(Main.player[pr.owner].controlUseItem?1:0)} ownDist{Vector2.Distance(Main.player[pr.owner].Center, pr.Center):0}" : "none"));
					try { File.AppendAllText(_crOut + ".proj.tsv", ln + "\n"); } catch { }
					shown++;
				}
			}
			if ((st.Run.Dbg || Environment.GetEnvironmentVariable("LEVY_CR_DEBUG") == "1") && st.Tick % 30 == 0)
			{
				int alv = st.Levies.Count(l => !l.Dead);
				float near = 1e9f; foreach (var l in st.Levies) if (!l.Dead) { var n = Main.npc[l.Idx]; if (n.active) near = Math.Min(near, Vector2.Distance(n.Center, e.Center)); }
				string line = string.Format(CultureInfo.InvariantCulture, "{0}\t{1}\t{2}\t{3}\t{4}\t{5}/{6}\t{7:0.0},{8:0.0}\t{9:0.0},{10:0.0}\tai={11:0.##},{12:0.##},{13:0.##},{14:0.##}\tlai={15:0.##},{16:0.##},{17:0.##},{18:0.##}\ttgt={19}({20})\tenr={21}\talive={22}\tadds={23}\tnearLevy={24:0.0}t\tdtd={25}\tlock={26}\tcmdDist={27:0.0}t",
					st.Run.Key, st.Tick, e.active ? 1 : 0, e.type, e.target, e.life, e.lifeMax, e.Center.X / 16f, e.Center.Y / 16f, e.velocity.X, e.velocity.Y, e.ai[0], e.ai[1], e.ai[2], e.ai[3], e.localAI[0], e.localAI[1], e.localAI[2], e.localAI[3],
					e.target, CrTgtClass(e) == 0 ? "levy" : CrTgtClass(e) == 1 ? "player" : "none", CrEnraged(e) ? 1 : 0, alv, adds, near / 16f, e.dontTakeDamage ? 1 : 0, LevyNpcData.Of(e).SoldierTarget, Vector2.Distance(Main.player[0].Center, e.Center) / 16f);
				try { File.AppendAllText(_crOut + ".dbg.tsv", line + "\n"); } catch { }
				if (st.Tick % 150 == 0 && Environment.GetEnvironmentVariable("LEVY_CR_LEVYDBG") == "1")
				{
					// per-levy state: what each levy is doing (target, state, why it stands still, distance to boss)
					var sb = new System.Text.StringBuilder();
					int k = 0;
					foreach (var l in st.Levies)
					{
						k++;
						var n = Main.npc[l.Idx];
						if (l.Dead || !n.active || !(n.ModNPC is SoldierNPC sn)) { sb.Append($"L{k}:{l.Cls}:dead\t"); continue; }
						string tt = "-"; float td = -1f;
						if (sn.TargetIndex >= 0 && sn.TargetIndex < Main.maxNPCs && Main.npc[sn.TargetIndex].active) { var t = Main.npc[sn.TargetIndex]; tt = t.type == st.BossType ? "BOSS" : st.AddTypes.Contains(t.type) ? "add:" + CrNpcName(t.type) : CrNpcName(t.type); td = Vector2.Distance(n.Center, t.Center) / 16f; }
						sb.Append(string.Format(CultureInfo.InvariantCulture, "L{0}:{1}:{2}:hp{3}:{4}:tgt={5}@{6:0}t:boss@{7:0}t:{8}\t", k, l.Cls, l.Weapon, n.life, sn.State, tt, td, Vector2.Distance(n.Center, e.Center) / 16f, sn.StillReason()));
					}
					try { File.AppendAllText(_crOut + ".levy.tsv", st.Run.Key + "\t" + st.Tick + "\t" + sb + "\n"); } catch { }
				}
			}
		}

		// ------------------------------------------------------------------------------------------------------------------ tier tables
		internal sealed class CrTierDef
		{
			public string Name, Why; public int Index;
			public Dictionary<char, List<(string w, string ammo, int wt)>> Weapons = new();
			public Dictionary<char, List<string[]>> Armor = new();
			public List<string> Acc = new();
			public bool Post => Index >= 3; // 200 hp, mana crystals 5-9, Healing Potion (as the vanilla "post-Eye" tier of the original rolls)
		}

		private static string[] CrVictide(char c) => CrSet("CalamityMod/VictideHead" + (c == 'R' ? "Ranged" : c == 'M' ? "Magic" : c == 'E' ? "Melee" : "Summon"), "CalamityMod/VictideBreastplate", "CalamityMod/VictideGreaves");
		private static string[] CrAerospec(char c) => CrSet("CalamityMod/AerospecHead" + (c == 'R' ? "Ranged" : c == 'M' ? "Magic" : c == 'E' ? "Melee" : "Summon"), "CalamityMod/AerospecBreastplate", "CalamityMod/AerospecLeggings");
		private static readonly string[] CrBee = CrSet("Bee Headgear", "Bee Breastplate", "Bee Greaves");

		private static List<CrTierDef> _crTiers;
		/// <summary>The tier data block. Tier = what is obtainable BEFORE that boss in Boss Checklist order with Calamity (King Slime 1, Desert Scourge 1.6, Eye 2, Crabulon 2.7, Eater/Brain 3, Hive Mind 3.98, Perforators 3.99,
		/// Queen Bee 4, Skeletron 5). Calamity picks are the best by Levy's WeaponClassifier.Score among items whose acquisition is certain (boss drop or recipe from that tier's materials); vanilla options of the tier stay in the pool.
		/// Weights: 3 = the best of the class at that tier, 1-2 = ordinary. Every name is verified at boot (unknown ones are logged and dropped).</summary>
		private static List<CrTierDef> CrTiers()
		{
			if (_crTiers != null) return _crTiers;
			var tiers = new List<CrTierDef>();
			CrTierDef T(int idx, string name, string why) { var t = new CrTierDef { Index = idx, Name = name, Why = why }; foreach (char c in "RMES") { t.Weapons[c] = new(); t.Armor[c] = new(); } tiers.Add(t); return t; }
			void W(CrTierDef t, char c, string w, string ammo, int wt) => t.Weapons[c].Add((w, ammo, wt));
			void Van(CrTierDef t, bool post) { var pool = post ? CrPoolPost : CrPoolPre; foreach (var kv in pool) foreach (var (w, a) in kv.Value) W(t, kv.Key, w, a, 1); }
			void Arm(CrTierDef t, char c, params string[][] sets) { foreach (var s in sets) t.Armor[c].Add(s); }

			// T0: King Slime, Desert Scourge fights. Nothing from either boss yet; Wulfrum from the surface enemies.
			var t0 = T(0, "pre-DS", "nothing beaten yet but King Slime: vanilla early gear plus Wulfrum (surface Wulfrum Drones; WulfrumKnife/Prosthesis/Controller score 71/55/47)");
			Van(t0, false);
			W(t0, 'M', "CalamityMod/WulfrumProsthesis", "", 2); W(t0, 'S', "CalamityMod/WulfrumController", "", 2); W(t0, 'E', "CalamityMod/WulfrumScrewdriver", "", 1);
			foreach (char c in "RMES") { foreach (var a in CrArmorPre[c]) t0.Armor[c].Add(a); Arm(t0, c, CrWulfrum, CrWulfrum); }
			t0.Acc.AddRange(CrAccCommon);

			// T2: Eye of Cthulhu fight. Desert Scourge beaten (Sahara Slicers / Barinade / Sandstream Scepter / Brittle Star Staff drops, Sand Cloak), Sea Prism + Navystone gear, Victide armor.
			var t2 = T(2, "post-DS", "KS + Desert Scourge beaten: DS drops (SaharaSlicers 129, SandstreamScepter 62, Barinade, BrittleStarStaff), Sea Prism/Navystone gear (StormSurge 78, AquamarineStaff 60, SeashineSword), Victide armor, Wulfrum");
			Van(t2, false);
			W(t2, 'R', "CalamityMod/StormSurge", "", 3); W(t2, 'R', "CalamityMod/Barinade", "Flaming Arrow|Wooden Arrow", 2);
			W(t2, 'M', "CalamityMod/SandstreamScepter", "", 3); W(t2, 'M', "CalamityMod/AquamarineStaff", "", 3);
			W(t2, 'E', "CalamityMod/SaharaSlicers", "", 3); W(t2, 'E', "CalamityMod/SeashineSword", "", 2); W(t2, 'E', "CalamityMod/UrchinMace", "", 1);
			W(t2, 'S', "CalamityMod/BrittleStarStaff", "", 2); W(t2, 'S', "CalamityMod/WulfrumController", "", 1);
			foreach (char c in "RMES") { foreach (var a in CrArmorPre[c]) t2.Armor[c].Add(a); Arm(t2, c, CrVictide(c), CrVictide(c), CrVictide(c), CrWulfrum); }
			t2.Acc.AddRange(CrAccCommon); t2.Acc.Add("CalamityMod/SandCloak"); t2.Acc.Add("CalamityMod/SandCloak");

			// T3: Eater of Worlds / Brain of Cthulhu fights. Eye + Crabulon beaten (TeardropCleaver 107, DeathstareRod 59, MycelialClaws 310, Fungicide 102, HyphaeRod 71, PuffShroom); evil-biome and hell gear is open.
			var t3 = T(3, "post-Crabulon", "Eye + Crabulon beaten: Eye drops (TeardropCleaver, DeathstareRod), Crabulon drops (MycelialClaws, Fungicide, HyphaeRod, PuffShroom), DS drops, Victide, Molten/Crimson/Shadow");
			Van(t3, true);
			W(t3, 'R', "CalamityMod/Fungicide", "Silver Bullet|Musket Ball", 3); W(t3, 'R', "CalamityMod/StormSurge", "", 2); W(t3, 'R', "CalamityMod/Barinade", "Hellfire Arrow|Flaming Arrow", 1);
			W(t3, 'M', "CalamityMod/HyphaeRod", "", 3); W(t3, 'M', "CalamityMod/SandstreamScepter", "", 2); W(t3, 'M', "CalamityMod/AquamarineStaff", "", 2);
			W(t3, 'E', "CalamityMod/MycelialClaws", "", 3); W(t3, 'E', "CalamityMod/TeardropCleaver", "", 3); W(t3, 'E', "CalamityMod/SaharaSlicers", "", 2);
			W(t3, 'S', "CalamityMod/DeathstareRod", "", 3); W(t3, 'S', "CalamityMod/PuffShroom", "", 2); W(t3, 'S', "CalamityMod/BrittleStarStaff", "", 1);
			foreach (char c in "RMES") { foreach (var a in CrArmorPost[c]) t3.Armor[c].Add(a); Arm(t3, c, CrVictide(c), CrVictide(c)); }
			t3.Acc.AddRange(CrAccCommon); t3.Acc.Add("CalamityMod/SandCloak");

			// T4: Queen Bee fight. Hive Mind / Perforators beaten (Shadethrower 103, Eviscerator 96, BloodBath 131, ShaderainStaff, SausageMaker 111, Aorta 93, FleshOfInfidelity, DankStaff), EoW/BoC loot.
			var t4 = T(4, "post-HiveMind/Perforators", "Hive Mind or Perforators beaten: their drops (Shadethrower, Eviscerator, BloodBath, ShaderainStaff, SausageMaker, Aorta, FleshOfInfidelity, DankStaff) plus every earlier tier");
			Van(t4, true);
			W(t4, 'R', "CalamityMod/Shadethrower", "Gel", 3); W(t4, 'R', "CalamityMod/Eviscerator", "Silver Bullet|Musket Ball", 3); W(t4, 'R', "CalamityMod/Fungicide", "Silver Bullet|Musket Ball", 2);
			W(t4, 'M', "CalamityMod/BloodBath", "", 3); W(t4, 'M', "CalamityMod/ShaderainStaff", "", 2); W(t4, 'M', "CalamityMod/HyphaeRod", "", 2);
			W(t4, 'E', "CalamityMod/SausageMaker", "", 3); W(t4, 'E', "CalamityMod/Aorta", "", 2); W(t4, 'E', "CalamityMod/MycelialClaws", "", 3); W(t4, 'E', "CalamityMod/TeardropCleaver", "", 2);
			W(t4, 'S', "CalamityMod/FleshOfInfidelity", "", 2); W(t4, 'S', "CalamityMod/DeathstareRod", "", 3); W(t4, 'S', "CalamityMod/DankStaff", "", 1); W(t4, 'S', "CalamityMod/PuffShroom", "", 1);
			foreach (char c in "RMES") { foreach (var a in CrArmorPost[c]) t4.Armor[c].Add(a); Arm(t4, c, CrVictide(c)); }
			t4.Acc.AddRange(CrAccCommon); t4.Acc.Add("CalamityMod/SandCloak");

			// T5: Skeletron fight. Queen Bee beaten (Bee armor, Bee Gun/Hornet Staff), Aerialite gear (Tradewinds 126, WindBlade 112, Galeforce 94).
			var t5 = T(5, "post-QueenBee", "Queen Bee beaten: Bee armor/weapons, Aerialite gear (Tradewinds, WindBlade, Galeforce, Aerospec armor) plus every earlier tier");
			Van(t5, true);
			W(t5, 'R', "CalamityMod/Galeforce", "Flaming Arrow|Hellfire Arrow", 3); W(t5, 'R', "CalamityMod/Shadethrower", "Gel", 2); W(t5, 'R', "CalamityMod/Eviscerator", "Silver Bullet|Musket Ball", 3); W(t5, 'R', "Bee Gun", "", 1);
			W(t5, 'M', "CalamityMod/Tradewinds", "", 3); W(t5, 'M', "CalamityMod/BloodBath", "", 3); W(t5, 'M', "CalamityMod/ShaderainStaff", "", 1);
			W(t5, 'E', "CalamityMod/WindBlade", "", 3); W(t5, 'E', "CalamityMod/SausageMaker", "", 3); W(t5, 'E', "CalamityMod/MycelialClaws", "", 2); W(t5, 'E', "Bee Keeper", "", 1);
			W(t5, 'S', "Hornet Staff", "", 3); W(t5, 'S', "CalamityMod/FleshOfInfidelity", "", 2); W(t5, 'S', "CalamityMod/DeathstareRod", "", 2);
			foreach (char c in "RMES") { foreach (var a in CrArmorPost[c]) t5.Armor[c].Add(a); Arm(t5, c, CrAerospec(c), CrAerospec(c), CrAerospec(c), CrBee, CrVictide(c)); }
			t5.Acc.AddRange(CrAccCommon); t5.Acc.Add("CalamityMod/SandCloak");

			CrValidateTiers(tiers);
			_crTiers = tiers;
			return tiers;
		}

		private static bool CrTryItemId(string name, out int id)
		{
			id = 0;
			try
			{
				if (name.Contains('/')) { if (ModContent.TryFind(name, out ModItem mi)) { id = mi.Type; return true; } return false; }
				id = CrItemId(name); return true;
			}
			catch { return false; }
		}

		private static void CrValidateTiers(List<CrTierDef> tiers)
		{
			var unknown = new SortedSet<string>(); int ok = 0;
			bool Check(string n) { if (string.IsNullOrEmpty(n)) return true; if (CrTryItemId(n, out _)) { ok++; return true; } unknown.Add(n); return false; }
			foreach (var t in tiers)
			{
				foreach (char c in "RMES")
				{
					t.Weapons[c] = t.Weapons[c].Where(x => Check(x.w) && (string.IsNullOrEmpty(x.ammo) || x.ammo.Split('|').Any(a => CrTryItemId(a, out _)))).ToList();
					t.Armor[c] = t.Armor[c].Where(set => set.All(Check)).ToList();
				}
				t.Acc = t.Acc.Where(Check).ToList();
			}
			global::Levy.Levy.Instance.Logger.Info($"HARNESS-CR: tier tables verified: {ok} item lookups OK, {unknown.Count} unknown names dropped: {string.Join(", ", unknown)}");
			foreach (var t in tiers) global::Levy.Levy.Instance.Logger.Info($"HARNESS-CR: tier {t.Index} {t.Name}: " + string.Join(" | ", "RMES".Select(c => c + "=" + string.Join(",", t.Weapons[c].Select(x => x.w.Replace("CalamityMod/", "C:") + "x" + x.wt)))));
		}

		private static CrTierDef CrTierOf(string boss)
		{
			var ts = CrTiers();
			int idx = boss switch { "kslime" => 0, "ds" => 0, "eye" => 2, "eow" => 3, "brain" => 3, "qb" => 4, "skel" => 5, _ => 2 };
			return ts.First(t => t.Index == idx);
		}

		internal static List<CrLevySpec> CrRollTier(string boss, int seed, int total, float hpx, bool allLevy)
		{
			var T = CrTierOf(boss); bool post = T.Post;
			var rng = new Random(seed * 7919 + 101 + T.Index * 13);
			var list = new List<CrLevySpec>();
			int nr = (int)Math.Round(total * 0.3), nm = (int)Math.Round(total * 0.3), ne = (int)Math.Round(total * 0.2), ns = Math.Max(0, total - nr - nm - ne);
			foreach (var (cls, n) in new[] { ('R', nr), ('M', nm), ('E', ne), ('S', ns) })
				for (int k = 0; k < n; k++)
				{
					var pool = T.Weapons[cls]; int sum = pool.Sum(x => x.wt), pick = rng.Next(sum), acc0 = 0; var chosen = pool[0];
					foreach (var x in pool) { acc0 += x.wt; if (pick < acc0) { chosen = x; break; } }
					string ammo = ""; if (!string.IsNullOrEmpty(chosen.ammo)) { var ao = chosen.ammo.Split('|').Where(a => CrTryItemId(a, out _)).ToArray(); if (ao.Length > 0) ammo = ao[rng.Next(ao.Length)]; }
					var armor = T.Armor[cls][rng.Next(T.Armor[cls].Count)];
					bool maa = !allLevy && rng.NextDouble() < 0.35;
					int slots = maa ? 3 : 1;
					var accPool = T.Acc.ToList(); if (cls == 'E') { accPool.Add("Feral Claws"); accPool.Add("Feral Claws"); } if (cls == 'M') { accPool.Add("Band of Starpower"); accPool.Add("Band of Starpower"); }
					var acc = new List<string>();
					for (int a = 0; a < slots; a++) { string pk = accPool[rng.Next(accPool.Count)]; if (pk != "" && !acc.Contains(pk)) acc.Add(pk); }
					int lc;
					if (post) lc = 5; else { double q = rng.NextDouble(); lc = q < .20 ? 0 : q < .40 ? 1 : q < .65 ? 2 : q < .80 ? 3 : q < .90 ? 4 : 5; }
					int mc;
					if (post) mc = cls == 'M' ? 9 : rng.Next(5, 10);
					else if (cls == 'M') { double q = rng.NextDouble(); mc = q < .15 ? 1 : q < .35 ? 2 : q < .55 ? 3 : q < .75 ? 4 : q < .90 ? 6 : 9; }
					else mc = rng.Next(0, 5);
					if (hpx != 1f) lc = (int)Math.Round((((100 + 20 * lc) * hpx) - 100) / 20.0);
					list.Add(new CrLevySpec { Cls = cls, Weapon = chosen.w, Ammo = ammo, Armor = armor, Acc = acc.ToArray(), LifeCrystals = lc, ManaCrystals = mc, ManAtArms = maa, Potions = rng.Next(3, 9), Potion = post ? "Healing Potion" : "Lesser Healing Potion" });
				}
			return list;
		}

		private static string CrFallbackWeapon(char cls) => cls switch { 'R' => "Minishark", 'M' => "Diamond Staff", 'E' => "Enchanted Sword", _ => "Slime Staff" };

		/// <summary>Dump the tier tables (for docs): LEVY_CR_TIERDOC=<path>.</summary>
		internal void CrDumpTierDoc()
		{
			string path = Environment.GetEnvironmentVariable("LEVY_CR_TIERDOC"); if (string.IsNullOrEmpty(path)) return;
			var sb = new System.Text.StringBuilder();
			foreach (var t in CrTiers())
			{
				sb.AppendLine($"## tier {t.Index}: {t.Name}\nwhy: {t.Why}\nlife crystals: {(t.Post ? "5 (200 hp)" : "rolled 0-5")}; potion: {(t.Post ? "Healing" : "Lesser Healing")}");
				foreach (char c in "RMES")
				{
					sb.AppendLine($"- {c} weapons: " + string.Join(", ", t.Weapons[c].Select(x => $"{x.w}[w{x.wt}{(x.ammo != "" ? "; ammo " + x.ammo : "")}]")));
					sb.AppendLine($"- {c} armor sets: " + string.Join(" / ", t.Armor[c].Select(a => a[0])));
				}
				sb.AppendLine("- accessory pool: " + string.Join(", ", t.Acc.Distinct()) + "\n");
			}
			File.WriteAllText(path, sb.ToString());
		}
	}
}
