using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Team
{
	/// <summary>
	/// The commander as the DEFENDED ally of a levy's Paladin's Shield. In vanilla a wearer's shield puts the Paladin's Shield buff on every teammate within 800 px; a player under that buff takes 0.75 of each hit
	/// (ApplyVanillaHurtEffectModifiers) and the nearest wearer takes a third of it (OnHurt_Part2). Vanilla looks for wearers among the Players only, so a levy wearer was never found. Here the buff is on the commander (TeamItems pulse),
	/// and when he is hit under it with no Player wearer to share it, the damage is cut to 0.75 and the nearest eligible levy wearer takes the third (the same SoakFor the levies already use between themselves).
	/// </summary>
	public sealed class TeamPlayer : ModPlayer
	{
		private readonly List<int> _palNpcs = new();
		/// <summary>Self-check counters.</summary>
		public static int PaladinCuts, PaladinShares, PaladinRawShared;
		/// <summary>Lab counters: shares actually struck on levies, life they really lost (after their own defense), hits that had holders, damage of those hits, and shares that spawned a further split (must stay 0).</summary>
		public static int PaladinSoaks, PaladinHits, PaladinNested; public static long PaladinSoakDone, PaladinHitDmg;

		/// <summary>Vanilla's own test (Player.TeammateHasPalidinShieldAndCanTakeDamage): another Player on the team wears a shield above a quarter of his life, and is not in immunity.</summary>
		public static bool PlayerTeammateShields(Player p)
		{
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player o = Main.player[i];
				if (i != p.whoAmI && o.active && !o.dead && !o.immune && o.hasPaladinShield && o.team == p.team && o.statLife > o.statLifeMax2 * 0.25f) return true;
			}
			return false;
		}

		/// <summary>Every one of this commander's levies wearing a Paladin's Shield within 800 px, above a quarter of his life and out of immunity (the pool the commander's hit is shared across).</summary>
		public static List<SoldierNPC> FindLevyPaladins(Player p)
		{
			var res = new List<SoldierNPC>();
			foreach (SoldierNPC s in TeamItems.Levies(p))
			{
				if (!s.CanSoak) continue;
				if (Vector2.Distance(s.NPC.Center, p.Center) < LevyConstants.PaladinRangePx) res.Add(s);
			}
			return res;
		}

		public override void PostUpdateEquips()
		{
			if (!TeamSystem.Enabled || !TeamItems.Built || !ThoriumLoaded) return;
			try { ThoriumEquips(); }
			catch (Exception e) { TeamItems.WarnOnce("thorium commander equips", e); }
		}

		private static readonly bool ThoriumLoaded = ModLoader.HasMod("ThoriumMod");
		private void ThoriumEquips() { if (TeamThorium.Installed) TeamThorium.CommanderEquips(Player); } // a separate method: the only place the JIT-gated type is touched

		public override void ModifyHurt(ref Player.HurtModifiers modifiers)
		{
			_palNpcs.Clear();
			if (TeamNet.InShare || !TeamItems.IsLocal(Player) || !Player.defendedByPaladin || PlayerTeammateShields(Player)) return;
			var pals = FindLevyPaladins(Player);
			if (pals.Count == 0) return;
			modifiers.FinalDamage *= LevyConstants.PaladinAllyScale;
			foreach (var pal in pals) _palNpcs.Add(pal.NPC.whoAmI);
			PaladinCuts++;
		}

		public override void OnHurt(Player.HurtInfo info)
		{
			if (_palNpcs.Count == 0 || TeamNet.InShare) { _palNpcs.Clear(); return; }
			var idx = _palNpcs.ToArray(); _palNpcs.Clear();
			int total = Math.Max(idx.Length, (int)(info.Damage * LevyConstants.PaladinShareFraction)), each = total / idx.Length, extra = total % idx.Length; // split evenly: the total stays a third
			PaladinRawShared += total;
			for (int k = 0; k < idx.Length; k++) { TeamNet.RequestPaladinShare(Player, idx[k], each + (k < extra ? 1 : 0), info.DamageSource.SourceNPCIndex); PaladinShares++; }
		}
	}
}
