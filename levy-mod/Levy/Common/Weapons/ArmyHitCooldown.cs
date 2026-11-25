using System;
using System.Collections.Generic;
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
	/// <para/>A driven minion (owner 255, the dummy slot) is covered the same way: its cooldown is its levy's own, not one the whole army shares.
	/// <para/>The fix: for the length of the hit pass of a levy's shot (Projectile.Damage, which is where the cooldown is read and written), every enemy's cooldown for the owner slot holds that LEVY's own cooldown;
	/// whatever the pass wrote is kept as the levy's, and the slot's real value (the commander's own) is put back in the same finally. The commander's own shots never see a levy's cooldown, a levy never sees hers
	/// or another levy's, and one levy's own shots throttle each other exactly as a player's do. Server / single player only (where the hit is decided). Lab A/B: env LEVY_ARMYIFRAMES=0.
	/// </summary>
	public sealed class ArmyHitCooldown : ModSystem
	{
		public static readonly bool Disabled = Environment.GetEnvironmentVariable("LEVY_ARMYIFRAMES") == "0";
		/// <summary>Lab A/B: LEVY_ARMYIDSTATIC=0 leaves the game's static ID immunity (one row per projectile type) shared by the whole army, as it was before 2026-11-25.</summary>
		public static readonly bool IdStaticShared = Environment.GetEnvironmentVariable("LEVY_ARMYIDSTATIC") == "0";
		/// <summary>Lab counters: hit passes run with a levy's own cooldowns; enemies whose cooldown the levy's passes set.</summary>
		public static long Passes, Set, IdPasses;

		private const int MaxDepth = 8;
		/// <summary>Per soldier (by NPC slot): the body it belongs to and, per enemy slot, the tick its cooldown ends.</summary>
		private static readonly object[] _owners = new object[Main.maxNPCs];
		private static readonly uint[][] _until = new uint[Main.maxNPCs][];
		private static readonly int[][] _saved = new int[MaxDepth][];
		/// <summary>Per soldier (by NPC slot): the static-ID-immunity rows (projectile type to the tick each enemy slot is free again) that levy hits with, and the body they belong to.</summary>
		private static readonly Dictionary<int, uint[]>[] _idRows = new Dictionary<int, uint[]>[Main.maxNPCs];
		private static readonly object[] _idOwners = new object[Main.maxNPCs];
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
		private static void Reset() { Array.Clear(_owners); Array.Clear(_until); Array.Clear(_idRows); Array.Clear(_idOwners); _depth = 0; }

		private static void DamageDetour(On_Projectile.orig_Damage orig, Projectile self)
		{ _hl_DamageDetour_1++;
			if (_depth >= MaxDepth || !self.friendly || self.damage <= 0 || self.owner < 0 || self.owner > Main.maxPlayers || !LevyUtil.IsServerOrSingle
				|| !self.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex < 0 || tag.SoldierIndex >= Main.maxNPCs)
			{ orig(self); return; }
			NPC body = Main.npc[tag.SoldierIndex];
			Player owner = Main.player[self.owner];
			// a driven minion is owned by slot 255 (SoldierNPC.DummySlot, Main.maxPlayers): the dummy stands in that slot for its frame, and npc.immune[255] is the cooldown every levy's minions read, so without this pass the whole
			// army's minions shared one cooldown and four levies with a contact minion did what one does (measured 2026-11-25, IdentityWorldCheck.SummonAB army mode)
			if (!body.active || body.ModNPC == null || owner == null || (!owner.active && self.owner < Main.maxPlayers)) { orig(self); return; }
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
			// the same for the game's STATIC ID immunity (Baby Slime, Flinx, Vampire Frog, Stormjaw ... : Projectile.usesIDStaticNPCImmunity): its cooldown is one row per projectile TYPE, shared by every owner, so the
			// whole army's slimes shared one hit cooldown on a boss (4 levies with a Slime Staff did what 1 does: measured 2026-11-25, IdentityWorldCheck.SummonAB army mode). Each levy gets a row of its own per type for the
			// length of its pass (references swapped, nothing copied) and the real row is put back in the same finally.
			uint[][] idRows = Projectile.perIDStaticNPCImmunity; uint[] realRow = null;
			if (!IdStaticShared && self.usesIDStaticNPCImmunity && idRows != null && self.type >= 0 && self.type < idRows.Length && idRows[self.type] != null)
			{
				realRow = idRows[self.type];
				var mineRows = _idRows[si] ??= new Dictionary<int, uint[]>();
				if (!ReferenceEquals(_idOwners[si], body.ModNPC)) { mineRows.Clear(); _idOwners[si] = body.ModNPC; }
				if (!mineRows.TryGetValue(self.type, out uint[] row) || row.Length != realRow.Length) mineRows[self.type] = row = new uint[realRow.Length];
				idRows[self.type] = row; IdPasses++;
			}
			_depth++; Passes++;
			try { orig(self); }
			finally
			{
				if (realRow != null) idRows[self.type] = realRow;
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
