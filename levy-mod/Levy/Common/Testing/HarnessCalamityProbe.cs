using System;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Read-only Calamity probes for the combat harness. Reads, by reflection (Levy has no compile-time reference to CalamityMod, and adding one would break the old-toolchain build):
	///  * CalamityMod.CalPlayer.CalamityPlayer (a ModPlayer, CalPlayer/CalamityPlayer.cs): public float rogueStealth (current stealth, 1.0 = 100), rogueStealthMax,
	///    rage / rageMax (line ~624), adrenaline / adrenalineMax (line ~658), and StealthStrikeAvailable() (rogueStealthMax &gt; 0 and rogueStealth &gt;= max x cost multiplier).
	///  * CalamityMod.Projectiles.CalamityGlobalProjectile (a GlobalProjectile, Projectiles/CalamityGlobalProjectile.cs): public bool stealthStrike (set on the projectile a rogue
	///    stealth strike fires) and stealthStrikeSubProjectile (a child spawned by one).
	/// Touched only when CalamityMod is loaded; every call is wrapped so a missing or renamed member just reads as "unavailable".
	/// </summary>
	[JITWhenModsEnabled("CalamityMod")]
	public static class HarnessCalamityProbe
	{
		private static bool _init, _ok;
		private static Type _cpT, _gpT;
		private static ModPlayer _cpBase; private static GlobalProjectile _gpBase;
		private static MethodInfo _getModPlayer, _getGlobalProj, _strikeAvail;
		private static FieldInfo _stealth, _stealthMax, _rage, _rageMax, _adr, _adrMax, _strike, _strikeSub;

		public static bool Available { get { Init(); return _ok; } }

		private static void Init()
		{
			if (_init) return;
			_init = true;
			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
				_cpT = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
				_gpT = cal.Code.GetType("CalamityMod.Projectiles.CalamityGlobalProjectile");
				if (_cpT == null || _gpT == null) return;
				_cpBase = ModContent.GetContent<ModPlayer>().FirstOrDefault(m => m.GetType() == _cpT);
				_gpBase = ModContent.GetContent<GlobalProjectile>().FirstOrDefault(m => m.GetType() == _gpT);
				if (_cpBase == null || _gpBase == null) return;
				_getModPlayer = typeof(Player).GetMethods().First(m => m.Name == "GetModPlayer" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1).MakeGenericMethod(_cpT);
				_getGlobalProj = typeof(Projectile).GetMethods().First(m => m.Name == "GetGlobalProjectile" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1).MakeGenericMethod(_gpT);
				const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				_stealth = _cpT.GetField("rogueStealth", F); _stealthMax = _cpT.GetField("rogueStealthMax", F);
				_rage = _cpT.GetField("rage", F); _rageMax = _cpT.GetField("rageMax", F);
				_adr = _cpT.GetField("adrenaline", F); _adrMax = _cpT.GetField("adrenalineMax", F);
				_strikeAvail = _cpT.GetMethod("StealthStrikeAvailable", F, null, Type.EmptyTypes, null);
				_strike = _gpT.GetField("stealthStrike", F); _strikeSub = _gpT.GetField("stealthStrikeSubProjectile", F);
				_ok = _stealth != null && _stealthMax != null && _strike != null;
			}
			catch (Exception) { _ok = false; }
		}

		private static object CalPlayer(Player p) { try { return _getModPlayer.Invoke(p, new object[] { _cpBase }); } catch (Exception) { return null; } }

		/// <summary>Stealth {current, max} of this player's CalamityPlayer, or null when unreadable.</summary>
		public static float[] Stealth(Player p)
		{
			Init(); if (!_ok || p == null) return null;
			object c = CalPlayer(p); if (c == null) return null;
			try { return new[] { (float)_stealth.GetValue(c), (float)_stealthMax.GetValue(c) }; } catch (Exception) { return null; }
		}

		public static bool StealthStrikeAvailable(Player p)
		{
			Init(); if (!_ok || _strikeAvail == null || p == null) return false;
			try { object c = CalPlayer(p); return c != null && (bool)_strikeAvail.Invoke(c, null); } catch (Exception) { return false; }
		}

		/// <summary>Rage and adrenaline {rage, rageMax, adrenaline, adrenalineMax} (informational), or null.</summary>
		public static float[] RageAdrenaline(Player p)
		{
			Init(); if (!_ok || p == null || _rage == null || _adr == null) return null;
			object c = CalPlayer(p); if (c == null) return null;
			try { return new[] { (float)_rage.GetValue(c), (float)_rageMax.GetValue(c), (float)_adr.GetValue(c), (float)_adrMax.GetValue(c) }; } catch (Exception) { return null; }
		}

		/// <summary>True when Calamity flagged this projectile as a stealth strike (or a sub-projectile of one).</summary>
		public static bool IsStealthStrike(Projectile proj, out bool sub)
		{
			sub = false;
			Init(); if (!_ok || proj == null) return false;
			try
			{
				object g = _getGlobalProj.Invoke(proj, new object[] { _gpBase });
				if (g == null) return false;
				bool s = (bool)_strike.GetValue(g);
				sub = _strikeSub != null && (bool)_strikeSub.GetValue(g);
				return s;
			}
			catch (Exception) { return false; }
		}
	}
}
