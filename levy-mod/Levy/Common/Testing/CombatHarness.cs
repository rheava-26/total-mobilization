using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Common.Compat.Thorium;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// HEADLESS COMBAT HARNESS (measurement only; changes no game behaviour). Does NOTHING unless the environment variable LEVY_HARNESS is set (read once, at Load),
	/// and only on a server / single-player world (never a multiplayer client). Run it through tools/run_harness.sh.
	///
	/// WHAT IT DOES
	///  1. FAKE COMMANDER: marks Main.player[0] active + alive and named "LevyTester" (hp 500/500, mana 200) and pins its position at the test site, so
	///     LevyUtil.FindPlayerByName / SoldierNPC.Commander / RosterSystem.ResolveCommanders / CompatAllies.CommanderOf / healer + bard ally searches behave as if a
	///     player were present. It has no network client, no inventory use, no input. Its life is only touched at the start of a healer/bard trial (set to 50%).
	///  2. TEST RANGE: a sealed-off box high in the sky (rows 60..119 cleared, walls/liquid removed, floor rows 120..123 of gray brick, 201 tiles wide, centred on the
	///     world's middle column), so terrain never confounds a trial. Vanilla spawning is suppressed (HarnessSpawnSuppress) and stray hostiles are removed each tick.
	///  3. TRIALS: one soldier per (weapon, range) via the mod's real path (a SoldierRecord added to RosterSystem.Regulars + RosterSystem.SpawnBody, so the genuine
	///     SoldierNPC AI runs). Target = a pinned, huge-HP Green Slime (NOT the Target Dummy: SoldierTargeting.IsNeverTarget lists NPCID.TargetDummy), at 10 / 25 / 40 tiles.
	///     Each trial: SETUP ticks, then RUN ticks (default 360 = 6 s of game time), then cleanup. Healer/bard trials also get a wounded ally soldier and a 50% commander.
	///  4. RECORDS one TSV row per (weapon, range) to LEVY_HARNESS_OUT and one summary line per weapon to server.log, then exits the server WITHOUT saving the world.
	///
	/// ENV VARS: LEVY_HARNESS (enable), LEVY_HARNESS_OUT (TSV path), LEVY_HARNESS_MOD (mod whose items are tested, default ThoriumMod), LEVY_HARNESS_FILTER (comma list of
	/// case-insensitive substrings of item name / internal name), LEVY_HARNESS_TICKS (run ticks per range, default 360), LEVY_HARNESS_SHARD ("k/n": only weapons whose index % n == k),
	/// LEVY_HARNESS_SKIPCONTROLS (any value: skip the vanilla controls), LEVY_HARNESS_RANGES (comma list of tile distances, default 10,25,40).
	/// </summary>
	public class CombatHarness : ModSystem
	{
		internal static bool Enabled;
		internal static string OutPath, TestMod;
		private static string[] _filter;
		private static int _runTicks = 360, _shardK = 0, _shardN = 1;
		private static bool _skipControls;
		private static int[] _ranges = { 10, 25, 40 };

		// ---- range geometry (tiles) ----
		internal const int FloorY = 120;           // top row of the floor
		internal const int ClearTop = 60;          // rows ClearTop..FloorY-1 are air
		internal const int HalfWidth = 100;        // range spans cx-HalfWidth .. cx+HalfWidth
		private const int SetupTicks = 30;
		private const int CleanupTicks = 6;
		private const string CommanderName = "LevyTester";

		private enum Ph { Boot, Setup, Run, Cleanup, Done }
		private Ph _ph = Ph.Boot;
		private int _phTick, _bootTick;
		private int _cx;
		private readonly List<Trial> _trials = new();
		private int _ti;
		private readonly List<string> _rows = new();
		private readonly Dictionary<string, WeaponAgg> _agg = new();
		private readonly List<string> _weaponOrder = new();
		private HarnessLogCapture _capture;

		private sealed class Trial
		{
			public int ItemType;            // 0 = no weapon (negative control)
			public string Control = "";     // "pos" / "neg" / ""
			public string AmmoName = "";
			public int Range;
			public string Key;
		}

		private sealed class WeaponAgg
		{
			public string Name; public int Fired, Hit, Rows; public int Issued; public bool HealerCast, Healed, Exc;
		}

		public override void Load()
		{
			try
			{
				string v = Environment.GetEnvironmentVariable("LEVY_HARNESS");
				Enabled = !string.IsNullOrEmpty(v) && v != "0";
				if (!Enabled) return;
				OutPath = Environment.GetEnvironmentVariable("LEVY_HARNESS_OUT") ?? "levy_harness.tsv";
				TestMod = Environment.GetEnvironmentVariable("LEVY_HARNESS_MOD") ?? "ThoriumMod";
				string f = Environment.GetEnvironmentVariable("LEVY_HARNESS_FILTER");
				if (!string.IsNullOrWhiteSpace(f)) _filter = f.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_HARNESS_TICKS"), out int t) && t >= 60) _runTicks = t;
				string sh = Environment.GetEnvironmentVariable("LEVY_HARNESS_SHARD");
				if (!string.IsNullOrWhiteSpace(sh) && sh.Contains('/')) { var p = sh.Split('/'); if (int.TryParse(p[0], out int k) && int.TryParse(p[1], out int n) && n > 0) { _shardK = k; _shardN = n; } }
				_skipControls = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS_SKIPCONTROLS"));
				string rg = Environment.GetEnvironmentVariable("LEVY_HARNESS_RANGES");
				if (!string.IsNullOrWhiteSpace(rg))
				{
					var list = new List<int>();
					foreach (var s in rg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) if (int.TryParse(s, out int r) && r >= 3 && r <= 90) list.Add(r);
					if (list.Count > 0) _ranges = list.ToArray();
				}
				if (Main.dedServ) Main.OnTickForThirdPartySoftwareOnly += DriveServer;
				Mod.Logger.Info($"HARNESS: ENABLED (LEVY_HARNESS set). out={OutPath} mod={TestMod} filter={(f ?? "-")} ticks={_runTicks} shard={_shardK}/{_shardN} skipControls={_skipControls} ranges={string.Join(",", _ranges)}");
			}
			catch (Exception e) { Enabled = false; Mod.Logger.Warn("HARNESS: load failed, disabled: " + e.Message); }
		}

		public override void Unload() { try { Main.OnTickForThirdPartySoftwareOnly -= DriveServer; } catch { } Enabled = false; _capture?.Detach(); _capture = null; _filter = null; }

		public override void OnWorldLoad()
		{
			if (!Enabled) return;
			_ph = Ph.Boot; _bootTick = 0; _ti = 0; _trials.Clear(); _rows.Clear(); _agg.Clear(); _weaponOrder.Clear();
		}

		// =========================================================================================================
		// Tick driver
		// =========================================================================================================
		// A dedicated server with no client connected does not advance the world on its own, so (as the earlier drone harness did) the harness steps
		// Main.Update itself from the server loop's per-turn callback: the REAL game update runs (NPC AI, projectiles, every ModSystem/GlobalNPC hook), just
		// driven faster than 60/s. StepsPerTurn real updates per server-loop turn.
		private static System.Reflection.MethodInfo _update;
		private static bool _inDrive;
		private static int StepsPerTurn = 20;

		private static void DriveServer()
		{
			if (!Enabled || _inDrive || !Main.dedServ || Main.netMode != NetmodeID.Server) return;
			var h = Instance;
			if (h == null || h._ph == Ph.Done) return;
			_inDrive = true;
			try
			{
				_update ??= typeof(Main).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(GameTime) }, null);
				for (int i = 0; i < StepsPerTurn && h._ph != Ph.Done; i++) _update.Invoke(Main.instance, new object[] { new GameTime() });
			}
			catch (Exception e) { Instance?.Mod.Logger.Warn("HARNESS: world update threw: " + (e is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : e)); }
			finally { _inDrive = false; }
		}

		public override void PostUpdateEverything()
		{
			if (!Enabled || Main.netMode == NetmodeID.MultiplayerClient) return;
			try { Step(); }
			catch (Exception e)
			{
				Mod.Logger.Warn("HARNESS: driver exception: " + e);
				_cur?.Exceptions.Add("driver:" + e.GetType().Name + ":" + e.Message);
				if (_ph == Ph.Boot) { Finish("driver exception during boot"); }
				else { try { EndTrial(); } catch { } _ph = Ph.Cleanup; _phTick = 0; }
			}
		}

		private void Step()
		{
			if (_ph == Ph.Done) return;
			// frozen sunny noon: no night spawns, no events
			Main.dayTime = true; Main.time = 27000; Main.raining = false; Main.bloodMoon = false; Main.eclipse = false;
			if (Main.invasionType != 0) Main.invasionType = 0;

			switch (_ph)
			{
				case Ph.Boot:
					if (++_bootTick < 180) return;
					BootInit();
					return;
				case Ph.Setup:
					if (_phTick == 0) BeginTrial();
					KeepSite();
					if (++_phTick >= SetupTicks) { _ph = Ph.Run; _phTick = 0; StartRun(); }
					return;
				case Ph.Run:
					KeepSite();
					SampleTick();
					if (++_phTick >= _runTicks) { EndTrial(); _ph = Ph.Cleanup; _phTick = 0; }
					return;
				case Ph.Cleanup:
					if (_phTick == 0) Teardown();
					if (++_phTick >= CleanupTicks)
					{
						_ti++;
						_ph = Ph.Setup; _phTick = 0;
						if (_ti >= _trials.Count) Finish("all trials complete");
					}
					return;
			}
		}

		// =========================================================================================================
		// Boot: range, commander, trial list
		// =========================================================================================================
		private void BootInit()
		{
			_cx = Main.maxTilesX / 2;
			TryAttachCapture();
			BuildRange();
			PlaceCommander(false);
			BuildTrials();
			Mod.Logger.Info($"HARNESS: range built at x={_cx - HalfWidth}..{_cx + HalfWidth}, floor y={FloorY}; world {Main.maxTilesX}x{Main.maxTilesY}; {_trials.Count} trials queued; thoriumActive={ThoriumCompatSystem.Active}; netMode={Main.netMode}");
			if (_trials.Count == 0) { Finish("no trials"); return; }
			_ph = Ph.Setup; _phTick = 0; _ti = 0;
		}

		private void TryAttachCapture()
		{
			try { _capture = new HarnessLogCapture(); _capture.Attach(Mod.Logger); }
			catch (Exception e) { Mod.Logger.Warn("HARNESS: could not attach the log capture (in-game exception capture disabled): " + e.Message); _capture = null; }
		}

		private void BuildRange()
		{
			int x0 = Math.Max(10, _cx - HalfWidth), x1 = Math.Min(Main.maxTilesX - 10, _cx + HalfWidth);
			for (int x = x0 - 2; x <= x1 + 2; x++)
			{
				for (int y = ClearTop; y < FloorY; y++)
				{
					Tile t = Main.tile[x, y];
					t.HasTile = false; t.TileType = 0; t.WallType = 0; t.LiquidAmount = 0; t.TileFrameX = 0; t.TileFrameY = 0;
				}
				for (int y = FloorY; y < FloorY + 4; y++)
				{
					Tile t = Main.tile[x, y];
					t.HasTile = true; t.TileType = TileID.GrayBrick; t.WallType = 0; t.LiquidAmount = 0;
					t.BlockType = Terraria.ID.BlockType.Solid;
					t.TileFrameX = 0; t.TileFrameY = 0;
				}
			}
			// side walls so nothing wanders off the range
			foreach (int x in new[] { x0 - 3, x1 + 3 })
				for (int y = ClearTop; y < FloorY + 4; y++)
				{
					Tile t = Main.tile[x, y];
					t.HasTile = true; t.TileType = TileID.GrayBrick; t.BlockType = Terraria.ID.BlockType.Solid;
				}
		}

		private Vector2 SoldierX0Bottom => new Vector2(_cx * 16f + 8f, FloorY * 16f);

		/// <summary>Stand-in commander. Sets: active, name, alive, hp/mana, team, position (4 tiles left of the soldier's spawn), zero velocity.</summary>
		private void PlaceCommander(bool wounded)
		{
			Player p = Main.player[0];
			if (!p.active || p.name != CommanderName)
			{
				p.active = true;
				p.name = CommanderName;
				p.statLifeMax = p.statLifeMax2 = 500;
				p.statManaMax = p.statManaMax2 = 200;
				p.statLife = 500; p.statMana = 200;
				p.dead = false; p.team = 0;
				p.whoAmI = 0;
			}
			p.dead = false;
			p.statLifeMax = 500; p.statLifeMax2 = 500;
			p.statLife = wounded ? 250 : 500;
			p.statMana = 200;
			p.position = new Vector2(_cx * 16f - 4 * 16f, FloorY * 16f - p.height);
			p.velocity = Vector2.Zero;
			for (int b = 0; b < p.buffType.Length; b++) { p.buffType[b] = 0; p.buffTime[b] = 0; }
		}

		private void KeepSite()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName;
			p.position = new Vector2(_cx * 16f - 4 * 16f, FloorY * 16f - p.height);
			p.velocity = Vector2.Zero;
			p.statMana = p.statManaMax2;
			if (p.statLife < 1) p.statLife = 1;
			// sweep: nothing hostile except our target may exist on/near the range
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.townNPC) continue;
				if (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) continue;
				if (!n.friendly || n.type == NPCID.TargetDummy) { n.active = false; }
			}
		}

		// =========================================================================================================
		// Trial list
		// =========================================================================================================
		private static bool NameMatches(Item it)
		{
			if (_filter == null) return true;
			string n = it.Name ?? ""; string inn = it.ModItem?.Name ?? "";
			foreach (var f in _filter) if (n.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0 || inn.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
			return false;
		}

		/// <summary>The weapon predicate: from the tested mod, and anything Levy's classifier or the companion compat treats as a weapon, plus any ordinary damage-dealing usable item.</summary>
		internal static bool IsWeaponCandidate(Item it, out string why)
		{
			why = "";
			if (it == null || it.IsAir || it.ModItem == null) return false;
			if (it.ModItem.Mod.Name != TestMod) return false;
			if (WeaponClassifier.IsIssuableWeapon(it)) { why = "issuable"; return true; }
			string kind = CompanionKind(it);
			if (kind != "") { why = kind; return true; }
			if (it.damage > 0 && it.useStyle > 0 && it.ammo == 0 && it.pick == 0 && it.axe == 0 && it.hammer == 0 && !it.accessory && !it.vanity && it.mountType < 0 && it.shoot >= 0
				&& !(it.consumable && it.shoot == 0)) { why = "damage-item"; return true; }
			return false;
		}

		/// <summary>"bard" / "healer:Role" / "" via the Thorium probe (only when Thorium is loaded).</summary>
		internal static string CompanionKind(Item it)
		{
			try { if (ModLoader.HasMod("ThoriumMod") && ThoriumCompatSystem.Active) return HarnessThoriumProbe.KindOf(it); }
			catch (Exception) { }
			return "";
		}

		private void BuildTrials()
		{
			if (!_skipControls)
			{
				AddWeaponTrials(ItemID.Musket, "pos", "Musket Ball", ItemID.MusketBall);
				AddWeaponTrials(ItemID.WoodenBow, "pos", "Wooden Arrow", ItemID.WoodenArrow);
				AddWeaponTrials(0, "neg", "", 0);
				AddWeaponTrials(0, "negc", "", 0); // no weapon, but WITH the wounded ally + 50% commander: the natural-regen baseline for the heal columns
			}
			int idx = 0;
			int count = ItemLoader.ItemCount;
			var seen = new HashSet<int>();
			for (int t = ItemID.Count; t < count; t++)
			{
				Item it = new Item();
				try { it.SetDefaults(t); } catch { continue; }
				if (it.IsAir || !seen.Add(t)) continue;
				if (!IsWeaponCandidate(it, out _)) continue;
				if (!NameMatches(it)) continue;
				if ((idx++ % _shardN) != _shardK) continue;
				AddWeaponTrials(t, "", null, 0);
			}
		}

		private void AddWeaponTrials(int itemType, string control, string ammoName, int ammoType)
		{
			string name = itemType == 0 ? (control == "negc" ? "(no weapon, with wounded ally + 50% commander)" : "(no weapon)") : ContentSamples.ItemsByType[itemType].Name;
			var item = itemType == 0 ? null : ContentSamples.ItemsByType[itemType];
			bool issuable = item != null && WeaponClassifier.IsIssuableWeapon(item);
			string key = itemType == 0 ? "NEG-" + control : itemType.ToString();
			_weaponOrder.Add(key);
			_agg[key] = new WeaponAgg { Name = name, Issued = issuable ? 1 : 0 };
			if (item != null && !issuable && control == "")
			{
				// can't be held: one row, no simulation
				_trials.Add(new Trial { ItemType = itemType, Control = control, Range = 0, Key = key });
				return;
			}
			foreach (int r in _ranges) _trials.Add(new Trial { ItemType = itemType, Control = control, Range = r, Key = key, AmmoName = ammoName ?? "" });
		}

		// =========================================================================================================
		// Per-trial state
		// =========================================================================================================
		private sealed class Cur
		{
			public Trial T; public Item Weapon; public string Family = "-", Kind = "", Where = "-"; public bool Issuable;
			public SoldierRecord Rec, AllyRec; public int BodyIdx = -1, AllyIdx = -1, TargetIdx = -1;
			public string Ammo = "-";
			public bool SetupOk;
			public List<string> Exceptions = new();
			public Dictionary<string, int> Proj = new(); public int ProjOther, ProjTotal;
			public long Damage; public int Hits;
			public int AnimTicks, FirstFire = -1, EngageTicks, AcquiredTicks, AliveTicks, DeadTicks; public float MinDist = 9999f, StartDist;
			public int CmdBefore, CmdAfter, CmdMax, AllyBefore, AllyAfter, AllyMax, AllyMaxLife;
			public int InspStart = -1, InspEnd = -1, InspSpent, InspPrev = -1, InspMax = -1;
			public uint EmpUnion, AllyEmpUnion;
			public HashSet<string> CmdBuffNames = new();
			public int ManaStart, ManaEnd, ManaMin;
			public float MovedX;
			public float StartX;
			public string Notes = "";
			public int ActiveTick;
			public HashSet<string> States = new();
			public int HealProjSeen;
		}
		private Cur _cur;

		/// <summary>Called by the projectile watcher at spawn.</summary>
		internal static CombatHarness Instance => ModContent.GetInstance<CombatHarness>();
		internal bool Collecting => Enabled && _ph == Ph.Run && _cur != null;
		private readonly List<Projectile> _pendingProj = new();
		internal void NoteSpawn(Projectile p) { if (Collecting) _pendingProj.Add(p); }

		private void BeginTrial()
		{
			Trial t = _trials[_ti];
			_cur = new Cur { T = t };
			_pendingProj.Clear();
			_capture?.Begin();
			ClearAll();
			try
			{
				Item w = null;
				if (t.ItemType > 0)
				{
					w = new Item(); w.SetDefaults(t.ItemType);
					if (w.IsAir) throw new Exception("SetDefaults left air for type " + t.ItemType);
				}
				_cur.Weapon = w;
				if (w != null)
				{
					_cur.Issuable = WeaponClassifier.IsIssuableWeapon(w);
					var prof = WeaponClassifier.GetProfile(w);
					_cur.Family = prof.Family.ToString() + "/rng" + prof.RangeTiles;
					_cur.Kind = CompanionKind(w);
				}
				if (t.Range == 0) { _cur.SetupOk = false; _cur.Notes = "not issuable: no trial run"; return; } // not-issued row

				bool companion = _cur.Kind != "" || t.Control == "negc";
				PlaceCommander(companion);

				// ---- the soldier ----
				var rec = MakeRecord("Pvt. Subject");
				var gear = rec.Gear;
				if (w != null)
				{
					Item held = w.Clone();
					if (WeaponClassifier.Classify(held) == WeaponFamily.Thrown) held.stack = Math.Min(held.maxStack > 1 ? held.maxStack : 999, 999);
					if (gear.CanHold(SoldierGear.SlotWeapon, held)) { gear.Weapon = held; _cur.Where = "weapon"; }
					else if (gear.CanHold(SoldierGear.SlotSidearm, held)) { gear.Sidearm = held; _cur.Where = "sidearm"; }
					else { _cur.Where = "none(CanHold false)"; }
					if (_cur.Where == "weapon") gear.SetLocked(SoldierGear.SlotWeapon, true);
					if (_cur.Where == "sidearm") gear.SetLocked(SoldierGear.SlotSidearm, true);
					if (w.useAmmo > 0 || t.AmmoName != "")
					{
						Item ammo = FindAmmo(w, t.AmmoName);
						if (ammo != null && gear.CanHold(SoldierGear.SlotAmmo, ammo)) { gear.Ammo = ammo; gear.SetLocked(SoldierGear.SlotAmmo, true); _cur.Ammo = ammo.Name + " x" + ammo.stack; }
						else _cur.Ammo = ammo == null ? "NONE FOUND for useAmmo " + w.useAmmo : ammo.Name + " (CanHold false)";
					}
				}
				rec.LoadoutLocked = true;
				rec.Order = SoldierOrder.Follow;
				rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				RosterSystem.Instance.Regulars.Add(rec);
				_cur.Rec = rec;
				Vector2 at = SoldierX0Bottom;
				int idx = RosterSystem.Instance.SpawnBody(rec, at);
				_cur.BodyIdx = idx;
				if (idx < 0 || !rec.HasBody) throw new Exception("SpawnBody failed (idx=" + idx + ")");
				var body = (SoldierNPC)Main.npc[idx].ModNPC;
				body.NPC.Bottom = at;

				// ---- companion trials: a wounded squadmate ----
				if (companion)
				{
					var ar = MakeRecord("Pvt. Wounded");
					ar.Order = SoldierOrder.Hold; ar.HoldFire = true; ar.LoadoutLocked = true;
					ar.CurrentHp = Math.Max(1, (int)(ar.MaxHp * 0.30f)); ar.Mana = ar.MaxMana;
					RosterSystem.Instance.Regulars.Add(ar);
					_cur.AllyRec = ar;
					Vector2 aat = at + new Vector2(-7 * 16f, 0f);
					int ai = RosterSystem.Instance.SpawnBody(ar, aat);
					_cur.AllyIdx = ai;
					if (ai >= 0 && ar.HasBody) { var ab = (SoldierNPC)Main.npc[ai].ModNPC; ab.NPC.Bottom = aat; ab.ReceiveOrder(SoldierOrder.Hold); ab.NPC.life = ar.CurrentHp; _cur.AllyMaxLife = ab.NPC.lifeMax; }
					else _cur.Notes += "ally spawn failed;";
				}

				// ---- target ----
				SpawnTarget(t.Range);
				_cur.StartX = body.NPC.Center.X;
				_cur.SetupOk = true;
			}
			catch (Exception e) { _cur.Exceptions.Add("setup:" + e.GetType().Name + ":" + e.Message); }
		}

		private SoldierRecord MakeRecord(string name)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy,
				Temperament = Temperament.Sellsword, Male = true, Name = name,
				CommanderName = CommanderName, CommanderIndex = 0,
				LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, // 500 hp / 200 mana (a fully grown soldier)
				HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			return rec;
		}

		private void SpawnTarget(int rangeTiles)
		{
			Vector2 pos = new Vector2(_cx * 16f + 8f + rangeTiles * 16f, FloorY * 16f);
			int i = NPC.NewNPC(new EntitySource_WorldEvent("LevyHarness"), (int)pos.X, (int)pos.Y, NPCID.GreenSlime);
			if (i < 0 || i >= Main.maxNPCs) throw new Exception("target NewNPC failed");
			NPC n = Main.npc[i];
			var ht = n.GetGlobalNPC<HarnessTarget>();
			ht.IsTarget = true;
			n.lifeMax = 1_000_000; n.life = 1_000_000; n.defense = 0; n.damage = 0; n.knockBackResist = 0f;
			n.width = 64; n.height = 96; // a generous hitbox (humanoid-sized) so Levy's +-6 degree aim error does not hide a working weapon
			n.noGravity = true; n.noTileCollide = true; n.aiStyle = -1; n.friendly = false; n.dontTakeDamage = false; n.value = 0f;
			n.velocity = Vector2.Zero;
			n.Bottom = pos;
			ht.Pin = n.position;
			n.netUpdate = true;
			_cur.TargetIdx = i;
		}

		private static Item FindAmmo(Item weapon, string preferredName)
		{
			int want = weapon.useAmmo;
			int type = 0;
			if (preferredName != "") { type = preferredName == "Musket Ball" ? ItemID.MusketBall : preferredName == "Wooden Arrow" ? ItemID.WoodenArrow : 0; }
			if (type == 0)
			{
				if (want == AmmoID.Bullet) type = ItemID.MusketBall;
				else if (want == AmmoID.Arrow) type = ItemID.WoodenArrow;
				else if (want == AmmoID.Rocket) type = ItemID.RocketI;
				else if (want == AmmoID.Dart) type = ItemID.PoisonDart;
				else if (want == AmmoID.Gel) type = ItemID.Gel;
				else if (want == AmmoID.Flare) type = ItemID.Flare;
				else if (want == AmmoID.Snowball) type = ItemID.Snowball;
				else if (want == AmmoID.Stake) type = ItemID.Stake;
				else if (want == AmmoID.Sand) type = ItemID.SandBlock;
				else
				{
					for (int t = 1; t < ItemLoader.ItemCount && type == 0; t++)
					{
						Item s = ContentSamples.ItemsByType.TryGetValue(t, out var ss) ? ss : null;
						if (s != null && !s.IsAir && s.ammo == want && !s.notAmmo) type = t;
					}
				}
			}
			if (type == 0) return null;
			Item a = new Item(); a.SetDefaults(type);
			a.stack = Math.Min(a.maxStack, LevyConstants.AmmoPouchMax);
			return a;
		}

		private void StartRun()
		{
			var c = _cur;
			if (c == null || !c.SetupOk) return;
			try
			{
				Player p = Main.player[0];
				c.CmdBefore = c.CmdMax = c.CmdAfter = p.statLife;
				if (c.AllyIdx >= 0 && Main.npc[c.AllyIdx].active) { c.AllyBefore = c.AllyMax = c.AllyAfter = Main.npc[c.AllyIdx].life; }
				var body = Main.npc[c.BodyIdx].ModNPC as SoldierNPC;
				c.ManaStart = c.ManaMin = c.Rec.Mana;
				if (c.Kind == "bard") { c.InspStart = c.InspPrev = HarnessThoriumProbe.BardResource(body); c.InspMax = HarnessThoriumProbe.BardMax(body); }
				c.StartDist = Vector2.Distance(Main.npc[c.BodyIdx].Center, Main.npc[c.TargetIdx].Center) / 16f;
				_pendingProj.Clear();
			}
			catch (Exception e) { c.Exceptions.Add("startrun:" + e.GetType().Name + ":" + e.Message); }
		}

		private void SampleTick()
		{
			var c = _cur;
			if (c == null || !c.SetupOk) return;
			try
			{
				c.ActiveTick++;
				NPC bn = Main.npc[c.BodyIdx];
				bool alive = bn.active && bn.ModNPC is SoldierNPC sb && ReferenceEquals(sb.Record, c.Rec) && sb.IsAlive;
				if (!alive) { c.DeadTicks++; }
				else
				{
					c.AliveTicks++;
					var s = (SoldierNPC)bn.ModNPC;
					if (s.ItemAnimation > 0) { c.AnimTicks++; if (c.FirstFire < 0) c.FirstFire = c.ActiveTick; }
					if (s.State == SoldierState.Engage) c.EngageTicks++;
					c.States.Add(s.State.ToString());
					if (s.TargetIndex == c.TargetIdx) c.AcquiredTicks++;
					NPC tn = Main.npc[c.TargetIdx];
					if (tn.active) { float d = Vector2.Distance(bn.Center, tn.Center) / 16f; if (d < c.MinDist) c.MinDist = d; }
					c.MovedX = bn.Center.X - c.StartX;
					c.ManaEnd = c.Rec.Mana; if (c.Rec.Mana < c.ManaMin) c.ManaMin = c.Rec.Mana;
					if (bn.TryGetGlobalNPC(out SoldierThoriumGlobalNPC g)) c.EmpUnion |= g.EmpMask;
					if (c.Kind == "bard")
					{
						int r = HarnessThoriumProbe.BardResource(s);
						if (c.InspPrev >= 0 && r < c.InspPrev) c.InspSpent += c.InspPrev - r;
						c.InspPrev = r; c.InspEnd = r;
					}
				}

				// target damage
				NPC t = Main.npc[c.TargetIdx];
				if (t.active && t.life < t.lifeMax) { c.Damage += t.lifeMax - t.life; c.Hits++; t.life = t.lifeMax; }
				if (!t.active) c.Exceptions.Add("target vanished @tick" + c.ActiveTick);

				// healing
				Player p = Main.player[0];
				c.CmdAfter = p.statLife; if (p.statLife > c.CmdMax) c.CmdMax = p.statLife;
				foreach (int b in p.buffType) if (b > 0) c.CmdBuffNames.Add(BuffLoader.GetBuff(b)?.Name ?? ("buff" + b));
				if (c.AllyIdx >= 0)
				{
					NPC an = Main.npc[c.AllyIdx];
					if (an.active && an.ModNPC is SoldierNPC)
					{
						c.AllyAfter = an.life; if (an.life > c.AllyMax) c.AllyMax = an.life;
						if (an.TryGetGlobalNPC(out SoldierThoriumGlobalNPC ag)) c.AllyEmpUnion |= ag.EmpMask;
					}
				}

				// projectiles spawned this tick
				ResolvePending();
			}
			catch (Exception e) { c.Exceptions.Add("sample:" + e.GetType().Name + ":" + e.Message); }
		}

		private void ResolvePending()
		{
			var c = _cur;
			for (int i = 0; i < _pendingProj.Count; i++)
			{
				Projectile p = _pendingProj[i];
				string name = "vanilla" + p.type;
				if (p.ModProjectile != null) name = p.ModProjectile.Mod.Name + "/" + p.ModProjectile.Name;
				else if (ProjectileID.Search.TryGetName(p.type, out string vname)) name = vname;
				string key = name + "#" + p.type;
				bool ours = false;
				if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) ours = tag.SoldierIndex == c.BodyIdx;
				if (ours)
				{
					c.Proj[key] = c.Proj.TryGetValue(key, out int n) ? n + 1 : 1; c.ProjTotal++;
					if (name.EndsWith("SoldierHeal")) c.HealProjSeen++;
				}
				else c.ProjOther++;
			}
			_pendingProj.Clear();
		}

		private void EndTrial()
		{
			var c = _cur;
			if (c == null) return;
			if (c.SetupOk) ResolvePending();
			var cap = _capture?.End();
			if (cap != null) foreach (var m in cap) c.Exceptions.Add("log:" + m);
			string row = BuildRow(c);
			_rows.Add(row);
			// aggregate
			var a = _agg[c.T.Key]; a.Rows++;
			if (c.ProjTotal > 0 || c.AnimTicks > 0) a.Fired++;
			if (c.Damage > 0) a.Hit++;
			if (c.HealProjSeen > 0) a.HealerCast = true;
			if (c.CmdMax > c.CmdBefore + 1 || c.AllyMax > c.AllyBefore) a.Healed = true;
			if (c.Exceptions.Count > 0) a.Exc = true;
			// per-weapon summary line once its last row is in
			bool last = _ti + 1 >= _trials.Count || _trials[_ti + 1].Key != c.T.Key;
			if (last)
			{
				var rows = _rows.Count;
				Mod.Logger.Info($"HARNESS SUMMARY | {a.Name} | issued={(a.Issued == 1 ? "Y" : "N")} | fired-in-ranges={a.Fired}/{a.Rows} | hit-in-ranges={a.Hit}/{a.Rows} | healer-cast={a.HealerCast} healed={a.Healed} | exceptions={a.Exc} | family={c.Family}");
			}
			WriteTsv();
		}

		private static string Fmt(Dictionary<string, int> d) => d.Count == 0 ? "-" : string.Join(";", d.OrderBy(k => k.Key).Select(k => k.Key + "x" + k.Value));

		private string BuildRow(Cur c)
		{
			var t = c.T;
			Item w = c.Weapon;
			string name = w == null ? (t.Control == "negc" ? "(no weapon, with wounded ally + 50% commander)" : "(no weapon)") : w.Name;
			string inner = w?.ModItem?.Name ?? (w != null ? ItemID.Search.GetName(w.type) : "-");
			string mod = w == null ? "-" : (w.ModItem?.Mod.Name ?? "Terraria");
			var f = new List<string>
			{
				t.Control == "pos" ? "POSCONTROL" : t.Control == "neg" ? "NEGCONTROL" : t.Control == "negc" ? "NEGCONTROL-COMPANION" : "weapon",
				name, (w?.type ?? 0).ToString(), inner, mod, c.Family, c.Kind == "" ? "-" : c.Kind,
				c.Issuable ? "Y" : "N", c.Where, c.Ammo, t.Range == 0 ? "-" : t.Range.ToString(), c.SetupOk ? "Y" : "N",
				c.AliveTicks + "/" + c.ActiveTick, string.Join(",", c.States.OrderBy(x => x)), c.EngageTicks.ToString(), c.AcquiredTicks.ToString(),
				c.MinDist > 9000 ? "-" : c.MinDist.ToString("0.0"), c.StartDist.ToString("0.0"), c.MovedX.ToString("0"),
				(c.ProjTotal > 0 ? "Y" : "N"), c.ProjTotal.ToString(), Fmt(c.Proj), c.ProjOther.ToString(),
				c.AnimTicks.ToString(), c.FirstFire.ToString(),
				c.Damage.ToString(), c.Hits.ToString(),
				c.AllyIdx >= 0 || c.Kind.StartsWith("healer") || c.Kind == "bard" ? $"{c.CmdBefore}>{c.CmdAfter}(max{c.CmdMax})" : "-",
				c.AllyIdx >= 0 ? $"{c.AllyBefore}>{c.AllyAfter}(max{c.AllyMax}/{c.AllyMaxLife})" : "-",
				c.InspStart < 0 ? "-" : $"{c.InspStart}>{c.InspEnd}/{c.InspMax}", c.Kind == "bard" ? c.InspSpent.ToString() : "-",
				c.EmpUnion == 0 ? "-" : "0x" + c.EmpUnion.ToString("x"), c.AllyEmpUnion == 0 ? "-" : "0x" + c.AllyEmpUnion.ToString("x"),
				c.CmdBuffNames.Count == 0 ? "-" : string.Join(",", c.CmdBuffNames.OrderBy(x => x)),
				c.SetupOk ? $"{c.ManaStart}>{c.ManaEnd}(min{c.ManaMin})" : "-",
				c.Exceptions.Count == 0 ? "-" : string.Join(" || ", c.Exceptions.Take(6).Select(Clean)) + (c.Exceptions.Count > 6 ? " ||(+" + (c.Exceptions.Count - 6) + " more)" : ""),
				string.IsNullOrEmpty(c.Notes) ? "-" : Clean(c.Notes),
			};
			return string.Join("\t", f.Select(Clean));
		}

		private static string Clean(string s) => (s ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

		private const string Header = "kind\tweapon\titemType\tinternal\tmod\tclassifier\tcompanionKind\tissued\theldIn\tammoGiven\trangeTiles\tsetupOk\taliveTicks\tstates\tengageTicks\ttargetAcquiredTicks\tminDistTiles\tstartDistTiles\tsoldierMovedPx\tfired(proj)\tprojectiles\tprojTypes\tprojUntagged\titemAnimTicks\tfirstAnimTick\tdamageToTarget\ttargetHitTicks\tcommanderHp\tallyHp\tinspiration(start>end/max)\tinspSpent\tempowermentsSoldier\tempowermentsAlly\tcommanderBuffs\tmana(start>end)\texceptions\tnotes";

		private void WriteTsv()
		{
			try
			{
				var sb = new StringBuilder(); sb.AppendLine(Header);
				foreach (var r in _rows) sb.AppendLine(r);
				string tmp = OutPath + ".tmp";
				File.WriteAllText(tmp, sb.ToString());
				File.Move(tmp, OutPath, true);
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS: could not write TSV: " + e.Message); }
		}

		private void Teardown()
		{
			ClearAll();
		}

		/// <summary>Removes every soldier of the test commander (record + body), the target and all projectiles.</summary>
		private void ClearAll()
		{
			try
			{
				var ros = RosterSystem.Instance;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active) continue;
					if (n.ModNPC is SoldierNPC sn) { if (sn.Record != null) { sn.Record.Gear = new SoldierGear(); } n.active = false; }
					else if (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) n.active = false;
				}
				ros.Regulars.RemoveAll(r => r.CommanderName == CommanderName);
				for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active) Main.projectile[i].Kill();
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				for (int i = 0; i < Main.maxItems; i++) if (Main.item[i].active) Main.item[i].active = false;
				_pendingProj.Clear();
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS: clear failed: " + e.Message); }
		}

		private void Finish(string why)
		{
			if (_ph == Ph.Done) return;
			_ph = Ph.Done;
			try { ClearAll(); } catch { }
			WriteTsv();
			Mod.Logger.Info($"HARNESS: DONE ({why}); {_rows.Count} rows -> {OutPath}");
			Mod.Logger.Info("HARNESS: shutting the server down WITHOUT saving the world.");
			try { Netplay.SaveOnServerExit = false; Netplay.Disconnect = true; } catch { }
			var th = new System.Threading.Thread(() => { System.Threading.Thread.Sleep(15000); Environment.Exit(0); }) { IsBackground = true };
			th.Start();
		}
	}

	/// <summary>Marks the harness target and pins it in place (no AI, no gravity, no knockback).</summary>
	public class HarnessTarget : GlobalNPC
	{
		public override bool InstancePerEntity => true;
		public bool IsTarget;
		public Vector2 Pin;
		public override bool PreAI(NPC npc)
		{
			if (!IsTarget) return true;
			npc.velocity = Vector2.Zero; npc.position = Pin; npc.timeLeft = 3600; npc.noGravity = true;
			return false;
		}
		public override bool CheckActive(NPC npc) => !IsTarget;
	}

	/// <summary>While the harness is on: no vanilla spawns anywhere.</summary>
	public class HarnessSpawnSuppress : GlobalNPC
	{
		public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
		{
			if (!CombatHarness.Enabled) return;
			spawnRate = 100000000; maxSpawns = 0;
		}
		public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
		{
			if (!CombatHarness.Enabled) return;
			pool.Clear();
		}
	}

	/// <summary>Reports every projectile that spawns during a trial's measured window to the harness.</summary>
	public class HarnessProjectileWatch : GlobalProjectile
	{
		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (!CombatHarness.Enabled) return;
			CombatHarness.Instance?.NoteSpawn(projectile);
		}
	}

	/// <summary>Captures WARN/ERROR log lines (and anything with an exception) logged during a trial, so a swallowed in-game exception is attributed to its trial.</summary>
	internal sealed class HarnessLogCapture : log4net.Appender.AppenderSkeleton
	{
		private readonly object _lock = new();
		private Dictionary<string, int> _msgs = new();
		private bool _on;
		private log4net.Repository.Hierarchy.Logger _root;

		public void Attach(log4net.ILog log)
		{
			var repo = (log4net.Repository.Hierarchy.Hierarchy)log.Logger.Repository;
			_root = repo.Root;
			Name = "LevyHarnessCapture";
			ActivateOptions();
			_root.AddAppender(this);
		}
		public void Detach() { try { _root?.RemoveAppender(this); } catch { } }
		public void Begin() { lock (_lock) { _msgs = new Dictionary<string, int>(); _on = true; } }
		public List<string> End()
		{
			lock (_lock)
			{
				_on = false;
				return _msgs.Select(k => (k.Value > 1 ? "x" + k.Value + " " : "") + k.Key).ToList();
			}
		}
		protected override void Append(log4net.Core.LoggingEvent e)
		{
			lock (_lock)
			{
				if (!_on) return;
				string msg = e.RenderedMessage ?? "";
				if (msg.StartsWith("HARNESS")) return;
				bool bad = e.Level >= log4net.Core.Level.Warn || e.ExceptionObject != null;
				if (!bad) return;
				string key = "[" + e.Level + " " + e.LoggerName + "] " + (msg.Length > 260 ? msg.Substring(0, 260) : msg);
				if (e.ExceptionObject != null) key += " {" + e.ExceptionObject.GetType().Name + ": " + e.ExceptionObject.Message + "}";
				key = key.Replace("\r", " ").Replace("\n", " ");
				_msgs[key] = _msgs.TryGetValue(key, out int n) ? n + 1 : 1;
			}
		}
	}
}
