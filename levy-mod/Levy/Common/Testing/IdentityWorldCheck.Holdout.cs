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
		// ---------------------------------------------------------------------------------------------- HOLDOUT WORLD: charge and holdout guns fire from the levy, and levies use their modes
		// Logged as "[HoldoutWorld]" lines with its own summary. The stand-in commander stands 64 tiles LEFT of the levies (the designer saw a levy's Ontological Despoiler fire from her; 60+ tiles makes "from her" unmistakable).
		// Part 1 (Calamity): two levies carry the Ontological Despoiler against one pinned target 12 tiles right of them (a tough one: a million life), then against a crowd of four, then while the commander fires her own Despoiler.
		//   Every projectile of the weapon (holdout, the two kinds of shot, the sphere, the beam) is followed from the tick it is first seen: whose it is (the levy's tag) and where it is born, against its levy and against the commander.
		//   The holdouts the levies spawn are read for their mode (ai[2]: 0 unstable, 5 null, 10 / 15 a mode swap, 20 a swap tried without a charged shot first).
		// Part 2: a sample of other charge / holdout / channel guns and bows (vanilla on the vanilla half, Calamity on the Calamity half) are carried by one levy each: nothing they make is born at the commander, and they hit.
		// Env: LEVY_HOLDOUTWORLD=0 skips, LEVY_HOLDOUTWORLD_ALONE=1 runs only this, LEVY_HOLDOUT_TICKS=N (Despoiler fight length, default 1500), LEVY_HOLDOUT_SWEEP=1 (every Calamity charge / holdout / channel gun and bow, notes only),
		// LEVY_HOLDOUT_ONLY=Name;Name (sweep these), LEVY_HOLDOUT_DEBUG=1.

		private static int _hwPass, _hwFail;
		private static void HCheck(string name, bool ok) { if (ok) _hwPass++; else _hwFail++; _mod.Logger.Info($"[HoldoutWorld] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void HNote(string s) => _mod.Logger.Info("[HoldoutWorld] OBSERVED " + s);
		private const int HoldCmdTiles = 64;
		private const float HoldNearLevyTiles = 8f;
		private static readonly bool HoldDebug = Environment.GetEnvironmentVariable("LEVY_HOLDOUT_DEBUG") == "1";

		private sealed class HoldStat
		{
			public string Label, Mode; public long Damage; public int Ticks, Levies;
			/// <summary>Births by projectile name: total, born within 8 tiles of its levy, born at the commander (within 10 tiles of her and nearer her than any levy), untagged (the commander's own or nobody's).</summary>
			public Dictionary<string, (int n, int atLevy, int atCmd, int untagged)> Born = new();
			public float MaxBornLevy, MinBornCmd = 1e9f;
			public int Unstable, Null, Spheres, Beams, Swaps, DudSwaps, HoldoutsUnstable, HoldoutsNull, CmdOwn, Alt, Variant, Presses; public string Err = "";
			public HashSet<int> SwapLevies = new();
			public int AtCmdTotal => Born.Values.Sum(v => v.atCmd);
			public int Total => Born.Values.Sum(v => v.n);
			public override string ToString() => $"{Label} [{Mode}] levies={Levies} ticks={Ticks} dmg={Damage} born={Total} atCmd={AtCmdTotal} maxBornFromLevy={MaxBornLevy / 16f:0.0}t minBornFromCmd={(MinBornCmd > 1e8f ? -1 : MinBornCmd / 16f):0.0}t holdouts unstable/null={HoldoutsUnstable}/{HoldoutsNull} shots unstable/null={Unstable}/{Null} spheres={Spheres} beams={Beams} swaps={Swaps} (levies {SwapLevies.Count}) dudSwaps={DudSwaps} cmdOwn={CmdOwn} altFired={Alt} variantSpawns={Variant} variantPresses={Presses} [{string.Join(", ", Born.OrderByDescending(kv => kv.Value.n).Take(8).Select(kv => kv.Key + " " + kv.Value.n + "/" + kv.Value.atLevy + (kv.Value.atCmd > 0 ? " CMD" + kv.Value.atCmd : "") + (kv.Value.untagged > 0 ? " untagged" + kv.Value.untagged : "")))}]{(Err.Length > 0 ? " ERR " + Err : "")}";
		}

		private static Vector2 HoldCmdAt() => new Vector2((Cx - HoldCmdTiles) * 16f, (FloorY - 3) * 16f);

		/// <summary>One weapon in the hands of <paramref name="levies"/> levies for <paramref name="ticks"/> ticks; every projectile followed from birth.</summary>
		private static HoldStat HoldRun(int weaponType, int ammoType, string label, string mode, int levies, int ticks, Item cmdWeapon, int crowd, Func<Projectile, bool> follow, float strafe = 0f, bool parity = false, int levyTiles = 0, int manaStart = -1)
		{
			var st = new HoldStat { Label = label, Mode = mode, Levies = levies };
			// parity: the commander stands where the first levy would, 12 tiles from the target, and uses the weapon as a player does (HoldCommanderUse)
			_cmdAt = parity ? new Vector2(Cx * 16f + 8f + (levyTiles > 0 ? (OriginTargetTiles - levyTiles) * 16f : 0f), FloorY * 16f - 24f) : HoldCmdAt(); ClearSoldiers(); _cmdHolds = false; ReleaseCommanderButtons(); ClearRogueAdds();
			Player cmd = Main.player[0]; cmd.inventory[0] = new Item(); cmd.heldProj = -1; cmd.itemAnimation = 0; cmd.itemTime = 0;
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			SpawnOriginTarget(at);
			_sweepVel = Vector2.Zero;
			for (int c = 0; c < crowd; c++)
			{
				Vector2 pos = new Vector2(_sweepTargetAt.X + (c % 2 == 0 ? 1 : -1) * (40f + 24f * c), FloorY * 16f - 48f * (c / 2));
				int ai = NPC.NewNPC(new EntitySource_WorldEvent("IdentityWorldCheck"), (int)pos.X, (int)pos.Y, NPCID.GreenSlime);
				if (ai < 0 || ai >= Main.maxNPCs) continue;
				NPC a = Main.npc[ai];
				a.lifeMax = 2000; a.life = 2000; a.defense = 0; a.damage = 0; a.knockBackResist = 0f; a.noGravity = true; a.noTileCollide = true; a.aiStyle = -1; a.value = 0f; a.Bottom = pos;
				_rogueAdds.Add(a); _rogueAddsAt.Add(pos);
			}
			var bodies = new List<int>();
			for (int k = 0; k < levies; k++)
			{
				Item w = new Item(); w.SetDefaults(weaponType);
				if (_holdManaCap > 0 && w.mana > _holdManaCap) w.mana = _holdManaCap;
				SoldierRecord rec = MakeOriginRecord(w, ammoType); rec.Name = "Pvt. Holdout" + k;
				Vector2 pos = at + new Vector2(-34f * k + (levyTiles > 0 ? (OriginTargetTiles - levyTiles) * 16f : 0f), 0f);
				if (manaStart >= 0) rec.Mana = Math.Min(rec.MaxMana, manaStart);
				rec.Order = SoldierOrder.Hold; rec.HoldPos = pos; // they hold their ground 12 tiles from the target, the commander 64 tiles behind them (a Follow order walks them back to her)
				RosterSystem.Instance.Regulars.Add(rec);
				int b = RosterSystem.Instance.SpawnBody(rec, pos);
				if (b < 0 || !rec.HasBody) throw new Exception("levy " + k + " did not spawn");
				Main.npc[b].Bottom = pos; bodies.Add(b);
			}
			if (cmdWeapon != null)
			{
				int am = cmdWeapon.useAmmo > 0 && AmmoByKind().TryGetValue(cmdWeapon.useAmmo, out int a2) ? a2 : 0;
				if (am > 0) { Item ai = new Item(); ai.SetDefaults(am); ai.stack = 999; cmd.inventory[54] = ai; }
			}
			int alt0 = AltPolicies.AltFired.TryGetValue(weaponType, out int a0) ? a0 : 0, var0 = AltPolicies.HoldVariantSpawns.TryGetValue(weaponType, out int v0) ? v0 : 0, pr0 = AltPolicies.HoldVariantPresses.TryGetValue(weaponType, out int q0) ? q0 : 0;
			Main.rand = new Terraria.Utilities.UnifiedRandom(4242);
			KeepCommander(); PinSweepTarget(); PinRogueAdds(); CursorOnCommander(); Step(10);
			var seen = new HashSet<(int, long)>(); int dbg = 0; var lastSerial = new long[Main.maxProjectiles];
			var deaths = new Dictionary<int, (string name, int born, Vector2 at, int id)>(); int deathNotes = 0;
			HoldBirthProbe.At.Clear(); HoldBirthProbe.On = true;
			try {
			for (int k = 0; k < ticks; k++)
			{
				if (manaStart < 0) foreach (var r in RosterSystem.Instance.Regulars) r.Mana = r.MaxMana;
				if (strafe > 0f && _sweepTarget != null)
				{
					// a fast target: runs back and forth 14 tiles either side of its spot at the given speed (the harness target's path sets its real velocity; the pin follows the path)
					var ht = _sweepTarget.GetGlobalNPC<HarnessTarget>();
					if (ht.Path == null)
					{
						float amp = 14 * 16f, per = 4f * amp / strafe; Vector2 c0 = _sweepTargetAt - new Vector2(0f, _sweepTarget.height / 2f);
						ht.Path = t => { float ph = (t % per) / per; float off = ph < 0.25f ? ph * 4f * amp : ph < 0.75f ? amp - (ph - 0.25f) * 4f * amp : -amp + (ph - 0.75f) * 4f * amp; return c0 + new Vector2(off, 0f); };
						ht.PathT = 0;
					}
					_sweepTargetAt = ht.Path(ht.PathT) + new Vector2(0f, _sweepTarget.height / 2f);
				}
				if (cmdWeapon != null) { try { if (parity) HoldCommanderUse(cmdWeapon, HoldParityPress(cmdWeapon, k)); else MegaCommanderUse(cmdWeapon, !cmdWeapon.channel || cmdWeapon.type != weaponType || k % 30 < 20); } catch (Exception e) { st.Err = "cmd use " + (e is TargetInvocationException && e.InnerException != null ? e.InnerException.GetType().Name : e.GetType().Name); } } else CursorOnCommander();
				Step(1); ClearNaturalHostiles();
				if (_sweepTarget != null && _sweepTarget.active) { int d = _sweepTarget.lifeMax - _sweepTarget.life; if (d > 0) st.Damage += d; }
				foreach (NPC a in _rogueAdds) if (a != null && a.active) { int d = a.lifeMax - a.life; if (d > 0) st.Damage += d; }
				st.Ticks++;
				if (HoldDebug)
					foreach (int di in deaths.Keys.ToList())
					{
						Projectile dp = Main.projectile[di]; var dv = deaths[di];
						if (dp.active && dp.identity == dv.id) continue;
						deaths.Remove(di);
						if (deathNotes++ < 60) HNote($"  dbg {label} [{mode}] t={k} gone {dv.name} lived {k - dv.born} ticks, last at {(dp.position.X - _sweepTarget.Center.X) / 16f:0.0},{(dp.position.Y - _sweepTarget.Center.Y) / 16f:0.0} tiles from the target");
					}
				Vector2 cmdC = Main.player[0].Center;
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active || p.hostile) continue;
					// a birth: NewProjectile filled this slot since the last look (the probe numbers every spawn), else a slot never seen before
					bool probed = HoldBirthProbe.At.TryGetValue(i, out var ba) && ba.type == p.type;
					if (probed ? ba.serial == lastSerial[i] : !seen.Add((i, ((long)p.identity << 20) | (uint)p.type))) continue;
					if (probed) lastSerial[i] = ba.serial;
					if (follow != null && !follow(p)) continue;
					Vector2 born = probed ? ba.at : p.Center; // where NewProjectile put it (a beam with 80 extra updates is 20 tiles on by the end of its first tick)
					bool tagged = p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0 && bodies.Contains(tag.SoldierIndex);
					float dl = 1e9f;
					if (tagged) dl = Vector2.Distance(born, Main.npc[tag.SoldierIndex].Center);
					else foreach (int b in bodies) dl = Math.Min(dl, Vector2.Distance(born, Main.npc[b].Center));
					float dc = Vector2.Distance(born, cmdC);
					string pn = p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type);
					bool atCmd = dc < 10 * 16f && dc < dl, atLevy = dl <= HoldNearLevyTiles * 16f;
					if (!tagged && cmdWeapon != null && dc < dl) { st.CmdOwn++; continue; } // the commander's own weapon: not the levies'
					if (HoldDebug && deaths.Count < 400) deaths[i] = (pn, k, born, p.identity);
					st.Born.TryGetValue(pn, out var bt); st.Born[pn] = (bt.n + 1, bt.atLevy + (atLevy ? 1 : 0), bt.atCmd + (atCmd ? 1 : 0), bt.untagged + (tagged ? 0 : 1));
					if (!pn.EndsWith("Burst")) st.MaxBornLevy = Math.Max(st.MaxBornLevy, dl);
					st.MinBornCmd = Math.Min(st.MinBornCmd, dc);
					if (pn == "OntologicalDespoilerHoldout")
					{
						int m = (int)Math.Round(p.ai[2]);
						if (m == 0) st.HoldoutsUnstable++; else if (m == 5) st.HoldoutsNull++; else if (m == 10 || m == 15) { st.Swaps++; if (tagged) st.SwapLevies.Add(tag.SoldierIndex); } else if (m == 20) st.DudSwaps++;
					}
					else if (pn == "OntologicalDespoilerShot") { if ((int)Math.Round(p.ai[2]) == 5) st.Null++; else st.Unstable++; }
					else if (pn == "OntologicalDespoilerGrenade") st.Spheres++;
					else if (pn == "OntologicalDespoilerBeam") st.Beams++;
					if (HoldDebug && pn.EndsWith("Shot") && st.Unstable + st.Null <= 3) HNote($"  dbg shot {label} [{mode}] t={k} {pn} dmg={p.damage} crit={p.CritChance} pen={p.penetrate} local={p.usesLocalNPCImmunity}/{p.localNPCHitCooldown} static={p.usesIDStaticNPCImmunity} owner={p.owner} cls={p.DamageType?.Name}");
					if (HoldDebug && !pn.EndsWith("Shot") && dbg++ < 80) HNote($"  dbg {label} [{mode}] t={k} new {pn} owner={p.owner} tagged={tagged} fromLevy={dl / 16f:0.0}t fromCmd={dc / 16f:0.0}t ai=({p.ai[0]:0},{p.ai[1]:0},{p.ai[2]:0}) vel=({p.velocity.X:0.0},{p.velocity.Y:0.0})");
				}
			}
			} finally { HoldBirthProbe.On = false; HoldBirthProbe.At.Clear(); _sweepVel = Vector2.Zero; _parityAim = null; if (_sweepTarget != null && _sweepTarget.active) _sweepTarget.GetGlobalNPC<HarnessTarget>().Path = null; }
			if (cmdWeapon != null) { try { MegaCommanderUse(cmdWeapon, false); } catch (Exception) { } Main.mouseLeft = false; Main.mouseLeftRelease = true; _parityAim = null; } // the raw button is global: the next run must not start with it down
			st.Alt = (AltPolicies.AltFired.TryGetValue(weaponType, out int a1) ? a1 : 0) - alt0;
			st.Variant = (AltPolicies.HoldVariantSpawns.TryGetValue(weaponType, out int v1) ? v1 : 0) - var0;
			st.Presses = (AltPolicies.HoldVariantPresses.TryGetValue(weaponType, out int q1) ? q1 : 0) - pr0;
			ClearSoldiers(); ClearRogueAdds(); ReleaseCommanderButtons(); cmd.inventory[0] = new Item(); cmd.inventory[54] = new Item(); cmd.heldProj = -1; cmd.itemAnimation = 0; cmd.itemTime = 0;
			return st;
		}

		/// <summary>The Calamity weapons a sweep covers: every ranged gun / bow (and magic gun) whose shot is a held projectile or that is channelled.</summary>
		private static List<(int type, string label)> HoldSweepWeapons(Mod cal)
		{
			var list = new List<(int, string)>();
			string only = Environment.GetEnvironmentVariable("LEVY_HOLDOUT_ONLY");
			string[] names = string.IsNullOrEmpty(only) ? null : only.Split(';', StringSplitOptions.RemoveEmptyEntries);
			var probe = new SoldierGear();
			for (int i = ItemID.Count; i < ItemLoader.ItemCount; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item s) || s == null || s.IsAir || s.ModItem?.Mod != cal || s.damage <= 0 || s.shoot <= 0) continue;
				if (names != null) { if (!names.Contains(s.ModItem.Name)) continue; }
				else
				{
					if (!s.CountsAsClass(DamageClass.Ranged) && !s.CountsAsClass(DamageClass.Magic)) continue;
					Projectile sample = WeaponClassifier.ProjectileSample(s.shoot);
					bool holdout = false;
					for (Type b = sample?.ModProjectile?.GetType(); b != null && b != typeof(ModProjectile); b = b.BaseType) if (b.Name == "BaseGunHoldoutProjectile") holdout = true;
					if (!holdout && !s.channel) continue;
				}
				Item it = new Item(); it.SetDefaults(i);
				if (!probe.CanHold(SoldierGear.SlotWeapon, it)) continue;
				list.Add((i, "CalamityMod:" + s.ModItem.Name));
			}
			return list;
		}

		/// <summary>
		/// The stand-in commander uses a weapon as a player does, for a parity measurement: <see cref="MegaCommanderUse"/> (her real ItemCheck, the cursor on the target) plus what a player's own client does that the stand-in skips:
		/// Calamity's per-player cursor on the target (its holdouts aim from it), the raw left button, and the holdout a held-item weapon gets for whoever holds it (Calamity spawns it in the LOCAL player's misc effects and idle-holdout
		/// check, which a server never runs for her): spawned once, at her, as Calamity spawns it.
		/// </summary>
		/// <summary>The parity commander's cursor, put back before every projectile pass while she uses her weapon (null: not in a parity run).</summary>
		internal static Vector2? _parityAim;

		private static void HoldCommanderUse(Item weapon, bool press)
		{
			MegaCommanderUse(weapon, press);
			Player p = Main.player[0];
			Vector2 aim = _sweepTarget != null ? _sweepTarget.Center : p.Center + new Vector2(300f, 0f);
			_parityAim = aim; // her cursor stays on the target through the projectile pass too (her holdouts aim from Main.MouseWorld there)
			try { object cp = AltPolicies.CalamityPlayerOf(p); if (cp != null) AltPolicies.SetCalAimPublic(cp, aim - p.MountedCenter); } catch (Exception) { }
			Main.mouseLeft = press; Main.mouseLeftRelease = !press;
			int ht = ModShotKinds.HolderType(weapon);
			if (ht > 0 && ModShotKinds.HoldSpawned(weapon) && !Main.projectile.Any(q => q.active && q.owner == 0 && q.type == ht))
			{
				int saved = Main.myPlayer;
				try { Main.myPlayer = 0; Projectile.NewProjectile(p.GetSource_ItemUse(weapon), p.Center, p.Center.DirectionTo(aim), ht, p.GetWeaponDamage(weapon), weapon.knockBack, 0); }
				finally { Main.myPlayer = saved; }
			}
		}

		/// <summary>How a player works the button of a charge-and-release weapon in the parity run: held for the weapon's charge plan (the same one the levy uses), let go, then held again; any other weapon is held down.</summary>
		private static bool HoldParityPress(Item weapon, int tick)
		{
			ChargePlan cp = ModShotKinds.ChargeRelease(weapon);
			if (cp == null) return true;
			int cycle = cp.HoldTicks + Math.Max(8, cp.LingerTicks);
			return tick % cycle < cp.HoldTicks;
		}

		/// <summary>
		/// The held guns fixed in the brokenguns pass (2026-11-22): each one is carried by one levy for a window long enough for its charge cycle, with the commander 64 tiles away; every projectile it makes must be the levy's (tagged, none
		/// born at the commander) and it must hit. Before the pass they all did 0 damage in levy hands. With LEVY_HOLDOUT_PARITY=1 the commander also uses each one herself from the levy's spot (the DPS a player gets), logged beside the levy's.
		/// </summary>
		private static readonly string[] HoldFixedCal = { "BarracudaGun", "Condemnation", "HeavenlyGale", "HalleysInferno", "SurgeDriver", "Starfleet", "Starmada", "BurningSea", "ClockworkBow", "DarkSpark", "TauCannon", "TheHive",
			"GruesomeEminence", "UnstableCastersGauntlet", "HolofibreImmolator", "Hellborn", "Eternity", "ThreadOfEradication" };

		/// <summary>Ship sample of part 2: charge / holdout guns covered by the held chain and by the plain path, Calamity and vanilla.</summary>
		private static readonly string[] HoldSampleCal = { "FetidEmesis", "PurgeGuzzler", "ArcNovaDiffuser", "Vulcan", "TitaniumRailgun", "Drataliornus" };
		private static readonly short[] HoldSampleVanilla = { ItemID.ChargedBlasterCannon, ItemID.LastPrism, ItemID.Phantasm, ItemID.LaserMachinegun };

		private static void HoldoutWorldCheck()
		{
			if (Environment.GetEnvironmentVariable("LEVY_HOLDOUTWORLD") == "0") return;
			_hwPass = _hwFail = 0;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_HOLDOUT_TICKS"), out int tk) ? tk : 1500;
			var sw = System.Diagnostics.Stopwatch.StartNew();
			try
			{
				Arena(); _cmdAt = HoldCmdAt(); KeepCommander(); Step(2);
				var ammo = AmmoByKind();
				bool cal = ModLoader.TryGetMod("CalamityMod", out Mod calMod);
				// ---- part 1: the Ontological Despoiler
				if (Environment.GetEnvironmentVariable("LEVY_HOLDOUT_PART1") == "0") HNote("despoiler: part 1 skipped (LEVY_HOLDOUT_PART1=0)");
				else if (cal && calMod.TryFind("OntologicalDespoiler", out ModItem desp))
				{
					var types = new HashSet<int>();
					foreach (string n in new[] { "OntologicalDespoilerHoldout", "OntologicalDespoilerShot", "OntologicalDespoilerGrenade", "OntologicalDespoilerBeam", "OntoligicalDespoilerBurst" })
						if (calMod.TryFind(n, out ModProjectile mp)) types.Add(mp.Type);
					Func<Projectile, bool> follow = p => types.Contains(p.type);
					Item mine = new Item(); mine.SetDefaults(desp.Type);
					Item lmg = new Item(); lmg.SetDefaults(ItemID.LaserMachinegun);
					int c0 = HeldWeapons.ChargeReleases, t0 = HeldWeapons.TapReleases, m0 = HeldWeapons.SiblingMasks;
					const string L = "CalamityMod:OntologicalDespoiler";
					HoldStat pinned = HoldRun(desp.Type, 0, L, "pinned tough target", 2, ticks, null, 0, follow);
					HNote("despoiler: " + pinned);
					HoldStat fast = HoldRun(desp.Type, 0, L, "fast tough target", 2, ticks, null, 0, follow, 8f);
					HNote("despoiler: " + fast);
					HoldStat crowd = HoldRun(desp.Type, 0, L, "crowd of 4", 2, Math.Min(ticks, 600), null, 3, follow);
					HNote("despoiler: " + crowd);
					HoldStat alone = HoldRun(desp.Type, 0, L, "commander fires her Despoiler, no levies", 0, Math.Min(ticks, 600), mine, 0, follow);
					HNote("despoiler: " + alone);
					HoldStat own = HoldRun(desp.Type, 0, L, "commander fires her own Despoiler", 2, Math.Min(ticks, 600), mine, 0, follow);
					HNote("despoiler: " + own);
					HoldStat other = HoldRun(desp.Type, 0, L, "commander channels a Laser Machinegun", 2, Math.Min(ticks, 600), lmg, 0, follow);
					HNote("despoiler: " + other);
					HNote($"despoiler: releases uncharged {HeldWeapons.TapReleases - t0}, charged {HeldWeapons.ChargeReleases - c0}; other levies' holdouts hidden from a Shoot hook {HeldWeapons.SiblingMasks - m0} times");
					var all = new[] { pinned, fast, crowd, own, other }; // the commander-alone run is the control of the last check only
					foreach (HoldStat s in all)
					{
						HCheck($"despoiler [{s.Mode}]: every shot is born at its levy, the commander {HoldCmdTiles} tiles away ({s.Total} born, farthest {s.MaxBornLevy / 16f:0.0} tiles from its levy, {s.AtCmdTotal} at the commander, nearest to her {(s.MinBornCmd > 1e8f ? -1 : s.MinBornCmd / 16f):0.0} tiles)",
							s.Total > 0 && s.AtCmdTotal == 0 && s.MaxBornLevy <= HoldNearLevyTiles * 16f && s.Born.Values.All(v => v.untagged == 0));
						HCheck($"despoiler [{s.Mode}]: it hits (damage {s.Damage})", s.Damage > 0);
					}
					HCheck($"despoiler: on a fast target both modes fire from the levy (unstable shots {fast.Unstable}, null shots {fast.Null}, beams {fast.Beams})", fast.Unstable > 0 && fast.Null + fast.Beams > 0);
					HCheck($"despoiler: the levies swap modes on a fast target ({fast.Swaps} swaps by {fast.SwapLevies.Count} of 2 levies)", fast.Swaps > 0 && fast.SwapLevies.Count == 2);
					HCheck($"despoiler: a swap is only tried after a charged shot (swaps with no charge first, which only waste time: {all.Sum(x => x.DudSwaps)})", all.Sum(x => x.DudSwaps) == 0);
					HCheck($"despoiler: on a pinned target the levies keep the uncharged unstable volley, its best damage ({pinned.Unstable} unstable shots, {pinned.Null} null, {pinned.Beams} beams, {pinned.Swaps} swaps)", pinned.Unstable > 0 && pinned.Swaps == 0);
					HCheck($"despoiler: the commander's own Despoiler fires as well while two levies fire theirs as with no levies ({own.CmdOwn} of her projectiles with them, {alone.CmdOwn} alone, in {own.Ticks} ticks)", alone.CmdOwn > 0 && own.CmdOwn * 4 >= alone.CmdOwn * 3);
				}
				else HNote("despoiler: Calamity not loaded, part 1 skipped");

				// ---- part 2: a sample of charge / holdout / channel guns
				var sample = new List<(int type, string label)>();
				foreach (short t in HoldSampleVanilla) sample.Add((t, "Terraria:" + ItemID.Search.GetName(t)));
				if (cal) foreach (string n in HoldSampleCal) if (calMod.TryFind(n, out ModItem mi)) sample.Add((mi.Type, "CalamityMod:" + n));
				bool full = cal && (Environment.GetEnvironmentVariable("LEVY_HOLDOUT_SWEEP") == "1" || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HOLDOUT_ONLY")));
				var sweep = full ? HoldSweepWeapons(calMod) : new List<(int, string)>();
				foreach (var (type, label) in sample.Concat(sweep.Where(w => !sample.Any(s => s.type == w.Item1))))
				{
					try
					{
						Item smp = ContentSamples.ItemsByType[type];
						int am = smp.useAmmo > 0 && ammo.TryGetValue(smp.useAmmo, out int a) ? a : 0;
						bool hasAlt = false; try { hasAlt = ItemLoader.AltFunctionUse(smp, Main.player[0]); } catch (Exception) { }
						HoldStat s = HoldRun(type, am, label, "1 levy", 1, 360, null, 0, null);
						Item fi = new Item(); fi.SetDefaults(type); WeaponProfile fp = WeaponClassifier.GetProfile(fi);
						string route = fp.Family + (ModShotKinds.Of(fi) != ModShotKind.None ? "/" + ModShotKinds.Of(fi) : "") + (ModShotKinds.ChargeRelease(fi) != null ? "/charge" : "") + (AltPolicies.HasRow(type) ? "/alt=" + AltPolicies.For(type).Kind : "");
						HNote($"sweep: {s} route={route} rightClick={(hasAlt ? "yes" : "no")}");
						bool inSample = sample.Any(x => x.type == type);
						if (inSample)
						{
							HCheck($"sweep: {label} nothing it makes is born at the commander ({s.AtCmdTotal} of {s.Total}, farthest from the levy {s.MaxBornLevy / 16f:0.0} tiles)", s.Total > 0 && s.AtCmdTotal == 0);
							HCheck($"sweep: {label} hits (damage {s.Damage})", s.Damage > 0);
						}
					}
					catch (Exception e) { HCheck($"sweep: {label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}", false); }
				}
			}
			catch (Exception e) { HCheck("holdout world threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _sweepTarget = null; ClearRogueAdds(); ClearSoldiers(); ReleaseCommanderButtons(); }
			try { HoldFixedCheck(); }
			catch (Exception e) { HCheck("fixed held guns threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _sweepTarget = null; ClearRogueAdds(); ClearSoldiers(); ReleaseCommanderButtons(); }
			HNote($"holdout world ran in {sw.ElapsedMilliseconds / 1000} s");
			_mod.Logger.Info($"[HoldoutWorld] self-check pass={_hwPass} fail={_hwFail}");
		}
	}

	public partial class IdentityWorldCheck
	{
		/// <summary>Part 3 of the holdout world check (Calamity only): the held guns of the brokenguns pass (see <see cref="HoldFixedCal"/>). Env LEVY_HOLDOUT_FIXED=0 skips, LEVY_HOLDOUT_FIXEDTICKS=N (window, default 600),
		/// LEVY_HOLDOUT_PARITY=1 (the commander's own DPS with each), LEVY_HOLDOUT_FIXEDONLY=Name;Name.</summary>
		/// <summary>Lab: the weapons of a run cost at most this much mana a cast (0 = their own cost). A levy is only issued a weapon his pool can pay (WeaponUse.CanEverPay); the test levy's pool is 200, so a dearer cast (Eternity's 300) is
		/// tried at 200, standing in for a levy whose gear raises his pool.</summary>
		private static int _holdManaCap;

		private static void HoldFixedCheck()
		{
			if (Environment.GetEnvironmentVariable("LEVY_HOLDOUT_FIXED") == "0" || !ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_HOLDOUT_FIXEDTICKS"), out int tk) ? tk : 600;
			bool parity = Environment.GetEnvironmentVariable("LEVY_HOLDOUT_PARITY") == "1";
			string only = Environment.GetEnvironmentVariable("LEVY_HOLDOUT_FIXEDONLY");
			string[] names = string.IsNullOrEmpty(only) ? HoldFixedCal : only.Split(';', StringSplitOptions.RemoveEmptyEntries);
			var ammo = AmmoByKind();
			Arena(); _cmdAt = HoldCmdAt(); KeepCommander(); Step(2);
			int s0 = HeldWeapons.StallReleases;
			foreach (string n in names)
			{
				if (!cal.TryFind(n, out ModItem mi)) { HNote("fixed: " + n + " not in this Calamity"); continue; }
				string label = "CalamityMod:" + n;
				try
				{
					Item smp = ContentSamples.ItemsByType[mi.Type];
					int am = smp.useAmmo > 0 && ammo.TryGetValue(smp.useAmmo, out int a) ? a : 0;
					_holdManaCap = smp.mana > 200 ? 200 : 0;
					if (_holdManaCap > 0) HNote($"fixed: {label} costs {smp.mana} mana a cast, above the test levy's pool of 200: tried at 200 (a levy is issued it only when his gear raises his pool that far)");
					Item fi = new Item(); fi.SetDefaults(mi.Type); WeaponProfile fp = WeaponClassifier.GetProfile(fi);
					int tiles = Math.Min(OriginTargetTiles, Math.Max(2, fp.RangeTiles - 2)); // where the levy stands: his fighting range, as a player stands within her weapon's reach
					HoldStat lv = HoldRun(mi.Type, am, label, "1 levy at " + tiles + " tiles, " + ticks + " ticks", 1, ticks, null, 0, null, 0f, false, tiles);
					string route = fp.Family + (ModShotKinds.Of(fi) != ModShotKind.None ? "/" + ModShotKinds.Of(fi) : "") + (ModShotKinds.ChargeRelease(fi) != null ? "/charge" : "") + (ModShotKinds.HolderType(fi) != fi.shoot ? "/holder=" + (ProjectileLoader.GetProjectile(ModShotKinds.HolderType(fi))?.Name ?? "?") : "") + (ModShotKinds.HoldSpawned(fi) ? "/held-spawn" : "");
					float lvDps = lv.Damage * 60f / Math.Max(1, lv.Ticks);
					string par = "";
					if (parity)
					{
						Item cw = new Item(); cw.SetDefaults(mi.Type); if (_holdManaCap > 0) cw.mana = _holdManaCap;
						HoldStat pc = HoldRun(mi.Type, am, label, "commander alone, " + ticks + " ticks", 0, ticks, cw, 0, null, 0f, true, tiles);
						float pDps = pc.Damage * 60f / Math.Max(1, pc.Ticks);
						par = $" | commander alone at the levy's spot: dmg={pc.Damage} dps={pDps:0} (her projectiles {pc.CmdOwn}{(pc.Err.Length > 0 ? " ERR " + pc.Err : "")}) levy/commander={(pDps > 0 ? (lvDps / pDps).ToString("0.00") : "n/a")}";
					}
					HNote($"fixed: {lv} dps={lvDps:0} route={route}{par}");
					HCheck($"fixed: {label} everything it makes is the levy's, none born at the commander {HoldCmdTiles} tiles away ({lv.AtCmdTotal} at her, {lv.Born.Values.Sum(v => v.untagged)} untagged, of {lv.Total})", lv.Total > 0 && lv.AtCmdTotal == 0 && lv.Born.Values.All(v => v.untagged == 0));
					HCheck($"fixed: {label} hits (damage {lv.Damage} in {lv.Ticks} ticks)", lv.Damage > 0);
				}
				catch (Exception e) { HCheck($"fixed: {label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}", false); }
				finally { _holdManaCap = 0; }
			}
			HNote($"fixed: stall releases (a holdout with no plan let go after {LevyConstants.HeldStallReleaseTicks} ticks of making nothing) {HeldWeapons.StallReleases - s0}");
			// the right-clicks of held modded weapons: made in the levy's chain, never a plain use owned by the commander. Chronomancer's Scythe (ALT_AOE: a crowd within 9 tiles) right-clicks its spinning scythe; the Apoctosis Array and
			// Ion Blaster (MANA_REFILL: the pool under a quarter) hold the right button on their holdout, which drinks mana back.
			if (string.IsNullOrEmpty(only) || only.Contains("alt"))
				foreach (var (n, mana) in new[] { ("ChronomancersScythe", -1), ("ApoctosisArray", 20), ("IonBlaster", 20) })
				{
					if (!cal.TryFind(n, out ModItem mi)) continue;
					string label = "CalamityMod:" + n;
					try
					{
						HoldStat a = HoldRun(mi.Type, 0, label, "alt: crowd of 4 at 6 tiles" + (mana >= 0 ? ", mana " + mana : ""), 1, ticks, null, 3, null, 0f, false, 6, mana);
						HNote($"fixed alt: {a}");
						bool used = mana >= 0 ? a.Variant + a.Presses > 0 : a.Alt > 0;
						HCheck($"fixed alt: {label} right-clicks in a crowd from the levy, none of it born at the commander ({(mana >= 0 ? "right button pressed " + a.Presses + " times, holdouts spawned with it " + a.Variant : "alt uses " + a.Alt)}, {a.AtCmdTotal} born at her, {a.Born.Values.Sum(v => v.untagged)} untagged, of {a.Total})",
							used && a.Total > 0 && a.AtCmdTotal == 0 && a.Born.Values.All(v => v.untagged == 0));
						HCheck($"fixed alt: {label} hits (damage {a.Damage})", a.Damage > 0);
					}
					catch (Exception e) { HCheck($"fixed alt: {label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}", false); }
				}
		}
	}

	/// <summary>Lab only (the holdout world check): where each projectile was when NewProjectile made it, by slot, while <see cref="On"/>.</summary>
	public class HoldBirthProbe : GlobalProjectile
	{
		internal static bool On;
		internal static readonly Dictionary<int, (int type, Vector2 at, long serial)> At = new();
		private static long _serial;
		public override void OnSpawn(Projectile projectile, IEntitySource source) { if (On) At[projectile.whoAmI] = (projectile.type, projectile.Center, ++_serial); }
	}
}
