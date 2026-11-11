using System;
using System.Text;
using Terraria;
using Levy.Common;
using Levy.Common.Dialogue;
using Levy.Common.Util;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// FEAR OF FLYING. Levies love the ground. A winged levy that has been in the air for ShoutAfterFlyTicks since it took off on its wings shouts one long "Whaaaooooo" over its head, once per flight.
	/// Taking off, a long flight and landing each have a line of their own (TAKEOFF, FLIGHT_FEAR, LANDING in the dialogue file). All of it shares ShoutMaxAtOnce slots, each held for ShoutSlotTicks,
	/// so an army taking off together does not fill the screen. Server and single player decide; the line reaches clients through the ordinary floating-text path.
	/// </summary>
	public partial class SoldierNPC
	{
		private static readonly uint[] ShoutSlot = new uint[ShoutMaxAtOnce];
		private int _ftAir; private bool _ftTook, _ftShouted, _ftFeared, _ftFuelNoted;
		/// <summary>Harness telemetry: shouts and other flight lines said since boot.</summary>
		internal static int FlightShouts, FlightLines;
		/// <summary>Harness: -1 follows the default, 0 off.</summary>
		internal static int FlightTalkOverride = -1;

		private static bool ClaimShoutSlot()
		{
			uint now = Main.GameUpdateCount;
			for (int i = 0; i < ShoutSlot.Length; i++) if (ShoutSlot[i] <= now) { ShoutSlot[i] = now + (uint)ShoutSlotTicks; return true; }
			return false;
		}

		/// <summary>One long frightened shout; the length and the letters vary.</summary>
		internal static string ShoutText()
		{
			var r = Main.rand; var sb = new StringBuilder();
			switch (r.Next(4))
			{
				case 0: case 1: sb.Append("Wha").Append('a', r.Next(1, 5)).Append(r.Next(4) == 0 ? 'u' : 'o', r.Next(5, 12)); break;
				case 2: sb.Append("Wh").Append('o', r.Next(3, 9)).Append('a', r.Next(1, 4)).Append('h', r.Next(0, 3)); break;
				default: sb.Append('W').Append('a', r.Next(3, 9)).Append('h', r.Next(1, 4)); break;
			}
			sb.Append(r.Next(3) == 0 ? "!!" : "!");
			return sb.ToString();
		}

		private void StatArcFuelOutNote() { if (_ftFuelNoted) return; _ftFuelNoted = true; StatArcFuelOut++; }

		private bool FlightTalkOn => FlightTalkOverride != 0 && !LevyUtil.IsClient && Record != null && Abilities != null && Abilities.HasWings && !OfficerMode;

		private void SayFlightLine(DialogueTrigger t)
		{
			string line = DialogueSystem.PickLine(t, Record, new DialogueContext { Player = Record.CommanderName });
			if (string.IsNullOrEmpty(line) || !ClaimShoutSlot()) return;
			FlightLines++;
			DialogueSystem.FloatingText(this, line, t);
		}

		/// <summary>The route starts an arc: sometimes a complaint first.</summary>
		private void TakeoffTalk()
		{
			if (!FlightTalkOn || Main.rand.Next(100) >= 35) return;
			SayFlightLine(DialogueTrigger.TAKEOFF);
		}

		private void FlightTalkTick()
		{
			if (!FlightTalkOn) { if (_ftTook) { _ftTook = false; _ftAir = 0; } return; }
			bool onGround = NPC.velocity.Y == 0f && !_flying;
			if (_flying) _ftTook = true;
			if (onGround)
			{
				if (_ftTook && _ftAir >= 45 && Main.rand.Next(100) < 55) SayFlightLine(DialogueTrigger.LANDING);
				_ftTook = false; _ftAir = 0; _ftShouted = _ftFeared = _ftFuelNoted = false;
				return;
			}
			if (!_ftTook) return;
			_ftAir++;
			if (!_ftShouted && _ftAir >= ShoutAfterFlyTicks)
			{
				_ftShouted = true;
				if (ClaimShoutSlot()) { FlightShouts++; DialogueSystem.FloatingText(this, ShoutText(), DialogueTrigger.FLIGHT_SHOUT); }
			}
			else if (!_ftFeared && _ftAir >= ShoutAfterFlyTicks * 2 + 30)
			{
				_ftFeared = true;
				if (Main.rand.Next(100) < 60) SayFlightLine(DialogueTrigger.FLIGHT_FEAR);
			}
		}
	}
}
