using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Net;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// The Thorium-touching half of <see cref="SoldierThoriumGlobalNPC"/>: an instrument-holder gets its Inspiration pool sized and refilled
	/// (<see cref="BardCompat.Tick"/>) and its empowerments tracked (<see cref="EmpowerCompat.Tick"/>); a heal-weapon holder runs the healer AI (<see cref="HealerCompat.Tick"/>).
	/// Reaches Levy only through its public surface (<c>SoldierNPC.Dummy</c>, <c>Record</c>, <c>WeaponUse</c>, the generic hooks). JIT'd only when ThoriumMod is enabled; only ever called behind the Active check.
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class ThoriumDriver
	{
		public static void Drive(SoldierNPC s, SoldierThoriumGlobalNPC state)
		{
			var gear = s.Record.Gear;
			if (gear == null) return;
			if (BardCompat.IsBardWeapon(gear.Weapon) || BardCompat.IsBardWeapon(gear.Sidearm)) BardCompat.Tick(s, state);
			else if (state.InspMax != 0) { state.InspMax = 0; state.InspCur = 0; s.NPC.netUpdate = true; } // no longer carries an instrument: the bar goes

			EmpowerCompat.Tick(s, state);
			// Attack-speed empowerments: the weapon's cooldown runs down faster by the bonus (extra ticks, spread by chance so +9% is +9% on average).
			if (state.EmpAttackSpeed > 0.001f && s.AttackCooldown > 1 && Main.rand.NextFloat() < state.EmpAttackSpeed / (1f + state.EmpAttackSpeed)) s.AttackCooldown--;

			Item held = WeaponUse.HeldItem(s);
			if (HealerCompat.IsHealWeapon(held)) HealerCompat.Tick(s, held, state);
		}

		/// <summary>Client draw of the Inspiration bar and empowerment markers.</summary>
		public static void Draw(NPC npc, SoldierThoriumGlobalNPC state, SpriteBatch spriteBatch, Vector2 screenPos) => CompatDraw.Draw(npc, state, spriteBatch, screenPos);

		/// <summary>Register the generic Levy hooks (Bard / Healer weapons issuable; per-shot Inspiration gate and heal-weapon veto; empowerment stat pass; effect packets).</summary>
		public static void Register()
		{
			WeaponClassifier.ExtraCombatClass = CompatHooks.ExtraCombatClass;
			WeaponClassifier.AiDrivenHealer = HealerCompat.IsHealWeapon; // heal-driven weapons classify as standoff casters past the consumable / useStyle gates
			WeaponClassifier.HealerScore = HealerCompat.HealingScore;    // and rank by healing value, not the -1 damage Thorium stamps on them
			WeaponUse.PreFireGate = CompatHooks.PreFireGate;
			WeaponUse.UseSoundFallback = CompatHooks.UseSound;
			SoldierNPC.ExtraBuffStats = CompatHooks.ExtraBuffStats;
			LevyPackets.Register(LevyPacket.CompanionEffect, OnCompanionPacket);
			LevyPackets.Register(LevyPacket.CompanionRequest, OnCompanionRequest);
			// The PLAYER's own instrument also empowering his soldiers: a detour on Thorium's per-note empower. Optional: if Thorium's internals differ the soldiers' side still works.
			try { PlayerBardCompat.Install(); }
			catch (System.Exception e) { Levy.Instance.Logger.Warn("Levy/Thorium compat: could not hook the player's instrument, so the player's own tunes will not empower his soldiers: " + e.Message); }
		}

		/// <summary>Server side of <see cref="LevyPacket.CompanionRequest"/>: a heal or an empowerment the commander's own Thorium cast asks the server to lay on his soldiers.</summary>
		private static void OnCompanionRequest(BinaryReader reader, int fromWhoAmI)
		{
			byte kind = reader.ReadByte();
			if (kind == CompanionRequest.KindHeal) CompanionRequest.OnHeal(reader, fromWhoAmI);
			else if (kind == CompanionRequest.KindEmpower) EmpowerCompat.OnRequest(reader, fromWhoAmI);
		}

		/// <summary>Clear the hooks (Levy is unloading; leave nothing pointing at a dead delegate).</summary>
		public static void Unregister()
		{
			WeaponClassifier.ExtraCombatClass = null;
			WeaponClassifier.AiDrivenHealer = null;
			WeaponClassifier.HealerScore = null;
			WeaponUse.PreFireGate = null;
			WeaponUse.UseSoundFallback = null;
			SoldierNPC.ExtraBuffStats = null;
		}

		/// <summary>Client side of <see cref="LevyPacket.CompanionEffect"/>: a heal or an empowerment the server laid on the local player.</summary>
		private static void OnCompanionPacket(BinaryReader reader, int fromWhoAmI)
		{
			byte kind = reader.ReadByte();
			if (kind == CompanionFx.KindHeal) CompanionFx.OnHealPacket(reader);
			else if (kind == CompanionFx.KindEmpower) EmpowerCompat.OnPacket(reader);
		}
	}
}
