using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Officers;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// The Clothier's squad over a long fight, and the aim audit's core set, both part of <see cref="OfficerWorldCheck"/>.
	///  * CHURN: a two-minute fight against a pinned enemy in reach. Every 600 ticks one skeleton is struck down (a loss); the squad size is sampled each second. The squad must stay at its size apart from the gap a
	///    loss leaves before the next is raised, raising must never stop (a skeleton raised after every loss), and no skeleton that keeps dealing damage may crumble.
	///  * IDLE DECAY: an enemy near the Clothier (a fight) but out of reach of his skeletons: nothing they do deals damage, so they crumble after the idle life and are raised again, the squad staying full.
	///  * CORE AUDIT: the aim audit (<see cref="AuditType"/>) over every skeleton on the Clothier's ladder, the strongest creatures of each stage and the casters that throw orbs.
	/// </summary>
	public partial class OfficerWorldCheck
	{
		private static int SerialOf(NPC n) => n.GetGlobalNPC<ConjuredGlobalNPC>().Serial;
		private static List<NPC> LiveSquad(int owner) => Conjured(owner).Where(n => !n.GetGlobalNPC<ConjuredGlobalNPC>().IsChild).ToList();

		private static void ClothierChurn()
		{
			SpawnEnemy(10);
			NPC town = Commission(NPCID.Clothier, 0, out SoldierNPC avatar);
			Check("churn: the Clothier is commissioned and marching", town != null && avatar != null);
			if (avatar == null) { if (town != null) town.active = false; KillEnemy(); return; }
			int owner = avatar.NPC.whoAmI, cap = LevyConstants.Summon.ClothierSkeletonCount;
			avatar.NPC.Bottom = new Vector2(Cx * 16f - 40f, FloorY * 16f);
			Step(90);
			const int Ticks = 7200;
			var birth = new Dictionary<int, int>(); var known = new HashSet<int>();
			int crumb0 = ConjuredGlobalNPC.Crumbled, raised = 0, losses = 0, crumbled = 0, minSize = 99, samples = 0, atCap = 0, sum = 0, longest = 0, lastRaisedTick = 0;
			var killedNow = new HashSet<int>();
			for (int tick = 0; tick < Ticks; tick++)
			{
				if (tick > 0 && tick % 600 == 0) { var v = LiveSquad(owner).FirstOrDefault(); if (v != null) { killedNow.Add(SerialOf(v)); v.SimpleStrikeNPC(100000, 1, false, 0f, null, false, 0f, true); losses++; } }
				Step(1);
				var squad = LiveSquad(owner); var now = new HashSet<int>(squad.Select(SerialOf));
				foreach (int w in now) if (known.Add(w)) { raised++; birth[w] = tick; lastRaisedTick = tick; }
				foreach (int w in known.ToList()) if (!now.Contains(w)) { known.Remove(w); killedNow.Remove(w); longest = Math.Max(longest, tick - birth[w]); }
				if (tick % 60 == 0) { samples++; sum += squad.Count; minSize = Math.Min(minSize, squad.Count); if (squad.Count >= cap) atCap++; }
			}
			foreach (int w in known) longest = Math.Max(longest, Ticks - birth[w]);
			crumbled = ConjuredGlobalNPC.Crumbled - crumb0;
			double mean = sum / (double)Math.Max(1, samples);
			Note($"churn: 2-minute fight ({Ticks} ticks): squad size mean {mean:0.00}, min {minSize}, at full size in {atCap}/{samples} one-second samples; {raised} skeletons raised (5 at the start, {losses} struck down), {crumbled} crumbled while fighting, longest-lived {longest} ticks, last one raised at tick {lastRaisedTick}");
			Check($"churn: the squad is at full size in at least 90% of the fight's seconds ({atCap}/{samples})", atCap >= samples * 0.9);
			Check($"churn: he raised a skeleton after every loss ({raised - cap} raised for {losses} lost)", raised - cap >= losses);
			Check($"churn: raising never stopped (the last was raised at tick {lastRaisedTick} of {Ticks})", lastRaisedTick > Ticks - 1300);
			Check($"churn: a skeleton that keeps fighting stays (longest-lived {longest} ticks, idle life {LevyConstants.Summon.ClothierSkeletonIdleLifeTicks})", longest > LevyConstants.Summon.ClothierSkeletonIdleLifeTicks * 3);
			Check($"churn: none crumbled while the squad was dealing damage ({crumbled})", crumbled == 0);
			KillEnemy();

			// idle decay: an enemy near him but out of his skeletons' reach
			SpawnEnemy(10); _targetAt.Y -= 20 * 16f; if (_target != null) { _target.Bottom = _targetAt; _target.GetGlobalNPC<HarnessTarget>().Pin = _target.position; } // high in the air beside him: a fight, but out of any skeleton's reach
			avatar.NPC.Bottom = new Vector2(Cx * 16f - 40f, FloorY * 16f); avatar.NPC.velocity = Vector2.Zero;
			Step(30);
			Note("idle decay: NPCs: " + string.Join(" ", Main.npc.Where(o => o.active).Select(o => $"{o.TypeName}#{o.whoAmI}(f={o.friendly},d={o.damage},pinned={(o.TryGetGlobalNPC(out HarnessTarget hh) && hh.IsTarget)},y={(int)((o.Bottom.Y - _targetAt.Y) / 16f)},x={(int)((o.Center.X - _targetAt.X) / 16f)})")) + " _target=" + (_target == null ? "null" : _target.whoAmI.ToString()));
			var before = new HashSet<int>(LiveSquad(owner).Select(SerialOf));
			int raised1 = ConjuredGlobalNPC.Raised, crumb = 0, minIdle = 99; var seen = new HashSet<int>(before); long dealt = 0;
			for (int tick = 0; tick < LevyConstants.Summon.ClothierSkeletonIdleLifeTicks * 3; tick++)
			{
				Step(1);
				var squad = LiveSquad(owner); var now = new HashSet<int>(squad.Select(SerialOf));
				foreach (int w in seen.ToList()) if (!now.Contains(w)) { seen.Remove(w); crumb++; }
				foreach (int w in now) seen.Add(w);
				if (_target != null && _target.active && _target.life < _target.lifeMax) dealt += _target.lifeMax - _target.life;
				if ((tick % 300 == 0) && squad.Count > 0) { var g0 = squad[0].GetGlobalNPC<ConjuredGlobalNPC>(); Note($"idle decay tick {tick}: first skeleton idle ticks {g0.IdleTicksNow}, fight={g0.FightNow}, life budget {g0.IdleLifeTicks}, target {g0.Target}, at {(int)((squad[0].Center.X - _targetAt.X) / 16f)},{(int)((squad[0].Center.Y - _targetAt.Y) / 16f)} tiles from the enemy"); }
				if (tick > 300) minIdle = Math.Min(minIdle, squad.Count);
			}
			Note($"idle decay: enemy out of reach, {LevyConstants.Summon.ClothierSkeletonIdleLifeTicks * 3} ticks: {crumb} skeletons crumbled, smallest squad after the first 300 ticks {minIdle}, damage dealt {dealt}");
			Check($"idle decay: skeletons that deal no damage in a fight crumble ({crumb})", crumb >= cap);
			int replaced = ConjuredGlobalNPC.Raised - raised1;
			Note($"idle decay: {replaced} raised to replace them");
			Check($"idle decay: and are raised again, one for each that crumbled ({replaced} raised, {crumb} crumbled)", replaced >= crumb - 1 && minIdle >= 1);
			Dismiss(town);
			KillEnemy();
		}

		private static void AuditCore()
		{
			NPC town = Commission(NPCID.Merchant, 0, out SoldierNPC avatar);
			if (avatar == null) { Check("aim audit: an owner officer is marching", false); return; }
			_avatarTypes = AvatarBaseline(avatar);
			var types = new List<(int t, string n, int ai)>(); var seen = new HashSet<int>();
			for (int st = 0; st <= 10; st++) foreach (int t in ConjureRoster.SkeletonPool(st)) if (seen.Add(t)) { var pr = new NPC(); pr.SetDefaults(t); types.Add((t, Lang.GetNPCNameValue(t), pr.aiStyle)); }
			for (int st = 0; st <= 10; st++) foreach (var f in ConjureRoster.BeastCandidates(st, false, out _).Take(3)) if (seen.Add(f.Type)) types.Add((f.Type, f.Name, f.AiStyle));
			foreach (int t in new[] { NPCID.FireImp, NPCID.GoblinSorcerer, NPCID.DarkCaster, NPCID.GoblinArcher, NPCID.Harpy, NPCID.Gastropod, NPCID.Necromancer, 285, 286, 471 })
				if (seen.Add(t)) { var pr = new NPC(); pr.SetDefaults(t); types.Add((t, Lang.GetNPCNameValue(t), pr.aiStyle)); }
			string filter = Environment.GetEnvironmentVariable("LEVY_AUDIT_TYPES"); // lab: names or ids, comma separated
			if (!string.IsNullOrEmpty(filter)) { var want = new HashSet<string>(filter.Split(',').Select(x => x.Trim().Replace('_', ' '))); types = types.Where(x => want.Contains(x.n) || want.Contains(x.t.ToString())).ToList(); }
			int reps = int.TryParse(Environment.GetEnvironmentVariable("LEVY_AUDIT_REPEAT"), out int rp) && rp > 1 ? rp : 1;
			if (reps > 1) types = Enumerable.Range(0, reps).SelectMany(_ => types).ToList();
			var bad = new List<string>(); int run = 0;
			foreach (var (t, n, ai) in types)
			{
				if (!avatar.NPC.active || avatar.NPC.life <= 0) { town.active = false; Step(2); town = Commission(NPCID.Merchant, 0, out avatar); if (avatar == null) break; }
				var r = AuditType(avatar, t, n, ai, ai == NPCAIStyleID.Caster ? FightTicks * 3 : FightTicks, ai == NPCAIStyleID.Caster ? IdleTicks * 2 : IdleTicks);
				run++; if (!r.Pass) { bad.Add(n + " [" + r + "]"); _mod.Logger.Info("[OfficerWorld] OBSERVED audit failure: " + r); }
			}
			Note($"aim audit core set: {run} creature types, {run - bad.Count} pass");
			Check($"aim audit: every creature in the core set hurts the enemy with its attacks, aims none at a player and throws nothing at no enemy ({bad.Count} of {run} fail)", bad.Count == 0 && (run > 30 || !string.IsNullOrEmpty(filter)));
			Check("aim audit: every excluded type is out of the roster", ConjureRoster.AimExcluded.Keys.All(t => ConjureRoster.AllFacts().All(f => f.Type != t)));
			Dismiss(town);
			KillEnemy();
		}
	}
}
