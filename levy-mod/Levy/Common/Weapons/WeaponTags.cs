using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// The tags the armory's rules speak in. Every issuable weapon has ONE class tag (melee, ranged, magic, summon, and rogue / bard / healer when those mods are loaded) and ONE derived category tag
	/// (bow, gun, launcher, ...), both worked out from the item itself: its ammo, its shot, its use style and the <see cref="WeaponClassifier"/> family, so a modded weapon is tagged without a table.
	/// A player can add or remove categories and replace the class per item type in an armory (see <see cref="ArmoryRules"/>); the derived tags here are what they edit.
	/// </summary>
	public static class WeaponTags
	{
		/// <summary>Every category id, in the order the rule editor lists them.</summary>
		public static readonly string[] Categories =
		{
			"bow", "repeater", "gun", "launcher", "staff", "tome", "wand", "sword", "shortsword", "spear", "yoyo", "flail", "boomerang", "whip", "minion", "sentry", "thrown", "held",
		};

		/// <summary>The class ids that always exist; rogue, bard and healer follow when their mods are loaded (<see cref="ClassKeys"/>).</summary>
		public static readonly string[] VanillaClasses = { "melee", "ranged", "magic", "summon" };

		private static readonly string[] TomeWords = { "book", "tome", "grimoire", "codex", "scripture", "bible", "spellbook", "lexicon", "manual" };
		private static readonly HashSet<int> VanillaTomes = new()
		{
			ItemID.WaterBolt, ItemID.DemonScythe, ItemID.BookofSkulls, ItemID.CursedFlames, ItemID.GoldenShower, ItemID.CrystalStorm, ItemID.MagnetSphere, ItemID.RazorbladeTyphoon, ItemID.LunarFlareBook,
		};

		private static readonly Dictionary<int, string> _catCache = new();
		private static string[] _classKeys;

		/// <summary>Drop caches (mod unload / world load).</summary>
		public static void Reset() { _catCache.Clear(); _classKeys = null; }

		/// <summary>The class ids available in this game: the four vanilla ones, then rogue / bard / healer for each loaded mod that adds one.</summary>
		public static string[] ClassKeys
		{
			get
			{
				if (_classKeys != null) return _classKeys;
				var l = new List<string>(VanillaClasses);
				foreach (var dc in ExtraDamageClasses.All) { string k = ClassKey(dc); if (k != null && !l.Contains(k)) l.Add(k); }
				return _classKeys = l.ToArray();
			}
		}

		/// <summary>The id of a damage class ("melee", "ranged", "magic", "summon", "rogue", "bard", "healer"), or null for none.</summary>
		public static string ClassKey(DamageClass dc)
		{
			if (dc == null) return null;
			if (ReferenceEquals(dc, DamageClass.Melee)) return "melee";
			if (ReferenceEquals(dc, DamageClass.Ranged)) return "ranged";
			if (ReferenceEquals(dc, DamageClass.Magic)) return "magic";
			if (ReferenceEquals(dc, DamageClass.Summon)) return "summon";
			string n = dc.Name ?? "";
			foreach (string k in new[] { "rogue", "bard", "healer" }) if (n.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return k;
			return null;
		}

		/// <summary>The damage class an id stands for, or null (unknown id, or the mod that adds it is not loaded).</summary>
		public static DamageClass ClassFromKey(string key)
		{
			switch (key)
			{
				case "melee": return DamageClass.Melee;
				case "ranged": return DamageClass.Ranged;
				case "magic": return DamageClass.Magic;
				case "summon": return DamageClass.Summon;
				case null: return null;
			}
			foreach (var dc in ExtraDamageClasses.All) if (ClassKey(dc) == key) return dc;
			return null;
		}

		/// <summary>The class id the item has by itself (no armory edits), or null for a class-less weapon.</summary>
		public static string DerivedClass(Item it) => ClassKey(WeaponClassifier.ItemDamageClassDerived(it));

		/// <summary>The category id the item has by itself (no armory edits), or null when it is not an issuable weapon.</summary>
		public static string DerivedCategory(Item it)
		{
			if (it == null || it.IsAir) return null;
			if (_catCache.TryGetValue(it.type, out var c)) return c;
			c = Compute(it);
			_catCache[it.type] = c;
			return c;
		}

		private static string Compute(Item it)
		{
			var p = WeaponClassifier.GetProfile(it);
			switch (p.Family)
			{
				case WeaponFamily.Bow: return NameHas(it, "repeater", "crossbow") ? "repeater" : "bow";
				case WeaponFamily.Gun: return it.useAmmo == AmmoID.Bullet ? "gun" : "launcher";
				case WeaponFamily.Magic: return Item.staff[it.type] ? "staff" : IsTome(it) ? "tome" : "wand";
				case WeaponFamily.Broadsword: return "sword";
				case WeaponFamily.Shortsword: return "shortsword";
				case WeaponFamily.Spear: return "spear";
				case WeaponFamily.Yoyo: return "yoyo";
				case WeaponFamily.Flail: return "flail";
				case WeaponFamily.Boomerang: return "boomerang";
				case WeaponFamily.Whip: return "whip";
				case WeaponFamily.Summon: return "minion";
				case WeaponFamily.Sentry: return "sentry";
				case WeaponFamily.Thrown: case WeaponFamily.ThrownMelee: return "thrown";
				case WeaponFamily.HeldWeapon: return "held";
				default: return null;
			}
		}

		private static bool IsTome(Item it)
		{
			if (it.type < ItemID.Count) return VanillaTomes.Contains(it.type);
			return NameHas(it, TomeWords);
		}

		private static bool NameHas(Item it, params string[] words)
		{
			string n = it.type < ItemID.Count ? (ItemID.Search.TryGetName(it.type, out var s) ? s : "") : (it.ModItem?.Name ?? "");
			foreach (string w in words) if (n.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return true;
			return false;
		}

		/// <summary>The text with its first letter capitalized.</summary>
		public static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

		/// <summary>The plural name of a category or class id, capitalized: a list entry such as "Bows".</summary>
		public static string Title(string id) => Cap(DisplayName(id, true));

		/// <summary>The localized display name of a category or class id.</summary>
		public static string DisplayName(string id, bool plural = false)
			=> string.IsNullOrEmpty(id) ? "-" : Util.LevyUtil.T("Rules." + (plural ? "Plural_" : "Name_") + id);
	}
}
