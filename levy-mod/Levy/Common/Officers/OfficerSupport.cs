using Microsoft.Xna.Framework;
using Levy.Content.NPCs;

namespace Levy.Common.Officers
{
	/// <summary>
	/// The PASSIVE officer effects that a soldier's weapon code reads shot by shot instead of through a buff on the soldier: truer aim
	/// (<see cref="OfficerKit.AimSupport"/>) and bottomless ammo (<see cref="OfficerKit.BottomlessAmmo"/>). Each is a plain range check against the on-duty officer
	/// avatars (<see cref="OfficerSystem.ActiveBodies"/>), so it needs no buff, no timer and no state: the effect is there the moment an officer stands close and gone
	/// the moment he falls, is dismissed or goes off duty. With no officer fielded, or none in reach, both return at once with a neutral answer (1 / false), so a
	/// soldier far from any such officer is exactly as he was.
	/// </summary>
	public static class OfficerSupport
	{
		/// <summary>
		/// The product of the aim-error multipliers of every on-duty officer whose accuracy reach covers <paramref name="s"/> (1 when none does). Feeds
		/// <c>WeaponUse.AimErrorMultiplier</c>, so it tightens jitter and lead error together, and it multiplies with Veteran, the Discipline book and any other officer's.
		/// </summary>
		public static float AimErrorFactor(SoldierNPC s)
		{
			if (s == null) return 1f;
			var officers = OfficerSystem.Instance.ActiveBodies;
			float m = 1f;
			for (int i = 0; i < officers.Count; i++)
			{
				OfficerBody b = officers[i];
				if (!b.IsAlive) continue;
				OfficerKit kit = b.Avatar.OfficerDef?.Kit;
				if (kit == null || !kit.AimSupport(out int radiusTiles, out float errorMult)) continue;
				if (InReach(s, b, radiusTiles)) m *= errorMult;
			}
			return m;
		}

		/// <summary>True when an on-duty officer whose kit grants bottomless ammo stands within his reach of <paramref name="s"/>. Feeds <c>WeaponUse.ConsumesAmmo</c>.</summary>
		public static bool AmmoIsFree(SoldierNPC s)
		{
			if (s == null) return false;
			var officers = OfficerSystem.Instance.ActiveBodies;
			for (int i = 0; i < officers.Count; i++)
			{
				OfficerBody b = officers[i];
				if (!b.IsAlive) continue;
				OfficerKit kit = b.Avatar.OfficerDef?.Kit;
				if (kit != null && kit.BottomlessAmmo(out int radiusTiles) && InReach(s, b, radiusTiles)) return true;
			}
			return false;
		}

		private static bool InReach(SoldierNPC s, OfficerBody officer, int radiusTiles)
		{
			float reach = radiusTiles * LevyConstants.TilePx;
			return Vector2.DistanceSquared(s.NPC.Center, officer.NPC.Center) <= reach * reach;
		}
	}
}
