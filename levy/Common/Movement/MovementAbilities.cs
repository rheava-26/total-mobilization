using Terraria;

namespace Levy.Common.Movement
{
	/// <summary>What a soldier can do, read from its gear via the dummy player. OWNED BY THE MOVEMENT AGENT.</summary>
	public class MovementAbilities
	{
		public float WalkSpeed = 1f;       // multiplier on town-NPC walk speed
		public float MaxRunSpeed = 3f;     // px/tick
		public int JumpTiles = 3;          // vertical reach in tiles
		public int ExtraJumps;
		public bool HasWings; public int WingTimeMax; public bool CanGlide;
		public bool NoFallDamage = true;
		public static MovementAbilities FromDummy(Player dummy) => new MovementAbilities();
	}
}
