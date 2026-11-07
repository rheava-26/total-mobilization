using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Movement
{
	/// <summary>
	/// How much movement an accessory gives a soldier: extra jumps (any vanilla or modded ExtraJump) and wings. Read generically by wearing a copy on a dummy player and counting the enabled jumps,
	/// the same way <see cref="MovementAbilities.FromDummy"/> reads a levy's gear; cached per item type. The armory uses it to give every levy one mobility accessory first.
	/// </summary>
	public static class AccessoryMobility
	{
		private static readonly Dictionary<int, (int jumps, bool wings)> Cache = new();
		private static Player _dummy;

		/// <summary>Extra jumps the item grants and whether it is a pair of wings.</summary>
		public static (int jumps, bool wings) Read(Item it)
		{
			if (it == null || it.IsAir || !it.accessory || it.vanity) return (0, false);
			if (Cache.TryGetValue(it.type, out var hit)) return hit;
			var res = (0, it.wingSlot > 0);
			try {
				_dummy ??= new Player();
				var p = _dummy;
				p.ResetEffects();
				for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
				var c = new Item(); c.SetDefaults(it.type);
				p.armor[3] = c;
				p.UpdateEquips(250);
				int jumps = 0;
				var ordered = ExtraJumpLoader.OrderedJumps;
				if (ordered != null) for (int i = 0; i < ordered.Count; i++) { var j = ordered[i]; if (j != null && p.GetJumpState(j).Enabled) jumps++; }
				bool wings = it.wingSlot > 0 || p.wingsLogic > 0;
				res = (jumps, wings);
				p.armor[3] = new Item();
				p.ResetEffects();
			}
			catch (Exception) { }
			Cache[it.type] = res;
			return res;
		}

		/// <summary>0 for nothing; otherwise wings outrank any number of jumps, and more jumps outrank fewer.</summary>
		public static float Value(Item it)
		{
			var (jumps, wings) = Read(it);
			if (wings) return LevyConstants.Armory.MobilityWingsValue;
			return jumps <= 0 ? 0f : LevyConstants.Armory.MobilityJumpValue * Math.Min(jumps, LevyConstants.Armory.MobilityMaxJumpsCounted);
		}

		public static bool Is(Item it) => Value(it) > 0f;

		/// <summary>The best mobility value among the worn accessories (0 if none); empty slots and skipIndex are ignored.</summary>
		public static float Best(Item[] worn, int skipIndex = -1)
		{
			if (worn == null) return 0f;
			float b = 0f;
			for (int i = 0; i < worn.Length; i++) { if (i == skipIndex) continue; var w = worn[i]; if (w == null || w.IsAir) continue; b = Math.Max(b, Value(w)); }
			return b;
		}
	}
}
