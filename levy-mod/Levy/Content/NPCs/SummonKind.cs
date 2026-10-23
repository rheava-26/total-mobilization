using Terraria.ID;
using Levy.Common;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// The recipe for one kind of summoned combat unit. A single <see cref="SummonedUnitNPC"/> type serves every kind, so a new
	/// summon kit only declares one of these (and adds it to <see cref="SummonKinds"/>) — no new ModNPC. Immutable; the live values
	/// (HP, damage, projectile, sprite, speed) are read off the kind, and damage is scaled by world progress at fire time.
	/// Every number lives in <see cref="LevyConstants.Summon"/>; nothing is hard-coded here.
	/// </summary>
	public sealed class SummonKind
	{
		/// <summary>Stable id synced to clients so they resolve the kind back to its sprite/HP (see <see cref="SummonKinds.ById"/>). Must be unique and dense from 0.</summary>
		public readonly int Id;
		/// <summary>Hover name shown over the unit (the kind's flavor name, e.g. "Pirate").</summary>
		public readonly string Name;
		/// <summary>Max life of the unit (flat; the healthbar makes it a real target, not a chip minion). Not scaled by progress.</summary>
		public readonly int MaxHp;
		/// <summary>Base attack damage before the world-progress multiply (<see cref="Officers.OfficerBrain.TownDamageScale"/>).</summary>
		public readonly int BaseDamage;
		/// <summary>The vanilla NPC whose sprite the unit borrows (drawn like an officer avatar borrows its town sprite). No new art asset.</summary>
		public readonly int DrawNpcType;
		/// <summary>Attack projectile fired at a hostile in reach (a flintlock ball for the pirate; a melee kit uses a short-range fast projectile here).</summary>
		public readonly int ProjectileType;
		/// <summary>Launch speed (px/tick), knockback, and the arc lift (px) a far shot is aimed above the target to meet gravity.</summary>
		public readonly float ProjectileSpeed, Knockback, GravityCorrection;
		/// <summary>Pause after a shot (fixed + a random extra), in ticks.</summary>
		public readonly int CooldownTicks, RandomCooldownTicks;
		/// <summary>How far (tiles) a hostile may be and still be engaged.</summary>
		public readonly float RangeTiles;
		/// <summary>Walk speed cap (px/tick) when following the owning officer.</summary>
		public readonly float MoveSpeedPx;
		/// <summary>Taunt weight for enemy targeting (0 = weighed like a plain soldier body).</summary>
		public readonly int Aggro;
		/// <summary>Ticks the unit lives before it despawns on its own (0 = none; the owner respawns it on cadence either way).</summary>
		public readonly int LifespanTicks;

		public SummonKind(int id, string name, int maxHp, int baseDamage, int drawNpcType, int projectileType,
			float projectileSpeed, float knockback, float gravityCorrection, int cooldownTicks, int randomCooldownTicks,
			float rangeTiles, float moveSpeedPx, int aggro, int lifespanTicks = 0)
		{
			Id = id; Name = name; MaxHp = maxHp; BaseDamage = baseDamage; DrawNpcType = drawNpcType; ProjectileType = projectileType;
			ProjectileSpeed = projectileSpeed; Knockback = knockback; GravityCorrection = gravityCorrection;
			CooldownTicks = cooldownTicks; RandomCooldownTicks = randomCooldownTicks;
			RangeTiles = rangeTiles; MoveSpeedPx = moveSpeedPx; Aggro = aggro; LifespanTicks = lifespanTicks;
		}
	}

	/// <summary>
	/// The registry of summon kinds. Ids are explicit and dense so a unit's kind survives the network (a client resolves the synced
	/// <see cref="SummonKind.Id"/> back to the recipe). A new kit adds its kind here with the next id and references the field from its kit.
	/// </summary>
	public static class SummonKinds
	{
		/// <summary>The pirate officer's boarding crew: flintlock gunners that scale their damage with boss progress. The proof kind.</summary>
		public static readonly SummonKind PirateCrew = new SummonKind(
			id: 0,
			name: "Pirate",
			maxHp: LevyConstants.Summon.PirateUnitHp,
			baseDamage: LevyConstants.Summon.PirateUnitBaseDamage,
			drawNpcType: NPCID.PirateDeadeye,
			projectileType: ProjectileID.Bullet,
			projectileSpeed: LevyConstants.Summon.PirateFlintlockSpeed,
			knockback: LevyConstants.Summon.PirateFlintlockKnockback,
			gravityCorrection: LevyConstants.Summon.PirateFlintlockGravityCorrection,
			cooldownTicks: LevyConstants.Summon.PirateFlintlockCooldownTicks,
			randomCooldownTicks: LevyConstants.Summon.PirateFlintlockRandomCooldownTicks,
			rangeTiles: LevyConstants.Summon.PirateRangeTiles,
			moveSpeedPx: LevyConstants.Summon.PirateUnitMoveSpeedPx,
			aggro: LevyConstants.Summon.PirateUnitAggro);

		private static readonly SummonKind[] _all = { PirateCrew };

		/// <summary>The kind for a synced id, or null when the id is out of range (a stale/forward packet): the unit then waits for a valid sync.</summary>
		public static SummonKind ById(int id) => (uint)id < (uint)_all.Length ? _all[id] : null;

		public static int Count => _all.Length;
	}
}
