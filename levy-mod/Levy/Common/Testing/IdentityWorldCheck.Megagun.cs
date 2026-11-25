using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
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
		// ---------------------------------------------------------------------------------------------- MEGAGUN: five levies hold a hold-down weapon while the commander channels her own
		// Five levies each carry a held-projectile weapon (aiStyle HeldProjectile: the Laser Machinegun, Phantasm, Vortex Beater, Last Prism, Charged Blaster Cannon, ... and Calamity's holdouts) and fight one pinned enemy 12 tiles right of
		// them; the stand-in commander stands 45 tiles to the left. Three readings of the report are run:
		//   idle       the commander holds nothing (reading a: levies carrying hold-down weapons)
		//   cmd-same   the commander channels the SAME weapon herself through the real ItemCheck, so her own held projectile is alive on her (reading b)
		//   cmd-LMG    the commander channels a different hold-down weapon (the Laser Machinegun) while the levies hold this one
		// Every tick each levy's held projectile (the chain holder, and the beam of a thick-laser one) is measured against its levy and against the commander; the shots the weapon makes are followed like the origin sweep does.
		// Env: LEVY_MEGAGUN=0 skips, LEVY_MEGAGUN_ONLY=Name;Name, LEVY_MEGAGUN_CAL=N (Calamity channel weapons added, default 12; 0 none), LEVY_MEGAGUN_TICKS=N (default 150), LEVY_MEGAGUN_LEVIES=N (default 5).

		private sealed class MegaStat
		{
			public string Label, Mode; public int Levies, Ticks, HolderTicks, HolderFar, HolderNearCmd, BeamTicks, BeamFar, OrbitTicks, OrbitNearCmd, ShotsSeen, ShotsAtCmd, CmdHolderTicks, CmdHolderFar; public float MaxHolderDist, MinHolderCmd = 1e9f, MaxCmdHolderDist, MaxBeamDist; public long Damage; public string Err = ""; public int NearCmdTicks, CmdHeldHijack; public string HijackNote = ""; public Dictionary<string, (int n, int near)> ByType = new();
			public override string ToString() => $"{Label} [{Mode}] levies={Levies} holderTicks={HolderTicks} holderFar(>{MegaNearTiles}t)={HolderFar} maxHolderDistLevy={MaxHolderDist / 16f:0.0}t minHolderDistCmd={(MinHolderCmd > 1e8f ? -1 : MinHolderCmd / 16f):0.0}t holderNearCmd={HolderNearCmd} beamTicks={BeamTicks} beamFar={BeamFar} orbitTicks={OrbitTicks} orbitNearCmd={OrbitNearCmd} shots={ShotsSeen} shotsAtCmd={ShotsAtCmd} cmdHolderTicks={CmdHolderTicks} cmdHolderFar={CmdHolderFar} cmdMaxDist={MaxCmdHolderDist / 16f:0.0}t nearCmdTicks={NearCmdTicks} cmdHeldProjHijacked={CmdHeldHijack} {HijackNote}dmg={Damage} [{string.Join(",", ByType.OrderByDescending(kv => kv.Value.n).Take(6).Select(kv => kv.Key + "x" + kv.Value.n + (kv.Value.near > 0 ? "(near" + kv.Value.near + ")" : "")))}]{(Err.Length > 0 ? " ERR " + Err : "")}";
		}

		private const float MegaNearTiles = 6f;
		/// <summary>Weapons that read more of the commander than her buttons (see <see cref="KnownButtonResiduals"/>).</summary>
		private static readonly string[] MegaKnownResiduals = { "AnahitasArpeggio", "MidnightSunBeacon" };
		private static readonly bool MegaDebug = Environment.GetEnvironmentVariable("LEVY_MEGAGUN_DEBUG") == "1";

		/// <summary>The commander uses her own weapon through the real vanilla ItemCheck (the button down, her cursor on the enemy), as a single-player client does every tick.</summary>
		/// <summary>Lab: the stand-in commander's mana pool while she uses a weapon (at least 200; the summon sweep raises it for a staff that costs more a cast, the Universe Splitter's 300).</summary>
		internal static int _cmdManaFloor;
		private static void MegaCommanderUse(Item weapon, bool press)
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false;
			p.statManaMax = p.statManaMax2 = Math.Max(200, _cmdManaFloor); p.statMana = p.statManaMax2;
			if (p.inventory[0] == null || p.inventory[0].IsAir || p.inventory[0].type != weapon.type) p.inventory[0] = weapon.Clone();
			p.selectedItem = 0; p.noItems = false;
			Vector2 aim = _sweepTarget != null ? _sweepTarget.Center + _abAimOffset : p.Center + new Vector2(300f, 0f);
			Main.screenPosition = Vector2.Zero; Main.mouseX = (int)aim.X; Main.mouseY = (int)aim.Y;
			p.controlUseItem = press; p.controlUseTile = false; p.channel = press && weapon.channel; if (!press) p.releaseUseItem = true;
			Main.mouseLeft = press; Main.mouseLeftRelease = !press;
			p.direction = aim.X >= p.Center.X ? 1 : -1;
			if (_itemCheck == null)
				foreach (var m in typeof(Player).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
					if (m.Name == "ItemCheck") { var ps = m.GetParameters(); if (ps.Length == 0) { _itemCheck = m; _itemCheckTakesInt = false; break; } if (ps.Length == 1 && ps[0].ParameterType == typeof(int)) { _itemCheck = m; _itemCheckTakesInt = true; } }
			int saved = Main.myPlayer;
			try { Main.myPlayer = 0; InStandInUse = true; _itemCheck.Invoke(p, _itemCheckTakesInt ? new object[] { 0 } : null); }
			finally { Main.myPlayer = saved; InStandInUse = false; }
		}

		private static MegaStat MegaOne(int weaponType, int ammoType, string label, string mode, int levies, int ticks, Item cmdWeapon, int cmdAmmoType)
		{
			var st = new MegaStat { Label = label, Mode = mode, Levies = levies };
			_cmdAt = OriginCmdAt(); ClearSoldiers(); _cmdHolds = false; ReleaseCommanderButtons();
			Player cmd = Main.player[0]; cmd.inventory[0] = new Item(); cmd.heldProj = -1; cmd.itemAnimation = 0; cmd.itemTime = 0;
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			SpawnOriginTarget(at);
			var bodies = new List<int>();
			for (int k = 0; k < levies; k++)
			{
				Item w = new Item(); w.SetDefaults(weaponType);
				SoldierRecord rec = MakeOriginRecord(w, ammoType); rec.Name = "Pvt. Mega" + k;
				RosterSystem.Instance.Regulars.Add(rec);
				Vector2 pos = at + new Vector2(-34f * k, 0f);
				int b = RosterSystem.Instance.SpawnBody(rec, pos);
				if (b < 0 || !rec.HasBody) throw new Exception("levy " + k + " did not spawn");
				Main.npc[b].Bottom = pos; bodies.Add(b);
			}
			if (cmdWeapon != null && cmdAmmoType > 0) { Item am = new Item(); am.SetDefaults(cmdAmmoType); am.stack = 999; cmd.inventory[54] = am; for (int s = 50; s < 54; s++) cmd.inventory[s] = new Item(); }
			Main.rand = new Terraria.Utilities.UnifiedRandom(777);
			KeepCommander(); PinSweepTarget(); CursorOnCommander(); Step(10);
			var seen = new HashSet<(int, int)>();
			for (int k = 0; k < ticks; k++)
			{
				foreach (var r in RosterSystem.Instance.Regulars) r.Mana = r.MaxMana;
				if (cmdWeapon != null) { try { MegaCommanderUse(cmdWeapon, true); } catch (Exception e) { st.Err = "cmd use " + (e is TargetInvocationException && e.InnerException != null ? e.InnerException.GetType().Name : e.GetType().Name); } } else CursorOnCommander();
				Step(1); ClearNaturalHostiles();
				if (_sweepTarget != null && _sweepTarget.active) { int d = _sweepTarget.lifeMax - _sweepTarget.life; if (d > 0) st.Damage += d; }
				st.Ticks++;
				Vector2 cmdC = Main.player[0].Center;
				if (cmdWeapon != null && cmdWeapon.channel)
				{
					// the commander's held-projectile slot must still be her own holder (a levy's holdout writes owner.heldProj every tick)
					int hp = Main.player[0].heldProj; bool ownHolder = hp >= 0 && hp < Main.maxProjectiles && Main.projectile[hp].active && Main.projectile[hp].owner == 0 && !(Main.projectile[hp].TryGetGlobalProjectile(out SoldierProjectileTag ht) && ht.SoldierIndex >= 0);
					if (!ownHolder) { st.CmdHeldHijack++; if (st.HijackNote.Length == 0) st.HijackNote = $"firstHijackTick={k} heldProj={hp}({(hp >= 0 && hp < Main.maxProjectiles ? (Main.projectile[hp].ModProjectile?.Name ?? ProjectileID.Search.GetName(Main.projectile[hp].type)) + "@owner" + Main.projectile[hp].owner : "none")}) "; }
				}
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active || p.hostile) continue;
					bool tagged = p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0;
					if (!tagged)
					{
						// the commander's own projectile: her held one must stay on her
						if (p.owner == 0 && p.aiStyle == ProjAIStyleID.HeldProjectile)
						{
							st.CmdHolderTicks++; float dc0 = Vector2.Distance(p.Center, cmdC); st.MaxCmdHolderDist = Math.Max(st.MaxCmdHolderDist, dc0); if (dc0 > MegaNearTiles * 16f) st.CmdHolderFar++;
						}
						continue;
					}
					if (!bodies.Contains(tag.SoldierIndex)) continue;
					Vector2 levyC = Main.npc[tag.SoldierIndex].Center;
					float dl = Vector2.Distance(p.Center, levyC), dc = Vector2.Distance(p.Center, cmdC);
					bool chain = p.TryGetGlobalProjectile(out HeldWeaponProjectile g) && g.Chain;
					if (!chain && p.type == ProjectileID.FinalFractal) { st.OrbitTicks++; if (dc < dl) st.OrbitNearCmd++; } // Zenith's swords orbit their owner: the levy, never the commander
					if (chain && p.aiStyle == ProjAIStyleID.HeldProjectile)
					{
						if (MegaDebug && st.HolderTicks < 6) Note($"  dbg {label} {mode} t={k} proj#{i} type={p.type} owner={p.owner} pos=({p.Center.X / 16f:0.0},{p.Center.Y / 16f:0.0}) levy#{tag.SoldierIndex}=({levyC.X / 16f:0.0},{levyC.Y / 16f:0.0}) cmd=({cmdC.X / 16f:0.0},{cmdC.Y / 16f:0.0}) vel=({p.velocity.X:0.0},{p.velocity.Y:0.0}) ai=({p.ai[0]:0.0},{p.ai[1]:0.0}) rot={p.rotation:0.00} tl={p.timeLeft} ownerSlotPos=({Main.player[p.owner].Center.X / 16f:0.0},{Main.player[p.owner].Center.Y / 16f:0.0}) dummyHeld={(Main.npc[tag.SoldierIndex].ModNPC as SoldierNPC)?.Dummy?.heldProj}");
						st.HolderTicks++; st.MaxHolderDist = Math.Max(st.MaxHolderDist, dl); st.MinHolderCmd = Math.Min(st.MinHolderCmd, dc);
						if (dl > MegaNearTiles * 16f) st.HolderFar++;
						if (dc < MegaNearTiles * 16f && dc < dl) st.HolderNearCmd++;
					}
					else if (chain)
					{
						st.BeamTicks++; st.MaxBeamDist = Math.Max(st.MaxBeamDist, dl);
						if (dl > 2.5f * 16f && dc < dl) st.BeamFar++; // a beam starts at its levy; one that starts closer to the commander is hers
					}
					bool nearCmd = dc < 10 * 16f && dc < dl; string pn = p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type);
					if (nearCmd) st.NearCmdTicks++;
					st.ByType.TryGetValue(pn, out var bt); bool first = seen.Add((i, p.identity)); st.ByType[pn] = (bt.n + (first ? 1 : 0), bt.near + (nearCmd ? 1 : 0));
					if (MegaDebug && first && st.ShotsSeen < 12) Note($"  dbg {label} {mode} t={k} new {pn} owner={p.owner} levy={tag.SoldierIndex} pos(levy)=({(p.Center.X - levyC.X) / 16f:0.0},{(p.Center.Y - levyC.Y) / 16f:0.0}) pos(cmd)=({(p.Center.X - cmdC.X) / 16f:0.0},{(p.Center.Y - cmdC.Y) / 16f:0.0}) vel=({p.velocity.X:0.0},{p.velocity.Y:0.0}) ai=({p.ai[0]:0.0},{p.ai[1]:0.0})");
					if (first)
					{
						st.ShotsSeen++;
						if (dc < 10 * 16f && dc < dl) st.ShotsAtCmd++;
					}
				}
			}
			if (cmdWeapon != null) { try { MegaCommanderUse(cmdWeapon, false); } catch (Exception) { } }
			ClearSoldiers(); ReleaseCommanderButtons(); cmd.inventory[0] = new Item(); cmd.inventory[54] = new Item(); cmd.heldProj = -1; cmd.itemAnimation = 0; cmd.itemTime = 0;
			return st;
		}


		private static void MegagunCheck()
		{
			if (Environment.GetEnvironmentVariable("LEVY_MEGAGUN") == "0") return;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_MEGAGUN_TICKS"), out int tk) ? tk : 150;
			int levies = int.TryParse(Environment.GetEnvironmentVariable("LEVY_MEGAGUN_LEVIES"), out int lv) ? lv : 5;
			int calN = int.TryParse(Environment.GetEnvironmentVariable("LEVY_MEGAGUN_CAL"), out int cn) ? cn : 12;
			string only = Environment.GetEnvironmentVariable("LEVY_MEGAGUN_ONLY");
			var weapons = new List<(int type, string label)>();
			foreach (int t in new[] { ItemID.LaserMachinegun, ItemID.Phantasm, ItemID.VortexBeater, ItemID.LastPrism, ItemID.ChargedBlasterCannon, ItemID.ChainGun, ItemID.ClockworkAssaultRifle, ItemID.SDMG, ItemID.Megashark, ItemID.StarCannon, ItemID.Zenith })
				weapons.Add((t, "Terraria:" + ItemID.Search.GetName(t)));
			if (calN > 0 && ModLoader.TryGetMod("CalamityMod", out Mod cal))
			{
				int n = 0;
				for (int i = ItemID.Count; i < ItemLoader.ItemCount && n < calN; i++)
				{
					if (!ContentSamples.ItemsByType.TryGetValue(i, out Item s) || s == null || s.IsAir || s.ModItem?.Mod != cal || !s.channel || s.shoot <= 0 || s.damage <= 0) continue;
					weapons.Add((i, "CalamityMod:" + s.ModItem.Name)); n++;
				}
				// the two weapons the designer still saw "fire from the commander" in 3.0.8: the plant flamethrower (Flamethrower + Meowthrower + Living Shards) and the Mythril / Dubious Plating / Mysterious Circuitry magic gun (the old Gauss Pistol)
				foreach (string nm in new[] { "WildfireBloom", "Vulcan" })
					if (cal.TryFind(nm, out ModItem mi) && !weapons.Any(w => w.type == mi.Type)) weapons.Add((mi.Type, "CalamityMod:" + nm));
			}
			if (!string.IsNullOrEmpty(only)) { string[] names = only.Split(';', StringSplitOptions.RemoveEmptyEntries); weapons = weapons.Where(w => names.Any(n => w.label.EndsWith(":" + n))).ToList(); }
			if (weapons.Count == 0) return;
			SetupOriginArena();
			_spNet = Environment.GetEnvironmentVariable("LEVY_MEGAGUN_SPNET") == "1";
			var ammoByKind = AmmoByKind();
			var all = new List<(MegaStat idle, MegaStat own, MegaStat other)>();
			Item lmg = new Item(); lmg.SetDefaults(ItemID.LaserMachinegun);
			var sw = System.Diagnostics.Stopwatch.StartNew();
			try
			{
				KeepCommander(); Step(2);
				foreach (var (type, label) in weapons)
				{
					try
					{
						Item sample = ContentSamples.ItemsByType[type];
						int ammo = sample.useAmmo > 0 && ammoByKind.TryGetValue(sample.useAmmo, out int a) ? a : 0;
						Item mine = new Item(); mine.SetDefaults(type);
						MegaStat idle = MegaOne(type, ammo, label, "idle", levies, ticks, null, 0);
						MegaStat own = MegaOne(type, ammo, label, "cmd-same", levies, ticks, mine, ammo);
						MegaStat other = type == ItemID.LaserMachinegun ? own : MegaOne(type, ammo, label, "cmd-LMG", levies, ticks, lmg, 0);
						all.Add((idle, own, other));
						Note("megagun: " + idle); Note("megagun: " + own); if (!ReferenceEquals(other, own)) Note("megagun: " + other);
					}
					catch (Exception e) { Note($"megagun: {label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}"); }
				}
			}
			finally { _spNet = false; _sweepTarget = null; sw.Stop(); ClearSoldiers(); ReleaseCommanderButtons(); }
			Note($"megagun: {all.Count} weapons x 3 modes run in {sw.ElapsedMilliseconds / 1000} s ({ticks} ticks, {levies} levies each)");
			foreach (var (idle, own, other) in all)
				foreach (MegaStat s in new[] { idle, own, other }.Distinct())
				{
					// a levy's holder (the held projectile, aiStyle HeldProjectile) stays on its levy and never goes to the commander; her own held projectile stays on her; the weapon still damages the enemy
					bool ran = s.HolderTicks > 0;
					Check($"megagun: {s.Label} [{s.Mode}] every levy's held projectile stays on its levy (max {s.MaxHolderDist / 16f:0.0} tiles from it over {s.HolderTicks} ticks, {s.HolderFar} ticks beyond {MegaNearTiles} tiles, {s.HolderNearCmd} ticks nearer the commander)", !ran || (s.HolderFar == 0 && s.HolderNearCmd == 0));
					if (s.CmdHolderTicks > 0) Check($"megagun: {s.Label} [{s.Mode}] the commander's held-projectile slot stays her own holder ({s.CmdHeldHijack} of {s.CmdHolderTicks} ticks it pointed at something else {s.HijackNote})", s.CmdHeldHijack == 0);
					if (s.CmdHolderTicks > 0) Check($"megagun: {s.Label} [{s.Mode}] the commander's own held projectile stays on her (max {s.MaxCmdHolderDist / 16f:0.0} tiles from her over {s.CmdHolderTicks} ticks)", s.CmdHolderFar == 0);
					if (s.BeamTicks > 0) Check($"megagun: {s.Label} [{s.Mode}] a levy's beam starts at the levy ({s.BeamFar} of {s.BeamTicks} ticks it starts nearer the commander)", s.BeamFar == 0);
					if (s.OrbitTicks > 0) Check($"megagun: {s.Label} [{s.Mode}] an orbiting weapon circles its levy ({s.OrbitNearCmd} of {s.OrbitTicks} ticks nearer the commander)", s.OrbitNearCmd == 0);
					Check($"megagun: {s.Label} [{s.Mode}] no shot of the levies is first seen at the commander ({s.ShotsAtCmd} of {s.ShotsSeen})", s.ShotsAtCmd == 0);
				}
			// the levies still hurt the enemy in every reading (a held weapon that was left unanchored sat at the world's corner and dealt nothing). A Calamity holdout's fire rate is read from the commander's
			// held item (observed: IonBlasterHoldout.fireRate = Owner.HeldItem.useAnimation), so its output depends on what she holds and is not compared with the idle run, only required to be alive.
			foreach (var (idle, own, other) in all)
				if (idle.Damage > 0 && !MegaKnownResiduals.Any(k => idle.Label.EndsWith(":" + k)))
					Check($"megagun: {idle.Label} the levies still deal damage while the commander channels (idle {idle.Damage}, same weapon {own.Damage}, other weapon {other.Damage})", own.Damage > 0 && other.Damage > 0);
			// the held weapons are alive at all (the wrapper that anchors them to the levy ran: holder ticks and damage in the idle run)
			foreach (var (idle, own, other) in all.Where(a => a.idle.Label.StartsWith("Terraria:") && (a.idle.HolderTicks > 0 || a.idle.OrbitTicks > 0 || a.idle.Label.EndsWith(":LaserMachinegun"))))
				Check($"megagun: {idle.Label} the held weapon runs on its levies (holder ticks {idle.HolderTicks}, orbit ticks {idle.OrbitTicks}, damage {idle.Damage})", idle.Damage > 0);
		}
	}
}
