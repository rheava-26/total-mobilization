using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// MEASUREMENT ONLY (env LEVY_FIXPROBE=1; does nothing otherwise). Per soldier projectile TYPE: how far from the soldier and from the real player it spawned (first tick), and at age 8 the angle between its heading and the
	/// direction to the soldier's target / to the commander's cursor. Also samples the live friendly-projectile count (total, soldier-tagged, top types) every 30 ticks. Written to the log every 300 ticks as
	/// FIXPROBE-SHOT / FIXPROBE-PROJ lines, which the lab reads.
	/// </summary>
	public class FixProbeProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		private int _age; private bool _first;

		public override void PostAI(Projectile p)
		{
			if (!FixProbe.On || !p.friendly) return;
			if (!p.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex < 0 || tag.SoldierIndex >= Main.maxNPCs) return;
			NPC sn = Main.npc[tag.SoldierIndex];
			if (!sn.active) return;
			_age++;
			if (!_first)
			{
				_first = true;
				Player real = Main.player[Math.Clamp(SoldierMinions.TrueMyPlayer == 255 ? 0 : SoldierMinions.TrueMyPlayer, 0, 254)];
				FixProbe.Shot(p).Spawn(Vector2.Distance(p.Center, sn.Center), real != null && real.active ? Vector2.Distance(p.Center, real.Center) : -1f);
				if (ExtraDamageClasses.IsRogue(p.DamageType))
				{
					FixProbe.RogueShots++;
					if (HarnessCalamityProbe.IsStealthStrike(p, out bool sub)) { if (sub) FixProbe.StealthSubs++; else FixProbe.StealthStrikes++; }
				}
			}
			if (_age == 8 && sn.ModNPC is SoldierNPC s && s.TargetIndex >= 0 && s.TargetIndex < Main.maxNPCs && Main.npc[s.TargetIndex].active && p.velocity.LengthSquared() > 0.25f)
			{
				Vector2 toT = Main.npc[s.TargetIndex].Center - p.Center;
				Vector2 toC = Main.MouseWorld - p.Center;
				FixProbe.Shot(p).Heading(Ang(p.velocity, toT), Ang(p.velocity, toC));
			}
		}

		private static float Ang(Vector2 a, Vector2 b)
		{
			if (a == Vector2.Zero || b == Vector2.Zero) return 0f;
			float d = Math.Abs(MathHelper.WrapAngle(a.ToRotation() - b.ToRotation()));
			return MathHelper.ToDegrees(d);
		}
	}

	public sealed class FixShot
	{
		public string Name; public int N, Headings, FarSoldier, FarReal;
		public double SumSold, SumReal, MaxSold, SumAngT, SumAngC; public int ToCursor;
		public void Spawn(float dSold, float dReal)
		{
			N++; SumSold += dSold; if (dSold > MaxSold) MaxSold = dSold;
			if (dSold > 160f) FarSoldier++;
			if (dReal >= 0f) { SumReal += dReal; if (dReal < dSold && dSold > 160f) FarReal++; }
		}
		public void Heading(float angT, float angC) { Headings++; SumAngT += angT; SumAngC += angC; if (angC < 12f && angT > 40f) ToCursor++; }
	}

	public class FixProbe : ModSystem
	{
		public static bool On;
		private static readonly Dictionary<string, FixShot> _shots = new();
		private static int _maxTotal, _maxSold, _samples; private static long _sumTotal, _sumSold; private static Dictionary<string, int> _topAtMax = new();
		private static uint _last;
		internal static double DrawProbeDist; internal static int DrawProbes, DrawProbeAtSoldier, DrawProbeNotRestored; internal static int[] MeleeModes = new int[4]; internal static int RogueShots, StealthStrikes, StealthSubs, RogueLevySamples, MeleeSamples, MeleeIdleSamples, MeleeAvoidSamples; internal static double MeleeBossTilesSum;
		private static uint _bossSeen;

		public override void Load() => On = Environment.GetEnvironmentVariable("LEVY_FIXPROBE") == "1";

		/// <summary>Lab: which modded projectile types read the owner's cursor (the ones the cursor puppet covers), how many per mod, and the flare-named ones.</summary>
		public override void PostAddRecipes()
		{
			if (!On || Environment.GetEnvironmentVariable("LEVY_FIXPROBE_SCAN") != "1") return;
			var perMod = new Dictionary<string, int>(); var flare = new List<string>(); int total = 0;
			var sw = System.Diagnostics.Stopwatch.StartNew();
			for (int t = ProjectileID.Count; t < ProjectileLoader.ProjectileCount; t++)
			{
				Projectile smp = WeaponClassifier.ProjectileSample(t);
				if (smp?.ModProjectile == null) continue;
				total++;
				if (!OwnerLockProbe.ReadsCursor(t)) continue;
				string mod = smp.ModProjectile.Mod.Name;
				perMod[mod] = perMod.TryGetValue(mod, out int c) ? c + 1 : 1;
				if (smp.ModProjectile.Name.IndexOf("flare", StringComparison.OrdinalIgnoreCase) >= 0) flare.Add(mod + "/" + smp.ModProjectile.Name);
			}
			// weapons whose shots fly through tiles (no line of sight needed), before (the shot the item names has tile collision off) and after (every shot it can put out, AI included)
			int oldN = 0, newN = 0, weapons = 0; var gained = new List<string>(); var lost = new List<string>();
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				Item it = ContentSamples.ItemsByType.TryGetValue(t, out var smp2) ? smp2 : null;
				if (it == null || it.IsAir || it.damage <= 0 || it.shoot <= 0 || it.accessory) continue;
				WeaponProfile prof = WeaponClassifier.GetProfile(it);
				if (!prof.Supported || !prof.IsRanged || prof.UsesAmmo) continue;
				weapons++;
				int proj = prof.BeamProjType > 0 ? prof.BeamProjType : prof.Shoot;
				Projectile ps = WeaponClassifier.ProjectileSample(proj);
				bool before = !prof.IsHeld && ps != null && !ps.tileCollide;
				bool after = ShotTiles.Passes(it, proj, prof.IsHeld);
				if (before) oldN++; if (after) newN++;
				string nm = (it.ModItem != null ? it.ModItem.Mod.Name + "/" + it.ModItem.Name : it.Name);
				if (after && !before) gained.Add(nm); else if (before && !after) lost.Add(nm);
			}
			Mod.Logger.Info($"FIXPROBE-SCAN-TILES weapons(no ammo)={weapons} passTilesBefore={oldN} passTilesAfter={newN} gained({gained.Count})=[{string.Join(" ", gained.Take(60))}] lost({lost.Count})=[{string.Join(" ", lost.Take(60))}]");
			Mod.Logger.Info($"FIXPROBE-SCAN modded projectiles={total} readCursor={string.Join(",", perMod.Select(kv => kv.Key + ":" + kv.Value))} flareNamed=[{string.Join(" ", flare)}] ms={sw.ElapsedMilliseconds}");
		}

		internal static FixShot Shot(Projectile p)
		{
			string n = p.ModProjectile != null ? p.ModProjectile.Mod.Name + "/" + p.ModProjectile.Name : "vanilla/" + p.type;
			if (!_shots.TryGetValue(n, out FixShot f)) _shots[n] = f = new FixShot { Name = n };
			return f;
		}

		public override void PostUpdateEverything()
		{
			if (!On || Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) return;
			uint now = Main.GameUpdateCount;
			if (now % 30 == 0)
			{
				int total = 0, sold = 0; var byType = new Dictionary<string, int>();
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active) continue;
					total++;
					if (p.friendly && p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex >= 0)
					{
						sold++;
						string n = p.ModProjectile != null ? p.ModProjectile.Name : "v" + p.type;
						byType[n] = byType.TryGetValue(n, out int c) ? c + 1 : 1;
					}
				}
				_samples++; _sumTotal += total; _sumSold += sold;
				if (total > _maxTotal) _maxTotal = total;
				if (sold > _maxSold) { _maxSold = sold; _topAtMax = byType; }
			}
			if (now % 30 == 7)
			{
				// the draw swap, run on every live chain projectile as the draw would: the owner slot must hold the soldier for it, and be the original again after
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active || p.owner != SoldierNPC.DummySlot) continue;
					HeldWeaponProjectile.ProbeDraw(p, out float d, out bool back); if (d >= 0f) { DrawProbes++; DrawProbeDist += d; if (d < 1f) DrawProbeAtSoldier++; }
					if (!back) DrawProbeNotRestored++;
				}
			}
			if (now % 30 == 15)
			{
				bool boss = SoldierTargeting.AnyBossNpcActive();
				if (!boss) _bossSeen = 0; else if (_bossSeen == 0) _bossSeen = now;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					if (Main.npc[i].ModNPC is not SoldierNPC s || !s.IsAlive) continue;
					if (s.HoldsRogueWeapon()) RogueLevySamples++;
					if (boss && now - _bossSeen > 300 && s.ProbeMelee(out float bt, out int since, out bool avoiding, out int mmode))
					{
						MeleeSamples++; MeleeBossTilesSum += bt;
						if (since > SoldierNPC.MeleeStallTicks) MeleeIdleSamples++;
						if (avoiding) MeleeAvoidSamples++;
						MeleeModes[mmode]++;
					}
				}
			}
			if (now - _last >= 300) Flush();
		}

		public static void Flush()
		{
			_last = Main.GameUpdateCount;
			var log = ModContent.GetInstance<Levy>()?.Logger; if (log == null) return;
			foreach (FixShot f in _shots.Values.OrderByDescending(x => x.N).Take(12))
				log.Info($"FIXPROBE-SHOT {f.Name} n={f.N} dSoldMean={f.SumSold / Math.Max(1, f.N):0} dSoldMax={f.MaxSold:0} dRealMean={f.SumReal / Math.Max(1, f.N):0} farFromSoldier={f.FarSoldier} nearerRealThanSoldier={f.FarReal} angToTargetMean={f.SumAngT / Math.Max(1, f.Headings):0.0} angToCursorMean={f.SumAngC / Math.Max(1, f.Headings):0.0} towardCursorNotTarget={f.ToCursor}/{f.Headings}");
			if (_samples > 0)
			{
				var top = string.Join(",", _topAtMax.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + ":" + kv.Value));
				log.Info($"FIXPROBE-PROJ samples={_samples} totalMean={_sumTotal / (double)_samples:0.0} totalMax={_maxTotal} soldierMean={_sumSold / (double)_samples:0.0} soldierMax={_maxSold} top={top} cap={Main.maxProjectiles}");
			}
			log.Info($"FIXPROBE-CURSOR cursorFallbacks={SoldierNPC.CursorFallbacks} cursorPuppets={SoldierNPC.CursorPuppets} steerNoTarget={SteerToTargetProjectile.SteerNoTarget} drawSwaps={CombatFix.DrawSwaps} drawProbes={DrawProbes} drawProbeAtSoldier={DrawProbeAtSoldier} drawProbeNotRestored={DrawProbeNotRestored} drawOwnerDistSum={DrawProbeDist:0}");
			log.Info($"FIXPROBE-BUDGET retiredArmy={PersistentShotBudget.RetiredArmy} retiredSoldier={PersistentShotBudget.RetiredSoldier} peakLingering={PersistentShotBudget.PeakLingering}");
			log.Info($"FIXPROBE-MELEE noTarget={MeleeModes[0]} bossHeldOff={MeleeModes[1]} bossEngaged={MeleeModes[2]} otherTarget={MeleeModes[3]} samples={MeleeSamples} idle={MeleeIdleSamples} avoiding={MeleeAvoidSamples} meanBossTiles={MeleeBossTilesSum / Math.Max(1, MeleeSamples):0.0} chargesStarted={SoldierNPC.ChargesStarted} chargeThinks={SoldierNPC.ChargeThinks} holdThinks={SoldierNPC.HoldThinks}");
			log.Info($"FIXPROBE-STEALTH holds={StealthPolicy.Holds} bossSaves={StealthPolicy.BossSaves} rogueShots={RogueShots} strikes={StealthStrikes} subs={StealthSubs} rogueLevySeconds={RogueLevySamples * 0.5:0.0} strikesPerRogueLevyMin={(RogueLevySamples > 0 ? StealthStrikes / (RogueLevySamples * 0.5 / 60.0) : 0):0.00}");
			SoldierNPC.CursorFallbacks = SoldierNPC.CursorPuppets = SoldierNPC.ChargesStarted = SoldierNPC.ChargeThinks = SoldierNPC.HoldThinks = 0; SteerToTargetProjectile.SteerNoTarget = 0; CombatFix.DrawSwaps = 0;
			PersistentShotBudget.RetiredArmy = PersistentShotBudget.RetiredSoldier = PersistentShotBudget.PeakLingering = 0; StealthPolicy.Holds = StealthPolicy.BossSaves = 0;
			RogueShots = StealthStrikes = StealthSubs = RogueLevySamples = MeleeSamples = MeleeIdleSamples = MeleeAvoidSamples = 0; Array.Clear(MeleeModes, 0, 4); DrawProbes = DrawProbeAtSoldier = DrawProbeNotRestored = 0; DrawProbeDist = 0; MeleeBossTilesSum = 0;
			_shots.Clear(); _samples = 0; _sumTotal = _sumSold = 0; _maxTotal = _maxSold = 0; _topAtMax = new();
		}

		public override void OnWorldUnload() { if (On) Flush(); }
	}
}
