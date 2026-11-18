using System;
using System.Collections.Generic;
using System.Diagnostics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// APPROXIMATE GEAR FRAME for fighting levies ("Gear update interval when fighting", Levy: Performance).
	/// <para/>The gear frame (SoldierNPC.GearUpkeep.cs) is a BUILD (ResetEffects of the game and every mod, the buff pass, UpdateEquips, the armor sets, the station and potion stats: it wipes the stats and rebuilds them) and a
	/// TAIL (PostUpdateEquips, PostUpdateMiscEffects: Rage, Adrenaline, rogue stealth, cooldowns, timers). With an interval N above 1, a fighting levy runs the build once every N ticks (staggered across the army by body
	/// index) and on the ticks between the leaves that build wrote are put back to what it left (the last build's stats), then the tail runs as every tick. The stats a levy fights with are therefore always exactly the
	/// stats of a full build of at most N-1 ticks ago: never more, never less. What lags is how fast the stats follow the body's motion (motion-reactive accessories) and anything else the build reads that is not
	/// an input checked here.
	/// <para/>A full build runs at once, whatever the interval, when an input changes: worn gear, held weapon, the dummy's buffs (added, removed or about to run out), the body's buffs (potions, stations, banners),
	/// a 5% step of life or a 10% step of mana, the set state, the world flags the build reads (day, events, bosses alive), and on every stat refresh (once a second and whenever the stats are marked dirty).
	/// <para/>What the build writes is learned per levy: the dummy (Player, every ModPlayer, the worn ModItems) is imaged either side of its first few builds (StateImage) and every leaf that differs is marked; only
	/// those are written back. Leaves other code moves every tick are never written back (the body's motion and vitals, the buff timers, which a served tick counts down itself, the tail's meters). A levy whose build spawns
	/// a projectile (a set bonus that casts) or draws random numbers keeps the exact frame. Calamity's Virid Vanguard counters, which its ResetEffects steps every tick, are stepped by the same statements.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>The default of "Gear update interval when fighting" (chosen by measurement: docs/LAG_FINDINGS.md, section 8).</summary>
		public const int GearApproxDefaultInterval = 4;

		private GearApprox _ga;
		/// <summary>Checks: how the last gear frame went (0 none / exact frame, 2 cache build, 3 served by the approximate frame, 4 built with the approximate frame on).</summary>
		internal int LastFrameKind;
		internal bool GearApproxReady => _ga != null && _ga.Ready;
		/// <summary>Checks: one gear frame now, as a fighting levy's tick runs it.</summary>
		internal void TestGearFrame() { _gearDormant = false; GearUpkeep.PrepareCountsOncePerTick(); GearFrame(); }
		/// <summary>The next frame is a stat refresh: the build runs in full (RefreshStats sets it).</summary>
		private bool _gearFullNext;
		/// <summary>This tick's frame belongs to a dormant levy (calm and far from every enemy): it builds in full.</summary>
		private bool _gearDormant;

		internal sealed class GearApprox
		{
			// ------------------------------------------------------------------------------------------------------------
			// Settings and lab counters
			// ------------------------------------------------------------------------------------------------------------
			private static readonly int EnvInterval = int.TryParse(Environment.GetEnvironmentVariable("LEVY_GEARINTERVAL"), out int ev) ? ev : 0;
			/// <summary>Harness override (token gi=N), 0 = none.</summary>
			internal static int HarnessInterval;
			/// <summary>The interval in force: harness, then LEVY_GEARINTERVAL, then the config.</summary>
			internal static int Interval
			{
				get
				{
					if (HarnessInterval > 0) return HarnessInterval;
					if (EnvInterval > 0) return EnvInterval;
					try { var c = Common.Perf.PerfConfig.Get; return c != null ? Math.Clamp(c.GearUpdateIntervalWhenFighting, 1, 8) : GearApproxDefaultInterval; } catch { return GearApproxDefaultInterval; }
				}
			}
			/// <summary>LEVY_GA_RNGEXEMPT=1 (lab): a build that draws random numbers keeps the levy on the exact frame. Off: such a build is approximated like any other (its draws pick cosmetic variants: the Yoyo Bag's counterweight, set dust); one that casts something is caught by the spawn check.</summary>
			/// <summary>Lab: every served tick also runs the full build and compares (<see cref="GearApproxProbeTick"/>).</summary>
			internal static bool Probe = Environment.GetEnvironmentVariable("LEVY_GEARAPPROX_PROBE") == "1";
			/// <summary>Checks: the probe compares only these bodies (null: every levy).</summary>
			internal static HashSet<int> ProbeOnly;
			internal static bool RngExempt = Environment.GetEnvironmentVariable("LEVY_GA_RNGEXEMPT") == "1";
			private const int LearnBuilds = 4;
			/// <summary>Every how many full builds the learning images are taken (a leaf the tail starts moving later is caught at the next learning build); in between only the written-back leaves are imaged.</summary>
			internal static readonly int LearnEveryDefault = int.TryParse(Environment.GetEnvironmentVariable("LEVY_GA_LEARNEVERY"), out int le) && le > 0 ? le : 8;
			internal static int LearnEvery = LearnEveryDefault;
			internal static long ServedFrameTicks, FullFrameTicks;
			internal static long Served, Builds, Forced, LearnTicks, PostTicks, ApplyTicks, ShapeResets, ExemptSpawn, ExemptRng, ExemptCounter, Masks, EligibleBuilds, BeforeTicks, AfterTicks, MaskTicks;
			internal static readonly Dictionary<string, long> Why = new();
			internal static readonly HashSet<string> RngGear = new(), SpawnGear = new(), UpGear = new();
			internal static void Note(string why) { Why.TryGetValue(why, out long n); Why[why] = n + 1; }
			internal static void ResetCounters() { ServedFrameTicks = FullFrameTicks = 0; Served = Builds = Forced = LearnTicks = PostTicks = ApplyTicks = ShapeResets = ExemptSpawn = ExemptRng = ExemptCounter = Masks = EligibleBuilds = BeforeTicks = AfterTicks = MaskTicks = 0; Why.Clear(); RngGear.Clear(); SpawnGear.Clear(); UpGear.Clear(); }
			/// <summary>Gear signatures whose build was seen to spawn or draw random numbers: a levy in the same gear starts on the exact frame.</summary>
			private static readonly HashSet<long> ExactSigs = new();

			// ------------------------------------------------------------------------------------------------------------
			// Per levy
			// ------------------------------------------------------------------------------------------------------------
			private readonly StateImage _pre = new() { Skip = GearCache.SkipPath }, _post = new() { Skip = GearCache.SkipPath };
			private readonly List<object> _roots = new(); private readonly List<string> _names = new();
			private int[] _write, _writeSet; private object[] _live;
			private bool _ready, _learnThis, _exempt, _havePost, _sigSet; private int _learned, _sinceLearn, _hot;
			private readonly Dictionary<string, int> _upRuns = new(); private readonly List<string> _upKeys = new();
			private List<StateImage.Cool> _cool = new(); private readonly List<string> _up = new();
			private long _key, _sig; private int _buffLifeRegen; private bool _campfire, _lantern;
			private readonly int[] _rngSeed = new int[56]; private int _rngNext, _rngNextp, _spawn0;
			private ModPlayer _cp;

			/// <summary>Never written back: state other code moves every tick (Levy moves the body, takes hits, counts the buffs down; the tail evolves its meters; the frame steps its own timers before the build).</summary>
			private static readonly HashSet<string> KeepLeaves = new()
			{
				"P.position", "P.velocity", "P.direction", "P.oldDirection", "P.statLife", "P.statMana", "P.buffType", "P.buffTime", "P.wingTime", "P.rocketTime", "P.lifeRegenTime", "P.lifeRegenCount", "P.miscCounter",
				"P.ownedProjectileCounts", "P.numMinions", "P.slotsMinions", "P.highestStormTigerGemOriginalDamage", "P.highestAbigailCounterOriginalDamage", "P.petalTimer", "P.shadowDodgeTimer", "P.boneGloveTimer",
				"P.crystalLeafCooldown", "P.itemAnimation", "P.itemAnimationMax", "P.itemTime", "P.immune", "P.immuneTime", "P.immuneNoBlink", "P.hurtCooldowns", "P.controlJump", "P.selectedItem", "P.beetleOrbs", "P.solarShields",
				"P.whoAmI", "P.head", "P.body", "P.legs", "P.dashDelay", "P.timeSinceLastDashStarted", "P.jump", "P.fallStart", "P.fallStart2", "P.gravDir", "P.mount",
			};
			/// <summary>Reset by Calamity's ResetEffects and set by a shot: written back like any other leaf the build writes (the exact cache keeps it because its build never reads it).</summary>
			/// <summary>
			/// Stats the game's ResetEffects sets afresh on every build, written back on a served tick even before the learning has seen the build change them: the tail adds to some of them only now and then (Calamity's life
			/// regeneration, its damage bonuses), and a leaf the tail adds to that is not written back would add up over the ticks between builds.
			/// </summary>
			private static readonly HashSet<string> AlwaysLeaves = new()
			{
				"P.lifeRegen", "P.lifeRegenBuff", "P.statDefense", "P.endurance", "P.moveSpeed", "P.maxRunSpeed", "P.runAcceleration", "P.runSlowdown", "P.accRunSpeed", "P.jumpSpeedBoost", "P.statLifeMax2", "P.statManaMax2",
				"P.manaCost", "P.manaRegenBonus", "P.manaRegenDelayBonus", "P.maxMinions", "P.maxTurrets", "P.aggro", "P.noKnockback", "P.damageData", "P.armorPenetration", "P.wingTimeMax", "P.lifeSteal", "P.thorns", "P.pickSpeed",
			};
			private static bool Always(string leaf) => AlwaysLeaves.Contains(leaf);

			/// <summary>Counted up by the build but only cosmetic: the Yoyo Bag's counterweight type is drawn at random every build.</summary>
			private static readonly HashSet<string> CosmeticCounters = new() { "P.counterWeight", "P.beetleFrame", "P.beetleFrameCounter" };
			private static readonly HashSet<string> NotKept = new() { "CalamityPlayer.stealthStrikeThisFrame" };
			private static bool Keep(string leaf) => KeepLeaves.Contains(leaf) || (GearCache.Keep.Contains(leaf) && !NotKept.Contains(leaf)) || GearCache.Stepped.Contains(leaf) || GearCache.SkipPath(leaf);

			private void Roots(SoldierNPC s, Player d)
			{
				_roots.Clear(); _names.Clear();
				_roots.Add(d); _names.Add("P");
				foreach (var mp in d.ModPlayers) { _roots.Add(mp); _names.Add(mp.GetType().Name); }
				for (int a = 0; a < d.armor.Length; a++) { var mi = d.armor[a]?.ModItem; if (mi != null && !d.armor[a].IsAir) { _roots.Add(mi); _names.Add("A" + a); } }
				var g = s.Record.Gear;
				for (int a = DummyAccessorySlots; a < g.Accessories.Length; a++) { var mi = g.Accessories[a]?.ModItem; if (mi != null && !g.Accessories[a].IsAir) { _roots.Add(mi); _names.Add("X" + a); } }
				if (g.Wings?.ModItem != null && !g.Wings.IsAir) { _roots.Add(g.Wings.ModItem); _names.Add("W"); }
				if (g.Boots?.ModItem != null && !g.Boots.IsAir) { _roots.Add(g.Boots.ModItem); _names.Add("B"); }
			}

			/// <summary>The worn gear (dummy armor and accessory slots, the extra accessories, wings, boots): a change starts the learning again.</summary>
			private static long GearSig(SoldierNPC s, Player d)
			{
				var h = new HashCode();
				for (int a = 0; a < d.armor.Length; a++) { var it = d.armor[a]; h.Add(it == null || it.IsAir ? 0 : it.type); }
				var g = s.Record.Gear;
				for (int a = 0; a < g.Accessories.Length; a++) { var it = g.Accessories[a]; h.Add(it == null || it.IsAir ? 0 : it.type); }
				h.Add(g.Wings == null || g.Wings.IsAir ? 0 : g.Wings.type); h.Add(g.Boots == null || g.Boots.IsAir ? 0 : g.Boots.type);
				return h.ToHashCode();
			}

			private static uint _wkTick = uint.MaxValue; private static int _wk;
			/// <summary>The world flags and scene the build reads (time, weather, events, hardmode, difficulty, nearby furniture, Calamity's boss flag), once a tick.</summary>
			private static int WorldKey()
			{
				if (_wkTick == Main.GameUpdateCount) return _wk;
				_wkTick = Main.GameUpdateCount;
				var h = new HashCode(); var sm = Main.SceneMetrics;
				h.Add(Main.dayTime); h.Add(Main.raining); h.Add(Main.bloodMoon); h.Add(Main.eclipse); h.Add(Main.pumpkinMoon); h.Add(Main.snowMoon); h.Add(Main.hardMode); h.Add(Main.GameMode);
				h.Add(sm.HasCampfire); h.Add(sm.HasHeartLantern); h.Add(sm.HasSunflower); h.Add(sm.HasStarInBottle); h.Add(sm.HasCatBast); h.Add(sm.HasGardenGnome); h.Add(sm.HasClock);
				h.Add(GearCache.CalBosses());
				return _wk = h.ToHashCode();
			}

			/// <summary>Everything a served tick must match the last build on (taken where the build starts).</summary>
			private static long KeyOf(SoldierNPC s, Player d)
			{
				var h = new HashCode();
				h.Add(WorldKey()); // what the build reads of the world (the same for every levy this tick)
				h.Add((int)s.Stations); h.Add(s._setKind); h.Add(s._setOrbs); h.Add(s._nebulaLevel[0]); h.Add(s._nebulaLevel[1]); h.Add(s._nebulaLevel[2]);
				h.Add(s.UsingSidearm); h.Add(s._defDamageLive); h.Add(GearUpkeep.Mode); h.Add(s.WearsChlorophyteSet); h.Add(s.WearsStardustSet); h.Add(ExtraBuffStats != null);
				var body = s.NPC;
				for (int i = 0; i < NPC.maxBuffs; i++) if (body.buffType[i] > 0 && body.buffTime[i] > 0) h.Add(body.buffType[i]); // the potion pass reads which buffs the body carries
				for (int b = 0; b < d.buffType.Length; b++) { int t = d.buffType[b]; if (t > 0 && d.buffTime[b] > 0) h.Add(t); }
				h.Add(d.selectedItem); var held = d.inventory[d.selectedItem]; h.Add(held == null || held.IsAir ? 0 : held.type);
				var n = s.NPC;
				h.Add(n.lifeMax <= 0 ? 0 : n.life >= n.lifeMax ? 21 : (int)((long)n.life * 20 / n.lifeMax));
				h.Add(d.statManaMax2 <= 0 ? 0 : d.statMana >= d.statManaMax2 ? 11 : d.statMana * 10 / d.statManaMax2);
				h.Add(d.mount.Active); h.Add(s.Hostile); h.Add(s.MindControlled);
				return h.ToHashCode();
			}

			// ------------------------------------------------------------------------------------------------------------
			// A served tick
			// ------------------------------------------------------------------------------------------------------------
			/// <summary>Where the build would start: true when the last build's stats were written back instead (and the buff timers counted down), false when the build must run.</summary>
			public bool TryServe(SoldierNPC s, Player d, int interval)
			{
				if (_exempt) { Note("exact gear"); return false; }
				if (!_ready) { Note("learning"); return false; }
				if ((int)((Main.GameUpdateCount + (uint)s.NPC.whoAmI) % (uint)interval) == 0) { Note("due"); return false; }
				if (GearCache.ViridItem > 0 && d.HeldItem.type == GearCache.ViridItem && GearCache.ViridBladeAlive()) { Note("virid"); return false; }
				long key = KeyOf(s, d);
				if (key != _key) { Note("input"); Forced++; return false; }
				// the buff pass counts every buff down once a tick: so does a served tick; one about to run out is a change of input (the build that removes it runs)
				for (int b = 0; b < d.buffType.Length; b++)
				{
					int t = d.buffType[b];
					if (t > 0 && d.buffTime[b] > 0 && !BuffID.Sets.TimeLeftDoesNotDecrease[t] && d.buffTime[b] <= 1) { Note("buff ends"); Forced++; return false; }
				}
				long t0 = Stopwatch.GetTimestamp();
				for (int b = 0; b < d.buffType.Length; b++) { int t = d.buffType[b]; if (t > 0 && d.buffTime[b] > 0 && !BuffID.Sets.TimeLeftDoesNotDecrease[t]) d.buffTime[b]--; }
				_post.RestoreInto(_live, _write);
				if (_cool.Count > 0) StateImage.StepCooldowns(_cool);
				GearCache.StepVirid(ref _cp, d);
				if (_campfire) Main.SceneMetrics.HasCampfire = true;
				if (_lantern) Main.SceneMetrics.HasHeartLantern = true;
				s._buffLifeRegen = _buffLifeRegen;
				ApplyTicks += Stopwatch.GetTimestamp() - t0;
				Served++;
				return true;
			}

			// ------------------------------------------------------------------------------------------------------------
			// A full build
			// ------------------------------------------------------------------------------------------------------------
			public void BeforeBuild(SoldierNPC s, Player d)
			{
				Builds++;
				if (_exempt && !s._gearFullNext) return; // exact gear: nothing to do until a stat refresh (which every change of gear goes through) shows other gear
				long b0 = Stopwatch.GetTimestamp();
				if (s._gearFullNext || !_sigSet) // every change of gear comes through a stat refresh
				{
					long sig = GearSig(s, d); _sigSet = true;
					if (sig != _sig) { _sig = sig; _ready = false; _learned = 0; _hot = 0; _exempt = ExactSigs.Contains(sig); }
					if (_exempt) return;
				}
				EligibleBuilds++;
				_key = KeyOf(s, d);
				_learnThis = _learned < LearnBuilds || _hot > 0 || ++_sinceLearn >= LearnEvery;
				Roots(s, d);
				if (_learnThis) { _sinceLearn = 0; if (_hot > 0) _hot--; _pre.Take(_roots, _names); }
				if (RngExempt) GearCache.RngState(Main.rand, _rngSeed, out _rngNext, out _rngNextp);
				_spawn0 = GearUpkeep.Spawned.Count;
				BeforeTicks += Stopwatch.GetTimestamp() - b0;
			}

			public void AfterBuild(SoldierNPC s, Player d, bool campfire0, bool lantern0)
			{
				if (_exempt) return;
				long a0 = Stopwatch.GetTimestamp();
				try { AfterBuildInner(s, d, campfire0, lantern0); } finally { AfterTicks += Stopwatch.GetTimestamp() - a0; }
			}

			private void AfterBuildInner(SoldierNPC s, Player d, bool campfire0, bool lantern0)
			{
				// a build that cast something, or drew random numbers, is not one whose result can stand for the next ticks
				if (SpawnedReal(_spawn0)) { Exempt(s, SpawnGear, null); ExemptSpawn++; return; }
				if (RngExempt)
				{
					var seed = Scratch; GearCache.RngState(Main.rand, seed, out int nx, out int np);
					if (nx != _rngNext || np != _rngNextp || !seed.AsSpan().SequenceEqual(_rngSeed)) { Exempt(s, RngGear, null); ExemptRng++; return; }
				}
				long t0 = Stopwatch.GetTimestamp();
				// the last build's image against where this one started: the fields nothing outside the build touched in between (a counter the build steps shows there)
				if (!_learnThis && _ready)
				{
					// between learning builds the image is taken again without learning: Recapture follows an object the build made anew (ResetEffects re-creates some of its lists and arrays) so a served tick writes into the live one
					// only the written-back nodes and the path to them are imaged; a list or array the build made anew is followed to the new object
					if (_post.RecaptureSet(_writeSet)) _post.LiveObjects(_live); else if (_post.Take(_roots, _names)) _post.LiveObjects(_live); else { _ready = false; _learned = 0; ShapeResets++; }
					PostTicks += Stopwatch.GetTimestamp() - t0;
					_campfire = Main.SceneMetrics.HasCampfire && !campfire0; _lantern = Main.SceneMetrics.HasHeartLantern && !lantern0;
					_buffLifeRegen = s._buffLifeRegen;
					return;
				}
				bool prevAligned = _learnThis && _havePost && _post.Aligned(_pre);
				if (prevAligned) _post.MarkUntouched(_pre);
				bool kept = _post.Take(_roots, _names); _havePost = true;
				if (!kept) { _ready = false; _learned = 0; prevAligned = false; ShapeResets++; } // a fresh capture: the learned leaves went with the old shape
				bool remask = false;
				if (_learnThis)
				{
					long t1 = Stopwatch.GetTimestamp();
					if (_pre.Aligned(_post))
					{
						remask |= _post.LearnWrites(_pre); // every leaf this build changed: written back on the ticks between builds
						if (prevAligned)
						{
							_up.Clear(); remask |= _post.DetectCounters(_pre, _up); // cooldowns the build counts down: a served tick counts them down too
							foreach (var u in _up) if (!Keep(u)) { _upRuns.TryGetValue(u, out int r); _upRuns[u] = ++r; if (r >= 1 && !CosmeticCounters.Contains(u)) { Exempt(s, UpGear, u); ExemptCounter++; return; } } // a counter the build counts up to a threshold (up by one on two learning builds running): exact frame
							foreach (var k in _upKeys.Count == 0 ? (IEnumerable<string>)Array.Empty<string>() : _upKeys) if (!_up.Contains(k)) _upRuns[k] = 0;
							_upKeys.Clear(); _upKeys.AddRange(_up);
						}
						_learned++;
						if (remask && _ready) _hot = 4; // something new was learned: the next few builds learn too
						if (_learned >= LearnBuilds && (remask || !_ready)) { long m0 = Stopwatch.GetTimestamp(); _write = _post.WriteMask(Keep, out _live, Always); _writeSet = _post.WithAncestors(_write); _cool = _post.Cooldowns(); _ready = true; Masks++; MaskTicks += Stopwatch.GetTimestamp() - m0; }
					}
					else { _learned = 0; _ready = false; }
					LearnTicks += Stopwatch.GetTimestamp() - t1;
				}
				if (_ready) { _post.LiveObjects(_live); if (_cool.Count > 0) _cool = _post.Cooldowns(); }
				PostTicks += Stopwatch.GetTimestamp() - t0;
				_campfire = Main.SceneMetrics.HasCampfire && !campfire0; _lantern = Main.SceneMetrics.HasHeartLantern && !lantern0;
				_buffLifeRegen = s._buffLifeRegen;
			}

			private void Exempt(SoldierNPC s, HashSet<string> gear, string why)
			{
				_exempt = true; _ready = false; ExactSigs.Add(_sig);
				if (gear.Count < 16) gear.Add((why != null ? why + " with " : "") + s.GearSummary());
			}

			private static readonly int[] Scratch = new int[56];

			/// <summary>The build spawned a projectile that stays (not one a hand-made twin covers or an owner-bound helper that is dropped anyway).</summary>
			internal static bool SpawnedReal(int from)
			{
				var list = GearUpkeep.Spawned;
				for (int k = from; k < list.Count; k++)
				{
					int idx = list[k]; if (idx < 0 || idx >= Main.maxProjectiles) continue;
					var p = Main.projectile[idx];
					if (p.active && !GearUpkeep.CoveredByHandMade(p) && !GearUpkeep.BoundNow(p.type)) return true;
				}
				return false;
			}

			/// <summary>Lab: the learned leaves (names).</summary>
			internal IEnumerable<string> Written() => _post.WrittenNames();
			internal bool Ready => _ready;
		}

		// ------------------------------------------------------------------------------------------------------------
		// DRIFT PROBE (LEVY_GEARAPPROX_PROBE=1, lab): on every tick the approximate frame serves, the full build also runs from the same state and the two are compared, stat by stat and leaf by leaf. The served
		// result is the one that stays (the fight is the approximate fight). A spawn the full build would have made is counted (a cast the served tick missed) and removed.
		// ------------------------------------------------------------------------------------------------------------
		internal static class GearApproxProbe
		{
			internal static readonly string[] StatNames = { "defense", "endurance", "moveSpeed", "lifeRegen", "wingTimeMax", "maxRunSpeed", "runAcceleration", "jumpSpeedBoost", "aggro", "maxMinions", "lifeMax2", "manaMax2", "manaCost",
				"dmg.generic", "dmg.melee", "dmg.ranged", "dmg.magic", "dmg.summon", "dmg.rogue", "crit.generic", "crit.melee", "crit.ranged", "crit.magic", "crit.rogue", "speed.melee", "speed.ranged", "speed.magic", "armorPen", "noKnockback", "buffLifeRegen" };
			internal static long Checks, Diverged, MissedSpawns;
			internal static readonly long[] Diff = new long[StatNames.Length], Higher = new long[StatNames.Length], Lower = new long[StatNames.Length];
			internal static readonly double[] SumAbs = new double[StatNames.Length], MaxAbs = new double[StatNames.Length], SumRel = new double[StatNames.Length];
			internal static readonly Dictionary<string, long> Leaves = new();
			internal static readonly List<string> Samples = new();
			internal static void Reset() { Checks = Diverged = MissedSpawns = 0; Array.Clear(Diff); Array.Clear(Higher); Array.Clear(Lower); Array.Clear(SumAbs); Array.Clear(MaxAbs); Array.Clear(SumRel); Leaves.Clear(); Samples.Clear(); }
			private static DamageClass _rogue; private static bool _rogueLooked;
			internal static DamageClass Rogue { get { if (!_rogueLooked) { _rogueLooked = true; try { if (ModLoader.TryGetMod("CalamityMod", out Mod cal) && cal.TryFind<DamageClass>("RogueDamageClass", out var r)) _rogue = r; } catch { } } return _rogue; } }

			internal static void Vec(Player d, int buffRegen, double[] v)
			{
				int i = 0; var rg = Rogue ?? DamageClass.Throwing;
				v[i++] = (int)d.statDefense; v[i++] = d.endurance; v[i++] = d.moveSpeed; v[i++] = d.lifeRegen; v[i++] = d.wingTimeMax; v[i++] = d.maxRunSpeed; v[i++] = d.runAcceleration; v[i++] = d.jumpSpeedBoost; v[i++] = d.aggro; v[i++] = d.maxMinions;
				v[i++] = d.statLifeMax2; v[i++] = d.statManaMax2; v[i++] = d.manaCost;
				v[i++] = d.GetTotalDamage(DamageClass.Generic).ApplyTo(100f); v[i++] = d.GetTotalDamage(DamageClass.Melee).ApplyTo(100f); v[i++] = d.GetTotalDamage(DamageClass.Ranged).ApplyTo(100f); v[i++] = d.GetTotalDamage(DamageClass.Magic).ApplyTo(100f);
				v[i++] = d.GetTotalDamage(DamageClass.Summon).ApplyTo(100f); v[i++] = d.GetTotalDamage(rg).ApplyTo(100f);
				v[i++] = d.GetTotalCritChance(DamageClass.Generic); v[i++] = d.GetTotalCritChance(DamageClass.Melee); v[i++] = d.GetTotalCritChance(DamageClass.Ranged); v[i++] = d.GetTotalCritChance(DamageClass.Magic); v[i++] = d.GetTotalCritChance(rg);
				v[i++] = d.GetTotalAttackSpeed(DamageClass.Melee); v[i++] = d.GetTotalAttackSpeed(DamageClass.Ranged); v[i++] = d.GetTotalAttackSpeed(DamageClass.Magic); v[i++] = d.GetTotalArmorPenetration(DamageClass.Generic);
				v[i++] = d.noKnockback ? 1 : 0; v[i++] = buffRegen;
			}
		}

		private readonly double[] _probeP = new double[GearApproxProbe.StatNames.Length], _probeC = new double[GearApproxProbe.StatNames.Length], _probeF = new double[GearApproxProbe.StatNames.Length];

		/// <summary>Lab: a tick the approximate frame would serve, served, and compared with the full build from the same state. False when it would not serve (the caller builds as usual).</summary>
		private bool GearApproxProbeTick(GearApprox ga, Player d, int gi, int savedNum, float savedSlots, int hs, int ha, ref long pf)
		{
			_par0 ??= new StateImage(); _parC ??= new StateImage(); _parF ??= new StateImage();
			ParityRoots(d);
			_par0.Capture(_parRoots, _parNames);
			int regen0 = _buffLifeRegen; bool cf0 = Main.SceneMetrics.HasCampfire, hl0 = Main.SceneMetrics.HasHeartLantern;
			GearApproxProbe.Vec(d, _buffLifeRegen, _probeP);
			if (!ga.TryServe(this, d, gi)) return false;
			_parC.Capture(_parRoots, _parNames);
			int regenC = _buffLifeRegen; bool cfC = Main.SceneMetrics.HasCampfire, hlC = Main.SceneMetrics.HasHeartLantern;
			GearApproxProbe.Vec(d, regenC, _probeC);

			_par0.Restore(); _buffLifeRegen = regen0; Main.SceneMetrics.HasCampfire = cf0; Main.SceneMetrics.HasHeartLantern = hl0;
			int sp0 = GearUpkeep.Spawned.Count;
			GearBuild(d, savedNum, savedSlots, hs, ha, ref pf);
			if (GearApprox.SpawnedReal(sp0)) GearApproxProbe.MissedSpawns++;
			for (int k = sp0; k < GearUpkeep.Spawned.Count; k++) { int idx = GearUpkeep.Spawned[k]; if (idx >= 0 && idx < Main.maxProjectiles) { Main.projectile[idx].active = false; Main.projectile[idx].timeLeft = 0; } }
			if (GearUpkeep.Spawned.Count > sp0) GearUpkeep.Spawned.RemoveRange(sp0, GearUpkeep.Spawned.Count - sp0);
			GearApproxProbe.Vec(d, _buffLifeRegen, _probeF);
			_parF.Capture(_parRoots, _parNames);
			var diffs = new List<string>();
			int n = _parC.DiffInto(_parF, diffs, 24);
			if (cfC != Main.SceneMetrics.HasCampfire) diffs.Add("scene.HasCampfire");
			if (hlC != Main.SceneMetrics.HasHeartLantern) diffs.Add("scene.HasHeartLantern");

			_parC.Restore(); _buffLifeRegen = regenC; Main.SceneMetrics.HasCampfire = cfC; Main.SceneMetrics.HasHeartLantern = hlC;
			GearApproxProbe.Checks++;
			if (diffs.Count > 0)
			{
				GearApproxProbe.Diverged++;
				foreach (var f in diffs)
				{
					GearApproxProbe.Leaves.TryGetValue(f, out long c); GearApproxProbe.Leaves[f] = c + 1;
					if (c < 3 && !f.StartsWith("RNG", StringComparison.Ordinal))
					{
						long vc = 0, vf = 0, vp = 0; _parC.Flatten((k, v) => { if (k == f) vc = v; }); _parF.Flatten((k, v) => { if (k == f) vf = v; }); _par0.Flatten((k, v) => { if (k == f) vp = v; });
						GearApproxProbe.Samples.Add("leaf " + f + " pre=" + vp + " served=" + vc + " full=" + vf + " levy " + NPC.whoAmI);
					}
				}
			}
			for (int i = 0; i < _probeC.Length; i++)
			{
				double a = _probeC[i], b = _probeF[i], dd = a - b;
				if (Math.Abs(dd) <= 1e-6 * Math.Max(1, Math.Abs(b))) continue;
				GearApproxProbe.Diff[i]++; if (dd > 0) GearApproxProbe.Higher[i]++; else GearApproxProbe.Lower[i]++;
				if (GearApproxProbe.Diff[i] <= 3) GearApproxProbe.Samples.Add(GearApproxProbe.StatNames[i] + " pre=" + _probeP[i] + " served=" + a + " full=" + b + " sinceBuild=" + (Main.GameUpdateCount % (uint)gi) + " buffs " + string.Join("|", System.Linq.Enumerable.Where(d.buffType, t => t > 0)) + " body " + string.Join("|", System.Linq.Enumerable.Where(NPC.buffType, t => t > 0)) + " levy " + NPC.whoAmI + " (" + GearSummary() + ")");
				GearApproxProbe.SumAbs[i] += Math.Abs(dd); GearApproxProbe.MaxAbs[i] = Math.Max(GearApproxProbe.MaxAbs[i], Math.Abs(dd)); GearApproxProbe.SumRel[i] += Math.Abs(dd) / Math.Max(1e-9, Math.Abs(b));
			}
			return true;
		}

		/// <summary>The approximate frame may serve this levy: interval above 1, fighting (not dormant: the caller says), not an officer or turned, and the exact cache off.</summary>
		private bool GearApproxEligible(int interval) => interval > 1 && !GearCache.On && !OfficerMode && Record != null && !Hostile && !MindControlled;
	}
}
