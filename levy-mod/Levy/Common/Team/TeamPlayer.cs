using System;
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
		private int _palNpc = -1;
		/// <summary>Self-check counters.</summary>
		public static int PaladinCuts, PaladinShares;

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

		/// <summary>The nearest of this commander's levies wearing a Paladin's Shield within 800 px, above a quarter of his life and out of immunity; null when none.</summary>
		public static SoldierNPC FindLevyPaladin(Player p)
		{
			SoldierNPC best = null; float bestDist = LevyConstants.PaladinRangePx;
			foreach (SoldierNPC s in TeamItems.Levies(p))
			{
				if (!s.CanSoak) continue;
				float d = Vector2.Distance(s.NPC.Center, p.Center);
				if (d < bestDist) { bestDist = d; best = s; }
			}
			return best;
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
			_palNpc = -1;
			if (TeamNet.InShare || !TeamItems.IsLocal(Player) || !Player.defendedByPaladin || PlayerTeammateShields(Player)) return;
			SoldierNPC pal = FindLevyPaladin(Player);
			if (pal == null) return;
			modifiers.FinalDamage *= LevyConstants.PaladinAllyScale;
			_palNpc = pal.NPC.whoAmI;
			PaladinCuts++;
		}

		public override void OnHurt(Player.HurtInfo info)
		{
			int idx = _palNpc; _palNpc = -1;
			if (idx < 0 || TeamNet.InShare) return;
			int share = Math.Max(1, (int)(info.Damage * LevyConstants.PaladinShareFraction));
			TeamNet.RequestPaladinShare(Player, idx, share, info.DamageSource.SourceNPCIndex);
			PaladinShares++;
		}
	}
}
