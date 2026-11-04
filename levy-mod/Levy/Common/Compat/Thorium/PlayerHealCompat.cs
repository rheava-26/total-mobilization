using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using ThoriumMod;
using ThoriumMod.Items;
using Levy.Content.NPCs;
using Levy.Common.Weapons;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// A PLAYER's Thorium "heal allies" reaching the player's own soldiers and officer avatars. Thorium only ever looks for Players, so a wounded soldier is invisible to it. The heal itself
	/// is delivered by <see cref="AllyHealHook"/>, which hooks Thorium's own shared routines (<c>ProjectileHelper.ThoriumHeal</c> and <c>CanBeHealed</c>) and hands the heal here:
	/// <see cref="HealAround"/> heals every wounded soldier or officer avatar of that player within the heal's radius of its anchor, by the same amount Thorium works out.
	/// <list type="bullet">
	/// <item>How much: Thorium's own heal amount (plus the caster's heal bonus, and the weapon's custom rule run for the soldier), times <see cref="HealerCompat.HealPotency"/> - the same amount a soldier healer's bolt delivers.
	/// For an on-hit sweep (which has no amount of its own to read) it is the heal amount of the ally-heal item that dealt the hit (<see cref="AmountFromSource"/>).</item>
	/// <item>One heal per target per <see cref="CompanionRequest.ReserveTicks"/> ticks (shared with the soldier healers), and for a projectile heal each soldier healed spends one of its hits, as a player would.</item>
	/// <item>Multiplayer: the owning client picks the targets and asks the server (<see cref="CompanionRequest"/>); the server re-validates and applies.</item>
	/// </list>
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class PlayerHealCompat
	{
		/// <summary>Thorium's default heal radius around a heal projectile (a few projectiles use 20-40; 30 is the common one).</summary>
		public const float HealRadius = 30f;

		private static readonly List<(float dist, NPC npc)> Found = new();
		private static readonly List<(int npc, int amount)> Pending = new();

		// ---- how much ----

		/// <summary>The ally-heal amount of an item (0 when it is not an Ally / AllyAndPlayer heal item).</summary>
		public static int AllyHealOf(Item item) =>
			item != null && !item.IsAir && item.ModItem is ThoriumItem ti && (ti.healType == HealType.Ally || ti.healType == HealType.AllyAndPlayer) ? Math.Max(1, ti.healAmount) : 0;

		/// <summary>The heal amount a freshly spawned projectile carries: its item-use source, else its parent projectile, else the caster's item in hand (0 = none).</summary>
		public static int AmountFromSource(Projectile p, IEntitySource source)
		{
			int a = 0;
			if (source is EntitySource_Parent sp && sp.Entity is NPC sn && sn.ModNPC is SoldierNPC sold) return Positive(AllyHealOf(WeaponUse.HeldItem(sold))); // a soldier's shot: the weapon IN HIS HAND, not the commander's
			if (source is EntitySource_ItemUse use) a = AllyHealOf(use.Item);
			else if (source is EntitySource_Parent parent && parent.Entity is Projectile pp && pp.TryGetGlobalProjectile(out PlayerCastGlobalProjectile g) && g.BaseHeal > 0) a = g.BaseHeal;
			if (a <= 0 && p.owner >= 0 && p.owner < Main.maxPlayers) a = AllyHealOf(Main.player[p.owner].HeldItem);
			return Positive(a);
		}

		private static int Positive(int a) => a > 0 ? a : -1;

		// ---- the heal ----

		/// <summary>
		/// Heal the owner's wounded soldiers and officer avatars within <paramref name="radius"/> of <paramref name="anchor"/> (distance measured to the body, as Thorium measures to a player's body).
		/// <paramref name="amountFor"/> gives the life each soldier gets (null or 0 = skip). When <paramref name="hits"/> is a projectile, every soldier healed spends one of its hits and it dies when they run out.
		/// Owning client only (single player / listen host applies directly, a multiplayer client asks the server). Returns how many soldiers were healed or asked for.
		/// </summary>
		public static int HealAround(Player owner, Vector2 anchor, float radius, Func<NPC, int> amountFor, Projectile hits)
		{
			if (owner == null || !owner.active || owner.dead || radius <= 0f) return 0;
			uint now = Main.GameUpdateCount;
			Found.Clear();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.life <= 0 || n.life >= n.lifeMax || n.ModNPC is not SoldierNPC s || s.Record == null || !s.IsAlive || !CompanionRequest.Serves(s, owner)) continue;
				if (n.TryGetGlobalNPC(out SoldierThoriumGlobalNPC st) && st.HealReservedUntil > now) continue;
				Rectangle hb = n.Hitbox;
				float dx = Math.Max(Math.Max(hb.Left - anchor.X, 0f), anchor.X - hb.Right), dy = Math.Max(Math.Max(hb.Top - anchor.Y, 0f), anchor.Y - hb.Bottom);
				float d2 = dx * dx + dy * dy;
				if (d2 <= radius * radius) Found.Add((d2, n));
			}
			if (Found.Count == 0) return 0;
			Found.Sort((a, b) => a.dist.CompareTo(b.dist));

			int done = 0;
			Pending.Clear();
			foreach (var (_, n) in Found)
			{
				int amount = amountFor?.Invoke(n) ?? 0;
				if (amount <= 0) continue;
				if (Main.netMode == NetmodeID.MultiplayerClient)
				{
					Pending.Add((n.whoAmI, amount));
					if (n.TryGetGlobalNPC(out SoldierThoriumGlobalNPC st)) st.HealReservedUntil = now + CompanionRequest.ReserveTicks; // do not ask again while the heal is in flight
				}
				else if (CompanionRequest.HealSoldier(n, amount) <= 0) continue;
				done++;
				if (!Main.dedServ) Sparkle(n);
				if (hits != null && hits.penetrate > 0 && --hits.penetrate == 0) { hits.Kill(); break; } // each soldier healed spends one of the projectile's hits, as a player would
			}
			if (Pending.Count > 0) CompanionRequest.SendHeal(Pending);
			return done;
		}

		/// <summary>The life a heal of <paramref name="baseHeal"/> (Thorium's amount, heal bonus already included) gives a soldier: Thorium's number times the soldier healers' potency.</summary>
		public static int ScaleForSoldier(int baseHeal) => baseHeal <= 0 ? 0 : Math.Max(1, (int)Math.Round(baseHeal * HealerCompat.HealPotency));

		private static void Sparkle(NPC n)
		{
			for (int i = 0; i < 6; i++)
			{
				Dust d = Dust.NewDustPerfect(n.Center, DustID.GoldFlame, Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 1.2f);
				d.noGravity = true;
			}
		}
	}
}
