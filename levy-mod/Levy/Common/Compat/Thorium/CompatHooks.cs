using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using ThoriumMod;
using ThoriumMod.Items;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>The delegates the Thorium compat hands to Levy (see <see cref="ThoriumCompatSystem"/>). JIT'd only when ThoriumMod is enabled.</summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class CompatHooks
	{
		/// <summary>WeaponClassifier.ExtraCombatClass: Thorium Bard weapons and the Healer weapons a soldier can use are issuable combat weapons (support tools are not).</summary>
		public static bool ExtraCombatClass(Item item) => BardCompat.IsBardWeapon(item) || HealerCompat.IsHealerClassWeapon(item);

		/// <summary>WeaponUse.PreFireGate: true = the soldier may shoot now.</summary>
		public static bool PreFireGate(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			if (BardCompat.IsBardWeapon(item)) return BardCompat.TryPlay(s, item);
			// A pure ally-heal, self-heal or support item is only ever used by the healer driver, never fired at the enemy Levy's brain picked.
			// Radiant damage, lifesteal and hybrid (damage + heal) weapons fire at enemies like any ranged / magic weapon.
			if (HealerCompat.VetoEnemyFire(item)) return false;
			return true;
		}

		/// <summary>
		/// WeaponUse.UseSoundFallback: a Thorium instrument's own sound. Thorium moves an instrument's <c>UseSound</c> into <c>BardItem.PlayedSound</c> (and nulls <c>UseSound</c>) so it can play it
		/// itself with pitch and its volume setting; a soldier plays it the same way as any other bystander's instrument (Thorium's "others' instruments" volume).
		/// </summary>
		public static SoundStyle? UseSound(Item item)
		{
			if (item?.ModItem is not BardItem bi || !bi.PlayedSound.HasValue) return null;
			SoundStyle sound = bi.PlayedSound.Value;
			try { sound = sound.WithVolumeScale(ThoriumConfigClient.Instance.OthersInstrumentSoundVolume); } catch (System.Exception) { }
			return sound;
		}

		/// <summary>SoldierNPC.ExtraBuffStats: the soldier's live bard empowerments, laid on his dummy.</summary>
		public static int ExtraBuffStats(SoldierNPC s, Player d) => EmpowerCompat.ApplyStats(s, d);
	}
}
