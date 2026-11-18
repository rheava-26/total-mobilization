using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// GEAR CACHE: the gear frame's equipment build, run only when its inputs changed.
	/// <para/>The frame (SoldierNPC.GearUpkeep.cs) has two halves. The BUILD (ResetEffects of the game and every mod, the buff pass, UpdateEquips with every item's hooks, the armor sets, the station and potion stats) wipes the
	/// dummy's stats and rebuilds them from its gear, buffs and state. The TAIL (PostUpdateEquips, Calamity's per-tick step in PostUpdateMiscEffects: Rage, Adrenaline, rogue stealth, cooldowns, defense damage) works across ticks.
	/// The build is a function of its inputs: given the same dummy state, the same gear and buffs, and the same world flags, it leaves the dummy in the same state. So when nothing it reads has changed since the last
	/// full build, the state it would leave is the one it left then.
	/// <para/>Each tick the dummy is imaged (StateImage: every field of the Player, of every ModPlayer and of the worn items, collections followed) at the point the build starts and compared with the image taken at the start
	/// of the last full build. Equal (outside the leaves listed in <see cref="Classify"/>), the image of what that build produced is written back and the tail runs on it as usual; anything different, and the build runs in full.
	/// The listed leaves are the body's motion (Levy moves the dummy every tick), what the tail itself evolves and the build never reads (Rage, Adrenaline, stealth: checked against Calamity's build code), what the build
	/// overwrites before anything reads it (the damage classes, aggro), and the few counters the build steps itself, which are stepped here by the same statements (<see cref="Step"/>).
	/// <para/>A build that consumed random numbers, spawned a projectile or wrote a leaf the cache keeps is not reused (the levy stays on the full frame until a build does none of that). LEVY_GEARCACHE=0 turns it off;
	/// LEVY_GEARCACHE_PARITY=1 runs the full build on every cached tick as well and compares the two, field by field (SoldierNPC.GearCacheParity.cs).
	/// </summary>
	public partial class SoldierNPC
	{
		private GearCache _gc;

		internal sealed class GearCache
		{
			/// <summary>
			/// OFF unless LEVY_GEARCACHE=1 (lab): measured in an engaged army it serves a quarter of the ticks and costs more than it saves (docs/LAG_FINDINGS.md, section 7). Not readonly: the harness flips it per fight
			/// for a same-boot A/B.
			/// </summary>
			internal static bool On = LevyProf.Opt && Environment.GetEnvironmentVariable("LEVY_GEARCACHE") == "1" && !ModLoader.HasMod("ThoriumMod");
			internal static long BuildTicks, BuildN;
			internal static long Hits, Fulls, HitTicks, FullTicks, TakeTicks, TakeN, TakeRebuilt, CmpTicks, MidTicks, ApplyTicks;
			internal static readonly Dictionary<string, long> Rebuilds = new(), NodeTicks = new();
			internal static string NodeDump;
			internal static readonly HashSet<string> RngGear = new();
			internal static readonly Dictionary<string, long> Why = new();
			internal static void Note(string why) { Why.TryGetValue(why, out long n); Why[why] = n + 1; }
			internal static void ResetCounters() { BuildTicks = BuildN = 0; Hits = Fulls = HitTicks = FullTicks = TakeTicks = TakeN = TakeRebuilt = CmpTicks = MidTicks = ApplyTicks = 0; Why.Clear(); Rebuilds.Clear(); NodeTicks.Clear(); }

			public readonly StateImage Start = new() { Skip = SkipPath }, Mid = new() { Skip = SkipPath }, Cur = new() { Skip = SkipPath };
			/// <summary>The nodes a cached tick writes back: those the last full build changed, and those holding leaves it rebuilds.</summary>
			private int[] _write;

			/// <summary>State no build reads or writes, left out of the images: the menus and radial bindings, research, other loadouts, saved data, door / torch / tile interaction, draw trails, spawn points.</summary>
			private static readonly string[] SkipPrefixes =
			{
				"P.creativeTracker", "P.DpadRadial", "P.CircularRadial", "P.QuicksRadial", "P.Loadouts", "P.doorHelper", "P.TouchedTiles", "P._blackListedTileCoordsForGrappling", "P._projectilesToInteractWith",
				"P._temporaryItemSlots", "P.<ModSaveErrors>", "P.<NearbyModTorch>", "P.<CurrentSceneEffect>", "P.nearbyTorch", "P.unlitTorchX", "P.unlitTorchY", "P.spX", "P.spY", "P.spN", "P.spI", "P._adjTile", "P._oldAdjTile",
				"P._advancedShadows", "P.shadowPos", "P.shadowRotation", "P.shadowOrigin", "P.shadowDirection", "P.itemFlamePos", "P.speedSlice", "P.hermesStepSound", "P.dye", "P.miscDyes", "P.hideVisibleAccessory", "P.inventoryChestStack",
				"P.mount._debugDraw", "P.mount._defaultDelegatesData", "ModAccessorySlotPlayer.exLoadouts", "UnloadedPlayer.", "CalamityPlayer.DoGCartSegments", "CalamityPlayer.subtitleColors", "CalamityPlayer.SeenDraedonDialogs", "ProgressionPlayer.AmmoObtained",
			};
			private static bool SkipPath(string path)
			{
				foreach (var p in SkipPrefixes) if (path.StartsWith(p, StringComparison.Ordinal)) return true;
				return path.EndsWith(".<StatsModifiedBy>k__BackingField", StringComparison.Ordinal);
			}
			public bool Valid; public long Key; public int BuffLifeRegen; public bool Campfire, Lantern; public string NotValidWhy;
			private readonly List<object> _roots = new(); private readonly List<string> _names = new();
			// the random generator before the build (a build that draws from it is not reused)
			private readonly int[] _rngSeed = new int[56]; private int _rngNext, _rngNextp; public int Spawn0;

			// ------------------------------------------------------------------------------------------------------------
			// Leaves
			// ------------------------------------------------------------------------------------------------------------
			/// <summary>Kept: the current value stays (Levy writes it, or the tail evolves it), and the build must not write it (checked at every full build).</summary>
			private static readonly HashSet<string> Keep = new()
			{
				"P.oldPosition", "P.oldVelocity", // Levy moves the body; the dummy follows it every tick (position and velocity themselves are read by the build: compared)
				"P.lifeRegenCount", // Levy's regeneration clock (SoldierNPC.Healing.cs); lifeRegenTime is read by a debuff's Update: compared
				// the weapon's swing and the body's animation, written by Levy every tick; no build code reads them (GearCacheReadScan)
				"P.itemRotation", "P.compositeFrontArm", "P.compositeBackArm", "P.heldProj", "P.itemTime", "P.controlLeft", "P.controlRight", "P.stealth", "P.stealthTimer", "CalamityPlayer.stealthStrikeThisFrame",
				// Calamity's tail (PostUpdateMiscEffects) evolves these every tick; its build (ResetEffects, UpdateEquips, the items' and buffs' hooks) never reads them
				"CalamityPlayer.TimeHoldingMelee", "CalamityPlayer.TimeHoldingRanged", "CalamityPlayer.TimeHoldingMagic", "CalamityPlayer.TimeHoldingSummon", "CalamityPlayer.TimeHoldingRogue", "CalamityPlayer.TimeHoldingClassless", // PostUpdateEquips
				"CalamityPlayer.rage", "CalamityPlayer.adrenaline", "CalamityPlayer.rageCombatFrames", "CalamityPlayer.adrenalinePauseTimer", "CalamityPlayer.persecutedEnchantSummonTimer", "CalamityPlayer.stealthUIAlpha", "CalamityPlayer.rogueStealth",
			};
			/// <summary>Kept and stepped by <see cref="Step"/>: the build's own per-tick counters.</summary>
			private static readonly HashSet<string> Stepped = new() { "CalamityPlayer.ViridVanguardRotation", "CalamityPlayer.ViridVanguardRotationToAdd", "CalamityPlayer.ViridVanguardActiveCooldown", "CalamityPlayer.ViridVanguardActiveAttackerThisFrame" };
			/// <summary>Not compared: the build overwrites them before anything reads them (the tail changes them every tick: rogue stealth raises the rogue damage and lowers aggro). Restored from the build's image.</summary>
			private static readonly HashSet<string> Rebuilt = new() { "P.aggro", "CalamityPlayer.stealthDamage" };
			private static bool RebuiltNode(string path) => path == "P.damageData" || path.StartsWith("P.damageData.", StringComparison.Ordinal);

			/// <summary>Lab: extra kept leaves (LEVY_GEARCACHE_EXTRAKEEP, comma list) to measure what a leaf costs the hit rate.</summary>
			private static readonly HashSet<string> ExtraKeep = new((Environment.GetEnvironmentVariable("LEVY_GEARCACHE_EXTRAKEEP") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries));
			private static int Classify(string leaf) => Stepped.Contains(leaf) || SkipPath(leaf) ? 3 : Keep.Contains(leaf) || ExtraKeep.Contains(leaf) ? 2 : Rebuilt.Contains(leaf) ? 1 : 0;
			private static int ClassifyNode(string path) => RebuiltNode(path) ? 1 : 0;

			private void Roots(SoldierNPC s, Player d)
			{
				_roots.Clear(); _names.Clear();
				_roots.Add(d); _names.Add("P");
				foreach (var mp in d.ModPlayers) { _roots.Add(mp); _names.Add(mp.GetType().Name); }
				// the worn items' own state (an accessory that keeps a counter in its ModItem); the items themselves are compared by identity through the dummy's slots
				for (int a = 0; a < d.armor.Length; a++) { var mi = d.armor[a]?.ModItem; if (mi != null && !d.armor[a].IsAir) { _roots.Add(mi); _names.Add(ItemName(a)); } }
				var g = s.Record.Gear;
				for (int a = DummyAccessorySlots; a < g.Accessories.Length; a++) { var mi = g.Accessories[a]?.ModItem; if (mi != null && !g.Accessories[a].IsAir) { _roots.Add(mi); _names.Add("X" + a); } }
				if (g.Wings?.ModItem != null && !g.Wings.IsAir) { _roots.Add(g.Wings.ModItem); _names.Add("W"); }
				if (g.Boots?.ModItem != null && !g.Boots.IsAir) { _roots.Add(g.Boots.ModItem); _names.Add("B"); }
			}

			private static readonly string[] _itemNames = new string[32];
			private static string ItemName(int a) => a < _itemNames.Length ? _itemNames[a] ??= "A" + a : "A" + a;

			/// <summary>
			/// Where the build would start: true when every input of the last full build is the same now (the live dummy against its image, the key), so its result can be written back instead. False: the dummy is imaged
			/// (the next build's input) and the build runs.
			/// </summary>
			public bool Begin(SoldierNPC s, Player d)
			{
				long t0 = Stopwatch.GetTimestamp();
				Roots(s, d);
				bool hit = Decide(s, d);
				long t1 = Stopwatch.GetTimestamp(); CmpTicks += t1 - t0; TakeN++;
				if (hit) return true;
				bool kept = Cur.Take(_roots, _names);
				if (!kept)
				{
					Cur.Mask(Classify, ClassifyNode); // a recaptured shape keeps its marks
					if (Lab && NodeDump == null) NodeDump = string.Join(" ", System.Linq.Enumerable.Select(Cur.NodeList(), x => x.path + ":" + x.leaves));
					TakeRebuilt++; if (Lab && Cur.FailAt != null) { string k = Cur.FailAt.Path; Rebuilds.TryGetValue(k, out long q); Rebuilds[k] = q + 1; }
				}
				TakeTicks += Stopwatch.GetTimestamp() - t1;
				return false;
			}

			private static int _rageBuff = -2, _adrBuff = -2;
			/// <summary>Rage Mode / Adrenaline Mode on the dummy: their buffs' Update (part of the build) reads the bars the tail drains every tick.</summary>
			private static bool ModeBuff(Player d)
			{
				if (_rageBuff == -2) { _rageBuff = ModContent.TryFind<ModBuff>("CalamityMod/RageMode", out var rb) ? rb.Type : -1; _adrBuff = ModContent.TryFind<ModBuff>("CalamityMod/AdrenalineMode", out var ab) ? ab.Type : -1; }
				if (_rageBuff < 0 && _adrBuff < 0) return false;
				for (int b = 0; b < d.buffType.Length; b++) { int t = d.buffType[b]; if (t > 0 && (t == _rageBuff || t == _adrBuff)) return true; }
				return false;
			}

			private bool Decide(SoldierNPC s, Player d)
			{
				if (!Valid) { Note(NotValidWhy ?? "first"); return false; }
				if (ModeBuff(d)) { Note("rage mode"); return false; }
				long key = KeyOf(s, d);
				if (key != Key) { Note("key"); return false; }
				if (ViridBladeAlive()) { Note("virid blade"); return false; }
				int at = Start.FirstDiffLive(_roots, out int slot);
				if (at == -2) { Note("shape"); return false; }
				if (at >= 0) { if (Lab) Note("diff " + Start.LeafName(at, slot)); else Note("diff"); return false; }
				return true;
			}

			private string KeptName(int node)
			{
				var x = Start.Order[node]; var y = Mid.Order[node]; x.Names();
				if (x.CV != null) foreach (int k in x.CV) if (x.V[k] != y.V[k]) return x.LeafV[k];
				if (x.CRaw != null) foreach (int k in x.CRaw) if (!x.Raw.AsSpan(x.P.RawBuf[k], x.P.RawSize[k]).SequenceEqual(y.Raw.AsSpan(x.P.RawBuf[k], x.P.RawSize[k]))) return x.LeafRaw[k];
				return x.Path;
			}

			/// <summary>Lab: name the leaf that sent a levy to the full build (costs a string per miss).</summary>
			internal static bool Lab = Environment.GetEnvironmentVariable("LEVY_HARNESS") == "1";

			/// <summary>Cached tick: the build's result written back, the kept leaves left as they are, the build's own counters stepped.</summary>
			public void Apply(SoldierNPC s, Player d)
			{
				long t0 = Stopwatch.GetTimestamp();
				try { ApplyInner(s, d); } finally { ApplyTicks += Stopwatch.GetTimestamp() - t0; }
			}

			private void ApplyInner(SoldierNPC s, Player d)
			{
				Mid.RestoreInto(Start.Live, _write);
				Step(s, d);
				if (Campfire) Main.SceneMetrics.HasCampfire = true;
				if (Lantern) Main.SceneMetrics.HasHeartLantern = true;
				s._buffLifeRegen = BuffLifeRegen;
			}

			public void BeforeBuild()
			{
				Valid = false; NotValidWhy = "build failed"; // set again once the build has run (a build that throws leaves no result to reuse)
				var r = Main.rand; RngState(r, _rngSeed, out _rngNext, out _rngNextp);
				Spawn0 = GearUpkeep.Spawned.Count;
			}

			/// <summary>Full build done (the split point): the inputs it ran on become the reference, its result is imaged, and it is checked for what a cached tick could not repeat.</summary>
			public void AfterBuild(SoldierNPC s, Player d, bool campfire0, bool lantern0)
			{
				// the image taken in Begin is this build's input
				Start.Swap(Cur); // Cur now holds the previous reference (marked when it was captured)
				Key = KeyOf(s, d);
				long t0 = Stopwatch.GetTimestamp();
				if (!Mid.Take(_roots, _names)) Mid.Mask(Classify, ClassifyNode);
				MidTicks += Stopwatch.GetTimestamp() - t0;
				Valid = false;
				if (!Start.Aligned(Mid)) { NotValidWhy = "build shape"; return; }
				if (!Start.SameOnKept(Mid, out int at)) { NotValidWhy = Lab ? "build writes " + KeptName(at) : "build writes kept"; return; }
				var r = Main.rand; int[] seed = Scratch56; RngState(r, seed, out int nx, out int np);
				if (nx != _rngNext || np != _rngNextp || !seed.AsSpan().SequenceEqual(_rngSeed)) { NotValidWhy = "build draws random"; if (Lab && RngGear.Count < 12) RngGear.Add(s.GearSummary()); return; }
				if (GearUpkeep.Spawned.Count != Spawn0) { NotValidWhy = "build spawns"; return; }
				_write = Start.NodesToWrite(Mid);
				Campfire = Main.SceneMetrics.HasCampfire && !campfire0; Lantern = Main.SceneMetrics.HasHeartLantern && !lantern0;
				BuffLifeRegen = s._buffLifeRegen;
				Valid = true; NotValidWhy = null;
			}

			// ------------------------------------------------------------------------------------------------------------
			// What the build reads outside the dummy
			// ------------------------------------------------------------------------------------------------------------
			private static FieldInfo _bossesField; private static bool _bossesLooked;
			private static bool CalBosses()
			{
				if (!_bossesLooked) { _bossesLooked = true; try { _bossesField = ModLoader.TryGetMod("CalamityMod", out Mod cal) ? cal.Code?.GetType("CalamityMod.CalPlayer.CalamityPlayer")?.GetField("areThereAnyDamnBosses", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) : null; } catch { } }
				try { return _bossesField != null && (bool)_bossesField.GetValue(null); } catch { return false; }
			}

			private static long KeyOf(SoldierNPC s, Player d)
			{
				var h = new HashCode();
				var sm = Main.SceneMetrics;
				h.Add(Main.dayTime); h.Add(Main.raining); h.Add(Main.bloodMoon); h.Add(Main.eclipse); h.Add(Main.pumpkinMoon); h.Add(Main.snowMoon); h.Add(Main.hardMode); h.Add(Main.GameMode);
				h.Add(sm.HasCampfire); h.Add(sm.HasHeartLantern); h.Add(sm.HasSunflower); h.Add(sm.HasStarInBottle); h.Add(sm.HasCatBast); h.Add(sm.HasGardenGnome); h.Add(sm.HasClock);
				h.Add(CalBosses());
				h.Add((int)s.Stations); h.Add(s._setKind); h.Add(s._setOrbs); h.Add(s._nebulaLevel[0]); h.Add(s._nebulaLevel[1]); h.Add(s._nebulaLevel[2]);
				h.Add(s.UsingSidearm); h.Add(s._defDamageLive); h.Add(GearUpkeep.Mode); h.Add(s.WearsChlorophyteSet); h.Add(s.WearsStardustSet); h.Add(ExtraBuffStats != null);
				var n = s.NPC;
				for (int i = 0; i < NPC.maxBuffs; i++) h.Add(n.buffType[i] > 0 && n.buffTime[i] > 0 ? n.buffType[i] : 0); // the potion pass reads which buffs the body carries
				return h.ToHashCode();
			}

			// ------------------------------------------------------------------------------------------------------------
			// The build's own per-tick counters, stepped by the statements Calamity's ResetEffects runs
			// ------------------------------------------------------------------------------------------------------------
			private static bool _vvLooked; private static Func<ModPlayer, float> _vvRotGet, _vvAddGet; private static Action<ModPlayer, float> _vvRotSet, _vvAddSet;
			private static Func<ModPlayer, float> _vvCdGet; private static Action<ModPlayer, float> _vvCdSet; private static Action<ModPlayer, bool> _vvAtkSet;
			private static Type _calPlayer; private static int _vvItem = -1, _vvBlade = -1; private static float _vvIdle; private static int _vvCdMax;
			private static uint _vvTick = uint.MaxValue; private static bool _vvAlive;
			private ModPlayer _cp;

			private static void LookVirid()
			{
				if (_vvLooked) return; _vvLooked = true;
				try
				{
					if (!ModLoader.TryGetMod("CalamityMod", out Mod cal) || cal.Code == null) return;
					_calPlayer = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
					var vv = cal.Code.GetType("CalamityMod.Items.Weapons.Summon.ViridVanguard") ?? FindType(cal.Code, "ViridVanguard");
					if (_calPlayer == null || vv == null) return;
					_vvIdle = (float)vv.GetProperty("IdleCirclingSpeed", BindingFlags.Public | BindingFlags.Static).GetValue(null);
					_vvCdMax = (int)vv.GetProperty("ActiveAttackCooldown", BindingFlags.Public | BindingFlags.Static).GetValue(null);
					_vvItem = cal.TryFind<ModItem>("ViridVanguard", out var it) ? it.Type : -1;
					_vvBlade = cal.TryFind<ModProjectile>("ViridVanguardBlade", out var bl) ? bl.Type : -1;
					_vvRotGet = Getter<float>("ViridVanguardRotation"); _vvRotSet = Setter<float>("ViridVanguardRotation");
					_vvAddGet = Getter<float>("ViridVanguardRotationToAdd"); _vvAddSet = Setter<float>("ViridVanguardRotationToAdd");
					_vvCdGet = Getter<float>("ViridVanguardActiveCooldown"); _vvCdSet = Setter<float>("ViridVanguardActiveCooldown");
					_vvAtkSet = Setter<bool>("ViridVanguardActiveAttackerThisFrame");
				}
				catch (Exception e) { Levy.Instance?.Logger.Warn("Levy: gear cache could not read Calamity's Virid Vanguard counters: " + e.Message); _calPlayer = null; }
			}

			private static Type FindType(Assembly a, string name) { try { foreach (var t in a.GetTypes()) if (t.Name == name && typeof(ModItem).IsAssignableFrom(t)) return t; } catch { } return null; }

			private static Func<ModPlayer, T> Getter<T>(string f)
			{
				var fi = _calPlayer.GetField(f, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var p = System.Linq.Expressions.Expression.Parameter(typeof(ModPlayer));
				return System.Linq.Expressions.Expression.Lambda<Func<ModPlayer, T>>(System.Linq.Expressions.Expression.Field(System.Linq.Expressions.Expression.Convert(p, _calPlayer), fi), p).Compile();
			}

			private static Action<ModPlayer, T> Setter<T>(string f)
			{
				var fi = _calPlayer.GetField(f, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var p = System.Linq.Expressions.Expression.Parameter(typeof(ModPlayer)); var v = System.Linq.Expressions.Expression.Parameter(typeof(T));
				return System.Linq.Expressions.Expression.Lambda<Action<ModPlayer, T>>(System.Linq.Expressions.Expression.Assign(System.Linq.Expressions.Expression.Field(System.Linq.Expressions.Expression.Convert(p, _calPlayer), fi), v), p, v).Compile();
			}

			/// <summary>A Virid Vanguard blade in the world: Calamity's ResetEffects asks every player whether one of theirs is attacking, which a cached tick does not repeat. Once a tick.</summary>
			private static bool ViridBladeAlive()
			{
				LookVirid();
				if (_vvBlade < 0) return false;
				uint now = Main.GameUpdateCount;
				if (_vvTick != now) { _vvTick = now; _vvAlive = false; for (int i = 0; i < Main.maxProjectiles; i++) { var p = Main.projectile[i]; if (p.active && p.type == _vvBlade) { _vvAlive = true; break; } } }
				return _vvAlive;
			}

			/// <summary>Calamity's ResetEffects, its three per-tick lines for the Virid Vanguard, verbatim (no blade alive: none of the dummy's is attacking).</summary>
			private void Step(SoldierNPC s, Player d)
			{
				LookVirid();
				if (_calPlayer == null) return;
				if (_cp == null) foreach (var mp in d.ModPlayers) if (mp.GetType() == _calPlayer) { _cp = mp; break; }
				if (_cp == null) return;
				_vvAtkSet(_cp, false);
				_vvRotSet(_cp, MathHelper.WrapAngle(_vvRotGet(_cp) + _vvAddGet(_cp)));
				_vvAddSet(_cp, _vvIdle);
				float cd = _vvCdGet(_cp); // a float in Calamity (decremented and incremented by one)
				if (d.HeldItem.type == _vvItem) { if (cd > 0) { cd--; _vvCdSet(_cp, cd); } }
				else if (cd >= 0 && cd < _vvCdMax) { cd++; _vvCdSet(_cp, cd); }
			}

			// ------------------------------------------------------------------------------------------------------------
			// The random generator's state (UnifiedRandom: SeedArray, inext, inextp)
			// ------------------------------------------------------------------------------------------------------------
			private static readonly int[] Scratch56 = new int[56];
			private static FieldInfo _rs, _rn, _rp;
			internal static void RngState(Terraria.Utilities.UnifiedRandom r, int[] seed, out int next, out int nextp)
			{
				if (_rs == null)
				{
					var t = typeof(Terraria.Utilities.UnifiedRandom);
					_rs = t.GetField("SeedArray", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); _rn = t.GetField("inext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); _rp = t.GetField("inextp", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				}
				var a = (int[])_rs.GetValue(r); Array.Copy(a, seed, Math.Min(a.Length, seed.Length));
				next = (int)_rn.GetValue(r); nextp = (int)_rp.GetValue(r);
			}
		}

		/// <summary>The gear cache may serve this levy's build: on, Calamity's or another gear mod's frame in use, not an officer.</summary>
		private bool GearCacheEligible => GearCache.On && !OfficerMode && Record != null && !Hostile;
	}
}
