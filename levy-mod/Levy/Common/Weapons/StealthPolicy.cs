using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Compat.Calamity;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// ROGUE STEALTH PATIENCE. Calamity's rogue gets a stealth strike only from a FULL stealth bar, and ANY other throw empties the bar (<c>rogueStealth = 0</c> on the first frame of a use that was not a strike). A rogue who
	/// throws the moment the weapon is ready therefore never fills the bar; a player gets strikes by letting go for the two seconds it takes (standing still) and then throwing. A levy that fires the instant its cooldown
	/// ends never did, so a rogue levy almost never struck. This holds the throw while the bar is on its way up and the wait is short, and throws the instant it is full, and keeps a full bar for a boss that is
	/// about to be in reach instead of spending it on a rabble in front of it. Bounded: never more than <see cref="MaxHoldTicks"/> in a row, never when the bar will not fill soon (a levy running about regenerates at half
	/// speed), and never when the levy has no stealth bar at all. Server / single player only.
	/// </summary>
	internal static class StealthPolicy
	{
		/// <summary>Env LEVY_STEALTH_WAIT (lab): the longest fill time in ticks worth waiting for; 0 turns the patience off. Default 130: a full bar from empty standing still (120 ticks) is worth waiting for.</summary>
		internal static readonly int WaitTicks = Read("LEVY_STEALTH_WAIT", 130);
		internal const int MaxHoldTicks = 200, SaveForBossTicks = 240;
		/// <summary>Lab override: -1 follow the per-weapon policy, 0 never wait (spam), 1 always wait for the strike (the pre-policy behaviour).</summary>
		internal static int Force = -1;
		/// <summary>Lab counters: throws held for the bar, strikes the bar was kept for, throws saved for a boss.</summary>
		internal static int Holds, BossSaves;

		private static int Read(string n, int d) { string v = Environment.GetEnvironmentVariable(n); return !string.IsNullOrEmpty(v) && int.TryParse(v, out int x) ? x : d; }

		/// <summary>True when this throw should wait: the levy holds a rogue weapon with a stealth bar that will fill soon, or has a full bar it is saving for a boss.</summary>
		internal static bool Hold(SoldierNPC s, Item item, NPC target)
		{
			if (WaitTicks <= 0 || Force == 0 || !CombatFix.On(CombatFix.Stealth) || !LevyUtil.IsServerOrSingle || s.Dummy == null || !ExtraDamageClasses.IsRogue(item.DamageType)) return false;
			object cp = LevyRippers.Of(s.Dummy);
			if (cp == null || !LevyRippers.IsRogue(cp)) { s.StealthHoldRun = 0; return false; } // no stealth bar (no rogue armor): nothing to wait for
			RogueMode mode = Force >= 1 ? RogueMode.Wait : RoguePolicy.For(item); // the lab can force the old always-wait behaviour; otherwise the weapon's own policy (RoguePolicy.cs)
			if (mode == RogueMode.Spam || (mode == RogueMode.SmallTargets && target.boss)) { s.StealthHoldRun = 0; s.StealthSaveRun = 0; return false; }
			if (LevyRippers.StealthStrikeReady(cp))
			{
				// a full bar: throw now, unless a boss is about to be in reach and this is not it
				if (!(target.boss) && BossAboutToBeInReach(s, item) && s.StealthSaveRun < SaveForBossTicks) { s.StealthSaveRun++; BossSaves++; return true; }
				s.StealthHoldRun = 0; s.StealthSaveRun = 0;
				return false;
			}
			if (mode == RogueMode.Situational && !(target.boss || Crowd(target))) { s.StealthHoldRun = 0; return false; } // a plain throw: this strike is not worth the wait on a lone small target
			float max = LevyRippers.StealthMax(cp);
			if (max <= 0f) return false;
			bool moving = Math.Abs(s.NPC.velocity.X) > 0.4f || Math.Abs(s.NPC.velocity.Y) > 0.8f;
			float gen = LevyRippers.StealthGen(cp, moving);
			float fillTicks = Math.Max(0f, 1f - LevyRippers.Stealth(cp) / max) * 120f / gen;
			if (fillTicks > (Force == 2 ? 900 : WaitTicks) || s.StealthHoldRun >= (Force == 2 ? 900 : MaxHoldTicks)) { if (s.StealthHoldRun >= (Force == 2 ? 900 : MaxHoldTicks)) s.StealthHoldRun = 0; return false; } // too long to wait: throw as before (a run this long fires once, then waits again)
			s.StealthHoldRun++; Holds++;
			return true;
		}

		/// <summary>Three or more hostile bodies within 12 tiles of the target: a strike that pierces, bursts or splits has something to hit.</summary>
		internal static bool Crowd(NPC target)
		{
			int n = 0; float r2 = 12f * 16f * 12f * 16f;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC o = Main.npc[i];
				if (!o.active || o.friendly || o.life <= 0 || o.lifeMax <= 5 || o.dontTakeDamage || NPCID.Sets.ProjectileNPC[o.type]) continue;
				if (Vector2.DistanceSquared(o.Center, target.Center) <= r2 && ++n >= 3) return true;
			}
			return false;
		}

		/// <summary>A boss (not this levy's target) is alive and inside twice the weapon's reach: the levy will have it in range within a few seconds.</summary>
		private static bool BossAboutToBeInReach(SoldierNPC s, Item item)
		{
			float reachPx = Math.Max(600f, (s.Weapon?.RangeTiles ?? 20f) * LevyConstants.TilePx) * 2f;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || !n.boss) continue;
				if (Vector2.Distance(s.NPC.Center, n.Center) <= reachPx) return true;
			}
			return false;
		}
	}
}
