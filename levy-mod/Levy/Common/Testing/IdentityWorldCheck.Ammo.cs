using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- AMMO WORLD: special ammo does in a levy's gun what it does in a player's
		// Logged as "[AmmoWorld]" lines with its own summary. For each special ammo (vanilla on the vanilla half, every Calamity bullet and arrow on the Calamity half) one gun or bow fires it for a window twice:
		//   LEVY:   one levy holds its ground 12 tiles from a pinned target, the stand-in commander (slot 0, the single-player owner: Main.myPlayer = 0 while projectiles update) stands 64 tiles to the LEFT with her cursor on herself;
		//   PLAYER: no levy, the commander herself fires the same gun and ammo from the levy's spot with her cursor on the target (her real ItemCheck).
		// Every friendly projectile is followed from the tick NewProjectile made it: where it was born (against the shooter and the commander), its parent, how long it lived, any jump in one tick far beyond its speed (a warp),
		// and every hit it lands (count, damage). The checks: damage per shot within 25% of the player's, nothing born at or warped to the commander, every child born near the shot that made it, no lifetime beyond the player's.
		// Env: LEVY_AMMOWORLD=0 skips, LEVY_AMMOWORLD_ALONE=1 runs only this, LEVY_AMMO_TICKS=N (window, default 600), LEVY_AMMO_ONLY=Name;Name (ammo item names), LEVY_AMMO_DEBUG=1 (per-projectile lines).

		private static int _awPass, _awFail;
		private static void ACheck(string name, bool ok) { if (ok) _awPass++; else _awFail++; _mod.Logger.Info($"[AmmoWorld] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void ANote(string s) => _mod.Logger.Info("[AmmoWorld] OBSERVED " + s);
		private static readonly bool AmmoDebug = Environment.GetEnvironmentVariable("LEVY_AMMO_DEBUG") == "1";
		private const int AmmoCmdTiles = 64, AmmoNearCmdTiles = 10;
		/// <summary>Lab: the age (ticks) at which LEVY_AMMO_DEBUG notes a shot's place (LEVY_AMMO_DEBUGAGE), and the offset of the commander's Calamity cursor from her in a levy run (LEVY_AMMO_CMDCURSOR=x,y in pixels; default 0: on herself; her screen cursor stays on her).</summary>
		private static readonly int AmmoDebugAge = int.TryParse(Environment.GetEnvironmentVariable("LEVY_AMMO_DEBUGAGE"), out int dage) ? dage : 30;
		private static Vector2? AmmoCmdCursor = ParseCursor(Environment.GetEnvironmentVariable("LEVY_AMMO_CMDCURSOR"));
		private static Vector2? ParseCursor(string v) { if (string.IsNullOrEmpty(v)) return null; var a = v.Split(','); return a.Length == 2 && float.TryParse(a[0], out float x) && float.TryParse(a[1], out float y) ? new Vector2(x, y) : null; }

		private sealed class AmmoType
		{
			public int Born, Children, AtCmd, Untagged, Died, Jumps, JumpsToCmd, Hits, NearCmdTicks, OnCmdTicks; public long LifeSum, HitDamage; public int MaxLife; public float MaxJump, MaxChildFromParent, MinFromCmd = 1e9f, MaxBornFromShooter;
		}

		private sealed class AmmoStat
		{
			public string Label, Mode, Err = ""; public long Damage; public int Ticks, Shots; public int ShotType; public double SumX, SumN; public float LevyX => SumN > 0 ? (float)(SumX / SumN) : 0f;
			public Dictionary<string, AmmoType> ByType = new();
			public int AtCmd => ByType.Values.Sum(v => v.AtCmd);
			public int JumpsToCmd => ByType.Values.Sum(v => v.JumpsToCmd);
			public int NearCmd => ByType.Values.Sum(v => v.NearCmdTicks);
			/// <summary>Ticks a shot of the levy's spent within 4 tiles of the commander (where a shot that goes to her cursor ends up).</summary>
			public int OnCmd => ByType.Values.Sum(v => v.OnCmdTicks);
			public int Untagged => ByType.Values.Sum(v => v.Untagged);
			public float MaxChildFromParent => ByType.Count == 0 ? 0f : ByType.Values.Max(v => v.MaxChildFromParent);
			public long HitDamage; /// <summary>Hit damage (every hit the shot and its children landed, without damage over time) per shot fired.</summary>
			/// <summary>Gun parity: the tick of the first and last shot (any root projectile that is not the gun's own holdout), how many, and the target's damage from the first shot on.</summary>
			public int FirstShot = -1, LastShot = -1, Roots; public long DamageAfter; public List<string> ShotTicks = new();
			public int Window => FirstShot < 0 ? 0 : Ticks - FirstShot;
			public double DpsAfter => Window > 0 ? DamageAfter * 60.0 / Window : 0;
			public double RatePerSec => Window > 0 ? Roots * 60.0 / Window : 0;
			public double PerShot => Shots > 0 ? HitDamage / (double)Shots : 0;
			public double Dps => Ticks > 0 ? Damage * 60.0 / Ticks : 0;
			public override string ToString() => $"{Label} [{Mode}] ticks={Ticks} shots={Shots} dmg={Damage} hitDmg={HitDamage} perShot={PerShot:0.0} dps={Dps:0} atCmd={AtCmd} jumpsToCmd={JumpsToCmd} nearCmdTicks={NearCmd} untagged={Untagged}{(Err.Length > 0 ? " ERR " + Err : "")} | "
				+ string.Join("; ", ByType.OrderByDescending(kv => kv.Value.Born).Take(6).Select(kv => $"{kv.Key} born={kv.Value.Born} (child {kv.Value.Children}, maxFromShooter {kv.Value.MaxBornFromShooter / 16f:0.0}t, maxFromParent {kv.Value.MaxChildFromParent / 16f:0.0}t) life avg={(kv.Value.Died > 0 ? kv.Value.LifeSum / (double)kv.Value.Died : 0):0.0} max={kv.Value.MaxLife} hits={kv.Value.Hits} hitDmg={kv.Value.HitDamage} jumps={kv.Value.Jumps} (max {kv.Value.MaxJump / 16f:0.0}t, to cmd {kv.Value.JumpsToCmd})"));
		}

		private sealed class AmmoLive { public int Type, Born, Index; public long Serial; public Vector2 Last; public float LastSpeed; public string Name; }

		/// <summary>One gun + ammo for <paramref name="ticks"/> ticks, fired by one levy (<paramref name="player"/> false) or by the commander from the levy's spot.</summary>
		private static AmmoStat AmmoRun(int weaponType, int ammoType, string label, bool player, int ticks, float standX = 0f, int levies = 1, int cmdGun = 0, int cmdAmmo = 0, int pressEvery = 0, Vector2? levyCmdAt = null)
		{
			var st = new AmmoStat { Label = label, Mode = player ? "player" : levies > 1 ? levies + " levies" : "levy" };
			Item ammoSample = ContentSamples.ItemsByType[ammoType];
			st.ShotType = ammoSample.shoot;
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			_cmdAt = player ? new Vector2(standX > 0f ? standX : at.X, at.Y - 24f) : levyCmdAt ?? HoldCmdAt(); // the player stands where the levy stood (on average) while it fired
			ClearSoldiers(); _cmdHolds = false; ReleaseCommanderButtons(); ClearRogueAdds();
			Player cmd = Main.player[0]; cmd.inventory[0] = new Item(); cmd.heldProj = -1; cmd.itemAnimation = 0; cmd.itemTime = 0;
			for (int s = 50; s < 58; s++) cmd.inventory[s] = new Item();
			SpawnOriginTarget(at);
			_sweepVel = Vector2.Zero;
			int body = -1; var bodies = new List<int>();
			for (int k = 0; !player && k < levies; k++)
			{
				Item w = new Item(); w.SetDefaults(weaponType);
				SoldierRecord rec = MakeOriginRecord(w, ammoType); rec.Name = "Pvt. Ammo" + k;
				rec.Gear.Ammo.stack = rec.Gear.Ammo.maxStack;
				Vector2 pos = levies <= 1 ? at : at + new Vector2((k % 2 == 0 ? -1 : 1) * 24f * (1 + k / 2), 0f); // an army stands around the one levy's spot, as near the target on average
				rec.Order = SoldierOrder.Hold; rec.HoldPos = pos;
				RosterSystem.Instance.Regulars.Add(rec);
				int b = RosterSystem.Instance.SpawnBody(rec, pos);
				if (b < 0 || !rec.HasBody) throw new Exception("levy did not spawn");
				Main.npc[b].Bottom = pos; bodies.Add(b); if (body < 0) body = b;
			}
			Item cw = null;
			if (player) { cw = new Item(); cw.SetDefaults(weaponType); Item am = new Item(); am.SetDefaults(ammoType); am.stack = am.maxStack; cmd.inventory[54] = am; }
			else if (cmdGun > 0) { cw = new Item(); cw.SetDefaults(cmdGun); Item am = new Item(); am.SetDefaults(cmdAmmo); am.stack = am.maxStack; cmd.inventory[54] = am; st.Mode += " + the commander firing " + ItemName(cmdGun) + (cmdAmmo > 0 ? "/" + ItemName(cmdAmmo) : "") + " at the target"; }
			Main.rand = new Terraria.Utilities.UnifiedRandom(4242);
			KeepCommander(); PinSweepTarget(); CursorOnCommander(); Step(10);
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			var live = new Dictionary<int, AmmoLive>(); var lastSerial = new long[Main.maxProjectiles];
			var lastPos = new Dictionary<long, Vector2>();
			AmmoProbe.Reset(); AmmoProbe.On = true; AmmoProbe.ArmyOnly = !player && cw != null; // the commander's own hits are left out while she fires too
			int dbg = 0;
			try
			{
				for (int k = 0; k < ticks; k++)
				{
					string dbgUse = AmmoDebug && player && k < 24 ? $"t={k} before {cmd.itemAnimation}/{cmd.itemAnimationMax},{cmd.itemTime}/{cmd.itemTimeMax}" : null;
					if (player) { try { HoldCommanderUse(cw, pressEvery > 0 && !cw.channel && !ModShotKinds.HoldSpawned(cw) && ModShotKinds.HolderType(cw) == cw.shoot ? k % pressEvery == 0 : cw.autoReuse || cmd.itemAnimation > 1 || k % 2 == 0); /* a held gun is held down */ } catch (Exception e) { st.Err = "cmd use " + (e is TargetInvocationException && e.InnerException != null ? e.InnerException.GetType().Name : e.GetType().Name); } }
					else if (cw != null) { try { HoldCommanderUse(cw, true); } catch (Exception e) { st.Err = "cmd use " + (e is TargetInvocationException && e.InnerException != null ? e.InnerException.GetType().Name : e.GetType().Name); } }
					else { CursorOnCommander(); if (AmmoCmdCursor.HasValue) { Player c0 = Main.player[0]; try { object cp = AltPolicies.CalamityPlayerOf(c0); if (cp != null) AltPolicies.SetCalAimPublic(cp, AmmoCmdCursor.Value); } catch (Exception) { } } }
					if (!player && cw != null && cmd.inventory[54] != null && !cmd.inventory[54].IsAir) cmd.inventory[54].stack = cmd.inventory[54].maxStack;
					if (!player) foreach (var r in RosterSystem.Instance.Regulars) { if (r.Gear.Ammo != null && !r.Gear.Ammo.IsAir) r.Gear.Ammo.stack = r.Gear.Ammo.maxStack; }
					else if (cmd.inventory[54] != null && !cmd.inventory[54].IsAir) cmd.inventory[54].stack = cmd.inventory[54].maxStack;
					Main.dayTime = true; Main.time = 27000; // noon: no falling stars
					if (dbgUse != null) dbgUse += $" afterUse {cmd.itemAnimation},{cmd.itemTime} press={Main.mouseLeft} ctl={cmd.controlUseItem} rel={cmd.releaseUseItem}";
					Step(1); ClearNaturalHostiles();
					if (dbgUse != null) ANote($"  dbg use {label}: {dbgUse} afterStep {cmd.itemAnimation},{cmd.itemTime} roots={AmmoProbe.Roots}");
					if (_sweepTarget != null && _sweepTarget.active) { int d = _sweepTarget.lifeMax - _sweepTarget.life; if (d > 0) { st.Damage += d; if (st.FirstShot >= 0) st.DamageAfter += d; } }
					if (AmmoProbe.Roots > st.Roots) { if (st.FirstShot < 0) { st.FirstShot = k; if (_sweepTarget != null && _sweepTarget.active && _sweepTarget.lifeMax > _sweepTarget.life) st.DamageAfter += _sweepTarget.lifeMax - _sweepTarget.life; } st.LastShot = k; if (st.ShotTicks.Count < 14) st.ShotTicks.Add(k + (AmmoProbe.Roots - st.Roots > 1 ? "x" + (AmmoProbe.Roots - st.Roots) : "") + "/" + Main.player[0].itemAnimation + "," + Main.player[0].itemTime); st.Roots = AmmoProbe.Roots; }
					st.Ticks++;
					if (AmmoDebug && levies > 1 && k < 40 && _sweepTarget != null) { var im = new List<string>(); for (int j = 0; j < 256; j++) if (_sweepTarget.immune[j] > 0) im.Add(j + ":" + _sweepTarget.immune[j]); ANote($"  dbg {label} [{st.Mode}] t={k} target immune [{string.Join(",", im)}] hits so far {AmmoProbe.Hits.Values.Sum(h => h.n)}"); }
					Vector2 cmdC = Main.player[0].Center;
					Vector2 shooter = player ? cmdC : (body >= 0 && Main.npc[body].active ? Main.npc[body].Center : at);
					if (!player && k >= 30) { st.SumX += shooter.X; st.SumN++; }
					// deaths: a slot that went inactive or was refilled
					foreach (int slot in live.Keys.ToList())
					{
						Projectile q = Main.projectile[slot]; AmmoLive l = live[slot];
						bool same = q.active && q.type == l.Type && (!AmmoProbe.Births.TryGetValue(slot, out var bb) || bb.serial == l.Serial);
						if (same) continue;
						live.Remove(slot);
						if (st.ByType.TryGetValue(l.Name, out AmmoType t)) { t.Died++; int life = k - l.Born; t.LifeSum += life; t.MaxLife = Math.Max(t.MaxLife, life); }
					}
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile p = Main.projectile[i];
						if (!p.active || p.hostile || !p.friendly && p.damage <= 0) continue;
						if (!player && cw != null && !(p.TryGetGlobalProjectile(out SoldierProjectileTag ct) && ct.SoldierIndex >= 0)) continue; // the commander's own shots: not the levies'

						bool probed = AmmoProbe.Births.TryGetValue(i, out var ba) && ba.type == p.type;
						string pn = p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type);
						bool tagged = p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0 && bodies.Contains(tag.SoldierIndex);
						if (probed && ba.serial != lastSerial[i])
						{
							lastSerial[i] = ba.serial;
							var born = ba;
							if (!st.ByType.TryGetValue(pn, out AmmoType t)) st.ByType[pn] = t = new AmmoType();
							t.Born++;
							float dsh = Vector2.Distance(ba.at, shooter), dc = Vector2.Distance(ba.at, cmdC);
							bool child = born.parentProj && !born.parentHolder;
							if (child) { t.Children++; t.MaxChildFromParent = Math.Max(t.MaxChildFromParent, Vector2.Distance(ba.at, born.parentAt)); }
							else t.MaxBornFromShooter = Math.Max(t.MaxBornFromShooter, dsh);
							if (!player && dc < AmmoNearCmdTiles * 16f && dc < dsh) t.AtCmd++;
							if (!player && !tagged) t.Untagged++;
							t.MinFromCmd = Math.Min(t.MinFromCmd, dc);
							live[i] = new AmmoLive { Type = p.type, Born = k, Index = t.Born, Serial = ba.serial, Last = p.Center, LastSpeed = p.velocity.Length() * (1 + p.extraUpdates), Name = pn };
							if (AmmoDebug && dbg++ < 60) ANote($"  dbg {label} [{st.Mode}] t={k} born {pn}{(child ? " child of " + born.parentName : "")} fromShooter={dsh / 16f:0.0}t fromCmd={dc / 16f:0.0}t dmg={p.damage} pen={p.penetrate} tl={p.timeLeft} upd={1 + p.extraUpdates} tagged={tagged}");
							continue;
						}
						if (!live.TryGetValue(i, out AmmoLive lv) || lv.Type != p.type) continue;
						AmmoType at2 = st.ByType[lv.Name];
						float moved = Vector2.Distance(p.Center, lv.Last), speed = Math.Max(lv.LastSpeed, p.velocity.Length() * (1 + p.extraUpdates));
						if (moved > speed * 2f + 64f)
						{
							at2.Jumps++; at2.MaxJump = Math.Max(at2.MaxJump, moved);
							float jc = Vector2.Distance(p.Center, cmdC);
							if (!player && jc < AmmoNearCmdTiles * 16f) at2.JumpsToCmd++;
							if (AmmoDebug && dbg++ < 60) ANote($"  dbg {label} [{st.Mode}] t={k} {pn} jumped {moved / 16f:0.0}t to {(p.Center.X - shooter.X) / 16f:0.0},{(p.Center.Y - shooter.Y) / 16f:0.0}t from the shooter, {jc / 16f:0.0}t from the commander, ai=({p.ai[0]:0.#},{p.ai[1]:0.#}) vel=({p.velocity.X:0.0},{p.velocity.Y:0.0}) dmg={p.damage} pen={p.penetrate}");
						}
						if (!player && Vector2.Distance(p.Center, cmdC) < AmmoNearCmdTiles * 16f) { at2.NearCmdTicks++; if (Vector2.Distance(p.Center, cmdC) < 4 * 16f) at2.OnCmdTicks++; if (AmmoDebug && at2.NearCmdTicks <= 3) ANote($"  dbg {label} [{st.Mode}] t={k} {pn} near the commander: {(p.Center.X - cmdC.X) / 16f:0.0},{(p.Center.Y - cmdC.Y) / 16f:0.0}t from her, born t={lv.Born}, vel=({p.velocity.X:0.0},{p.velocity.Y:0.0}) ai=({p.ai[0]:0.#},{p.ai[1]:0.#},{p.ai[2]:0.#}) tl={p.timeLeft} owner={p.owner}"); }
						else if (AmmoDebug && !player && k - lv.Born == AmmoDebugAge && lv.Index <= 6) ANote($"  dbg {label} [{st.Mode}] t={k} {pn} {AmmoDebugAge} ticks old: {(p.Center.X - shooter.X) / 16f:0.0},{(p.Center.Y - shooter.Y) / 16f:0.0}t from the shooter, {(p.Center.X - _sweepTargetAt.X) / 16f:0.0},{(p.Center.Y - _sweepTargetAt.Y) / 16f:0.0}t from the target's feet, {(p.Center.X - cmdC.X) / 16f:0.0},{(p.Center.Y - cmdC.Y) / 16f:0.0}t from her, vel=({p.velocity.X:0.0},{p.velocity.Y:0.0}) tl={p.timeLeft} owner={p.owner}");
						lv.Last = p.Center; lv.LastSpeed = p.velocity.Length() * (1 + p.extraUpdates);
					}
				}
			}
			finally { AmmoProbe.On = false; _parityAim = null; _cmdHolds = false; }
			st.Shots = AmmoProbe.Shots.TryGetValue(st.ShotType, out int fired) ? fired : 0; // counted as NewProjectile makes them (a shot that dies inside its first tick is never seen by a scan)
			foreach (var kv in AmmoProbe.Hits) { st.HitDamage += kv.Value.dmg; string n = ProjName(kv.Key); if (!st.ByType.TryGetValue(n, out AmmoType t)) st.ByType[n] = t = new AmmoType(); t.Hits += kv.Value.n; t.HitDamage += kv.Value.dmg; }
			if (cw != null) { try { MegaCommanderUse(cw, false); } catch (Exception) { } Main.mouseLeft = false; Main.mouseLeftRelease = true; }
			ClearSoldiers(); ClearRogueAdds(); ReleaseCommanderButtons(); cmd.inventory[0] = new Item(); cmd.inventory[54] = new Item(); cmd.heldProj = -1; cmd.itemAnimation = 0; cmd.itemTime = 0;
			return st;
		}

		/// <summary>Every flamethrower a levy can be dealt: the gel guns of every loaded mod (useAmmo Gel) and any weapon whose shot runs the vanilla Flamethrower AI.</summary>
		private static List<(int type, string label)> FlameWeapons()
		{
			var list = new List<(int, string)>(); var probe = new SoldierGear();
			string only = Environment.GetEnvironmentVariable("LEVY_FLAME_ONLY");
			string[] names = string.IsNullOrEmpty(only) ? null : only.Split(';', StringSplitOptions.RemoveEmptyEntries);
			for (int i = 1; i < ItemLoader.ItemCount; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item s) || s == null || s.IsAir || s.damage <= 0 || s.shoot <= 0 || s.ammo > 0) continue;
				string mod = s.ModItem?.Mod.Name ?? "Terraria";
				if (mod == "Levy" || mod == "Mechanization" || mod == "Warbands") continue;
				string name = s.ModItem?.Name ?? ItemID.Search.GetName(i);
				if (names != null) { if (!names.Contains(name)) continue; }
				else
				{
					bool gel = s.useAmmo == AmmoID.Gel;
					bool flameAi = false; try { flameAi = WeaponClassifier.ProjectileSample(s.shoot)?.aiStyle == ProjAIStyleID.Flamethrower; } catch (Exception) { }
					if (!gel && !flameAi) continue;
				}
				Item it = new Item(); it.SetDefaults(i);
				if (!probe.CanHold(SoldierGear.SlotWeapon, it)) continue;
				list.Add((i, mod + ":" + name));
			}
			return list;
		}

		/// <summary>
		/// THE FLAME SWEEP (part 2 of the ammo world check): every flamethrower is carried by one levy holding its ground 12 tiles (or its reach) from the pinned target, the commander 64 tiles to the left. Every projectile it makes must be the
		/// levy's, born near him and never at her, and it must hit. With LEVY_FLAME_PARITY=1 the commander also fires each one herself from the levy's spot (the DPS a player gets). Env LEVY_FLAME=0 skips, LEVY_FLAME_ONLY=Name;Name, LEVY_FLAME_TICKS=N (default 300).
		/// </summary>
		private static void FlameSweep()
		{
			if (Environment.GetEnvironmentVariable("LEVY_FLAME") == "0") return;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_FLAME_TICKS"), out int tk) ? tk : 300;
			bool parity = Environment.GetEnvironmentVariable("LEVY_FLAME_PARITY") == "1";
			var ammo = AmmoByKind();
			Arena(); _cmdAt = HoldCmdAt(); KeepCommander(); Step(2);
			foreach (var (type, label) in FlameWeapons())
			{
				try
				{
					Main.dayTime = true; Main.time = 27000; // noon: no falling stars in the window
					Item smp = ContentSamples.ItemsByType[type];
					int am = smp.useAmmo > 0 && ammo.TryGetValue(smp.useAmmo, out int a) ? a : 0;
					Item fi = new Item(); fi.SetDefaults(type); WeaponProfile fp = WeaponClassifier.GetProfile(fi);
					int tiles = Math.Min(OriginTargetTiles, Math.Max(2, fp.RangeTiles - 2));
					HoldStat lv = HoldRun(type, am, label, "flame: 1 levy at " + tiles + " tiles", 1, ticks, null, 0, null, 0f, false, tiles);
					float lvDps = lv.Damage * 60f / Math.Max(1, lv.Ticks);
					string par = "";
					if (parity)
					{
						Item cw = new Item(); cw.SetDefaults(type);
						HoldStat pc = HoldRun(type, am, label, "flame: commander alone", 0, ticks, cw, 0, null, 0f, true, tiles);
						float pDps = pc.Damage * 60f / Math.Max(1, pc.Ticks);
						par = $" | commander alone at the levy's spot: dmg={pc.Damage} dps={pDps:0} (her projectiles {pc.CmdOwn}) levy/commander={(pDps > 0 ? (lvDps / pDps).ToString("0.00") : "n/a")}";
					}
					bool drawOwner = false; try { drawOwner = OwnerLockProbe.ReadsOwnerAtDraw(smp.shoot); } catch (Exception) { }
					ANote($"flame: {lv} dps={lvDps:0} range={fp.RangeTiles}t family={fp.Family} shotDrawReadsOwner={drawOwner}{par}");
					ACheck($"flame: {label} everything it makes is the levy's and born near him, none at the commander {HoldCmdTiles} tiles away ({lv.AtCmdTotal} at her, {lv.Born.Values.Sum(v => v.untagged)} untagged, farthest from the levy {lv.MaxBornLevy / 16f:0.0} tiles, of {lv.Total})",
						lv.Total > 0 && lv.AtCmdTotal == 0 && lv.Born.Values.All(v => v.untagged == 0));
					ACheck($"flame: {label} hits (damage {lv.Damage} in {lv.Ticks} ticks)", lv.Damage > 0);
				}
				catch (Exception e) { ACheck($"flame: {label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}", false); }
			}
			// the army: six levies follow the commander with the vanilla Flamethrower and the Elf Melter (Calamity's half adds its gel guns), the enemy over her head
			Arena(); KeepCommander(); Step(2);
			foreach (var (type, label) in FlameWeapons().Where(w => w.type == ItemID.Flamethrower || w.type == ItemID.ElfMelter || Environment.GetEnvironmentVariable("LEVY_FLAME_ARMY_ALL") == "1"))
			{
				try
				{
					Item smp = ContentSamples.ItemsByType[type];
					int am = smp.useAmmo > 0 && ammo.TryGetValue(smp.useAmmo, out int a) ? a : 0;
					var r = FlameArmyRun(type, am, 6, ticks, 6);
					ANote($"flame army: {label} 6 levies follow the commander, the enemy 6 tiles over her head: born {r.born} [{r.types}], at the commander {r.atCmd}, untagged {r.untagged}, farthest from a levy {r.farFromLevy / 16f:0.0} tiles, damage {r.damage}");
					ACheck($"flame army: {label} every flame is born at a levy, none at the commander standing among them ({r.atCmd} at her, {r.untagged} untagged, farthest from a levy {r.farFromLevy / 16f:0.0} tiles, of {r.born})", r.born > 0 && r.atCmd == 0 && r.untagged == 0 && r.farFromLevy <= 4 * 16f);
				}
				catch (Exception e) { ACheck($"flame army: {label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}", false); }
			}
		}

		/// <summary>
		/// A flamethrower army the way Eve fights an event: <paramref name="levies"/> levies follow the commander, who stands still, and the enemy hovers <paramref name="above"/> tiles over her head (the event's flyers go for the player).
		/// Every friendly projectile is followed from birth: how many were born within 2 tiles of the commander with no levy nearer, and the farthest birth from the nearest levy.
		/// </summary>
		private static (int born, int atCmd, int untagged, float farFromLevy, long damage, string types) FlameArmyRun(int weaponType, int ammoType, int levies, int ticks, int above)
		{
			ClearSoldiers(); _cmdHolds = false; ReleaseCommanderButtons(); ClearRogueAdds();
			Player cmd = Main.player[0]; cmd.inventory[0] = new Item(); cmd.heldProj = -1; cmd.itemAnimation = 0; cmd.itemTime = 0;
			_cmdAt = new Vector2(Cx * 16f + 8f, FloorY * 16f - 24f);
			SpawnOriginTarget(new Vector2(_cmdAt.X - OriginTargetTiles * 16f, FloorY * 16f));
			_sweepTargetAt = new Vector2(_cmdAt.X + 16f, _cmdAt.Y - above * 16f); _sweepTarget.Bottom = _sweepTargetAt; _sweepTarget.GetGlobalNPC<HarnessTarget>().Pin = _sweepTarget.position;
			var bodies = new List<int>();
			for (int k = 0; k < levies; k++)
			{
				Item w = new Item(); w.SetDefaults(weaponType);
				SoldierRecord rec = MakeOriginRecord(w, ammoType); rec.Name = "Pvt. Flame" + k; rec.Order = SoldierOrder.Follow;
				if (rec.Gear.Ammo != null) rec.Gear.Ammo.stack = rec.Gear.Ammo.maxStack;
				RosterSystem.Instance.Regulars.Add(rec);
				Vector2 pos = new Vector2(_cmdAt.X + (k % 2 == 0 ? -1 : 1) * (2 + 2 * (k / 2)) * 16f, FloorY * 16f);
				int b = RosterSystem.Instance.SpawnBody(rec, pos);
				if (b < 0 || !rec.HasBody) throw new Exception("levy " + k + " did not spawn");
				Main.npc[b].Bottom = pos; bodies.Add(b);
			}
			Main.rand = new Terraria.Utilities.UnifiedRandom(4242);
			KeepCommander(); PinSweepTarget(); CursorOnCommander(); Step(10);
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			AmmoProbe.Reset(); AmmoProbe.On = true;
			var lastSerial = new long[Main.maxProjectiles]; int born = 0, atCmd = 0, untagged = 0; float far = 0f; long dmg = 0; var types = new Dictionary<string, int>();
			try
			{
				for (int k = 0; k < ticks; k++)
				{
					foreach (var r in RosterSystem.Instance.Regulars) if (r.Gear.Ammo != null && !r.Gear.Ammo.IsAir) r.Gear.Ammo.stack = r.Gear.Ammo.maxStack;
					Main.dayTime = true; Main.time = 27000;
					CursorOnCommander(); Step(1); ClearNaturalHostiles();
					if (_sweepTarget != null && _sweepTarget.active) { int d = _sweepTarget.lifeMax - _sweepTarget.life; if (d > 0) dmg += d; }
					Vector2 cmdC = Main.player[0].Center;
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile p = Main.projectile[i];
						if (!p.active || p.hostile || !AmmoProbe.Births.TryGetValue(i, out var ba) || ba.type != p.type || ba.serial == lastSerial[i]) continue;
						lastSerial[i] = ba.serial;
						if (ba.parentProj) continue; // a child is judged by its parent
						born++;
						string pn = p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type); types[pn] = types.TryGetValue(pn, out int tn) ? tn + 1 : 1;
						bool tagged = p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0;
						if (!tagged) untagged++;
						float dl = 1e9f; foreach (int b in bodies) if (Main.npc[b].active) dl = Math.Min(dl, Vector2.Distance(ba.at, Main.npc[b].Center));
						float dc = Vector2.Distance(ba.at, cmdC);
						far = Math.Max(far, dl);
						if (dc < 32f && dl > dc + 16f) atCmd++;
					}
				}
			}
			finally { AmmoProbe.On = false; }
			ClearSoldiers(); ClearRogueAdds(); ReleaseCommanderButtons();
			return (born, atCmd, untagged, far, dmg, string.Join(",", types.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + "x" + kv.Value)));
		}

		private static string ProjName(int type) => type >= ProjectileID.Count ? (ProjectileLoader.GetProjectile(type)?.Name ?? type.ToString()) : ProjectileID.Search.GetName(type);

		/// <summary>The gun or bow each ammo is fired from: a plain one that does not convert the ammo (Megashark for bullets, Platinum Bow for arrows).</summary>
		private static int AmmoGunFor(Item ammo)
		{
			if (ammo.ammo == AmmoID.Bullet) return ItemID.Megashark;
			if (ammo.ammo == AmmoID.Arrow) return ItemID.PlatinumBow;
			return 0;
		}

		/// <summary>The guns an ammo is checked in: the plain one, plus (God Slayer Slug, and anything named in LEVY_AMMO_GUNS=Name;Name) Calamity's held-holdout guns, whose shots a levy makes in the held chain.</summary>
		private static List<int> AmmoGunsFor(Item ammo, string label)
		{
			var guns = new List<int>(); int plain = AmmoGunFor(ammo); if (plain > 0) guns.Add(plain);
			string env = Environment.GetEnvironmentVariable("LEVY_AMMO_GUNS");
			string[] extra = !string.IsNullOrEmpty(env) ? env.Split(';', StringSplitOptions.RemoveEmptyEntries) : label.EndsWith(":GodSlayerSlug") ? AmmoHeldGuns : Array.Empty<string>();
			foreach (string n in extra)
			{
				int t = -1;
				foreach (Mod m in ModLoader.Mods) if (m.TryFind(n, out ModItem mi)) { t = mi.Type; break; }
				if (t < 0) { int id = ItemID.Search.TryGetId(n, out int vid) ? vid : -1; t = id; }
				if (t > 0 && ContentSamples.ItemsByType.TryGetValue(t, out Item g) && g.useAmmo == ammo.ammo && !guns.Contains(t)) guns.Add(t);
			}
			return guns;
		}

		/// <summary>Calamity's held-holdout bullet guns the God Slayer Slug is checked in on the ship run.</summary>
		private static readonly string[] AmmoHeldGuns = { "SDFMG", "Kingsbane" };

		private static string ItemName(int type) => type >= ItemID.Count ? (ItemLoader.GetItem(type)?.Name ?? type.ToString()) : ItemID.Search.GetName(type);

		private static void AmmoWorldCheck()
		{
			if (Environment.GetEnvironmentVariable("LEVY_AMMOWORLD") == "0") return;
			_awPass = _awFail = 0;			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_AMMO_TICKS"), out int tk) ? tk : 600;
			int levyArmy = int.TryParse(Environment.GetEnvironmentVariable("LEVY_AMMO_ARMY"), out int la) ? la : 4; // levies in the "army" run (several levies share the commander's slot: an ammo that counts its owner's shots sees all of theirs)
			var sw = System.Diagnostics.Stopwatch.StartNew();
			var list = new List<(int ammo, string label)>();
			foreach (short t in new short[] { ItemID.ChlorophyteBullet, ItemID.ExplodingBullet, ItemID.MoonlordBullet, ItemID.HolyArrow, ItemID.JestersArrow }) list.Add((t, "Terraria:" + ItemID.Search.GetName(t)));
			if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
				for (int i = ItemID.Count; i < ItemLoader.ItemCount; i++)
					if (ContentSamples.ItemsByType.TryGetValue(i, out Item s) && s?.ModItem?.Mod == cal && (s.ammo == AmmoID.Bullet || s.ammo == AmmoID.Arrow) && s.shoot > 0) list.Add((i, "CalamityMod:" + s.ModItem.Name));
			string only = Environment.GetEnvironmentVariable("LEVY_AMMO_ONLY");
			if (!string.IsNullOrEmpty(only)) { var names = only.Split(';', StringSplitOptions.RemoveEmptyEntries); list = list.Where(a => names.Any(n => a.label.EndsWith(":" + n))).ToList(); }
			bool day0 = Main.dayTime; double time0 = Main.time;
			Main.dayTime = true; Main.time = 27000; // noon: no falling stars (friendly projectiles from the sky) to count as somebody's shots
			try
			{
				Arena(); _cmdAt = HoldCmdAt(); KeepCommander(); Step(2);
				foreach (var (ammo, label) in list)
				{
					try
					{
						Item am = ContentSamples.ItemsByType[ammo];
						foreach (int gun in AmmoGunsFor(am, label))
						{
						string l = label + " in " + ItemName(gun);
						Item gi = new Item(); gi.SetDefaults(gun);
						bool alters = false; try { alters = WorldAlter.AltersWith(gi, am); } catch (Exception) { }
						if (alters || label.EndsWith(":MortarRound") || label.EndsWith(":RubberMortarRound"))
						{
							// a round that breaks blocks is the army's only with the player's consent (WorldAlter): not fired here, so the world checks never alter the field (TileSweep)
							ANote($"{l}: breaks blocks (WorldAlter knows it: {alters}); not fired by the world check (the armory issues it only with the player's consent). Lab result 2026-11-22: hit damage per shot levy/player 0.94 (Mortar), 1.11 (Rubber Mortar)");
							continue;
						}
						AmmoStat lv = AmmoRun(gun, ammo, l, false, ticks);
						int cadence = lv.Shots > 0 ? Math.Max(1, (int)Math.Round(ticks / (double)lv.Shots)) : 0; // the player fires at the levy's rate (a shot every N ticks): an ammo whose effect builds with fire rate compares like for like
						AmmoStat pl = AmmoRun(gun, ammo, l, true, ticks, lv.LevyX, 1, 0, 0, cadence);
						ANote($"{l}: the player pulls the trigger every {cadence} ticks, the levy's rate");
						ANote($"{l}: the levy stood {(_sweepTargetAt.X - lv.LevyX) / 16f:0.0} tiles from the target on average; the player fires from there");
						ANote(lv.ToString()); ANote(pl.ToString());
						double ratio = pl.PerShot > 0 ? lv.PerShot / pl.PerShot : 0;
						ANote($"{l}: levy/player damage per shot {ratio:0.00}, dps {lv.Dps:0} / {pl.Dps:0}");
						// a piercing shot with the default hit cooldown is given its own cooldown of 10 updates when a levy fires it (WeaponUse.TagSoldierProjectile); a player's runs 10 ticks for all her shots, so a levy's re-hits sooner
						Projectile shotSample = ContentSamples.ProjectilesByType.TryGetValue(lv.ShotType, out Projectile ps) ? ps : null;
						bool converted = shotSample != null && shotSample.penetrate != 1 && !shotSample.usesLocalNPCImmunity && !shotSample.usesIDStaticNPCImmunity;
						if (converted && ratio > 1.25) ANote($"{l}: KNOWN DIVERGENCE (decision pending): the levy's piercing shot does {ratio:0.00}x the player's per shot ({lv.PerShot:0.0} vs {pl.PerShot:0.0}): a levy's piercing shot gets its own 10-update hit cooldown, a player's shares her 10-tick one");
						else ACheck($"{l}: a levy's shot does what the player's does (hit damage per shot {lv.PerShot:0.0} vs {pl.PerShot:0.0}, ratio {ratio:0.00}, within 0.75 to 1.25)", lv.Shots > 0 && pl.Shots > 0 && ratio >= 0.75 && ratio <= 1.25);
						ACheck($"{l}: nothing of the levy's shot is born at, warped to or lingers at the commander {AmmoCmdTiles} tiles away (born {lv.AtCmd}, warped {lv.JumpsToCmd}, ticks near her {lv.NearCmd}, untagged {lv.Untagged})", lv.AtCmd == 0 && lv.JumpsToCmd == 0 && lv.NearCmd == 0 && lv.Untagged == 0);
						float pMaxLife = pl.ByType.Count == 0 ? 0 : pl.ByType.Values.Max(v => v.MaxLife), lMaxLife = lv.ByType.Count == 0 ? 0 : lv.ByType.Values.Max(v => v.MaxLife);
						if (levyArmy > 1)
						{
							AmmoStat ar = AmmoRun(gun, ammo, l, false, ticks, 0f, levyArmy);
							double ar1 = lv.PerShot > 0 ? ar.PerShot / lv.PerShot : 0;
							ANote(ar.ToString());
							if (label.EndsWith(":VeriumBolt")) ANote($"{l}: {levyArmy} levies firing together do {ar1:0.00} of one levy's damage per shot; Verium's doom is one mark per enemy (a 100 + 15 per stack strike 90 ticks after the first hit), shared by every shooter, players included, not checked");
							else if (AmmoProbe.IdStatic.Contains(lv.ShotType)) ANote($"{l}: {levyArmy} levies firing together do {ar1:0.00} of one levy's damage per shot; the shot uses a per-type hit cooldown (shared by every shooter of it, players included), not checked");
							else ACheck($"{l}: {levyArmy} levies firing together each do what one does (hit damage per shot {ar.PerShot:0.0} vs {lv.PerShot:0.0}, ratio {ar1:0.00}, within 0.7 to 1.35)", ar.Shots > 0 && ar1 >= 0.7 && ar1 <= 1.35);
							if (label.EndsWith(":GodSlayerSlug") || label.EndsWith(":ExplodingBullet") || label.EndsWith(":JestersArrow") || Environment.GetEnvironmentVariable("LEVY_AMMO_CMDFIRE") == "1")
							{
								// the commander fights too: her Megashark's Exploding Bullets hit the same target (each explosion gives it her slot's hit cooldown)
								AmmoStat cf = AmmoRun(gun, ammo, l, false, ticks, 0f, levyArmy, ItemID.Megashark, ItemID.ExplodingBullet);
								double cr = ar.PerShot > 0 ? cf.PerShot / ar.PerShot : 0;
								ANote(cf.ToString());
								ACheck($"{l}: {levyArmy} levies hit as hard while the commander fires her own gun at the same target (hit damage per shot {cf.PerShot:0.0} vs {ar.PerShot:0.0} without her, ratio {cr:0.00}, within 0.75 to 1.25)", cf.Shots > 0 && cr >= 0.75 && cr <= 1.25);
							}
						}
						ACheck($"{l}: no projectile of the levy's outlives the player's (longest {lMaxLife} vs {pMaxLife} ticks)", lMaxLife <= pMaxLife * 1.5f + 10);
						}
					}
					catch (Exception e) { ACheck($"{label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}", false); }
				}
			}
			catch (Exception e) { ACheck("ammo world threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _sweepTarget = null; ClearRogueAdds(); ClearSoldiers(); ReleaseCommanderButtons(); }
			try { GunParityCheck(); }
			catch (Exception e) { ACheck("gun parity threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _sweepTarget = null; ClearRogueAdds(); ClearSoldiers(); ReleaseCommanderButtons(); }
			try { FlameSweep(); }
			catch (Exception e) { ACheck("flame sweep threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _sweepTarget = null; ClearRogueAdds(); ClearSoldiers(); ReleaseCommanderButtons(); }
			Main.dayTime = day0; Main.time = time0;
			ANote($"army hit cooldown: {ArmyHitCooldown.Passes} levy hit passes ran on the levy's own enemy cooldowns, {ArmyHitCooldown.Set} cooldowns set ({(ArmyHitCooldown.Disabled ? "OFF" : "on")})");
			ANote($"ammo world ran in {sw.ElapsedMilliseconds / 1000} s ({list.Count} ammo, {ticks} ticks each side)");
			_mod.Logger.Info($"[AmmoWorld] self-check pass={_awPass} fail={_awFail}");
		}
	}

	/// <summary>Lab only (the ammo world check): each projectile's parent at birth, and every hit a friendly projectile lands, while <see cref="On"/>.</summary>
	public class AmmoProbe : GlobalProjectile
	{
		internal static bool On, ArmyOnly;
		internal static readonly Dictionary<int, (int type, Vector2 at, long serial, bool parentProj, Vector2 parentAt, string parentName, bool parentHolder)> Births = new();
		private static long _serial;
		internal static readonly Dictionary<int, (int n, long dmg)> Hits = new();
		/// <summary>Shots fired by type: spawns that are not a child of another shot (a gun's holdout counts as the gun).</summary>
		internal static readonly Dictionary<int, int> Shots = new();
		/// <summary>Projectile types seen with Calamity's per-type hit cooldown (usesIDStaticNPCImmunity): one cooldown for every projectile of the type, whoever owns it, so several shooters share it by design.</summary>
		internal static readonly HashSet<int> IdStatic = new();
		/// <summary>Shots fired, every type but a gun's own holdout (the gun parity rate): what <see cref="Shots"/> counts, summed, less the holdouts.</summary>
		internal static int Roots;
		internal static void Reset() { Births.Clear(); Hits.Clear(); Shots.Clear(); Roots = 0; }
		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (!On) return;
			if (projectile.usesIDStaticNPCImmunity) IdStatic.Add(projectile.type);
			bool pp = source is EntitySource_Parent ep && ep.Entity is Projectile;
			Projectile par = pp ? (Projectile)((EntitySource_Parent)source).Entity : null;
			bool holder = par != null && (par.aiStyle == ProjAIStyleID.HeldProjectile || (par.ModProjectile != null && par.ModProjectile.GetType().Name.Contains("Holdout")));
			if ((!pp || holder) && (!ArmyOnly || !(source is EntitySource_ItemUse eu && ReferenceEquals(eu.Entity, Main.player[0]))))
			{
				Shots[projectile.type] = Shots.TryGetValue(projectile.type, out int sn) ? sn + 1 : 1;
				bool isHoldout = projectile.aiStyle == ProjAIStyleID.HeldProjectile || (projectile.ModProjectile != null && projectile.ModProjectile.GetType().Name.Contains("Holdout"));
				if (!isHoldout) Roots++;
			}
			Births[projectile.whoAmI] = (projectile.type, projectile.Center, ++_serial, pp, par != null ? par.Center : Vector2.Zero, par != null ? (par.ModProjectile?.Name ?? ProjectileID.Search.GetName(par.type)) : "", holder);
		}
		private static int _cursorNotes;
		/// <summary>Lab (LEVY_AMMO_CURSORTRACE=Name): what the named shot's owner cursor reads inside its own AI (Calamity's per-player cursor, the owner's place), a few times.</summary>
		private static readonly string CursorTrace = Environment.GetEnvironmentVariable("LEVY_AMMO_CURSORTRACE");
		public override void PostAI(Projectile projectile)
		{
			if (!On || CursorTrace == null || _cursorNotes >= 160 || projectile.ModProjectile?.Name != CursorTrace || Main.GameUpdateCount % 20 != 0) return;
			Player o = Main.player[projectile.owner];
			object cp = AltPolicies.CalamityPlayerOf(o);
			Vector2 d = cp != null ? AltPolicies.GetCalAimPublic(cp).Item1 : Vector2.Zero;
			_cursorNotes++;
			ModContent.GetInstance<Levy>().Logger.Info($"[AmmoWorld] OBSERVED   cursor trace {CursorTrace} tl={projectile.timeLeft} owner={projectile.owner} ownerCenter=({o.MountedCenter.X / 16f:0.0},{o.MountedCenter.Y / 16f:0.0})t calDelta=({d.X / 16f:0.0},{d.Y / 16f:0.0})t cursor=({(o.MountedCenter.X + d.X) / 16f:0.0},{(o.MountedCenter.Y + d.Y) / 16f:0.0})t shot=({projectile.Center.X / 16f:0.0},{projectile.Center.Y / 16f:0.0})t vel=({projectile.velocity.X:0.0},{projectile.velocity.Y:0.0}) puppetDepth={OwnerPuppet.DebugDepth} shark={(cp != null ? cp.GetType().GetField("sharkGunDamageScaling")?.GetValue(cp) : "-")} ai=({projectile.ai[0]:0.#},{projectile.ai[1]:0.#},{projectile.ai[2]:0.#})");
		}
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!On) return;
			if (ArmyOnly && !(projectile.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex >= 0)) return; // a levy run counts the levies' hits only
			Hits.TryGetValue(projectile.type, out var h); Hits[projectile.type] = (h.n + 1, h.dmg + damageDone);
		}
	}
}
