using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Owner;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- player-safety sweep: levies
		// What a phase of the tick (the NPCs, the projectiles) changed on slot 0: the end-of-tick snapshot misses a value that a later phase puts back.
		private static bool _watch; private static Dictionary<string, string> _phaseBefore; private static readonly HashSet<string> PhaseDiffs = new();
		private static NPC _sweepTarget; private static Vector2 _sweepTargetAt;
		/// <summary>The pinned target's velocity as other code reads it (zero except while the holdout check moves it: a fast target).</summary>
		private static Vector2 _sweepVel;

		private static void PhaseOpen() { if (_watch) _phaseBefore = PlayerProbe.WatchedSnapshot(Main.player[0]); }
		private static void PhaseClose(string phase)
		{
			if (!_watch || _phaseBefore == null) return;
			var d = new HashSet<string>(); PlayerProbe.Differences(_phaseBefore, PlayerProbe.WatchedSnapshot(Main.player[0]), d);
			foreach (string k in d) PhaseDiffs.Add(phase + ":" + k);
		}

		private static void PinSweepTarget()
		{
			if (_sweepTarget == null || !_sweepTarget.active) return;
			_sweepTarget.active = true; _sweepTarget.timeLeft = 9999; _sweepTarget.Bottom = _sweepTargetAt; _sweepTarget.velocity = _sweepVel; _sweepTarget.life = _sweepTarget.lifeMax; _sweepTarget.dontTakeDamage = false;
		}

		/// <summary>
		/// THE PLAYER-SAFETY SWEEP, levies. Every weapon a levy can hold (vanilla and every loaded mod: Calamity, Thorium, ...) is carried by one levy next to a pinned enemy, with the commander as slot 0 standing in as the
		/// single-player local player (Main.myPlayer = 0 while NPCs and projectiles update, the situation in which an army shot's owner is the real player). The levy fires for 240 ticks. The watched use-state fields of the
		/// real player (item animation and time, held projectile, channel, control flags, selected item, rotation, direction, velocity, arms, mouse-world listeners ...) are compared with a control run (a levy with a plain
		/// sword in the same arena, whose differences are noise), at the end of every tick and across every phase. A weapon whose fire moves any of them is listed. Target 0.
		/// Env: LEVY_IDENTITY_SWEEP=1 sweeps every weapon, =0 skips; unset runs a sample of about 80 weapons spread over the list. LEVY_IDENTITY_SWEEP_SHARD=i/n runs one n-th of the list (split across processes).
		/// LEVY_IDENTITY_SWEEP_ONLY=Name;Name re-checks named weapons. LEVY_USEGUARD=0 turns the use guard off, which is how the unguarded counts are taken.
		/// </summary>
		private static void SafetySweepLevies()
		{
			string mode = Environment.GetEnvironmentVariable("LEVY_IDENTITY_SWEEP");
			if (mode == "0") return;
			var weapons = new List<(int type, string label)>();
			var probeGear = new SoldierGear();
			for (int i = 1; i < ItemLoader.ItemCount; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item s) || s == null || s.IsAir || s.damage <= 0 || s.accessory || s.ammo > 0 || s.pick > 0 || s.axe > 0 || s.hammer > 0) continue;
				string mod = s.ModItem?.Mod.Name;
				if (mod == "Levy" || mod == "Mechanization" || mod == "Warbands") continue;
				Item it = new Item(); it.SetDefaults(i);
				if (!probeGear.CanHold(SoldierGear.SlotWeapon, it)) continue;
				WeaponFamily f = WeaponClassifier.Classify(it);
				if (f == WeaponFamily.Broadsword || f == WeaponFamily.Shortsword) continue; // blades: no shot of their own to hold
				weapons.Add((i, (mod ?? "Terraria") + ":" + s.Name));
			}
			int total = weapons.Count;
			string only = Environment.GetEnvironmentVariable("LEVY_IDENTITY_SWEEP_ONLY");
			if (!string.IsNullOrEmpty(only)) { var names = only.Replace('_', ' ').Split(';'); weapons = weapons.Where(w => names.Any(n => w.label.EndsWith(":" + n))).ToList(); }
			else if (mode != "1") { int step = Math.Max(1, weapons.Count / 80); weapons = weapons.Where((w, k) => k % step == 0).ToList(); }
			string shard = Environment.GetEnvironmentVariable("LEVY_IDENTITY_SWEEP_SHARD");
			if (!string.IsNullOrEmpty(shard) && shard.Split('/') is { Length: 2 } sh && int.TryParse(sh[0], out int si) && int.TryParse(sh[1], out int sn) && sn > 1) weapons = weapons.Where((w, k) => k % sn == si).ToList();

			var ammoByKind = new Dictionary<int, int>();
			for (int i = 1; i < ItemLoader.ItemCount; i++)
				if (ContentSamples.ItemsByType.TryGetValue(i, out Item a) && a != null && !a.IsAir && a.ammo > 0 && !a.IsACoin && !ammoByKind.ContainsKey(a.ammo)) ammoByKind[a.ammo] = i;

			_watch = true; bool wasUg = UseGuard.Disabled;
			UseGuard.Measure = true; UseGuard.Moves.Clear(); UseGuard.GuardTicks = 0; UseGuard.GuardSnaps = 0; UseGuard.Undone = 0; UseGuard.GuardedUpdates = 0; UseGuard.Scopes = 0;
			var others = new List<string>(); var leakers = new List<string>(); var tally = new Dictionary<string, int>(); int done = 0, fired = 0, threw = 0;
			var sw = Stopwatch.StartNew();
			try
			{
				KeepCommander(); Step(2);
				var control = new HashSet<string>();
				for (int c = 0; c < 2; c++) { var r = SweepOne(ItemID.CopperShortsword, 0, "control"); foreach (string k in r.leaks) control.Add(k); }
				Note("player-safety sweep control (noise, ignored): [" + string.Join(", ", control) + "]");
				foreach (var (type, label) in weapons)
				{
					try
					{
						int ammo = ContentSamples.ItemsByType[type].useAmmo is int ua && ua > 0 && ammoByKind.TryGetValue(ua, out int at) ? at : 0;
						var r = SweepOne(type, ammo, label);
						r.leaks.ExceptWith(control); done++; if (r.fired > 0) fired++;
						// the use state (the Player's own use fields, the mouse-world listeners) is what the guard answers for; another mod's own counter or an immunity flicker is listed, not failed
						var other = r.leaks.Where(lf => !IsUseField(lf)).ToList(); r.leaks.ExceptWith(other);
						if (other.Count > 0) others.Add($"{label} [{string.Join(", ", other.Take(4).Select(lf => r.detail.TryGetValue(lf, out string dv2) ? lf + " " + dv2 : lf))}]");
						if (r.leaks.Count > 0)
						{
							leakers.Add($"{label} [{string.Join(", ", r.leaks.Take(6).Select(lf => r.detail.TryGetValue(lf, out string dv) ? lf + " " + dv : lf))}{(r.leaks.Count > 6 ? ", +" + (r.leaks.Count - 6) : "")}]");
							foreach (string k in r.leaks) tally[k] = tally.TryGetValue(k, out int n) ? n + 1 : 1;
						}
					}
					catch (Exception e) { threw++; if (threw <= 3) Note($"player-safety sweep: {label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}"); }
				}
			}
			finally { _watch = false; UseGuard.Measure = false; _sweepTarget = null; }
			sw.Stop();
			Note($"player-safety sweep (use guard {(UseGuard.Disabled ? "OFF" : "on")}): {done} of {total} levy weapons run ({fired} fired a shot, {threw} threw) in {sw.ElapsedMilliseconds / 1000} s; the guard held {UseGuard.Scopes} scopes and {UseGuard.GuardedUpdates} round updates, and had to put the use state back {UseGuard.Undone} times; {UseGuard.GuardSnaps} snapshots took {UseGuard.GuardTicks * 1000.0 / Stopwatch.Frequency:F1} ms ({UseGuard.GuardTicks * 1e6 / Stopwatch.Frequency / Math.Max(1, UseGuard.GuardSnaps):F2} us per snapshot + restore)");
			if (UseGuard.Moves.Count > 0) Note("player-safety sweep, passes the guard had to undo (kind:projectile type = count): " + string.Join(", ", UseGuard.Moves.OrderByDescending(kv => kv.Value).Take(25).Select(kv => kv.Key + "=" + kv.Value)));
			foreach (string o in others.Take(20)) Note("player-safety sweep, another field of the real player moved (not use state, listed): " + o);
			foreach (string l in leakers.Take(80)) Note("player-safety sweep, touches the real player: " + l);
			if (leakers.Count > 80) Note($"player-safety sweep: {leakers.Count - 80} more weapons touch the real player");
			Note("player-safety sweep, fields moved (weapon count): " + string.Join(", ", tally.OrderByDescending(kv => kv.Value).Take(25).Select(kv => kv.Key + "=" + kv.Value)));
			Check($"player-safety sweep, levies: none of {done} weapons ({fired} fired) changes the real player's use state (touched: {leakers.Count})", done > 0 && fired > 0 && leakers.Count == 0 && threw == 0);
			_ = wasUg;
		}

		private static readonly HashSet<string> UseFields = new() { "itemAnimation", "itemAnimationMax", "itemTime", "itemTimeMax", "reuseDelay", "channel", "controlUseItem", "controlUseTile", "releaseUseItem", "heldProj", "noItems", "selectedItem", "itemRotation", "itemLocation", "direction", "velocity", "noThrow", "stoned", "frozen", "webbed", "dead", "talkNPC", "mount", "compositeFrontArm", "compositeBackArm" };
		/// <summary>A watched key (optionally "phase:" prefixed) that is a use field: one of the Player's own use fields, or a mod player's mouse-world listener.</summary>
		private static bool IsUseField(string key)
		{
			int c = key.IndexOf(':'); if (c >= 0 && (key.StartsWith("npcs:") || key.StartsWith("projectiles:"))) key = key.Substring(c + 1);
			if (key.IndexOf("mouseWorld", StringComparison.OrdinalIgnoreCase) >= 0) return true;
			return key.StartsWith("Player.") && UseFields.Contains(key.Substring(7));
		}

		/// <summary>One levy fires one weapon at the pinned enemy for 240 ticks; returns what moved on slot 0 and whether a shot of the army appeared.</summary>
		private static (HashSet<string> leaks, int fired, Dictionary<string, string> detail) SweepOne(int weaponType, int ammoType, string label)
		{
			ClearSoldiers();
			if (_sweepTarget != null) _sweepTarget.active = false;
			_sweepTargetAt = new Vector2(Cx * 16f + 10 * 16f, FloorY * 16f);
			int ti = NPC.NewNPC(new EntitySource_WorldEvent("IdentityWorldCheck"), (int)_sweepTargetAt.X, (int)_sweepTargetAt.Y, NPCID.GreenSlime);
			if (ti < 0 || ti >= Main.maxNPCs) throw new Exception("enemy did not spawn");
			_sweepTarget = Main.npc[ti];
			_sweepTarget.lifeMax = 1_000_000; _sweepTarget.life = 1_000_000; _sweepTarget.defense = 0; _sweepTarget.damage = 0; _sweepTarget.knockBackResist = 0f; _sweepTarget.width = 64; _sweepTarget.height = 96;
			_sweepTarget.noGravity = true; _sweepTarget.noTileCollide = true; _sweepTarget.aiStyle = -1; _sweepTarget.friendly = false; _sweepTarget.value = 0f; _sweepTarget.Bottom = _sweepTargetAt;
			var ht = _sweepTarget.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = _sweepTarget.position;

			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Sweep",
				CommanderName = "IdentityTester", CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			Item w = new Item(); w.SetDefaults(weaponType);
			rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true);
			if (ammoType > 0) { Item am = new Item(); am.SetDefaults(ammoType); am.stack = Math.Min(999, am.maxStack); rec.Gear.Ammo = am; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
			rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow;
			RosterSystem.Instance.Regulars.Add(rec);
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) throw new Exception("levy did not spawn");
			Main.npc[body].Bottom = at;

			Main.rand = new Terraria.Utilities.UnifiedRandom(777);
			KeepCommander(); PinSweepTarget(); Step(20); // the levy settles and takes the weapon
			PhaseDiffs.Clear();
			Dictionary<string, string> before = PlayerProbe.WatchedSnapshot(Main.player[0]);
			var leaks = new HashSet<string>(); int fired = 0; var detail = new Dictionary<string, string>();
			for (int k = 0; k < 240; k++)
			{
				rec.Mana = rec.MaxMana;
				Step(1);
				var now = PlayerProbe.WatchedSnapshot(Main.player[0]);
				var fresh = new HashSet<string>(); PlayerProbe.Differences(before, now, fresh);
				foreach (string fk in fresh) if (leaks.Add(fk)) detail[fk] = (before.TryGetValue(fk, out string bv) ? bv : "?") + " -> " + now[fk] + " @tick " + k;
				bool shot = false;
				for (int i = 0; i < Main.maxProjectiles && !shot; i++) { Projectile p = Main.projectile[i]; if (p.active && p.TryGetGlobalProjectile(out SoldierProjectileTag t) && (t.IsSoldierShot || t.Cast || t.DrivenChild)) shot = true; }
				if (shot) fired++;
			}
			foreach (string d in PhaseDiffs) leaks.Add(d);
			ClearSoldiers();
			return (leaks, fired, detail);
		}
	}
}
