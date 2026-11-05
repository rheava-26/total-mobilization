using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// The mod-added damage classes a levy can specialise in beside the four vanilla ones: Calamity's rogue, Thorium's bard and healer. Found at runtime by NAME among the
	/// loaded <see cref="DamageClass"/> content (no reference to either mod, so both stay optional): a class whose name says rogue / bard / healer is that kind's class, and an item
	/// belongs to a kind when its damage type is, or counts as, one of the kind's classes (Thorium's healer tools count as healer). Empty when neither mod is loaded.
	/// </summary>
	public static class ExtraDamageClasses
	{
		private static readonly string[] Kinds = { "rogue", "bard", "healer" };
		/// <summary>Exact names preferred as a kind's canonical class when several classes match its keyword.</summary>
		private static readonly string[] PreferredNames = { "RogueDamageClass", "BardDamage", "HealerDamage" };

		private static List<DamageClass>[] _members;
		private static DamageClass[] _canonical;
		private static DamageClass[] _all;
		private static bool _ready;

		/// <summary>Drop the cached lookup (world load / mod unload).</summary>
		public static void Reset() { _ready = false; _members = null; _canonical = null; _all = null; }

		private static void Ensure()
		{
			if (_ready) return;
			var members = new List<DamageClass>[Kinds.Length];
			for (int k = 0; k < Kinds.Length; k++) members[k] = new List<DamageClass>();
			try {
				foreach (var dc in ModContent.GetContent<DamageClass>()) {
					if (dc == null || dc.Mod == null || dc.Mod.Name == "ModLoader") continue;
					string n = dc.Name ?? "";
					for (int k = 0; k < Kinds.Length; k++)
						if (n.IndexOf(Kinds[k], StringComparison.OrdinalIgnoreCase) >= 0) { members[k].Add(dc); break; }
				}
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("Levy: could not look up the rogue / bard / healer damage classes: " + e.Message); }
			var canon = new DamageClass[Kinds.Length];
			for (int k = 0; k < Kinds.Length; k++) {
				var m = members[k];
				if (m.Count == 0) continue;
				canon[k] = m.FirstOrDefault(c => c.Name == PreferredNames[k]) ?? m.FirstOrDefault(c => c.Name.IndexOf("tool", StringComparison.OrdinalIgnoreCase) < 0) ?? m[0];
			}
			_members = members; _canonical = canon; _all = canon.Where(c => c != null).ToArray(); _ready = true;
		}

		/// <summary>The loaded rogue / bard / healer classes (one canonical class per kind); empty without Calamity / Thorium.</summary>
		public static IReadOnlyList<DamageClass> All { get { Ensure(); return _all; } }

		/// <summary>The rogue / bard / healer class the item's damage type belongs to, or null (a vanilla-class item, or none of these mods loaded).</summary>
		public static DamageClass Of(Item item)
		{
			if (item == null || item.IsAir) return null;
			Ensure();
			if (_all.Length == 0) return null;
			var dt = item.DamageType;
			if (dt == null) return null;
			for (int k = 0; k < Kinds.Length; k++) {
				if (_canonical[k] == null) continue;
				foreach (var c in _members[k]) {
					bool hit;
					try { hit = ReferenceEquals(dt, c) || dt.CountsAsClass(c); } catch (Exception) { hit = false; }
					if (hit) return _canonical[k];
				}
			}
			return null;
		}
	}

	/// <summary>Clears <see cref="ExtraDamageClasses"/>'s lookup on world load / unload so a mod reload never pins a stale class.</summary>
	public class ExtraDamageClassesSystem : ModSystem
	{
		public override void OnWorldLoad() => ExtraDamageClasses.Reset();
		public override void Unload() => ExtraDamageClasses.Reset();
	}
}
