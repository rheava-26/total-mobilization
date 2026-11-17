using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Compat.Calamity;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// A chat line to the commander when a permanent upgrade he just used is one Levy also gives his army. Today that is Calamity's six Rage and Adrenaline upgrades (Mushroom Plasma Root, Infernal Blood,
	/// Red Lightning Container, Electrolyte Gel Pack, Starlight Fuel Cell, Ectoheart): <see cref="LevyRippers.CopyBoosters"/> copies them onto every levy. Upgrades Levy does not share (life and mana fruit,
	/// Calamity's other permanent items) say nothing. Server and single player. It watches the same flags the sharing reads, so a line means the levies really get it: one line per flag that turns on,
	/// none for a flag already on when the player is first seen (a loaded character is not "using" anything), and none while he has no levies.
	/// </summary>
	public class SharedUpgradeNotice : ModSystem
	{
		private struct Seen { public bool Init; public int Mask, Ticks; }
		private static readonly Seen[] Last = new Seen[Main.maxPlayers];
		private const int SettleTicks = 120, ScanTicks = 6;

		/// <summary>Self-check telemetry: every line sent since boot, with the player slot it went to.</summary>
		internal static readonly List<(int Player, int Booster, string Text)> Sent = new();

		public override void OnWorldLoad() => Reset();
		public override void OnWorldUnload() => Reset();
		internal static void Reset() { Array.Clear(Last); }

		public override void PostUpdateEverything()
		{
			if (LevyUtil.IsClient || Main.GameUpdateCount % ScanTicks != 0) return;
			try { Scan(); }
			catch (Exception) { /* a notice must never throw into the game loop */ }
		}

		/// <summary>One pass over the players (internal so the world check can run it on demand).</summary>
		internal static void Scan()
		{
			if (!LevyRippers.Active) return;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (!p.active) { Last[i] = default; continue; }
				object cp = LevyRippers.Of(p);
				if (cp == null) continue;
				int mask = LevyRippers.BoosterMask(cp);
				ref Seen s = ref Last[i];
				if (!s.Init) { s = new Seen { Init = true, Mask = mask }; continue; }
				int risen = mask & ~s.Mask;
				s.Mask = mask;
				if (s.Ticks < SettleTicks) { s.Ticks += ScanTicks; continue; }
				if (risen == 0 || !HasArmy(p)) continue;
				for (int b = 0; b < LevyRippers.BoosterSlots; b++)
					if ((risen & (1 << b)) != 0) Announce(p, b);
			}
		}

		private static bool HasArmy(Player p) => RosterSystem.Instance != null && RosterSystem.Instance.RegularsOf(p).Any();

		private static void Announce(Player p, int booster)
		{
			string text = Line(booster);
			if (string.IsNullOrEmpty(text)) return;
			Sent.Add((p.whoAmI, booster, text));
			RosterSystem.SendToPlayer(p, text);
		}

		/// <summary>The line for one upgrade: its own name and what it adds, in Calamity's numbers.</summary>
		internal static string Line(int booster)
		{
			if ((uint)booster >= LevyRippers.BoosterSlots) return null;
			string name = ModContent.TryFind("CalamityMod", LevyRippers.BoosterItems[booster], out ModItem mi) ? mi.DisplayName.Value : LevyRippers.BoosterItems[booster];
			return booster < 3
				? LevyUtil.T("Talk.SharedRage", name, Math.Round(LevyRippers.RageSecondsPerBooster, 1))
				: LevyUtil.T("Talk.SharedAdrenaline", name, Math.Round(LevyRippers.AdrenalineDamagePerBooster * 100f), Math.Round(LevyRippers.AdrenalineDrPerBooster * 100f));
		}
	}
}
