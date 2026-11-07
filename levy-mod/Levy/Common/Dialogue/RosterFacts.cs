using System;
using System.Collections.Generic;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Terraria;
using Terraria.ID;
using Levy.Common.Boss;
using Levy.Common.Officers;
using Levy.Common.Weapons;

namespace Levy.Common.Dialogue
{
	/// <summary>Roster-wide facts the chatter conditions and the {Tank} / {Glutton} placeholders read. The roster can be swapped for a fake one so the boot self-check can feed it.</summary>
	public static class RosterFacts
	{
		public const int TankMinDeaths = 3;
		public const int GluttonMinKills = 25;
		public const int FragileHp = 200;

		/// <summary>Set by the self-check only; null in play (the live roster is used).</summary>
		internal static IReadOnlyList<SoldierRecord> TestRoster;

		private static IReadOnlyList<SoldierRecord> Roster
		{
			get
			{
				if (TestRoster != null) return TestRoster;
				try { return RosterSystem.Instance?.Regulars; } catch (Exception) { return null; }
			}
		}

		// Test overrides (self-check only; null in play).
		internal static bool? TestHardmode, TestGoblin, TestBoss;
		internal static string TestOfficer, TestWorld;

		/// <summary>Tick the last main-boss fight ended (set by the roster when a boss dies).</summary>
		public static int BossEndTick = int.MinValue / 2;

		public static string WorldName() => TestWorld ?? Main.worldName;

		/// <summary>The role of a commissioned officer (e.g. "Painter"), or null when none is mobilised.</summary>
		public static string OfficerName()
		{
			if (TestOfficer != null) return TestOfficer;
			try
			{
				var list = OfficerSystem.Instance.ActiveBodies;
				for (int i = 0; i < list.Count; i++) if (list[i].TownNpc != null && list[i].TownNpc.active) return list[i].TownNpc.TypeName;
			}
			catch (Exception) { }
			return null;
		}

		public static bool BossFightOn()
		{
			if (TestBoss.HasValue) return TestBoss.Value;
			try
			{
				if ((uint)((int)Main.GameUpdateCount - BossEndTick) < 3600u) return true;
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n != null && n.active && n.life > 0 && BossRule.IsMainBoss(n)) return true; }
			}
			catch (Exception) { }
			return false;
		}

		/// <summary>A mage: a magic-class weapon in hand, else magic armor on his back.</summary>
		public static bool IsMage(SoldierRecord r)
		{
			var g = r?.Gear;
			if (g == null) return false;
			try
			{
				if (g.HasWeapon && WeaponClassifier.ItemDamageClass(g.Weapon) == DamageClass.Magic) return true;
				return ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs) == DamageClass.Magic;
			}
			catch (Exception) { return false; }
		}

		public static bool ArmyHasMage(SoldierRecord except)
		{
			var list = Roster;
			if (list == null) return false;
			for (int i = 0; i < list.Count; i++) if (!ReferenceEquals(list[i], except) && IsMage(list[i])) return true;
			return false;
		}

		private static bool Under20(SoldierRecord r)
		{
			if (r.Dead || r.Reserve) return false;
			if (r.HasBody) return Main.npc[r.BodyIndex].life < 20;
			return r.CurrentHp < 20;
		}

		/// <summary>A summoner: a summon-class weapon in hand, else summon armor on his back.</summary>
		public static bool IsSummoner(SoldierRecord r)
		{
			var g = r?.Gear;
			if (g == null) return false;
			try
			{
				if (g.HasWeapon && WeaponClassifier.ItemDamageClass(g.Weapon) == DamageClass.Summon) return true;
				return ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs) == DamageClass.Summon;
			}
			catch (Exception) { return false; }
		}

		public static bool Matches(LineCond cond, SoldierRecord speaker)
		{
			if (speaker == null) return false;
			if ((cond & LineCond.Summoner) != 0 && !IsSummoner(speaker)) return false;
			if ((cond & LineCond.NonSummonerWithSummoner) != 0 && (IsSummoner(speaker) || !ArmyHasSummoner(speaker))) return false;
			if ((cond & LineCond.Fragile) != 0 && !MostFragile()) return false;
			if ((cond & LineCond.Mage) != 0 && !IsMage(speaker)) return false;
			if ((cond & LineCond.NonMageWithMage) != 0 && (IsMage(speaker) || !ArmyHasMage(speaker))) return false;
			if ((cond & LineCond.HardmodeOfficer) != 0 && !((TestHardmode ?? Main.hardMode) && OfficerName() != null)) return false;
			if ((cond & LineCond.Goblin) != 0 && !(TestGoblin ?? (NPC.FindFirstNPC(NPCID.GoblinTinkerer) >= 0))) return false;
			if ((cond & LineCond.LowHp) != 0 && !Under20(speaker)) return false;
			if ((cond & LineCond.BossFight) != 0 && !BossFightOn()) return false;
			return true;
		}

		public static bool ArmyHasSummoner(SoldierRecord except)
		{
			var list = Roster;
			if (list == null) return false;
			for (int i = 0; i < list.Count; i++) if (!ReferenceEquals(list[i], except) && IsSummoner(list[i])) return true;
			return false;
		}

		/// <summary>True when more than half the regulars have under <see cref="FragileHp"/> max HP.</summary>
		public static bool MostFragile()
		{
			var list = Roster;
			if (list == null || list.Count == 0) return false;
			int low = 0;
			for (int i = 0; i < list.Count; i++) if (list[i].MaxHp < FragileHp) low++;
			return low * 2 > list.Count;
		}

		/// <summary>The levy with the most deaths (alive or dead), when it is at least 3 and not the speaker.</summary>
		public static SoldierRecord DeathLeader(SoldierRecord speaker)
		{
			var best = Best(r => r.Deaths);
			return best != null && best.Deaths >= TankMinDeaths && !ReferenceEquals(best, speaker) ? best : null;
		}

		/// <summary>The levy with the most kills, when it is at least 25 and not the speaker.</summary>
		public static SoldierRecord KillLeader(SoldierRecord speaker)
		{
			var best = Best(r => r.Kills);
			return best != null && best.Kills >= GluttonMinKills && !ReferenceEquals(best, speaker) ? best : null;
		}

		private static SoldierRecord Best(Func<SoldierRecord, int> score)
		{
			var list = Roster;
			if (list == null) return null;
			SoldierRecord best = null;
			for (int i = 0; i < list.Count; i++) if (best == null || score(list[i]) > score(best)) best = list[i];
			return best;
		}
	}
}
