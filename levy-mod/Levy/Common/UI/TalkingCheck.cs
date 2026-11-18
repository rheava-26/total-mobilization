using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Dialogue;
using Levy.Common.Net;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// Boot-time self-check of "talking to levies": the chat-bar queue limits, the Close up stance, the levy inventory swap rules (including hand locks against the Armory),
	/// the new packets, and the dialogue file. Logs one summary line (or the failures); inspect-and-log only, touches no world state, never throws.
	/// </summary>
	public class TalkingCheck : ModSystem
	{
		private static Item Make(int type) { var it = new Item(); it.SetDefaults(type); return it; }

		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(PostSetupRecipesCore);

		private void PostSetupRecipesCore()
		{
			var failures = new List<string>();
			int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			void Guard(string name, Action body)
			{
				try { body(); }
				catch (Exception e) { failures.Add($"{name} threw {e.GetType().Name}: {e.Message}"); }
			}
			Guard("chat bar queue", () => CheckQueue(Check));
			Guard("chat bar draw rules", () => CheckBarTiming(Check));
			Guard("chat bar wrapping", () => CheckWrap(Mod, Check));
			Guard("close up", () => CheckCloseUp(Check));
			Guard("inventory rules", () => CheckInventory(Check));
			Guard("armory locks", () => CheckArmoryLocks(Check));
			Guard("packets", () => CheckPackets(Check));
			Guard("dialogue file", () => CheckDialogueFile(Mod, Check));
			try
			{
				if (failures.Count == 0) Mod.Logger.Info($"Levy talking self-check: {ok} checks OK");
				else
				{
					Mod.Logger.Warn($"Levy talking self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!");
					foreach (var f in failures) Mod.Logger.Warn("Levy talking self-check FAILED: " + f);
				}
			}
			catch (Exception) { }
		}

		// ------------------------------------------------------------------ chat bar queue
		private static void CheckQueue(Action<bool, string> check)
		{
			// 30 chatterers in one tick at distances 1..30: only MaxVisible get bars, the waiting list is capped, the NEAREST win the slots.
			{
				var q = new ChatBarQueue();
				var dist = new Dictionary<int, float>();
				for (int i = 1; i <= 30; i++) { dist[i] = 31 - i; q.Enqueue(i, "line " + i, 1, 0, dist[i]); } // speaker 30 is nearest (1 tile), speaker 1 farthest (30)
				q.Tick(1, s => dist[s]);
				check(q.Active.Count == LevyConstants.ChatBarMaxVisible, $"a burst shows exactly {LevyConstants.ChatBarMaxVisible} bars (got {q.Active.Count})");
				check(q.Waiting.Count <= LevyConstants.ChatBarMaxWaiting, $"waiting lines are capped (got {q.Waiting.Count})");
				bool nearest = true;
				foreach (var e in q.Active) if (e.Speaker < 26) nearest = false; // the five nearest are speakers 26..30
				check(nearest, "the five nearest speakers win the slots of a burst");
			}
			// A big line bumps a chatter bar once that bar has been readable for ChatBarMinShowTicks, and not before.
			{
				var q = new ChatBarQueue();
				var dist = new Dictionary<int, float>();
				for (int i = 1; i <= LevyConstants.ChatBarMaxVisible; i++) { dist[i] = 5 + i; q.Enqueue(i, "chatter " + i, 1, 0, dist[i]); }
				q.Tick(1, s => dist.TryGetValue(s, out var d) ? d : 40f);
				dist[99] = 60f; // far away, but a boss sighting
				q.Enqueue(99, "A boss", LevyConstants.ChatBarPriorityBig, 5, dist[99]);
				q.Tick(6, s => dist[s]);
				bool early = q.Active.Exists(e => e.Speaker == 99);
				q.Tick(1 + LevyConstants.ChatBarMinShowTicks, s => dist[s]);
				bool later = q.Active.Exists(e => e.Speaker == 99);
				check(!early, "a big line does not bump a bar that is still being read");
				check(later && q.Active.Count == LevyConstants.ChatBarMaxVisible, "a big line takes a slot from a chatter bar once it has been up long enough, without exceeding the limit");
			}
			// A chatter line never bumps a bigger one; the nearest waiting line takes the next free slot.
			{
				var q = new ChatBarQueue();
				var dist = new Dictionary<int, float>();
				string longLine = new string('n', 200); // stays up for the whole test
				for (int i = 1; i <= LevyConstants.ChatBarMaxVisible; i++) { dist[i] = 5; q.Enqueue(i, longLine, LevyConstants.ChatBarPriorityNotice, 0, 5); }
				q.Tick(1, s => dist[s]);
				dist[50] = 40f; dist[51] = 8f;
				q.Enqueue(50, "far", 1, 2, 40f); q.Enqueue(51, "near", 1, 2, 8f);
				q.Tick(200, s => dist[s]);
				check(q.Active.Count == LevyConstants.ChatBarMaxVisible && !q.Active.Exists(e => e.Speaker == 50 || e.Speaker == 51), "chatter does not bump notices and the limit holds");
				q.Active.RemoveAt(0); // a bar ends
				q.Tick(201, s => dist[s]);
				check(q.Active.Exists(e => e.Speaker == 51) && !q.Active.Exists(e => e.Speaker == 50), "the nearest waiting line takes the free slot, not the farther one");
			}
			// One bar per speaker: a weaker line is refused while the old one types; a stronger one replaces it; a bar that has been read may be replaced by anything.
			{
				var q = new ChatBarQueue();
				q.Enqueue(1, "a long enough first line to still be typing", LevyConstants.ChatBarPriorityNotice, 0, 3f);
				q.Tick(1, s => 3f);
				check(!q.Enqueue(1, "weak", 1, 2, 3f), "a weaker line is refused while the speaker's bar is still typing");
				check(q.Enqueue(1, "strong", LevyConstants.ChatBarPriorityBig, 3, 3f) && q.Active.Count == 1 && q.Active[0].Text == "strong", "a stronger line replaces the speaker's bar");
				int read = 3 + q.Active[0].ReadTicks;
				check(q.Enqueue(1, "after", 1, read, 3f) && q.Active.Count == 1 && q.Active[0].Text == "after", "a bar that has been read is replaced by any new line");
			}
			// A long line is never cut off by the same speaker's next line: the next one waits until the first has been read, then takes the bar.
			{
				var q = new ChatBarQueue();
				string first = "Commander, I am telling you, and I want this on the record, that the slime from earlier was looking at me funny and nobody believes me!";
				q.Enqueue(1, first, 1, 0, 3f);
				q.Tick(1, s => 3f);
				var bar = q.Active[0];
				int typed = 1 + bar.RevealTicks + 1;
				q.Enqueue(1, "Another thing!", 1, typed, 3f);
				q.Tick(typed, s => 3f);
				check(q.Active.Count == 1 && q.Active[0].Text == first, "the speaker's next line does not replace a long bar that has finished typing but not been read");
				int readAt = 1 + bar.ReadTicks;
				check(bar.Alpha(readAt - 1) == 1f, "the bar is fully shown for its whole reading time");
				q.Tick(readAt, s => 3f);
				check(q.Active.Count == 1 && q.Active[0].Text == "Another thing!", "once the first line has been read, the waiting line takes the speaker's bar");
			}
			// Range, empty text, vanished speaker, stale waiting line.
			{
				var q = new ChatBarQueue();
				check(!q.Enqueue(1, "far", 1, 0, LevyConstants.ChatBarRangeTiles + 1f), "a speaker beyond range gets no bar");
				check(!q.Enqueue(1, "", 1, 0, 3f), "empty text gets no bar");
				q.Enqueue(2, "gone", 1, 0, 3f); q.Tick(1, s => -1f);
				check(q.Active.Count == 0 && q.Waiting.Count == 0, "a speaker that vanished loses the line");
				var full = new ChatBarQueue(); var d2 = new Dictionary<int, float>();
				string longLine = new string('x', 200);
				for (int i = 1; i <= LevyConstants.ChatBarMaxVisible; i++) { d2[i] = 3; full.Enqueue(i, longLine, 2, 0, 3); }
				full.Tick(1, s => 3f);
				full.Enqueue(77, "late", 1, 2, 3f);
				full.Tick(2 + LevyConstants.ChatBarWaitExpireTicks + 1, s => 3f);
				check(!full.Waiting.Exists(e => e.Speaker == 77) && !full.Active.Exists(e => e.Speaker == 77) && full.Dropped > 0, "a waiting line that never got a slot goes stale and is dropped");
			}
			// The trigger-to-priority map: boss and fear moments outrank chatter.
			check(ChatBars.PriorityOf((byte)DialogueTrigger.BOSS_SIGHTED) > ChatBars.PriorityOf((byte)DialogueTrigger.ENGAGE), "a boss sighting outranks combat chatter");
			check(ChatBars.PriorityOf((byte)DialogueTrigger.COMRADE_DOWN) > ChatBars.PriorityOf((byte)DialogueTrigger.KILL), "a comrade down outranks a kill quip");
			check(ChatBars.PriorityOf(byte.MaxValue) == LevyConstants.ChatBarPriorityChatter, "an unlabelled line is chatter");
		}

		private static void CheckBarTiming(Action<bool, string> check)
		{
			var e = new ChatBarEntry { Text = new string('x', 60), Shown = 100, Priority = 1 };
			check(e.RevealedChars(100) >= 1 && e.RevealedChars(100) < 10, "the typewriter starts with the first characters");
			int prev = 0; bool mono = true;
			for (int t = 100; t < 100 + e.RevealTicks + 5; t++) { int r = e.RevealedChars(t); if (r < prev) mono = false; prev = r; }
			check(mono && e.RevealedChars(100 + e.RevealTicks + 1) == e.Text.Length, "the typewriter only moves forward and ends on the full text");
			check(e.Alpha(100) == 1f && e.Alpha(100 + e.LifeTicks - 1) > 0f && e.Alpha(100 + e.LifeTicks) == 0f, "the bar is opaque while shown, fades, then ends");
			check(e.LifeTicks > e.RevealTicks, "the bar stays after the last character types");
		}

		// ------------------------------------------------------------------ chat bar wrapping
		/// <summary>
		/// The wrap the bars draw with (ChatWrap.Fit, the same call as ChatBars), measured with an estimate of the mouse font (7 px a character at scale 1, 9 for wide letters) since a server has no fonts.
		/// Every dialogue line in the table, with a long name in each slot, keeps every character; very long lines and long words are kept too; reading time grows with length.
		/// </summary>
		private static void CheckWrap(Mod mod, Action<bool, string> check)
		{
			float sc = LevyConstants.ChatBarTextScale;
			Func<string, float> measure = s => { float w = 0f; foreach (char c in s) w += (c == 'W' || c == 'M' || c == 'm' || c == 'w') ? 9f : 7f; return w * sc; };
			string Fill(string t) => System.Text.RegularExpressions.Regex.Replace(t ?? "", "\\{[^}]*\\}", "Bartholomew-Featherstonehaugh");
			string NoSpace(string t) => new string((t ?? "").Where(ch => !char.IsWhiteSpace(ch)).ToArray());
			int lines = 0, lost = 0, tooWide = 0, maxRows = 0; string worst = null;
			foreach (var kv in DialogueTable.Lines)
				foreach (var l in kv.Value)
				{
					string text = Fill(l.Text); if (text.Length == 0) continue;
					lines++;
					var rows = ChatWrap.Fit(text, measure, LevyConstants.ChatBarTextWidth, LevyConstants.ChatBarWideTextWidth, LevyConstants.ChatBarPreferredRows, out float w);
					if (NoSpace(string.Concat(rows)) != NoSpace(text)) { lost++; worst ??= text; }
					if (w > LevyConstants.ChatBarWideTextWidth + 0.5f) tooWide++;
					maxRows = Math.Max(maxRows, rows.Count);
				}
			check(lines > 500, $"the dialogue table is loaded for the wrap check ({lines} lines)");
			check(lost == 0, $"every dialogue line keeps every character on its bar ({lost} of {lines} lost text; first: {worst})");
			check(tooWide == 0, $"no bar is wider than {LevyConstants.ChatBarWideTextWidth} px of text ({tooWide})");
			mod.Logger.Info($"Levy chat bar wrap: OBSERVED {lines} dialogue lines with long names, at most {maxRows} rows on a bar");

			// the old wrap kept 3 rows of 230 px and dropped the rest; these keep all of it
			string longLine = string.Join(" ", Enumerable.Repeat("Rigroth the Undying would NOT support sentry staves and neither would I,", 8));
			var lr = ChatWrap.Fit(longLine, measure, LevyConstants.ChatBarTextWidth, LevyConstants.ChatBarWideTextWidth, LevyConstants.ChatBarPreferredRows, out float lw);
			check(NoSpace(string.Concat(lr)) == NoSpace(longLine) && lr.Count > LevyConstants.ChatBarPreferredRows && lw <= LevyConstants.ChatBarWideTextWidth + 0.5f, $"a {longLine.Length}-character line keeps every word, on {lr.Count} rows at most {LevyConstants.ChatBarWideTextWidth} px wide");
			var mid = ChatWrap.Fit(new string('a', 5) + " " + string.Join(" ", Enumerable.Repeat("abcdefg", 18)), measure, LevyConstants.ChatBarTextWidth, LevyConstants.ChatBarWideTextWidth, LevyConstants.ChatBarPreferredRows, out float mw);
			check(mid.Count <= LevyConstants.ChatBarPreferredRows && mw > LevyConstants.ChatBarTextWidth, $"a line too long for 3 rows at {LevyConstants.ChatBarTextWidth} px widens the bar instead of adding a 4th row ({mid.Count} rows, {mw:0} px)");
			var shortRows = ChatWrap.Fit("Hi!", measure, LevyConstants.ChatBarTextWidth, LevyConstants.ChatBarWideTextWidth, LevyConstants.ChatBarPreferredRows, out float sw);
			check(shortRows.Count == 1 && shortRows[0] == "Hi!" && sw < 40f, "a short line is one row and the bar is sized to it");
			string word = new string('Z', 90);
			var wr = ChatWrap.Wrap("A " + word + " B", measure, LevyConstants.ChatBarTextWidth);
			bool rowsFit = wr.All(r => measure(r) <= LevyConstants.ChatBarTextWidth + 0.5f);
			check(NoSpace(string.Concat(wr)) == NoSpace("A " + word + " B") && rowsFit, "a word wider than a row is split across rows, nothing lost");
			check(ChatWrap.Wrap("", measure, 100).Count == 0 && ChatWrap.Wrap("   ", measure, 100).Count == 0, "blank text wraps to no rows");
			check(ChatWrap.Wrap("one\\ntwo", measure, 400).Count == 1 && ChatWrap.Wrap("one\ntwo", measure, 400).Count == 2, "a line break in the text starts a new row");

			// reading time: proportional to length, and every bar lives at least its reading time
			check(ChatWrap.ReadTicks(200) > ChatWrap.ReadTicks(100) && ChatWrap.ReadTicks(100) > ChatWrap.ReadTicks(20), "reading time grows with the line");
			var e200 = new ChatBarEntry { Text = new string('x', 200), Shown = 0 };
			var e20 = new ChatBarEntry { Text = new string('x', 20), Shown = 0 };
			check(e200.LifeTicks >= 200 * 60 / 16 && e200.LifeTicks > e200.RevealTicks + LevyConstants.ChatBarHoldBaseTicks, $"a 200-character bar stays long enough to read at 16 characters a second ({e200.LifeTicks} ticks)");
			check(e20.LifeTicks >= 120 && e20.LifeTicks < e200.LifeTicks, $"a 20-character bar stays at least two seconds and less than a long one ({e20.LifeTicks} ticks)");
			// the typewriter reaches the last row of a wrapped line before the bar starts to fade
			var rowsT = ChatWrap.Fit(longLine, measure, LevyConstants.ChatBarTextWidth, LevyConstants.ChatBarWideTextWidth, LevyConstants.ChatBarPreferredRows, out _);
			var eL = new ChatBarEntry { Text = longLine, Shown = 0 };
			int total = 0; foreach (var r in rowsT) total += r.Length;
			check(total <= longLine.Length && eL.RevealedChars(eL.RevealTicks) >= total && eL.RevealTicks < eL.ReadTicks, "the typewriter shows every row of a long line before the bar fades");
		}

		// ------------------------------------------------------------------ Close up
		private static void CheckCloseUp(Action<bool, string> check)
		{
			check(Formation.Next(FormationMode.Normal) == FormationMode.Spread && Formation.Next(FormationMode.Spread) == FormationMode.CloseUp && Formation.Next(FormationMode.CloseUp) == FormationMode.Normal, "the stance button cycles Normal, Spread out, Close up, Normal");
			check(Formation.Majority(10, 6, 0) == FormationMode.Spread && Formation.Majority(10, 0, 6) == FormationMode.CloseUp && Formation.Majority(10, 5, 5) == FormationMode.Normal && Formation.Majority(0, 0, 0) == FormationMode.Normal, "the squad stance is the strict majority");
			var rec = new SoldierRecord { Id = 1, Name = "T" };
			Formation.Apply(rec, FormationMode.Spread);
			check(rec.SpreadOut && !rec.CloseUp, "Spread out clears Close up");
			Formation.Apply(rec, FormationMode.CloseUp);
			check(!rec.SpreadOut && rec.CloseUp && Formation.Of(rec) == FormationMode.CloseUp, "Close up clears Spread out");
			// the stance survives a save and a network snapshot
			var back = SoldierRecord.Load(rec.Save());
			check(back.CloseUp && !back.SpreadOut, "Close up is saved with the soldier");
			using var ms = new MemoryStream();
			using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) rec.WriteSnapshot(w);
			ms.Position = 0;
			using var r = new BinaryReader(ms);
			var snap = SoldierRecord.ReadSnapshot(r);
			check(snap.CloseUp && !snap.SpreadOut && snap.Id == 1 && ms.Position == ms.Length, "Close up rides the roster snapshot and the snapshot reads back exactly");
			// the numbers: Close up is tighter than normal in every way the brain uses
			check(LevyConstants.CloseUpMult < 1f && LevyConstants.CloseUpChaseLimit < LevyConstants.FollowChaseLimit, "Close up shrinks the dispersion gaps and the chase leash");
			check(LevyConstants.CloseUpFollowStartMoveTiles < LevyConstants.FollowDistance / 2f && LevyConstants.CloseUpFollowStopTiles < LevyConstants.FollowDistance / 3f, "Close up shrinks the slack a following soldier tolerates (normal: starts walking past 6 tiles from its slot)");
			// 40 followers: the farthest slot stays inside the clump (normal ranks stretch 1.2 tiles per soldier)
			float far = Math.Min(LevyConstants.CloseUpFollowStartTiles + (39 / 2) * LevyConstants.CloseUpFollowStepTiles, LevyConstants.CloseUpFollowMaxTiles);
			check(far <= LevyConstants.CloseUpFollowMaxTiles && far < 40 * LevyConstants.FollowSlotSpacingTiles / 2f, $"40 followers on Close up all stand within {LevyConstants.CloseUpFollowMaxTiles} tiles of the commander (normal: the 40th stands {40 * LevyConstants.FollowSlotSpacingTiles:0} tiles back)");
		}

		// ------------------------------------------------------------------ inventory rules
		private static void CheckInventory(Action<bool, string> check)
		{
			var g = new SoldierGear();
			var bow = Make(ItemID.IronBow); var helmet = Make(ItemID.IronHelmet); var arrows = Make(ItemID.WoodenArrow); arrows.stack = 1500;
			check(g.CanHold(SoldierGear.SlotWeapon, bow), "a bow fits the weapon slot");
			check(!g.CanHold(SoldierGear.SlotHead, bow) && !g.CanHold(SoldierGear.SlotWeapon, helmet), "a bow does not fit the head slot and a helmet does not fit the weapon slot");
			check(g.CanHold(SoldierGear.SlotHead, helmet) && !g.CanHold(SoldierGear.SlotBody, helmet), "a helmet fits the head slot only");
			check(g.CanHold(SoldierGear.SlotAmmo, arrows) && !g.CanHold(SoldierGear.SlotWeapon, arrows), "arrows fit the ammo pouch only");

			// swap in: the slot takes the item and LOCKS
			check(g.TrySet(SoldierGear.SlotWeapon, bow, 1, out _) && g.Weapon.type == ItemID.IronBow && g.IsLocked(SoldierGear.SlotWeapon), "swapping a weapon in sets the slot lock");
			// take out: the slot empties and the lock clears (a lock on an empty slot would mean nothing)
			check(g.TrySet(SoldierGear.SlotWeapon, new Item(), 1, out _) && g.Weapon.IsAir && !g.IsLocked(SoldierGear.SlotWeapon), "taking a weapon out empties the slot and clears its lock");
			// a refused item changes nothing
			g.TrySet(SoldierGear.SlotWeapon, bow, 1, out _);
			check(!g.TrySet(SoldierGear.SlotWeapon, helmet, 1, out _) && g.Weapon.type == ItemID.IronBow && g.IsLocked(SoldierGear.SlotWeapon), "a refused item leaves the slot and its lock untouched");
			// ammo pouch is trimmed, the excess comes back
			check(g.TrySet(SoldierGear.SlotAmmo, arrows, 1, out var extra) && g.Ammo.stack == LevyConstants.AmmoPouchMax && extra != null && extra.stack == 1500 - LevyConstants.AmmoPouchMax && g.IsLocked(SoldierGear.SlotAmmo), "an oversized ammo stack is trimmed to the pouch and the excess is handed back");
			// accessories: grade limit and duplicates
			var boots = Make(ItemID.BandofRegeneration); var boots2 = Make(ItemID.BandofRegeneration); // (wings and boots have slots of their own)
			bool bootsOk = g.CanHold(SoldierGear.SlotAcc0, boots);
			if (bootsOk)
			{
				check(g.TrySet(SoldierGear.SlotAcc0, boots, 1, out _) && g.IsLocked(SoldierGear.SlotAcc0), "an accessory swaps into an open slot and locks it");
				check(!g.TrySet(SoldierGear.SlotAcc0 + 1, boots2, 1, out _), "an accessory slot the grade has not opened refuses");
				check(!g.TrySet(SoldierGear.SlotAcc0 + 1, boots2, 3, out _), "the same accessory twice is refused");
			}
			else check(false, "Band of Regeneration should be a useful accessory");
			check(g.CanHold(SoldierGear.SlotWings, Make(ItemID.AngelWings)) && g.CanHold(SoldierGear.SlotBoots, Make(ItemID.HermesBoots)) && !g.CanHold(SoldierGear.SlotAcc0 + 2, Make(ItemID.HermesBoots)) && !g.CanHold(SoldierGear.SlotBoots, Make(ItemID.AngelWings)), "wings and boots fit their own slots and no accessory slot");

			// vanity dyes: a dye only, one at a time, never locked
			var dye = Make(ItemID.RedDye); dye.stack = 5;
			// A headless server never loads the dye shaders, which is what gives a dye item its dye id; stand one in so the rule (dye id above zero) is what is tested.
			bool shaderIds = dye.dye > 0;
			if (!shaderIds) dye.dye = 1;
			var blue = Make(ItemID.BlueDye); if (blue.dye <= 0) blue.dye = 2;
			int locksBefore = g.LockMask;
			check(g.TrySetDye(0, dye, 1, out var dyeExtra) && g.Dyes[0].type == ItemID.RedDye && g.Dyes[0].stack == 1 && dyeExtra != null && dyeExtra.stack == 4, "a dye stack keeps one dye and hands the rest back (dye id " + dye.dye + ", real shader ids: " + shaderIds + ")");
			check(g.LockMask == locksBefore, "dyes take no lock (the Armory never touches them)");
			check(!g.TrySetDye(1, Make(ItemID.IronBow), 1, out _) && g.Dyes[1].IsAir, "a non-dye does not fit a vanity slot");
			check(!g.TrySetDye(3 + 2, blue, 1, out _), "the dye over an accessory slot the grade has not opened refuses");
			check(g.TrySetDye(0, new Item(), 1, out _) && g.Dyes[0].IsAir, "taking a dye out empties its slot");
			check(!SoldierGear.CanHoldDye(SoldierGear.DyeSlots, dye) && !SoldierGear.CanHoldDye(-1, dye), "dye slots outside range refuse");
		}

		/// <summary>How a hand lock interacts with the Armory's allocator: a locked slot is never replaced or emptied by IssueGear; an emptied slot is refilled on the next visit.</summary>
		private static void CheckArmoryLocks(Action<bool, string> check)
		{
			var te = new ArmoryTE();
			te.Storage[0] = Make(ItemID.ChlorophyteSaber);
			te.Storage[1] = Make(ItemID.IronHelmet);
			var s = new SoldierRecord { Id = 700 };
			s.Gear.TrySet(SoldierGear.SlotWeapon, Make(ItemID.WoodenSword), 1, out _);
			ArmoryTE.TestServed = new List<SoldierRecord> { s };
			try
			{
				te.IssueGear(s);
				check(s.Gear.Weapon.type == ItemID.WoodenSword && s.Gear.IsLocked(SoldierGear.SlotWeapon), "the Armory leaves a hand-placed (locked) weapon alone even with a better one in storage");
				s.Gear.TrySet(SoldierGear.SlotWeapon, new Item(), 1, out _);
				te.IssueGear(s);
				check(s.Gear.HasWeapon && s.Gear.Weapon.type != ItemID.WoodenSword, "a weapon taken away is refilled by the Armory on the next visit (unless the soldier is in reserve)");
			}
			finally { ArmoryTE.TestServed = null; }
			// a hand-placed helmet survives an issue pass; an emptied head slot is filled again
			var s2 = new SoldierRecord { Id = 701 };
			var tin = Make(ItemID.TinHelmet);
			s2.Gear.TrySet(SoldierGear.SlotHead, tin, 1, out _);
			ArmoryTE.TestServed = new List<SoldierRecord> { s2 };
			try
			{
				te.IssueGear(s2);
				check(s2.Gear.Head.type == ItemID.TinHelmet && s2.Gear.IsLocked(SoldierGear.SlotHead), "a hand-placed helmet is not swapped for the Armory's");
			}
			finally { ArmoryTE.TestServed = null; }
			// the soldier-wide lock: LoadoutLocked soldiers are skipped by the auto-upgrade
			var s3 = new SoldierRecord { Id = 702, LoadoutLocked = true };
			s3.Gear.Weapon = Make(ItemID.WoodenSword);
			var te2 = new ArmoryTE(); te2.Storage[0] = Make(ItemID.TerraBlade);
			check(!te2.UpgradeWeapons(s3) && s3.Gear.Weapon.type == ItemID.WoodenSword, "a LoadoutLocked soldier is never auto-upgraded");
		}

		// ------------------------------------------------------------------ packets
		private static void CheckPackets(Action<bool, string> check)
		{
			// the enum only grows at the end of the client packets: old values must not move
			check((byte)LevyPacket.Enlist == 1 && (byte)LevyPacket.SoldierGearSet == 21 && (byte)LevyPacket.ArmoryRulesSet == 32, "existing packet values did not move");
			check((byte)LevyPacket.SoldierDyeSet == 33 && (byte)LevyPacket.TalkCall == 34 && (byte)LevyPacket.RosterSnapshot == 100, "the new packets are appended after the last client packet");
			check(LevyPackets.IsRegistered(LevyPacket.SoldierDyeSet) && LevyPackets.IsRegistered(LevyPacket.TalkCall), "both new packets have a handler");

			// SoldierDyeSet round trip
			{
				var dye = Make(ItemID.GreenDye); dye.stack = 3;
				using var ms = new MemoryStream();
				using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) SoldierGear.WriteDyePayload(w, 4242, 7, dye);
				ms.Position = 0;
				using var r = new BinaryReader(ms);
				SoldierGear.ReadDyePayload(r, out int id, out int slot, out Item item);
				check(id == 4242 && slot == 7 && item.type == ItemID.GreenDye && item.stack == 3 && ms.Position == ms.Length, "SoldierDyeSet reads back exactly what was written");
			}
			// SoldierGearSet payload (the existing layout the inventory window reuses): weapon with a prefix
			{
				var sword = Make(ItemID.IronBroadsword); sword.Prefix(PrefixID.Legendary);
				using var ms = new MemoryStream();
				using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) { w.Write(99); w.Write((byte)SoldierGear.SlotWeapon); ItemIO.Send(sword, w, writeStack: true); }
				ms.Position = 0;
				using var r = new BinaryReader(ms);
				int id = r.ReadInt32(); byte slot = r.ReadByte(); Item item = ItemIO.Receive(r, readStack: true);
				check(id == 99 && slot == SoldierGear.SlotWeapon && item.type == ItemID.IronBroadsword && item.prefix == sword.prefix && ms.Position == ms.Length, "SoldierGearSet round-trips the item with its prefix");
			}
			// a cleared slot (air) survives the trip
			{
				using var ms = new MemoryStream();
				using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) SoldierGear.WriteDyePayload(w, 1, 0, new Item());
				ms.Position = 0;
				using var r = new BinaryReader(ms);
				SoldierGear.ReadDyePayload(r, out _, out _, out Item item);
				check(item.IsAir, "an emptied slot reads back as air");
			}
		}

		// ------------------------------------------------------------------ dialogue file
		private static void CheckDialogueFile(Mod Mod, Action<bool, string> check)
		{
			check(DialogueTable.LineCount > 0 && DialogueTable.Lines.Count > 0, "the dialogue table is loaded");
			var now = DialogueFile.Flatten(DialogueTable.Lines);

			// export -> read from disk -> parse: the file the /levydialogue command writes loads back to exactly today's lines
			string tmp = Path.Combine(Path.GetTempPath(), "levy-dialogue-check-" + Guid.NewGuid().ToString("N") + ".txt");
			try
			{
				File.WriteAllText(tmp, DialogueFile.Serialize(DialogueTable.Lines), new System.Text.UTF8Encoding(false));
				var again = new Dictionary<string, List<DialogueLine>>(StringComparer.OrdinalIgnoreCase);
				int bad = DialogueTable.Parse(null, tmp, DialogueFile.ReadTextFile(tmp), again);
				var back = DialogueFile.Flatten(again);
				check(bad == 0, "an exported dialogue file parses with no bad lines");
				bool same = back.Count == now.Count;
				if (same) for (int i = 0; i < now.Count; i++) if (now[i] != back[i]) { same = false; break; }
				check(same, $"the exported file loads back to exactly the same {now.Count} lines");
			}
			finally { try { File.Delete(tmp); } catch (Exception) { } }

			// the packed file itself, read straight from the mod: it is the source of truth for the export command
			byte[] packed = Mod.FileExists("Dialogue/en-US.txt") ? Mod.GetFileBytes("Dialogue/en-US.txt") : null;
			check(packed != null && packed.Length > 0, "Dialogue/en-US.txt is in the mod file");
			if (packed != null)
			{
				var again = new Dictionary<string, List<DialogueLine>>(StringComparer.OrdinalIgnoreCase);
				int bad = DialogueTable.Parse(null, "packed", new System.Text.UTF8Encoding(false).GetString(packed), again);
				var back = DialogueFile.Flatten(again);
				bool sameAsLoaded = back.Count == now.Count;
				if (sameAsLoaded) for (int i = 0; i < now.Count; i++) if (now[i] != back[i]) { sameAsLoaded = false; break; }
				// (an edited copy in the save folder may legitimately differ from the packed file; only compare when none is in use)
				if (DialogueTable.LoadedFrom.StartsWith("mod file")) check(bad == 0 && sameAsLoaded, "the loaded table equals the packed file line for line");
			}

			// every spoken group is present, and the mutineers' lines match the strings they used to be
			foreach (var key in new[] { "IDLE", "NAMEDROP", "ENGAGE", "KILL", "HURT", "BOSS_SIGHTED", "COMRADE_DOWN", "ROUTED", "DEATH_MESSAGE", "OFFICER_MARCH", "NURSE_MARCH", "OFFICER_CHAT_FIGHT", "OFFICER_Nurse_FIGHT", "MUTINY", "MUTINY_BEGIN", "MUTINY_REFUSE", "MUTINY_CALM", "DRINK" })
				check(DialogueTable.Get(key) != null, "the dialogue file has a group for " + key);
			// the triggers the commander's actions, the armory and the drinks fire: each has a pool, a delivery channel and enough lines not to repeat at once
			var fresh = new (DialogueTrigger t, int min, DialogueChannel channel)[]
			{
				(DialogueTrigger.LIFE_CRYSTAL, 8, DialogueChannel.FloatingText), (DialogueTrigger.CHEST_OPENED, 8, DialogueChannel.FloatingText),
				(DialogueTrigger.ARMORY_GOT, 8, DialogueChannel.FloatingText), (DialogueTrigger.ARMORY_WANT, 6, DialogueChannel.ChatWindow),
				(DialogueTrigger.ARMORY_BEST, 6, DialogueChannel.ChatWindow), (DialogueTrigger.ARMORY_WORST, 6, DialogueChannel.ChatWindow),
				(DialogueTrigger.DRINK_ALE, 5, DialogueChannel.FloatingText), (DialogueTrigger.DRINK_STOUT, 5, DialogueChannel.FloatingText),
				(DialogueTrigger.DRINK_RUM, 5, DialogueChannel.FloatingText), (DialogueTrigger.DRINK_FIZZ, 5, DialogueChannel.FloatingText),
				(DialogueTrigger.DRINK_ICEBERG, 5, DialogueChannel.FloatingText), (DialogueTrigger.ECLIPSE_END, 6, DialogueChannel.FloatingText),
			};
			foreach (var f in fresh)
			{
				var pool = DialogueTable.Get(f.t);
				int plain = 0;
				if (pool != null) foreach (var l in pool) if ((l.Cond & LineCond.Grumpy) == 0) plain++;
				check(plain >= f.min, $"{f.t} has {plain} lines (needs {f.min})");
				check(DialogueSystem.ChannelOf(f.t, out _) == f.channel, $"{f.t} is delivered through {f.channel}");
				check(!DialogueSystem.MilitiaMayBark(f.t), $"{f.t} is not a militia trigger");
				// the trigger name round-trips through the table's own parser, so the file can use it
				var into = new Dictionary<string, List<DialogueLine>>(StringComparer.OrdinalIgnoreCase);
				check(DialogueTable.Parse(null, "selfcheck", f.t + " | Any | Hello.", into) == 0 && into.ContainsKey(f.t.ToString()), $"the parser takes {f.t} as a trigger");
			}
			string[] cries = { "Mutiny.Cry1", "Mutiny.Cry2", "Mutiny.Cry3", "Mutiny.Cry4" };
			var mut = DialogueTable.Get("MUTINY");
			if (mut != null)
			{
				bool all = mut.Count >= 4;
				foreach (string k in cries) { string t = LevyUtil.T(k); if (!mut.Exists(l => l.Text == t)) all = false; }
				check(all, "the four mutiny cries in the file are the four that were in the localization");
			}
			void Same(string key, string loc) { var p = DialogueTable.Get(key); check(p != null && p.Count == 1 && p[0].Text == LevyUtil.T(loc), key + " is the line it was"); }
			Same("MUTINY_BEGIN", "Mutiny.Shout"); Same("MUTINY_REFUSE", "Mutiny.Refuse"); Same("MUTINY_CALM", "Mutiny.Calm");
			// the editable-file lookup names a path under the save folder and finds nothing in a clean save
			check(DialogueFile.EditablePath("en-US").EndsWith(Path.Combine("Levy", "Dialogue", "en-US.txt")), "the editable file lives under the save folder at Levy/Dialogue/en-US.txt");
		}
	}
}
