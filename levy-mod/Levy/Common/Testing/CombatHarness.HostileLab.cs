using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Content.Buffs;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// HOSTILE-LEVY / TIME-TO-KILL LAB (measurement only; a calrev `fight` line whose tokens carry tp=hl=1;k=KIND;..., driven by a tipsy-style spec, see warbands-mod/docs/TTK_EXPERT.md).
	/// The stand-in commander (Main.player[0]) is dressed in a stage kit (real armor and accessories, so statDefense, endurance and i-frames come from the real equip pass) and strafes on the tipsy path.
	/// Its life is held at 1,000,000 and every landed hit is counted from ModPlayer.PostHurt, so the real Player.Hurt decides what each hit does (defense, difficulty rules, immunity frames).
	///   k=levy   n Levy soldiers (real weapon use, Levy-grade aim) shoot an invisible proxy NPC that is glued to the stand-in; each hit on the proxy is turned into a Player.Hurt on the stand-in.
	///            A projectile hit uses the hostile-projectile rule (hmP multiplies the projectile damage), a melee/beam strike counts as an NPC contact hit (hmC).
	///   k=enemy  real vanilla enemies (e=Name, n of them) attack the stand-in with their own AI, contact and projectiles (Main.myPlayer is swapped to 0 so the real collision code runs).
	///   k=calib  real hostile projectiles and direct Hurt calls at known damage, in Classic / Expert / Master, to read the hostile-projectile rule.
	///   k=npcdef player weapons of a stage against an NPC with a given defense through the real NPC.CalculateHitInfo / StrikeNPC, with three immunity models.
	///   k=pvp    the same weapons fired by one player at another through Player.Hurt(pvp: true), against the stage kit.
	/// Tokens: s=stage 0|1|3|5  n=count  d=distance tiles  ph=player max hp  nif=1 no i-frames  pc=extra accessory list (+ separated, _ for spaces)  hmP hmC=damage multipliers  warm win=ticks.
	/// Rows: LEVY_CR_OUT.hl.tsv.
	/// </summary>
	public partial class CombatHarness
	{
		// ---------------------------------------------------------------------------------------------------- shared state
		internal static bool HlOn;               // the measured window is open
		internal static bool HlActive;           // a lab run owns the stand-in commander
		internal static int HlProxyIdx = -1, HlShots;
		internal static readonly HashSet<int> HlLevyIdx = new();
		internal struct HlHit { public int Tick; public int Dmg; public bool Proj; public int Src; }
		internal static readonly List<HlHit> HlLanded = new();
		internal struct HlPend { public int Src, Dmg; public bool Proj; public int PType; }
		internal static readonly List<HlPend> HlPending = new();
		internal static int HlAttempts, HlTickNow; internal static long HlSrcP, HlSrcC; internal static int HlSrcPn, HlSrcCn;
		internal static readonly Dictionary<int, (int n, long dmg)> HlProjSpawn = new();
		internal const int HlBigLife = 1_000_000;

		private bool _hlStarted; private int _hlTick, _hlDeadTick = -1, _hlFirstHit = -1, _hlCalibI; private string _hlKind = "";
		private readonly List<NPC> _hlEnemies = new(); private readonly List<float> _hlLevyX = new(); private int _hlSavedMyPlayer = -1;
		private float _hlPvBase; private int _hlStage = -1; private string _hlDressKey = "";
		private int _hlCalibT0; private int _hlWarm = 240, _hlWin = 1800, _hlMeasureStart = -1;
		private float _hlHmP = 1f, _hlHmC = 1f; private bool _hlNif, _hlUpd = true; private int _hlModeAt = -100;
		private float _hlBx, _hlBaseY, _hlAmpX, _hlAmpY, _hlPh; private bool _hlStrafe = true;
		private Vector2 _hlPrev;

		// ---------------------------------------------------------------------------------------------------- stage kits
		private static readonly string[][] HlArmor =
		{
			new[] { "Gold Helmet", "Gold Chainmail", "Gold Greaves" },
			new[] { "Shadow Helmet", "Shadow Scalemail", "Shadow Greaves" },
			null,
			new[] { "Mythril Helmet", "Mythril Chainmail", "Mythril Greaves" },
			null,
			new[] { "Shroomite Headgear", "Shroomite Breastplate", "Shroomite Leggings" },
		};
		private static readonly string[][] HlAcc =
		{
			new[] { "Shackle", "Hermes Boots", "Band of Regeneration" },
			new[] { "Shackle", "Worm Scarf", "Hermes Boots" },
			null,
			new[] { "Cobalt Shield", "Worm Scarf", "Hermes Boots" },
			null,
			new[] { "Ankh Shield", "Worm Scarf", "Lightning Boots" },
		};
		private static readonly int[] HlHp = { 200, 300, 0, 400, 0, 500 };
		/// <summary>Weapons per stage and class: gun, bow, magic, melee, sniper (class letter, item, ammo).</summary>
		private static readonly (string kind, char cls, string w, string a)[][] HlWeapons =
		{
			new[] { ("gun", 'R', "Minishark", "Silver Bullet"), ("bow", 'R', "Demon Bow", "Flaming Arrow"), ("magic", 'M', "Ruby Staff", ""), ("melee", 'E', "Blade of Grass", ""), ("sniper", 'R', "Musket", "Silver Bullet") },
			new[] { ("gun", 'R', "Phoenix Blaster", "Meteor Shot"), ("bow", 'R', "Molten Fury", "Flaming Arrow"), ("magic", 'M', "Flamelash", ""), ("melee", 'E', "Light's Bane", ""), ("sniper", 'R', "Musket", "Meteor Shot") },
			null,
			new[] { ("gun", 'R', "Clockwork Assault Rifle", "Silver Bullet"), ("bow", 'R', "Mythril Repeater", "Hellfire Arrow"), ("magic", 'M', "Crystal Storm", ""), ("melee", 'E', "Excalibur", ""), ("sniper", 'R', "Musket", "High Velocity Bullet") },
			null,
			new[] { ("gun", 'R', "Megashark", "Chlorophyte Bullet"), ("bow", 'R', "Chlorophyte Shotbow", "Chlorophyte Arrow"), ("magic", 'M', "Nettle Burst", ""), ("melee", 'E', "Terra Blade", ""), ("sniper", 'R', "Sniper Rifle", "High Velocity Bullet") },
		};
		private static (string kind, char cls, string w, string a)? HlFind(int stage, string kind) { foreach (var w in HlWeapons[stage]) if (w.kind == kind) return w; return null; }

		private static string[] HlList(string s) => string.IsNullOrEmpty(s) ? Array.Empty<string>() : s.Split('+', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Replace('_', ' ')).ToArray();

		private void HlDress(int stage, string[] extraAcc)
		{
			Player p = Main.player[0];
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			var ar = HlArmor[stage]; var ac = HlAcc[stage].Concat(extraAcc).ToArray();
			for (int i = 0; i < 3; i++) p.armor[i] = CrMake(ar[i]);
			for (int i = 0; i < ac.Length && 3 + i < 8; i++) p.armor[3 + i] = CrMake(ac[i]);
			_hlStage = stage;
		}

		private string HlLog(string s) { Mod.Logger.Info("HARNESS-HL: " + s); return s; }

		// ---------------------------------------------------------------------------------------------------- stand-in
		private Vector2 HlPath(int t)
		{
			if (!_hlStrafe) return new Vector2(_hlBx * 16f, _hlBaseY * 16f);
			return new Vector2(_hlBx * 16f + _hlAmpX * 16f * (float)Math.Sin(2 * Math.PI * t / 360.0 + _hlPh), _hlBaseY * 16f + _hlAmpY * 16f * (float)Math.Sin(2 * Math.PI * t / 180.0 + 1.7 * _hlPh));
		}

		private void HlClearImmunity(Player p) { p.immune = false; p.immuneTime = 0; for (int i = 0; i < p.hurtCooldowns.Length; i++) p.hurtCooldowns[i] = 0; }

		/// <summary>Per tick: hold the stand-in (alive, huge life, on its path), run its own update (the server skips a client-less slot), then put it back on the path.</summary>
		private void HlDrive(int t, bool update = true)
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.team = 0; p.ghost = false;
			p.statLifeMax = p.statLifeMax2 = HlBigLife; p.statLife = HlBigLife; p.statManaMax = p.statManaMax2 = 200; p.statMana = 200;
			int bt = ModContent.BuffType<SpectatorBuff>(); if (p.HasBuff(bt)) p.ClearBuff(bt);
			Vector2 c = HlPath(t), c1 = HlPath(t + 1);
			p.Center = c; p.velocity = c1 - c;
			if (update && _hlUpd) { bool keep = _hlKind == "enemy" || _hlKind == "calib"; int sm = Main.myPlayer; Main.myPlayer = 0; try { p.Update(0); } finally { Main.myPlayer = keep ? 0 : sm; } }
			p.active = true; p.dead = false; p.statLife = HlBigLife;
			p.Center = c; p.velocity = c1 - c;
			if (_hlNif) HlClearImmunity(p);
		}

		/// <summary>The real Player.Hurt for one hit: kind = proj (hostile projectile), contact (NPC), pvp.</summary>
		private double HlHurt(Player p, int dmg, string kind)
		{
			PlayerDeathReason why = kind == "pvp" ? PlayerDeathReason.ByCustomReason("pvp") : PlayerDeathReason.ByCustomReason("levy");
			return p.Hurt(why, Math.Max(1, dmg), 1, kind == "pvp", true, -1);
		}

		internal static void HlOnHurt(Player.HurtInfo info)
		{
			if (!HlOn) return;
			bool proj = info.DamageSource != null && info.DamageSource.SourceProjectileLocalIndex >= 0;
			HlLanded.Add(new HlHit { Tick = HlTickNow, Dmg = info.Damage, Proj = proj, Src = 0 });
		}

		// ---------------------------------------------------------------------------------------------------- start / step
		private void StepHostile(CrRun run)
		{
			try { StepHostileInner(run); }
			catch (Exception)
			{
				HlOn = false; HlActive = false; _hlStarted = false; HlLevyIdx.Clear(); HlProxyIdx = -1; HlPending.Clear();
				if (_hlSavedMyPlayer >= 0) { Main.myPlayer = _hlSavedMyPlayer; _hlSavedMyPlayer = -1; } else if (Main.netMode == NetmodeID.Server) Main.myPlayer = 255;
				throw;
			}
		}

		private void StepHostileInner(CrRun run)
		{
			var kv = BtKv(run.Tp);
			string G(string k, string d) => kv.TryGetValue(k, out var v) ? v : d;
			float F(string k, float d) => kv.TryGetValue(k, out var v) ? float.Parse(v, CultureInfo.InvariantCulture) : d;
			_hlKind = G("k", "levy");
			if (!_hlStarted) { HlStart(run, kv, G, F); return; }
			_hlTick++; HlTickNow = _hlTick;
			Main.dayTime = false; Main.time = 16200;
			Player p = Main.player[0];
			// pending proxy hits become Hurt calls on the stand-in
			if (_hlKind == "levy" && HlOn)
			{
				foreach (var h in HlPending.ToArray())
				{
					HlAttempts++;
					float m = h.Proj ? _hlHmP : _hlHmC;
					if (h.Proj) { HlSrcP += h.Src; HlSrcPn++; } else { HlSrcC += h.Src; HlSrcCn++; }
					// vanilla Projectile.Damage (Projectile.cs line 13440): a hostile projectile hurts a player for Main.DamageVar(damage) * 2; an NPC contact hit passes Main.DamageVar(npc.damage) (Player.Update_NPCCollision)
					int dmg = Math.Max(1, Main.DamageVar(Math.Max(1f, h.Src * m)) * (h.Proj ? 2 : 1));
					HlHurt(p, dmg, h.Proj ? "proj" : "contact");
				}
				HlPending.Clear();
			}
			if (_hlKind == "levy" || _hlKind == "enemy") HlDrive(_hlTick);
			else HlDrive(_hlTick);
			if (_hlKind == "levy") HlStepLevy(run, kv);
			else if (_hlKind == "enemy") HlStepEnemy(run, kv);
			else if (_hlKind == "calib") HlStepCalib(run, kv);
			else if (_hlKind == "npcdef") { if (_hlTick == 6) { HlNpcDef(run, kv); HlFinish(run, kv); } }
			else if (_hlKind == "pvp")
			{
				int[] gms = { GameModeID.Normal, GameModeID.Expert, GameModeID.Master };
				for (int i = 0; i < 3; i++) { if (_hlTick == 6 + 4 * i) Main.GameMode = gms[i]; if (_hlTick == 9 + 4 * i) HlPvp(run, kv, gms[i]); }
				if (_hlTick == 18) HlFinish(run, kv);
			}
			else { Mod.Logger.Warn("HARNESS-HL: unknown kind " + _hlKind); HlFinish(run, kv); }
		}

		private void HlStart(CrRun run, Dictionary<string, string> kv, Func<string, string, string> G, Func<string, float, float> F)
		{
			ClearAll(); CrPurgeNpcs(); CrApplyMode("exp"); _crRunCur = run;
			Main.rand = new Terraria.Utilities.UnifiedRandom(run.Seed * 7919 + 13);
			if (G("gm", "") == "classic") Main.GameMode = GameModeID.Normal; else if (G("gm", "") == "master") Main.GameMode = GameModeID.Master;
			_hlWarm = (int)F("warm", 240); _hlWin = (int)F("win", 1800); _hlHmP = F("hmP", 1f); _hlHmC = F("hmC", 1f); _hlNif = G("nif", "0") == "1"; _hlUpd = G("upd", "0") == "1";
			int stage = (int)F("s", 0);
			HlDress(stage, HlList(G("pc", "")));
			HlLanded.Clear(); HlPending.Clear(); HlLevyIdx.Clear(); HlProjSpawn.Clear(); _hlEnemies.Clear(); _hlLevyX.Clear();
			HlOn = false; HlShots = 0; HlAttempts = 0; HlSrcP = HlSrcC = 0; HlSrcPn = HlSrcCn = 0; _hlDeadTick = -1; _hlFirstHit = -1; _hlTick = 0; _hlCalibI = 0; _hlMeasureStart = -1;
			_hlStrafe = G("m", "strafe") == "strafe";
			float x0 = _crCx - 30f, d = F("d", 16f);
			_hlBx = x0 + d; _hlBaseY = CrF - 3.2f; _hlAmpX = _hlStrafe ? 8f : 0f; _hlAmpY = _hlStrafe ? 2.5f : 0f; _hlPh = run.Seed * 1.3f;
			if (_hlKind == "enemy" || _hlKind == "calib" || _hlKind == "npcdef" || _hlKind == "pvp") { _hlBx = _crCx - 4f; if (_hlKind != "enemy") { _hlStrafe = false; _hlAmpX = _hlAmpY = 0f; } else { _hlBaseY = CrF - 1.4f; _hlAmpY = F("ay", 0f); } }
			_crCmdPos = new Vector2(x0 * 16f, CrF * 16f);
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.team = 0; p.position = new Vector2(_hlBx * 16f, _hlBaseY * 16f);
			p.statLifeMax = p.statLifeMax2 = HlBigLife; p.statLife = HlBigLife;
			HlClearImmunity(p);
			HlOn = false; // opened by the kind's own step once the kit has had its first update
			if (_hlKind == "levy")
			{
				var cls = G("c", "R")[0];
				var spec = new CrLevySpec { Cls = cls, Weapon = G("w", "Minishark").Replace('_', ' '), Ammo = G("a", "").Replace('_', ' '), LifeCrystals = 15, ManaCrystals = 9, Potions = 0, Potion = "Healing Potion", ManAtArms = false };
				int n = (int)F("n", 1);
				for (int i = 0; i < n; i++)
				{
					var lv = CrSpawnLevy(spec, run, i, n, new Vector2(x0 * 16f, CrF * 16f));
					HlLevyIdx.Add(lv.Idx); _hlLevyX.Add(Main.npc[lv.Idx].position.X);
				}
				int ix = NPC.NewNPC(new EntitySource_WorldEvent("LevyHostileLab"), (int)(_hlBx * 16f), (int)(_hlBaseY * 16f), NPCID.Zombie);
				if (ix < 0 || ix >= Main.maxNPCs) throw new Exception("proxy spawn failed");
				NPC pr = Main.npc[ix]; var ht = pr.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true;
				pr.width = 20; pr.height = 42; pr.lifeMax = 100_000_000; pr.life = pr.lifeMax; pr.damage = 0; pr.knockBackResist = 0f; pr.noGravity = true; pr.noTileCollide = true; pr.aiStyle = -1; pr.friendly = false;
				pr.dontTakeDamage = false; pr.value = 0f; pr.boss = false; pr.defense = 0; pr.defDefense = 0; pr.Center = new Vector2(_hlBx * 16f, _hlBaseY * 16f); pr.velocity = Vector2.Zero; ht.Pin = pr.position;
				if (_hlStrafe) ht.Path = t => HlPath(t + 1);
				HlProxyIdx = ix; ht.PathT = 0;
				HlLog($"start levy {run.Key} stage {stage} {spec.Weapon}/{spec.Ammo} n={n} d={d}");
			}
			else if (_hlKind == "enemy")
			{
				string en = G("e", "Zombie"); int n = (int)F("n", 1);
				if (!NPCID.Search.TryGetId(en, out int nt)) throw new Exception("unknown npc " + en);
				bool fly = G("fly", "0") == "1";
				for (int i = 0; i < n; i++)
				{
					float side = (i % 2 == 0 ? -1f : 1f) * (14f + 3f * i);
					int ix = NPC.NewNPC(new EntitySource_WorldEvent("LevyHostileLab"), (int)((_hlBx + side) * 16f), (int)((fly ? CrF - 14f : CrF - 3f) * 16f), nt);
					if (ix < 0 || ix >= Main.maxNPCs) throw new Exception("enemy spawn failed");
					Main.npc[ix].lifeMax = Main.npc[ix].life = 100_000_000; Main.npc[ix].timeLeft = 100000; _hlEnemies.Add(Main.npc[ix]);
				}
				_hlSavedMyPlayer = Main.myPlayer; Main.myPlayer = 0;
				HlLog($"start enemy {run.Key} {en} x{n}: damage {Main.npc[_hlEnemies[0].whoAmI].damage}");
			}
			else { if (_hlKind == "calib") { _hlSavedMyPlayer = Main.myPlayer; Main.myPlayer = 0; } HlLog($"start {_hlKind} {run.Key} stage {stage}"); }
			_hlStarted = true; HlActive = true;
		}

		private void HlStepLevy(CrRun run, Dictionary<string, string> kv)
		{
			Player p = Main.player[0];
			if (HlProxyIdx >= 0 && Main.npc[HlProxyIdx].active) { p.Center = Main.npc[HlProxyIdx].Center; p.velocity = Main.npc[HlProxyIdx].velocity; }
			bool pin = kv.TryGetValue("pin", out var pv) ? pv != "0" : (kv.TryGetValue("c", out var cv) ? cv != "E" : true);
			int k = 0;
			foreach (int idx in HlLevyIdx)
			{
				NPC lv = Main.npc[idx];
				if (lv.active && lv.ModNPC is SoldierNPC sn)
				{
					sn.Stations = Systems.StationFlags.Campfire | Systems.StationFlags.HeartLantern;
					if (pin) { lv.position.X = _hlLevyX[k]; lv.velocity.X = 0f; }
				}
				k++;
			}
			if (HlProxyIdx >= 0) { NPC pr = Main.npc[HlProxyIdx]; pr.timeLeft = 3600; }
			// weapons that spawn stray NPCs are not used here; keep the arena clean of anything but the proxy
			CrPinStrays(HlProxyIdx);
			if (_hlTick == _hlWarm) { HlLanded.Clear(); HlPending.Clear(); HlShots = 0; HlAttempts = 0; HlSrcP = HlSrcC = 0; HlSrcPn = HlSrcCn = 0; HlClearImmunity(p); HlOn = true; _hlMeasureStart = _hlTick; HlLog($"measuring: def {p.statDefense} endurance {p.endurance} immuneTime-after-hit pending"); }
			if (!HlOn) return;
			long taken = 0; foreach (var h in HlLanded) taken += h.Dmg;
			int hp = (int)(kv.TryGetValue("ph", out var phv) ? float.Parse(phv, CultureInfo.InvariantCulture) : HlHp[_hlStage]);
			if (_hlDeadTick < 0 && taken >= hp) _hlDeadTick = _hlTick - _hlMeasureStart;
			if (_hlFirstHit < 0 && HlLanded.Count > 0) _hlFirstHit = HlLanded[0].Tick - _hlMeasureStart;
			if (_hlDeadTick >= 0 || _hlTick >= _hlWarm + _hlWin) HlFinish(run, kv);
		}

		private void HlStepEnemy(CrRun run, Dictionary<string, string> kv)
		{
			Player p = Main.player[0];
			Main.myPlayer = 0;
			foreach (var e in _hlEnemies) if (e.active) { e.life = e.lifeMax; e.timeLeft = 100000; }
			if (HlOn || _hlTick >= _hlWarm - 1) HlRealHits(p);
			if (_hlTick == _hlWarm) { HlLanded.Clear(); HlProjSpawn.Clear(); HlClearImmunity(p); HlOn = true; _hlMeasureStart = _hlTick; HlLog($"measuring: def {p.statDefense} endurance {p.endurance}"); }
			if (!HlOn) return;
			long taken = 0; foreach (var h in HlLanded) taken += h.Dmg;
			int hp = (int)(kv.TryGetValue("ph", out var phv) ? float.Parse(phv, CultureInfo.InvariantCulture) : HlHp[_hlStage]);
			if (_hlDeadTick < 0 && taken >= hp) _hlDeadTick = _hlTick - _hlMeasureStart;
			if (_hlFirstHit < 0 && HlLanded.Count > 0) _hlFirstHit = HlLanded[0].Tick - _hlMeasureStart;
			if (_hlDeadTick >= 0 || _hlTick >= _hlWarm + _hlWin) HlFinish(run, kv);
		}

		/// <summary>Contact: the real Player.Update_NPCCollision (public, normally run only for the local player). Hostile projectiles: on a dedicated server Projectile.Damage returns at "Main.netMode == 2 || !hostile" before it reaches
		/// the player (a client runs it for its own player), so the three vanilla lines that matter are replayed (Projectile.cs 13414-13441): hitbox test, i-frame test (a projectile with a cooldown slot ignores the general i-frames),
		/// and Player.Hurt(Main.DamageVar(damage) * 2, pvp false, cooldown slot).</summary>
		private void HlRealHits(Player p)
		{
			int sm = Main.myPlayer; Main.myPlayer = 0;
			try
			{
				p.Update_NPCCollision();
				Rectangle pr = p.getRect();
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile q = Main.projectile[i];
					if (!q.active || !q.hostile || q.damage <= 0) continue;
					int slot = q.ModProjectile != null ? q.ModProjectile.CooldownSlot : -1;
					if (!p.active || p.dead || (p.immune && slot == -1)) continue;
					if (!q.Colliding(q.Hitbox, pr)) continue;
					int dir = p.Center.X < q.Center.X ? -1 : 1;
					int dmg = Main.DamageVar(q.damage, 0f) * 2;
					p.Hurt(PlayerDeathReason.ByCustomReason("hostile projectile"), dmg, dir, false, true, slot);
					q.Kill();
				}
			}
			finally { Main.myPlayer = sm; }
		}

		// ---------------------------------------------------------------------------------------------------- calibration: the hostile-projectile rule
		private void HlStepCalib(CrRun run, Dictionary<string, string> kv)
		{
			Player p = Main.player[0];
			Main.myPlayer = 0;
			int[] modes = { GameModeID.Normal, GameModeID.Expert, GameModeID.Master }; int[] dmgs = { 40, 100 };
			int total = modes.Length * dmgs.Length * 2; // direct + real projectile
			int period = 100;
			if (_hlTick < 10) return;
			int step = (_hlTick - 10) / period, at = (_hlTick - 10) % period;
			if (step >= total) { HlFinish(run, kv); return; }
			int mi = step / (dmgs.Length * 2), di = step / 2 % dmgs.Length, real = step % 2;
			int mode = modes[mi], dmg = dmgs[di];
			if (at == 0) { Main.GameMode = mode; HlClearImmunity(p); HlOn = true; HlLanded.Clear(); }
			if (at == 5)
			{
				_hlCalibT0 = _hlTick;
				if (real == 0) { double r = HlHurt(p, dmg, "contact"); HlRow(run, "calib", $"direct\tmode={mode}\tdmg={dmg}\tdef={p.statDefense}\tendur={p.endurance:0.###}\ttaken={r:0}\timmuneAfter={p.immuneTime}"); }
				else
				{
					Vector2 from = p.Center + new Vector2(-96f, 0f);
					int pi = Projectile.NewProjectile(new EntitySource_Misc("LevyHostileLab"), from, new Vector2(10f, 0f), ProjectileID.BulletDeadeye, dmg, 0f, 255);
					if (pi >= 0 && pi < Main.maxProjectiles) { Projectile pr = Main.projectile[pi]; pr.hostile = true; pr.friendly = false; pr.damage = dmg; pr.timeLeft = 120; pr.tileCollide = false; pr.penetrate = 1; }
				}
			}
			if (real == 0 && (at == 15 || at == 35)) HlRow(run, "calib", $"immune@+{at - 5}\tmode={mode}\timmuneTime={p.immuneTime}\timmune={p.immune}\tupd={_hlUpd}");
			if (real == 1 && at >= 5) HlRealHits(p);
			if (real == 1 && at >= 6 && at <= 20 && at % 2 == 0)
			{
				string pj = "none"; for (int i = 0; i < Main.maxProjectiles; i++) { var q = Main.projectile[i]; if (q.active && q.type == ProjectileID.BulletDeadeye) { pj = $"idx{i} pos={q.Center.X - p.Center.X:0},{q.Center.Y - p.Center.Y:0} hostile={q.hostile} friendly={q.friendly} owner={q.owner} dmg={q.damage} vel={q.velocity.X:0.0}"; break; } }
				HlRow(run, "calib", $"trace@+{at - 5}\t{pj}\tmyPlayer={Main.myPlayer}\tplayerActive={p.active}\timmune={p.immune}");
			}
			if (at == 40 && real == 1)
			{
				long taken = 0; foreach (var h in HlLanded) taken += h.Dmg;
				HlRow(run, "calib", $"proj\tmode={mode}\tdmg={dmg}\tdef={p.statDefense}\tendur={p.endurance:0.###}\ttaken={taken}\thits={HlLanded.Count}\timmuneAfter={p.immuneTime}");
			}
		}

		// ---------------------------------------------------------------------------------------------------- weapon data
		private struct HlWeapon { public string Kind, Name, Ammo; public char Cls; public float Hit, UseTicks; public int ShotsPerUse, Pellets, Crit; public bool Piercing, Melee; public int CycleTicks; }

		private HlWeapon HlWeaponData(int stage, string kind)
		{
			var w = HlFind(stage, kind) ?? throw new Exception("no weapon " + kind + " at stage " + stage);
			Item it = CrMake(w.w); float hit = it.damage;
			if (!string.IsNullOrEmpty(w.a)) { Item am = CrMake(w.a); hit += am.damage; }
			bool melee = w.cls == 'E';
			int ut = Math.Max(1, it.useTime), ua = Math.Max(1, it.useAnimation), rd = it.reuseDelay;
			int shotsPerUse = melee ? 1 : Math.Max(1, (int)Math.Ceiling(ua / (double)ut));
			int cycle = ua + rd;
			int pellets = w.w == "Boomstick" ? 4 : 1;
			bool piercing = false;
			int pt = it.shoot > ProjectileID.None ? it.shoot : 0;
			if (!string.IsNullOrEmpty(w.a)) { Item am = CrMake(w.a); if (am.shoot > 0 && w.cls == 'R') pt = am.shoot; }
			if (pt > 0) { var pr = new Projectile(); pr.SetDefaults(pt); piercing = pr.penetrate != 1; }
			return new HlWeapon { Kind = kind, Name = it.Name, Ammo = w.a, Cls = w.cls, Hit = hit, UseTicks = ut, ShotsPerUse = shotsPerUse, Pellets = pellets, Crit = 4 + it.crit, Piercing = piercing, Melee = melee, CycleTicks = Math.Max(1, cycle) };
		}

		/// <summary>The tick of every shot (each pellet) of a weapon over a window, with the spacing a real player's ItemCheck gives it: shotsPerUse shots every useTime, then the animation (plus reuse delay) ends.</summary>
		private static List<int> HlShotTicks(HlWeapon w, int window)
		{
			var l = new List<int>();
			for (int t0 = 0; t0 < window; t0 += w.CycleTicks)
				for (int s = 0; s < w.ShotsPerUse; s++) { int t = t0 + (int)(s * w.UseTicks); if (t < window) l.Add(t); }
			return l;
		}

		// ---------------------------------------------------------------------------------------------------- npcdef: player weapons against a defended NPC
		private void HlNpcDef(CrRun run, Dictionary<string, string> kv)
		{
			int stage = _hlStage; int trials = 100; int window = 60 * 30;
			int[] defs = { 0, 8, 14, 24, 34, 40, 46, 55, 68, 75 };
			int armorDef = 0; foreach (var a in HlArmor[stage]) armorDef += CrMake(a).defense;
			var defList = defs.Concat(new[] { armorDef }).Distinct().OrderBy(x => x).ToArray();
			int nix = NPC.NewNPC(new EntitySource_WorldEvent("LevyHostileLab"), (int)(_hlBx * 16f), (int)((_hlBaseY - 6f) * 16f), NPCID.Zombie);
			if (nix < 0 || nix >= Main.maxNPCs) throw new Exception("npcdef spawn failed");
			NPC npc = Main.npc[nix]; npc.GetGlobalNPC<HarnessTarget>().IsTarget = true; npc.noGravity = true; npc.noTileCollide = true; npc.aiStyle = -1; npc.damage = 0; npc.friendly = false;
			int[] bandRef = { 90, 180, 0, 555, 0, 750 }; float[] lives = { 100f, 300f, 1000f, 3000f, bandRef[stage] * 0.8f * 2f, bandRef[stage] * 1.2f * 2f }; // the last two: a Warbands shooter / melee member in Expert (stage reference x share x2)
			foreach (var kind in new[] { "gun", "bow", "magic", "melee", "sniper" })
			{
				var w = HlWeaponData(stage, kind);
				var shots = HlShotTicks(w, window);
				foreach (int def in defList)
				{
					foreach (string model in new[] { "vanilla", "iframes", "none" })
					{
						double sumDmg = 0, sumHits = 0, sumAtt = 0; var ttk = new List<double>[lives.Length]; for (int li = 0; li < lives.Length; li++) ttk[li] = new List<double>();
						for (int tr = 0; tr < trials; tr++)
						{
							npc.active = true; npc.lifeMax = npc.life = 100_000_000; npc.defense = def; npc.defDefense = def; npc.dontTakeDamage = false; npc.Center = new Vector2(_hlBx * 16f, (_hlBaseY - 6f) * 16f); npc.velocity = Vector2.Zero;
							int gate = 0; double dmgTot = 0, hits = 0, att = 0; var tKill = new double[lives.Length]; for (int li = 0; li < lives.Length; li++) tKill[li] = -1;
							for (int si = 0; si < shots.Count; si++)
							{
								int t = shots[si];
								for (int pl = 0; pl < w.Pellets; pl++)
								{
									att++;
									bool landed = true;
									if (model == "iframes") landed = t >= gate;
									else if (model == "vanilla") landed = !(w.Piercing && t < gate);
									if (!landed) continue;
									bool crit = Main.rand.Next(100) < w.Crit;
									var hit = npc.CalculateHitInfo((int)w.Hit, 1, crit, 0f, DamageClass.Generic, true, 0f);
									int got = npc.StrikeNPC(hit, false, true);
									dmgTot += got; hits++;
									if (model == "iframes") gate = t + 40; else if (model == "vanilla" && w.Piercing) gate = t + 10;
									for (int li = 0; li < lives.Length; li++) if (tKill[li] < 0 && dmgTot >= lives[li]) tKill[li] = t / 60.0;
								}
							}
							sumDmg += dmgTot; sumHits += hits; sumAtt += att; for (int li = 0; li < lives.Length; li++) if (tKill[li] >= 0) ttk[li].Add(tKill[li]);
						}
						double dps = sumDmg / trials / (window / 60.0);
						HlRow(run, "npcdef", $"s={stage}\t{kind}\t{w.Name}\t{w.Ammo}\thit={w.Hit}\tdef={def}\tmodel={model}\tdps={dps:0.0}\tperHit={(sumHits > 0 ? sumDmg / sumHits : 0):0.0}\thitsPerSec={sumHits / trials / (window / 60.0):0.00}\tshotsPerSec={sumAtt / trials / (window / 60.0):0.00}\tttk={string.Join("/", ttk.Select((l, li) => lives[li].ToString("0") + ":" + (l.Count > 0 ? (l.Count == trials ? "" : "~") + l.Average().ToString("0.0") : "none")))}\tarmorDef={armorDef}");
					}
				}
			}
		}

		// ---------------------------------------------------------------------------------------------------- pvp / ideal-aim hostile: weapons through the real Player.Hurt
		private void HlPvp(CrRun run, Dictionary<string, string> kv, int gm)
		{
			Player p = Main.player[0]; int stage = _hlStage; int window = 60 * 30; int trials = 40;
			HlLog($"pvp stage {stage} gm {gm}: def {p.statDefense} endurance {p.endurance} defEff {p.DefenseEffectiveness.Value}");
			{
				foreach (var kind in new[] { "gun", "bow", "magic", "melee", "sniper" })
				{
					var w = HlWeaponData(stage, kind); var shots = HlShotTicks(w, window);
					foreach (string rule in new[] { "pvp", "proj", "contact" })
					{
						foreach (bool nif in new[] { false, true })
						{
							double dmgTot = 0, hits = 0, att = 0, firstSum = 0; int immSeen = 0;
							for (int tr = 0; tr < trials; tr++)
							{
								HlClearImmunity(p); p.statLife = HlBigLife; long start = p.statLife; int gate = 0; int next = 0; double got = 0, h = 0; bool first = false;
								for (int t = 0; t < window; t++)
								{
									if (p.immuneTime > 0) p.immuneTime--; p.immune = p.immuneTime > 0; for (int c = 0; c < p.hurtCooldowns.Length; c++) if (p.hurtCooldowns[c] > 0) p.hurtCooldowns[c]--;
									if (nif) HlClearImmunity(p);
									while (next < shots.Count && shots[next] == t)
									{
										next++;
										for (int pl = 0; pl < w.Pellets; pl++)
										{
											att++;
											bool crit = rule == "pvp" && Main.rand.Next(100) < w.Crit;
											float baseDmg = w.Hit * (crit ? 2f : 1f);
											int d = Main.DamageVar(baseDmg) * (rule == "proj" ? 2 : 1); // a hostile projectile hurts for DamageVar(damage) * 2 (Projectile.cs 13440)
											HlLanded.Clear(); HlOn = true;
											double r = HlHurt(p, d, rule);
											HlOn = false;
											if (r > 0) { got += r; h++; if (!first) { first = true; firstSum += t; } if (immSeen == 0) immSeen = p.immuneTime; }
										}
									}
								}
								dmgTot += got; hits += h;
							}
							double secs = window / 60.0;
							HlRow(run, "pvp", $"s={stage}\tgm={gm}\t{kind}\t{w.Name}\thit={w.Hit}\trule={rule}\tnif={(nif ? 1 : 0)}\tdef={p.statDefense}\tendur={p.endurance:0.###}\tdps={dmgTot / trials / secs:0.0}\tperHit={(hits > 0 ? dmgTot / hits : 0):0.0}\tlanded/s={hits / trials / secs:0.00}\tattempts/s={att / trials / secs:0.00}\timmuneTicksAfterHit={immSeen}");
						}
					}
				}
			}
			HlClearImmunity(p);
		}

		// ---------------------------------------------------------------------------------------------------- output
		private string _hlTag = "";
		private void HlRow(CrRun run, string kind, string body)
		{
			try
			{
				string path = _crOut + ".hl.tsv";
				File.AppendAllText(path, string.Join("\t", new[] { kind, run.Tag, run.Seed.ToString(), BtKv(run.Tp).GetValueOrDefault("s", ""), body }) + "\n");
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS-HL: row failed: " + e.Message); }
		}

		private void HlFinish(CrRun run, Dictionary<string, string> kv)
		{
			string G(string k, string d) => kv.TryGetValue(k, out var v) ? v : d;
			Player p = Main.player[0];
			string f(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
			if (_hlKind == "levy" || _hlKind == "enemy")
			{
				long taken = 0; int max = 0; foreach (var h in HlLanded) { taken += h.Dmg; max = Math.Max(max, h.Dmg); }
				int hp = (int)(kv.TryGetValue("ph", out var phv) ? float.Parse(phv, CultureInfo.InvariantCulture) : HlHp[_hlStage]);
				double secsRun = (_hlDeadTick >= 0 ? _hlDeadTick : (_hlTick - _hlMeasureStart)) / 60.0;
				int nProj = HlLanded.Count(h => h.Proj);
				string pr = string.Join(",", HlProjSpawn.Select(x => x.Key + ":" + (x.Value.n > 0 ? x.Value.dmg / x.Value.n : 0) + "x" + x.Value.n));
				var times = string.Join(",", HlLanded.Take(40).Select(h => (h.Tick - _hlMeasureStart) + ":" + h.Dmg));
				int npcDmg = _hlEnemies.Count > 0 ? _hlEnemies[0].damage : 0;
				HlRow(run, _hlKind, $"w={G("w", G("e", ""))}\tc={G("c", "")}\tn={G("n", "1")}\tdef={p.statDefense}\tendur={p.endurance:0.###}\thp={hp}\tdm={_hlHmP}/{_hlHmC}\tnif={(_hlNif ? 1 : 0)}\tttk={(_hlDeadTick >= 0 ? f(_hlDeadTick / 60.0) : "-1")}\ttaken={taken}\tlanded={HlLanded.Count}\tprojLanded={nProj}\tattempts={HlAttempts}\tshots={HlShots}\tfirstHit={(_hlFirstHit >= 0 ? f(_hlFirstHit / 60.0) : "-1")}\tmaxHit={max}\tmeanHit={(HlLanded.Count > 0 ? f((double)taken / HlLanded.Count) : "0")}\tdps={f(taken / Math.Max(0.5, secsRun))}\twindowS={f(secsRun)}\tsrcProj={(HlSrcPn > 0 ? f((double)HlSrcP / HlSrcPn) : "0")}/{HlSrcPn}\tsrcMelee={(HlSrcCn > 0 ? f((double)HlSrcC / HlSrcCn) : "0")}/{HlSrcCn}\tnpcDamage={npcDmg}\thostileProj={pr}\thits={times}");
			}
			HlOn = false;
			if (_hlSavedMyPlayer >= 0) { Main.myPlayer = _hlSavedMyPlayer; _hlSavedMyPlayer = -1; }
			else if (Main.netMode == NetmodeID.Server) Main.myPlayer = 255;
			var cols = CrHeader.Split('\t').Length; var v = new string[cols]; for (int i = 0; i < cols; i++) v[i] = "0";
			v[0] = run.Key; v[1] = run.Kind; v[2] = run.Boss; v[3] = run.Mode; v[4] = run.Order; v[5] = run.Seed.ToString(); v[6] = "HL"; v[7] = _hlTick.ToString(); v[8] = f(_hlTick / 60.0); v[cols - 1] = "hl";
			File.AppendAllText(_crOut, string.Join("\t", v) + "\n");
			Main.GameMode = GameModeID.Expert;
			_hlStarted = false; _cr = null; _crS = null; _crStarted = false; _crI++;
			ClearAll(); CrPurgeNpcs();
			HlLevyIdx.Clear(); HlProxyIdx = -1; HlPending.Clear(); HlActive = false;
		}
	}

	/// <summary>Hostile lab: the proxy NPC's hits become pending Player.Hurt calls (the strike runs HitEffect first, then OnHitByProjectile when a projectile did it).</summary>
	public class HlProxyHits : GlobalNPC
	{
		public override bool InstancePerEntity => false;
		public override void HitEffect(NPC npc, NPC.HitInfo hit)
		{
			if (!CombatHarness.HlOn || npc.whoAmI != CombatHarness.HlProxyIdx) return;
			CombatHarness.HlPending.Add(new CombatHarness.HlPend { Src = hit.SourceDamage, Dmg = hit.Damage, Proj = false, PType = 0 });
		}
		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			if (!CombatHarness.HlOn || npc.whoAmI != CombatHarness.HlProxyIdx || CombatHarness.HlPending.Count == 0) return;
			var last = CombatHarness.HlPending[CombatHarness.HlPending.Count - 1];
			last.Proj = true; last.PType = projectile.type; CombatHarness.HlPending[CombatHarness.HlPending.Count - 1] = last;
		}
	}

	/// <summary>Hostile lab: counts the shots the lab levies fire and the damage of every hostile projectile an enemy spawns.</summary>
	public class HlProjProbe : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		private bool _seen;
		public override void PostAI(Projectile p)
		{
			if (!CombatHarness.HlOn || _seen) return;
			_seen = true;
			if (p.friendly && !p.minion && p.damage > 0 && p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && CombatHarness.HlLevyIdx.Contains(tag.SoldierIndex)) CombatHarness.HlShots++;
			if (p.hostile && p.damage > 0)
			{
				CombatHarness.HlProjSpawn.TryGetValue(p.type, out var cur);
				CombatHarness.HlProjSpawn[p.type] = (cur.n + 1, cur.dmg + p.damage);
			}
		}
	}

	public class HlPlayerProbe : ModPlayer
	{
		public override void PostHurt(Player.HurtInfo info)
		{
			if (CombatHarness.HlOn && Player.whoAmI == 0) CombatHarness.HlOnHurt(info);
		}
	}
}
