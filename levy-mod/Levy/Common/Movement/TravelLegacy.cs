namespace Levy.Common.Movement
{
	/// <summary>Lab switch only (set by the travel lab from LEVY_TRAVEL_LEGACY=1): turns the long-trip fixes off so the same build can be run as its own "before". Always false in play.</summary>
	public static class TravelLegacy { public static bool On; }
}
