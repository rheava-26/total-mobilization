using System;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Compat.Calamity
{
	/// <summary>
	/// Calamity's Rage and Adrenaline ("the rippers") for levies. Read from CalamityMod's source (CalPlayer/CalamityPlayer.cs, CalamityPlayerMiscEffects.UpdateRippers, CalamityPlayerHitHurt, Utilities/PlayerUtils.cs):
	///  * both exist only while Revengeance is on (CalamityWorld.revenge; Rage also with Shattered Community, Adrenaline with Draedon's Heart).
	///  * Rage fills from standing near enemies (bosses 3x) and burns for 9 s at +35% damage; Adrenaline fills over 30 s while a boss is alive, burns for 5 s at +150% damage, and holds 50% damage reduction while full.
	///  * players switch them on with a hotkey. A levy has no hotkey, so it switches them on by itself the moment the bar is full and a boss is alive near it.
	/// A levy carries a dummy Player with a real CalamityPlayer. Its own PostUpdateMiscEffects (the fill/burn step) only runs for levies in the per-tick gear frame (modded gear), so this class runs the same private step
	/// for the rest, restores the mode flags ResetEffects clears, switches the modes on, scales projectile hits (a levy's shots are owned by the commander, so Calamity's ModifyHitNPCWithProj reads the commander's modes, not the levy's;
	/// melee strikes already run Calamity's hooks on the levy's dummy) and applies the full-bar damage reduction and the adrenaline loss to the levy's incoming hits.
	/// Everything is by reflection (Levy has no compile-time reference to CalamityMod); any missing member turns the feature off.
	/// </summary>
	public static class LevyRippers
	{
		/// <summary>Harness A/B switch (env LEVY_RIPPERS=0).</summary>
		public static bool Disabled = Environment.GetEnvironmentVariable("LEVY_RIPPERS") == "0";
		/// <summary>Counters for the harness: how many times a levy switched Rage / Adrenaline on, how many hits the full-bar reduction cut, how many hits cost adrenaline.</summary>
		public static int RageActivations, AdrenalineActivations, DrCuts, AdrenalineLost;

		/// <summary>A boss must be alive within this many pixels of a levy for it to switch a full bar on.</summary>
		public const float ActivateRangePx = 70f * 16f;

		private static bool _init, _ok;
		private static Type _cpT;
		private static ModPlayer _cpBase;
		private static MethodInfo _getModPlayer, _updateRippers, _loseAdrenaline, _applyRippers, _getDr;
		private static FieldInfo _rage, _rageMax, _rageMode, _adr, _adrMax, _adrMode, _bossesAlive, _revenge;
		private static PropertyInfo _rageEnabled, _adrEnabled, _shielding;

		public static bool Available { get { Init(); return _ok; } }
		/// <summary>True when the levies use the rippers (Calamity loaded and not switched off).</summary>
		public static bool Active { get { Init(); return _ok && !Disabled; } }

		private static void Init()
		{
			if (_init) return;
			_init = true;
			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal) || cal.Code == null) return;
				_cpT = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
				Type util = cal.Code.GetType("CalamityMod.CalamityUtils");
				Type world = cal.Code.GetType("CalamityMod.World.CalamityWorld");
				if (_cpT == null || util == null || world == null) return;
				_cpBase = ModContent.GetContent<ModPlayer>().FirstOrDefault(m => m.GetType() == _cpT);
				if (_cpBase == null) return;
				_getModPlayer = typeof(Player).GetMethods().First(m => m.Name == "GetModPlayer" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1).MakeGenericMethod(_cpT);
				const BindingFlags I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
				_rage = _cpT.GetField("rage", I); _rageMax = _cpT.GetField("rageMax", I); _rageMode = _cpT.GetField("rageModeActive", I);
				_adr = _cpT.GetField("adrenaline", I); _adrMax = _cpT.GetField("adrenalineMax", I); _adrMode = _cpT.GetField("adrenalineModeActive", I);
				_bossesAlive = _cpT.GetField("areThereAnyDamnBosses", S);
				_revenge = world.GetField("revenge", S);
				_rageEnabled = _cpT.GetProperty("RageEnabled", I); _adrEnabled = _cpT.GetProperty("AdrenalineEnabled", I); _shielding = _cpT.GetProperty("TotalEnergyShielding", I);
				_updateRippers = _cpT.GetMethod("UpdateRippers", I, null, Type.EmptyTypes, null);
				_loseAdrenaline = _cpT.GetMethod("LoseAdrenalineOnHurt", I);
				_applyRippers = util.GetMethod("ApplyRippersToDamage", S);
				_getDr = util.GetMethod("GetAdrenalineDR", S);
				_ok = _rage != null && _rageMax != null && _rageMode != null && _adr != null && _adrMax != null && _adrMode != null && _bossesAlive != null && _revenge != null
					&& _rageEnabled != null && _adrEnabled != null && _updateRippers != null && _loseAdrenaline != null && _applyRippers != null && _getDr != null;
				if (!_ok) Levy.Instance?.Logger.Warn("LEVYRIPPERS: Calamity member missing, rippers off (updateRippers=" + (_updateRippers != null) + " lose=" + (_loseAdrenaline != null) + " apply=" + (_applyRippers != null) + " dr=" + (_getDr != null) + ")");
				else Levy.Instance?.Logger.Info("LEVYRIPPERS: ready (Calamity rage and adrenaline for levies)");
			}
			catch (Exception e) { _ok = false; Levy.Instance?.Logger.Warn("LEVYRIPPERS: init failed: " + e.Message); }
		}

		/// <summary>The dummy's CalamityPlayer, or null.</summary>
		public static object Of(Player p)
		{
			Init(); if (!_ok || p == null) return null;
			try { return _getModPlayer.Invoke(p, new object[] { _cpBase }); } catch (Exception) { return null; }
		}

		public static bool RevengeanceOn { get { Init(); try { return _ok && (bool)_revenge.GetValue(null); } catch (Exception) { return false; } } }
		public static bool BossAlive { get { Init(); try { return _ok && (bool)_bossesAlive.GetValue(null); } catch (Exception) { return false; } } }

		public static float Rage(object cp) => cp == null ? 0f : (float)_rage.GetValue(cp);
		public static float RageMax(object cp) => cp == null ? 0f : (float)_rageMax.GetValue(cp);
		public static float Adrenaline(object cp) => cp == null ? 0f : (float)_adr.GetValue(cp);
		public static float AdrenalineMax(object cp) => cp == null ? 0f : (float)_adrMax.GetValue(cp);
		public static bool RageEnabled(object cp) { try { return (bool)_rageEnabled.GetValue(cp); } catch (Exception) { return false; } }
		public static bool AdrenalineEnabled(object cp) { try { return (bool)_adrEnabled.GetValue(cp); } catch (Exception) { return false; } }
		public static bool RageModeActive(object cp) => cp != null && (bool)_rageMode.GetValue(cp);
		public static bool AdrenalineModeActive(object cp) => cp != null && (bool)_adrMode.GetValue(cp);
		public static void SetModes(object cp, bool rage, bool adrenaline) { _rageMode.SetValue(cp, rage); _adrMode.SetValue(cp, adrenaline); }
		public static void SetRage(object cp, float v) => _rage.SetValue(cp, v);
		public static void SetAdrenaline(object cp, float v) => _adr.SetValue(cp, v);

		/// <summary>Calamity's own fill/burn step (private UpdateRippers), for a dummy that is not in the per-tick gear frame.</summary>
		public static void RunStep(object cp) { try { _updateRippers.Invoke(cp, null); } catch (TargetInvocationException e) { Warn("UpdateRippers", e.InnerException ?? e); } catch (Exception e) { Warn("UpdateRippers", e); } }

		/// <summary>Calamity's adrenaline loss for a hit of this size (private LoseAdrenalineOnHurt).</summary>
		public static void LoseOnHurt(object cp, int damage)
		{
			try
			{
				var info = new Player.HurtInfo { Damage = damage, SourceDamage = damage };
				float before = Adrenaline(cp);
				_loseAdrenaline.Invoke(cp, new object[] { info, false });
				if (Adrenaline(cp) < before) AdrenalineLost++;
			}
			catch (TargetInvocationException e) { Warn("LoseAdrenalineOnHurt", e.InnerException ?? e); } catch (Exception e) { Warn("LoseAdrenalineOnHurt", e); }
		}

		/// <summary>Calamity's damage multiplier from the modes (1 = none): the same additive sum its ModifyHitNPCWithItem / WithProj applies.</summary>
		public static float DamageMult(object cp)
		{
			if (cp == null) return 1f;
			try { object[] a = { cp, false, 1f }; _applyRippers.Invoke(null, a); return (float)a[2]; } catch (Exception e) { Warn("ApplyRippersToDamage", e); return 1f; }
		}

		/// <summary>Incoming-hit multiplier from a full adrenaline bar that is not burning (50% reduction), 1 otherwise. Calamity gives no reduction while an energy shield is up.</summary>
		public static float IncomingMult(object cp)
		{
			if (cp == null || !AdrenalineEnabled(cp)) return 1f;
			try
			{
				if (AdrenalineModeActive(cp) || Adrenaline(cp) < AdrenalineMax(cp)) return 1f;
				if (_shielding != null && (int)_shielding.GetValue(cp) > 0) return 1f;
				float dr = (float)_getDr.Invoke(null, new object[] { cp });
				DrCuts++;
				return 1f - dr;
			}
			catch (Exception) { return 1f; }
		}

		private static bool _warned;
		private static void Warn(string what, Exception e) { if (_warned) return; _warned = true; Levy.Instance?.Logger.Warn("LEVYRIPPERS: " + what + " threw (once): " + e.Message); }
	}
}
