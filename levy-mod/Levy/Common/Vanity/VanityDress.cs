using Terraria;

namespace Levy.Common.Vanity
{
	/// <summary>
	/// Puts a <see cref="VanityLook"/> on a levy's dummy player: the head / body / legs pieces go into the SOCIAL armor slots (armor[10..12]), which is where vanilla's Player.PlayerFrame reads them to replace what is drawn,
	/// and the dyes into dye[0..2], which Player.UpdateDyes reads for the head / body / legs. The functional slots (armor[0..2]) are never touched, so nothing here can change a stat. Pure and player-only, so the
	/// boot self-check can run it on a scratch player.
	/// </summary>
	public static class VanityDress
	{
		public const int SocialHead = 10, SocialBody = 11, SocialLegs = 12;

		/// <summary>Fills the social slots with the look's pieces (a slot the look leaves empty gets <paramref name="blank"/>).</summary>
		public static void ApplySlots(Player d, VanityLook look, Item blank)
		{
			if (look == null || d == null) return;
			d.armor[SocialHead] = look.Head ?? blank;
			d.armor[SocialBody] = look.Body ?? blank;
			d.armor[SocialLegs] = look.Legs ?? blank;
		}

		/// <summary>Empties the social slots again, so no stat pass ever sees them.</summary>
		public static void ClearSlots(Player d, Item blank)
		{
			if (d == null) return;
			d.armor[SocialHead] = blank; d.armor[SocialBody] = blank; d.armor[SocialLegs] = blank;
		}

		/// <summary>
		/// Dye overrides. A vanity piece takes its row's dye (none if the row has none) so the real armor's dye does not tint it; a dye on a slot with no vanity piece dyes the real armor.
		/// Call after the dummy's dye slots were filled from its gear and before Player.UpdateDyes.
		/// </summary>
		public static void ApplyDyes(Player d, VanityLook look, Item blank)
		{
			if (look == null || d == null) return;
			for (int i = 0; i < 3; i++)
			{
				var dye = look.Items[VanitySet.DyeHead + i];
				if (look.Items[i] != null) d.dye[i] = dye ?? blank;
				else if (dye != null) d.dye[i] = dye;
			}
		}
	}
}
