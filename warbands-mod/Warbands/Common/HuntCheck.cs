using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>The scripted hunt, dodge, fallback and chatter checks (WARBANDS_LIVE=check): hostile bands that find the player off screen, neutral bands that stay neutral, damage from nothing the player did, a hurt member falling back, bands that talk one at a time.</summary>
	public partial class WarbandsLab
	{
		private void AddHuntCheckTrials()
		{
			var meteor = BandTypes.ById("MeteorMarauders");
			// a band 150 tiles off, hostile from the start, comes to the player over flat ground
			foreach (var (scen, d, label) in new[] { (Hn.Flat, 150, "open ground"), (Hn.Hills, 150, "rolling hills"), (Hn.Cliffs, 120, "stepped cliffs"), (Hn.Door, 150, "a wall with a closed door"), (Hn.Cave, 120, "a stair tunnel to a cave") })
			{
				var sc = scen; int D = d; int c25 = -1; int closest = int.MaxValue;
				_trials.Add(new Trial
				{
					Name = $"hunt: a hostile band {D} tiles off comes to a player over {label}", Max = 60 * 60,
					Setup = () =>
					{
						Main.rand = new Terraria.Utilities.UnifiedRandom(6100 + (int)sc); MvClearRoutes(); c25 = -1; closest = int.MaxValue;
						HnBuild(sc, D, 1, 1, out Point pl, out List<Point> spots);
						ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; DamageTaken = 0;
						_pinAt = new Vector2(pl.X * 16f + 8f - 10f, pl.Y * 16f - 42f); Main.player[0].position = _pinAt; Main.player[0].velocity = Vector2.Zero;
						MvForm(meteor, spots, 1);
					},
					Tick = t =>
					{
						if (t % 30 != 0) return false;
						foreach (var b in Levies()) { int dd = (int)(Vector2.Distance(b.NPC.Center, Main.player[0].Center) / 16f); closest = Math.Min(closest, dd); if (dd <= 25 && c25 < 0) c25 = t; }
						return c25 >= 0;
					},
					Result = () => (c25 >= 0, $"a member was within 25 tiles after {(c25 < 0 ? "never" : (c25 / 60f).ToString("0.0") + " s")}, closest {closest} tiles"),
					Cleanup = () => BuildArena(), // the hunt ground replaced the arena: put the sealed arena back for the trials after it
				});
			}

			// a neutral band is left alone: it neither hunts nor wakes from damage that came from nothing the player did
			BandBrain nb = null; int far0 = 0; bool unrelatedStayed = false, nearProvoked = false; int moved = 0;
			_trials.Add(new Trial
			{
				Name = "neutral band: stays neutral 150 tiles from the player, damage from no one does not provoke it, a hit with the player beside it does", Max = 1500,
				Setup = () =>
				{
					Main.rand = new Terraria.Utilities.UnifiedRandom(6200); MvClearRoutes();
					HnBuild(Hn.Flat, 150, 1, 1, out Point pl, out List<Point> spots);
					ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; DamageTaken = 0;
					_pinAt = new Vector2(pl.X * 16f + 8f - 10f, pl.Y * 16f - 42f); Main.player[0].position = _pinAt; Main.player[0].velocity = Vector2.Zero;
					MvForm(meteor, spots, 1); nb = BandSpawner.LastBrain; nb = BandBrain.All.Values.LastOrDefault();
					if (nb != null) { nb.Neutral = true; nb.Aggro = false; nb.Provoked = false; nb.Warns = false; }
					far0 = spots.Count; unrelatedStayed = false; nearProvoked = false; moved = 0;
				},
				Tick = t =>
				{
					var ls = Levies(); if (ls.Count == 0 || nb == null) return true;
					if (t == 400) { foreach (var b in ls) b.NPC.SimpleStrikeNPC(4, 1, false, 0f, null, false, 0f, true); } // lava, fall, any other enemy: nothing of the player's side near
					if (t == 600) { unrelatedStayed = nb.Neutral && !nb.Provoked && !nb.Aggro; moved = (int)(ls.Min(b => Vector2.Distance(b.NPC.Center, Main.player[0].Center)) / 16f); _pinAt = ls[0].NPC.Center + new Vector2(25f * 16f, -21f); Main.player[0].position = _pinAt; }
					if (t == 700) ls[0].NPC.SimpleStrikeNPC(4, 1, false, 0f, null, false, 0f, true); // the player stands 25 tiles away: this one counts
					if (t == 760) { nearProvoked = nb.Provoked || nb.Aggro; return true; }
					return false;
				},
				Result = () => (unrelatedStayed && nearProvoked && moved >= 100, $"after unrelated damage: neutral={unrelatedStayed}, closest member {moved} tiles from the player; after a hit with the player near: provoked={nearProvoked}"),
				Cleanup = () => BuildArena(),
			});

			// a badly hurt member falls back toward a healthy mate and keeps shooting
			bool fell = false; float d0 = 0f, d1 = 0f; int target = -1;
			_trials.Add(new Trial
			{
				Name = "fallback: a member at 20% life with healthy mates backs away from the fight", Max = 900,
				Setup = () =>
				{
					Main.rand = new Terraria.Utilities.UnifiedRandom(6300); MvClearRoutes(); fell = false; d0 = d1 = 0f; target = -1;
					ArmPlayer(Gold, 600, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; DamageTaken = 0;
					BandSpawner.SpawnBand(Main.player[0], meteor, 4, 24, 30, false, false);
					if (BandSpawner.LastBrain != null) { BandSpawner.LastBrain.Aggro = true; BandSpawner.LastBrain.Warns = false; BandSpawner.LastBrain.Neutral = false; }
				},
				Tick = t =>
				{
					var ls = Levies(); if (ls.Count < 3) return true;
					if (t == 240) { var m = ls[0]; target = m.NPC.whoAmI; m.NPC.life = Math.Max(1, m.NPC.lifeMax / 5); d0 = Vector2.Distance(m.NPC.Center, Main.player[0].Center) / 16f; }
					if (t == 243 && target >= 0 && Main.npc[target].ModNPC is BandLevyNPC m0) { m0.Morale = 100f; } // the drop in life cost morale; the check is about the fall-back rule, not the panic rule
					if (t > 240 && target >= 0 && Main.npc[target].active && Main.npc[target].ModNPC is BandLevyNPC mm)
					{
						if (mm.FallingBack) fell = true;
						if (t == 420) d1 = Vector2.Distance(mm.NPC.Center, Main.player[0].Center) / 16f;
					}
					return t >= 430;
				},
				Result = () => (fell, $"fell back={fell}, distance to the player {d0:0.0} -> {d1:0.0} tiles"),
			});

			// a band says one line at a time: five urgent lines in the same tick come out in turn, none on top of another
			var starts = new List<uint>(); var texts = new List<string>();
			_trials.Add(new Trial
			{
				Name = "chatter: five urgent lines in one tick are said one after another, not together", Max = 3000,
				Setup = () =>
				{
					Main.rand = new Terraria.Utilities.UnifiedRandom(6400); starts.Clear(); texts.Clear(); BandChatter.ResetTalk();
					ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; DamageTaken = 0;
					BandSpawner.SpawnBand(Main.player[0], BandTypes.ById("ForestDeserters"), 5, 24, 30, false, false);
					if (BandSpawner.LastBrain != null) { BandSpawner.LastBrain.Aggro = false; BandSpawner.LastBrain.Warns = true; BandSpawner.LastBrain.Neutral = true; BandSpawner.LastBrain.Provoked = false; }
					BandChatter.Probe = (npc, text) => { starts.Add(Main.GameUpdateCount); texts.Add(text); };
					foreach (var b in Levies()) BandChatter.Say(b, "SPOTTED", force: true);
				},
				Tick = t => t >= 2900,
				Result = () =>
				{
					BandChatter.Probe = null;
					bool spaced = true; for (int i = 1; i < starts.Count; i++) if (starts[i] - starts[i - 1] < 150u) spaced = false;
					return (starts.Count >= 2 && spaced, $"{starts.Count} lines shown, starts {string.Join(",", starts.Select(x => x.ToString()))}");
				},
				Cleanup = () => { BandChatter.Probe = null; },
			});
		}
	}
}
