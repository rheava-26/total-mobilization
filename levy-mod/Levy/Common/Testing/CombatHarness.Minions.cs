using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;
using Levy.Content.Projectiles;

namespace Levy.Common.Testing
{
	/// <summary>
	/// MINION SCENARIO (LEVY_HARNESS_SCENARIO=minions; LEVY_HARNESS_MINIONS = comma list of item names, or "list" to only classify every modded summon staff of LEVY_HARNESS_MOD).
	/// One soldier per staff (Follow order) with a pinned huge-HP target 10 tiles away; for 900 ticks records which path fields the minion (real = driven vanilla/modded projectile,
	/// stand-in = Levy's SoldierMinion), whether it stays alive, how much damage the target took, and every projectile the minions create (type, owner). One HARNESS-MINION line per staff. Measurement only.
	/// </summary>
	public partial class CombatHarness
	{
		private string[] _mnNames; private int _mnI = -1, _mnTick, _mnBody = -1, _mnTarget = -1; private bool _mnStarted, _mnList;
		private readonly List<int> _mnItems = new();
		private int _mnMaxReal, _mnMaxStand, _mnTargetLife0; private readonly HashSet<int> _mnIdentReal = new(), _mnIdentStand = new();
		private readonly Dictionary<string, int> _mnChildren = new(); private readonly HashSet<string> _mnRealTypes = new();
		private string _mnSamples = ""; private int _mnFirstReal = -1, _mnFirstStand = -1;
		private static bool _mnHooked; private static CombatHarness _mnSelf;

		private bool BeginMinions()
		{
			try { MonoModHooks.Add(typeof(Netplay).GetMethod("UpdateConnectedClients", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic), new HookUpdateConnectedClients(KeepStandinDetour)); _spHooked = true; } catch (Exception e) { Mod.Logger.Warn("minions: hook failed " + e.Message); }
			string v = Environment.GetEnvironmentVariable("LEVY_HARNESS_MINIONS") ?? "list";
			_mnList = v.Equals("list", StringComparison.OrdinalIgnoreCase); bool all = v.Equals("all", StringComparison.OrdinalIgnoreCase);
			_mnNames = v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				Item it = new Item();
				try { it.SetDefaults(t); } catch { continue; }
				if (it.IsAir || it.damage <= 0 || it.shoot <= 0) continue;
				bool summon = it.CountsAsClass(DamageClass.Summon) && !it.sentry;
				bool modded = it.ModItem != null && it.ModItem.Mod.Name == TestMod;
				if (_mnList || (all && summon && modded && WeaponClassifier.GetProfile(it).Family == WeaponFamily.Summon)) { if (summon && modded) _mnItems.Add(t); continue; }
				foreach (string n in _mnNames)
					if (it.Name.Equals(n, StringComparison.OrdinalIgnoreCase) || (it.ModItem != null && it.ModItem.Name.Equals(n, StringComparison.OrdinalIgnoreCase)) || (it.ModItem == null && ItemID.Search.TryGetName(t, out string idn) && idn.Equals(n, StringComparison.OrdinalIgnoreCase)))
					{ _mnItems.Add(t); break; }
			}
			foreach (int t in _mnItems) MnClassify(t);
			Mod.Logger.Info($"HARNESS-MINION: {_mnItems.Count} staffs queued (list-only={_mnList})");
			if (_mnList) return MnListOnly();
			_mnI = 0; _mnStarted = false;
			if (!_mnHooked) { _mnHooked = true; _mnSelf = this; On_Projectile.Kill += MnKill; On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float += MnNewProj; }
			return true;
		}

		private bool MnListOnly() { _mnI = int.MaxValue; return true; }

		private static string MnClassify(int type)
		{
			Item it = new Item(); it.SetDefaults(type);
			Projectile s = WeaponClassifier.ProjectileSample(it.shoot);
			var prof = WeaponClassifier.GetProfile(it);
			string line = $"item={it.Name} internal={(it.ModItem != null ? it.ModItem.Mod.Name + "/" + it.ModItem.Name : "vanilla")} class={it.DamageType?.Name} shoot={it.shoot}({(s != null ? s.ModProjectile?.Name ?? s.Name : "null")}) minionFlag={s?.minion} slots={s?.minionSlots} ai={s?.aiStyle} family={prof.Family} canDrive={SoldierMinions.CanDrive(it.shoot)}";
			ModContent.GetInstance<Levy>().Logger.Info("HARNESS-MINION-CLASS: " + line);
			return line;
		}

		private static int MnNewProj(On_Projectile.orig_NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float orig, Terraria.DataStructures.IEntitySource src, float x, float y, float sx, float sy, int type, int dmg, float kb, int owner, float a0, float a1, float a2)
		{
			int idx = orig(src, x, y, sx, sy, type, dmg, kb, owner, a0, a1, a2);
			try
			{
				var self = _mnSelf;
				if (self != null && self._mnStarted && type == self._mnShoot) self._mnSpawns++;
				if (self != null && self._ph == Ph.Scenario && self._mnStarted && src is Terraria.DataStructures.EntitySource_Parent par && par.Entity is Projectile pp && pp.minion)
				{
					string key = (pp.ModProjectile?.Name ?? pp.type.ToString()) + "->" + (idx >= 0 && idx < Main.maxProjectiles ? (Main.projectile[idx].ModProjectile?.Name ?? Main.projectile[idx].type.ToString()) : type.ToString()) + "(owner" + owner + ")";
					self._mnChildren[key] = self._mnChildren.GetValueOrDefault(key) + 1; self._mnWin++; self._mnWinSec++;
				}
			}
			catch (Exception) { }
			return idx;
		}

		private int _mnWinSec, _mnWin, _mnPeakRate, _mnPeakPass, _mnPass, _mnPeakKids, _mnSpawns, _mnKillLogs, _mnShoot, _mnPrevReal; private string _mnPrevDesc = "";
		private static void MnKill(On_Projectile.orig_Kill orig, Projectile self)
		{
			try
			{
				var m = _mnSelf;
				if (m != null && m._mnStarted && self.minion && self.active && SoldierMinions.OwnedBy(self, m._mnBody) && m._mnKillLogs++ < 3)
					m.Mod.Logger.Info("HARNESS-MINION-DBG kill " + (self.ModProjectile?.Name ?? self.type.ToString()) + " timeLeft=" + self.timeLeft + " trace=" + Environment.StackTrace.Replace("\n", " | ").Substring(0, 1000));
			}
			catch (Exception ex) { _mnSelf?.Mod.Logger.Info("HARNESS-MINION-DBG killhook " + ex.Message); }
			orig(self);
		}

		private void MnPin()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName;
			p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500; p.statManaMax = p.statManaMax2 = 200; p.statMana = 200;
			p.position = new Vector2(_cx * 16f - 4 * 16f - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
		}

		/// <summary>
		/// REARM TEST (LEVY_HARNESS_MNREARM=tick:gap[:swapStaffName]): at <tick> the soldier hands its whole kit back (weapon slot emptied, NotifyGearChanged: what Armory ReturnGear + RebindBody do), <gap> ticks later a fresh
		/// copy of the same staff (or the named other staff) is drawn again. Logs the flock and the bad-staff state each 60 ticks after the rearm.
		/// </summary>
		private int _mnRearmAt = -2, _mnRearmGap, _mnRearmSwap; private string _mnRearmSwapName; private bool _mnRearmGiven;
		private void MnRearm(int type)
		{
			if (_mnRearmAt == -2)
			{
				string v = Environment.GetEnvironmentVariable("LEVY_HARNESS_MNREARM");
				_mnRearmAt = -1;
				if (!string.IsNullOrEmpty(v))
				{
					var f = v.Split(':'); _mnRearmAt = int.Parse(f[0]); _mnRearmGap = f.Length > 1 ? int.Parse(f[1]) : 0; _mnRearmSwapName = f.Length > 2 ? f[2] : null;
				}
			}
			if (_mnRearmAt < 0) return;
			if (_mnTick == 1) _mnRearmGiven = false;
			var rec = RosterSystem.Instance.Regulars.LastOrDefault(r => r.HasBody && r.BodyIndex == _mnBody);
			if (rec == null || !(Main.npc[_mnBody].ModNPC is SoldierNPC sn)) return;
			Item shoot0 = new Item(); shoot0.SetDefaults(type);
			if (_mnRearmSwapName == "armory")
			{
				// the REAL rearm: a detached Armory stocked with the staff takes the whole kit back (ReturnGear) and issues again (IssueGear), then the live body re-reads its gear (RebindBody)
				if (_mnTick == _mnRearmAt)
				{
					rec.Gear.SetLocked(SoldierGear.SlotWeapon, false); rec.LoadoutLocked = false; rec.Gear.LockMask = 0;
					var te = new ArmoryTE();
					ArmoryTE.TestServed = new List<SoldierRecord> { rec };
					Item extra = new Item(); extra.SetDefaults(type);
					te.ReturnGear(rec.Gear);
					Item left = te.Deposit(extra);
					bool issued = te.IssueGear(rec);
					ArmoryTE.TestServed = null;
					sn.NotifyGearChanged();
					Mod.Logger.Info($"HARNESS-REARM t{_mnTick}: armory rearm issued={issued} weapon={(rec.Gear.HasWeapon ? rec.Gear.Weapon.Name : "none")} sidearm={(rec.Gear.HasSidearm ? rec.Gear.Sidearm.Name : "none")} flock real={CountReal()} bad={SoldierMinions.IsBadStaff(shoot0.shoot)}");
				}
			}
			else if (_mnTick == _mnRearmAt)
			{
				rec.Gear.SetLocked(SoldierGear.SlotWeapon, false); rec.LoadoutLocked = false;
				rec.Gear.Weapon = new Item();
				sn.NotifyGearChanged();
				Mod.Logger.Info($"HARNESS-REARM t{_mnTick}: kit returned; flock real={CountReal()} bad={SoldierMinions.IsBadStaff(shoot0.shoot)}");
			}
			if (_mnRearmSwapName != "armory" && _mnTick == _mnRearmAt + _mnRearmGap)
			{
				Item w = new Item();
				if (_mnRearmSwapName != null)
				{
					for (int t = 1; t < ItemLoader.ItemCount; t++) { var c = new Item(); try { c.SetDefaults(t); } catch { continue; } if (c.Name.Equals(_mnRearmSwapName, StringComparison.OrdinalIgnoreCase) && c.shoot > 0) { w = c; break; } }
					if (w.IsAir) Mod.Logger.Warn("HARNESS-REARM: swap staff not found " + _mnRearmSwapName);
				}
				else w.SetDefaults(type);
				rec.Gear.Weapon = w;
				sn.NotifyGearChanged();
				Mod.Logger.Info($"HARNESS-REARM t{_mnTick}: drew {w.Name}; flock real={CountReal()} bad={SoldierMinions.IsBadStaff(w.shoot)}");
			}
			if (_mnTick > _mnRearmAt && (_mnTick - _mnRearmAt) % 60 == 0 && _mnTick <= _mnRearmAt + _mnRearmGap + 1200)
				Mod.Logger.Info($"HARNESS-REARM t{_mnTick}: real={CountReal()} stand={CountStand()} bad={SoldierMinions.IsBadStaff(shoot0.shoot)} dmg={_mnTargetLife0 - Main.npc[_mnTarget].life}");
		}
		private int CountReal() { int n = 0; for (int i = 0; i < Main.maxProjectiles; i++) if (SoldierMinions.OwnedBy(Main.projectile[i], _mnBody)) n++; return n; }
		private int CountStand() { int st = ModContent.ProjectileType<SoldierMinion>(), n = 0; for (int i = 0; i < Main.maxProjectiles; i++) { var p = Main.projectile[i]; if (p.active && p.type == st && (int)p.ai[0] == _mnBody) n++; } return n; }

		private void StepMinions()
		{
			if (_mnI >= _mnItems.Count) { Finish("minions scenario complete"); return; }
			int type = _mnItems[_mnI];
			if (!_mnStarted)
			{
				ClearAll(); SpBuildFloor("run");
				_mnMaxReal = _mnMaxStand = 0; _mnSpawns = 0; _mnPeakKids = 0; _mnWin = _mnWinSec = _mnPeakRate = _mnPeakPass = _mnPass = 0; _mnKillLogs = 0; { var tmp = new Item(); tmp.SetDefaults(type); _mnShoot = tmp.shoot; } _mnIdentReal.Clear(); _mnIdentStand.Clear(); _mnChildren.Clear(); _mnRealTypes.Clear(); _mnSamples = ""; _mnFirstReal = _mnFirstStand = -1;
				MnPin();
				var rec = MakeRecord("Pvt. Summoner");
				Item w = new Item(); w.SetDefaults(type);
				var gear = rec.Gear;
				if (gear.CanHold(SoldierGear.SlotWeapon, w)) { gear.Weapon = w; gear.SetLocked(SoldierGear.SlotWeapon, true); }
				else { Mod.Logger.Warn("HARNESS-MINION: CanHold false for " + w.Name); _mnI++; return; }
				rec.LoadoutLocked = true; rec.Order = Environment.GetEnvironmentVariable("LEVY_HARNESS_MNHOLD") != null ? SoldierOrder.Hold : SoldierOrder.Follow; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				RosterSystem.Instance.Regulars.Add(rec);
				Vector2 at = SoldierX0Bottom;
				_mnBody = RosterSystem.Instance.SpawnBody(rec, at);
				if (_mnBody < 0 || !rec.HasBody) { Mod.Logger.Warn("HARNESS-MINION: SpawnBody failed"); _mnI++; return; }
				Main.npc[_mnBody].Bottom = at;
				_mnTarget = SpSpawnTarget(_cx * 16f + (int.TryParse(Environment.GetEnvironmentVariable("LEVY_HARNESS_MNDIST"), out int md) ? md : 10) * 16f, 64, 96, 0);
				_mnTargetLife0 = Main.npc[_mnTarget].life;
				_mnStarted = true; _mnTick = 0;
				return;
			}
			MnPin();
			_mnTick++;
			MnRearm(type);
			if (_mnTick % 20 == 0) { _mnPeakPass = Math.Max(_mnPeakPass, _mnWin - _mnPass); _mnPass = _mnWin; }
			if (_mnTick % 60 == 0) { _mnPeakRate = Math.Max(_mnPeakRate, _mnWinSec); _mnWinSec = 0; }
			int standType = ModContent.ProjectileType<SoldierMinion>();
			int real = 0, stand = 0, nKids = 0;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active) continue;
				if (p.TryGetGlobalProjectile(out SoldierProjectileTag kt) && kt.SoldierIndex == _mnBody && !p.minion && p.type != standType) nKids++;
				if (p.type == standType && (int)p.ai[0] == _mnBody) { stand++; _mnIdentStand.Add(p.identity); }
				else if (SoldierMinions.OwnedBy(p, _mnBody)) { real++; _mnIdentReal.Add(p.identity); _mnRealTypes.Add((p.ModProjectile?.Name ?? ("v" + p.type)) + (SoldierMinions.IsCast(p) ? "" : "(child)")); }
			}
			if (_mnPrevReal > 0 && real == 0 && _mnKillLogs++ < 6) Mod.Logger.Info($"HARNESS-MINION-DBG vanished at t{_mnTick}: last={_mnPrevDesc}");
			_mnPrevReal = real; _mnPrevDesc = "";
			for (int i = 0; i < Main.maxProjectiles; i++) { Projectile q = Main.projectile[i]; if (q.active && SoldierMinions.OwnedBy(q, _mnBody)) _mnPrevDesc += $"[{q.ModProjectile?.Name ?? q.type.ToString()} slot={i} timeLeft={q.timeLeft} ai={q.ai[0]}/{q.ai[1]}/{q.ai[2]} dmg={q.damage} slots={q.minionSlots} pos={(q.Center - Main.npc[_mnBody].Center)} vel={q.velocity} friendly={q.friendly}]"; }
			_mnPeakKids = Math.Max(_mnPeakKids, nKids); _mnMaxReal = Math.Max(_mnMaxReal, real); _mnMaxStand = Math.Max(_mnMaxStand, stand);
			if (real > 0 && _mnFirstReal < 0) _mnFirstReal = _mnTick;
			if (stand > 0 && _mnFirstStand < 0) _mnFirstStand = _mnTick;
			if (_mnTick == 60 || _mnTick == 180 || _mnTick == 360 || _mnTick == 300 || _mnTick == 450 || _mnTick == 600 || _mnTick == 750 || _mnTick == 900 || _mnTick == 1500 || _mnTick == 1900) _mnSamples += $"t{_mnTick}:r{real}/s{stand}/d{_mnTargetLife0 - Main.npc[_mnTarget].life} ";
			if (_mnTick == 200 || _mnTick == 500 || _mnTick == 850) Mod.Logger.Info($"HARNESS-MINION-DBG state t{_mnTick} target@{(Main.npc[_mnTarget].Center - Main.npc[_mnBody].Center)} {_mnPrevDesc}");
			if (_mnTick < (int.TryParse(Environment.GetEnvironmentVariable("LEVY_HARNESS_MNTICKS"), out int mt) ? mt : 900)) return;
			NPC tg = Main.npc[_mnTarget];
			int dmg = _mnTargetLife0 - tg.life;
			Item it = new Item(); it.SetDefaults(type);
			var prof = WeaponClassifier.GetProfile(it);
			string path = _mnMaxReal > 0 ? "REAL" : _mnMaxStand > 0 ? "STAND-IN" : "NONE";
			string kids = _mnChildren.Count == 0 ? "-" : string.Join(",", _mnChildren.Select(kv => kv.Key + "x" + kv.Value));
			Mod.Logger.Info($"HARNESS-MINION: item={it.Name} family={prof.Family} canDrive={SoldierMinions.CanDrive(it.shoot)} path={path} firstReal=t{_mnFirstReal} firstStand=t{_mnFirstStand} maxReal={_mnMaxReal} maxStand={_mnMaxStand} castSpawns={_mnSpawns} peakKids={_mnPeakKids} peakPerSec={_mnPeakRate} peakPer20={_mnPeakPass} realTypes=[{string.Join("/", _mnRealTypes)}] samples=[{_mnSamples.Trim()}] targetDamage={dmg} childProjectiles=[{kids}]");
			_mnI++; _mnStarted = false;
		}
	}
}
