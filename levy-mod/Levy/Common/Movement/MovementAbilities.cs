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
		public bool NoFallDamage = true;
		/// <summary>A dash accessory is equipped (Player.dashType: Tabi / Master Ninja Gear, Shield of Cthulhu, Solar Flare, Crystal Assassin).</summary>
		public bool HasDash;

		/// <summary>
		/// Reads the dummy's stats. Balloons add to jumpSpeedBoost in UpdateEquips; Frog Leg, Empress Brooch and Moon Lord
		/// Legs only add theirs in Player.UpdateJumpHeight (not run for the dummy), so their flags are added here.
		/// </summary>
		public static MovementAbilities FromDummy(Player dummy)
		{
			var a = new MovementAbilities();
			if (dummy == null) return a;

			a.WalkSpeed = MathHelper.Clamp(dummy.moveSpeed, LevyConstants.Movement.MinWalkSpeedMult, LevyConstants.Movement.MaxWalkSpeedMult);
			float run = Math.Max(dummy.maxRunSpeed, dummy.accRunSpeed);
			a.MaxRunSpeed = MathHelper.Clamp(run, LevyConstants.Movement.MinRunSpeed, LevyConstants.Movement.MaxRunSpeed);

			float boost = dummy.jumpSpeedBoost;
			if (dummy.frogLegJumpBoost) boost += LevyConstants.Movement.FrogLegBoost;
			if (dummy.empressBrooch) boost += LevyConstants.Movement.EmpressBroochBoost;
			if (dummy.moonLordLegs) boost += LevyConstants.Movement.MoonLordLegsBoost;
			int extra = (int)Math.Round(boost / LevyConstants.Movement.JumpBoostPerTile);
			if (dummy.jumpBoost) extra += LevyConstants.Movement.BalloonJumpTiles;
			a.JumpTiles = Math.Clamp(LevyConstants.Movement.BaseJumpTiles + extra, LevyConstants.Movement.BaseJumpTiles, LevyConstants.Movement.MaxJumpTiles);

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
			a.NoFallDamage = true;
			a.HasDash = dummy.dashType > 0;
			return a;
		}
	}
}
