using Terraria;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// A soldier's sentry attacks what its soldier is fighting. Sentries (and sentry-minions such as Calamity's Perdition beacon, which attacks ONLY an explicitly chosen target) pick their victim through the OWNER's
	/// <c>MinionAttackTargetNPC</c> ("Player.HasMinionAttackTargetNPC", set by a player's right-click): a soldier's sentry is owned by the commander, whose own choice would otherwise steer it (or leave it with no target).
	/// For the length of the tower's AI the owner's attack target is the soldier's target (<see cref="SoldierMinions.AttackTarget"/>, inside the soldier's leash), put back after. The same rule the driven minions already follow.
	/// </summary>
	public class SentryOwnerTarget : GlobalProjectile
	{
		private static Player _owner; private static int _saved;

		/// <summary>Puts the owner's own attack target back if a later PreAI skipped this tower's PostAI.</summary>
		internal static void Heal() { if (_owner != null) { _owner.MinionAttackTargetNPC = _saved; _owner = null; } }

		public override bool PreAI(Projectile p)
		{
			Heal();
			if (!p.sentry || !LevyUtil.IsServerOrSingle || p.owner < 0 || p.owner >= Main.maxPlayers || !p.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex < 0) return true;
			SoldierNPC s = EnemyTargeting.LiveBody(tag.SoldierIndex);
			if (s == null) return true;
			int t = SoldierMinions.AttackTarget(s);
			if (t < 0) return true;
			Player o = Main.player[p.owner];
			if (o == null) return true;
			_owner = o; _saved = o.MinionAttackTargetNPC;
			o.MinionAttackTargetNPC = t;
			return true;
		}

		public override void PostAI(Projectile p)
		{
			if (_owner == null) return;
			_owner.MinionAttackTargetNPC = _saved;
			_owner = null;
		}
	}

	public class SentryOwnerTargetSystem : ModSystem
	{
		public override void PostUpdateProjectiles() => SentryOwnerTarget.Heal();
	}
}
