using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Flavor;
using Levy.Common.Targeting;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;
using Levy.Content.Tiles;
using Mechanization.Content.Items;
using Mechanization.Content.NPCs;

namespace Mechanization.Common
{
	/// <summary>The world checks for the machines' placement, reach, weapon choice, hurt look and real shot path (see MechWorldCheck).</summary>
	public partial class MechWorldCheck
	{
		private static void PlatformRow(int cx, int row, int half)
		{
			for (int x = cx - half; x <= cx + half; x++) { Tile t = Main.tile[x, row]; t.HasTile = true; t.TileType = TileID.Platforms; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = BlockType.Solid; }
			_placed.Add((cx - half, row, half * 2 + 1, 1));
		}

		private static TurretNPC FindTurret() { TurretNPC t = null; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is TurretNPC tn) t = tn; return t; }

		/// <summary>
		/// A turret set down on a platform rests on it (the rule a field drone follows): the cursor on the platform, just above it, well above it; set down under it or on the ground it rests on the ground. It is still on the platform
		/// after a few seconds of real ticks (the fall never takes it through). Placed through the real item and the server's request path. Logged as "[TurretPlace] self-check pass=N fail=N".
		/// </summary>
		private static void TurretPlatformRule()
		{
			int cx = 1200; PrepareArena(cx);
			_secPass = _secFail = 0;
			float floorTop = FloorY * 16f; int platRow = FloorY - 8; float platTop = platRow * 16f;
			PlatformRow(cx, platRow, 6);
			Player p = Main.player[0];
			TurretSystem sys = TurretSystem.Instance;
			bool Near(float a, float b, float tol = 2.5f) => Math.Abs(a - b) <= tol;
			TurretNPC Place(Vector2 cursor, string label)
			{
				KillAll<TurretNPC>(); sys.OnWorldUnload();
				bool used = UseItem(p, new Item(ModContent.ItemType<TurretItem>(), 2), cursor, out string why);
				TurretNPC t = FindTurret();
				Note($"{label}: cursor {cursor} used={used} turret bottom {(t != null ? t.NPC.Bottom.Y.ToString("0.0") : "-")} (floor top {floorTop}, platform top {platTop}) {why}");
				return t;
			}
			TurretNPC t0 = Place(new Vector2(cx * 16f + 8f, platRow * 16f + 8f), "cursor on a platform");
			Check("turret platform: placed with the cursor on the platform", t0 != null);
			if (t0 != null) { Check("turret platform: it rests on the platform, not the floor below", Near(t0.NPC.Bottom.Y, platTop) && Near(t0.NPC.Center.X, cx * 16f + 8f, 17f)); Step(240); Check("turret platform: after 4 seconds it is still on the platform", t0.NPC.active && Near(t0.NPC.Bottom.Y, platTop)); }
			t0 = Place(new Vector2(cx * 16f + 8f, platTop - 20f), "cursor 20px above a platform");
			if (t0 != null) { Check("turret platform, just above: it rests on the platform", Near(t0.NPC.Bottom.Y, platTop)); Step(240); Check("turret platform, just above: still on it after 4 seconds", t0.NPC.active && Near(t0.NPC.Bottom.Y, platTop)); }
			t0 = Place(new Vector2(cx * 16f, platTop - 6f * 16f), "cursor 6 tiles above a platform");
			if (t0 != null) { Step(240); Check("turret platform, well above: it falls onto the platform and stays there", t0.NPC.active && Near(t0.NPC.Bottom.Y, platTop)); }
			t0 = Place(new Vector2(cx * 16f + 8f, floorTop - 20f), "cursor 20px above the floor, under the platform");
			if (t0 != null) { Step(120); Check("turret platform, under it: it rests on the floor", Near(t0.NPC.Bottom.Y, floorTop)); }
			t0 = Place(new Vector2((cx + 30) * 16f, floorTop - 20f), "cursor above open ground");
			if (t0 != null) { Step(120); Check("turret platform, open ground: it rests on the ground", Near(t0.NPC.Bottom.Y, floorTop)); }

			p.active = true; p.dead = false; KillAll<TurretNPC>(); sys.OnWorldUnload();
			Vector2 onPlat = new Vector2(cx * 16f + 8f, platRow * 16f + 8f);
			p.position = onPlat - new Vector2(p.width / 2f, p.height / 2f);
			p.inventory[0] = new Item(ModContent.ItemType<TurretItem>()); p.selectedItem = 0;
			Step(15); p.position = onPlat - new Vector2(p.width / 2f, p.height / 2f); p.active = true;
			bool placed = sys.PlaceFor(p, onPlat);
			TurretNPC sv = FindTurret();
			Check("turret platform, server request: it rests on the platform", placed && sv != null && Near(sv.NPC.Bottom.Y, platTop));
			p.inventory[0] = new Item();
			KillAll<TurretNPC>(); sys.OnWorldUnload(); ClearPlaced();
			_mod.Logger.Info($"[TurretPlace] self-check pass={_secPass} fail={_secFail}");
		}

		/// <summary>Whether a turret hits a pinned enemy at each distance, with a sniper rifle in the Armory and with no Armory at all. The reach it reports is logged beside what it did.</summary>
		private static void TurretReach()
		{
			int cx = 1200; PrepareArena(cx); Flatten(cx, 135);
			Player p = Main.player[0];
			int[] dists = { 20, 40, 60, 80, 100, 115 };
			foreach (bool armed in new[] { true, false })
			{
				KillAll<TurretNPC>(); TurretSystem.Instance.OnWorldUnload(); ClearPlaced(); Flatten(cx, 135);
				int tx = cx - 125;
				if (armed) ArmoryWith(cx, -133, ItemID.SniperRifle); // behind the turret, out of the line of fire
				_cmdAt = new Vector2(tx * 16f, (FloorY - 3) * 16f);
				UseItem(p, new Item(ModContent.ItemType<TurretItem>(), 2), new Vector2(tx * 16f, (FloorY - 1) * 16f), out string why);
				Step(120);
				TurretNPC t = FindTurret();
				if (t == null) { Check($"turret reach ({(armed ? "sniper rifle" : "no Armory")}): placed", false); continue; }
				Note($"turret reach, {(armed ? "sniper rifle in the Armory" : "no Armory")}: weapon type {t.MountedWeaponType}, reach {t.ReachPx / 16f:0.0} tiles");
				var hit = new List<string>(); int farthest = 0;
				foreach (int d in dists)
				{
					for (int k = 0; k < Main.maxProjectiles; k++) Main.projectile[k].active = false; // rounds still flying from the last distance would hit this one
					NPC tgt = Target(cx, 0); Vector2 at = t.NPC.Center + new Vector2(d * 16f, -12f) - tgt.Size * 0.5f; int seen = 0;
					MechShotProbe.On = true; MechShotProbe.Spawned = 0;
					_pinTgt = tgt; _pinAt = at; _pinFollow = null; _localIsSlot0 = true; // the pin runs before every NPC update, as in the player-safety check
					try { for (int k = 0; k < 240; k++) { KeepCommander(); Step(1); } }
					finally { _pinTgt = null; _localIsSlot0 = false; }
					MechShotProbe.On = false; seen = MechShotProbe.Spawned; // rounds the turret fired
					if (d == 20) Note($"turret reach debug: after 240 ticks target slot {tgt.whoAmI} active={tgt.active} life={tgt.life}/{tgt.lifeMax} at {tgt.Center} ({Vector2.Distance(tgt.Center, t.NPC.Center) / 16f:0.0} tiles from the turret at {t.NPC.Center}); turret holds target {t.HeldTarget}, alive {t.NPC.active}, hostile={Levy.Common.Util.LevyUtil.IsHostileTarget(tgt)}, never={Levy.Common.Targeting.SoldierTargeting.IsNeverTarget(tgt)}, canHit={Collision.CanHitLine(t.NPC.position, t.NPC.width, t.NPC.height, tgt.position, tgt.width, tgt.height)}");
					tgt.active = false; Step(2);
					hit.Add($"{d}:{(seen > 0 ? "fires" : "-")}");
					if (seen > 0) farthest = d;
				}
				Note($"turret reach, {(armed ? "sniper" : "plain bullet")}: opens fire at tiles [{string.Join(" ", hit)}] (farthest {farthest})");
				if (armed) Check("turret reach: a sniper rifle turret opens fire on an enemy 100 tiles out", farthest >= 100);
				else Check("turret reach: the plain-bullet turret opens fire on an enemy 20 tiles out and holds fire past its 32", farthest >= 20 && farthest < 40);
			}
			KillAll<TurretNPC>(); TurretSystem.Instance.OnWorldUnload(); ClearPlaced();
		}

		/// <summary>Which weapon each drone mounts from a mixed Armory: the player-built drones and the Barracks garrison, one line per drone.</summary>
		private static void DroneWeaponMix()
		{
			int[] guns = { ItemID.Handgun, ItemID.Minishark, ItemID.Musket, ItemID.TheUndertaker, ItemID.Boomstick, ItemID.SniperRifle };
			int cx = 1200; PrepareArena(cx);
			ArmoryTE armory = ArmoryWith(cx, -10, guns);
			if (armory != null) armory.Storage[guns.Length + 1] = new Item(ItemID.Bananarang, 25);
			Player p = Main.player[0];
			for (int k = 0; k < 5; k++) UseItem(p, new Item(ModContent.ItemType<DroneItem>(), 2), new Vector2((cx - 20 + k * 8) * 16f, (FloorY - 6) * 16f), out _);
			Step(240);
			var types = new List<int>();
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is DroneNPC d && d.IsField) { types.Add(d.FieldWeaponType); Note($"field drone {types.Count}: mounts {(d.FieldWeaponType > 0 ? Lang.GetItemNameValue(d.FieldWeaponType) : "the plain bullet")}"); }
			Note($"mixed Armory ({guns.Length} guns + 25 Bananarangs): {types.Count} field drones mount {types.Distinct().Count()} distinct weapon(s)");
			KillAll<DroneNPC>(); FieldDroneSystem.Instance?.OnWorldUnload();
			if (armory != null) { TileEntity.ByID.Remove(armory.ID); }
			ClearPlaced();

			PrepareArena(cx);
			GuideSystem g = Levy.Common.Flavor.GuideSystem.Instance; bool savedEng = g.Engineering; g.Engineering = true;
			Point16 bar = RealPlace(ModContent.TileType<BarracksTile>(), 0, cx + 2, FloorY - 1);
			ArmoryTE garm = ArmoryWith(cx, 8, guns.Concat(new int[] { ItemID.Shotgun, ItemID.FlintlockPistol, ItemID.RedRyder }).ToArray());
			Step(3300);
			var gt = new List<int>();
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is DroneNPC d && d.IsBound && !d.IsField) { gt.Add(d.FieldWeaponType); Note($"garrison drone {gt.Count}: mounts {(d.FieldWeaponType > 0 ? Lang.GetItemNameValue(d.FieldWeaponType) : "nothing")}"); }
			Note($"mixed Armory (9 guns): {gt.Count} garrison drones mount {gt.Distinct().Count()} distinct weapon(s)");
			KillAll<DroneNPC>(); g.Engineering = savedEng;
			if (garm != null) TileEntity.ByID.Remove(garm.ID);
			ClearPlaced();
		}

		/// <summary>The hurt look: the tint deepens with the damage, the alarm gap shortens, a healthy machine stays quiet, and the death blast on a server draws nothing.</summary>
		private static void MachineFxCheck()
		{
			Color lit = new Color(200, 200, 200);
			Color full = MachineFx.Hurt(lit, 100, 100), half = MachineFx.Hurt(lit, 50, 100), low = MachineFx.Hurt(lit, 10, 100);
			Check("hurt look: a full-health machine is untouched", full == lit);
			Check("hurt look: the tint goes redder as life drops (green and blue fall, red does not)", half.G < full.G && low.G < half.G && low.B < half.B && low.R >= full.R);
			Check("alarm: a healthy machine stays quiet, a hurt one beeps faster as it nears death",
				MachineFx.BeepGap(100, 100) == 0 && MachineFx.BeepGap(40, 100) == 0 && MachineFx.BeepGap(30, 100) > MachineFx.BeepGap(5, 100) && MachineFx.BeepGap(5, 100) >= MachineFx.FastBeepTicks);
			Check("alarm: a dedicated server plays nothing", !MachineFx.TryBeep(Vector2.Zero));
			int dust = 0; for (int i = 0; i < Main.maxDust; i++) if (Main.dust[i].active) dust++;
			MachineFx.Explode(new EntitySource_Misc("MechWorldCheck"), new Vector2(1200 * 16f, 280 * 16f), 30);
			int dust2 = 0; for (int i = 0; i < Main.maxDust; i++) if (Main.dust[i].active) dust2++;
			Check("the death blast spawns no projectile and nothing on a server", dust2 == dust);
		}

		// ---------------------------------------------------------------------------------------------- every weapon through the machine's real shot path
		private sealed class AuditBody : ITargetBody
		{
			public NPC NPC { get; set; } public Player Dummy { get; } = new Player();
			public int TargetAggro => 0; public bool IsAlive => true;
			public AuditBody(NPC n) { NPC = n; Dummy.active = true; Dummy.whoAmI = Levy.Content.NPCs.SoldierNPC.DummySlot; global::Levy.Common.Owner.SoldierOwner.RegisterDummy(Dummy); }
			public void SyncDummyTransform() { Dummy.position = NPC.position; Dummy.active = true; Dummy.whoAmI = Levy.Content.NPCs.SoldierNPC.DummySlot; }
		}

		/// <summary>
		/// Every mountable ranged and magic weapon (with every round it takes), asked the same question three ways: what the game's own Player.ItemCheck_Shoot spawns for a player holding it (the truth), what the old light shot
		/// would have fired (the projectile DroneArsenal resolves), and what the machine spawns now through <see cref="MachineShots"/>. A weapon "matches" when what the machine fires is among the types the real use spawns.
		/// Every shot is also fired with slot 0 as the single-player owner and every field of the player and its mod players compared across it (player-safety must be 0). TSV to LEVY_MECHSHOTS_OUT.
		/// </summary>
		private static void MachineShotAudit()
		{
			MethodInfo shootM = typeof(Player).GetMethod("ItemCheck_Shoot", BindingFlags.Instance | BindingFlags.NonPublic);
			if (shootM == null) { Check("machine shot audit: Player.ItemCheck_Shoot found", false); return; }
			int cx = 1200; PrepareArena(cx);
			var ammoByKind = new Dictionary<int, List<Item>>();
			for (int i = 1; i < ItemLoader.ItemCount; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item smp) || smp == null || smp.IsAir || smp.ammo <= 0 || smp.IsACoin) continue;
				if (!ammoByKind.TryGetValue(smp.ammo, out var l)) ammoByKind[smp.ammo] = l = new List<Item>();
				l.Add(smp);
			}
			NPC shooter = Target(cx, -4), tgt = Target(cx, 14);
			Vector2 shooterAt = shooter.position, tgtAt = tgt.position;
			var body = new AuditBody(shooter);
			var rows = new List<string> { "weapon\tmod\tkind\tammo\ttruth\told\tmachine\tbefore\tafter\tholdoutHooks\tsafety" };
			var byMod = new SortedDictionary<string, int[]>(); // total, before OK, after OK, fixed, regressed, truth empty, holdout, threw, unsafe
			int pairs = 0, unsafeN = 0, threwN = 0;
			var unsafeList = new List<string>(); var regressed = new List<string>(); var stillWrong = new List<string>();
			Player saved = Main.player[0]; int savedMy = Main.myPlayer;
			var control = new HashSet<string>(); string onlyNames = Environment.GetEnvironmentVariable("LEVY_MECHSHOTS_ONLY");
			int h0 = MachineShots.Holdouts, e0 = MachineShots.Empty, t0 = MachineShots.Threw;
			try
			{
				_localIsSlot0 = true; _pinTgt = null; // slot 0 is the local player only inside the NPC and projectile passes (and the direct calls below); a whole-tick myPlayer of 0 would run the client-only parts of Player.Update on a server
				KeepCommander(); Step(1);
				for (int c = 0; c < 2; c++) { var before = PlayerSnapshot(Main.player[0]); Step(3); var d = new HashSet<string>(); Differences(before, PlayerSnapshot(Main.player[0]), d); foreach (string f in d) if (IsWatched(f)) control.Add(f); }
				for (int wi = 1; wi < ItemLoader.ItemCount; wi++)
				{
					if (!ContentSamples.ItemsByType.TryGetValue(wi, out Item w0) || w0 == null || w0.IsAir) continue;
					Item probe = new Item(); probe.SetDefaults(wi);
					if (!DroneArsenal.IsMountable(probe)) continue;
					if (!string.IsNullOrEmpty(onlyNames) && !onlyNames.Split(',').Any(n => Lang.GetItemNameValue(wi).Contains(n, StringComparison.OrdinalIgnoreCase))) continue;
					string mod = probe.ModItem?.Mod.Name ?? "Terraria";
					string wname = (probe.ModItem != null ? mod + "/" : "") + Lang.GetItemNameValue(wi);
					bool ammoWeapon = probe.useAmmo > 0;
					List<Item> rounds = new List<Item>();
					if (ammoWeapon) { if (ammoByKind.TryGetValue(probe.useAmmo, out var al)) rounds.AddRange(al); } else rounds.Add(null);
					bool first = true;
					foreach (Item round in rounds)
					{
						pairs++;
						DroneShot shot = DroneArsenal.BuildShot(probe, round);
						if (!shot.Valid) continue;
						int old = shot.Projectile;
						// truth: the game's own use, on a scratch player
						var truth = new SortedSet<int>(); string err = null;
						try
						{
							var sp = new Player(); sp.whoAmI = 0; sp.active = true; sp.position = new Vector2(cx * 16 - 400, 280 * 16); sp.direction = 1;
							Main.player[0] = sp;
							for (int attempt = 0; attempt < 6; attempt++)
							{
								Item weapon = new Item(); weapon.SetDefaults(wi); weapon.stack = 1; sp.inventory[0] = weapon; sp.selectedItem = 0;
								if (round != null) { Item am = new Item(); am.SetDefaults(round.type); am.stack = am.maxStack; sp.inventory[54] = am; }
								sp.itemAnimation = sp.itemAnimationMax = Math.Max(1, weapon.useAnimation); sp.itemTime = 0; sp.controlUseItem = true; sp.channel = true;
								sp.statMana = sp.statManaMax2 = 400;
								Main.mouseX = (int)(sp.Center.X + 300 - Main.screenPosition.X); Main.mouseY = (int)(sp.Center.Y - Main.screenPosition.Y);
								var beforeSet = new HashSet<long>(); for (int k = 0; k < Main.maxProjectiles; k++) if (Main.projectile[k].active) beforeSet.Add(k * 100000L + Main.projectile[k].identity);
								int sm0 = Main.myPlayer; Main.myPlayer = 0;
								try { shootM.Invoke(sp, new object[] { 0, weapon, weapon.damage }); } finally { Main.myPlayer = sm0; }
								for (int k = 0; k < Main.maxProjectiles; k++) { Projectile pr = Main.projectile[k]; if (!pr.active) continue; if (!beforeSet.Contains(k * 100000L + pr.identity)) truth.Add(pr.type); pr.active = false; }
								if (truth.Contains(old)) break;
							}
						}
						catch (Exception e) { err = (e.InnerException ?? e).GetType().Name; }
						finally { Main.player[0] = saved; }
						// the machine: the new path, from the dummy of a pinned body, owned by slot 0
						var made = new SortedSet<int>(); bool holdout; bool threwHere = false; var diffs = new HashSet<string>();
						int hBefore = MachineShots.Holdouts; HashSet<string> sticky = null;
						bool firstOfWeapon = first;
						for (int attempt = 0; attempt < 3; attempt++) // a flagged field is shot again (twice at most): the player's own update moves a field now and then by itself, a shot that touches the player does it every time
						{
						diffs = new HashSet<string>(); first = firstOfWeapon;
						try
						{
							KeepCommander();
							Pin(shooter, shooterAt); Pin(tgt, tgtAt);
							var noise = new HashSet<string>();
							if (first) // a control for THIS weapon: the same three ticks with nothing fired, so whatever the player's own update does at this moment is not blamed on the shot
							{
								global::Levy.Common.Testing.WorldStep.Update();
								var csnap = PlayerSnapshot(Main.player[0]); var call = new HashSet<string>();
								for (int k = 0; k < 3; k++) { KeepCommander(); Pin(shooter, shooterAt); Pin(tgt, tgtAt); global::Levy.Common.Testing.WorldStep.Update(); Differences(csnap, PlayerSnapshot(Main.player[0]), call); }
								foreach (string f in call) if (IsWatched(f)) noise.Add(f);
								KeepCommander(); Pin(shooter, shooterAt); Pin(tgt, tgtAt); global::Levy.Common.Testing.WorldStep.Update();
							}
							var snap = PlayerSnapshot(Main.player[0]);
							Vector2 muzzle = shooter.Center; Vector2 vel = ShotKit.AimVelocity(shot, muzzle, tgt);
							int dmg = ShotKit.ScaledDamage(shot.BaseDamage);
							var beforeSet = new HashSet<int>(); for (int k = 0; k < Main.maxProjectiles; k++) if (Main.projectile[k].active) beforeSet.Add(k);
							int sm = Main.myPlayer; Main.myPlayer = 0;
							try { OwnerGuard.Hold(0, () => MachineShots.Fire(body, shot, muzzle, vel, dmg, 0, tgt.Center, pr => ShotKit.TagShot(pr, 0, shot.Class, shot.Crit), out _)); }
							finally { Main.myPlayer = sm; }
							for (int k = 0; k < Main.maxProjectiles; k++) if (Main.projectile[k].active && !beforeSet.Contains(k)) made.Add(Main.projectile[k].type);
							var all = new HashSet<string>(); Differences(snap, PlayerSnapshot(Main.player[0]), all);
							foreach (string f in all) if (IsWatched(f) && !control.Contains(f) && !noise.Contains(f)) { diffs.Add("fire:" + f); if (onlyNames != null) Note($"audit detail {wname}: fire changed {f}: {snap.GetValueOrDefault(f)} -> {PlayerSnapshot(Main.player[0]).GetValueOrDefault(f)}"); }
							if (first) // one short flight per weapon: the rounds' own first ticks run on the real world update, guarded as in play
							{
								Vector2 v0 = Main.player[0].velocity;
								for (int k = 0; k < 3; k++)
								{
									KeepCommander(); Pin(shooter, shooterAt); Pin(tgt, tgtAt); global::Levy.Common.Testing.WorldStep.Update(); all.Clear(); Differences(snap, PlayerSnapshot(Main.player[0]), all);
									all.Remove("Player.velocity"); // the player's own update gives and takes a fraction of gravity from tick to tick (landing noise): only a real push counts
									if ((Main.player[0].velocity - v0).Length() > 1.5f) diffs.Add("tick:Player.velocity");
									foreach (string f in all) if (IsWatched(f) && !control.Contains(f) && !noise.Contains(f)) diffs.Add(f);
								}
							}
						}
						catch (Exception e) { threwHere = true; threwN++; stillWrong.Add($"{wname} threw {e.GetType().Name}"); }
						finally { for (int k = 0; k < Main.maxProjectiles; k++) Main.projectile[k].active = false; }
						sticky = sticky == null ? new HashSet<string>(diffs) : new HashSet<string>(sticky.Intersect(diffs));
						if (sticky.Count == 0 || !firstOfWeapon) break;
						}
						diffs = sticky; first = false;
						holdout = MachineShots.Holdouts > hBefore;
						bool beforeOk = truth.Contains(old), afterOk = made.Overlaps(truth);
						string bv = truth.Count == 0 ? "no-truth" : beforeOk ? "match" : "differs";
						string av = truth.Count == 0 ? "no-truth" : afterOk ? "match" : holdout ? "holdout" : shot.StandIn ? "standin" : "differs";
						if (!byMod.TryGetValue(mod, out int[] c)) byMod[mod] = c = new int[9];
						c[0]++;
						if (truth.Count == 0) c[5]++;
						else
						{
							if (beforeOk) c[1]++; if (afterOk) c[2]++;
							if (!beforeOk && afterOk) c[3]++;
							if (beforeOk && !afterOk) { c[4]++; regressed.Add($"{wname} + {(round != null ? Lang.GetItemNameValue(round.type) : "-")} truth [{string.Join(",", truth.Select(ProjName))}] machine [{string.Join(",", made.Select(ProjName))}]{(holdout ? " (holdout)" : shot.StandIn ? " (held stand-in)" : "")}"); }
							if (!afterOk && !beforeOk && stillWrong.Count < 400) stillWrong.Add($"{wname} + {(round != null ? Lang.GetItemNameValue(round.type) : "-")} truth [{string.Join(",", truth.Select(ProjName))}] old {ProjName(old)} machine [{string.Join(",", made.Select(ProjName))}]{(holdout ? " (holdout)" : shot.StandIn ? " (held stand-in)" : "")}");
						}
						if (holdout || (shot.StandIn && truth.Count > 0 && !afterOk)) c[6]++;
						if (threwHere) c[7]++;
						if (diffs.Count > 0) { c[8]++; unsafeN++; unsafeList.Add($"{wname}: [{string.Join(", ", diffs)}]"); }
						rows.Add($"{wname}\t{mod}\t{(ammoWeapon ? "ammo" : "caster")}\t{(round != null ? Lang.GetItemNameValue(round.type) : "-")}\t{string.Join("|", truth.Select(ProjName))}\t{ProjName(old)}\t{string.Join("|", made.Select(ProjName))}\t{bv}\t{av}\t{(holdout ? "holdout" : "")}\t{(diffs.Count > 0 ? string.Join("|", diffs) : "")}{(err != null ? " truth-threw:" + err : "")}");
					}
				}
			}
			finally { _localIsSlot0 = false; Main.player[0] = saved; Main.myPlayer = savedMy; for (int k = 0; k < Main.maxProjectiles; k++) Main.projectile[k].active = false; shooter.active = false; tgt.active = false; }
			string outp = Environment.GetEnvironmentVariable("LEVY_MECHSHOTS_OUT");
			if (!string.IsNullOrEmpty(outp)) { try { System.IO.File.WriteAllLines(outp, rows); } catch { } }
			int[] tot = new int[9]; foreach (var kv in byMod) for (int i = 0; i < 9; i++) tot[i] += kv.Value[i];
			foreach (var kv in byMod) Note($"machine shot audit [{kv.Key}]: {kv.Value[0]} weapon+round pairs; truth empty {kv.Value[5]}; matched real shot BEFORE {kv.Value[1]}, AFTER {kv.Value[2]}; fixed {kv.Value[3]}, regressed {kv.Value[4]}; holdout / held stand-in {kv.Value[6]}; threw {kv.Value[7]}; player touched {kv.Value[8]}");
			Note($"machine shot audit [all]: {pairs} pairs; truth empty {tot[5]}; matched BEFORE {tot[1]}, AFTER {tot[2]}; fixed {tot[3]}, regressed {tot[4]}; holdout / held stand-in {tot[6]}; threw {tot[7]}; player touched {tot[8]}; hook counters: hooked {MachineShots.Hooked}, holdouts {MachineShots.Holdouts - h0}, empty {MachineShots.Empty - e0}, hook threw {MachineShots.Threw - t0}, light {MachineShots.Light}");
			foreach (string r in regressed.Take(60)) Note("machine shot audit, REGRESSED: " + r);
			foreach (string r in stillWrong.Take(120)) Note("machine shot audit, still differs: " + r);
			foreach (string r in unsafeList.Take(40)) Note("machine shot audit, touches the real player: " + r);
			Check($"machine shot audit: no weapon's shot changes the real player (touched: {unsafeN} of {pairs} pairs)", unsafeN == 0);
			Check($"machine shot audit: no weapon's shot threw ({threwN})", threwN == 0);
		}
	}
}
