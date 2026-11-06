using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using ThoriumMod;
using ThoriumMod.Items.Misc;
using Levy.Common.Compat.Thorium;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Team
{
	/// <summary>
	/// Thorium's worn-item team effects that read the OTHER players, made to count the commander's levies:
	/// <list type="bullet">
	/// <item>Bard accessories shared by nearby bards (<c>ThoriumPlayer.HandleNearbyPlayers</c>, 800 px): Conductor's Stick, the Subwoofers and the music-player levels. One detour on that routine runs for the commander's own Player and for every levy's equip frame; after Thorium's pass over the real
	/// players, the levies within 800 px of the wearer are added, so a levy's Subwoofer reaches the commander and the other levies, and the commander's reaches the levies (the real-player pass already did that).</item>
	/// <item>Ring of Unity (+1 flat damage, +1 more per other wearer within 400 px): the commander counts his levies who wear one, and a levy counts his squadmates.</item>
	/// <item>Thorium's life recovery (the heal-over-time most of its healer buffs add through <c>ThoriumPlayer.lifeRecovery</c>): a Player turns it into HP in a routine the levy's dummy never runs, so the gear frame turns it into the levy's own regeneration.</item>
	/// </list>
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class TeamThorium
	{
		public static bool Installed { get; private set; }
		/// <summary>Self-check counters.</summary>
		public static int SharedFlags, UnityLevies, RecoveryUnits;
		private static MethodInfo _handle;
		private const float NearbySq = 640000f, UnitySq = 160000f;

		public static void Install()
		{
			if (Installed) return;
			_handle = typeof(ThoriumPlayer).GetMethod("HandleNearbyPlayers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ?? throw new MissingMethodException("ThoriumPlayer.HandleNearbyPlayers");
			MonoModHooks.Add(_handle, new Action<Action<ThoriumPlayer>, ThoriumPlayer>(HandleNearbyDetour));
			Installed = true;
		}

		private static void HandleNearbyDetour(Action<ThoriumPlayer> orig, ThoriumPlayer self)
		{
			orig(self);
			try { ShareFromLevies(self); }
			catch (Exception e) { TeamItems.WarnOnce("bard accessory share", e); }
		}

		/// <summary>The levy whose equip frame is running, when <paramref name="p"/> is his dummy; null for a real player.</summary>
		private static SoldierNPC FrameBody(Player p) => GearUpkeep.Body != null && GearUpkeep.Body.Dummy == p ? GearUpkeep.Body : null;

		private static bool Provides(ThoriumPlayer t) => t.accConductorStickBard || t.accSubwoofer || t.accSubwooferFire || t.accSubwooferTerrarium || t.musicPlayerShared.Count > 0;

		private static void ShareFromLevies(ThoriumPlayer self)
		{
			if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) return; // a client has no levy dummies: the flags are only known where the levies are simulated
			Player me = self.Player;
			SoldierNPC frame = FrameBody(me);
			Player commander = frame != null ? TeamItems.CommanderOf(frame) : me;
			if (commander == null) return;
			foreach (SoldierNPC s in TeamItems.Levies(commander))
			{
				if (s == frame || s.Dummy == null || Vector2.DistanceSquared(s.NPC.Center, me.Center) > NearbySq) continue;
				ThoriumPlayer o = s.Dummy.GetModPlayer<ThoriumPlayer>();
				if (!Provides(o)) continue;
				Absorb(self, o);
			}
		}

		/// <summary>The same assignments Thorium's HandleNearbyPlayers makes for one nearby ally who wears the bard accessories.</summary>
		private static void Absorb(ThoriumPlayer self, ThoriumPlayer o)
		{
			if (o.accConductorStickBard && !self.accConductorStickAlly) { self.accConductorStickAlly = true; SharedFlags++; }
			if (o.accSubwoofer && !self.accSubwoofer) { self.accSubwoofer = true; SharedFlags++; }
			if (o.accSubwooferFire) { if (!self.accSubwooferFire) SharedFlags++; self.accSubwooferFire = true; self.Player.buffImmune[Terraria.ID.BuffID.OnFire] = true; }
			if (o.accSubwooferTerrarium && !self.accSubwooferTerrarium) { self.accSubwooferTerrarium = true; SharedFlags++; }
			foreach (var kv in o.musicPlayerShared)
			{
				self.musicPlayerLevels.TryGetValue(kv.Key, out int mine);
				o.musicPlayerLevels.TryGetValue(kv.Key, out int theirs);
				self.musicPlayerLevels[kv.Key] = Math.Max(mine, theirs);
			}
		}

		// ------------------------------------------------------------------------------------------------------------
		// Ring of Unity
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>The commander's real Player, after his accessories: each levy of his wearing a Ring of Unity within 400 px adds the ring's per-player damage.</summary>
		public static void CommanderEquips(Player p)
		{
			if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) return;
			ThoriumPlayer t = p.GetModPlayer<ThoriumPlayer>();
			if (!t.brother) return;
			int n = 0;
			foreach (SoldierNPC s in TeamItems.Levies(p))
				if (s.Dummy != null && s.Dummy.GetModPlayer<ThoriumPlayer>().brother && Vector2.DistanceSquared(s.NPC.Center, p.Center) < UnitySq) n++;
			if (n > 0) { p.GetDamage(DamageClass.Generic).Flat += n * RingofUnity.DamagePerPlayer; UnityLevies += n; }
		}

		/// <summary>A levy's equip frame, right after his accessories and the mods' post-equip hooks: his Ring of Unity counts his squadmates (the commander is a real Player Thorium already counts), and Thorium's life recovery becomes his regeneration.</summary>
		public static void LevyEquips(SoldierNPC s, Player d, ref int regenUnits)
		{
			ThoriumPlayer t = d.GetModPlayer<ThoriumPlayer>();
			if (t.brother)
			{
				Player c = TeamItems.CommanderOf(s);
				int n = 0;
				if (c != null)
					foreach (SoldierNPC m in TeamItems.Levies(c))
						if (m != s && m.Dummy != null && m.Dummy.GetModPlayer<ThoriumPlayer>().brother && Vector2.DistanceSquared(m.NPC.Center, s.NPC.Center) < UnitySq) n++;
				if (n > 0) { d.GetDamage(DamageClass.Generic).Flat += n * RingofUnity.DamagePerPlayer; UnityLevies += n; }
			}
			if (t.lifeRecovery > 0)
			{
				// ThoriumPlayer: every LifeRecoveryInterval ticks (60 by default) it heals lifeRecovery HP; the levy's regeneration is in vanilla lifeRegen units (2 units = 1 HP a second).
				int units = (int)Math.Round(2f * t.lifeRecovery * 60f / Math.Max(1, t.LifeRecoveryInterval));
				regenUnits += units; RecoveryUnits = units;
			}
		}
	}
}
