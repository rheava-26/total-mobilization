using System;
using System.Collections.Generic;
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

		// =====================================================================================================
		// Attacks with a telegraph or a safe place. The default (follow the commander's slot, dodge what is in flight) fails each of these the same way: the telegraph is a projectile with no damage
		// and the army never reads it, and the safe place is where only the commander was told to stand. Each rule reads the attack's own projectile or state, named here with the NoxusBoss file it comes from.
		// =====================================================================================================

		/// <summary>A place the army keeps out of (Keep false) or stands in (Keep true) for as long as the attack lasts.</summary>
		public struct Zone { public Rectangle Rect; public bool Keep; public string Why; }

		/// <summary>Self-check: false = the attack rules are off (the flight stays on), to tell what they add.</summary>
		internal static bool? TestZones;
		private static readonly List<Zone> _zones = new();
		private static uint _zoneStamp = uint.MaxValue;
		private static bool _zoneInit; private static int _frostTele = -1, _frostCol = -1, _field = -1, _solyn = -1; private static MethodInfo _aiInt;

		private static void ZoneInit()
		{
			if (_zoneInit) return; _zoneInit = true;
			try
			{
				if (!ModLoader.TryGetMod("NoxusBoss", out Mod nox)) return;
				if (nox.TryFind("FrostColumnTelegraph", out ModProjectile a)) _frostTele = a.Type;
				if (nox.TryFind("FrostColumn", out ModProjectile b)) _frostCol = b.Type;
				if (nox.TryFind("SolynProtectiveForcefield", out ModProjectile c)) _field = c.Type;
				if (nox.TryFind("BattleSolyn", out ModNPC d)) _solyn = d.Type;
				_aiInt = _avatarT?.GetMethod("GetAIInt", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
			}
			catch (Exception) { }
		}

		/// <summary>The keep-out and stand-in places of this tick's attack (built once a tick; empty outside the attacks below).</summary>
		public static List<Zone> Zones
		{
			get
			{
				uint now = Main.GameUpdateCount;
				if (_zoneStamp == now) return _zones;
				_zoneStamp = now; _zones.Clear();
				Read();
				if (_boss == null || !Enabled || TestZones == false) return _zones;
				ZoneInit();
				try { BuildZones(); } catch (Exception) { _zones.Clear(); }
				return _zones;
			}
		}

		private static void BuildZones()
		{
			string st = _state;
			NPC boss = _boss;
			// RealityShatter (AvatarOfEmptiness.BehaviorStates.RealityShatter.cs; Projectiles/AvatarSlamTelegraph.cs): the Avatar waits above its target while a lane 1400 px wide (the telegraph, drawn red) fills in over
			// RealityShatter_TelegraphTime, then falls down that lane at up to 107 px a tick with its contact damage on. Anything in the lane when it falls is hit; anything outside it is not.
			if (st == "RealityShatter_CreateAndWaitForTelegraph" || st == "RealityShatter_SlamDownward")
			{
				float half = 700f + 90f;
				_zones.Add(new Zone { Rect = new Rectangle((int)(boss.Center.X - half), (int)(boss.Center.Y - 600f), (int)(half * 2f), 24000), Keep = false, Why = "slam lane" });
			}
			// WhirlingIceStorm (AvatarOfEmptiness.BehaviorStates.WhirlingIceStorm.cs; Projectiles/FrostColumnTelegraph.cs and FrostColumn.cs): a column marked 1.25 s ahead (the telegraph carries no damage) then 540 px wide and
			// 4020 px tall rising from the marked point, damaging while it stands.
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active) continue;
				if (p.type == _frostTele && _frostTele >= 0)
					_zones.Add(new Zone { Rect = new Rectangle((int)(p.Center.X - 330f), (int)(p.Center.Y - 4100f), 660, 4220), Keep = false, Why = "frost column marked" });
				else if (p.type == _frostCol && _frostCol >= 0)
				{
					Rectangle h = p.Hitbox; h.Inflate(40, 0);
					_zones.Add(new Zone { Rect = h, Keep = false, Why = "frost column" });
				}
			}
			// BloodiedFountainBlasts_DenseBurst (AvatarOfEmptiness.BehaviorStates.BloodiedFountainBlasts.cs; Projectiles/SolynProjectiles/SolynProtectiveForcefield.cs): Solyn says "stay below me" and holds a shield
			// (BloodiedFountainBlasts_SolynForcefieldWidth wide, a little under as tall, centred 40 px under her) that destroys every Avatar projectile touching it (its hitbox is that rectangle moved 35 px up and grown 16 by 10):
			// the blood rain of the burst is stopped by it for whoever stands in its width at or below the shield. This is the one safe place of the attack.
			if (st == "BloodiedFountainBlasts_DenseBurst")
			{
				Rectangle? r = null;
				for (int i = 0; i < Main.maxProjectiles && r == null; i++)
				{
					Projectile p = Main.projectile[i];
					if (p.active && p.type == _field && _field >= 0 && p.Opacity > 0.3f && p.scale > 0.4f) { Rectangle h = p.Hitbox; h.Y -= 35; h.Inflate(16, 10); r = h; }
				}
				if (r == null && _solyn >= 0)
				{
					for (int i = 0; i < Main.maxNPCs && r == null; i++)
					{
						NPC n = Main.npc[i];
						if (!n.active || n.type != _solyn) continue;
						int w = 285; try { if (_aiInt != null) w = Convert.ToInt32(_aiInt.Invoke(null, new object[] { "BloodiedFountainBlasts_SolynForcefieldWidth" })); } catch (Exception) { }
						int h = (int)(w * 0.875f);
						r = new Rectangle((int)(n.Center.X - w / 2f), (int)(n.Center.Y + 40f - h / 2f - 35f), w, h);
					}
				}
				if (r != null) _zones.Add(new Zone { Rect = r.Value, Keep = true, Why = "solyn shield" });
			}
		}

		/// <summary>
		/// A body's goal moved for this tick's zones: out of a keep-out place (to the side of it the commander is on, or by its slot when he is in it), into a stand-in place (a grid in its lower half, one cell a body by slot).
		/// <paramref name="urgent"/> is true when the body is in a place it must leave, or outside one it must reach: it goes at full speed.
		/// </summary>
		public static Vector2 AdjustForZones(NPC body, Vector2 want, int slot, Player cmd, out bool urgent)
		{
			urgent = false;
			var zones = Zones;
			for (int zi = 0; zi < zones.Count; zi++)
			{
				Zone z = zones[zi];
				if (z.Keep)
				{
					int pad = 26;
					Rectangle r = z.Rect;
					int cols = Math.Max(1, (r.Width - 2 * pad) / 30);
					int col = slot % cols, row = slot / cols;
					float x = r.X + pad + (col + 0.5f) * (r.Width - 2f * pad) / cols;
					float y = Math.Min(r.Bottom - 30f, r.Center.Y + 10f + row * 30f);
					Vector2 inside = new Vector2(x, y);
					if (!r.Contains(body.Center.ToPoint())) urgent = true;
					want = inside;
				}
				else
				{
					Rectangle r = z.Rect; r.Inflate((int)(body.width / 2f + 24f), (int)(body.height / 2f + 24f));
					bool wantIn = r.Contains(want.ToPoint()), bodyIn = r.Contains(body.Center.ToPoint());
					if (!wantIn && !bodyIn) continue;
					int side;
					if (cmd != null && !r.Contains(cmd.Center.ToPoint())) side = cmd.Center.X < r.Center.X ? -1 : 1;
					else side = (slot & 1) == 0 ? -1 : 1;
					want = new Vector2(side < 0 ? r.Left - 8f : r.Right + 8f, want.Y);
					if (bodyIn) urgent = true;
				}
			}
			return want;
		}

		/// <summary>True when a point is the middle of the world, where the Avatar puts a player it sends to its universe.</summary>
		public static bool IsUniversePoint(Vector2 center)
		{
			Vector2 mid = new Vector2(Main.maxTilesX, Main.maxTilesY) * 8f;
			return Math.Abs(center.X - mid.X) < 160f && Math.Abs(center.Y - mid.Y) < 160f;
		}
	}
}
