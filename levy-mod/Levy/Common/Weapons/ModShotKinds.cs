using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// What KIND of owner-anchored shot a modded weapon makes, decided from the weapon's own compiled code and its projectile's class chain (never from an item name).
	/// Soldier shots are owned by the commander (or, on a dedicated server, by the null player in slot 255), so a projectile whose AI is written around the player holding it (reading its owner's
	/// position, <c>channel</c> button, cursor and held item) does nothing useful as a plain shot: a charge-and-release staff expires uncharged the moment it spawns, a held shortsword / mace flail sits
	/// at the commander (or dies at once on slot 255) and a plain-shooter soldier stands 46 tiles away from a blade that reaches 5. Each kind maps to the soldier-side family that really delivers it:
	///  * <see cref="ModShotKind.Holdout"/>: a modded projectile that reads <c>Player.channel</c>. The soldier HOLDS it like a player (<see cref="HeldWeapons"/>, dummy in slot 255, cursor on the target) and lets go after a charge.
	///  * <see cref="ModShotKind.Shortsword"/>: a Calamity <c>BaseShortswordProjectile</c> blade (a stab that exists for 12 ticks next to its owner): the soldier stabs with Levy's own thrust at blade reach.
	///  Not a kind: a Calamity mace flail (<c>BaseMaceFlailProjectile</c> / <c>BaseFlailProjectile</c>: spun by its owner, thrown at the cursor when the button is let go) also reads the owner's button but is NOT held. The commander never holds the button,
	///  so a plain shot spins through its windup and throws at once; with the cursor on the target (<see cref="SteerToTargetProjectile"/>) the throw lands. Holding it as a melee holdout was measured worse (it stood at 7 tiles, below the throw's reach).
	/// </summary>
	public enum ModShotKind { None, Holdout, Shortsword }

	public static class ModShotKinds
	{
		private static readonly Dictionary<int, ModShotKind> _cache = new();
		/// <summary>Measurement switch (harness A/B, env LEVY_NO_MODSHOT=1): behave as before this feature (no holdout / blade routing and no cursor steer for modded shots).</summary>
		internal static readonly bool Off = Environment.GetEnvironmentVariable("LEVY_NO_MODSHOT") == "1";

		/// <summary>Mods whose channel-reading holdouts are held like a player would (Calamity: measured 0 damage as plain shots, see docs/boss_lab/dud). Add a mod here only after measuring its holdouts both ways.</summary>
		private static readonly HashSet<string> HoldoutMods = new() { "CalamityMod" };

		/// <summary>Class-chain names of the Calamity base projectiles whose AI is a held melee stab (matched by name: no compile-time reference to Calamity).</summary>
		private const string ShortswordBase = "BaseShortswordProjectile", MaceFlailBase = "BaseMaceFlailProjectile", FlailBase = "BaseFlailProjectile";

		public static ModShotKind Of(Item item)
		{
			if (Off || item == null || item.IsAir || item.type < ItemID.Count) return ModShotKind.None;
			if (_cache.TryGetValue(item.type, out ModShotKind k)) return k;
			k = ModShotKind.None;
			try { k = Decide(item); }
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: shot-kind probe failed for {item.Name}: {e.Message}"); }
			_cache[item.type] = k;
			return k;
		}

		private static ModShotKind Decide(Item item)
		{
			// every projectile the weapon can spawn: its own shoot plus any it names in its Shoot / UseItem hooks
			var types = new List<Type>();
			Projectile sample = item.shoot > 0 ? WeaponClassifier.ProjectileSample(item.shoot) : null;
			if (sample?.ModProjectile != null) types.Add(sample.ModProjectile.GetType());
			foreach (Type t in OwnerLockProbe.NamedProjectileTypes(item)) if (!types.Contains(t)) types.Add(t);

			bool shortsword = false, mace = false;
			foreach (Type t in types)
				for (Type b = t; b != null && b != typeof(ModProjectile) && b != typeof(object); b = b.BaseType)
				{
					if (b.Name == ShortswordBase) shortsword = true;
					else if (b.Name == MaceFlailBase || b.Name == FlailBase) mace = true;
				}
			// A melee weapon whose shot is a held stab is a melee weapon: it fights at blade reach with Levy's own stab.
			if (shortsword && item.CountsAsClass(DamageClass.Melee)) return ModShotKind.Shortsword;
			if (mace) return ModShotKind.None; // thrown at the cursor, not held (see the class summary)
			// A held / charge-and-release holdout reads its owner's button (and is the weapon's own shot).
			// Only mods whose holdouts were measured as duds as plain shots are routed (Thorium's channelled weapons already work through the plain route and its compat; the audit shows what a mod would gain).
			if (HoldoutMods.Contains(item.ModItem?.Mod.Name ?? "") && item.channel && item.shoot > 0 && sample?.ModProjectile != null && OwnerLockProbe.ReadsOwnerButton(item.shoot)
				&& (item.CountsAsClass(DamageClass.Ranged) || item.CountsAsClass(DamageClass.Magic) || item.CountsAsClass(DamageClass.Melee)))
				return ModShotKind.Holdout;
			return ModShotKind.None;
		}
	}

	/// <summary>Measurement only (env LEVY_MODSHOT_AUDIT=1): after all content is set up, logs every modded weapon whose shot kind is not None, with the family it gets, so a content mod's whole catalogue can be reviewed for misrouting.</summary>
	public class ModShotAudit : ModSystem
	{
		public override void PostSetupContent()
		{
			if (Environment.GetEnvironmentVariable("LEVY_MODSHOT_AUDIT") != "1") return;
			try
			{
				int n = 0;
				for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
				{
					Item it = ContentSamples.ItemsByType.TryGetValue(t, out var smp) ? smp : null;
					if (it == null || it.IsAir || it.damage <= 0) continue;
					ModShotKind k = ModShotKinds.Of(it);
					if (k == ModShotKind.None) continue;
					WeaponProfile p = WeaponClassifier.GetProfile(it);
					Mod.Logger.Info($"MODSHOT-AUDIT {it.ModItem?.Mod.Name}/{it.ModItem?.Name} kind={k} family={p.Family} range={p.RangeTiles} class={it.DamageType?.Name} channel={it.channel} shoot={(it.shoot > 0 ? WeaponClassifier.ProjectileSample(it.shoot)?.ModProjectile?.GetType().Name : "-")}");
					n++;
				}
				Mod.Logger.Info($"MODSHOT-AUDIT total {n}");
			}
			catch (Exception e) { Mod.Logger.Warn("MODSHOT-AUDIT failed: " + e.Message); }
		}
	}
}
