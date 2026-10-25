using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using ThoriumMod;
using ThoriumMod.Items;
using Levy.Common;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>What a Thorium healing-class item does for a soldier who holds it.</summary>
	public enum HealerRole
	{
		/// <summary>Not a healer item.</summary>
		None,
		/// <summary>Radiant damage (or lifesteal, whose life return is incidental): fired at enemies like any ranged / magic weapon.</summary>
		Damage,
		/// <summary>Heals allies AND deals damage: fights enemies, and heals a badly wounded ally in reach between shots.</summary>
		Hybrid,
		/// <summary>Pure ally healer: lobs a heal at the most wounded ally in reach and is never fired at an enemy.</summary>
		AllyHeal,
		/// <summary>Heals only its wielder: used on the soldier himself when he is wounded, never fired at anyone.</summary>
		SelfHeal,
		/// <summary>Buff / shield / cleanse tool with no damage and no heal a soldier could use: not issued as a weapon.</summary>
		Support,
	}

	/// <summary>
	/// Thorium Healer on a Levy soldier. Thorium's own heal projectile only ever heals Players (and only on the owner's client), so a soldier cannot use it on a squadmate. Instead
	/// a healer soldier runs a basic healer AI from <see cref="SoldierThoriumGlobalNPC"/>, picked by the weapon's <see cref="HealerRole"/>:
	/// <list type="bullet">
	/// <item>Damage (radiant, lifesteal): no healer AI; the weapon fires at enemies through Levy's normal pipeline.</item>
	/// <item>AllyHeal / Hybrid: every <c>useTime</c> ticks it picks the most wounded ally in the weapon's reach and lobs a <see cref="SoldierHeal"/> at it. Allies are uniform: the commander
	/// (the player) and every squadmate. A pure healer never fires at an enemy; a hybrid fights and heals allies only when one is below <see cref="HybridHealBelow"/> of its health.</item>
	/// <item>SelfHeal, and AllyAndPlayer weapons when no ally needs them: restores the soldier himself when he is wounded. A plain Ally weapon never heals its wielder, as in Thorium.</item>
	/// </list>
	/// Every decision respects the weapon's own reach (its profile's RangeTiles) and mana cost.
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class HealerCompat
	{
		/// <summary>Heal delivered per cast as a multiple of the weapon's healAmount. 1 = exactly what the weapon says.</summary>
		public const float HealPotency = 3f;
		/// <summary>Ticks an ally is left alone after a heal is sent at it, so two healers (or one slow lob) do not overheal the same ally.</summary>
		private const int ReserveTicks = 40;
		/// <summary>A hybrid (damage + heal) weapon only stops fighting to heal an ally below this health fraction.</summary>
		public const float HybridHealBelow = 0.75f;
		/// <summary>A soldier uses a self-heal when he is below this health fraction (a hybrid below <see cref="HybridSelfHealBelow"/>).</summary>
		public const float SelfHealBelow = 0.85f, HybridSelfHealBelow = 0.6f;

		/// <summary>Main.GameUpdateCount until which a healer leaves each player alone (a heal is already flying at them).</summary>
		private static readonly uint[] PlayerReservedUntil = new uint[Main.maxPlayers + 1];

		/// <summary>Self-heal tools that do not set <c>HealType.Player</c>. Name -> how the heal is sized (mana converted 1:1).</summary>
		private static bool IsNamedSelfHeal(Item item) => item.ModItem != null && item.ModItem.Name == "Recuperate";

		/// <summary>Classifies a healing item by what a soldier can do with it.</summary>
		public static HealerRole RoleOf(Item item)
		{
			if (item == null || item.IsAir) return HealerRole.None;
			if (item.ModItem is not ThoriumItem ti) return HealerRole.None;
			bool healerClass = ti.isHealer || item.DamageType is HealerDamage || item.DamageType is HealerTool;
			if (!healerClass) return HealerRole.None;
			if (ti.healType == HealType.Player || IsNamedSelfHeal(item)) return HealerRole.SelfHeal;
			bool dmg = item.damage > 0;
			switch (ti.healType)
			{
				case HealType.Ally:
				case HealType.AllyAndPlayer: return dmg ? HealerRole.Hybrid : HealerRole.AllyHeal;
				case HealType.LifeSteal: return HealerRole.Damage;
				default: return dmg ? HealerRole.Damage : HealerRole.Support;
			}
		}

		/// <summary>True for a weapon of a Thorium healing class a soldier can use: radiant damage, lifesteal, hybrid, ally-heal or self-heal. Support tools are not issued.</summary>
		public static bool IsHealerClassWeapon(Item item)
		{
			if (item == null || item.IsAir) return false;
			HealerRole r = RoleOf(item);
			if (r != HealerRole.None) return r != HealerRole.Support && item.useStyle != Terraria.ID.ItemUseStyleID.None;
			return item.DamageType is HealerDamage || item.DamageType is HealerTool;
		}

		/// <summary>True for a healer weapon the healer AI drives (AllyHeal, Hybrid, SelfHeal).</summary>
		public static bool IsHealWeapon(Item item)
		{
			HealerRole r = RoleOf(item);
			return r == HealerRole.AllyHeal || r == HealerRole.Hybrid || r == HealerRole.SelfHeal;
		}

		/// <summary>True when Levy's brain must NOT fire this item at an enemy (it is a heal, self-heal or support tool).</summary>
		public static bool VetoEnemyFire(Item item)
		{
			HealerRole r = RoleOf(item);
			return r == HealerRole.AllyHeal || r == HealerRole.SelfHeal || r == HealerRole.Support;
		}

		/// <summary>The weapon's real reach in pixels (its Levy profile's RangeTiles, clamped to a sane healing range).</summary>
		private static float ReachPx(SoldierNPC s, Item item)
		{
			WeaponProfile prof = s.Weapon != null && s.Weapon.ItemType == item.type ? s.Weapon : WeaponClassifier.GetProfile(item);
			return Math.Clamp(prof?.RangeTiles ?? 20, 8, 40) * LevyConstants.TilePx;
		}

		/// <summary>Per tick (server / single player), for a soldier holding a heal weapon.</summary>
		public static void Tick(SoldierNPC s, Item item, SoldierThoriumGlobalNPC state)
		{
			if (state.HealCooldown > 0) { state.HealCooldown--; return; }
			if (s.IsRouted || item.ModItem is not ThoriumItem ti) return;
			HealerRole role = RoleOf(item);
			if (role != HealerRole.AllyHeal && role != HealerRole.Hybrid && role != HealerRole.SelfHeal) return;
			int manaCost = Math.Max(0, item.mana);
			if (s.Record.Mana < manaCost) return;
			if (role == HealerRole.Hybrid && s.AttackCooldown > 0) return; // one action per use: a hybrid heals between its shots

			float rangePx = ReachPx(s, item);
			int amount = Math.Max(1, (int)Math.Round(Math.Max(1, ti.healAmount) * HealPotency));

			// 1) allies: the commander (player) and every squadmate in reach, most wounded first
			if (role == HealerRole.AllyHeal || role == HealerRole.Hybrid)
			{
				float below = role == HealerRole.Hybrid ? HybridHealBelow : 1.01f;
				if (TryHealAlly(s, item, state, rangePx, below, amount, manaCost)) return;
			}

			// 2) himself: a SelfHeal weapon, or an AllyAndPlayer weapon with no ally to serve (a plain Ally weapon never heals its wielder)
			bool selfOk = role == HealerRole.SelfHeal || ti.healType == HealType.AllyAndPlayer;
			if (!selfOk) return;
			float selfBelow = role == HealerRole.Hybrid ? HybridSelfHealBelow : SelfHealBelow;
			if (s.NPC.life >= s.NPC.lifeMax * selfBelow) return;
			int self = role == HealerRole.SelfHeal && IsNamedSelfHeal(item) ? Math.Max(1, item.mana) : amount; // Recuperate turns mana into life 1:1
			HealSelf(s, item, state, self, manaCost);
		}

		private static bool TryHealAlly(SoldierNPC s, Item item, SoldierThoriumGlobalNPC state, float rangePx, float below, int amount, int manaCost)
		{
			SoldierNPC allyNpc = null; Player allyPlayer = null;
			if (!MostWoundedAlly(s, rangePx, below, out allyNpc, out allyPlayer)) return false;

			Vector2 muzzle = WeaponUse.Muzzle(s);
			Vector2 aim = allyPlayer != null ? allyPlayer.Center : allyNpc.NPC.Center;
			Vector2 to = aim - muzzle;
			float speed = item.shootSpeed > 0f ? Math.Max(6f, item.shootSpeed) : 8f;
			Vector2 vel = to.LengthSquared() > 1f ? Vector2.Normalize(to) * speed : Vector2.UnitX * speed;
			float target = allyPlayer != null ? SoldierHeal.PlayerTarget(allyPlayer.whoAmI) : allyNpc.NPC.whoAmI;
			Projectile p = WeaponUse.Spawn(s, muzzle, vel, ModContent.ProjectileType<SoldierHeal>(), 0, 0f, null, target, amount);
			if (p == null) return false;

			s.Record.Mana -= manaCost;
			state.HealCooldown = Math.Max(1, item.useTime);
			if (item.damage > 0) s.AttackCooldown = Math.Max(s.AttackCooldown, Math.Max(1, item.useTime));
			if (allyPlayer != null) PlayerReservedUntil[allyPlayer.whoAmI] = Main.GameUpdateCount + ReserveTicks;
			else if (allyNpc.NPC.TryGetGlobalNPC(out SoldierThoriumGlobalNPC allyState)) allyState.HealReservedUntil = Main.GameUpdateCount + ReserveTicks;
			ShowCast(s, item, vel);
			return true;
		}

		private static void HealSelf(SoldierNPC s, Item item, SoldierThoriumGlobalNPC state, int amount, int manaCost)
		{
			NPC n = s.NPC;
			int before = n.life;
			n.life = Math.Min(n.lifeMax, n.life + amount);
			int gained = n.life - before;
			if (gained <= 0) return;
			s.Record.CurrentHp = n.life;
			s.Record.Mana -= manaCost;
			n.HealEffect(gained, true);
			state.HealCooldown = Math.Max(1, item.useTime);
			if (item.damage > 0) s.AttackCooldown = Math.Max(s.AttackCooldown, Math.Max(1, item.useTime));
			ShowCast(s, item, Vector2.UnitX * n.direction);
			if (Main.netMode != Terraria.ID.NetmodeID.Server)
			{
				for (int i = 0; i < 8; i++)
				{
					Dust d = Dust.NewDustPerfect(n.Center, Terraria.ID.DustID.GoldFlame, Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 1.2f);
					d.noGravity = true;
				}
			}
		}

		/// <summary>Show the cast: face the target, raise the weapon, play its sound (only when the soldier is not already mid-swing at an enemy).</summary>
		private static void ShowCast(SoldierNPC s, Item item, Vector2 dir)
		{
			if (s.ItemAnimation <= 0) { s.ItemAnimation = s.ItemAnimationMax = Math.Max(1, item.useAnimation); s.UsesThisAnimation = 0; }
			s.AimRotation = dir.ToRotation();
			if (Math.Abs(dir.X) > 0.01f) s.NPC.direction = dir.X < 0f ? -1 : 1;
			if (item.UseSound.HasValue) SoundEngine.PlaySound(item.UseSound.Value, s.NPC.Center);
			s.NPC.netUpdate = true;
		}

		/// <summary>
		/// The most wounded ally in reach with a clear shot, nearest as the tiebreak. Allies are the commander (the player) and every other soldier of that commander;
		/// anyone not below <paramref name="below"/> of their health, already being healed, out of reach or behind cover is skipped. The caster is never his own target.
		/// </summary>
		private static bool MostWoundedAlly(SoldierNPC s, float rangePx, float below, out SoldierNPC bestNpc, out Player bestPlayer)
		{
			bestNpc = null; bestPlayer = null;
			float bestScore = float.MaxValue;
			Vector2 from = s.NPC.Center;
			uint now = Main.GameUpdateCount;

			Player cmd = CompatAllies.CommanderOf(s);
			if (cmd != null && cmd.statLife < cmd.statLifeMax2 && PlayerReservedUntil[cmd.whoAmI] <= now)
			{
				float frac = (float)cmd.statLife / Math.Max(1, cmd.statLifeMax2);
				float dist = Vector2.Distance(from, cmd.Center);
				if (frac < below && dist <= rangePx && Collision.CanHitLine(from, 1, 1, cmd.Center, 1, 1))
				{
					bestScore = frac + 0.25f * (dist / rangePx);
					bestPlayer = cmd;
				}
			}

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.whoAmI == s.NPC.whoAmI || n.life >= n.lifeMax || n.life <= 0) continue;
				if (n.ModNPC is not SoldierNPC a || a.Record == null || !a.IsAlive) continue;
				if (!CompatAllies.SameCommander(s, a)) continue;
				if (n.TryGetGlobalNPC(out SoldierThoriumGlobalNPC g) && g.HealReservedUntil > now) continue;
				float frac = (float)n.life / Math.Max(1, n.lifeMax);
				if (frac >= below) continue;
				float dist = Vector2.Distance(from, n.Center);
				if (dist > rangePx) continue;
				float score = frac + 0.25f * (dist / rangePx);
				if (score >= bestScore) continue;
				if (!Collision.CanHitLine(from, 1, 1, n.Center, 1, 1)) continue;
				bestScore = score; bestNpc = a; bestPlayer = null;
			}
			return bestNpc != null || bestPlayer != null;
		}
	}
}
