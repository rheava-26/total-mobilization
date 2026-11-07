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
	/// Fear and survivability options. Shipped defaults come from the config (Events fear, formation defense on); a dev env switch, or in the lab harness a spec token, overrides them for one run:
	///   LEVY_FEARMODEL = default (proximity) | proximity | events | proximity_slow | rally
	///   LEVY_SURV      = off | parity | veteran | formation   (comma list: parity,formation)
	///   LEVY_FORMATION = perAlly:cap  (e.g. 0.01:0.10; dev tuning of the formation numbers)
	/// Parity and veteran are dev switches only; they are never config options.
	/// </summary>
	public static class LevyOptions
	{
		private static FearModel? _fearOverride = ParseFearEnv(Environment.GetEnvironmentVariable("LEVY_FEARMODEL"));
		private static SurvMode? _survOverride = ParseSurvEnv(Environment.GetEnvironmentVariable("LEVY_SURV"));
		private static FearModel _fearConfig = FearModel.Events;
		private static bool _formationConfig = true;

		/// <summary>Formation numbers (per ally, cap). Constants unless the dev env LEVY_FORMATION overrides them.</summary>
		public static float FormationPerAlly = LevyConstants.Formation.DrPerAlly, FormationMax = LevyConstants.Formation.MaxDr;
		static LevyOptions()
		{
			string f = Environment.GetEnvironmentVariable("LEVY_FORMATION");
			if (!string.IsNullOrWhiteSpace(f))
			{
				string[] t = f.Split(':');
				if (t.Length == 2 && float.TryParse(t[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float a) && float.TryParse(t[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float c)) { FormationPerAlly = a; FormationMax = c; }
			}
		}

		/// <summary>The fear model in force: an env/harness override, else the config's. <see cref="FearModel.Default"/> is the proximity model.</summary>
		public static FearModel Fear { get => _fearOverride ?? _fearConfig; set => _fearOverride = value; }
		/// <summary>The survivability flags in force: an env/harness override, else formation when the config has it on.</summary>
		public static SurvMode Surv { get => _survOverride ?? (_formationConfig ? SurvMode.Formation : SurvMode.None); set => _survOverride = value; }

		/// <summary>Called from the config's Apply: the player's choices.</summary>
		public static void SetConfig(bool eventsFear, bool formation) { _fearConfig = eventsFear ? FearModel.Events : FearModel.Default; _formationConfig = formation; }

		private static FearModel? ParseFearEnv(string s) => string.IsNullOrWhiteSpace(s) ? null : ParseFear(s);
		private static SurvMode? ParseSurvEnv(string s) => string.IsNullOrWhiteSpace(s) ? null : ParseSurv(s);

		public static FearModel ParseFear(string s)
		{
			switch ((s ?? "").Trim().ToLowerInvariant().Replace("-", "_"))
			{
				case "events": return FearModel.Events;
				case "proximity": return FearModel.Default;
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
		/// <summary>True when parity or veteran is on (they change max HP and defense; formation does not).</summary>
		public static bool StatsOn => (Surv & (SurvMode.Parity | SurvMode.Veteran)) != 0;

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

		/// <summary>Formation: damage reduction fraction for a soldier with other living, un-routed levies close by (per ally, capped).</summary>
		public static float FormationDr(SoldierNPC s)
		{
			if ((Surv & SurvMode.Formation) == 0 || s.Record == null) return 0f;
			var roster = global::Levy.Common.Systems.RosterSystem.Instance;
			if (roster == null) return 0f;
			float r2 = LevyConstants.Formation.Tiles * LevyConstants.TilePx; r2 *= r2;
			int n = 0;
			foreach (SoldierNPC b in roster.ActiveBodies())
			{
				if (b == null || b.Record == null || ReferenceEquals(b, s) || !b.IsAlive || b.State == SoldierState.Routed) continue;
				if (Vector2.DistanceSquared(b.NPC.Center, s.NPC.Center) <= r2) n++;
			}
			return DrFor(n);
		}

		/// <summary>Formation damage reduction for a levy with <paramref name="allies"/> other levies close by.</summary>
		public static float DrFor(int allies) => Math.Min(FormationMax, allies * FormationPerAlly);
	}
}
