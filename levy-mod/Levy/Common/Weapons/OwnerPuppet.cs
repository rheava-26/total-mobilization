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
	/// EXPERIMENT, OFF BY DEFAULT (turned on only by the harness env LEVY_ONHIT_FIX, a bit mask). Soldier shots are owned by the commander, so any weapon / mod code that reads
	/// <c>Main.player[projectile.owner]</c> during a hit (Center, position, direction) spawns its follow-up effects at the commander. This puppets the OWNER'S transform to the soldier's for the
	/// length of one vanilla call and restores it in a finally, in the same tick, before any net send or draw:
	///   bit 1: Projectile.Damage (the hit pass: ModProjectile / GlobalProjectile / GlobalNPC OnHit hooks and the strike)
	///   bit 2: Projectile.Kill   (OnKill effects: explosions, split shots)
	///   bit 4: Projectile.Update (EVERY tick of a soldier shot, AI + movement + the hit pass: modded AI runs through ProjectileLoader.ProjectileAI, not Projectile.AI; returning / homing-to-owner projectiles go home to the soldier)
	///   bit 16: a projectile that spawns INSIDE one of these puppeted calls with no soldier tag of its own (spawned from a hook with a player / misc entity source instead of a parent projectile) inherits the soldier's tag, like a child that names its parent
	///   bit 8: Thorium's cached "local ThoriumPlayer" (PlayerHelper.localThoriumPlayer) is pointed at the soldier dummy's ThoriumPlayer around the Shoot hooks in WeaponUse.TryRealShoot
	///          (GetThoriumPlayer returns that cache for any player whose whoAmI == Main.myPlayer, which the dummy is given for the call).
	/// Only the transform is moved: the Player object, its ModPlayers, buffs and life stay the commander's, so loot credit and boss-kill resolution still run under the real player.
	/// </summary>
	public class OwnerPuppet : ModSystem
	{
		internal static int Mask;
		internal static int Swaps, Restores, ThSwaps, Inherited; // counters for the harness
		/// <summary>The soldier whose projectile is inside a puppeted call right now (null otherwise).</summary>
		internal static SoldierNPC Context;
		/// <summary>True when bit 16 is on and a puppeted call is running: an untagged friendly projectile spawned now belongs to that soldier.</summary>
		internal static bool TryInherit(Projectile p, out SoldierNPC body) { body = Context; return (Mask & 16) != 0 && body != null && !p.hostile && p.owner < Main.maxPlayers; }

		public override void Load()
		{
			string v = Environment.GetEnvironmentVariable("LEVY_ONHIT_FIX");
			if (!string.IsNullOrEmpty(v) && int.TryParse(v, out int m)) Mask = m;
			if (Mask == 0) return;
			On_Projectile.Damage += DamageDetour;
			On_Projectile.Kill += KillDetour;
			On_Projectile.Update += UpdateDetour;
			Mod.Logger.Info("OWNERPUPPET: enabled mask=" + Mask);
		}

		public override void Unload()
		{
			if (Mask != 0) { On_Projectile.Damage -= DamageDetour; On_Projectile.Kill -= KillDetour; On_Projectile.Update -= UpdateDetour; }
			Mask = 0;
		}

		private static bool Puppetable(Projectile p, out Player owner, out SoldierNPC body)
		{
			owner = null; body = null;
			if (p.owner < 0 || p.owner >= Main.maxPlayers || !p.TryGetGlobalProjectile(out SoldierProjectileTag t) || !t.IsSoldierShot) return false;
			owner = Main.player[p.owner];
			if (!owner.active) return false;
			body = EnemyTargeting.LiveBody(t.SoldierIndex);
			return body != null && !ReferenceEquals(body.Dummy, owner);
		}

		private static void Run(Projectile self, Action orig, int bit)
		{
			// bit 8 (Thorium cache) rides along with every puppeted call: GetThoriumPlayer(Main.player[owner]) would otherwise resolve to the commander's ThoriumPlayer for a soldier's hit
			bool moveTransform = (Mask & bit) != 0, swapCache = (Mask & 8) != 0 && bit != 4, inherit = (Mask & 16) != 0;
			if (!(moveTransform || swapCache || inherit) || !Puppetable(self, out Player p, out SoldierNPC body)) { orig(); return; }
			Vector2 pos = p.position, vel = p.velocity, oldPos = p.oldPosition; int dir = p.direction;
			object th = swapCache ? SwapThorium(body.Dummy) : null;
			SoldierNPC prevCtx = Context; Context = body;
			if (moveTransform) Swaps++;
			try
			{
				if (moveTransform) { p.Center = body.NPC.Center; p.oldPosition = p.position; p.velocity = body.NPC.velocity; p.direction = body.NPC.direction == 0 ? 1 : body.NPC.direction; }
				orig();
			}
			finally { if (moveTransform) { p.position = pos; p.velocity = vel; p.oldPosition = oldPos; p.direction = dir; Restores++; } RestoreThorium(th); Context = prevCtx; }
		}

		private static void DamageDetour(On_Projectile.orig_Damage orig, Projectile self) => Run(self, () => orig(self), 1);
		private static void KillDetour(On_Projectile.orig_Kill orig, Projectile self) => Run(self, () => orig(self), 2);
		private static void UpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i) => Run(self, () => orig(self, i), 4);

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

		private static readonly object Marker = new();
		/// <summary>Points Thorium's local-player cache at the dummy's ThoriumPlayer (the dummy's whoAmI is Main.myPlayer for the call, which SetLocalThoriumPlayer requires). Returns a token for RestoreThorium; null when off / unavailable.</summary>
		internal static object SwapThorium(Player dummy)
		{
			if ((Mask & 8) == 0 || dummy == null) return null;
			try
			{
				if (!EnsureThorium()) return null;
				int w = dummy.whoAmI; dummy.whoAmI = Main.myPlayer; // SetLocalThoriumPlayer only accepts the player whose whoAmI == Main.myPlayer
				try { _setLocal.Invoke(null, new object[] { dummy.GetModPlayer(_tpl) }); ThSwaps++; } finally { dummy.whoAmI = w; }
				return Marker;
			}
			catch (Exception) { return null; }
		}

		/// <summary>Hands the cache back to the commander (Main.player[Main.myPlayer]).</summary>
		internal static void RestoreThorium(object token)
		{
			if (token == null || !EnsureThorium()) return;
			try
			{
				Player c = Main.player[Main.myPlayer];
				if (c != null && c.active) _setLocal.Invoke(null, new object[] { c.GetModPlayer(_tpl) });
			}
			catch (Exception) { }
		}
	}
}
