using System;
using Terraria;
using Terraria.ModLoader;
using ThoriumMod;
using Levy.Common.Compat.Thorium;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>The Thorium-touching half of the combat harness (JIT-gated so it never resolves a Thorium type when Thorium is absent). Read-only probes.</summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class HarnessThoriumProbe
	{
		/// <summary>"bard", "healer:&lt;role&gt;" (Levy's HealerRole) or "".</summary>
		public static string KindOf(Item it)
		{
			if (BardCompat.IsBardWeapon(it)) return "bard";
			HealerRole r = HealerCompat.RoleOf(it);
			return r != HealerRole.None ? "healer:" + r : "";
		}

		/// <summary>Current Inspiration of a bard soldier (on his stat dummy's ThoriumPlayer), -1 when unreadable.</summary>
		public static int BardResource(SoldierNPC s)
		{
			try { return s?.Dummy == null ? -1 : s.Dummy.GetModPlayer<ThoriumPlayer>().bardResource; } catch (Exception) { return -1; }
		}

		public static int BardMax(SoldierNPC s)
		{
			try { return s?.Dummy == null ? -1 : s.Dummy.GetModPlayer<ThoriumPlayer>().bardResourceMax2; } catch (Exception) { return -1; }
		}

		// ---- the real PLAYER (baseline) ----
		public static void PrimePlayerBard(Player p)
		{
			var tp = p.GetModPlayer<ThoriumPlayer>();
			int max = tp.bardResourceMax2 > 0 ? tp.bardResourceMax2 : Math.Max(10, tp.bardResourceMax);
			if (tp.bardResourceMax < 10) tp.bardResourceMax = 10;
			if (tp.bardResourceMax2 < tp.bardResourceMax) tp.bardResourceMax2 = tp.bardResourceMax;
			tp.bardResource = Math.Max(max, tp.bardResourceMax2);
		}
		public static int PlayerBardResource(Player p) { try { return p.GetModPlayer<ThoriumPlayer>().bardResource; } catch (Exception) { return -1; } }
		public static int PlayerBardMax(Player p) { try { return p.GetModPlayer<ThoriumPlayer>().bardResourceMax2; } catch (Exception) { return -1; } }

		private static System.Reflection.FieldInfo _empField;
		/// <summary>Bit n set = the player is under Thorium empowerment type n (read from ThoriumPlayer.Empowerments.ActiveEmpowerments).</summary>
		public static uint PlayerEmpMask(Player p)
		{
			try
			{
				var tp = p.GetModPlayer<ThoriumPlayer>();
				_empField ??= typeof(ThoriumPlayer).GetField("Empowerments", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				object data = _empField?.GetValue(tp);
				if (data == null) return 0;
				var prop = data.GetType().GetProperty("ActiveEmpowerments") ?? (System.Reflection.MemberInfo)data.GetType().GetField("ActiveEmpowerments") as System.Reflection.PropertyInfo;
				object list = prop != null ? prop.GetValue(data) : data.GetType().GetField("ActiveEmpowerments")?.GetValue(data);
				uint m = 0;
				if (list is System.Collections.IEnumerable en) foreach (object o in en) { int t = Convert.ToInt32(o); if (t >= 0 && t < 32) m |= 1u << t; }
				return m;
			}
			catch (Exception) { return 0; }
		}
	}
}
