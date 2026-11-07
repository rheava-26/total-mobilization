using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common.Soldiers;
using Levy.Content.NPCs;

namespace Levy.Common.Options
{
	/// <summary>How the per-soldier fear meter is driven. <see cref="Default"/> is the shipped model (proximity gain every tick while a main boss is in view).</summary>
	public enum FearModel : byte { Default = 0, Events = 1, ProximitySlow = 2, Rally = 3 }

	/// <summary>Survivability options (flags: any combination). <see cref="None"/> is the shipped behaviour.</summary>
	[Flags] public enum SurvMode : byte { None = 0, Parity = 1, Veteran = 2, Formation = 4 }

	/// <summary>
	/// DESIGN OPTIONS LAB. Every option here is OFF by default and chosen by an env switch (or, in the lab harness, by a spec token that overrides the env for one fight):
	///   LEVY_FEARMODEL = default | events | proximity_slow | rally
	///   LEVY_SURV      = off | parity | veteran | formation   (comma list: parity,formation)
	/// With neither set, nothing in this file changes any number the game computes (every hook starts with a cheap static check).
	/// </summary>
	public static class LevyOptions
	{
		public static FearModel Fear = ParseFear(Environment.GetEnvironmentVariable("LEVY_FEARMODEL"));
		public static SurvMode Surv = ParseSurv(Environment.GetEnvironmentVariable("LEVY_SURV"));

		public static FearModel ParseFear(string s)
		{
			switch ((s ?? "").Trim().ToLowerInvariant().Replace("-", "_"))
			{
				case "events": return FearModel.Events;
				case "proximity_slow": case "slow": return FearModel.ProximitySlow;
				case "rally": return FearModel.Rally;
				default: return FearModel.Default;
			}
		}

		public static SurvMode ParseSurv(string s)
		{
			SurvMode m = SurvMode.None;
			foreach (string t in (s ?? "").Split(new[] { ',', '+', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				switch (t.ToLowerInvariant())
				{
					case "parity": m |= SurvMode.Parity; break;
					case "veteran": m |= SurvMode.Veteran; break;
					case "formation": m |= SurvMode.Formation; break;
				}
			return m;
		}

		public static bool SurvOn => Surv != SurvMode.None;

		// ================================================================================================= fear
		/// <summary>Scale on the per-tick boss-in-view fear gain (1 = shipped).</summary>
		public static float ProximityGainScale => Fear switch { FearModel.Events => 0f, FearModel.ProximitySlow => LevyConstants.Lab.SlowGainScale, _ => 1f };

		// ================================================================================================= survivability
		/// <summary>Harness-only: a fixed (max HP, defense) stand-in for "what the commander has" (the lab commander is a pinned 500-HP dummy). null = read the real players.</summary>
		public static (int hp, int def)? HarnessParity;

		private static uint _parityTick = uint.MaxValue; private static int _parityHp, _parityDef;
		private static void RefreshParity()
		{
			if (HarnessParity.HasValue) { _parityHp = HarnessParity.Value.hp; _parityDef = HarnessParity.Value.def; _parityTick = uint.MaxValue; return; }
			uint now = Main.GameUpdateCount;
			if (_parityTick == now) return;
			_parityTick = now;
			int hp = 0, def = 0;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p == null || !p.active || p.ghost) continue;
				hp = Math.Max(hp, p.statLifeMax2); def = Math.Max(def, p.statDefense);
			}
			_parityHp = hp; _parityDef = def;
		}

		/// <summary>Max HP the option gives this record: parity floors it at the commander's own max life, veteran adds a percentage for grade and boss fights survived. Regulars only; capped by the caller.</summary>
		public static int AdjustMaxHp(SoldierRecord r, int rawHp)
		{
			if (r.Kind != SoldierKind.Regular) return rawHp;
			int hp = rawHp;
			if ((Surv & SurvMode.Parity) != 0) { RefreshParity(); if (_parityHp > hp) hp = _parityHp; }
			if ((Surv & SurvMode.Veteran) != 0) hp = (int)Math.Round(hp * (1f + VeteranHpFrac(r)));
			return hp;
		}

		public static int AdjustDefense(SoldierNPC s, int def)
		{
			if (s.Record == null || s.Record.Kind != SoldierKind.Regular) return def;
			if ((Surv & SurvMode.Parity) != 0) { RefreshParity(); if (_parityDef > def) def = _parityDef; }
			if ((Surv & SurvMode.Veteran) != 0) def += Math.Min(LevyConstants.Lab.VetMaxFights, s.Record.BossFightsSurvived) / LevyConstants.Lab.VetFightsPerDefense;
			return def;
		}

		public static float VeteranHpFrac(SoldierRecord r)
		{
			int fights = Math.Min(LevyConstants.Lab.VetMaxFights, Math.Max(0, r.BossFightsSurvived));
			float f = fights * LevyConstants.Lab.VetHpPerFight;
			f += r.Grade == Grade.Champion ? LevyConstants.Lab.VetChampionHp : r.Grade == Grade.ManAtArms ? LevyConstants.Lab.VetManAtArmsHp : 0f;
			return f;
		}

		/// <summary>Formation: damage reduction fraction for a soldier with other living, un-routed levies close by (1% per neighbour, capped).</summary>
		public static float FormationDr(SoldierNPC s)
		{
			if ((Surv & SurvMode.Formation) == 0 || s.Record == null) return 0f;
			var roster = global::Levy.Common.Systems.RosterSystem.Instance;
			if (roster == null) return 0f;
			float r2 = LevyConstants.Lab.FormationTiles * LevyConstants.TilePx; r2 *= r2;
			int n = 0;
			foreach (SoldierNPC b in roster.ActiveBodies())
			{
				if (b == null || b.Record == null || ReferenceEquals(b, s) || !b.IsAlive || b.State == SoldierState.Routed) continue;
				if (Vector2.DistanceSquared(b.NPC.Center, s.NPC.Center) <= r2) n++;
			}
			return Math.Min(LevyConstants.Lab.FormationMaxDr, n * LevyConstants.Lab.FormationDrPerNeighbour);
		}
	}
}
