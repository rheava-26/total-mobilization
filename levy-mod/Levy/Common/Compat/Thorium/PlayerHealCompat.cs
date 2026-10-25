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

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// A PLAYER's Thorium heal reaching the player's own soldiers and officer avatars. Thorium's heal projectiles only look for Players, so a wounded soldier standing under one is ignored.
	/// <see cref="PlayerCastGlobalProjectile"/> hands every tick of a player-owned Thorium ally-heal projectile here, and a soldier or officer avatar of that player who is wounded and
	/// under the projectile (within Thorium's own 30 px heal radius, measured to the body) is healed. Allies are uniform: it is exactly what the projectile does for a player standing there.
	/// <list type="bullet">
	/// <item>Which projectiles: the ones whose Thorium code calls <c>ProjectileHelper.ThoriumHeal</c> or <c>CanBeHealed</c>, found by reading the methods of the projectile's class chain once
	/// (so every heal projectile in Thorium, and a mod that adds more through the same helper, counts without a list to keep).</item>
	/// <item>How much: the heal amount of the ally-heal item that fired it (read off the item-use source, else the parent projectile, else the item in hand) plus the caster's heal bonus,
	/// times <see cref="HealerCompat.HealPotency"/> - the same amount a soldier healer's bolt delivers. Only <c>Ally</c> / <c>AllyAndPlayer</c> items count; lifesteal and self-heals stay the player's own.</item>
	/// <item>One heal per target per <see cref="CompanionRequest.ReserveTicks"/> ticks (shared with the soldier healers), and each soldier healed spends one of the projectile's hits, as a player would.</item>
	/// <item>Multiplayer: the owning client picks the targets and asks the server (<see cref="CompanionRequest"/>); the server re-validates and applies.</item>
	/// </list>
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class PlayerHealCompat
	{
		/// <summary>Thorium's default heal radius around a heal projectile (a few projectiles use 20-40; 30 is the common one).</summary>
		public const float HealRadius = 30f;

		private static readonly Dictionary<Type, bool> TypeCache = new();
		private static List<int> _healTokens;
		private static readonly List<(float dist, NPC npc)> Found = new();
		private static readonly List<(int npc, int amount)> Pending = new();

		// ---- which projectiles heal allies ----

		/// <summary>Metadata tokens of Thorium's heal helpers; a projectile class that calls any of them is a heal projectile.</summary>
		private static List<int> HealTokens()
		{
			if (_healTokens != null) return _healTokens;
			var list = new List<int>();
			try
			{
				Type helper = typeof(ThoriumPlayer).Assembly.GetType("ThoriumMod.Utilities.ProjectileHelper");
				if (helper != null)
					foreach (MethodInfo m in helper.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
						if (m.Name == "ThoriumHeal" || m.Name == "CanBeHealed") list.Add(m.MetadataToken);
			}
			catch (Exception) { }
			_healTokens = list;
			return list;
		}

		/// <summary>True when this projectile's Thorium class (or a base class) calls the ally-heal helpers.</summary>
		public static bool IsHealProjectile(Projectile p)
		{
			Type t = p?.ModProjectile?.GetType();
			if (t == null) return false;
			if (TypeCache.TryGetValue(t, out bool known)) return known;
			bool heals = false;
			try
			{
				List<int> tokens = HealTokens();
				for (Type ty = t; !heals && Walkable(ty); ty = ty.BaseType)
					foreach (MethodInfo m in ty.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
						if (CallsAny(m, tokens)) { heals = true; break; }
			}
			catch (Exception) { }
			TypeCache[t] = heals;
			return heals;

			static bool Walkable(Type ty) => ty != null && ty != typeof(ModProjectile) && ty != typeof(object);
		}

		private static bool CallsAny(MethodInfo m, List<int> tokens)
		{
			if (tokens.Count == 0 || m.IsAbstract) return false;
			byte[] il;
			try { il = m.GetMethodBody()?.GetILAsByteArray(); } catch (Exception) { return false; }
			if (il == null) return false;
			for (int i = 0; i + 4 < il.Length; i++)
			{
				if (il[i] != 0x28 && il[i] != 0x6F) continue; // call / callvirt
				int tok = BitConverter.ToInt32(il, i + 1);
				for (int k = 0; k < tokens.Count; k++) if (tokens[k] == tok) return true;
			}
			return false;
		}

		// ---- how much ----

		/// <summary>The ally-heal amount of an item (0 when it is not an Ally / AllyAndPlayer heal item).</summary>
		private static int AllyHealOf(Item item) =>
			item != null && !item.IsAir && item.ModItem is ThoriumItem ti && (ti.healType == HealType.Ally || ti.healType == HealType.AllyAndPlayer) ? Math.Max(1, ti.healAmount) : 0;

		/// <summary>The heal amount a freshly spawned projectile carries: its item-use source, else its parent projectile, else the caster's item in hand (0 = none).</summary>
		public static int AmountFromSource(Projectile p, IEntitySource source)
		{
			int a = 0;
			if (source is EntitySource_ItemUse use) a = AllyHealOf(use.Item);
			else if (source is EntitySource_Parent parent && parent.Entity is Projectile pp && pp.TryGetGlobalProjectile(out PlayerCastGlobalProjectile g) && g.BaseHeal > 0) a = g.BaseHeal;
			if (a <= 0 && p.owner >= 0 && p.owner < Main.maxPlayers) a = AllyHealOf(Main.player[p.owner].HeldItem);
			return a > 0 ? a : -1;
		}

		// ---- the heal ----

		/// <summary>Per tick, on the owning client, for a player-owned projectile that came from an ally-heal item.</summary>
		public static void Tick(Projectile p, PlayerCastGlobalProjectile g)
		{
			if (p.penetrate == 0 || !IsHealProjectile(p)) return;
			Player owner = Main.player[p.owner];
			if (owner == null || !owner.active || owner.dead || owner != Main.LocalPlayer) return;

			// the soldiers under it
			uint now = Main.GameUpdateCount;
			Found.Clear();
			Vector2 c = p.Center;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.life <= 0 || n.life >= n.lifeMax || n.ModNPC is not SoldierNPC s || s.Record == null || !s.IsAlive || !CompanionRequest.Serves(s, owner)) continue;
				if (n.TryGetGlobalNPC(out SoldierThoriumGlobalNPC st) && st.HealReservedUntil > now) continue;
				Rectangle hb = n.Hitbox;
				float dx = Math.Max(Math.Max(hb.Left - c.X, 0f), c.X - hb.Right), dy = Math.Max(Math.Max(hb.Top - c.Y, 0f), c.Y - hb.Bottom);
				float d2 = dx * dx + dy * dy;
				if (d2 <= HealRadius * HealRadius) Found.Add((d2, n));
			}
			if (Found.Count == 0) return;
			Found.Sort((a, b) => a.dist.CompareTo(b.dist));

			int amount = Math.Max(1, (int)Math.Round((g.BaseHeal + Math.Max(0, owner.GetModPlayer<ThoriumPlayer>().healBonus)) * HealerCompat.HealPotency));
			Pending.Clear();
			foreach (var (_, n) in Found)
			{
				if (Main.netMode == NetmodeID.MultiplayerClient)
				{
					Pending.Add((n.whoAmI, amount));
					if (n.TryGetGlobalNPC(out SoldierThoriumGlobalNPC st)) st.HealReservedUntil = now + CompanionRequest.ReserveTicks; // do not ask again while the heal is in flight
				}
				else if (CompanionRequest.HealSoldier(n, amount) <= 0) continue;
				if (!Main.dedServ) Sparkle(n);
				if (p.penetrate > 0 && --p.penetrate == 0) { p.Kill(); break; } // each soldier healed spends one of the projectile's hits, as a player would
			}
			if (Pending.Count > 0) CompanionRequest.SendHeal(Pending);
		}

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
