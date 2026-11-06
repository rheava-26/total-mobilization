using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Common.Systems;
using Levy.Common.Weapons;

namespace Levy.Content.TileEntities
{
	/// <summary>One weapon a reforge run could work on, and where it is.</summary>
	public struct ReforgeCandidate
	{
		public Item Item;
		/// <summary>0 = bulk storage, 1 = weapon rack, 2 = a levy's primary, 3 = a levy's sidearm.</summary>
		public byte Region;
		public int Slot;
		/// <summary>The levy holding it (Region 2 and 3), else null.</summary>
		public SoldierRecord Holder;
		public bool Held => Region >= 2;
	}

	/// <summary>What a <see cref="ReforgePlan"/> would do if run now. Estimates only: the rolls themselves are random.</summary>
	public class ReforgePreview
	{
		/// <summary>Weapons the target set holds.</summary>
		public int Selected;
		/// <summary>Of those, weapons that will be rerolled.</summary>
		public int Weapons;
		/// <summary>Selected weapons that already meet the goal, and that cannot take the named prefix.</summary>
		public int AlreadyMet, NotApplicable;
		public double EstRolls;
		public long EstCost;
		/// <summary>The cost estimate was cut down to the plan's spending cap.</summary>
		public bool CapApplied;
		public long Treasury;
		public bool NoPayer;
	}

	/// <summary>
	/// Targeted reforging. A run touches only the weapons its <see cref="ReforgePlan"/> names (levies' hands, recent deposits, hand-picked slots, a class or category), rerolls each toward the plan's goal (today's floor,
	/// or one named prefix) with the unchanged roll loop and price, and never spends past the plan's cap. <see cref="ReforgeToFloor"/> keeps working as before.
	/// </summary>
	public partial class ArmoryTE
	{
		/// <summary>Every weapon a plan could name, in the order a run works: levies' hands, the weapon rack, then bulk storage. Server / single player see the levies; a client sees storage and the rack only.</summary>
		public List<ReforgeCandidate> ReforgeCandidates()
		{
			var list = new List<ReforgeCandidate>();
			foreach (var rec in new List<SoldierRecord>(ServedSoldiers()))
			{
				if (rec?.Gear == null) continue;
				if (!IsAir(rec.Gear.Weapon)) list.Add(new ReforgeCandidate { Item = rec.Gear.Weapon, Region = 2, Holder = rec });
				if (!IsAir(rec.Gear.Sidearm)) list.Add(new ReforgeCandidate { Item = rec.Gear.Sidearm, Region = 3, Holder = rec });
			}
			for (int i = 0; i < IssueWeapons.Length; i++) if (!IsAir(IssueWeapons[i])) list.Add(new ReforgeCandidate { Item = IssueWeapons[i], Region = 1, Slot = i });
			for (int i = 0; i < Storage.Length; i++) if (!IsAir(Storage[i])) list.Add(new ReforgeCandidate { Item = Storage[i], Region = 0, Slot = i });
			return list;
		}

		/// <summary>Whether the plan's target set contains this weapon (it says nothing about the goal). <paramref name="now"/> is the world clock.</summary>
		public static bool InTargetSet(ReforgePlan plan, ReforgeCandidate c, ArmoryRules rules, long now)
		{
			if (plan == null || !IsReforgeable(c.Item)) return false;
			switch (plan.Mode)
			{
				case ReforgeMode.Issued: if (!c.Held) return false; break;
				case ReforgeMode.Recent:
				{
					long stamp = DepositStamp.Of(c.Item);
					if (stamp <= DepositStamp.Adopted || now - stamp > (long)plan.RecentDays * LevyClock.TicksPerDay) return false;
					break;
				}
				case ReforgeMode.Picked: return !c.Held && plan.IsPicked(c.Region, c.Slot); // a hand pick is explicit: no class or category filter narrows it
			}
			rules ??= ArmoryRules.Default();
			if (plan.ClassKey != null && rules.ClassKeyOf(c.Item) != plan.ClassKey) return false;
			if (plan.Category != null && Array.IndexOf(rules.CatsOf(c.Item), plan.Category) < 0) return false;
			return true;
		}

		private enum Verdict { Roll, AlreadyMet, NotApplicable }

		/// <summary>What the goal makes of one weapon: it needs rolling, already meets the goal, or cannot reach it (cannot take a prefix, or cannot take the named one).</summary>
		private static Verdict Judge(Item item, ReforgePlan plan, out Item sample, out double threshold, out double score, out int rollable, out int hitting)
		{
			threshold = 0; score = 0; rollable = 0; hitting = 0; sample = null;
			if (!IsReforgeable(item) || !ContentSamples.ItemsByType.TryGetValue(item.type, out sample) || sample == null) return Verdict.NotApplicable;
			var scores = LearnPrefixScores(sample);
			if (scores.Length == 0) return Verdict.NotApplicable;
			rollable = scores.Length;
			score = PrefixQuality(item, sample);
			if (plan.Goal == ReforgeGoal.Prefix)
			{
				if (plan.PrefixId <= 0 || !sample.CanApplyPrefix(plan.PrefixId)) return Verdict.NotApplicable;
				hitting = 1;
				return item.prefix == plan.PrefixId ? Verdict.AlreadyMet : Verdict.Roll;
			}
			float frac = LevyConstants.UI.ReforgeFloorTopFraction[Math.Max(0, Math.Min(LevyConstants.UI.ReforgeFloorTopFraction.Length - 1, plan.FloorIndex))];
			threshold = FloorThreshold(scores, frac) - 1e-6;
			foreach (double s in scores) if (s >= threshold) hitting++;
			return score < threshold ? Verdict.Roll : Verdict.AlreadyMet;
		}

		/// <summary>Expected paid rolls to hit a goal that a roll meets with probability 1 / <paramref name="rollable"/> x <paramref name="hitting"/>, given at most <see cref="LevyConstants.UI.ReforgeMaxRollsPerWeapon"/>.</summary>
		private static double ExpectedRolls(int rollable, int hitting)
		{
			double p = Math.Clamp((double)Math.Max(1, hitting) / Math.Max(1, rollable), 1e-6, 1.0);
			int m = LevyConstants.UI.ReforgeMaxRollsPerWeapon;
			return (1.0 - Math.Pow(1.0 - p, m)) / p;
		}

		/// <summary>What the plan would do right now: how many weapons it holds, how many need rolling, and about what that costs. Reads only; client-safe (a client sees storage and the rack, not the levies' hands).</summary>
		public ReforgePreview PreviewReforge(ReforgePlan plan, BarracksTE payer)
		{
			var pv = new ReforgePreview { NoPayer = payer == null, Treasury = payer?.Treasury ?? 0 };
			if (plan == null) return pv;
			long now = LevyClock.Now;
			double cost = 0;
			foreach (var c in ReforgeCandidates())
			{
				if (!InTargetSet(plan, c, Rules, now)) continue;
				pv.Selected++;
				var v = Judge(c.Item, plan, out _, out _, out _, out int rollable, out int hitting);
				if (v == Verdict.AlreadyMet) { pv.AlreadyMet++; continue; }
				if (v == Verdict.NotApplicable) { pv.NotApplicable++; continue; }
				pv.Weapons++;
				double rolls = ExpectedRolls(rollable, hitting);
				pv.EstRolls += rolls;
				long mean = _meanRollPrice.TryGetValue(c.Item.type, out var mp) ? mp : ReforgePrice(c.Item);
				cost += ReforgePrice(c.Item) + (rolls - 1) * mean;
			}
			pv.EstCost = (long)Math.Round(cost);
			if (plan.CapCopper > 0 && pv.EstCost > plan.CapCopper) { pv.EstCost = plan.CapCopper; pv.CapApplied = true; }
			return pv;
		}

		/// <summary>
		/// Runs the plan: every weapon in its target set that has not met the goal is rerolled (a paid roll each, from <paramref name="payer"/>'s treasury, at the vanilla reforge price) until it does, up to the usual roll cap
		/// per weapon. A weapon that misses keeps the best prefix it rolled. The run stops spending at the plan's cap, and when the treasury cannot pay. Server / single player only.
		/// </summary>
		public ReforgeResult ReforgeTargeted(BarracksTE payer, ReforgePlan plan)
		{
			var res = new ReforgeResult { Log = new List<string>() };
			if (Main.netMode == NetmodeID.MultiplayerClient || plan == null) return res;
			if (payer == null) { res.NoPayer = true; return res; }
			long now = LevyClock.Now;
			long cap = plan.CapCopper > 0 ? plan.CapCopper : long.MaxValue;
			bool changed = false;
			int maxRolls = LevyConstants.UI.ReforgeMaxRollsPerWeapon;
			foreach (var c in ReforgeCandidates())
			{
				if (!InTargetSet(plan, c, Rules, now)) continue;
				res.Selected++;
				var v = Judge(c.Item, plan, out Item sample, out double threshold, out double score, out _, out _);
				if (v == Verdict.AlreadyMet) { res.AlreadyMet++; continue; }
				if (v == Verdict.NotApplicable) { res.NotApplicable++; continue; }
				if (res.RanDry) { res.Short++; continue; }

				int want = plan.PrefixId;
				Func<Item, double, bool> reached = plan.Goal == ReforgeGoal.Prefix ? (t, s) => t.prefix == want : (t, s) => s >= threshold;
				var one = RollLoop(c.Item, sample, score, payer, maxRolls, 0, cap - res.Spent, reached);
				res.Spent += one.Spent;
				res.Rolls += one.Rolls;
				if (one.CapStopped) res.CapHit = true; // the cap only stops what no longer fits: a cheaper weapon later in the run may still be rolled
				else if (one.RanDry) res.RanDry = true;
				if (one.Rolls > 0) res.Weapons++;
				res.Log.Add($"{c.Item.Name}|r{c.Region}|{one.Rolls} rolls|{(one.Reached ? "reached" : "short")}|{one.Spent}");
				if (!ReferenceEquals(one.Best, c.Item)) { PutReforged(c, one.Best); changed = true; }
				if (one.Reached) res.Reached++;
				else { res.Short++; if (one.Rolls >= maxRolls) res.Capped++; }
			}
			if (changed) MarkDirty();
			if (res.Spent > 0) payer.MarkDirty();
			return res;
		}

		/// <summary>Writes a reforged weapon back where the candidate was found, and tells a levy's body his weapon changed.</summary>
		private void PutReforged(ReforgeCandidate c, Item best)
		{
			switch (c.Region)
			{
				case 0: Storage[c.Slot] = best; break;
				case 1: IssueWeapons[c.Slot] = best; break;
				case 2: c.Holder.Gear.Weapon = best; NotifyBody(c.Holder); break;
				case 3: c.Holder.Gear.Sidearm = best; NotifyBody(c.Holder); break;
			}
		}

		private static void NotifyBody(SoldierRecord rec)
		{
			if (rec != null && rec.HasBody && Main.npc[rec.BodyIndex].ModNPC is Content.NPCs.SoldierNPC body) body.NotifyGearChanged();
		}

		/// <summary>The prefix ids a weapon type can be rolled to (those the vanilla reforge could give it), best quality first. Empty when it takes no prefix.</summary>
		public static List<int> ValidPrefixes(Item sample)
		{
			var list = new List<(int id, double q)>();
			if (sample == null || sample.IsAir || !sample.CanHavePrefixes()) return new List<int>();
			int count = PrefixLoader.PrefixCount;
			for (int id = 1; id < count; id++)
			{
				if (!sample.CanApplyPrefix(id)) continue;
				var t = new Item(); t.SetDefaults(sample.type);
				if (!t.Prefix(id) || t.prefix != id) continue;
				list.Add((id, PrefixQuality(t, sample)));
			}
			list.Sort((a, b) => b.q.CompareTo(a.q));
			return list.ConvertAll(x => x.id);
		}

		/// <summary>The prefixes valid for the weapons a plan's target set holds, each with how many of those weapons can take it. The list the prefix picker shows; the goal field of the plan is ignored.</summary>
		public List<(int id, int weapons)> ValidPrefixesFor(ReforgePlan plan)
		{
			var counts = new Dictionary<int, int>();
			var seenType = new Dictionary<int, List<int>>();
			long now = LevyClock.Now;
			foreach (var c in ReforgeCandidates())
			{
				if (!InTargetSet(plan, c, Rules, now)) continue;
				if (!seenType.TryGetValue(c.Item.type, out var valid))
				{
					valid = ContentSamples.ItemsByType.TryGetValue(c.Item.type, out var sample) ? ValidPrefixes(sample) : new List<int>();
					seenType[c.Item.type] = valid;
				}
				foreach (int id in valid) counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
			}
			var res = new List<(int, int)>();
			foreach (var kv in counts) res.Add((kv.Key, kv.Value));
			res.Sort((a, b) => b.Item2 != a.Item2 ? b.Item2.CompareTo(a.Item2) : a.Item1.CompareTo(b.Item1));
			return res;
		}

		/// <summary>The display name of a prefix (vanilla or modded).</summary>
		public static string PrefixName(int id)
		{
			if (id <= 0) return "";
			try
			{
				if (id < PrefixID.Count) return Lang.prefix[id].Value;
				var mp = PrefixLoader.GetPrefix(id);
				return mp != null ? mp.DisplayName.Value : "#" + id;
			}
			catch (Exception) { return "#" + id; }
		}

		/// <summary>Chat lines telling the player how a targeted run went. Server / single player.</summary>
		public static void ReportTargeted(Player to, ReforgeResult r, ReforgePlan plan)
		{
			if (to == null || r == null) return;
			if (r.NoPayer) { RosterSystem.SendToPlayer(to, LevyUtil.T("UI2.ReforgeNoBarracks")); return; }
			if (r.Selected == 0) { RosterSystem.SendToPlayer(to, LevyUtil.T("Reforge.NoTargets")); return; }
			if (r.Rolls == 0 && !r.CapHit && r.Short == 0) { RosterSystem.SendToPlayer(to, LevyUtil.T("Reforge.NothingToDo", r.Selected)); return; }
			if (r.Rolls > 0) RosterSystem.SendToPlayer(to, LevyUtil.T("Reforge.Report", r.Weapons, r.Rolls, LevyUtil.Coins(r.Spent), r.Reached));
			if (r.CapHit) RosterSystem.SendToPlayer(to, LevyUtil.T("Reforge.CapReached", LevyUtil.Coins(plan?.CapCopper ?? 0), r.Short));
			else if (r.RanDry) RosterSystem.SendToPlayer(to, LevyUtil.T("UI.NotEnoughMoney") + " " + LevyUtil.T("UI2.ReforgeBroke", r.Short));
			else if (r.Capped > 0) RosterSystem.SendToPlayer(to, LevyUtil.T("UI2.ReforgeCapped", r.Capped, LevyConstants.UI.ReforgeMaxRollsPerWeapon));
		}
	}
}
