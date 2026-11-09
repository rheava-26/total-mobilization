using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Whether a weapon's shots fly through tiles. A weapon like that needs no line of sight: the soldier fires from where it stands and may pick a target behind a wall (the range still applies).
	/// A shot passes tiles when EVERY damaging projectile the weapon can put out has tile collision off and nothing in its AI switches it on:
	///  * the shot the item names (<c>item.shoot</c>, or the ammo it resolved to);
	///  * for a modded item, every projectile its Shoot / ModifyShootStats / UseItem hooks name (the hook may spawn something other than <c>item.shoot</c>);
	///  * for a modded holdout, every projectile its own AI names (it spawns its bolts itself).
	/// A projectile whose AI sets <c>tileCollide = true</c> (Calamity's Flare Bolt is tile-free until it launches) does not count as tile-free. A vanilla held beam (Last Prism, the Charged Blaster) stops at tiles, so it never counts.
	/// </summary>
	internal static class ShotTiles
	{
		private static readonly Dictionary<long, bool> _cache = new();

		public static bool Passes(Item item, int projType, bool held, int ammoShot = 0)
		{
			if (item == null || item.IsAir) { var s0 = WeaponClassifier.ProjectileSample(projType); return s0 != null && !s0.tileCollide; }
			long key = (((long)item.type << 32) ^ (uint)projType ^ ((long)ammoShot << 16)) ^ (held ? 1L << 62 : 0L);
			if (_cache.TryGetValue(key, out bool cached)) return cached;
			bool result = false;
			try { result = Compute(item, projType, held, ammoShot); }
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: tile-pass probe failed for {item.Name}: {e.Message}"); }
			_cache[key] = result;
			return result;
		}

		private static bool Compute(Item item, int projType, bool held, int ammoShot)
		{
			if (held && item.type < ItemID.Count) return false; // a vanilla held beam stops at tiles
			var types = new List<int>();
			if (projType > 0) types.Add(projType);
			if (ammoShot > 0 && !types.Contains(ammoShot)) types.Add(ammoShot); // the ammo a holdout fires (Firestorm Cannon's flares)
			if (held && item.shoot > 0 && !types.Contains(item.shoot)) types.Add(item.shoot);
			if (item.ModItem != null)
				foreach (Type t in OwnerLockProbe.NamedProjectileTypes(item)) AddType(types, t);
			// a holdout makes its shots itself: follow the names one level down
			if (held) for (int k = 0, n = types.Count; k < n; k++) foreach (Type t in OwnerLockProbe.NamedChildTypes(types[k])) AddType(types, t);
			bool any = false;
			foreach (int id in types)
			{
				Projectile sample = WeaponClassifier.ProjectileSample(id);
				if (sample == null) return false;
				if (!sample.friendly && sample.damage <= 0) continue; // a visual / helper (an implosion mark, a holdout's draw object) does not decide it
				if (sample.tileCollide) return false;
				if (sample.ModProjectile != null && OwnerLockProbe.SetsTileCollide(id)) return false;
				any = true;
			}
			return any;
		}

		private static void AddType(List<int> types, Type t)
		{
			foreach (ModProjectile mp in ModContent.GetContent<ModProjectile>())
				if (mp.GetType() == t) { if (!types.Contains(mp.Type)) types.Add(mp.Type); return; }
		}
	}
}
