using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Dialogue;

namespace Levy.Common.Compat.Wotg
{
	/// <summary>
	/// THE ARMY IN THE AVATAR OF EMPTINESS FIGHT (Wrath of the Gods, mod "NoxusBoss"): what the fight does to the space the army stands in, read by name and reflection only (no reference; a missing member turns the part off).
	///   The Avatar of Emptiness (NPC AvatarOfEmptiness) and the Avatar's rift (AvatarRift) are the fight; the second form's CurrentState (enum, read as its name by <see cref="WotgScenes.StateOf"/>) says which attack it is in.
	///   TileDisablingSystem.TilesAreUninteractable (static bool, NoxusBoss.Core.World.TileDisabling): true in every one of the Avatar's own dimensions (the frost, blood, dark and antishadow dimensions, the annihilation void, the
	///   dimension twist). Every tile collision, solid-collision and slope query of the whole world answers "nothing" while it is set, for players and NPCs alike: nothing stands on anything, and a body that is not held up falls forever.
	///   AvatarOfEmptiness.SendPlayerToMyUniverse (AvatarOfEmptiness.BehaviorStates.SendPlayerToMyUniverse.cs): at the end of the suck every player's Center is set to (Main.maxTilesX, Main.maxTilesY) * 8, the middle of the world,
	///   with no Player.Teleport call, no Player.Spawn and no packet other than its own; the dimension attacks are then fought around that point, in the void, until the Avatar returns the player.
	///   DarkPortal (the dark dimension's portals) moves a player with Player.Teleport inside the void.
	///   AvatarRift / AvatarOfEmptiness give every player infinite wing time for the whole fight (player.wingTime = wingTimeMax every tick).
	/// What Levy does with it (SoldierNPC.AvatarFlight.cs, AvatarFightSystem): the army comes along when the commander is moved, flies with them wherever a body can, and is carried where it cannot. Off with the "Levies fly with the
	/// commander in the Avatar of Emptiness fight" setting; <c>LEVY_AVBEHAVE=0</c> switches the whole thing off for the lab and the world check (A/B).
	/// </summary>
	public static class AvatarFight
	{
		public static readonly bool EnvOn = Environment.GetEnvironmentVariable("LEVY_AVBEHAVE") != "0";
		/// <summary>Self-check override: null = the setting, true/false = pretend.</summary>
		internal static bool? TestOn;

		/// <summary>The behaviour is on (the setting, and not switched off by the lab).</summary>
		public static bool Enabled
		{
			get
			{
				if (TestOn.HasValue) return TestOn.Value;
				if (!EnvOn) return false;
				try { return Config.LevyConfig.Get?.AvatarFightArmyFlies ?? true; } catch (Exception) { return true; }
			}
		}

		private static bool _init, _ok;
		private static PropertyInfo _tilesOff;
		private static Type _avatarT;

		private static void Init()
		{
			if (_init) return; _init = true;
			try
			{
				if (!ModLoader.TryGetMod("NoxusBoss", out Mod nox)) return;
				_tilesOff = nox.Code.GetType("NoxusBoss.Core.World.TileDisabling.TileDisablingSystem")?.GetProperty("TilesAreUninteractable", BindingFlags.Public | BindingFlags.Static);
				_avatarT = nox.Code.GetType("NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm.AvatarOfEmptiness");
				_ok = _tilesOff != null && _tilesOff.PropertyType == typeof(bool);
			}
			catch (Exception) { _ok = false; }
		}

		// ---- one read per game tick ----
		private static uint _stamp = uint.MaxValue; private static NPC _boss, _rift; private static bool _void; private static string _state;

		private static void Read()
		{
			uint now = Main.GameUpdateCount;
			if (_stamp == now) return;
			_stamp = now; _boss = null; _rift = null; _void = false; _state = null;
			if (!WotgScenes.Installed) return;
			Init();
			int at = WotgScenes.AvatarType, rt = WotgScenes.RiftType;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n == null || !n.active) continue;
				if (n.type == at && at >= 0) _boss = n; else if (n.type == rt && rt >= 0) _rift = n;
			}
			if (_boss != null) _state = WotgScenes.StateOf(_boss);
			if (_ok && (_boss != null || _rift != null)) { try { _void = (bool)_tilesOff.GetValue(null); } catch (Exception) { _void = false; } }
			if (TestVoid.HasValue && (_boss != null || _rift != null)) _void = TestVoid.Value;
		}
		/// <summary>Self-check: pretend the dimension is up (true) or down (false); null = read the game.</summary>
		internal static bool? TestVoid;

		/// <summary>The Avatar of Emptiness (second form) is up, or null.</summary>
		public static NPC Boss { get { Read(); return _boss; } }

		/// <summary>The fight is on: the Avatar of Emptiness, or its rift in the sky.</summary>
		public static bool Active { get { Read(); return _boss != null || _rift != null; } }

		/// <summary>The Avatar's dimension is up: no tile holds anything up, for anyone.</summary>
		public static bool Void { get { Read(); return _void; } }

		/// <summary>The name of the Avatar's current attack state, or null.</summary>
		public static string State { get { Read(); return _state; } }

		/// <summary>The behaviour applies to this tick: switched on and a fight is up.</summary>
		public static bool Applies => Enabled && Active;

		/// <summary>True when a point is the middle of the world, where the Avatar puts a player it sends to its universe.</summary>
		public static bool IsUniversePoint(Vector2 center)
		{
			Vector2 mid = new Vector2(Main.maxTilesX, Main.maxTilesY) * 8f;
			return Math.Abs(center.X - mid.X) < 160f && Math.Abs(center.Y - mid.Y) < 160f;
		}
	}
}
