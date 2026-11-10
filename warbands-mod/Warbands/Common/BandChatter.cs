using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>
	/// What a band says over its heads. Levy's dialogue voice: sincere, messy, run-on, aimed at someone (a band mate by name, or the target), never a setup and punchline. Lines are chosen by event; {mate} is another living member's
	/// first name, {who} the player's name, {armor} the set the player wears (Shroomite, Turtle...), {n} a count. Shown as floating text over the speaker; each speaker and each band has a cool-down so a fight does not become a wall of text.
	/// </summary>
	public static class BandChatter
	{
		public static readonly Dictionary<string, string[]> Pool = new()
		{
			["ENGAGE"] = new[] {
				"Everyone stay behind {mate}, he's got the most armor and the least sense!",
				"{mate} you take the left and I'll take the right, which is the left from here, no, your left!",
				"If I die in this one I'm haunting {mate} specifically!",
				"Wait is that {who}?! Nobody told me it was THE {who}!",
				"Okay, okay, okay, nobody panic, {mate} is panicking enough for all of us!",
			},
			["WARN2"] = new[] {
				"That's far enough. I mean it, {mate}, tell them I mean it!",
				"One more step and we're doing this the loud way, and I hate the loud way!",
			},
			["SNAP"] = new[] {
				"That's it, that's it, weapons up, {mate} weapons UP!",
				"I said turn back! Why does nobody ever turn back!",
			},
			["CHIP"] = new[] {
				"I hit him! I definitely hit him! Why is he still standing, {mate}!",
				"{mate} my arrows are doing nothing, are we sure these are real arrows?!",
				"That's the third time I've hit him and he didn't even blink, he BLINKED at {mate}!",
				"Is he wearing the whole mountain?! My sword is bouncing, {mate}, it's bouncing!",
			},
			["ONETAP"] = new[] {
				"{mate} just fell over and nobody even touched him, that guy touched him ONCE!",
				"One hit! ONE! He had a name, and now it's just a pile!",
				"I've known {mate} nine years and he lasted four seconds, FOUR!",
			},
			["MATEDIED"] = new[] {
				"{mate}! Oh no, {mate}, I borrowed money from him!",
				"Somebody go tell {mate}'s mother, no, not me, she scares me!",
				"There's fewer of us now and I want it noted that I wanted to leave earlier!",
			},
			["PANIC"] = new[] {
				"Nope nope nope, I'm retiring, {mate} you're in charge now!",
				"I can't feel my courage, {mate}, did you take it?!",
				"Tell my mother I'm... actually don't tell her, she'll make me come back!",
				"This isn't what the poster said, {mate}, the poster said a nice job!",
			},
			["RECALL"] = new[] {
				"Recall potion! I bought two for exactly this, {mate}, you should've bought two!",
				"I'm so sorry, it's just that I have a recall potion and you don't!",
				"Drinking it, drinking it, tell {mate} I'll write!",
			},
			["ARMOR"] = new[] {
				"We're picking on the guy in {armor}?! Who approved this, {mate}?!",
				"He's in full {armor}, {mate}, I can see my own face in it, and I look scared!",
				"Nobody said anything about {armor}, {mate}, I would've stayed home!",
			},
			["LEVYMASS"] = new[] {
				"Are those all HIS?! {mate}, count them, no, don't count them!",
				"He brought the whole barracks! Who brings the whole barracks to a skirmish!",
				"Since when do people have little guys with guns, {mate}, who gave him little guys!",
			},
			["SUMMONER"] = new[] {
				"It's the summon guy! Get in close, {mate}, the summons turn around slow!",
				"Don't look at the minions, {mate}, look at him, he's the only one doing real work!",
			},
			["MELEE"] = new[] {
				"He's got a sword! Back, {mate}, BACK, keep the range, a sword can't reach!",
				"Stay away from the pointy end, {mate}, and shoot from way over here!",
			},
			["GUNNER"] = new[] {
				"He's got a gun too?! Get close, {mate}, guns hate it up close!",
				"Everybody in, {mate}, if he wants to shoot he has to shoot all of us!",
			},
			["CASTER"] = new[] {
				"A mage! {mate}, rush him, they run out of mana, they always run out of mana!",
			},
			["LOWHP"] = new[] {
				"I'm at like three percent, {mate}, somebody tell me the healer's on a break and not gone!",
				"Everything's red, {mate}, why is everything red!",
			},
			["HURT"] = new[] {
				"Ow! Who did that, {mate}, was that you?!",
				"That was MY leg, {mate}, that leg was mine!",
			},
			["KILLLEVY"] = new[] {
				"Got one! Wait, that was one of his, it still counts, {mate} it counts!",
				"He's dead! {mate} look, I did a thing, look at it!",
			},
			["WIN"] = new[] {
				"We... won? {mate}, did we actually win?!",
				"Nobody tell the others it was this easy, {mate}, they'll want a cut!",
			},
		};

		/// <summary>Per-type lines swapped in for ENGAGE (the type's own voice).</summary>
		public static readonly Dictionary<string, string[]> Flavor = new()
		{
			["Zealots"] = new[] { "The Mind wants you, {who}, and the Mind is patient!", "{mate}, say it with me, flesh is temporary, and so is HE!", "Pray, {mate}, and then aim!" },
			["Wardens"] = new[] { "This is private property, {who}, and {mate} has the deed!", "We've held this ground for years, {mate}, we're not stopping for a stranger!" },
			["Paladins"] = new[] { "In the name of whoever's in charge of this, STOP, {who}!", "Hold the line, {mate}, the light will provide, and if it doesn't, {mate}, run!" },
			["CalWulfrum"] = new[] { "We built these drones to get away from the war, {who}, and you brought another one!", "{mate}, scrap him, he's raising an army, that's how it starts!" },
			["CalYharim"] = new[] { "Veterans don't get to leave, {mate}, we just get quieter!", "Nothing left but the war, {who}, so come on then!" },
			["CalIlmeris"] = new[] { "Our kingdom held back an empire, {who}, we can hold back you!", "Remember the water, {mate}, remember what it did to them!" },
			["CalAzafure"] = new[] { "These are our masters' lands, {who}, nobody trespasses, {mate} tell him!", "Burn him, {mate}, the stones are on our side!" },
			["CalOnyx"] = new[] { "Everything has a price, {who}, and I'm about to name yours!", "{mate}, the balance is off and he's the reason!" },
			["CalCult"] = new[] { "The dragon will wake, {who}, and you will not be here to see it, {mate} chant louder!", "Sealed on the moon and still furious, {mate}, that's our whole job!" },
			["CalProfaned"] = new[] { "All of it ends in ash, {who}, even your good armor, {mate} isn't it lovely!", "Stand in the light, {mate}, it's the only fair thing left!" },
			["CalVernal"] = new[] { "Silva's grove, {who}, her champion walked these paths, {mate} remind him!", "Life holds the line here, {mate}, so hold it!" },
			["Rangers"] = new[] { "Quiet now, {mate}, he hasn't seen us, no, he's seen us, shoot!", "Keep your distance, keep your distance, {mate}, that's the whole job!" },
		};

		private static readonly Dictionary<int, uint> _speakerAt = new();
		private static uint _bandAt;

		public static string Pick(string key, BandLevyNPC speaker, string flavor, string who, string armor, int n)
		{
			string[] pool;
			if (key == "ENGAGE" && flavor != null && Flavor.TryGetValue(flavor, out var f) && Main.rand.NextBool(2)) pool = f;
			else if (!Pool.TryGetValue(key, out pool)) return null;
			string line = pool[Main.rand.Next(pool.Length)];
			string mate = speaker?.RandomMateName() ?? "mate";
			return line.Replace("{mate}", mate).Replace("{who}", who ?? "you").Replace("{armor}", armor ?? "plate").Replace("{n}", n.ToString());
		}

		/// <summary>Says a line for an event, if the speaker and the band are not on cool-down. Returns the text (or null).</summary>
		public static string Say(BandLevyNPC speaker, string key, string who = null, string armor = null, int n = 0, string literal = null, bool force = false)
		{
			if (speaker == null || !speaker.NPC.active || Main.netMode == NetmodeID.MultiplayerClient) return null;
			uint now = Main.GameUpdateCount;
			if (!force)
			{
				if (_speakerAt.TryGetValue(speaker.NPC.whoAmI, out uint t) && now - t < 60 * 7) return null;
				if (now - _bandAt < 60 * 2) return null;
			}
			string text = literal ?? Pick(key, speaker, speaker.Roster?.FlavorKey, who, armor, n);
			if (text == null) return null;
			_speakerAt[speaker.NPC.whoAmI] = now; _bandAt = now;
			Emit(speaker.NPC, text);
			Log.Add(key + ": " + text);
			return text;
		}

		/// <summary>Floating text over the speaker (single player and server; the server broadcasts it).</summary>
		public static void Emit(NPC npc, string text)
		{
			var color = new Color(240, 130, 110);
			var rect = new Rectangle((int)npc.Center.X, (int)npc.Top.Y - 20, 1, 1);
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.CombatTextString, -1, -1, NetworkText.FromLiteral(text), (int)color.PackedValue, rect.X, rect.Y);
			else
			{
				int i = CombatText.NewText(rect, color, text);
				if (i >= 0 && i < Main.combatText.Length) Main.combatText[i].lifeTime = 210;
			}
		}

		/// <summary>Lines said this session, newest last (self-check and lab read it).</summary>
		public static class Log { public static readonly List<string> Lines = new(); public static void Add(string s) { if (Lines.Count < 400) Lines.Add(s); } }

		/// <summary>The name of the set a player wears, from the body slot: "Shroomite Breastplate" gives "Shroomite".</summary>
		public static string ArmorSetName(Player p)
		{
			if (p == null) return null;
			var body = p.armor[1];
			if (body == null || body.IsAir) return null;
			string n = body.Name;
			foreach (var suf in new[] { " Breastplate", " Chainmail", " Scale Mail", " Plate Mail", " Mail", " Scalemail", " Shirt", " Armor", " Suit", " Vest", " Jacket", " Tunic", " Robe", " Plate" })
				if (n.EndsWith(suf, StringComparison.Ordinal)) { n = n.Substring(0, n.Length - suf.Length); break; }
			return n;
		}
	}
}
