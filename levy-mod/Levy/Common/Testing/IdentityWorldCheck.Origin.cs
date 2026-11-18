using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
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
		// ---------------------------------------------------------------------------------------------- ORIGIN sweep: does a levy's shot come out of the LEVY?
		// One levy stands in the arena holding one weapon, the pinned enemy is 12 tiles to its right, and the stand-in commander (slot 0, the real single-player owner: Main.myPlayer = 0 while NPCs and
		// projectiles update) stands 45 tiles to the LEFT with nothing between. Every projectile the weapon makes is followed: where it is first seen, how far it is from the levy and from the commander, and
		// how long it spends within 10 tiles of the commander (nothing a levy fires toward an enemy on the far side has any reason to go there). The enemy's life is read back after every tick for the damage done.
		// A weapon is "from the commander" when a projectile is first seen within 10 tiles of the commander or spends 3 or more ticks there.
		// Env: LEVY_ORIGIN_ONLY=Name;Name (named weapons, any mod), LEVY_ORIGIN_MODS=CalamityMod (comma list, default CalamityMod), LEVY_ORIGIN_SHARD=i/n, LEVY_ORIGIN_TICKS=N (default 150), LEVY_ORIGIN_ALONE=1 (this check alone).

		private static readonly bool OriginDrawPuppet = Environment.GetEnvironmentVariable("LEVY_ORIGIN_DRAW") != "0"; // 0 = the unfixed draw (no owner puppet around the draw probe)
		private static readonly bool OriginDebug = Environment.GetEnvironmentVariable("LEVY_ORIGIN_DEBUG") == "1";
		/// <summary>Weapons that still read more of the commander than her buttons (Anahita's Arpeggio reads her held item and release state, the Midnight Sun Beacon is a summon): listed by the sweep, not failed.</summary>
		private static readonly string[] KnownButtonResiduals = { "AnahitasArpeggio", "MidnightSunBeacon" };
		/// <summary>The sky-rain weapons the ship sample runs (the class check below lists the rest on a full sweep): Astrum Deus's Star Shower, and the sky-drop weapons built the same way.</summary>
		private static readonly string[] SkyRainSample = { "StarShower", "Greentide" };
		/// <summary>Weapons that read Main.screenPosition and still do nothing in the sweep, identically with the old and the new cursor (measured both ways): Ice Barrage needs 300 mana per cast, Hyphae Rod's spores do not hurt the pinned target, and the three melee pieces
		/// (Banshee Hook, Celestial Claymore, Monstrous Knives) only act in reach, which the 12-tile enemy is not. Listed by the sweep, not failed.</summary>
		private static readonly string[] KnownSkyRainResiduals = { "IceBarrage", "HyphaeRod", "BansheeHook", "CelestialClaymore", "MonstrousKnives" };
		private static bool SkyRainOk(OResult r) => r.Projs > 0 && r.Damage > 0 && (r.Far == 0 || !r.Label.EndsWith(":StarShower") || r.FarAbove * 2 >= r.Far);
		private const int OriginCmdTiles = 45, OriginTargetTiles = 12, NearTiles = 10;

		private sealed class OProj
		{
			public int Type, Born, Identity, Owner; public string Name; public float SpawnDistLevy, SpawnDistCmd, MinDistCmd = 1e9f, MaxDistLevy; public int NearCmdTicks, Ticks;
			public bool Tagged;
		}

		private sealed class OResult
		{
			public string Label, Family = ""; public OResult Up, Holds; public int ItemType, Far, FarAbove, Projs, AtLevy, AtCmd, NearCmdTicks, DamageTicks, RanTicks, EngageTicks, DrawProbes, DrawAtCmd; public float MinDrawCmd = 1e9f; public long Damage; public float MinDistCmd = 1e9f; public bool Threw; public string End = "";
			public Dictionary<string, (int n, int atCmd)> ByType = new();
			public bool FromCommander => AtCmd > 0 || NearCmdTicks >= 3;
			/// <summary>Followed the commander in both places (left of the levy and above it).</summary>
			public bool FromCommanderBoth => FromCommander && Up != null && Up.FromCommander;
			/// <summary>A projectile of the weapon FIRST appeared at the commander, left of the levy and above it alike: it is made from her place, not the levy's.</summary>
			public bool SpawnedAtCommanderBoth => AtCmd > 0 && Up != null && Up.AtCmd > 0;
			public bool DrawFromCommander => DrawAtCmd > 0;
			/// <summary>The weapon's output collapses while the commander holds her own use button: it reads her buttons, not the levy's.</summary>
			public bool ButtonControlled => Holds != null && ((Damage >= 50 && Holds.Damage * 4 < Damage) || (Damage < 50 && Projs >= 8 && Holds.Projs * 4 < Projs));
			public override string ToString()
			{
				string types = string.Join(",", ByType.OrderByDescending(kv => kv.Value.n).Take(5).Select(kv => kv.Key + "x" + kv.Value.n + (kv.Value.atCmd > 0 ? "(cmd" + kv.Value.atCmd + ")" : "")));
				return $"{Label} far={FarAbove}/{Far} projs={Projs} atLevy={AtLevy} atCmd={AtCmd} nearCmdTicks={NearCmdTicks} minCmdTiles={(MinDistCmd > 1e8f ? -1 : MinDistCmd / 16f):0.0} dmgTicks={DamageTicks} dmg={Damage} ran={RanTicks} engage={EngageTicks} draw={(DrawProbes == 0 ? "-" : DrawAtCmd + "/" + DrawProbes)} fam={Family} {End} [{types}]";
			}
		}

		/// <summary>Where the stand-in commander stands: 45 tiles left of the levy, or (up) 72 tiles straight above it. A weapon is only listed when its shots follow the commander in BOTH places: a barrage that lands 50 tiles to the side, a shot that
		/// reflects backwards, a rain from the sky above the enemy each pass the commander by geometry in one of them.</summary>
		private static Vector2 OriginCmdAt(bool up = false) => up ? new Vector2(Cx * 16f + 8f, (FloorY - 72) * 16f) : new Vector2((Cx - OriginCmdTiles) * 16f, (FloorY - 3) * 16f);

		private static void SetupOriginArena()
		{
			Arena();
			_cmdAt = OriginCmdAt();
		}

		/// <summary>Moves the pinned enemy to the spot 12 tiles right of the levy (a fresh slime, pinned like the safety sweep's).</summary>
		private static void SpawnOriginTarget(Vector2 levyAt)
		{
			if (_sweepTarget != null) _sweepTarget.active = false;
			_sweepTargetAt = new Vector2(levyAt.X + OriginTargetTiles * 16f, FloorY * 16f);
			int ti = NPC.NewNPC(new EntitySource_WorldEvent("IdentityWorldCheck"), (int)_sweepTargetAt.X, (int)_sweepTargetAt.Y, NPCID.GreenSlime);
			if (ti < 0 || ti >= Main.maxNPCs) throw new Exception("enemy did not spawn");
			_sweepTarget = Main.npc[ti];
			_sweepTarget.lifeMax = 1_000_000; _sweepTarget.life = 1_000_000; _sweepTarget.defense = 0; _sweepTarget.damage = 0; _sweepTarget.knockBackResist = 0f; _sweepTarget.width = 64; _sweepTarget.height = 96;
			_sweepTarget.noGravity = true; _sweepTarget.noTileCollide = true; _sweepTarget.aiStyle = -1; _sweepTarget.friendly = false; _sweepTarget.value = 0f; _sweepTarget.Bottom = _sweepTargetAt;
			var ht = _sweepTarget.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = _sweepTarget.position;
		}

		/// <summary>The real commander's cursor stands ON the commander (screen position zero, mouse = the commander's world position, Calamity's per-player cursor offset zero): whatever of a levy's shot still reads the commander's cursor flies at the commander.</summary>
		private static void CursorOnCommander()
		{
			Player c = Main.player[0];
			Main.screenPosition = Vector2.Zero; Main.mouseX = (int)c.Center.X; Main.mouseY = (int)c.Center.Y;
			try { object cp = global::Levy.Common.Weapons.AltPolicies.CalamityPlayerOf(c); if (cp != null) global::Levy.Common.Weapons.AltPolicies.SetCalAimPublic(cp, Vector2.Zero, 0f); } catch (Exception) { }
		}

		/// <summary>The stand-in commander lets go of her use buttons (nothing in the stand-in world does it for her: a run that held them would leave them held for the next).</summary>
		private static void ReleaseCommanderButtons() { Player c = Main.player[0]; c.channel = false; c.controlUseItem = false; c.controlUseTile = false; c.releaseUseItem = true; }

		private static void OriginStep() { CursorOnCommander(); Step(1); }

		private static void ClearNaturalHostiles()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n != _sweepTarget && WorldStep.IsNaturalHostile(n)) n.active = false; }
		}

		private static SoldierRecord MakeOriginRecord(Item w, int ammoType)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Origin",
				CommanderName = "IdentityTester", CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true);
			if (ammoType > 0) { Item am = new Item(); am.SetDefaults(ammoType); am.stack = Math.Min(999, am.maxStack); rec.Gear.Ammo = am; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
			rec.LoadoutLocked = true; rec.Order = Enum.TryParse(Environment.GetEnvironmentVariable("LEVY_ORIGIN_ORDER"), out SoldierOrder so) ? so : SoldierOrder.Follow;
			return rec;
		}

		/// <summary>One levy fires one weapon at the pinned enemy; every projectile is followed.</summary>
		private static OResult OriginOne(int weaponType, int ammoType, string label, int ticks, bool up = false, bool holds = false)
		{
			var res = new OResult { Label = label, ItemType = weaponType };
			_cmdAt = OriginCmdAt(up);
			try { Item fi = new Item(); fi.SetDefaults(weaponType); WeaponProfile fp = WeaponClassifier.GetProfile(fi); res.Family = fp.Family + (fp.Supported ? "" : "(unsupported)") + (ModShotKinds.Of(fi) != ModShotKind.None ? "/" + ModShotKinds.Of(fi) : ""); } catch (Exception) { }
			ClearSoldiers();
			_cmdHolds = holds || Environment.GetEnvironmentVariable("LEVY_ORIGIN_HOLDS") == "1"; ReleaseCommanderButtons(); // nothing carries over from the weapon before
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			SpawnOriginTarget(at);
			Item w = new Item(); w.SetDefaults(weaponType);
			SoldierRecord rec = MakeOriginRecord(w, ammoType);
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) throw new Exception("levy did not spawn");
			Main.npc[body].Bottom = at;
			Main.rand = new Terraria.Utilities.UnifiedRandom(777);
			KeepCommander(); PinSweepTarget(); CursorOnCommander(); Step(20);
			var live = new Dictionary<int, OProj>(); int dbg = 0;
			for (int k = 0; k < ticks; k++)
			{
				rec.Mana = rec.MaxMana;
				OriginStep(); ClearNaturalHostiles();
				if (_sweepTarget != null && _sweepTarget.active) { int d = _sweepTarget.lifeMax - _sweepTarget.life; if (d > 0) { res.Damage += d; res.DamageTicks++; } }
				if (!Main.npc[body].active || Main.npc[body].ModNPC is not SoldierNPC soldier) { res.End = "levyGone"; break; }
				res.RanTicks++; if (soldier.State == SoldierState.Engage) res.EngageTicks++; res.End = $"state={soldier.State} x={(Main.npc[body].Center.X - at.X) / 16f:0.0}";
				Vector2 levy = Main.npc[body].Center, cmd = Main.player[0].Center;
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active || p.hostile) { live.Remove(i); continue; }
					if (live.TryGetValue(i, out OProj o) && (o.Type != p.type || o.Identity != p.identity)) { live.Remove(i); o = null; }
					else o = live.TryGetValue(i, out OProj o2) ? o2 : null;
					float dl = Vector2.Distance(p.Center, levy), dc = Vector2.Distance(p.Center, cmd);
					if (o == null)
					{
						string nm = p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type);
						o = new OProj { Type = p.type, Identity = p.identity, Born = k, Name = nm, Owner = p.owner, SpawnDistLevy = dl, SpawnDistCmd = dc };
						live[i] = o; res.Projs++;
						bool atCmd = dc < NearTiles * 16f && dc < dl; if (atCmd) res.AtCmd++; else if (dl < 12 * 16f) res.AtLevy++;
						// SKY RAIN class: a projectile that first appears 25 or more tiles above the enemy (a rain from the top of the screen). It must come in above the TARGET (within 30 tiles sideways, no more than 100 tiles over it), as it does over a player's cursor.
						if (!atCmd && _sweepTarget != null && (_sweepTarget.Center.Y - p.Center.Y) / 16f >= 25f)
						{
							res.Far++;
							float tdx = (p.Center.X - _sweepTarget.Center.X) / 16f, tdy = (_sweepTarget.Center.Y - p.Center.Y) / 16f;
							if (Math.Abs(tdx) <= 30f && tdy <= 100f) res.FarAbove++;
							if (OriginDebug) Note($"  dbg far {nm} dx={tdx:0.0} above={tdy:0.0} tiles");
						}
						res.ByType.TryGetValue(nm, out var ent); res.ByType[nm] = (ent.n + 1, ent.atCmd + (atCmd ? 1 : 0));
					}
					if (OriginDebug && dbg < 40 && o.Ticks < 4) { dbg++; Note($"  dbg t={k} proj#{i} {o.Name} owner={p.owner} age={o.Ticks} pos(levy)=({(p.Center.X - levy.X) / 16f:0.0},{(p.Center.Y - levy.Y) / 16f:0.0}) pos(cmd)=({(p.Center.X - cmd.X) / 16f:0.0},{(p.Center.Y - cmd.Y) / 16f:0.0}) vel=({p.velocity.X:0.0},{p.velocity.Y:0.0}) ai0={p.ai[0]:0.0} ai1={p.ai[1]:0.0} ai2={p.ai[2]:0.0} tl={p.timeLeft} tagged={(p.TryGetGlobalProjectile(out SoldierProjectileTag tg) && tg.SoldierIndex >= 0)}"); }
					if (!up && !holds && o.Ticks % 4 == 0 && p.ModProjectile != null && global::Levy.Common.Weapons.OwnerLockProbe.ReadsOwnerAtDraw(p.type))
					{
						// where this shot's own draw code puts its owner-anchored end (a flail's chain end is Main.GetPlayerArmPosition), measured in the state the real draw runs in
						bool moved = OriginDrawPuppet && global::Levy.Common.Weapons.OwnerPuppet.BeginDraw(p, force: true);
						Vector2 arm; try { arm = Main.GetPlayerArmPosition(p); } finally { if (moved) global::Levy.Common.Weapons.OwnerPuppet.EndDraw(); }
						float al = Vector2.Distance(arm, levy), ac = Vector2.Distance(arm, cmd);
						if (OriginDebug && res.DrawProbes < 6) Note($"  dbg draw probe {o.Name} moved={moved} arm=({(arm.X - levy.X) / 16f:0.0},{(arm.Y - levy.Y) / 16f:0.0}) from levy; cmd=({(cmd.X - levy.X) / 16f:0.0},{(cmd.Y - levy.Y) / 16f:0.0}) from levy; owner={p.owner}");
						res.DrawProbes++; res.MinDrawCmd = Math.Min(res.MinDrawCmd, ac);
						if (ac < NearTiles * 16f && ac < al) res.DrawAtCmd++;
					}
					o.Ticks++; o.MinDistCmd = Math.Min(o.MinDistCmd, dc); o.MaxDistLevy = Math.Max(o.MaxDistLevy, dl);
					if (dc < NearTiles * 16f && dc < dl) { o.NearCmdTicks++; res.NearCmdTicks++; }
					res.MinDistCmd = Math.Min(res.MinDistCmd, dc);
				}
			}
			ClearSoldiers(); _cmdHolds = false; ReleaseCommanderButtons();
			return res;
		}

		/// <summary>The weapons a sweep covers.</summary>
		private static List<(int type, string label, string cls)> OriginWeapons()
		{
			var weapons = new List<(int, string, string)>();
			var probeGear = new SoldierGear();
			string[] mods = (Environment.GetEnvironmentVariable("LEVY_ORIGIN_MODS") ?? "CalamityMod").Split(',', StringSplitOptions.RemoveEmptyEntries);
			string only = Environment.GetEnvironmentVariable("LEVY_ORIGIN_ONLY");
			string[] names = string.IsNullOrEmpty(only) ? null : only.Replace('_', ' ').Split(';', StringSplitOptions.RemoveEmptyEntries);
			for (int i = 1; i < ItemLoader.ItemCount; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item s) || s == null || s.IsAir || s.damage <= 0 || s.accessory || s.ammo > 0 || s.pick > 0 || s.axe > 0 || s.hammer > 0) continue;
				string mod = s.ModItem?.Mod.Name ?? "Terraria";
				if (names == null ? !mods.Contains(mod) : !names.Any(n => s.Name.Replace(" ", "") == n.Replace(" ", "") || (s.ModItem != null && s.ModItem.Name == n))) continue;
				if (names == null && s.shoot <= 0 && !s.sentry && !s.CountsAsClass(DamageClass.Summon)) continue; // no projectile, nothing to follow
				Item it = new Item(); it.SetDefaults(i);
				if (!probeGear.CanHold(SoldierGear.SlotWeapon, it)) continue;
				string cls = s.DamageType?.Name ?? "?"; cls = cls.Replace("DamageClass", "");
				weapons.Add((i, mod + ":" + (s.ModItem?.Name ?? s.Name), cls));
			}
			string shard = Environment.GetEnvironmentVariable("LEVY_ORIGIN_SHARD");
			if (!string.IsNullOrEmpty(shard) && shard.Split('/') is { Length: 2 } sh && int.TryParse(sh[0], out int si) && int.TryParse(sh[1], out int sn) && sn > 1) weapons = weapons.Where((w, k) => k % sn == si).ToList();
			return weapons;
		}

		private static Dictionary<int, int> AmmoByKind()
		{
			var ammoByKind = new Dictionary<int, int>();
			for (int i = 1; i < ItemLoader.ItemCount; i++)
				if (ContentSamples.ItemsByType.TryGetValue(i, out Item a) && a != null && !a.IsAir && a.ammo > 0 && !a.IsACoin && !ammoByKind.ContainsKey(a.ammo)) ammoByKind[a.ammo] = i;
			return ammoByKind;
		}

		/// <summary>The ORIGIN sweep: each weapon is carried by one levy; the list of weapons whose projectiles come out of (or run to) the commander is logged ("origin sweep, from the commander: ...").</summary>
		private static void OriginSweep()
		{
			if (Environment.GetEnvironmentVariable("LEVY_ORIGIN") == "0") return;
			bool full = Environment.GetEnvironmentVariable("LEVY_ORIGIN") == "1" || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_ORIGIN_ONLY"));
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_ORIGIN_TICKS"), out int tk) ? tk : 150;
			var weapons = OriginWeapons();
			var ammoByKind = AmmoByKind();
			if (!full)
			{
				// the ship sample: the two weapons Eve reported (the Calamity gun built from Magna Cannon + Opal Striker + Core of Calamity and the Great Sand Shark's Tumbleweed), when Calamity is loaded
				weapons = weapons.Where(w => w.label == "CalamityMod:ArcNovaDiffuser" || w.label == "CalamityMod:Tumbleweed" || SkyRainSample.Contains(w.label.Replace("CalamityMod:", ""))).ToList();
				if (weapons.Count == 0) { var cal = new[] { "ArcNovaDiffuser", "Tumbleweed" }.Concat(SkyRainSample).ToArray(); weapons = Array.Empty<(int, string, string)>().ToList(); foreach (string n in cal) if (ModLoader.TryGetMod("CalamityMod", out Mod cm) && cm.TryFind(n, out ModItem mi)) weapons.Add((mi.Type, "CalamityMod:" + n, "")); }
			}
			if (weapons.Count == 0) return;
			SetupOriginArena();
			_spNet = Environment.GetEnvironmentVariable("LEVY_ORIGIN_SPNET") == "1"; // the phases run as a single-player CLIENT (netMode SinglePlayer) when set
			var sw = Stopwatch.StartNew(); int done = 0, threw = 0; var from = new List<OResult>(); var all = new List<(OResult r, string cls)>();
			try
			{
				KeepCommander(); Step(2);
				foreach (var (type, label, cls) in weapons)
				{
					try
					{
						Item sample = ContentSamples.ItemsByType[type];
						int ammo = sample.useAmmo > 0 && ammoByKind.TryGetValue(sample.useAmmo, out int at) ? at : 0;
						OResult r = OriginOne(type, ammo, label, ticks); r.Up = OriginOne(type, ammo, label, ticks, true); r.Holds = OriginOne(type, ammo, label, ticks, false, true); done++; all.Add((r, cls));
						if (r.FromCommanderBoth) from.Add(r);
						if (full) Note($"origin sweep: {r} [{cls}] | up: {r.Up.Projs} projs, {r.Up.AtCmd} at commander, {r.Up.NearCmdTicks} near, dmg {r.Up.Damage} | commander holding her button: {r.Holds.Projs} projs, dmg {r.Holds.Damage}{(r.ButtonControlled ? " BUTTON-CONTROLLED" : "")}");
					}
					catch (Exception e) { threw++; Note($"origin sweep: {label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}"); }
				}
			}
			finally { _sweepTarget = null; _spNet = false; }
			sw.Stop();
			Note($"origin sweep: {done} weapons run in {sw.ElapsedMilliseconds / 1000} s ({ticks} ticks each, each run twice, commander {OriginCmdTiles} tiles left of the levy and 72 tiles above it, enemy {OriginTargetTiles} tiles right): {all.Count(a => a.r.Projs > 0)} made a projectile, {all.Count(a => a.r.Damage > 0)} damaged the enemy, {all.Count(a => a.r.FromCommander)} followed the commander on the left, {all.Count(a => a.r.Up.FromCommander)} above, {from.Count} both ways, {threw} threw");
			Note("origin sweep, spawned at the commander in both places: " + (all.Any(a => a.r.SpawnedAtCommanderBoth) ? string.Join("; ", all.Where(a => a.r.SpawnedAtCommanderBoth).Select(a => a.r.ToString())) : "none"));
			Note("origin sweep, flew through the commander both places (beam / reflection geometry, informational): " + (from.Count == 0 ? "none" : string.Join("; ", from.Select(r => r.ToString() + " | up: " + r.Up))));
			Note("origin sweep, one place only (geometry, not listed): left " + string.Join(", ", all.Where(a => a.r.FromCommander && !a.r.FromCommanderBoth).Select(a => a.r.Label.Replace("CalamityMod:", ""))) + " | above " + string.Join(", ", all.Where(a => !a.r.FromCommander && a.r.Up.FromCommander).Select(a => a.r.Label.Replace("CalamityMod:", ""))));
			Note("origin sweep, output collapses while the commander holds her own use button (" + all.Count(a => a.r.ButtonControlled) + "): " + string.Join("; ", all.Where(a => a.r.ButtonControlled).Select(a => a.r.Label.Replace("CalamityMod:", "") + " " + a.r.Damage + "/" + a.r.Projs + " -> " + a.r.Holds.Damage + "/" + a.r.Holds.Projs)) + $" (use-button puppet {((global::Levy.Common.Weapons.OwnerPuppet.Mask & 128) != 0 ? "on" : "OFF")})");
			Note("origin sweep, draw anchored on the commander: " + (all.Any(a => a.r.DrawFromCommander) ? string.Join("; ", all.Where(a => a.r.DrawFromCommander).Select(a => a.r.ToString())) : "none"));
			Note($"origin sweep, weapons whose own draw reads the owner: {all.Count(a => a.r.DrawProbes > 0)} ({string.Join(", ", all.Where(a => a.r.DrawProbes > 0).Select(a => a.r.Label.Replace("CalamityMod:", "") + ":" + a.r.DrawAtCmd + "/" + a.r.DrawProbes))}); draw puppet {(OriginDrawPuppet ? "on" : "OFF")}");
			var rain = all.Where(a => a.r.ItemType > 0 && global::Levy.Common.Weapons.OwnerLockProbe.ReadsScreenAtUse(a.r.ItemType)).ToList();
			Note($"origin sweep, SKY RAIN class (weapons whose use code reads Main.screenPosition; whatever they spawn 25+ tiles above the enemy must come in within 30 tiles sideways of it and no more than 100 over it, and the weapon must hit) ({rain.Count}): " + string.Join("; ", rain.Select(a => a.r.Label.Replace("CalamityMod:", "") + " aboveTarget=" + a.r.FarAbove + "/" + a.r.Far + " projs=" + a.r.Projs + " dmg=" + a.r.Damage + (SkyRainOk(a.r) ? " ok" : KnownSkyRainResiduals.Any(k => a.r.Label.EndsWith(":" + k)) ? " known residual" : " FAILS"))));
			Note("origin sweep, made nothing: " + string.Join(", ", all.Where(a => a.r.Projs == 0).Select(a => a.r.Label)));
			Note("origin sweep, projectiles but no damage: " + string.Join(", ", all.Where(a => a.r.Projs > 0 && a.r.Damage == 0).Select(a => a.r.Label)));
			foreach (var (r, cls) in all)
				if (!full && (r.Label.EndsWith("ArcNovaDiffuser") || r.Label.EndsWith("Tumbleweed")))
				{
					Note("origin sweep: " + r);
					Check($"origin: {r.Label} fires from the levy, not the commander (projs {r.Projs}, from levy {r.AtLevy}, from commander {r.AtCmd}, ticks near commander {r.NearCmdTicks}; above: {r.Up.AtCmd} / {r.Up.NearCmdTicks})", r.Projs > 0 && r.AtLevy > 0 && !r.FromCommanderBoth && r.Up.Projs > 0);
					Check($"origin: {r.Label} hits the enemy (damage {r.Damage} over {r.DamageTicks} ticks)", r.Damage > 0);
						Check($"origin: {r.Label} does the same while the commander holds her own use button (damage {r.Holds.Damage} against {r.Damage} idle, {r.Holds.Projs} shots against {r.Projs})", r.Holds.Damage * 2 >= r.Damage && r.Holds.Projs * 2 >= r.Projs);
					if (r.DrawProbes > 0) Check($"origin: {r.Label} is drawn from the levy (chain / arm anchor on the commander {r.DrawAtCmd} of {r.DrawProbes} probes, nearest {(r.MinDrawCmd / 16f):0.0} tiles)", r.DrawAtCmd == 0);
				}
			foreach (var (r, cls) in all)
				if (!full && SkyRainSample.Any(n => r.Label.EndsWith(":" + n)))
					Check($"origin: {r.Label} rains from above the enemy (spawned above the target {r.FarAbove} of {r.Far} far shots) and hits it (damage {r.Damage})", SkyRainOk(r));
			if (full) Check($"origin sweep: every weapon that places its shot on the screen a player sees makes shots and hits the enemy ({rain.Count} in the class; failing: {string.Join(", ", rain.Where(a => !SkyRainOk(a.r) && !KnownSkyRainResiduals.Any(k => a.r.Label.EndsWith(":" + k))).Select(a => a.r.Label.Replace("CalamityMod:", "")))}; known residuals {string.Join(", ", KnownSkyRainResiduals)})", rain.All(a => SkyRainOk(a.r) || KnownSkyRainResiduals.Any(k => a.r.Label.EndsWith(":" + k))));
			if (full) Check($"origin sweep: none of {done} weapons fires from the commander (spawned at her in both places: {all.Count(a => a.r.SpawnedAtCommanderBoth)}, drawn from her: {all.Count(a => a.r.DrawFromCommander)}, output controlled by her button: {all.Count(a => a.r.ButtonControlled)})", done > 0 && threw == 0 && !all.Any(a => a.r.SpawnedAtCommanderBoth || a.r.DrawFromCommander || (a.r.ButtonControlled && !KnownButtonResiduals.Any(k => a.r.Label.EndsWith(":" + k)))));
		}

		// ---------------------------------------------------------------------------------------------- SENTRY count: levies vs one player
		private static Player _oScratch, _oSaved255; private static bool _oPlayerPhase;

		private static void OriginPlayerUse(Item it, Vector2 aim, bool press)
		{
			Player p = _oScratch;
			p.whoAmI = 255; p.active = true; p.dead = false; p.ghost = false; p.name = "OriginScratch";
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
			if (p.statLifeMax2 < 500) p.statLifeMax = p.statLifeMax2 = 500; p.statLife = p.statLifeMax2; p.statManaMax2 = p.statManaMax = 200; p.statMana = 200;
			if (p.inventory[0] == null || p.inventory[0].IsAir || p.inventory[0].type != it.type) p.inventory[0] = it.Clone();
			p.selectedItem = 0; p.noItems = false;
			Main.screenPosition = Vector2.Zero; Main.mouseX = (int)aim.X; Main.mouseY = (int)aim.Y;
			p.controlUseItem = press; p.controlUseTile = false; p.channel = press && it.channel; if (!press) p.releaseUseItem = true;
			try { PlayerInput.Triggers.Current.MouseLeft = press; } catch { }
			Main.mouseLeft = press; Main.mouseLeftRelease = !press;
			p.direction = aim.X >= p.Center.X ? 1 : -1;
			int saved = Main.myPlayer;
			try
			{
				Main.myPlayer = 255;
				if (_itemCheck == null)
					foreach (var m in typeof(Player).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
						if (m.Name == "ItemCheck") { var ps = m.GetParameters(); if (ps.Length == 0) { _itemCheck = m; _itemCheckTakesInt = false; break; } if (ps.Length == 1 && ps[0].ParameterType == typeof(int)) { _itemCheck = m; _itemCheckTakesInt = true; } }
				_itemCheck.Invoke(p, _itemCheckTakesInt ? new object[] { 255 } : null);
			}
			catch (Exception e) { var ie = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e; if (!(ie.StackTrace ?? "").Contains("ItemCheck_PlayInstruments")) Note("  player ItemCheck threw " + ie.GetType().Name + ": " + ie.Message); }
			finally { Main.myPlayer = saved; }
		}
		private static MethodInfo _itemCheck; private static bool _itemCheckTakesInt;

		private sealed class SentryStats { public string Label; public int MaxTurrets, MaxMines, TurretSum, MineSum, Spawned, Gone, Ticks; public double Ms, TurretUpdMs, MineUpdMs; public long MineUpdates; public List<int> MineSeries = new(); public override string ToString() => $"{Label}: turrets max {MaxTurrets} avg {TurretSum / (double)Math.Max(1, Ticks):0.00}; mines max {MaxMines} avg {MineSum / (double)Math.Max(1, Ticks):0.0}; mines spawned {Spawned} ({Spawned * 60.0 / Math.Max(1, Ticks):0.0}/s), ended {Gone}; whole tick {Ms / Math.Max(1, Ticks):0.000} ms; of it turret updates {TurretUpdMs / Math.Max(1, Ticks):0.0000} ms/tick, mine updates {MineUpdMs / Math.Max(1, Ticks):0.0000} ms/tick ({MineUpdMs * 1000.0 / Math.Max(1, MineUpdates):0.0} us per mine update); {Ticks} ticks"; }

		private static SentryStats _timing; private static int _timeTurretT, _timeMineT;
		private static void TimedUpdate(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			SentryStats st = _timing;
			if (st == null || (self.type != _timeMineT && self.type != _timeTurretT)) { orig(self, i); return; }
			long t0 = Stopwatch.GetTimestamp();
			try { orig(self, i); }
			finally { double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency; if (self.type == _timeMineT) { st.MineUpdMs += ms; st.MineUpdates++; } else st.TurretUpdMs += ms; }
		}

		/// <summary>Counts a sentry weapon's turrets and projectiles over a run: <paramref name="levies"/> levies carrying it (0 with <paramref name="player"/> true: the stand-in player uses it through vanilla ItemCheck, <paramref name="hold"/> keeps the use button down).</summary>
		private static SentryStats SentryRun(string label, int weaponType, string turretName, string mineName, int levies, bool player, bool hold, int ticks)
		{
			var st = new SentryStats { Label = label };
			Mod cal = ModLoader.GetMod("CalamityMod");
			int turretT = cal.Find<ModProjectile>(turretName).Type, mineT = cal.Find<ModProjectile>(mineName).Type;
			ClearSoldiers();
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			SpawnOriginTarget(at);
			Item w = new Item(); w.SetDefaults(weaponType);
			var recs = new List<SoldierRecord>();
			for (int k = 0; k < levies; k++)
			{
				SoldierRecord rec = MakeOriginRecord(w.Clone(), 0); rec.Name = "Pvt. Mine" + k;
				RosterSystem.Instance.Regulars.Add(rec); recs.Add(rec);
				int body = RosterSystem.Instance.SpawnBody(rec, at + new Vector2(-30f * k, 0f));
				if (body < 0 || !rec.HasBody) throw new Exception("levy did not spawn");
				Main.npc[body].Bottom = at + new Vector2(-30f * k, 0f);
			}
			if (player)
			{
				_oSaved255 = Main.player[255];
				_oScratch = new Player { whoAmI = 255, name = "OriginScratch" };
				_oScratch.statLifeMax = _oScratch.statLifeMax2 = 500; _oScratch.statLife = 500; _oScratch.ResetEffects();
				_oScratch.inventory[0] = new Item(); _oScratch.selectedItem = 0;
				Main.player[255] = _oScratch; _my = 255; _cmdAt = at - new Vector2(0, 28f);
				if (Main.GameViewMatrix == null) Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null);
			}
			_timing = st; _timeTurretT = turretT; _timeMineT = mineT; On_Projectile.Update += TimedUpdate;
			try
			{
				Main.rand = new Terraria.Utilities.UnifiedRandom(4242);
				KeepCommander(); PinSweepTarget(); Step(20);
				var prev = new HashSet<int>(); var prevId = new Dictionary<int, int>();
				var sw = new Stopwatch();
				for (int k = 0; k < ticks; k++)
				{
					foreach (var r in recs) r.Mana = r.MaxMana;
					sw.Restart();
					if (player) { OriginPlayerUse(w, _sweepTarget.Center, hold || k == 0 || k == 1); }
					Step(1);
					sw.Stop(); st.Ms += sw.Elapsed.TotalMilliseconds; ClearNaturalHostiles();
					int turrets = 0, mines = 0; var now = new HashSet<int>();
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile p = Main.projectile[i]; if (!p.active) continue;
						if (p.type == turretT) turrets++;
						else if (p.type == mineT) { mines++; now.Add(i); if (!prev.Contains(i) || prevId[i] != p.identity) st.Spawned++; prevId[i] = p.identity; }
					}
					foreach (int i in prev) if (!now.Contains(i)) st.Gone++;
					prev = now; st.Ticks++; st.TurretSum += turrets; st.MineSum += mines; st.MaxTurrets = Math.Max(st.MaxTurrets, turrets); st.MaxMines = Math.Max(st.MaxMines, mines);
					if (k % 60 == 59) st.MineSeries.Add(mines);
				}
			}
			finally
			{
				On_Projectile.Update -= TimedUpdate; _timing = null;
				if (player)
				{
					_my = 0; if (_oScratch != null && ReferenceEquals(Main.player[255], _oScratch)) Main.player[255] = _oSaved255; _oScratch = null; _oSaved255 = null;
					global::Levy.Common.Targeting.EnemyTargeting.ResetState();
				}
				ClearSoldiers();
				_cmdAt = OriginCmdAt();
			}
			return st;
		}

		/// <summary>The Dreadmine Staff (Calamity sentry: a turret that plants sea mines): turrets and mines with 1 and 5 levies against one player who casts it once and one who holds the button.</summary>
		private static void SentryProbe()
		{
			string spec = Environment.GetEnvironmentVariable("LEVY_SENTRY_PROBE"); // Item/Turret/Shot
			if (string.IsNullOrEmpty(spec) || !ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
			string[] a = spec.Split('/'); if (a.Length != 3 || !cal.TryFind(a[0], out ModItem it)) return;
			SetupOriginArena(); KeepCommander(); Step(2);
			SentryStats pl = SentryRun("PLAYER " + a[0], it.Type, a[1], a[2], 0, true, false, 600);
			SentryStats lv = SentryRun("1 LEVY " + a[0], it.Type, a[1], a[2], 1, false, false, 600);
			Note("sentry probe: " + pl); Note("sentry probe: " + lv);
		}

		private static void DreadmineCount()
		{
			if (Environment.GetEnvironmentVariable("LEVY_ORIGIN") == "0" || !ModLoader.TryGetMod("CalamityMod", out Mod cal) || !cal.TryFind("DreadmineStaff", out ModItem staff)) return;
			bool full = Environment.GetEnvironmentVariable("LEVY_ORIGIN") == "1" || Environment.GetEnvironmentVariable("LEVY_ORIGIN_ALONE") == "1" || Environment.GetEnvironmentVariable("LEVY_DREADMINE") == "1";
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_DREADMINE_TICKS"), out int tk) ? tk : 900;
			SetupOriginArena();
			KeepCommander(); Step(2);
			try
			{
				SentryStats ctl = SentryRun("control (no weapon fired, 1 levy with a copper shortsword)", ItemID.CopperShortsword, "DreadmineTurret", "Dreadmine", 1, false, false, ticks);
				SentryStats p1 = SentryRun("ONE PLAYER, one cast", staff.Type, "DreadmineTurret", "Dreadmine", 0, true, false, ticks);
				SentryStats ph = full ? SentryRun("ONE PLAYER, button held", staff.Type, "DreadmineTurret", "Dreadmine", 0, true, true, ticks) : null;
				SentryStats l1 = SentryRun("1 LEVY", staff.Type, "DreadmineTurret", "Dreadmine", 1, false, false, ticks);
				SentryStats l5 = full ? SentryRun("5 LEVIES", staff.Type, "DreadmineTurret", "Dreadmine", 5, false, false, ticks) : null;
				Note("dreadmine: " + ctl); Note("dreadmine: " + p1); if (ph != null) Note("dreadmine: " + ph); Note("dreadmine: " + l1); if (l5 != null) Note("dreadmine: " + l5);
				Note("dreadmine mines per second of play (one count a second): player " + string.Join(",", p1.MineSeries) + " | 1 levy " + string.Join(",", l1.MineSeries) + (l5 != null ? " | 5 levies " + string.Join(",", l5.MineSeries) : ""));
				// a levy keeps ONE turret (a player's sentry slot), and its turret's mine count (cap 25) is the player's
				Check($"dreadmine: one levy keeps at most as many turrets as one player's slots ({l1.MaxTurrets} vs {p1.MaxTurrets})", p1.MaxTurrets >= 1 && l1.MaxTurrets <= Math.Max(1, p1.MaxTurrets));
				Check($"dreadmine: one levy's mines never pass one player's (max {l1.MaxMines} vs {p1.MaxMines}; per-turret cap 25)", l1.MaxMines <= Math.Max(25, p1.MaxMines));
				if (l5 != null) Check($"dreadmine: five levies keep {l5.MaxTurrets} turrets and at most {l5.MaxMines} mines (5 x 25 = 125 is the player's rule per tower)", l5.MaxTurrets <= 5 && l5.MaxMines <= 5 * 25);
			}
			catch (Exception e) { Check("dreadmine count threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
		}
	}
}
