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
	}
}
