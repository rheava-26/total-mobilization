using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// WHAT AN ACCESSORY IS WORTH TO A CLASS. A dummy Player wears the accessory and the delta against bare is read per damage class: damage, crit and attack speed (the class's own part and the part every class
	/// shares), and the non-damage effects a class lives on (mana pool and mana cost for magic; arrow / bullet / rocket damage and ammo saving for ranged; auto-swing, yoyo and knockback gear for melee; minion and sentry
	/// slots for summon). Effects the equip pass cannot show (a mod that sets a flag on its own ModPlayer) are caught from the item's tooltip: a line about melee, magic, ranged, summon (minions, sentries, whips) or rogue
	/// (stealth, throwing) names the class. From the two readings come <see cref="AccessoryClassFit"/> (how much it helps this class) and <see cref="AccessoryMismatch"/> (it is a class's accessory and not this one's, and it
	/// helps this class by nothing), which the armory's accessory score uses so a melee levy is never dealt Celestial Cuffs while a fitting or generic accessory is on the shelf. Cached per item type.
	/// </summary>
	public static partial class ArmorDoctrine
	{
		/// <summary>Fit a tooltip line about the class adds (an effect the equip pass cannot show, hidden in a mod's own flags, still names its class).</summary>
		public const float TagFit = 0.08f;

		/// <summary>The accessory reading before class affinity: damage and crit (and minion slots for summon) only, nothing is ever a mismatch. Harness-only switch for before/after runs (env LEVY_ACC_LEGACY=1).</summary>
		internal static bool LegacyAccessoryFit = Environment.GetEnvironmentVariable("LEVY_ACC_LEGACY") == "1";

		private sealed class AccProfile { public float[] Fit, FitLegacy; public int Mask; public int Tags; }
		private static readonly Dictionary<int, AccProfile> _accProf = new();
		private static AccRead _accBase; private static bool _accBaseSet;

		private struct AccRead { public float[] Tot, Raw, Speed, SpeedRaw, Extra; public float Throwing, Minions; }

		private static void ResetAccessoryProfiles() { _accProf.Clear(); _accBaseSet = false; }

		// Non-damage effects read by field name (a name a build does not have is skipped, never an error). ClassIdx indexes Classes (0 melee, 1 ranged, 2 magic, 3 summon).
		private enum XK { Flag, Num, Mod, Reduction }
		private static readonly (string Field, int ClassIdx, XK Kind, float W)[] Extras =
		{
			("manaFlower", 2, XK.Flag, .10f), ("magicCuffs", 2, XK.Flag, .10f), ("manaMagnet", 2, XK.Flag, .08f), ("manaCost", 2, XK.Reduction, 1f), ("statManaMax2", 2, XK.Num, .002f), ("manaRegenBonus", 2, XK.Num, .002f),
			("magicQuiver", 1, XK.Flag, .10f), ("arrowDamage", 1, XK.Mod, 1f), ("bulletDamage", 1, XK.Mod, 1f), ("specialistDamage", 1, XK.Mod, 1f), ("rocketDamage", 1, XK.Mod, 1f),
			("ammoBox", 1, XK.Flag, .10f), ("ammoPotion", 1, XK.Flag, .10f), ("ammoCost75", 1, XK.Flag, .10f), ("ammoCost80", 1, XK.Flag, .10f), ("scope", 1, XK.Flag, .05f),
			("autoReuseGlove", 0, XK.Flag, .10f), ("kbGlove", 0, XK.Flag, .05f), ("magmaStone", 0, XK.Flag, .05f), ("yoyoGlove", 0, XK.Flag, .10f), ("counterWeight", 0, XK.Num, .05f), ("yoyoString", 0, XK.Flag, .05f), ("meleeScaleGlove", 0, XK.Flag, .05f),
			("maxTurrets", 3, XK.Num, .10f), ("dd2Accessory", 3, XK.Flag, .10f),
		};
		private static MemberInfo[] _extraMembers;

		private static float ReadMember(Player p, int k)
		{
			var m = _extraMembers[k];
			if (m == null) return 0f;
			object v = m is FieldInfo f ? f.GetValue(p) : ((PropertyInfo)m).GetValue(p);
			switch (v)
			{
				case bool b: return b ? 1f : 0f;
				case int i: return i;
				case float x: return x;
				case StatModifier sm: return (sm.Additive - 1f) + (sm.Multiplicative - 1f);
				default: return 0f;
			}
		}

		private static float[] ReadExtras(Player p)
		{
			if (_extraMembers == null)
			{
				var mem = new MemberInfo[Extras.Length];
				for (int k = 0; k < Extras.Length; k++)
					mem[k] = (MemberInfo)typeof(Player).GetField(Extras[k].Field, BindingFlags.Public | BindingFlags.Instance) ?? typeof(Player).GetProperty(Extras[k].Field, BindingFlags.Public | BindingFlags.Instance);
				_extraMembers = mem;
			}
			var r = new float[Extras.Length];
			for (int k = 0; k < r.Length; k++) { try { r[k] = ReadMember(p, k); } catch (Exception) { r[k] = 0f; } }
			return r;
		}

		private static AccRead ReadAccessory(int type)
		{
			EnsureScratch();
			var p = _scratch;
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			for (int i = 0; i < p.dye.Length; i++) p.dye[i] = new Item();
			if (type != 0) p.armor[3].SetDefaults(type);
			p.inventory[0] = new Item();
			p.selectedItem = 0;
			p.ResetEffects();
			SafeUpdateEquips(p);
			p.UpdateArmorSets(SoldierNPC.DummySlot);
			int n = Classes.Length;
			var r = new AccRead { Tot = new float[n], Raw = new float[n], Speed = new float[n], SpeedRaw = new float[n], Extra = new float[n] };
			var ex = ReadExtras(p);
			r.Minions = Math.Max(0, p.maxMinions - 1);
			for (int i = 0; i < n; i++)
			{
				var c = Classes[i];
				var td = p.GetTotalDamage(c); var rd = p.GetDamage(c);
				r.Tot[i] = (td.Additive - 1f) + (td.Multiplicative - 1f) + p.GetTotalCritChance(c) * CritWeight;
				r.Raw[i] = (rd.Additive - 1f) + (rd.Multiplicative - 1f) + p.GetCritChance(c) * CritWeight;
				r.Speed[i] = p.GetTotalAttackSpeed(c) - 1f;
				var rs = p.GetAttackSpeed(c);
				r.SpeedRaw[i] = rs - 1f;
				if (c == DamageClass.Summon) r.Extra[i] += Math.Max(0, p.maxMinions - 1) * LevyConstants.MinionDoctrineWeight;
			}
			{ var th = p.GetDamage(DamageClass.Throwing); r.Throwing = (th.Additive - 1f) + (th.Multiplicative - 1f) + p.GetCritChance(DamageClass.Throwing) * CritWeight; }
			for (int k = 0; k < Extras.Length; k++)
			{
				var e = Extras[k]; if (e.ClassIdx >= n) continue;
				r.Extra[e.ClassIdx] += (e.Kind == XK.Reduction ? (1f - ex[k]) : ex[k]) * e.W; // a cost reduction reads 1 - value; the bare reading's 0 is subtracted by the caller
			}
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			return r;
		}

		private static readonly Regex NegCtx = new(@"\b(from|against|by|of|to)\s+(melee|ranged|magic|summon)\w*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex[] TagRx =
		{
			new(@"\bmelee\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new(@"\branged\b|\barrows?\b|\bbullets?\b|\bammo(nition)?\b|\brockets?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new(@"\bmagic(al)?\b|\bmana\b|\bspells?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new(@"\bminions?\b|\bsentr(y|ies)\b|\bsummon(er)? (damage|weapons?|crit\w*)|\bsummoner\b|\bwhips?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
		};
		private static readonly Regex RogueRx = new(@"\brogue\b|\bstealth\b|\bthrowing\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <summary>The accessory's tooltip as plain lines (empty when it has none or the read fails).</summary>
		internal static string TooltipText(Item it)
		{
			try
			{
				var tt = it.ToolTip; if (tt == null) return "";
				var sb = new StringBuilder();
				for (int i = 0; i < tt.Lines; i++) sb.Append(tt.GetLine(i)).Append('\n');
				return sb.ToString();
			}
			catch (Exception) { return ""; }
		}

		/// <summary>Bit i set when the tooltip is about class i of <see cref="Classes"/>.</summary>
		internal static int TooltipTags(string text)
		{
			if (string.IsNullOrEmpty(text)) return 0;
			text = NegCtx.Replace(text, " ");
			int tags = 0;
			for (int i = 0; i < Classes.Length; i++)
			{
				bool hit = i < TagRx.Length ? TagRx[i].IsMatch(text) : Classes[i].Name.IndexOf("rogue", StringComparison.OrdinalIgnoreCase) >= 0 && RogueRx.IsMatch(text);
				if (hit) tags |= 1 << i;
			}
			return tags;
		}

		private static float extraSummonMinions(AccRead a, AccRead b, int i) => Math.Max(0f, a.Minions - b.Minions) * LevyConstants.MinionDoctrineWeight;

		private static AccProfile ProfileOf(Item acc)
		{
			if (_accProf.TryGetValue(acc.type, out var pr)) return pr;
			int n = Classes.Length;
			pr = new AccProfile { Fit = new float[n], FitLegacy = new float[n] };
			try
			{
				if (LevyConfig.Get.UseVanillaStatPipeline)
				{
					if (!_accBaseSet) { _accBase = ReadAccessory(0); _accBaseSet = true; }
					var b = _accBase; var a = ReadAccessory(acc.type);
					pr.Tags = TooltipTags(TooltipText(acc));
					for (int i = 0; i < n; i++)
					{
						float extra = Math.Max(0f, a.Extra[i] - b.Extra[i]);
						float stat = Math.Max(0f, a.Tot[i] - b.Tot[i]) + Math.Max(0f, a.Speed[i] - b.Speed[i]) + extra;
						float raw = Math.Max(0f, a.Raw[i] - b.Raw[i]) + Math.Max(0f, a.SpeedRaw[i] - b.SpeedRaw[i]) + extra;
						if (i >= VanillaClassCount && Classes[i] != DamageClass.Throwing && Classes[i].CountsAsClass(DamageClass.Throwing)) raw += Math.Max(0f, a.Throwing - b.Throwing);
						bool tagged = (pr.Tags & (1 << i)) != 0;
						pr.Fit[i] = stat + (tagged ? TagFit : 0f);
						pr.FitLegacy[i] = Math.Max(0f, a.Tot[i] - b.Tot[i]) + (Classes[i] == DamageClass.Summon ? extraSummonMinions(a, b, i) : 0f);
						if (raw > 0f || tagged) pr.Mask |= 1 << i;
					}
				}
			}
			catch (Exception) { pr = new AccProfile { Fit = new float[n], FitLegacy = new float[n] }; }
			_accProf[acc.type] = pr;
			return pr;
		}

		/// <summary>How much an accessory helps the given class (a fraction like the armor reading: damage + 0.005 per crit point + attack speed + the class's own non-damage effects + <see cref="TagFit"/> for a tooltip line about the class); 0 when it does not or the read fails.</summary>
		public static float AccessoryClassFit(Item acc, DamageClass cls)
		{
			if (acc == null || acc.IsAir || cls == null || _broken) return 0f;
			int ci = Array.IndexOf(Classes, cls);
			if (ci < 0) return 0f;
			var pr = ProfileOf(acc);
			return Math.Max(0f, LegacyAccessoryFit ? pr.FitLegacy[ci] : pr.Fit[ci]);
		}

		/// <summary>True when the accessory is made for one or more other classes (its damage, speed, class effects or tooltip name them) and helps <paramref name="cls"/> by nothing: a melee levy's Mana Flower. Generic gear never mismatches.</summary>
		public static bool AccessoryMismatch(Item acc, DamageClass cls)
		{
			if (acc == null || acc.IsAir || cls == null || _broken || LegacyAccessoryFit) return false;
			int ci = Array.IndexOf(Classes, cls);
			if (ci < 0) return false;
			var pr = ProfileOf(acc);
			return pr.Mask != 0 && (pr.Mask & (1 << ci)) == 0 && pr.Fit[ci] <= 0f;
		}

		/// <summary>Test / log use: the accessory's fit per class, a star on each class it is for (none = generic).</summary>
		internal static string DebugAccessory(Item acc)
		{
			var pr = ProfileOf(acc);
			var sb = new StringBuilder();
			for (int i = 0; i < Classes.Length; i++) sb.Append(Classes[i].Name).Append(((pr.Mask >> i) & 1) != 0 ? "*" : "").Append('=').Append(pr.Fit[i].ToString("F3")).Append(' ');
			return sb.ToString().TrimEnd();
		}
	}
}
