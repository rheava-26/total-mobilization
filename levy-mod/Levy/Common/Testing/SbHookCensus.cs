using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Levy.Common.Owner;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Lab only (loads only with LEVY_HARNESS=1): counts every player hook a ModPlayer is asked to run, split into "the hook ran for a levy's dummy" and "the hook ran for any other player", and for the dummy how often
	/// <c>Player.whoAmI == Main.myPlayer</c> held at that moment (the gate most mods put in front of a projectile spawn or a heal). The set-bonus audit (CombatHarness.SetBonus.Observe.cs) prints the table per set:
	/// a hook a set's text depends on that shows 0 for the dummy never runs for a levy; one that runs with the gate false never spawns what a player's would.
	/// </summary>
	public class SbHookCensus : ModPlayer
	{
		public override bool IsLoadingEnabled(Mod mod) => Environment.GetEnvironmentVariable("LEVY_HARNESS") == "1";

		/// <summary>hook -> [dummy calls, dummy calls with whoAmI == myPlayer, other-player calls]</summary>
		public static readonly Dictionary<string, int[]> Counts = new();
		public static void Clear() { Counts.Clear(); }

		private void Tick(string hook)
		{
			if (!CombatHarness.Enabled) return;
			bool dummy = SoldierOwner.IsDummy(Player);
			if (!Counts.TryGetValue(hook, out int[] c)) Counts[hook] = c = new int[3];
			if (dummy) { c[0]++; if (Player.whoAmI == Main.myPlayer) c[1]++; } else c[2]++;
		}

		public static string Table(bool dummyOnly = false)
		{
			var l = new List<string>();
			foreach (var kv in Counts.OrderBy(k => k.Key, StringComparer.Ordinal))
			{
				int[] c = kv.Value;
				if (c[0] == 0 && c[2] == 0) continue;
				l.Add($"{kv.Key}={c[0]}/{c[1]}" + (c[2] > 0 && !dummyOnly ? $"+{c[2]}other" : ""));
			}
			return string.Join(" ", l);
		}

		public override void PreUpdate() => Tick("PreUpdate");
		public override void PostUpdate() => Tick("PostUpdate");
		public override void PreUpdateBuffs() => Tick("PreUpdateBuffs");
		public override void PostUpdateBuffs() => Tick("PostUpdateBuffs");
		public override void UpdateEquips() => Tick("UpdateEquips");
		public override void PostUpdateEquips() => Tick("PostUpdateEquips");
		public override void PostUpdateMiscEffects() => Tick("PostUpdateMiscEffects");
		public override void PostUpdateRunSpeeds() => Tick("PostUpdateRunSpeeds");
		public override void UpdateLifeRegen() => Tick("UpdateLifeRegen");
		public override void UpdateBadLifeRegen() => Tick("UpdateBadLifeRegen");
		public override void ProcessTriggers(TriggersSet triggersSet) => Tick("ProcessTriggers");
		public override void ArmorSetBonusActivated() => Tick("ArmorSetBonusActivated");
		public override void OnHitAnything(float x, float y, Entity victim) => Tick("OnHitAnything");
		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Tick("OnHitNPC");
		public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) => Tick("OnHitNPCWithItem");
		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) => Tick("OnHitNPCWithProj");
		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) => Tick("ModifyHitNPC");
		public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers) => Tick("ModifyHitNPCWithItem");
		public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers) => Tick("ModifyHitNPCWithProj");
		public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) => Tick("ModifyHitByNPC");
		public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) => Tick("ModifyHitByProjectile");
		public override void OnHitByNPC(NPC npc, Player.HurtInfo hurtInfo) => Tick("OnHitByNPC");
		public override void OnHitByProjectile(Projectile proj, Player.HurtInfo hurtInfo) => Tick("OnHitByProjectile");
		public override bool FreeDodge(Player.HurtInfo info) { Tick("FreeDodge"); return false; }
		public override bool ConsumableDodge(Player.HurtInfo info) { Tick("ConsumableDodge"); return false; }
		public override void ModifyHurt(ref Player.HurtModifiers modifiers) => Tick("ModifyHurt");
		public override void OnHurt(Player.HurtInfo info) => Tick("OnHurt");
		public override void PostHurt(Player.HurtInfo info) => Tick("PostHurt");
		public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genGore, ref Terraria.DataStructures.PlayerDeathReason damageSource) { Tick("PreKill"); return true; }
		public override void ModifyWeaponDamage(Item item, ref StatModifier damage) => Tick("ModifyWeaponDamage");
		public override void MeleeEffects(Item item, Microsoft.Xna.Framework.Rectangle hitbox) => Tick("MeleeEffects");
		public override bool Shoot(Item item, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Vector2 velocity, int type, int damage, float knockback) { Tick("Shoot"); return true; }
		public override void OnConsumeMana(Item item, int manaConsumed) => Tick("OnConsumeMana");
	}
}
