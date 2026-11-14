using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Owner
{
	/// <summary>
	/// What a ModPlayer of another mod would see when a levy's dummy is dispatched to it: two stand-in ModPlayers, one given the Hidden view and one the Local view (<see cref="IdentityGuard.Forced"/>), record
	/// at every state hook whether the Player they were handed is "the local player" (<c>whoAmI == Main.myPlayer</c>), whether <c>Main.LocalPlayer</c> is a dummy, and what talk NPC the local player reads as.
	/// The identity world check reads the counters; outside a check they do nothing.
	/// </summary>
	public abstract class IdentityProbeBase : ModPlayer
	{
		public static bool On;
		/// <summary>Per view: [0] dispatches for a dummy, [1] the dummy was the local player (whoAmI == myPlayer), [2] Main.LocalPlayer was a dummy, [3] the Player's talk NPC differed from the real local player's, [4] dispatches for a real player, [5] a real player was NOT the local player at a dispatch for it.</summary>
		protected static void Note(long[] c, Player p)
		{
			if (!On || p == null) return;
			if (SoldierOwner.IsDummy(p))
			{
				c[0]++;
				if (p.whoAmI == Main.myPlayer) c[1]++;
				if (SoldierOwner.IsDummy(Main.LocalPlayer)) c[2]++;
				Player real = Weapons.SoldierMinions.TrueMyPlayer is int r && r >= 0 && r < Main.maxPlayers ? Main.player[r] : null;
				if (real != null && real != p && Main.LocalPlayer.talkNPC != real.talkNPC) c[3]++;
			}
			else { c[4]++; if (p.whoAmI != Main.myPlayer && p.whoAmI == 0) c[5]++; }
		}
	}

	public sealed class IdentityProbeHidden : IdentityProbeBase
	{
		public static readonly long[] Seen = new long[6];
		public override void ResetEffects() => Note(Seen, Player);
		public override void PreUpdateBuffs() => Note(Seen, Player);
		public override void PostUpdateBuffs() => Note(Seen, Player);
		public override void UpdateEquips() => Note(Seen, Player);
		public override void PostUpdateEquips() => Note(Seen, Player);
		public override void PostUpdateMiscEffects() => Note(Seen, Player);
	}

	public sealed class IdentityProbeGear : IdentityProbeBase
	{
		public static readonly long[] Seen = new long[6];
		public override void ResetEffects() => Note(Seen, Player);
		public override void PreUpdateBuffs() => Note(Seen, Player);
		public override void PostUpdateBuffs() => Note(Seen, Player);
		public override void UpdateEquips() => Note(Seen, Player);
		public override void PostUpdateEquips() => Note(Seen, Player);
		public override void PostUpdateMiscEffects() => Note(Seen, Player);
	}
}
