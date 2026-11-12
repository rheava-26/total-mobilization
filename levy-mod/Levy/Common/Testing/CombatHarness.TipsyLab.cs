using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Weapons;
using Levy.Content.Buffs;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// TIPSY / AIM LAB (measurement only; a calrev `fight` line whose tokens carry tp=k=v;k=v;..., driven by levy-mod/lab/levylab through a `tipsy:` spec).
	/// One levy with one weapon stands on the arena floor and fires at an indestructible dummy a fixed distance away; the harness counts the damage the dummy takes in a fixed window, the projectiles the levy fires and how many of them hit.
	///   w=weapon  a=ammo  c=class R|M|E|S  d=distance in tiles  m=static|strafe (the dummy hovers, or moves on a Lissajous path with a peak speed of about 3 px per tick)  z=small|boss (a zombie-sized or an Eye-of-Cthulhu-sized hitbox)
	///   b=none|ale|stout|rum (the real drink buff, so the whole Tipsy path runs)  x=aim error multiplier applied on top through the Levy aim-accuracy seam (1 = none)  pin=1|0 (hold the levy at its spot; 0 lets a melee levy walk up)
	///   warm / win = settle ticks and measured ticks. Result rows go to LEVY_CR_OUT.tipsy.tsv (and a stub row to LEVY_CR_OUT so the driver counts the fight as finished).
	/// </summary>
	public partial class CombatHarness
	{
		private bool _tpStarted; private int _tpTick, _tpDummy = -1, _tpLife0, _tpFirstHit, _tpDistN, _tpAlive; private double _tpDist; private CrLevy _tpLevy; private float _tpPrevAcc = 1f;
		internal static int TpLevyIdx = -1, TpShots, TpProjHits, TpHits, TpStrikes; internal static int TpDummyIdx = -1; internal static bool TpMeasuring;

		private static int TpNpcType(string z) => z == "boss" ? NPCID.EyeofCthulhu : NPCID.Zombie;

		private void StepTipsy(CrRun run)
		{
			var kv = BtKv(run.Tp);
			string G(string k, string d) => kv.TryGetValue(k, out var v) ? v : d;
			float F(string k, float d) => kv.TryGetValue(k, out var v) ? float.Parse(v, CultureInfo.InvariantCulture) : d;
			int warm = (int)F("warm", 600), win = (int)F("win", 1200);
			float distT = F("d", 25f), x = F("x", 1f); string motion = G("m", "static"), size = G("z", "small"), drink = G("b", "none"), cls = G("c", "R");
			bool pin = G("pin", "1") == "1";
			bool rogue = kv.ContainsKey("rg"); // rogue lab: the levy is a Calamity rogue in Statigel (a stealth bar), and pol=spam|wait|auto forces its throw policy (Weapons/StealthPolicy.cs)
			float x0 = _crCx - 30f; // tiles; the levy stands here, the dummy distT tiles to its right
			if (!_tpStarted)
			{
				ClearAll(); CrPurgeNpcs(); CrApplyMode("exp"); _crRunCur = run;
				Main.rand = new Terraria.Utilities.UnifiedRandom(run.Seed * 7919 + 13);
				_crCmdPos = new Vector2((x0 - 3f) * 16f, CrF * 16f);
				CrPinCommander();
				_tpPrevAcc = WeaponUse.AimAccuracyFactor; WeaponUse.SetAimAccuracy(1f / Math.Max(0.05f, x));
				var spec = new CrLevySpec { Cls = cls[0], Weapon = G("w", "Minishark").Replace('_', ' '), Ammo = G("a", "").Replace('_', ' '), LifeCrystals = 15, ManaCrystals = 9, Potions = 0, Potion = "Healing Potion", ManAtArms = false };
				if (rogue) { spec.Cls = 'T'; spec.Armor = new[] { "CalamityMod/StatigelHeadRogue", "CalamityMod/StatigelArmor", "CalamityMod/StatigelGreaves" }; StealthPolicy.Force = G("pol", "auto") == "spam" ? 0 : G("pol", "auto") == "wait" ? 1 : G("pol", "auto") == "patient" ? 2 : -1; TpStrikes = 0; }
				_tpLevy = CrSpawnLevy(spec, run, 0, 1, new Vector2(x0 * 16f, CrF * 16f));
				NPC ln = Main.npc[_tpLevy.Idx]; var sn = (SoldierNPC)ln.ModNPC;
				if (drink != "none")
				{
					int type = DrinkBuffs.TypeOf(drink == "ale" ? 0 : drink == "stout" ? 1 : 2);
					ln.AddBuff(type, 60 * 60 * 5);
					if (!DrinkBuffs.IsDrunk(ln)) { ln.buffType[0] = type; ln.buffTime[0] = 60 * 60 * 5; }
					sn.StatsDirty = true;
				}
				TpLevyIdx = _tpLevy.Idx; TpShots = TpProjHits = TpHits = 0; TpMeasuring = false;
				bool boss = size == "boss"; float baseY = CrF - (boss ? 4.5f : 3.2f), amp = motion == "strafe" ? (boss ? 1.5f : 2.5f) : 0f, ampX = motion == "strafe" ? 8f : 0f;
				float bx = x0 + distT, ph = run.Seed * 1.3f;
				int i = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("LevyTipsy"), (int)(bx * 16f), (int)(baseY * 16f), TpNpcType(size));
				if (i < 0 || i >= Main.maxNPCs) throw new Exception("dummy spawn failed");
				NPC d = Main.npc[i]; var ht = d.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true;
				d.lifeMax = 100_000_000; d.life = d.lifeMax; d.damage = 0; d.knockBackResist = 0f; d.noGravity = true; d.noTileCollide = true; d.aiStyle = -1; d.friendly = false; d.dontTakeDamage = false; d.value = 0f; d.boss = rogue && size == "boss"; d.defense = 10; d.defDefense = 10;
				d.Center = new Vector2(bx * 16f, baseY * 16f); d.velocity = Vector2.Zero; ht.Pin = d.position;
				if (motion == "strafe")
				{
					float cx = bx * 16f, cy = baseY * 16f, ax = ampX * 16f, ay = amp * 16f;
					ht.Path = t => new Vector2(cx + ax * (float)Math.Sin(2 * Math.PI * t / 360.0 + ph), cy + ay * (float)Math.Sin(2 * Math.PI * t / 180.0 + 1.7 * ph)); // 6 s horizontal, 3 s vertical
				}
				d.netUpdate = true; _tpDummy = i; TpDummyIdx = i;
				_tpTick = 0; _tpStarted = true; _tpFirstHit = -1; _tpDist = 0; _tpDistN = 0; _tpAlive = 0; _tpLife0 = d.life;
				Mod.Logger.Info($"HARNESS-TP: start {run.Key} tp={run.Tp}");
				return;
			}
			_tpTick++;
			Main.dayTime = false; Main.time = 16200;
			CrPinCommander();
			CrPinStrays(_tpDummy);
			NPC dm = Main.npc[_tpDummy]; NPC lv = Main.npc[_tpLevy.Idx];
			bool body = _tpLevy.Rec.HasBody && lv.active && lv.ModNPC is SoldierNPC && lv.life > 0;
			if (body)
			{
				var sn = (SoldierNPC)lv.ModNPC; sn.Stations = Systems.StationFlags.Campfire | Systems.StationFlags.HeartLantern;
				if (pin) { lv.position.X = x0 * 16f - lv.width / 2f; lv.velocity.X = 0f; }
				_tpAlive++;
				if (_tpTick >= warm) { _tpDist += Vector2.Distance(lv.Center, dm.Center) / 16f; _tpDistN++; }
			}
			if (_tpTick == warm) { _tpLife0 = dm.life; TpShots = TpProjHits = TpHits = TpStrikes = 0; TpMeasuring = true; }
			if (_tpTick > warm && _tpFirstHit < 0 && dm.life < _tpLife0) _tpFirstHit = _tpTick - warm;
			if (_tpTick >= warm + win) TpFinish(run, dm, win, kv);
		}

		private void TpFinish(CrRun run, NPC dm, int win, Dictionary<string, string> kv)
		{
			long dmg = _tpLife0 - dm.life; double secs = win / 60.0;
			string f(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
			string G(string k, string d) => kv.TryGetValue(k, out var v) ? v : d;
			try
			{
				string path = _crOut + ".tipsy.tsv";
				if (!File.Exists(path)) File.WriteAllText(path, "key\ttag\tseed\tw\tc\td\tm\tz\tb\tx\tdps\tdmg\tshots\tprojHits\thits\tfirstHit\tavgDist\talivePct\tstrikes\tpol\n");
				File.AppendAllText(path, string.Join("\t", new[] { run.Key, run.Tag, run.Seed.ToString(), G("w", ""), G("c", ""), G("d", ""), G("m", ""), G("z", ""), G("b", "none"), G("x", "1"), f(dmg / secs), dmg.ToString(), TpShots.ToString(), TpProjHits.ToString(), TpHits.ToString(),
					_tpFirstHit < 0 ? "-1" : f(_tpFirstHit / 60.0), f(_tpDistN > 0 ? _tpDist / _tpDistN : -1), f(100.0 * _tpAlive / Math.Max(1, _tpTick)), TpStrikes.ToString(), G("pol", "") }) + "\n");
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS-TP: row failed: " + e.Message); }
			Mod.Logger.Info($"HARNESS-TP: done {run.Tag} seed {run.Seed}: dps {dmg / secs:0.0} shots {TpShots} hits {TpHits}");
			var cols = CrHeader.Split('\t').Length; var v = new string[cols]; for (int i = 0; i < cols; i++) v[i] = "0";
			v[0] = run.Key; v[1] = run.Kind; v[2] = run.Boss; v[3] = run.Mode; v[4] = run.Order; v[5] = run.Seed.ToString(); v[6] = "TIPSY"; v[7] = win.ToString(); v[8] = f(secs); v[cols - 1] = "tipsy";
			File.AppendAllText(_crOut, string.Join("\t", v) + "\n");
			WeaponUse.SetAimAccuracy(_tpPrevAcc <= 0f ? 1f : 1f / _tpPrevAcc);
			TpMeasuring = false; TpLevyIdx = -1; TpDummyIdx = -1; StealthPolicy.Force = -1;
			_tpStarted = false; _cr = null; _crS = null; _crStarted = false; _crI++;
			ClearAll(); CrPurgeNpcs();
		}
	}

	/// <summary>Tipsy lab counters: projectiles a levy fires (counted on their first tick alive) and how many of them land on the dummy.</summary>
	public class TipsyProbe : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		private bool _seen, _hit;
		public override void PostAI(Projectile p)
		{
			if (!CombatHarness.TpMeasuring || _seen) return;
			_seen = true;
			if (!p.friendly || p.minion || p.damage <= 0) return;
			if (Terraria.ModLoader.ModLoader.HasMod("CalamityMod") && p.TryGetGlobalProjectile(out SoldierProjectileTag tag0) && tag0.SoldierIndex == CombatHarness.TpLevyIdx && StrikeFlag(p)) CombatHarness.TpStrikes++;
			if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex == CombatHarness.TpLevyIdx) CombatHarness.TpShots++;
		}
		private static bool StrikeFlag(Projectile p) => HarnessCalamityProbe.IsStealthStrike(p, out bool sub) && !sub;
		public override void OnHitNPC(Projectile p, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!CombatHarness.TpMeasuring || target.whoAmI != CombatHarness.TpDummyIdx) return;
			CombatHarness.TpHits++;
			if (!_hit && p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex == CombatHarness.TpLevyIdx) { _hit = true; CombatHarness.TpProjHits++; }
		}
	}
}
