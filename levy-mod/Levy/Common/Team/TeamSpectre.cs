using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Thorium;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Team
{
	/// <summary>
	/// The Spectre Hood (and Spectre Mask healing set) heal goes to the MOST WOUNDED ally in 3000 px (Projectile.ghostHeal: the player on the team with the most life missing, the wearer by default). A commander's levies
	/// are allies, so a hook on ghostHeal runs the same pick over the commander's levies too: when a levy has lost MORE life than every player the vanilla pick would heal, the bolt goes to that levy
	/// (<see cref="SoldierHeal"/>, the same homing courier a soldier healer fires; it delivers on the server) and the lifesteal pool is charged exactly as the pick it replaces would have charged it.
	/// Vanilla spends <c>heal</c> from the pool before it picks; Calamity's rebalance (AdjustSpectreHealing) rounds the amount, caps it at the missing life and charges 66% of it, so that variant is mirrored when Calamity is loaded.
	/// When no levy is more wounded than the player pick, the original runs untouched. The other direction (a levy in the Spectre set) is SoldierNPC.SpectreHealOnHit.
	/// </summary>
	public static class TeamSpectre
	{
		public static bool Installed { get; private set; }
		/// <summary>Self-check counter: how many bolts this hook sent to a levy.</summary>
		public static int RedirectedBolts;
		/// <summary>Self-check counters: bolts a levy in the Spectre set sent to an ally, and bolts that landed.</summary>
		public static int LevyBolts, LevyDelivered;

		public static void Install()
		{
			if (Installed) return;
			On_Projectile.ghostHeal += Hook;
			Installed = true;
		}

		public static void Uninstall()
		{
			if (!Installed) return;
			On_Projectile.ghostHeal -= Hook;
			Installed = false;
		}

		private static void Hook(On_Projectile.orig_ghostHeal orig, Projectile self, int dmg, Vector2 position, Entity victim)
		{
			try { if (TryLevy(self, dmg, position)) return; }
			catch (Exception e) { TeamItems.WarnOnce("spectre heal hook", e); }
			orig(self, dmg, position, victim);
		}

		/// <summary>The most wounded levy of the projectile's owner within reach, and how much life he is missing. Null when none.</summary>
		public static SoldierNPC MostWoundedLevy(Player owner, Vector2 from, float range, out int missing)
		{
			missing = 0; SoldierNPC best = null;
			foreach (SoldierNPC s in TeamItems.Levies(owner))
			{
				if (Vector2.Distance(from, s.NPC.Center) > range) continue;
				int m = s.NPC.lifeMax - s.NPC.life;
				if (m > missing) { missing = m; best = s; }
			}
			return best;
		}

		private static bool TryLevy(Projectile self, int dmg, Vector2 position)
		{
			bool _calamity = ModLoader.HasMod("CalamityMod");
			if (self.owner < 0 || self.owner >= Main.maxPlayers || !TeamItems.IsLocal(Main.player[self.owner])) return false;
			Player owner = Main.player[self.owner];
			float mult = 0.2f - self.numHits * 0.05f;
			if (mult <= 0f) return false;
			float amount = _calamity ? (int)Math.Round(dmg * mult) : dmg * mult;
			if ((int)amount <= 0 || owner.lifeSteal <= 0f || !self.DamageType.CountsAsClass(DamageClass.Magic)) return false;

			// Vanilla's own pick among the players: the one missing the most life (the owner by default), team rules and reach as in ghostHeal.
			float playerMissing = 0f;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (!p.active || p.dead || !((!owner.hostile && !p.hostile) || owner.team == p.team) || self.Distance(p.Center) > LevyConstants.TeamSpectreRangePx) continue;
				float m = p.statLifeMax2 - p.statLife;
				if (m > playerMissing) playerMissing = m;
			}
			SoldierNPC levy = MostWoundedLevy(owner, self.Center, LevyConstants.TeamSpectreRangePx, out int levyMissing);
			if (levy == null || levyMissing <= playerMissing) return false; // a player is as wounded or more: the vanilla pick stands

			int heal = _calamity ? Math.Min((int)amount, levyMissing) : (int)amount;
			if (heal <= 0) return false;
			owner.lifeSteal -= _calamity ? heal * LevyConstants.TeamSpectreCalamityCost : amount;
			RedirectedBolts++;
			// The bolt: the same homing courier a levy healer fires. It heals on the server / single player machine, so the owning client only spawns it.
			int proj = Projectile.NewProjectile(self.GetSource_FromThis(), position, Vector2.Zero, ModContent.ProjectileType<SoldierHeal>(), 0, 0f, self.owner, levy.NPC.whoAmI, heal);
			if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].velocity = (levy.NPC.Center - position).SafeNormalize(Vector2.UnitY) * 7f;
			return true;
		}
	}
}
