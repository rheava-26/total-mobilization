using Microsoft.Xna.Framework;
using Terraria.ID;

namespace Levy.Common
{
	/// <summary>
	/// Numbers for the armor set bonuses that need a visible body of their own on a soldier: the Nebula booster pickups (SoldierNebulaBooster), the Spectre Mask's Wrath bolts
	/// (SoldierNPC.SpectreWrathOnHit) and the Stardust Guardian (SoldierStardustGuardian). Additive partial; the older set-bonus numbers (the Nebula booster levels, the Spectre cooldown
	/// pool, the other sets) live in LevyConstants.MiscProj.cs.
	/// </summary>
	public static partial class LevyConstants
	{
		// ---------- Nebula armor: the booster pickups (Projectile.Damage drops ItemID.NebulaPickup1/2/3; Player.PickupItem -> NebulaLevelup) ----------
		/// <summary>The vanilla pickup that stands for each booster kind (0 life, 1 damage, 2 mana): ItemID.NebulaPickup2 (3454), NebulaPickup1 (3453), NebulaPickup3 (3455).</summary>
		public static readonly int[] NebulaPickupItem = { ItemID.NebulaPickup2, ItemID.NebulaPickup1, ItemID.NebulaPickup3 };
		/// <summary>The dust each booster kind sheds: life pink, damage orange, mana violet (the colour of the pickup's own glow).</summary>
		public static readonly int[] NebulaPickupDust = { DustID.PinkTorch, DustID.OrangeTorch, DustID.PurpleTorch };
		/// <summary>The light each pickup casts (Item.UpdateItem_VisualEffects): life, damage, mana.</summary>
		public static readonly Vector3[] NebulaPickupLight = { new(0.5f, 0.1f, 0.25f), new(0.5f, 0.3f, 0.05f), new(0.25f, 0.1f, 0.5f) };
		/// <summary>The pickup pops out of the struck enemy sideways (away from the soldier) at 10..31 x 0.2 px a tick and up/down at -20..1 x 0.2, drifting on friction for this many ticks.</summary>
		public const float NebulaOrbPopXMin = 2f, NebulaOrbPopXMax = 6.2f, NebulaOrbPopYMin = -4f, NebulaOrbPopYMax = 0.2f, NebulaOrbPopFriction = 0.96f;
		public const int NebulaOrbPopTicks = 10;
		/// <summary>Player.PullItem_Pickup(item, 12, 5): once it is pulled the pickup steers at 12 px a tick, blending (v * 4 + aim) / 5.</summary>
		public const float NebulaOrbSpeed = 12f, NebulaOrbPull = 5f;
		/// <summary>A soldier who runs or flies faster than the pickup is chased at his speed plus this much, so a booster always catches him.</summary>
		public const float NebulaOrbCatchUpPx = 4f;
		/// <summary>The soldier takes the booster when it comes this close (px) or touches him; a pickup that never arrives expires after this many ticks.</summary>
		public const float NebulaOrbArriveRangePx = 22f, NebulaOrbBurstRangePx = 80f;
		public const int NebulaOrbTimeLeftTicks = 900, NebulaOrbSizePx = 12;
		/// <summary>The pickups' sprite sheet is four frames, a new one every six ticks (Main.RegisterItemAnimation(3453, new DrawAnimationVertical(6, 4))).</summary>
		public const int NebulaOrbFrames = 4, NebulaOrbFrameTicks = 6;
		/// <summary>Dust shed by the pickup in flight (one in this many ticks), the burst where it pops out, and the burst where the soldier takes it.</summary>
		public const int NebulaOrbTrailEveryTicks = 2, NebulaOrbPopDust = 8, NebulaOrbTakeDust = 16;
		/// <summary>Player.UpdateManaRegen: each Mana Booster level adds one to a counter a tick and a mana point is paid out each time it reaches six (10 / 20 / 30 mana a second at levels 1 / 2 / 3).</summary>
		public const int NebulaManaCounterPerPoint = 6;

		// ---------- Spectre Mask: Wrath bolts (Projectile.ghostHurt -> ProjectileID.SpectreWrath) ----------
		/// <summary>Spectral dust thrown from the struck enemy when a Wrath bolt is made, so the proc reads at a glance.</summary>
		public const int SpectreWrathFlashDust = 10;
		public const float SpectreWrathFlashSpeed = 3.5f;

		// ---------- Stardust armor: the Guardian (Player.UpdateArmorSets casts ProjectileID.StardustGuardian: damage 30, knockback 10; Projectile.AI_120_StardustGuardian) ----------
		/// <summary>Vanilla's cast: the Guardian's base damage (scaled by the wearer's summon damage) and knockback.</summary>
		public const int StardustGuardianBaseDamage = 30;
		public const float StardustGuardianKnockback = 10f;
		/// <summary>Hitbox, sprite sheet frames and the per-enemy hit gap (usesLocalNPCImmunity, localNPCHitCooldown 5).</summary>
		public const int StardustGuardianWidthPx = 50, StardustGuardianHeightPx = 80, StardustGuardianFrames = 19, StardustGuardianHitCooldownTicks = 5;
		/// <summary>Resting place: 5 px plus half the wearer's width behind him (opposite his facing), 25 px above his centre; the Guardian glides there at 5% of the gap a tick.</summary>
		public const float StardustGuardianRestBackPx = 5f, StardustGuardianRestLiftPx = 25f, StardustGuardianFollowLerp = 0.05f;
		/// <summary>Idle: scans for an enemy only within this much of the resting place (vanilla's num + 20), eases toward it inside the leash, and the vertical band that eases it.</summary>
		public const float StardustGuardianLeashPx = 150f, StardustGuardianScanSlackPx = 20f, StardustGuardianVerticalPx = 100f;
		/// <summary>Reach: an enemy within this many px of the wearer is fair game, the Guardian commits to one this near it, and gives up when it is farther than the lookup from the wearer.</summary>
		public const float StardustGuardianLookupPx = 500f, StardustGuardianAttackPx = 500f;
		/// <summary>Punching: the Guardian hangs this far off the enemy's side (plus half its width) 10 px above its middle, closes at 6 px a tick scaling up to x4 with distance (50..400 px), accelerating 32.</summary>
		public const float StardustGuardianStandoffPx = 70f, StardustGuardianStandoffLiftPx = -10f, StardustGuardianChaseSpeed = 6f, StardustGuardianChaseAccel = 32f;
		public const float StardustGuardianChaseNearPx = 50f, StardustGuardianChaseFarPx = 400f, StardustGuardianChaseMaxMult = 4f;
		/// <summary>The fist: an 80 x 40 box centred 40 px in front of the Guardian, live only while it punches.</summary>
		public const float StardustGuardianFistReachPx = 40f, StardustGuardianFistWidthPx = 80f, StardustGuardianFistHeightPx = 40f;
		/// <summary>Frame cycle: idle frames 0-7 (9 ticks each), punch frames 14-18 (3 ticks each, the wind-up frames 12-13 8 ticks each), settle on 18 when it lets go.</summary>
		public const int StardustGuardianIdleFrames = 8, StardustGuardianIdleFrameTicks = 9, StardustGuardianPunchFirstFrame = 12, StardustGuardianPunchLoopFrame = 14, StardustGuardianPunchEndFrame = 19, StardustGuardianPunchFrameTicks = 3, StardustGuardianWindUpFrameTicks = 8, StardustGuardianReleaseFrame = 18;
		/// <summary>While punching it re-picks its enemy this often (ticks); idle it scans this often, staggered by projectile index.</summary>
		public const int StardustGuardianRetargetTicks = 3, StardustGuardianScanTicks = 3;
		/// <summary>A Guardian farther than this (px) from its wearer (a recall, a teleport) snaps to him in a flash instead of gliding across the map.</summary>
		public const float StardustGuardianTeleportDistPx = 1400f;
		/// <summary>The materialise flash: dust thrown when it appears (30 pairs in vanilla).</summary>
		public const int StardustGuardianPoofDust = 30;
		/// <summary>How often a soldier checks that he has exactly one Guardian while the set is worn (ticks), and the pause after a spawn.</summary>
		public const int StardustGuardianMaintainIntervalTicks = 20, StardustGuardianSpawnCooldownTicks = 40;
		/// <summary>The server re-sends a Guardian this often (ticks, staggered by projectile index) so remote screens keep it where it is (every change of target is sent at once; remote screens run the same movement between syncs); the punch spark is sent at most this often per Guardian.</summary>
		public const int StardustGuardianNetRefreshTicks = 60, StardustGuardianPunchFxTicks = 8;
		/// <summary>Ticks a Guardian is kept alive by each AI tick; the authority ends it when its wearer dies or takes the set off.</summary>
		public const int StardustGuardianTimeLeftTicks = 120;

		// ---------- Diagnostics ----------
		/// <summary>The "[Levy SETBONUS]" log writes at most this many distinct first-time lines per world, so a bad session cannot flood the log.</summary>
		public const int SetBonusLogMaxKeys = 64;
	}
}
