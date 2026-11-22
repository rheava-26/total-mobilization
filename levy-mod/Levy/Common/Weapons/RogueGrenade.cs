using System;
using System.Reflection;
using Terraria;
using Levy.Common.Compat.Calamity;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// CALAMITY'S DOOMSDAY DEVICE, thrown as a player throws it. The grenade is wound up while the button is down (the first 70% of 70% of its 55-tick use animation, 27 ticks), then charged (charge 1 to 4 at 0.1 a tick,
	/// 30 ticks) and thrown when the button is let go. A full charge thrown on a full stealth bar is a stealth strike: it ricochets from enemy to enemy (up to 5 bounces) and its last hit is five times as strong.
	/// A full-charge throw that is not a strike leaves the bar alone and gives back 30% of it on its first hit; a throw let go before the charge is full empties the bar. Holding a full charge more than 18 ticks
	/// drains the bar. So a player always lets go on a full charge, and holds it for the few ticks the bar still needs when they are inside those 18. Read by reflection (no compile-time reference to Calamity).
	/// </summary>
	internal static class RogueGrenade
	{
		/// <summary>Calamity's grace: ticks a full charge can be held before the bar starts to drain.</summary>
		internal const int GraceTicks = 18;
		/// <summary>The held path's safety cap: a grenade still in the hand this long is let go whatever it reports (a full charge comes at about 57 ticks).</summary>
		internal const int CapTicks = 100;
		/// <summary>Lab switch (env LEVY_DOOMSDAY_START): "always" winds the next grenade up as soon as the hand is free; "projected" waits until the bar will be full by the time the charge is.</summary>
		internal static readonly bool StartAlways = Environment.GetEnvironmentVariable("LEVY_DOOMSDAY_START") != "projected";
		/// <summary>Lab switch (env LEVY_DOOMSDAY=0): the grenade is held a fixed time and released, as before this rule.</summary>
		internal static readonly bool On = Environment.GetEnvironmentVariable("LEVY_DOOMSDAY") != "0";
		/// <summary>Lab counters: throws let go on a full charge, of those the strikes, and the ticks held inside the grace for the bar.</summary>
		internal static int FullThrows, StrikeThrows, GraceWaits;

		private static Type _t; private static FieldInfo _flung, _full, _pen;

		internal static bool Is(Item item) => On && item?.ModItem != null && item.ModItem.Mod.Name == "CalamityMod" && item.ModItem.Name == "DoomsdayDevice";

		private static bool Bind(Projectile p)
		{
			if (p?.ModProjectile == null) return false;
			Type t = p.ModProjectile.GetType();
			if (t != _t)
			{
				const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				_flung = t.GetField("flung", F); _full = t.GetField("hasReachedFullCharge", F); _pen = t.GetField("stealthPenaltyTimer", F); _t = t;
			}
			return _flung != null && _full != null && _pen != null;
		}

		/// <summary>The grenade has left the hand.</summary>
		internal static bool Flung(Projectile p) { try { return Bind(p) && (bool)_flung.GetValue(p.ModProjectile); } catch (Exception) { return false; } }
		internal static bool FullCharge(Projectile p) { try { return Bind(p) && (bool)_full.GetValue(p.ModProjectile); } catch (Exception) { return false; } }
		private static int Penalty(Projectile p) { try { return Bind(p) ? (int)_pen.GetValue(p.ModProjectile) : 0; } catch (Exception) { return 0; } }

		/// <summary>Calamity's own gate on the next throw (DoomsdayDevice.CanUseItem): no grenade of this owner with localAI[1] below 5, which a thrown one reaches a few ticks into its flight.</summary>
		internal static bool HandFree(Projectile thrown) => thrown == null || !thrown.active || Flung(thrown) && thrown.localAI[1] >= 5f;

		/// <summary>Ticks from the start of a throw to a full charge: the wind-up (0.7 x 0.7 of the use animation) and 30 ticks of charging.</summary>
		internal static int ChargeTicks(Item item) => (int)Math.Ceiling(Math.Max(1, item.useAnimation) * 0.49f) + 30;

		/// <summary>
		/// The button is let go this tick: the charge is full and the bar is full (a stealth strike), or the bar will not be full inside the grace (a plain full-charge throw, which keeps the bar), or the grace is
		/// about to end. A levy with no stealth bar lets go on the full charge. <paramref name="heldTicks"/> past <see cref="CapTicks"/> lets go whatever.
		/// </summary>
		internal static bool ReleaseNow(SoldierNPC s, Projectile p, int heldTicks)
		{
			if (heldTicks > CapTicks) return true;
			if (!FullCharge(p)) return false;
			object cp = s?.Dummy != null ? LevyRippers.Of(s.Dummy) : null;
			if (cp == null || !LevyRippers.IsRogue(cp)) { FullThrows++; return true; }
			if (LevyRippers.StealthStrikeReady(cp)) { FullThrows++; StrikeThrows++; return true; }
			int left = GraceTicks - 1 - Penalty(p);
			if (left <= 0) { FullThrows++; return true; }
			float max = LevyRippers.StealthMax(cp);
			bool moving = Math.Abs(s.NPC.velocity.X) > 0.4f || Math.Abs(s.NPC.velocity.Y) > 0.8f;
			float perTick = max * LevyRippers.StealthGen(cp, moving) / 120f;
			if (perTick <= 0f || (max - LevyRippers.Stealth(cp)) / perTick > left) { FullThrows++; return true; } // the bar will not make it inside the grace: throw it plain (the bar is kept)
			GraceWaits++;
			return false;
		}

		/// <summary>
		/// The policy's start test for the next grenade: true to start winding it up now. "always": whenever the hand is free (a plain full-charge throw costs no stealth, so there is nothing to save).
		/// "projected": only when the bar will be full by the time the charge is (or the levy has no bar).
		/// </summary>
		internal static bool StartNow(SoldierNPC s, Item item, object cp)
		{
			if (StartAlways || cp == null || !LevyRippers.IsRogue(cp) || LevyRippers.StealthStrikeReady(cp)) return true;
			float max = LevyRippers.StealthMax(cp); if (max <= 0f) return true;
			bool moving = Math.Abs(s.NPC.velocity.X) > 0.4f || Math.Abs(s.NPC.velocity.Y) > 0.8f;
			float perTick = max * LevyRippers.StealthGen(cp, moving) / 120f;
			return LevyRippers.Stealth(cp) + perTick * (ChargeTicks(item) + GraceTicks - 2) >= max;
		}
	}
}

namespace Levy.Common.Weapons
{
	/// <summary>
	/// A levy's held projectile hits with the real player as its owner (HeldWeapons.RunDamage). A rogue projectile whose hit writes "the owner's" stealth bar (the Doomsday Device's plain hit gives back 30% of
	/// the bar) would fill the real player's bar and never the levy's: for the call the real player's stealth state is the levy's (<see cref="StealthGuard"/>), and what the hit did to it is the levy's.
	/// </summary>
	internal struct StealthLend
	{
		private StealthGuard.Scope _scope;
		internal static StealthLend Begin(SoldierNPC body)
		{
			var l = new StealthLend();
			try
			{
				int o = SoldierMinions.TrueMyPlayer;
				if (body?.Dummy == null || o < 0 || o >= Main.maxPlayers) return l;
				Player real = Main.player[o];
				if (real != null && real.active) l._scope = StealthGuard.Open(real, body);
			}
			catch (Exception) { }
			return l;
		}
		internal void End() => StealthGuard.Close(ref _scope);
	}
}
