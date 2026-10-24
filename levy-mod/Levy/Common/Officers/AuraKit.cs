using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Officers
{
	/// <summary>
	/// A BUFF-AURA officer kit: every <see cref="CadenceTicks"/> it scans the friendly army within <see cref="RadiusTiles"/> and keeps each unit
	/// under the kit's buffs. The buff is REFRESHED (its timer set to <see cref="DurationTicks"/>), never stacked — NPC.AddBuff only raises an
	/// existing timer toward the new value, so an aura held for an hour is exactly as strong as one held for a second. The pulse runs on the server /
	/// single player; NPC.AddBuff syncs each buff to clients itself, and the buff's STAT effect is folded onto the dummy by <see cref="SoldierNPC"/>
	/// (ApplyPotionBuffs for a soldier, Officer_RefreshStats for an officer), so the aura actually changes stats and is not a bare icon.
	/// A concrete aura declares its buff set, radius, duration and cadence; <see cref="IncludeOfficers"/> extends the reach to fellow officer avatars.
	/// </summary>
	public abstract class AuraKit : OfficerKit
	{
		/// <summary>The buff type(s) granted to every friendly unit in reach.</summary>
		protected abstract int[] BuffTypes { get; }
		/// <summary>Reach of the aura (tiles) from the officer.</summary>
		protected abstract int RadiusTiles { get; }
		/// <summary>Ticks each application lasts; refreshed every pulse so it holds steady at this value and never grows past it. Keep it longer than the cadence so it never lapses between pulses.</summary>
		protected abstract int DurationTicks { get; }
		/// <summary>Ticks between pulses.</summary>
		protected abstract int CadenceTicks { get; }
		/// <summary>Also feed fellow officer avatars (not just the soldier rank and file), the aura's own officer included. Officer avatars are deliberately kept out of the roster scan, so they are reached separately.</summary>
		protected virtual bool IncludeOfficers => false;

		private sealed class State { public int Timer; }

		public override object CreateState() => new State();

		public override void Tick(OfficerContext ctx)
		{
			if (ctx.State is not State st) return;
			if (++st.Timer < CadenceTicks) return;
			st.Timer = 0;
			Pulse(ctx.Npc.Center, BuffTypes, RadiusTiles, DurationTicks, IncludeOfficers);
		}

		/// <summary>
		/// One aura pulse: refresh <paramref name="buffs"/> (for <paramref name="durationTicks"/>) on every friendly unit within <paramref name="radiusTiles"/> of <paramref name="from"/>.
		/// Public and static so a kit with other duties — the Dryad's mark, the Merchant's handouts — can pulse an aura on its own cadence instead of being an <see cref="AuraKit"/>.
		/// </summary>
		public static void Pulse(Vector2 from, int[] buffs, int radiusTiles, int durationTicks, bool includeOfficers)
		{
			if (buffs == null || buffs.Length == 0) return;
			float reach = radiusTiles * LevyConstants.TilePx;
			float reachSq = reach * reach;

			// The soldier rank and file — the Nurse-style scan (RosterSystem.ActiveBodies keeps officer avatars out on purpose).
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (s == null || !s.IsAlive) continue;
				if (Vector2.DistanceSquared(from, s.NPC.Center) > reachSq) continue;
				Feed(s, buffs, durationTicks);
			}

			// Fellow officer avatars, reached only when the aura opts in (the officer module keeps its own live-avatar cache).
			if (includeOfficers)
			{
				var officers = OfficerSystem.Instance.ActiveBodies;
				for (int i = 0; i < officers.Count; i++)
				{
					SoldierNPC av = officers[i].Avatar;
					if (av == null || !av.NPC.active || av.NPC.life <= 0) continue;
					if (Vector2.DistanceSquared(from, av.NPC.Center) > reachSq) continue;
					Feed(av, buffs, durationTicks);
				}
			}
		}

		/// <summary>Refresh the aura's buffs on one body and flag its stats for a recompute, so the effect lands rather than showing a dead icon. AddBuff refreshes instead of stacking, and on the server it broadcasts the buff to clients.</summary>
		private static void Feed(SoldierNPC body, int[] buffs, int durationTicks)
		{
			for (int i = 0; i < buffs.Length; i++)
				if (buffs[i] > 0) body.NPC.AddBuff(buffs[i], durationTicks);
			body.StatsDirty = true;
		}
	}
}
