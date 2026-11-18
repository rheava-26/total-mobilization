using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// The sky states of Wrath of the Gods (NoxusBoss), read by name and reflection only: Levy has no reference to it and works the same without it.
	/// What is read (all public in NoxusBoss; every member is optional, a missing one reads as off and is named in <see cref="Problem"/>):
	///   PostMLRiftAppearanceSystem (a ModSystem; Core.World.GameScenes.AvatarAppearances): static AvatarHasCoveredMoon (saved, true once the sun has been covered after Moon Lord),
	///     instance Ongoing / EventTimer (the covering scene in progress, about 27 s) and WaitingToStart (armed by the Moon Lord kill).
	///   RiftEclipseManagementSystem.RiftEclipseOngoing (static; Core.World.GameScenes.RiftEclipse): the covered sky as the mod applies it (off during some of its own fights, and once the sky is freed).
	///   RiftEclipseSnowSystem.SnowHeight (static float, 0 to 10): snow lying on the surface. Rain under the covered sky falls as snow.
	///   RiftEclipseFogEventManager.EventOngoing (static): the fog event (late game).
	///   Mod.Call("GetBossDefeated", "avatarofemptiness"): the sky has been freed for good.
	///   WorldSaveSystem.AvatarHasKilledOldDuke (static; Core.World.WorldSaving): set when the sky swallows Calamity's Old Duke (NoxusBoss takes over that NPC's AI while the sky is not
	///     freed and the flag is off; the NPC is CalamityMod/OldDuke, found by name).

	/// Reads are cached per game tick. Self-checks set the Test* overrides (null in play) or drive the real members through <see cref="SetReal"/>.
	/// </summary>
	public static class RiftSky
	{
		internal const string ModName = "NoxusBoss";
		internal const string AppearanceType = "NoxusBoss.Core.World.GameScenes.AvatarAppearances.PostMLRiftAppearanceSystem";
		internal const string ManagementType = "NoxusBoss.Core.World.GameScenes.RiftEclipse.RiftEclipseManagementSystem";
		internal const string SnowType = "NoxusBoss.Core.World.GameScenes.RiftEclipse.RiftEclipseSnowSystem";
		internal const string FogType = "NoxusBoss.Core.World.GameScenes.RiftEclipse.RiftEclipseFogEventManager";
		internal const string SaveType = "NoxusBoss.Core.World.WorldSaving.WorldSaveSystem";

		// ---- self-check overrides (null in play) ----
		internal static bool? TestInstalled, TestTransition, TestCovered, TestDefeated, TestSnowing, TestDukeKilled, TestDukeAlive;
		internal static float? TestSnowHeight;

		private static bool _resolved, _installed;
		private static Mod _mod;
		private static object _appearance;
		private static PropertyInfo _covered, _ongoing, _eventTimer, _waiting, _riftOngoing, _snowHeight, _fog, _dukeKilled;
		private static int _dukeType = -1;
		/// <summary>What could not be found (empty when everything resolved, "not installed" without the mod).</summary>
		public static string Problem { get; private set; } = "";
		/// <summary>The members found, for the self-check report.</summary>
		public static string Found { get; private set; } = "";

		private static uint _cacheTick = uint.MaxValue;
		private static bool _cTransition, _cCovered, _cRift, _cFog;
		private static float _cSnow;
		private static uint _defeatTick = uint.MaxValue;
		private static bool _cDefeated;

		public static bool Installed { get { if (TestInstalled.HasValue) return TestInstalled.Value; Resolve(); return _installed; } }

		/// <summary>Finds the members once. Never throws. False without NoxusBoss.</summary>
		public static bool Resolve()
		{
			if (_resolved) return _installed;
			_resolved = true;
			try
			{
				if (!ModLoader.TryGetMod(ModName, out _mod) || _mod == null) { _installed = false; Problem = "not installed"; return false; }
				_installed = true;
				Assembly code = _mod.Code;
				var problems = new System.Text.StringBuilder(); var found = new System.Text.StringBuilder();
				Type app = code?.GetType(AppearanceType), man = code?.GetType(ManagementType), snow = code?.GetType(SnowType), fog = code?.GetType(FogType), save = code?.GetType(SaveType);
				const BindingFlags S = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, I = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
				_covered = Prop(app, "AvatarHasCoveredMoon", S, typeof(bool), problems, found);
				_ongoing = Prop(app, "Ongoing", I, typeof(bool), problems, found);
				_eventTimer = Prop(app, "EventTimer", I, typeof(int), problems, found);
				_waiting = Prop(app, "WaitingToStart", I, typeof(bool), problems, found);
				_riftOngoing = Prop(man, "RiftEclipseOngoing", S, typeof(bool), problems, found);
				_snowHeight = Prop(snow, "SnowHeight", S, typeof(float), problems, found);
				_fog = Prop(fog, "EventOngoing", S, typeof(bool), problems, found);
				_dukeKilled = Prop(save, "AvatarHasKilledOldDuke", S, typeof(bool), problems, found);
				if (ModContent.TryFind("CalamityMod", "OldDuke", out ModNPC duke)) { _dukeType = duke.Type; found.Append("CalamityMod/OldDuke, "); }
				if (app != null)
					foreach (ModSystem sys in _mod.GetContent<ModSystem>())
						if (sys.GetType() == app) { _appearance = sys; break; }
				if (_appearance == null) problems.Append("no PostMLRiftAppearanceSystem instance; ");
				Problem = problems.ToString().TrimEnd(' ', ';');
				Found = found.ToString().TrimEnd(' ', ',');
			}
			catch (Exception e) { Problem = "resolve threw " + e.GetType().Name + ": " + e.Message; }
			return _installed;
		}

		private static PropertyInfo Prop(Type t, string name, BindingFlags flags, Type want, System.Text.StringBuilder problems, System.Text.StringBuilder found)
		{
			if (t == null) { problems.Append("type for ").Append(name).Append(" missing; "); return null; }
			PropertyInfo p = t.GetProperty(name, flags);
			if (p == null || p.PropertyType != want || p.GetMethod == null) { problems.Append(t.Name).Append('.').Append(name).Append(" missing; "); return null; }
			found.Append(t.Name).Append('.').Append(name).Append(", ");
			return p;
		}

		private static object Target(PropertyInfo p) => p.GetMethod.IsStatic ? null : _appearance;

		private static T Read<T>(PropertyInfo p, T fallback)
		{
			if (p == null) return fallback;
			object target = Target(p);
			if (target == null && !p.GetMethod.IsStatic) return fallback;
			try { return p.GetValue(target) is T v ? v : fallback; } catch (Exception) { return fallback; }
		}

		private static void Refresh()
		{
			uint now = Main.GameUpdateCount;
			if (now == _cacheTick) return;
			_cacheTick = now;
			if (!Resolve()) { _cTransition = _cCovered = _cRift = _cFog = false; _cSnow = 0f; return; }
			_cCovered = Read(_covered, false);
			_cTransition = !_cCovered && Read(_ongoing, false);
			_cRift = Read(_riftOngoing, false);
			_cSnow = Read(_snowHeight, 0f);
			_cFog = Read(_fog, false);
		}

		/// <summary>The sun is being covered right now (the scene after Moon Lord, before the sky is fully dark).</summary>
		public static bool Transition { get { if (TestTransition.HasValue) return TestTransition.Value; Refresh(); return _cTransition; } }

		/// <summary>The sun has been covered in this world (saved by NoxusBoss; stays true after the sky is freed: see <see cref="Defeated"/>).</summary>
		public static bool Covered { get { if (TestCovered.HasValue) return TestCovered.Value; Refresh(); return _cCovered; } }

		/// <summary>NoxusBoss's own "covered sky is on" (off during some of its fights). Read for the self-check report; the mood keys on <see cref="Covered"/> and <see cref="Defeated"/>.</summary>
		public static bool RiftOngoing { get { Refresh(); return _cRift; } }

		/// <summary>The covered sky is gone for good. Read through the mod's own call, at most once a second.</summary>
		public static bool Defeated
		{
			get
			{
				if (TestDefeated.HasValue) return TestDefeated.Value;
				if (!Resolve()) return false;
				uint now = Main.GameUpdateCount;
				if (_defeatTick != uint.MaxValue && now - _defeatTick < 60u) return _cDefeated;
				_defeatTick = now;
				try { _cDefeated = _mod.Call("GetBossDefeated", "avatarofemptiness") is bool b && b; } catch (Exception) { _cDefeated = false; }
				return _cDefeated;
			}
		}

		/// <summary>Snow lying on the surface, NoxusBoss's 0 to 10 scale.</summary>
		public static float SnowHeight { get { if (TestSnowHeight.HasValue) return TestSnowHeight.Value; Refresh(); return _cSnow; } }

		/// <summary>Snow is falling: rain under the covered sky.</summary>
		public static bool Snowing { get { if (TestSnowing.HasValue) return TestSnowing.Value; return Covered && !Defeated && Main.raining; } }

		/// <summary>The late-game fog event is on.</summary>
		public static bool Fog { get { Refresh(); return _cFog; } }

		/// <summary>The sky has swallowed the Old Duke in this world (NoxusBoss's saved flag).</summary>
		public static bool DukeKilled { get { if (TestDukeKilled.HasValue) return TestDukeKilled.Value; if (!Resolve()) return false; return Read(_dukeKilled, false); } }

		/// <summary>The NPC type of Calamity's Old Duke, or -1 when Calamity (or that NPC) is not loaded.</summary>
		public static int DukeType { get { Resolve(); return _dukeType; } }

		/// <summary>An Old Duke is alive right now and the sky will take it (NoxusBoss runs that scene while the flag is off and the sky is not freed). <paramref name="at"/> is where it is.</summary>
		public static bool DukeInScene(out Microsoft.Xna.Framework.Vector2 at)
		{
			at = TestDukeAt;
			if (TestDukeAlive.HasValue) return TestDukeAlive.Value;
			if (!Resolve() || _dukeType < 0 || DukeKilled || Defeated) return false;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n != null && n.active && n.type == _dukeType) { at = n.Center; return true; } }
			return false;
		}

		/// <summary>Self-check: where an overridden Old Duke stands.</summary>
		internal static Microsoft.Xna.Framework.Vector2 TestDukeAt;

		/// <summary>Self-check and world-check hook: write the real NoxusBoss members (null leaves one alone). Returns false when NoxusBoss or a member is missing.</summary>
		internal static bool SetReal(bool? covered = null, int? eventTimer = null, bool? waiting = null, float? snowHeight = null, bool? dukeKilled = null)
		{
			if (!Resolve()) return false;
			bool ok = true;
			try
			{
				if (covered.HasValue) { if (_covered?.SetMethod == null) ok = false; else _covered.SetValue(null, covered.Value); }
				if (eventTimer.HasValue) { if (_eventTimer?.SetMethod == null || _appearance == null) ok = false; else _eventTimer.SetValue(_appearance, eventTimer.Value); }
				if (waiting.HasValue) { if (_waiting?.SetMethod == null || _appearance == null) ok = false; else _waiting.SetValue(_appearance, waiting.Value); }
				if (snowHeight.HasValue) { if (_snowHeight?.SetMethod == null) ok = false; else _snowHeight.SetValue(null, snowHeight.Value); }
				if (dukeKilled.HasValue) { if (_dukeKilled?.SetMethod == null) ok = false; else _dukeKilled.SetValue(null, dukeKilled.Value); }
			}
			catch (Exception) { ok = false; }
			_cacheTick = uint.MaxValue; _defeatTick = uint.MaxValue;
			return ok;
		}

		/// <summary>Self-check hook: the real values (not the overrides), uncached.</summary>
		internal static (bool covered, bool ongoing, int timer, bool waiting, bool rift, float snow, bool fog) ReadReal()
		{
			_cacheTick = uint.MaxValue;
			if (!Resolve()) return default;
			return (Read(_covered, false), Read(_ongoing, false), Read(_eventTimer, 0), Read(_waiting, false), Read(_riftOngoing, false), Read(_snowHeight, 0f), Read(_fog, false));
		}

		internal static void ClearOverrides() { TestInstalled = TestTransition = TestCovered = TestDefeated = TestSnowing = TestDukeKilled = TestDukeAlive = null; TestSnowHeight = null; TestDukeAt = default; _cacheTick = uint.MaxValue; _defeatTick = uint.MaxValue; }
	}
}
