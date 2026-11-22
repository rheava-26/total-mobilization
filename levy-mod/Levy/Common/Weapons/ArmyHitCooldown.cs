using System;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Util;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// EACH LEVY HITS AS ITS OWN PLAYER. An enemy keeps one hit cooldown per player slot (<c>npc.immune[owner]</c>): a piercing or exploding shot with the default immunity sets it to 10 ticks when it hits, and while it runs
	/// that slot's piercing shots, local-immunity shots and explosions cannot hit the enemy (Projectile.Damage: <c>npc.immune[owner] == 0</c>). Every levy's shot is owned by the commander's slot, so the whole army
	/// and the commander shared ONE cooldown: one Exploding Bullet's blast froze four levies' Exploding Bullets out of the target for 10 ticks (four levies did a quarter of what one does each), and a levy's blast
	/// froze the commander's own piercing shots out too.
	/// <para/>The fix: for the length of the hit pass of a levy's shot (Projectile.Damage, which is where the cooldown is read and written), every enemy's cooldown for the owner slot holds that LEVY's own cooldown;
	/// whatever the pass wrote is kept as the levy's, and the slot's real value (the commander's own) is put back in the same finally. The commander's own shots never see a levy's cooldown, a levy never sees hers
	/// or another levy's, and one levy's own shots throttle each other exactly as a player's do. Server / single player only (where the hit is decided). Lab A/B: env LEVY_ARMYIFRAMES=0.
	/// </summary>
	public sealed class ArmyHitCooldown : ModSystem
	{
		public static readonly bool Disabled = Environment.GetEnvironmentVariable("LEVY_ARMYIFRAMES") == "0";
		/// <summary>Lab counters: hit passes run with a levy's own cooldowns; enemies whose cooldown the levy's passes set.</summary>
		public static long Passes, Set;

		private const int MaxDepth = 8;
		/// <summary>Per soldier (by NPC slot): the body it belongs to and, per enemy slot, the tick its cooldown ends.</summary>
		private static readonly object[] _owners = new object[Main.maxNPCs];
		private static readonly uint[][] _until = new uint[Main.maxNPCs][];
		private static readonly int[][] _saved = new int[MaxDepth][];
		private static int _depth;
		private static bool _hooked;

		public override void Load()
		{
			if (Disabled) return;
			On_Projectile.Damage += DamageDetour;
			_hooked = true;
		}

		public override void Unload()
		{
			if (_hooked) On_Projectile.Damage -= DamageDetour;
			_hooked = false; Reset();
		}

		public override void OnWorldLoad() => Reset();
		public override void OnWorldUnload() => Reset();
		private static void Reset() { Array.Clear(_owners); Array.Clear(_until); _depth = 0; }

		private static void DamageDetour(On_Projectile.orig_Damage orig, Projectile self)
		{ _hl_DamageDetour_1++;
			if (_depth >= MaxDepth || !self.friendly || self.damage <= 0 || self.owner < 0 || self.owner >= Main.maxPlayers || !LevyUtil.IsServerOrSingle
				|| !self.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex < 0 || tag.SoldierIndex >= Main.maxNPCs)
			{ orig(self); return; }
			NPC body = Main.npc[tag.SoldierIndex];
			Player owner = Main.player[self.owner];
			if (!body.active || body.ModNPC == null || owner == null || !owner.active) { orig(self); return; }
			int o = self.owner, si = tag.SoldierIndex;
			uint[] until = _until[si];
			if (until == null || !ReferenceEquals(_owners[si], body.ModNPC)) { until = _until[si] = new uint[Main.maxNPCs]; _owners[si] = body.ModNPC; }
			int[] saved = _saved[_depth] ??= new int[Main.maxNPCs];
			uint now = Main.GameUpdateCount;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active) { saved[i] = int.MinValue; continue; }
				int real = n.immune[o], mine = until[i] > now ? (int)(until[i] - now) : 0;
				saved[i] = real;
				if (real != mine) n.immune[o] = mine;
			}
			_depth++; Passes++;
			try { orig(self); }
			finally
			{
				_depth--;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					int real = saved[i];
					if (real == int.MinValue) continue;
					NPC n = Main.npc[i];
					int v = n.immune[o], mine = until[i] > now ? (int)(until[i] - now) : 0;
					if (v != mine) { until[i] = v > 0 ? now + (uint)v : 0u; if (v > 0) Set++; }
					if (v != real) n.immune[o] = real;
				}
			}
		} private static int _hl_DamageDetour_1;
	}
}
