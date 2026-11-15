using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// LIVE HIT PARITY (LEVY_HITPARITY_LIVE=1 in a calrev boss-lab fight; measurement only). Every 20 ticks the grid of <see cref="HitParityCheck"/> is laid over the biggest hostile bodies on the field in
	/// the pose the real AI has put them in (a Leviathan mid-dash, a worm in the sand), and the rule in force (LEVY_CONTACTPARITY: the player's rules, or 0 for the old rectangle) is compared with a Player standing at
	/// each position. Rows go to <c>.parity.tsv</c> per fight: positions, positions where a player is hurt, and where the rule in force differs (levy hurt where a player is not, the reverse, a different slot or multiplier).
	/// </summary>
	public partial class CombatHarness
	{
		private static readonly bool HpLive = Environment.GetEnvironmentVariable("LEVY_HITPARITY_LIVE") == "1";
		private static readonly Dictionary<string, HitParityCheck.Tally> _hpTally = new();
		private static readonly Dictionary<string, int> _hpSamples = new();
		private static Terraria.Player _hpRef;

		private void HitParityLive(CrState st)
		{
			if (!HpLive || st.Tick < 20 || st.Tick % 20 != 0) return;
			NPC body = null; SoldierNPC sn = null;
			foreach (var l in st.Levies) { NPC n = Main.npc[l.Idx]; if (!l.Dead && n.active && n.ModNPC is SoldierNPC s && s.Dummy != null) { body = n; sn = s; break; } }
			if (body == null) return;
			_hpRef ??= new Terraria.Player { whoAmI = Main.maxPlayers, active = true, width = body.width, height = body.height };
			var picks = new List<NPC>();
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !n.friendly && !n.townNPC && n.damage > 0 && !(n.ModNPC is SoldierNPC) && !(n.ModNPC is SummonedUnitNPC)) picks.Add(n); }
			foreach (NPC n in picks.OrderByDescending(n => (long)n.width * n.height).Take(10))
			{
				string name = n.TypeName; if (!_hpTally.TryGetValue(name, out var t)) _hpTally[name] = t = new HitParityCheck.Tally();
				_hpSamples.TryGetValue(name, out int c); _hpSamples[name] = c + 1;
				HitParityCheck.Matrix(n, _hpRef, sn.Dummy, body, t);
			}
		}

		private void HitParityLiveFinish(CrRun run)
		{
			if (!HpLive) return;
			try
			{
				string file = _crOut + ".parity.tsv";
				if (!File.Exists(file)) File.WriteAllText(file, "key\tnpc\tsamples\tpositions\tplayerHurts\truleLevyOnly\truleLevyMissed\truleSlotMult\toldLevyOnly\toldLevyMissed\terrors\n");
				var sb = new System.Text.StringBuilder();
				foreach (var kv in _hpTally.OrderByDescending(k => k.Value.Positions))
				{
					var t = kv.Value;
					sb.Append(run.Key).Append('\t').Append(kv.Key).Append('\t').Append(_hpSamples[kv.Key]).Append('\t').Append(t.Positions).Append('\t').Append(t.PlayerHurt).Append('\t').Append(t.NewOnly).Append('\t').Append(t.NewMissed).Append('\t').Append(t.NewSlotMult)
					  .Append('\t').Append(t.OldOnly).Append('\t').Append(t.OldMissed).Append('\t').Append(t.Errors).Append('\n');
				}
				File.AppendAllText(file, sb.ToString());
			}
			catch { }
			_hpTally.Clear(); _hpSamples.Clear();
		}
	}
}
