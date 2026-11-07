namespace Levy.Content.NPCs
{
	/// <summary>Why a body is standing still: the brain branch that decided (<see cref="WhyB"/>) and the movement branch that executed it (<see cref="WhyM"/>). One byte store each; the matrix harness samples them on near-stationary ticks.</summary>
	public partial class SoldierNPC
	{
		internal static class Why
		{
			// movement-side codes
			public const byte MNone = 0, MHook = 1, MDodgeHold = 2, MDodgePend = 3, MNoGoal = 4, MArrived = 5, MBlocked = 6, MBusyAir = 7;
			public static readonly string[] MNames = { "moving", "hook", "dodge-hold", "dodge-hop-wait", "no-goal", "arrived", "path-blocked", "air-apex" };
			// brain-side codes
			public const byte BUnk = 0, BFlee = 1, BRouted = 2, BFallBack = 3, BTalk = 4, BGrace = 5, BRetreat = 6, BStandoffNoStrafe = 7, BLedgeNoStep = 8, BOddThink = 9, BStrafe = 10, BNoLos = 11, BNoRoute = 12, BDeferred = 13,
				BMeleeAvoid = 14, BMeleeContact = 15, BPlatform = 16, BNoTarget = 17, BResupply = 18, BUnarmed = 19, BPerchSettle = 20, BAirStrafe = 21, BCantFight = 22, BPerch = 23, BApproach = 24, BHopWait = 25, BHoldSlot = 26;
			public static readonly string[] BNames = { "unknown", "flee", "routed", "autonomy-fallback", "talk-hold", "spawn-grace", "retreat", "strafe-suppressed-standoff", "strafe-no-safe-step", "strafe-odd-think", "strafe-goal", "no-line-debounce", "no-route-hold", "path-deferred",
				"melee-avoid-boss-hold", "melee-in-contact", "platform-climb", "no-target-order", "resupply", "unarmed", "perch-settle", "air-strafe", "cant-fight", "perch", "approach", "hop-wait", "hold-slot" };
		}
		internal byte WhyB, WhyM, BlockWhy;
		private static readonly string[] BlockNames = { "?", "goal-above", "door", "lava", "water", "overhang", "wall", "void-gap", "deep-drop" };
		/// <summary>Reason label for a near-stationary tick (harness telemetry).</summary>
		internal string StillReason()
		{
			byte m = WhyM, b = WhyB;
			string mn = m < Why.MNames.Length ? Why.MNames[m] : "m" + m, bn = b < Why.BNames.Length ? Why.BNames[b] : "b" + b;
			if (m == Why.MBlocked) mn += ":" + (BlockWhy < BlockNames.Length ? BlockNames[BlockWhy] : "?");
			return m == Why.MNone || m == Why.MNoGoal || m == Why.MArrived || m == Why.MBlocked || m == Why.MBusyAir ? mn + "/" + bn : mn;
		}
	}
}
