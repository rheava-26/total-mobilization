using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Reads which damage class a set of armor favours, so the armory can issue a matching weapon.
	/// Equips the three pieces on a private scratch Player, runs the vanilla equip pass, and compares the per-class damage/crit bonuses.
	/// Results are cached by the three armor item types.
	/// </summary>
	public static class ArmorDoctrine
	{
		/// <summary>Minimum bonus (damage fraction + crit points x 0.005) the leading class needs.</summary>
		public const float MinBonus = 0.03f;
		/// <summary>The leading class must beat the runner-up by at least this much, else the armor is generic/hybrid.</summary>
		public const float MinLead = 0.02f;
		private const float CritWeight = 0.005f; // 10 crit points count like 5% damage

		private static readonly DamageClass[] Classes = { DamageClass.Melee, DamageClass.Ranged, DamageClass.Magic, DamageClass.Summon };
		private static readonly Dictionary<(int, int, int), int> _cache = new(); // value: index into Classes, or -1 for no preference
		private static Player _scratch;
		private static float[] _baseline;
		private static bool _broken;
		/// <summary>How many times the vanilla equip pass actually ran (cache misses); for diagnostics.</summary>
		public static int PipelineRuns { get; private set; }

		/// <summary>Drop cached state (world load / mod unload).</summary>
		public static void Reset() { _cache.Clear(); _scratch = null; _baseline = null; _broken = false; }

		/// <summary>The damage class the given armor favours, or null when none stands out (no armor, generic armor, hybrids, or any failure).</summary>
		public static DamageClass PreferredClass(Item head, Item body, Item legs)
		{
			int h = TypeOf(head), b = TypeOf(body), l = TypeOf(legs);
			if (h == 0 && b == 0 && l == 0) return null;
			if (_broken) return null;
			var key = (h, b, l);
			if (_cache.TryGetValue(key, out int idx)) return idx >= 0 ? Classes[idx] : null;
			idx = Compute(h, b, l);
			_cache[key] = idx;
			return idx >= 0 ? Classes[idx] : null;
		}

		private static int TypeOf(Item it) => it == null || it.IsAir ? 0 : it.type;

		private static int Compute(int head, int body, int legs)
		{
			try {
				if (!LevyConfig.Get.UseVanillaStatPipeline) return -1;
				EnsureScratch();
				_baseline ??= Measure(0, 0, 0, null);
				var s = Measure(head, body, legs, _baseline);
				PipelineRuns++;
				int best = -1; float bestV = float.NegativeInfinity, second = float.NegativeInfinity;
				for (int i = 0; i < s.Length; i++) {
					if (s[i] > bestV) { second = bestV; bestV = s[i]; best = i; }
					else if (s[i] > second) second = s[i];
				}
				return best >= 0 && bestV >= MinBonus && bestV - second >= MinLead ? best : -1;
			}
			catch (Exception e) {
				_broken = true;
				Levy.Instance?.Logger.Warn("Levy: armor-class read failed, weapons will be issued without a class preference: " + e);
				return -1;
			}
		}

		private static void EnsureScratch()
		{
			if (_scratch != null) return;
			_scratch = new Player();
			_scratch.whoAmI = SoldierNPC.DummySlot;
			_scratch.active = true; _scratch.dead = false; _scratch.ghost = false;
			_scratch.selectedItem = 0;
		}

		/// <summary>Per-class bonus after equipping the three pieces; <paramref name="baseline"/> (the bare-player reading) is subtracted so default crit cancels.</summary>
		private static float[] Measure(int head, int body, int legs, float[] baseline)
		{
			var p = _scratch;
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			for (int i = 0; i < p.dye.Length; i++) p.dye[i] = new Item();
			if (head != 0) p.armor[0].SetDefaults(head);
			if (body != 0) p.armor[1].SetDefaults(body);
			if (legs != 0) p.armor[2].SetDefaults(legs);
			p.inventory[0] = new Item();
			p.selectedItem = 0;
			p.ResetEffects();
			p.UpdateEquips(SoldierNPC.DummySlot);
			p.UpdateArmorSets(SoldierNPC.DummySlot);

			var r = new float[Classes.Length];
			for (int i = 0; i < Classes.Length; i++) {
				var dmg = p.GetDamage(Classes[i]);
				r[i] = (dmg.Additive - 1f) + (dmg.Multiplicative - 1f) + p.GetCritChance(Classes[i]) * CritWeight;
				// Summoner armor mostly buys minion slots: every slot above the bare one counts like extra summon damage.
				if (Classes[i] == DamageClass.Summon) r[i] += Math.Max(0, p.maxMinions - 1) * LevyConstants.MinionDoctrineWeight;
				if (baseline != null) r[i] -= baseline[i];
			}
			// Leave the scratch player clean.
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			return r;
		}
	}

	/// <summary>Clears <see cref="ArmorDoctrine"/>'s cached Player so a reload does not pin the old assembly.</summary>
	public class ArmorDoctrineSystem : ModSystem
	{
		public override void OnWorldLoad() => ArmorDoctrine.Reset();
		public override void Unload() => ArmorDoctrine.Reset();
	}
}
