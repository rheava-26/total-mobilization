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
	///   bit 4: Projectile.AI     (EVERY tick: measurement only, would break returning / homing-to-owner projectiles)
	///   bit 8: Thorium's cached "local ThoriumPlayer" (PlayerHelper.localThoriumPlayer) is pointed at the soldier dummy's ThoriumPlayer around the Shoot hooks in WeaponUse.TryRealShoot
	///          (GetThoriumPlayer returns that cache for any player whose whoAmI == Main.myPlayer, which the dummy is given for the call).
	/// Only the transform is moved: the Player object, its ModPlayers, buffs and life stay the commander's, so loot credit and boss-kill resolution still run under the real player.
	/// </summary>
	public class OwnerPuppet : ModSystem
	{
		internal static int Mask;
		internal static int Swaps, Restores; // counters for the harness

		public override void Load()
		{
			string v = Environment.GetEnvironmentVariable("LEVY_ONHIT_FIX");
			if (!string.IsNullOrEmpty(v) && int.TryParse(v, out int m)) Mask = m;
			if (Mask == 0) return;
			On_Projectile.Damage += DamageDetour;
			On_Projectile.Kill += KillDetour;
			On_Projectile.AI += AiDetour;
			Mod.Logger.Info("OWNERPUPPET: enabled mask=" + Mask);
		}

		public override void Unload()
		{
			if (Mask != 0) { On_Projectile.Damage -= DamageDetour; On_Projectile.Kill -= KillDetour; On_Projectile.AI -= AiDetour; }
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
			if ((Mask & bit) == 0 || !Puppetable(self, out Player p, out SoldierNPC body)) { orig(); return; }
			Vector2 pos = p.position, vel = p.velocity, oldPos = p.oldPosition; int dir = p.direction;
			Swaps++;
			try
			{
				p.Center = body.NPC.Center; p.oldPosition = p.position; p.velocity = body.NPC.velocity; p.direction = body.NPC.direction == 0 ? 1 : body.NPC.direction;
				orig();
			}
			finally { p.position = pos; p.velocity = vel; p.oldPosition = oldPos; p.direction = dir; Restores++; }
		}

		private static void DamageDetour(On_Projectile.orig_Damage orig, Projectile self) => Run(self, () => orig(self), 1);
		private static void KillDetour(On_Projectile.orig_Kill orig, Projectile self) => Run(self, () => orig(self), 2);
		private static void AiDetour(On_Projectile.orig_AI orig, Projectile self) => Run(self, () => orig(self), 4);

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
			return _setLocal != null && _tpl != null;
		}

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
				_setLocal.Invoke(null, new object[] { dummy.GetModPlayer(_tpl) });
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
