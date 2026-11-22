using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Compat.Calamity
{
	/// <summary>
	/// What Calamity knows about a boss's life and armour that vanilla fields do not say, read by reflection (Levy has no compile-time reference to CalamityMod; a missing member turns that part off).
	///  * MIRRORED LIFE: Artemis and Apollo are two NPCs with no realLife link; Calamity clamps each one's life to the other's every tick (Artemis.cs / Apollo.cs "Link the HP of both twins")
	///    and finds the partner through <c>CalamityGlobalNPC.draedonExoMechTwinRed</c> / <c>draedonExoMechTwinGreen</c>. <see cref="MirrorPartner"/> reads those slots.
	///  * ARMOUR: Calamity's own damage reduction (<c>CalamityGlobalNPC.DR</c>, with <c>unbreakableDR</c>): Thanatos's closed segments sit at 0.9999 and drop to 0.1 while they vent.
	/// </summary>
	public static class CalamityLife
	{
		private static bool _init, _ok;
		/// <summary>Static NPC-slot fields of CalamityGlobalNPC that name two NPCs whose lives Calamity keeps equal (data: a new mirrored pair is one more line).</summary>
		private static readonly (string a, string b)[] MirrorPairs = { ("draedonExoMechTwinRed", "draedonExoMechTwinGreen") };
		private static FieldInfo[] _pairA, _pairB;
		private static GlobalNPC _calGlobal;
		private static PropertyInfo _dr; private static FieldInfo _unbreakable;

		private static void Init()
		{
			if (_init) return; _init = true;
			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
				Type g = cal.Code.GetType("CalamityMod.NPCs.CalamityGlobalNPC");
				if (g == null) return;
				_pairA = new FieldInfo[MirrorPairs.Length]; _pairB = new FieldInfo[MirrorPairs.Length];
				for (int i = 0; i < MirrorPairs.Length; i++)
				{
					_pairA[i] = g.GetField(MirrorPairs[i].a, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
					_pairB[i] = g.GetField(MirrorPairs[i].b, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
				}
				_dr = g.GetProperty("DR", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				_unbreakable = g.GetField("unbreakableDR", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				cal.TryFind("CalamityGlobalNPC", out _calGlobal);
				_ok = true;
			}
			catch (Exception) { _ok = false; }
		}

		/// <summary>World check: the twin link is not read (only the boss rule's fall-back check can find the partner).</summary>
		internal static bool MirrorOff;

		/// <summary>True when Calamity is loaded and its members were found.</summary>
		public static bool Available { get { Init(); return _ok; } }

		/// <summary>The NPC slot Calamity mirrors <paramref name="npc"/>'s life with (the other Exo twin), or -1.</summary>
		public static int MirrorPartner(NPC npc)
		{
			Init();
			if (!_ok || npc == null || MirrorOff) return -1;
			try
			{
				for (int i = 0; i < MirrorPairs.Length; i++)
				{
					if (_pairA[i] == null || _pairB[i] == null) continue;
					int a = (int)_pairA[i].GetValue(null), b = (int)_pairB[i].GetValue(null);
					if (a == npc.whoAmI && b >= 0 && b != a) return b;
					if (b == npc.whoAmI && a >= 0 && a != b) return a;
				}
			}
			catch (Exception) { }
			return -1;
		}

		/// <summary>Calamity's damage reduction on this NPC (0 without Calamity or when unknown), and whether it is unbreakable.</summary>
		public static float DamageReduction(NPC npc, out bool unbreakable)
		{
			unbreakable = false;
			Init();
			if (!_ok || npc == null || _calGlobal == null || _dr == null) return 0f;
			try
			{
				if (!npc.TryGetGlobalNPC(_calGlobal, out GlobalNPC inst) || inst == null) return 0f;
				if (_unbreakable != null) unbreakable = (bool)_unbreakable.GetValue(inst);
				return (float)_dr.GetValue(inst);
			}
			catch (Exception) { return 0f; }
		}
	}
}
