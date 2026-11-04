using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Common.Util;

namespace Levy.Common.Boss
{
	/// <summary>
	/// Keeps a boss that has been redirected onto the soldier army from fleeing / despawning before the fight is genuinely over.
	///
	/// Why it is needed: the army targeting swap only puts a soldier's stand-in Player into Main.player[255] for the one AI call it
	/// redirects (EnemyTargeting Swap/Unswap). Vanilla's own keep-alive, NPC.CheckActive, scans ONLY real players Main.player[0..254]
	/// (NPC.cs: "for (i = 0; i &lt; 255; i++)") and refreshes npc.timeLeft only while a real player is near the screen. The soldier in
	/// slot 255 is invisible to it, so a boss that chases the army away from the commander never gets its timeLeft refreshed, decays to
	/// zero and despawns — and any EncourageDespawn its AI raises that tick (timeLeft slashed, despawnEncouraged set) stands. While a
	/// boss is validly locked on a LIVE soldier/officer we refresh timeLeft and clear despawnEncouraged so it keeps fighting the army.
	///
	/// This is deliberately NOT "pin the boss forever": the refresh happens only while a live soldier target exists. With no live
	/// soldier to fight, nothing here fires and the boss despawns on vanilla's own terms (daytime, no players left, wandered too far).
	///
	/// Carve-out: Skeletron and Skeletron Prime (heads, hands and limbs) are never held into daytime. At dawn they enrage into a fast,
	/// lethal spin that — against a deathless, continuously respawning army — is an endless massacre. So while Main.dayTime is true they
	/// get neither the keep-alive here nor the soldier redirect (EnemyTargeting.BossAllowlisted consults <see cref="IsDaytimeExcluded"/>),
	/// reverting them to vanilla daytime behaviour: they fight the commander and leave as vanilla does. This applies to those two only.
	/// </summary>
	public class BossPersistenceGlobalNPC : GlobalNPC
	{
		/// <summary>Skeletron and Skeletron Prime, heads + every part/limb: the only bosses with the daytime carve-out.</summary>
		private static readonly HashSet<int> _skeletronDaytime = new()
		{
			NPCID.SkeletronHead, NPCID.SkeletronHand,
			NPCID.SkeletronPrime, NPCID.PrimeCannon, NPCID.PrimeSaw, NPCID.PrimeVice, NPCID.PrimeLaser,
		};

		/// <summary>Boss segments that carry neither <c>boss</c> nor ShouldBeCountedAsBoss but are army-drawable (mirrors EnemyTargeting).</summary>
		private static readonly HashSet<int> _bossParts = new(LevyConstants.Targeting.BossPartTypes);

		public override void PostAI(NPC npc)
		{
			// timeLeft / active are server-authoritative (CheckActive runs server side; clients mirror the despawn through the net).
			if (!LevyUtil.IsServerOrSingle || !EnemyTargeting.Enabled) return;
			LevyConfig cfg = LevyConfig.Get;
			if (cfg == null || !cfg.BossesTargetSoldiers) return;
			if (!IsBossLike(npc) || IsDaytimeExcluded(npc)) return;
			// Only while the boss is validly locked on a live soldier/officer body. If the lock is gone (soldier died, no army left),
			// we do nothing and vanilla's despawn resumes — the fight is genuinely over.
			if (EnemyTargeting.LiveTarget(LevyNpcData.Of(npc).SoldierTarget) == null) return;
			if (npc.timeLeft < NPC.activeTime) npc.timeLeft = NPC.activeTime;
			npc.despawnEncouraged = false;
		}

		/// <summary>True for Skeletron / Skeletron Prime (and their parts) while it is daytime. Used here and by EnemyTargeting.BossAllowlisted.</summary>
		public static bool IsDaytimeExcluded(NPC npc) => Main.dayTime && npc != null && _skeletronDaytime.Contains(npc.type);

		/// <summary>
		/// True for the NPCs EnemyTargeting treats as bosses for the army-redirect (the four boss guards of IsAllowlisted): a boss, a
		/// ShouldBeCountedAsBoss type, a known boss segment, a realLife part of a boss, or a main-boss part. Restricts the keep-alive to
		/// bosses so ordinary redirected enemies still despawn off-screen as normal.
		/// </summary>
		private static bool IsBossLike(NPC npc)
		{
			if (npc == null || !npc.active) return false;
			int type = npc.type;
			if (npc.boss) return true;
			if (type < 0 || type >= NPCID.Sets.ShouldBeCountedAsBoss.Length) return false;
			if (NPCID.Sets.ShouldBeCountedAsBoss[type] || _bossParts.Contains(type)) return true;
			if (npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife].boss) return true;
			return BossRule.IsMainBoss(LevyUtil.RootNpc(npc));
		}
	}
}
