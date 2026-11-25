using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Common.Soldiers;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// The combatfix part of the SCal arena check, logged as "[CombatFix]" lines. Supreme Calamitas's bullet hell: Calamity writes each shot relative to her target, so with a levy as her target the ring is centred on that levy
	/// and threatens only him. Three runs of the same scene (the real SCal over a built arena, her opening bullet hell): with the arena rule off (BEFORE), on (NOW), and on with no levy in the fight (SOLO, which must be vanilla).
	/// Measured per run: the bullet-hell shots Calamity asked for and where they were born against the box, the share born inside the box, the width of the arena their birth places cover, and which levies a shot's flight crossed
	/// (shots are followed by their velocity; levies stand across the floor, so the count of levies a flight crossed says whether the whole arena was threatened).
	/// </summary>
	public sealed partial class ScalArenaWorldCheck
	{
		private static int _cfPass, _cfFail;
		internal static void CF(string name, bool ok) { if (ok) _cfPass++; else _cfFail++; _mod.Logger.Info($"[CombatFix] {(ok ? "PASS" : "FAIL")} {name}"); }
		internal static void CFNote(string s) => _mod.Logger.Info("[CombatFix] OBSERVED " + s);
		internal static (int pass, int fail) CFTotals => (_cfPass, _cfFail);

		private sealed class BhResult
		{
			public int Asked, Moved, InsideBox, Ticks; public double CoverX, CoverY; public int LeviesCrossed, Levies; public string Shape = "";
			public int AboveBox, BelowBox, LeftOfBox, RightOfBox; public float BoxW, BoxH;
		}

		private static BhResult BhRun(string label, bool rule, bool army, int ticks)
		{
			var r = new BhResult();
			Cleanup(); _shape = Shape.Natural;
			Terraria.Main.rand = new Terraria.Utilities.UnifiedRandom(4242);
			Arena();
			Main.GameMode = GameModeID.Master; Main.hardMode = true;
			SetRevenge(true, true);
			var cfg = global::Levy.Common.Config.LevyConfig.Get; if (cfg != null) { _savedBossTarget ??= cfg.BossesTargetSoldiers; cfg.BossesTargetSoldiers = true; }
			ModContent.TryFind("CalamityMod/SupremeCalamitas", out ModNPC sc);
			BossDownedSystem.SetDownedForTest(sc.Type, true);
			_lifted = false; _box = null; _haveBox = false;
			_cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f); _cmdPrev = _cmdAt;
			KeepCommander(); Step(2);
			var levies = new List<SoldierNPC>();
			if (army)
			{
				// eight levies across the floor, wall to wall: the ring is centred on one of them
				for (int k = 0; k < 8; k++)
				{
					float off = (k - 3.5f) * 14f;
					var s = Levy(k, k % 2 == 0 ? Cls.Foot : Cls.Hooker, new Vector2((Cx + off) * 16f, FloorY * 16f)); if (s != null) levies.Add(s);
				}
			}
			Step(60);
			ScalBulletHell.On = rule;
			ScalBulletHell.Log = new List<(Vector2 asked, Vector2 born, bool moved)>();
			long moved0 = ScalBulletHell.Moved, solo0 = ScalBulletHell.SoloKept;
			Vector2 spawnAt = new Vector2(Cx * 16f, (FloorY - 10) * 16f);
			int bi = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("CombatFix"), (int)spawnAt.X, (int)spawnAt.Y, sc.Type, 1);
			if (bi < 0 || bi >= Main.maxNPCs) { CF("Supreme Calamitas spawns", false); return r; }
			NPC boss = Main.npc[bi]; boss.timeLeft = 99999; boss.netOffset = Vector2.Zero;
			var crossed = new HashSet<int>();
			var seen = new HashSet<int>();   // projectile identities already followed
			var born = new List<Vector2>();
			for (int t = 0; t < ticks; t++)
			{
				_cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f);
				foreach (var s0 in levies) { NPC n0 = s0.NPC; if (n0.active) { n0.dontTakeDamage = true; n0.life = n0.lifeMax; s0.Record.CurrentHp = n0.lifeMax; } }
				Step(1);
				if (!boss.active || boss.type != sc.Type) break;
				boss.timeLeft = 99999;
				r.Ticks++;
				// a levy's body crossed by a hostile bullet-hell shot (Brimstone hellblast and wave families) this tick
				for (int pj = 0; pj < Main.maxProjectiles; pj++)
				{
					Projectile pr = Main.projectile[pj];
					if (!pr.active || !pr.hostile || pr.damage <= 0) continue;
					string nm = pr.ModProjectile?.Name ?? ""; if (!nm.StartsWith("BrimstoneHellblast") && !nm.StartsWith("BrimstoneWave") && !nm.StartsWith("BrimstoneBarrage")) continue;
					for (int li = 0; li < levies.Count; li++) { NPC n = levies[li].NPC; if (n.active && pr.Hitbox.Intersects(n.Hitbox)) crossed.Add(li); }
				}
			}
			var log = ScalBulletHell.Log; ScalBulletHell.Log = null; ScalBulletHell.On = true;
			r.Asked = log.Count; r.Moved = log.Count(l => l.moved); r.Levies = levies.Count; r.LeviesCrossed = crossed.Count;
			if (_haveBox)
			{
				r.BoxW = _hi.X - _lo.X; r.BoxH = _hi.Y - _lo.Y;
				foreach (var l in log)
				{
					Vector2 b = l.born; born.Add(b);
					bool inside = b.X > _lo.X && b.X < _hi.X && b.Y > _lo.Y && b.Y < _hi.Y;
					if (inside) r.InsideBox++;
					if (b.Y <= _lo.Y) r.AboveBox++; else if (b.Y >= _hi.Y) r.BelowBox++;
					if (b.X <= _lo.X) r.LeftOfBox++; else if (b.X >= _hi.X) r.RightOfBox++;
				}
				if (born.Count > 1)
				{
					float x0 = born.Min(p => p.X), x1 = born.Max(p => p.X), y0 = born.Min(p => p.Y), y1 = born.Max(p => p.Y);
					r.CoverX = Math.Min(1.0, (x1 - x0) / Math.Max(1f, r.BoxW)); r.CoverY = Math.Min(1.0, (y1 - y0) / Math.Max(1f, r.BoxH));
				}
			}
			CFNote($"scal bullet hell [{label}] ticks {r.Ticks}, shots asked {r.Asked}, moved to the arena {r.Moved}, born inside the box {r.InsideBox}, born over/under/left/right of it {r.AboveBox}/{r.BelowBox}/{r.LeftOfBox}/{r.RightOfBox}; birth places cover {F(r.CoverX * 100)}% of the box's width and {F(r.CoverY * 100)}% of its height (box {r.BoxW / 16f:0}x{r.BoxH / 16f:0} tiles); levies a shot's flight crossed {r.LeviesCrossed} of {r.Levies}; solo-kept {ScalBulletHell.SoloKept - solo0}");
			Cleanup();
			return r;
		}

		/// <summary>Supreme Calamitas's bullet hell: the ring is the arena's while an army fights her, and vanilla when none does.</summary>
		private static void CombatFixScal()
		{
			if (!ScalBulletHell.Installed) { CF("the bullet-hell hook is installed", false); return; }
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_COMBATFIX_BHTICKS"), out int tk) ? tk : 900;
			var before = BhRun("BEFORE: army, arena rule off", false, true, ticks);
			var now = BhRun("NOW: army, arena rule on", true, true, ticks);
			var solo = BhRun("SOLO: no levy in the fight, arena rule on", true, false, ticks);
			CF($"a fight with an army: the opening bullet hell fires shots (before {before.Asked}, now {now.Asked})", before.Asked >= 20 && now.Asked >= 20);
			CF($"BEFORE the rule the ring follows the target: {before.Moved} shots moved, {before.InsideBox} of {before.Asked} born inside the box", before.Moved == 0 && before.Asked > 0);
			CF($"NOW every shot of the ring is born at the arena's walls, none inside it ({now.Moved} of {now.Asked} moved, {now.InsideBox} born inside the box)", now.Asked > 0 && now.Moved == now.Asked && now.InsideBox == 0);
			CF($"NOW the shots come from every direction: over {now.AboveBox}, left {now.LeftOfBox}, right {now.RightOfBox}", now.AboveBox > 0 && now.LeftOfBox > 0 && now.RightOfBox > 0);
			CF($"NOW the shots' flights cross more of the army: {now.LeviesCrossed} of {now.Levies} levies against {before.LeviesCrossed} before", now.LeviesCrossed >= Math.Min(now.Levies, 6));
			CF($"SOLO play is vanilla: no shot moved ({solo.Moved}), every one born where Calamity put it ({solo.Asked} shots)", solo.Moved == 0);
		}
	}
}
