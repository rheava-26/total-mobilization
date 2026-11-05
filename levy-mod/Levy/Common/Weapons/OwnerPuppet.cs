using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Soldier shots are owned by the commander, so any weapon / mod code that reads <c>Main.player[projectile.owner]</c> during a hit (Center, position, direction) would spawn its follow-up effects at the commander.
	/// This puppets the OWNER'S transform to the soldier's for the length of one vanilla call and restores it in a finally, in the same tick:
	///   bit 1: Projectile.Damage (the hit pass: ModProjectile / GlobalProjectile / GlobalNPC OnHit hooks and the strike)
	///   bit 2: Projectile.Kill   (OnKill effects: explosions, split shots)
	///   bit 4: Projectile.Update (every tick of a soldier shot, AI + movement + the hit pass; returning / homing-to-owner projectiles go home to the soldier)
	///   bit 16: a friendly projectile that spawns INSIDE one of these puppeted calls with no soldier tag of its own inherits the soldier's tag
	///   bit 8: Thorium's cached "local ThoriumPlayer" (PlayerHelper.localThoriumPlayer) is pointed at the soldier dummy's ThoriumPlayer around the Shoot hooks in WeaponUse.TryRealShoot and inside puppeted Damage / Kill
	///          (GetThoriumPlayer returns that cache for any player whose whoAmI == Main.myPlayer, which the dummy is given for the call).
	/// Only the transform is moved: the Player object, its ModPlayers, buffs and life stay the commander's, so loot credit and boss-kill resolution still run under the real player.
	/// On by default through <c>LevyConfig.SoldierEffectsAtSoldier</c> (all five bits). The harness env <c>LEVY_ONHIT_FIX</c> (a bit mask) wins when set, so 0 measures the unfixed behaviour.
	/// Safety: every swap restores in a finally. Calls nest (a child projectile killed or updated inside a puppeted pass) through a saved-state stack, so each exit restores exactly what its own entry saw.
	/// The owner must be a real active player and the body a live soldier. Player-state net messages (4, 13) sent while a swap is open go out with the real transform.
	/// </summary>
	public class OwnerPuppet : ModSystem
	{
		/// <summary>Effective bit mask, read on the hot path. Written only by <see cref="Refresh"/>.</summary>
		internal static int Mask = 31;
		private static bool _envSet; private static int _envMask; private static bool _cfgOn = true; private static bool _hooked;
		internal static int Swaps, Restores, ThSwaps, Inherited, NetGuards, MaxDepth; // counters for the harness
		/// <summary>The soldier whose projectile is inside a puppeted call right now (null otherwise).</summary>
		internal static SoldierNPC Context;
		/// <summary>True when bit 16 is on and a puppeted call is running: an untagged friendly projectile spawned now belongs to that soldier.</summary>
		internal static bool TryInherit(Projectile p, out SoldierNPC body) { body = Context; return (Mask & 16) != 0 && body != null && !p.hostile && p.owner < Main.maxPlayers; }

		/// <summary>Called by the config on load / change with the cached server-side switch.</summary>
		internal static void SetConfig(bool on) { _cfgOn = on; Refresh(); }
		private static void Refresh() => Mask = _envSet ? _envMask : (_cfgOn ? 31 : 0);

		public override void Load()
		{
			string v = Environment.GetEnvironmentVariable("LEVY_ONHIT_FIX");
			_envSet = !string.IsNullOrEmpty(v) && int.TryParse(v, out _envMask);
			Refresh();
			if (_envSet && _envMask == 0) return; // the harness asked for the unfixed behaviour: no detours at all
			On_Projectile.Damage += DamageDetour;
			On_Projectile.Kill += KillDetour;
			On_Projectile.Update += UpdateDetour;
			On_NetMessage.SendData += SendDataDetour;
			_hooked = true;
			Mod.Logger.Info("OWNERPUPPET: enabled mask=" + Mask + (_envSet ? " (env override)" : " (config)"));
		}

		public override void Unload()
		{
			if (_hooked) { On_Projectile.Damage -= DamageDetour; On_Projectile.Kill -= KillDetour; On_Projectile.Update -= UpdateDetour; On_NetMessage.SendData -= SendDataDetour; }
			_hooked = false; _envSet = false; _cfgOn = true; Mask = 31; _depth = 0; Context = null; _curTp = null;
		}

		private static bool Puppetable(Projectile p, out Player owner, out SoldierNPC body)
		{
			owner = null; body = null;
			if (p.owner < 0 || p.owner >= Main.maxPlayers || !p.TryGetGlobalProjectile(out SoldierProjectileTag t) || !t.IsSoldierShot) return false;
			owner = Main.player[p.owner];
			if (owner == null || !owner.active || owner.whoAmI != p.owner) return false; // a real, active player only
			body = EnemyTargeting.LiveBody(t.SoldierIndex);
			return body != null && body.NPC.active && !ReferenceEquals(body.Dummy, owner);
		}

		/// <summary>One open swap: what the owner's transform and the Thorium cache were when this call entered.</summary>
		private struct Frame { public bool Moved; public Player P; public Vector2 Pos, Vel, Old; public int Dir; public object Th; public SoldierNPC PrevCtx; }
		private const int MaxFrames = 32;
		private static readonly Frame[] _stack = new Frame[MaxFrames];
		private static int _depth;

		private static bool Enter(Projectile self, int bit)
		{
			// bit 8 (Thorium cache) rides along with Damage / Kill: GetThoriumPlayer(Main.player[owner]) would otherwise resolve to the commander's ThoriumPlayer for a soldier's hit
			int m = Mask;
			bool moveTransform = (m & bit) != 0, swapCache = (m & 8) != 0 && bit != 4, inherit = (m & 16) != 0;
			if (!(moveTransform || swapCache || inherit) || _depth >= MaxFrames || !Puppetable(self, out Player p, out SoldierNPC body)) return false;
			ref Frame f = ref _stack[_depth];
			f.Moved = moveTransform; f.P = p; f.Pos = p.position; f.Vel = p.velocity; f.Old = p.oldPosition; f.Dir = p.direction;
			f.PrevCtx = Context; f.Th = swapCache ? SwapThorium(body.Dummy) : null;
			_depth++; if (_depth > MaxDepth) MaxDepth = _depth;
			Context = body;
			if (moveTransform) { Swaps++; p.Center = body.NPC.Center; p.oldPosition = p.position; p.velocity = body.NPC.velocity; p.direction = body.NPC.direction == 0 ? 1 : body.NPC.direction; }
			return true;
		}

		private static void Exit()
		{
			if (_depth <= 0) return;
			ref Frame f = ref _stack[--_depth];
			try { if (f.Moved) { f.P.position = f.Pos; f.P.velocity = f.Vel; f.P.oldPosition = f.Old; f.P.direction = f.Dir; Restores++; } }
			finally { RestoreThorium(f.Th); Context = f.PrevCtx; f = default; }
		}

		private static void DamageDetour(On_Projectile.orig_Damage orig, Projectile self)
		{
			if (Mask == 0 || !Enter(self, 1)) { orig(self); return; }
			try { orig(self); } finally { Exit(); }
		}
		private static void KillDetour(On_Projectile.orig_Kill orig, Projectile self)
		{
			if (Mask == 0 || !Enter(self, 2)) { orig(self); return; }
			try { orig(self); } finally { Exit(); }
		}
		private static void UpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			if (Mask == 0 || !Enter(self, 4)) { orig(self, i); return; }
			try { orig(self, i); } finally { Exit(); }
		}

		/// <summary>Player-state messages (4 SyncPlayer, 13 PlayerControls) carry the player's position. If one is sent while that player's transform is puppeted (a hook calling NetMessage.SendData), send it with the real transform.</summary>
		private static void SendDataDetour(On_NetMessage.orig_SendData orig, int msgType, int remoteClient, int ignoreClient, Terraria.Localization.NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)
		{
			if (_depth == 0 || (msgType != 4 && msgType != 13)) { orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7); return; }
			int first = -1;
			for (int k = 0; k < _depth; k++) if (_stack[k].Moved && _stack[k].P.whoAmI == number) { first = k; break; }
			if (first < 0) { orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7); return; }
			ref Frame b = ref _stack[first]; Player p = b.P;
			Vector2 pos = p.position, vel = p.velocity, old = p.oldPosition; int dir = p.direction;
			NetGuards++;
			p.position = b.Pos; p.velocity = b.Vel; p.oldPosition = b.Old; p.direction = b.Dir;
			try { orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7); }
			finally { p.position = pos; p.velocity = vel; p.oldPosition = old; p.direction = dir; }
		}

		// ---- Thorium local-player cache: PlayerHelper.SetLocalThoriumPlayer (Thorium's own API; reflection so Levy has no compile-time Thorium dependency) ----
		private static MethodInfo _setLocal; private static ModPlayer _tpl; private static bool _tried;
		private static bool EnsureThorium()
		{
			if (!_tried)
			{
				_tried = true;
				try
				{
					if (ModLoader.TryGetMod("ThoriumMod", out Mod th))
					{
						_setLocal = th.Code.GetType("ThoriumMod.Utilities.PlayerHelper")?.GetMethod("SetLocalThoriumPlayer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
						foreach (ModPlayer m in ModContent.GetContent<ModPlayer>()) if (m.Mod == th && m.GetType().Name == "ThoriumPlayer") _tpl = m;
					}
				}
				catch (Exception) { }
			}
			if (!_logged) { _logged = true; ModContent.GetInstance<OwnerPuppet>()?.Mod.Logger.Info($"OWNERPUPPET: thorium cache api found={_setLocal != null} playerType found={_tpl != null}"); }
			return _setLocal != null && _tpl != null;
		}
		private static bool _logged;

		/// <summary>HARNESS ONLY: a real client's local player update calls SetLocalThoriumPlayer(commander); the stand-in commander on a dedicated server never does, so do it here.</summary>
		internal static void EmulateClientCache(Player commander)
		{
			try { if (EnsureThorium()) _setLocal.Invoke(null, new object[] { commander.GetModPlayer(_tpl) }); } catch (Exception) { }
		}

		private sealed class ThToken { public ModPlayer Prev; }
		/// <summary>The ModPlayer this class last installed in Thorium's cache (null = nothing of ours installed, the commander's own is there).</summary>
		private static ModPlayer _curTp;

		/// <summary>Points Thorium's local-player cache at the dummy's ThoriumPlayer (the dummy's whoAmI is Main.myPlayer for the call, which SetLocalThoriumPlayer requires). Returns a token for RestoreThorium; null when off / unavailable.</summary>
		internal static object SwapThorium(Player dummy, bool force = false)
		{
			if ((!force && (Mask & 8) == 0) || dummy == null) return null;
			try
			{
				if (!EnsureThorium()) return null;
				ModPlayer tp = dummy.GetModPlayer(_tpl);
				int w = dummy.whoAmI; dummy.whoAmI = Main.myPlayer; // SetLocalThoriumPlayer only accepts the player whose whoAmI == Main.myPlayer
				try { _setLocal.Invoke(null, new object[] { tp }); ThSwaps++; } finally { dummy.whoAmI = w; }
				var tok = new ThToken { Prev = _curTp }; _curTp = tp;
				return tok;
			}
			catch (Exception) { return null; }
		}

		/// <summary>Puts the cache back to what it was before the matching SwapThorium: an enclosing soldier's when nested, else the commander's.</summary>
		internal static void RestoreThorium(object token)
		{
			if (token is not ThToken tok || !EnsureThorium()) return;
			try
			{
				ModPlayer target = tok.Prev;
				if (target == null) { Player c = Main.player[Main.myPlayer]; if (c != null && c.active) target = c.GetModPlayer(_tpl); }
				if (target != null)
				{
					Player o = target.Player; int w = o.whoAmI; bool fix = w != Main.myPlayer; if (fix) o.whoAmI = Main.myPlayer;
					try { _setLocal.Invoke(null, new object[] { target }); } finally { if (fix) o.whoAmI = w; }
				}
			}
			catch (Exception) { }
			finally { _curTp = tok.Prev; }
		}
	}
}
