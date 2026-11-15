using System;
using System.Reflection;
using System.Reflection.Emit;
using Terraria;

namespace Levy.Common.Owner
{
	/// <summary>
	/// Writes a dummy's <c>talkNPC</c> without the side effects of <c>Player.SetTalkNPC</c>. The identity guard mirrors the commander's conversation into a levy's dummy around every hook a gear mod
	/// receives (twice per hook: in, out), and a levy in modded gear runs a gear frame every tick, so that is over a thousand mirrors a tick with 40 levies. SetTalkNPC with a conversation open recomputes
	/// the player's shopping settings (the happiness report of the shopkeeper, measured at about 8 microseconds); the field write the mirror actually needs costs under 0.1. The shop window keeps a
	/// conversation open for as long as it is up, which is how a shop made the whole army cost three times more per tick (see <see cref="IdentityGuard"/>, ShopLagCheck).
	/// The write goes straight to the property's backing field through a compiled setter; if that field is not found (tML renamed it) the mirror falls back to SetTalkNPC and stays correct, only slow.
	/// </summary>
	public static class TalkMirror
	{
		private static readonly Action<Player, int> Setter = Build();

		/// <summary>Lab A/B switch (ShopLagCheck): write through SetTalkNPC as the guard did before the fix.</summary>
		public static bool ForceSlow;

		/// <summary>True when the cheap write is in use.</summary>
		public static bool Direct => Setter != null;
		/// <summary>Writes that took the cheap path / the vanilla path (self-check counters).</summary>
		public static long Cheap, Slow;

		private static Action<Player, int> Build()
		{
			try
			{
				FieldInfo f = typeof(Player).GetField("<talkNPC>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
				if (f == null || f.FieldType != typeof(int) || f.IsInitOnly) return null;
				var dm = new DynamicMethod("LevyTalkMirror", typeof(void), new[] { typeof(Player), typeof(int) }, typeof(Player).Module, true);
				ILGenerator il = dm.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Stfld, f); il.Emit(OpCodes.Ret);
				return (Action<Player, int>)dm.CreateDelegate(typeof(Action<Player, int>));
			}
			catch (Exception) { return null; }
		}

		/// <summary>Makes <paramref name="p"/>.talkNPC equal <paramref name="npc"/> (no network traffic, no shopping-settings read).</summary>
		public static void Set(Player p, int npc)
		{
			if (p.talkNPC == npc) return;
			if (Setter != null && !ForceSlow) { Setter(p, npc); Cheap++; }
			else { p.SetTalkNPC(npc, true); Slow++; }
		}
	}
}
