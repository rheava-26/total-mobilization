using Levy.Common.Soldiers;
using Levy.Content.NPCs;

namespace Levy.Common.Dialogue
{
	/// <summary>Dialogue table, triggers, placeholders, bark budget, delivery channels. OWNED BY THE DIALOGUE AGENT.</summary>
	public static class DialogueSystem
	{
		/// <summary>Deliver a line for a trigger through its default channel, subject to the squad budget.</summary>
		public static void Bark(SoldierNPC s, DialogueTrigger trigger, DialogueContext ctx = null) { }
		/// <summary>A filled line for a trigger (or null if none). Used for chat windows and death messages.</summary>
		public static string PickLine(DialogueTrigger trigger, SoldierRecord rec, DialogueContext ctx = null) => null;
		/// <summary>Right-click chat text for a soldier.</summary>
		public static string ChatLine(SoldierNPC s) => "...";
		/// <summary>Chat-log notice (payday, muster, deaths...). Always delivered.</summary>
		public static void ChatLog(DialogueTrigger trigger, SoldierRecord rec, DialogueContext ctx = null) { }
		public static void Emote(SoldierNPC s, int emoteId) { }
		public static void FloatingText(SoldierNPC s, string text) { }
		/// <summary>Random soldier name from vanilla town-NPC name lists.</summary>
		public static string RandomSoldierName(bool male) => male ? "Andrew" : "Molly";
		/// <summary>Random settlement name.</summary>
		public static string RandomTownName() => "Fort Nowhere";
	}
}
