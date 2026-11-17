using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>
	/// A band's own shots never hurt a band. A hostile soldier's main shot is flagged hostile (Levy TagHostileShot) and so passes through other NPCs, but what that shot spawns (the Nettle Burst's
	/// segments, a rocket's fragments, a Seedler's seeds) inherits Levy's soldier tag only and stays a friendly projectile, and a friendly projectile hits every unfriendly NPC: the firing
	/// member and its band-mates. This tag marks everything a band member fires, children included, and the hit hooks refuse any band body as a target. It is the same rule a player's own projectiles follow
	/// (they never hit the player). Bands are enemies only of the player's side (HostileSide.IsEnemy), so no band shot has a reason to hit another band either.
	/// </summary>
	public class BandProjectileGuard : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		/// <summary>Fired by a band member, or spawned by something a band member fired.</summary>
		public bool FromBand;
		/// <summary>Lab and self-check: shots stopped from landing on a band body.</summary>
		public static long Refused;

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (source is EntitySource_Parent { Entity: NPC n } && n.ModNPC is BandLevyNPC) FromBand = true;
			else if (source is EntitySource_Parent { Entity: Projectile par } && par.TryGetGlobalProjectile(out BandProjectileGuard g) && g.FromBand) FromBand = true;
		}

		/// <summary>True when the projectile was fired by (or descends from a shot of) a band member. Reads the tag lazily, because Levy tags a soldier's shot after it has spawned.</summary>
		public static bool IsBandShot(Projectile p)
		{
			if (p == null || !p.TryGetGlobalProjectile(out BandProjectileGuard g)) return false;
			if (g.FromBand) return true;
			if (p.TryGetGlobalProjectile(out SoldierProjectileTag t))
			{
				if (t.SoldierIndex >= 0 && t.SoldierIndex < Main.maxNPCs && Main.npc[t.SoldierIndex].active && Main.npc[t.SoldierIndex].ModNPC is BandLevyNPC) { g.FromBand = true; return true; }
				if (t.HostileShot && t.SourceNpcIndex >= 0 && t.SourceNpcIndex < Main.maxNPCs && Main.npc[t.SourceNpcIndex].active && Main.npc[t.SourceNpcIndex].ModNPC is BandLevyNPC) { g.FromBand = true; return true; }
			}
			return false;
		}

		public override bool? CanHitNPC(Projectile projectile, NPC target)
		{
			if (target != null && target.ModNPC is BandLevyNPC && IsBandShot(projectile)) { Refused++; return false; }
			return null;
		}
	}
}
