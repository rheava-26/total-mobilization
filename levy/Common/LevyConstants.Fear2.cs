namespace Levy.Common
{
	/// <summary>Fear module numbers that are not part of the nested <c>LevyConstants.Fear</c> table. Tiles unless the name says ticks.</summary>
	public static partial class LevyConstants
	{
		/// <summary>COMRADE_DOWN is barked by the nearest living soldiers within this many tiles of the fallen one.</summary>
		public const int FearComradeBarkTiles = 30;
		/// <summary>How many soldiers bark COMRADE_DOWN for one death (1 or 2).</summary>
		public const int FearComradeBarkCount = 2;
		/// <summary>How many soldiers bark ROUTED when the commander dies mid-fight.</summary>
		public const int FearRoutBarkCount = 3;
		/// <summary>SHAKEN barks name the most recently fallen comrade if the death was this recent.</summary>
		public const int FearFallenRecallTicks = 60 * 30;
		/// <summary>Fear changes of this size trigger a network refresh of the body so clients see the meter.</summary>
		public const float FearNetStep = 10f;
	}
}
