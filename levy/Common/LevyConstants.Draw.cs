using Microsoft.Xna.Framework;

namespace Levy.Common
{
	public static partial class LevyConstants
	{
		/// <summary>Numbers used while drawing soldier bodies (client only).</summary>
		public static class Draw
		{
			/// <summary>The Shaken glow is the body hitbox grown by this many pixels on every side.</summary>
			public const int ShakenTintInflatePx = 6;
			/// <summary>Colour of the Shaken glow drawn behind the body (premultiplied: the multiplier is the opacity).</summary>
			public static readonly Color ShakenTint = Color.OrangeRed * 0.25f;
			/// <summary>Fake item-animation ticks used to keep a ranged weapon shouldered between shots while engaging.</summary>
			public const int RangedPoseHoldAnimation = 2;
		}
	}
}
