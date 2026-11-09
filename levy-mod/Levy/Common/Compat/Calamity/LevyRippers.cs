using System;
using System.Linq;
using System.Reflection;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
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
		/// <summary>Rogue alignment counters: activations fired on a full stealth bar, fired by the hold timeout, stealth strikes seen, and how many of those landed inside a burning mode. Plus sound/text requests and booster copies.</summary>
		public static int RogueAligned, RogueTimeouts, Strikes, StrikesBoosted, FxRequests, BoosterCopies;
		/// <summary>A rogue holds a full bar this long (ticks) for its stealth to fill; then it fires anyway.</summary>
		public const int RogueHoldTicks = 10 * 60;
		/// <summary>The six permanent Rage / Adrenaline upgrades (Mushroom Plasma Root, Infernal Blood, Red Lightning Container, Electrolyte Gel Pack, Starlight Fuel Cell, Ectoheart).</summary>
		private static readonly string[] BoosterNames = { "rageBoostOne", "rageBoostTwo", "rageBoostThree", "adrenalineBoostOne", "adrenalineBoostTwo", "adrenalineBoostThree" };
		private static FieldInfo[] _boosters; private static FieldInfo _genStand, _genMove; private static FieldInfo _stealth, _stealthMax, _draedon, _rageSnd, _adrSnd, _nanoSnd; private static MethodInfo _strikeAvail;

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
				_boosters = new FieldInfo[BoosterNames.Length];
				for (int i = 0; i < BoosterNames.Length; i++) _boosters[i] = _cpT.GetField(BoosterNames[i], I);
				_stealth = _cpT.GetField("rogueStealth", I); _stealthMax = _cpT.GetField("rogueStealthMax", I); _draedon = _cpT.GetField("draedonsHeart", I); _genStand = _cpT.GetField("stealthGenStandstill", I); _genMove = _cpT.GetField("stealthGenMoving", I);
				_strikeAvail = _cpT.GetMethod("StealthStrikeAvailable", I, null, Type.EmptyTypes, null);
				_rageSnd = _cpT.GetField("RageActivationSound", S); _adrSnd = _cpT.GetField("AdrenalineActivationSound", S); _nanoSnd = _cpT.GetField("NanomachinesActivationSound", S);
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

		/// <summary>The commander's permanent upgrades, copied onto a levy's dummy (Calamity keeps them out of ResetEffects, so a set value stays). Returns true when something changed.</summary>
		public static bool CopyBoosters(Player commander, object levyCp)
		{
			Init(); if (!_ok || levyCp == null || commander == null || _boosters == null) return false;
			object src = Of(commander); if (src == null || ReferenceEquals(src, levyCp)) return false;
			bool changed = false;
			try
			{
				foreach (FieldInfo f in _boosters)
				{
					if (f == null) continue;
					bool v = (bool)f.GetValue(src);
					if ((bool)f.GetValue(levyCp) != v) { f.SetValue(levyCp, v); changed = true; }
				}
			}
			catch (Exception e) { Warn("CopyBoosters", e); }
			if (changed) BoosterCopies++;
			return changed;
		}

		/// <summary>Harness: set all six upgrades on a player. How many of the six a CalamityPlayer carries.</summary>
		public static void GrantBoosters(Player p) { Init(); object cp = Of(p); if (cp == null || _boosters == null) return; foreach (FieldInfo f in _boosters) f?.SetValue(cp, true); }
		public static int BoosterCount(object cp) { int n = 0; if (cp != null && _boosters != null) foreach (FieldInfo f in _boosters) if (f != null && (bool)f.GetValue(cp)) n++; return n; }

		/// <summary>A rogue (Calamity gave the dummy a stealth bar): max stealth above zero.</summary>
		public static bool IsRogue(object cp) { try { return cp != null && _stealthMax != null && (float)_stealthMax.GetValue(cp) > 0f; } catch (Exception) { return false; } }
		public static float Stealth(object cp) { try { return _stealth == null ? 0f : (float)_stealth.GetValue(cp); } catch (Exception) { return 0f; } }
		public static float StealthMax(object cp) { try { return _stealthMax == null ? 0f : (float)_stealthMax.GetValue(cp); } catch (Exception) { return 0f; } }
		/// <summary>Calamity's stealth regeneration speed for the player's gear: 1 fills the bar in two seconds standing still; moving it is halved (BalancingConstants.MovingStealthGenRatio) before the move multiplier.</summary>
		public static float StealthGen(object cp, bool moving)
		{
			try
			{
				FieldInfo f = moving ? _genMove : _genStand;
				float g = f == null ? 1f : (float)f.GetValue(cp);
				return Math.Max(0.05f, moving ? g * 0.5f : g);
			}
			catch (Exception) { return moving ? 0.5f : 1f; }
		}
		/// <summary>Calamity's own test: the bar holds enough for the next throw to be a stealth strike.</summary>
		public static bool StealthStrikeReady(object cp) { try { return _strikeAvail != null && (bool)_strikeAvail.Invoke(cp, null); } catch (Exception) { return false; } }

		/// <summary>Calamity's activation sound for the mode (kind 0 Rage, 1 Adrenaline, 2 Nanomachines with Draedon's Heart).</summary>
		public static byte SoundKind(object cp, bool adrenaline)
		{
			if (!adrenaline) return 0;
			try { return (byte)(_draedon != null && (bool)_draedon.GetValue(cp) ? 2 : 1); } catch (Exception) { return 1; }
		}

		/// <summary>Client: play that sound at the position through the army throttle.</summary>
		public static void PlaySound(byte kind, Vector2 pos)
		{
			Init(); if (!_ok || Main.dedServ) return;
			try
			{
				FieldInfo f = kind == 0 ? _rageSnd : kind == 2 ? _nanoSnd : _adrSnd;
				if (f == null) return;
				global::Levy.Common.Audio.ArmySoundThrottle.PlayAsArmy((SoundStyle)f.GetValue(null), pos);
			}
			catch (Exception e) { Warn("PlaySound", e); }
		}

		/// <summary>Server / single player: tell every client (or play here in single player) that this levy just switched a mode on.</summary>
		public static void Announce(global::Levy.Content.NPCs.SoldierNPC s, byte kind)
		{
			FxRequests++;
			if (Main.netMode == NetmodeID.SinglePlayer) PlaySound(kind, s.NPC.Center);
			else if (Main.netMode == NetmodeID.Server)
			{
				ModPacket p = global::Levy.Common.Net.LevyPackets.New(global::Levy.Common.Net.LevyPacket.RipperFx);
				p.Write((short)s.NPC.whoAmI); p.Write(kind); p.Send();
			}
		}

		internal static void OnFxPacket(BinaryReader r, int from)
		{
			short idx = r.ReadInt16(); byte kind = r.ReadByte();
			if (Main.netMode != NetmodeID.MultiplayerClient || idx < 0 || idx >= Main.maxNPCs || !Main.npc[idx].active) return;
			PlaySound(kind, Main.npc[idx].Center);
		}

		private static bool _warned;
		private static void Warn(string what, Exception e) { if (_warned) return; _warned = true; Levy.Instance?.Logger.Warn("LEVYRIPPERS: " + what + " threw (once): " + e.Message); }
	}

	public class LevyRippersNet : ModSystem
	{
		public override void Load() { if (ModLoader.HasMod("CalamityMod")) global::Levy.Common.Net.LevyPackets.Register(global::Levy.Common.Net.LevyPacket.RipperFx, LevyRippers.OnFxPacket); }
	}
}
