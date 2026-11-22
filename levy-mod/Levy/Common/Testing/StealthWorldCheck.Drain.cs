using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Calamity;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// THE DRAIN PHASE of the stealth check: rogue levies throwing beside a rogue commander who stands still must leave her bar alone. Her bar is emptied and the ticks it takes to fill are counted, with no levies
	/// and with rogue levies next to her fighting, and every tick her bar went DOWN is counted by the part of the world update it went down in (players, NPCs, projectiles), so a drain is attributed as it happens.
	/// LEVY_STEALTH_SWEEP=1 (lab) runs every rogue weapon Calamity has, three levies at a time.
	/// </summary>
	public partial class StealthWorldCheck
	{
		/// <summary>Her bar at each boundary of the world update, and the drops seen in each part (lab and check).</summary>
		public sealed class DrainProbe : ModSystem
		{
			internal static bool On; internal static float Last; internal static int DropPlayers, DropNpcs, DropProj, DropOther; internal static float LostNpcs, LostProj, LostPlayers, LostOther;
			internal static readonly Dictionary<string, int> ProjAt = new();
			private static float Read() { float[] s = HarnessCalamityProbe.Stealth(Main.player[0]); return s != null ? s[0] : 0f; }
			internal static void Reset() { DropPlayers = DropNpcs = DropProj = DropOther = 0; LostNpcs = LostProj = LostPlayers = LostOther = 0f; ProjAt.Clear(); Last = Read(); }
			private static void Mark(ref int drops, ref float lost, bool proj)
			{
				if (!On) return;
				float now = Read();
				if (now < Last - 1e-5f)
				{
					drops++; lost += Last - now;
					if (proj)
						for (int i = 0; i < Main.maxProjectiles; i++)
						{
							Projectile p = Main.projectile[i];
							if (!p.active || p.owner != 0 || !p.TryGetGlobalProjectile(out SoldierProjectileTag t) || !t.IsSoldierShot) continue;
							string k = p.ModProjectile?.Name ?? ("vanilla " + p.type); ProjAt[k] = ProjAt.TryGetValue(k, out int n) ? n + 1 : 1;
						}
				}
				Last = now;
			}
			public override void PreUpdatePlayers() { if (On) { float now = Read(); if (now < Last - 1e-5f) { DropOther++; LostOther += Last - now; } Last = now; } }
			public override void PostUpdatePlayers() => Mark(ref DropPlayers, ref LostPlayers, false);
			public override void PreUpdateNPCs() { if (On) { float now = Read(); if (now < Last - 1e-5f) { DropOther++; LostOther += Last - now; } Last = now; } }
			public override void PostUpdateNPCs() => Mark(ref DropNpcs, ref LostNpcs, false);
			public override void PreUpdateProjectiles() { if (On) { float now = Read(); if (now < Last - 1e-5f) { DropOther++; LostOther += Last - now; } Last = now; } }
			public override void PostUpdateProjectiles() => Mark(ref DropProj, ref LostProj, true);
		}

		private sealed class Fill { public int Ticks = -1, Shots, Strikes, LevyBarMax; public int DP, DN, DPr, DO; public string ProjAt = ""; public float LevyBar; }

		/// <summary>Her bar from empty to full standing still beside these levies (cap ticks), with the drops attributed; the levy shots made and the levy strikes seen while it ran.</summary>
		private static Fill FillBeside(List<SoldierNPC> levies, int cap = 400)
		{
			var f = new Fill(); var seen = new HashSet<int>();
			HarnessCalamityProbe.SetStealth(Main.player[0], 0f); _moving = false; _use = false;
			Step(2); HarnessCalamityProbe.SetStealth(Main.player[0], 0f);
			DrainProbe.Reset(); DrainProbe.On = true;
			try
			{
				for (int t = 1; t <= cap; t++)
				{
					Step(1);
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile p = Main.projectile[i];
						if (!p.active || !p.TryGetGlobalProjectile(out SoldierProjectileTag tg) || !tg.IsSoldierShot) continue;
						if (seen.Add(p.identity * 1000 + i)) { f.Shots++; if (HarnessCalamityProbe.IsStealthStrike(p, out _)) f.Strikes++; }
					}
					float[] s = HarnessCalamityProbe.Stealth(Main.player[0]);
					if (f.Ticks < 0 && s != null && s[1] > 0f && s[0] >= s[1] - 1e-4f) { f.Ticks = t; if (t + 30 < cap) cap = t + 30; } // a few ticks more to see a drop right after the fill
				}
			}
			finally { DrainProbe.On = false; }
			f.DP = DrainProbe.DropPlayers; f.DN = DrainProbe.DropNpcs; f.DPr = DrainProbe.DropProj; f.DO = DrainProbe.DropOther;
			f.ProjAt = string.Join(", ", DrainProbe.ProjAt.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + " x" + kv.Value));
			foreach (var l in levies) { float[] ls = HarnessCalamityProbe.Stealth(l?.Dummy); if (ls != null) f.LevyBar = Math.Max(f.LevyBar, ls[1]); }
			return f;
		}

		private static string Show(Fill f) => $"fill {f.Ticks} ticks; her bar went down {f.DP + f.DN + f.DPr + f.DO} times (players {f.DP}, NPCs {f.DN}, projectiles {f.DPr}, between {f.DO}){(f.ProjAt.Length > 0 ? " [soldier shots out at a projectile drop: " + f.ProjAt + "]" : "")}; levy shots {f.Shots}, levy strikes {f.Strikes}; levy bar max {f.LevyBar:0.##}";

		private static void ClearLevies(List<SoldierNPC> levies)
		{
			foreach (var l in levies) if (l?.NPC != null) { if (l.Record != null) l.Record.Gear = new SoldierGear(); l.NPC.active = false; }
			for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.IsSoldierShot) p.active = false; }
			if (RosterSystem.Instance != null) RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander && r.Name.StartsWith("Pvt. Drain"));
			levies.Clear();
			Step(2);
		}

		/// <summary>N levies next to the commander holding <paramref name="weapon"/> in rogue armor, fighting the enemy beside her. Null when the armory will not hand one the weapon.</summary>
		private static List<SoldierNPC> DrainSquad(int n, int weapon, int head, int body, int legs, int acc = 0)
		{
			var list = new List<SoldierNPC>();
			for (int i = 0; i < n; i++)
			{
				var l = MakeLevy(_cmdAt + new Vector2((4 + 2 * i) * 16f, 0f), 500 + i, SoldierOrder.Follow, weapon, head, body, legs);
				if (l == null || l.Record?.Gear?.Weapon == null || l.Record.Gear.Weapon.type != weapon) { if (l != null) list.Add(l); ClearLevies(list); return null; }
				l.Record.Name = "Pvt. Drain" + i;
				if (acc > 0) { Item a = new Item(); a.SetDefaults(acc); l.Record.Gear.Accessories[0] = a; l.Record.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
				list.Add(l);
			}
			return list;
		}

		/// <summary>Rogue weapons whose own projectile code spends or sets "the owner's" stealth bar (found by the sweep: Calamity 2.2.2), and the everyday throw.</summary>
		private static readonly string[] DrainWeapons = { "PulseGrenade", "LeviathanTeeth" };

		/// <summary>The damage of her strike's hits that were not critical (a crit doubles a hit, and which hits crit depends on the random rolls the army also draws from), 0 when every hit crit.</summary>
		private static double NonCrit(Run3 r) => r.StrikeHits > r.StrikeCrits ? r.StrikeNonCritMean / (r.StrikeHits - r.StrikeCrits) : 0;

		/// <summary>Two mean hits of the same projectile damage agree within vanilla's random damage spread (each hit is rolled within 15 percent of the damage).</summary>
		private static bool WithinVariance(double x, double y) => Math.Abs(x - y) <= 0.15 * Math.Max(1.0, Math.Max(x, y));

		private static bool Clean(Fill f, Fill alone) => f.Ticks > 0 && Math.Abs(f.Ticks - alone.Ticks) <= 2 && f.DN + f.DPr + f.DO == 0;

		/// <summary>
		/// The commander stands still with an empty bar while rogue levies throw beside her: her bar fills in exactly the ticks it fills alone and never goes down, for one and for three levies, for the weapons whose projectiles
		/// spend "the owner's" bar and for the everyday throw; her stealth strike does the damage it does alone; the levies still land strikes of their own; a levy wearing the Eclipse Mirror (its dodge refills the wearer's bar)
		/// leaves hers alone. <paramref name="solo"/> is the no-army measurement of the same throw.
		/// </summary>
		private static void DrainPhase(int fran, int head, int body, int legs, Run3 solo)
		{
			_t2At = _cmdAt + new Vector2(16 * 16f, 0f); Pin(_t2, _t2At);
			var none = new List<SoldierNPC>();
			Fill alone = FillBeside(none);
			Note("drain: commander alone: " + Show(alone));
			Check($"drain: alone her bar fills in Calamity's 2 seconds and never goes down ({alone.Ticks} ticks, {alone.DN + alone.DPr + alone.DO + alone.DP} drops)", alone.Ticks >= 118 && alone.Ticks <= 124 && alone.DN + alone.DPr + alone.DO + alone.DP == 0);
			if (Environment.GetEnvironmentVariable("LEVY_STEALTH_SWEEP") == "1") { Sweep(head, body, legs, alone); return; }

			long lends0 = StealthGuard.Lends;
			int levyStrikes = 0;
			var weapons = new List<(int type, string name)> { (fran, "Iron Francisca") };
			foreach (string wn in DrainWeapons) { int w = ItemOf(wn); if (w > 0) weapons.Add((w, Lang.GetItemNameValue(w))); else Note("drain: " + wn + " is not in this Calamity build"); }
			foreach (var (w, name) in weapons)
				foreach (int n in new[] { 1, 3 })
				{
					var squad = DrainSquad(n, w, head, body, legs);
					if (squad == null) { Note($"drain: the armory will not hand a levy the {name}"); continue; }
					Step(90);
					Fill f = FillBeside(squad);
					levyStrikes += f.Strikes;
					Note($"drain: {n} levies throwing the {name}: " + Show(f));
					Check($"drain: {n} {(n == 1 ? "levy" : "levies")} throwing the {name} beside her: her bar fills as fast as alone ({alone.Ticks} vs {f.Ticks} ticks) and never goes down ({f.DN + f.DPr + f.DO} drops; {f.Shots} levy shots)", Clean(f, alone) && f.Shots > 0);
					if (w != fran)
					{
						// her own stealth strike and plain throw with these levies throwing beside her
						Run3 m = Measure($"{n} {name} levies", 2);
						double nc = NonCrit(m), nc0 = NonCrit(solo);
						Note($"drain: her throws beside {n} levies with the {name}: fill {m.ChargeStand} ticks (min {m.ChargeStandMin}, max {m.ChargeStandMax}); strikes {m.Strikes} (flagged {m.StrikeFlagged}), projectile damage {m.StrikeProjDmg:0.##}, enemy took {m.StrikeHitMean:0.##} a hit ({m.StrikeCrits} of {m.StrikeHits} crits, {nc:0.##} a non-crit hit); plain {m.NormalProjDmg:0.##}; alone: strike {solo.StrikeProjDmg:0.##} ({nc0:0.##} a non-crit hit), plain {solo.NormalProjDmg:0.##}");
						Check($"drain: beside {n} {name} {(n == 1 ? "levy" : "levies")} her stealth strike is a full one ({m.StrikeFlagged}/{m.Strikes} flagged; damage {m.StrikeProjDmg:0.##} vs {solo.StrikeProjDmg:0.##} alone, a non-crit hit {nc:0.##} vs {nc0:0.##}; fill {m.ChargeStandMin}-{m.ChargeStandMax} ticks)",
							m.Strikes >= 2 && m.StrikeFlagged == m.Strikes && Near(m.StrikeProjDmg, solo.StrikeProjDmg) && Near(m.NormalProjDmg, solo.NormalProjDmg) && m.ChargeStandMax <= solo.ChargeStandMax + 2 && (nc <= 0 || nc0 <= 0 || WithinVariance(nc, nc0)));
					}
					ClearLevies(squad);
				}
			Check($"drain: the levies' own rogue work ran through the stealth guard ({StealthGuard.Lends - lends0} lends)", StealthGuard.Lends > lends0);

			// the levies still land stealth strikes, judged on their own bars: three levies with a javelin Calamity gives a strike to (a levy waits for its bar with it) fighting for ten seconds
			{
				int jav = ItemOf("PalladiumJavelin"); if (jav <= 0) jav = fran; string jn = Lang.GetItemNameValue(jav);
				var squad = DrainSquad(3, jav, head, body, legs);
				if (squad != null)
				{
					var seen = new HashSet<int>(); int shots = 0, strikes = 0;
					for (int t = 0; t < 600; t++)
					{
						Step(1);
						for (int i = 0; i < Main.maxProjectiles; i++)
						{
							Projectile p = Main.projectile[i];
							if (!p.active || !p.TryGetGlobalProjectile(out SoldierProjectileTag tg) || !tg.IsSoldierShot || !seen.Add(p.identity * 1000 + i)) continue;
							shots++; if (HarnessCalamityProbe.IsStealthStrike(p, out _)) strikes++;
						}
					}
					Note($"drain: three {jn} levies over 600 ticks: {shots} shots, {strikes} stealth strikes; levy strikes in the fills above {levyStrikes}");
					Check($"drain: the levies still land stealth strikes of their own ({strikes} of {shots} throws in 10 s)", strikes >= 2);
					ClearLevies(squad);
				}
			}

			NotHerStrikes(head, body, legs);
			EclipseMirrorLevy(fran, head, body, legs, alone);
		}

		/// <summary>
		/// A levy never strikes off HER bar: Calamity's Refraction Rotor decides its strike from the LOCAL player's bar (<c>Main.LocalPlayer.Calamity().StealthStrikeAvailable()</c> in its Shoot), and the local player is the commander
		/// while a levy uses it. Her bar is kept full and the levies' bars empty: a strike flag on a levy's throw is a strike taken from her bar.
		/// </summary>
		private static void NotHerStrikes(int head, int body, int legs)
		{
			int rotor = ItemOf("RefractionRotor");
			if (rotor <= 0) { Note("drain: the Refraction Rotor is not in this Calamity build"); return; }
			var squad = DrainSquad(3, rotor, head, body, legs);
			if (squad == null) { Note("drain: the armory will not hand a levy the Refraction Rotor"); return; }
			var seen = new HashSet<int>(); int shots = 0, strikes = 0;
			for (int t = 0; t < 300; t++)
			{
				float[] her = Bar(); if (her != null) HarnessCalamityProbe.SetStealth(Main.player[0], her[1]);
				foreach (var l in squad) HarnessCalamityProbe.SetStealth(l.Dummy, 0f);
				Step(1);
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active || !p.TryGetGlobalProjectile(out SoldierProjectileTag tg) || !tg.IsSoldierShot || !seen.Add(p.identity * 1000 + i)) continue;
					shots++; if (HarnessCalamityProbe.IsStealthStrike(p, out _)) strikes++;
				}
			}
			Note($"drain: three Refraction Rotor levies on empty bars beside her full one, 300 ticks: {shots} shots, {strikes} flagged stealth strikes");
			Check($"drain: a levy on an empty bar never throws a stealth strike off her full one ({strikes} of {shots} Refraction Rotor throws flagged)", shots > 0 && strikes == 0);
			ClearLevies(squad);
		}

		/// <summary>A levy wearing Calamity's Eclipse Mirror (more bar, a strike at half cost, a dodge that refills the wearer's bar by half) fights beside her: her bar fills as alone, and its dodge fills the LEVY's bar, not hers.</summary>
		private static void EclipseMirrorLevy(int fran, int head, int body, int legs, Fill alone)
		{
			int mirror = ItemOf("EclipseMirror");
			if (mirror <= 0) { Note("drain: the Eclipse Mirror is not in this Calamity build"); return; }
			var squad = DrainSquad(1, fran, head, body, legs, mirror);
			if (squad == null) { Note("drain: the armory will not hand a levy the Eclipse Mirror kit"); return; }
			SoldierNPC l = squad[0];
			Step(90);
			bool wears = l.Dummy != null && Array.Exists(l.Dummy.armor, a => a != null && a.type == mirror);
			Fill f = FillBeside(squad);
			Note($"drain: a levy in the Eclipse Mirror (worn by its dummy: {wears}): " + Show(f));
			Check($"drain: a levy wearing the Eclipse Mirror beside her: her bar fills as fast as alone ({alone.Ticks} vs {f.Ticks} ticks, {f.DN + f.DPr + f.DO} drops)", Clean(f, alone));
			// its dodge: a hit on the levy that the mirror dodges refills the levy's bar by half; hers is not touched
			HarnessCalamityProbe.SetStealth(l.Dummy, 0f);
			HarnessCalamityProbe.SetStealth(Main.player[0], 0.25f);
			float[] her0 = Bar(), lv0 = HarnessCalamityProbe.Stealth(l.Dummy);
			int my = Main.myPlayer; Main.myPlayer = 0; SoldierNPC.HitResult r;
			try { r = l.PlayerSequence(_t2, null, Math.Max(200, l.NPC.lifeMax / 4), 1, -1); } finally { Main.myPlayer = my; }
			float[] her1 = Bar(), lv1 = HarnessCalamityProbe.Stealth(l.Dummy);
			Note($"drain: Eclipse Mirror levy hit: ran {r.Ran}, dodged {r.Dodged}; levy bar {lv0?[0]:0.##} -> {lv1?[0]:0.##}; her bar {her0?[0]:0.##} -> {her1?[0]:0.##}");
			Check($"drain: the Eclipse Mirror levy's dodge leaves her bar alone ({her0?[0]:0.##} -> {her1?[0]:0.##}; dodged {r.Dodged}, the levy's bar {lv0?[0]:0.##} -> {lv1?[0]:0.##})", her0 != null && her1 != null && Math.Abs(her1[0] - her0[0]) < 1e-4f && (!r.Dodged || (lv1 != null && lv0 != null && lv1[0] >= lv0[0] + 0.49f)));
			ClearLevies(squad);
		}

		private static void Sweep(int head, int body, int legs, Fill alone)
		{
			var weapons = new List<Item>();
			foreach (ModItem mi in ModLoader.GetMod("CalamityMod").GetContent<ModItem>())
			{
				Item it = mi.Item;
				if (it == null || it.damage <= 0 || !ExtraDamageClasses.IsRogue(it.DamageType) || it.accessory) continue;
				weapons.Add(it);
			}
			Note($"sweep: {weapons.Count} Calamity rogue weapons");
			int bad = 0, refused = 0, ran = 0; var badNames = new List<string>();
			foreach (Item w in weapons)
			{
				var squad = DrainSquad(3, w.type, head, body, legs);
				if (squad == null) { refused++; continue; }
				ran++;
				Step(90);
				Fill f = FillBeside(squad);
				bool off = f.Ticks < 0 || f.Ticks > alone.Ticks + 2 || f.DN + f.DPr + f.DO > 0;
				if (off) { bad++; badNames.Add(w.Name); }
				Note($"sweep {(off ? "DRAIN" : "ok")} {w.ModItem?.Name} ({w.Name}): " + Show(f));
				ClearLevies(squad);
			}
			Note($"sweep done: {ran} weapons run, {refused} refused by the armory, {bad} drained her bar: {string.Join(", ", badNames)}");
		}
	}
}
