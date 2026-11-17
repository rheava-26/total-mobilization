using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Movement
{
	/// <summary>
	/// What a soldier can do, read from its gear via the dummy player after the core ran ResetEffects/UpdateEquips.
	/// OWNED BY THE MOVEMENT AGENT.
	/// </summary>
	public class MovementAbilities
	{
		public float WalkSpeed = 1f;       // multiplier on town-NPC walk speed (Player.moveSpeed)
		public float MaxRunSpeed = 3f;     // px/tick (a player runs 3, 6 with Hermes Boots)
		public int JumpTiles = 3;          // vertical reach in tiles
		public int ExtraJumps;             // enabled tModLoader extra jumps (bottles, jars, mounts)
		public bool HasWings; public int WingTimeMax; public bool CanGlide;
		/// <summary>Player rules: any equipped wings, or a fall-damage immunity (Lucky Horseshoe family), and the extra tiles of fall a soldier gets free (Frog Leg, balloons).</summary>
		public bool NoFallDamage; public int ExtraFall;
		/// <summary>A dash accessory is equipped (Player.dashType: Tabi / Master Ninja Gear, Shield of Cthulhu, Solar Flare, Crystal Assassin).</summary>
		public bool HasDash;
		/// <summary>The dash the gear gives, as a player's dash behaves: the speed it starts at (px/tick), the factor its speed decays by while above the run speed (0.96, or 0.94 for the shield and solar dashes), and the ticks of recovery after it.
		/// Zero speed when there is no dash. Read from Player.dashType (Tabi, Master Ninja Gear, Shield of Cthulhu, Solar Flare, Crystal Assassin) and from Calamity's accessory dashes (Asgardian Aegis, Statis' Ninja Belt, Elysian Aegis ...).</summary>
		public float DashSpeed, DashSlow = 0.96f; public int DashDelay = 20;
		/// <summary>The run numbers a player's Player.Update leaves for HorizontalMovement, after the multipliers (move speed, Magiluminescence, Shadow armor): the ground run cap (maxRunSpeed), the sprint (accRunSpeed), the acceleration and the braking.
		/// The floor under the feet changes them (asphalt, honey, ice: <see cref="TileRun"/>).</summary>
		public float GroundRun = 3f, SprintRun = 3f, RunAcceleration = 0.08f, RunSlowdown = 0.2f;
		/// <summary>Ice Skates (Player.iceSkate) and Desert Boots (Player.desertBoots): what the ice and the sand do to a runner depends on them.</summary>
		public bool IceSkate, DesertBoots;
		/// <summary>Player.spikedBoots: 1 = Shoe Spikes / Climbing Claws (wall slide and wall jump), 2 = both, Tiger Climbing Gear, Master Ninja Gear (also holds the body still on the wall). Any gear that sets it, modded too.</summary>
		public int SpikedBoots; public bool CanWallClimb => SpikedBoots > 0;
		/// <summary>
		/// The widest gap (tiles of missing floor) a running jump clears on level ground: a plain jump covers GapJumpMaxTiles; speed carries further (flight time of the jump times the run speed, kept at 60% of that), and an extra jump mid-air adds AirGapBonusTiles.
		/// </summary>
		public int GapTiles => Math.Max(LevyConstants.Movement.GapJumpMaxTiles, (int)(MaxRunSpeed * LevyConstants.Movement.GapTilesPerRunSpeed)) + (ExtraJumps > 0 ? LevyConstants.Movement.AirGapBonusTiles : 0);

		/// <summary>A usable grappling hook is in the dummy's hook slot (miscEquips[4]); its reach and reel speed in px, and how many hooks it holds.</summary>
		public bool HasHook; public float HookReachPx, HookPullPx; public int HookCount;

		/// <summary>
		/// The top ground speed (px/tick) a real player with the dummy's gear reaches. UpdateEquips leaves the raw boot numbers; Player.Update then turns them into the speeds
		/// <c>HorizontalMovement</c> uses, in this order, which the dummy never runs: <c>maxRunSpeed *= moveSpeed</c> (accRunSpeed, the boots' sprint, is NOT scaled by moveSpeed),
		/// the flag multipliers (Magiluminescence, Shadow armor: x1.15 on both), then every ModPlayer.PostUpdateRunSpeeds. The player tops out at the larger of the two.
		/// The dummy's own fields are left as they were (a second read must not scale twice).
		/// </summary>
		public static float PlayerTopRunSpeed(Player dummy) { PlayerRun(dummy, out float g, out float sp, out _, out _); return Math.Max(g, sp); }

		/// <summary>The four run numbers (ground cap, sprint, acceleration, braking) as <see cref="PlayerTopRunSpeed"/> derives them; the dummy's own fields are left as they were.</summary>
		public static void PlayerRun(Player dummy, out float groundRun, out float sprint, out float accel, out float slowdown)
		{
			float maxRun0 = dummy.maxRunSpeed, accRun0 = dummy.accRunSpeed, accel0 = dummy.runAcceleration, slow0 = dummy.runSlowdown;
			try
			{
				dummy.runAcceleration *= dummy.moveSpeed; dummy.maxRunSpeed *= dummy.moveSpeed;
				if (dummy.hasMagiluminescence) { dummy.runAcceleration *= 1.75f; dummy.maxRunSpeed *= 1.15f; dummy.accRunSpeed *= 1.15f; dummy.runSlowdown *= 1.75f; }
				if (dummy.shadowArmor) { dummy.runAcceleration *= 1.75f; dummy.maxRunSpeed *= 1.15f; dummy.accRunSpeed *= 1.15f; dummy.runSlowdown *= 1.75f; }
				try { PlayerLoader.PostUpdateRunSpeeds(dummy); } catch { } // a mod's own run-speed rule (boots, armor sets, accessories); one that throws on the dummy simply does not count
				groundRun = dummy.maxRunSpeed; sprint = dummy.accRunSpeed; accel = dummy.runAcceleration; slowdown = dummy.runSlowdown;
			}
			finally { dummy.maxRunSpeed = maxRun0; dummy.accRunSpeed = accRun0; dummy.runAcceleration = accel0; dummy.runSlowdown = slow0; }
		}

		/// <summary>The dash of the dummy's gear: Player.dashType's own numbers (Player.DashMovement), else a Calamity accessory dash.</summary>
		private static void ReadDash(Player d, MovementAbilities a)
		{
			a.DashSpeed = 0f; a.DashSlow = 0.96f; a.DashDelay = 20;
			if (d.dashType > 0)
			{
				switch (d.dashType)
				{
					case 2: a.DashSpeed = 14.5f; a.DashSlow = 0.94f; a.DashDelay = 30; break; // Shield of Cthulhu
					case 3: a.DashSpeed = 21.9f; a.DashSlow = 0.94f; a.DashDelay = 20; break; // Solar Flare
					default: a.DashSpeed = 16.9f; break;                                          // Tabi, Master Ninja Gear, Crystal Assassin
				}
				a.HasDash = true; return;
			}
			if (global::Levy.Common.Compat.Calamity.CalamityDashes.TryRead(d, out float v)) { a.DashSpeed = Math.Clamp(v, 8f, LevyConstants.Movement.DashMaxSpeedPx); a.HasDash = true; }
		}

		/// <summary>
		/// Reads the dummy's stats. Balloons add to jumpSpeedBoost in UpdateEquips; Frog Leg, Empress Brooch and Moon Lord
		/// Legs only add theirs in Player.UpdateJumpHeight (not run for the dummy), so their flags are added here.
		/// </summary>
		public static MovementAbilities FromDummy(Player dummy)
		{
			var a = new MovementAbilities();
			if (dummy == null) return a;

			a.WalkSpeed = MathHelper.Clamp(dummy.moveSpeed, LevyConstants.Movement.MinWalkSpeedMult, LevyConstants.Movement.MaxWalkSpeedMult);
			PlayerRun(dummy, out a.GroundRun, out a.SprintRun, out a.RunAcceleration, out a.RunSlowdown);
			a.IceSkate = dummy.iceSkate; a.DesertBoots = dummy.desertBoots;
			a.MaxRunSpeed = MathHelper.Clamp(Math.Max(a.GroundRun, a.SprintRun), LevyConstants.Movement.MinRunSpeed, LevyConstants.Movement.MaxRunSpeed);

			float boost = dummy.jumpSpeedBoost;
			if (dummy.frogLegJumpBoost) boost += LevyConstants.Movement.FrogLegBoost;
			if (dummy.empressBrooch) boost += LevyConstants.Movement.EmpressBroochBoost;
			if (dummy.moonLordLegs) boost += LevyConstants.Movement.MoonLordLegsBoost;
			int extra = (int)Math.Round(boost / LevyConstants.Movement.JumpBoostPerTile);
			if (dummy.jumpBoost) extra += LevyConstants.Movement.BalloonJumpTiles;
			int baseJump = Content.NPCs.SoldierNPC.PlatMode >= 1 ? (Content.NPCs.SoldierNPC.RowsMode >= 1 ? LevyConstants.Movement.RowBaseJumpTiles : LevyConstants.Movement.PlayerBaseJumpTiles) : LevyConstants.Movement.BaseJumpTiles;
			a.JumpTiles = Math.Clamp(baseJump + extra, baseJump, LevyConstants.Movement.MaxJumpTiles);

			int jumps = 0;
			var ordered = ExtraJumpLoader.OrderedJumps;
			if (ordered != null)
			{
				for (int i = 0; i < ordered.Count; i++)
				{
					ExtraJump jump = ordered[i];
					if (jump != null && dummy.GetJumpState(jump).Enabled) jumps++;
				}
			}
			a.ExtraJumps = jumps;

			a.HasWings = dummy.wingsLogic > 0 && dummy.wingTimeMax > 0;
			a.WingTimeMax = a.HasWings ? dummy.wingTimeMax : 0;
			a.CanGlide = a.HasWings;
			a.NoFallDamage = dummy.noFallDmg || dummy.equippedWings != null; a.ExtraFall = dummy.extraFall;
			ReadDash(dummy, a);
			a.SpikedBoots = Math.Clamp(dummy.spikedBoots, 0, 2);
			var hook = HookInfo.Of(dummy.miscEquips != null && dummy.miscEquips.Length > 4 ? dummy.miscEquips[4] : null);
			if (hook.Usable) { a.HasHook = true; a.HookReachPx = hook.ReachPx; a.HookPullPx = hook.PullPx; a.HookCount = hook.Count; }
			return a;
		}
	}
}
