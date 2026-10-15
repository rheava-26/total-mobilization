namespace Levy.Common
{
	public enum Grade : byte { Levy = 0, ManAtArms = 1, Champion = 2 }
	public enum Temperament : byte { Greenhorn = 0, Sellsword = 1, Zealot = 2, Cynic = 3 }
	public enum SoldierKind : byte { Regular = 0, Militia = 1 }

	/// <summary>Brain states. Fear states only occur while a main boss is alive.</summary>
	public enum SoldierState : byte { Idle = 0, Follow = 1, Patrol = 2, Engage = 3, Haul = 4, Resupply = 5, Shaken = 6, Routed = 7, ReturnHome = 8, Dead = 9, Desert = 10, SearchDestroy = 11 }

	/// <summary>Standing orders for Regulars. Militia always patrol/engage at their settlement.</summary>
	/// <summary>SearchDestroy roams outward from the settlement / commander hunting enemies (bounded leash, standoff rules intact).</summary>
	public enum SoldierOrder : byte { Follow = 0, Hold = 1, Patrol = 2, ReturnToBarracks = 3, SearchDestroy = 4 }

	public enum WeaponFamily : byte { None = 0, Gun = 1, Bow = 2, Magic = 3, Broadsword = 4, Shortsword = 5, Spear = 6, Unsupported = 7 }

	public enum TombstoneMode { Vanilla, Always, Never }
	public enum MilitiaDeathMessageMode { Count, Individual, None }

	/// <summary>Dialogue triggers. BOSS_x lines are keyed by boss internal name via <see cref="DialogueTrigger.BOSS"/> + context.</summary>
	public enum DialogueTrigger
	{
		IDLE, IDLE_NIGHT, NAMEDROP, BLOOD_MOON, PARTY, GRAVEYARD, RAIN,
		BIOME_JUNGLE, BIOME_DUNGEON, BIOME_UNDERWORLD, BIOME_SNOW, BIOME_CORRUPTION, BIOME_CRIMSON, BIOME_HALLOW, BIOME_OCEAN, BIOME_UNDERGROUND,
		PAYDAY, UNPAID, DESERTED, MUSTER, STAND_DOWN,
		EVENT_GOBLINS, EVENT_PIRATES, EVENT_ECLIPSE, EVENT_PUMPKIN, EVENT_FROST, EVENT_MARTIANS, EVENT_OLDONES, EVENT_FROSTLEGION,
		ENGAGE, KILL, HURT, LOW_HP, OUT_OF_AMMO, HOLD_FIRE,
		BOSS_SIGHTED, BOSS,
		COMRADE_HIT, COMRADE_DOWN, SHAKEN, ROUTED, RALLIED, VICTORY, PROMOTED, VETERAN, DEATH_MESSAGE,
		NO_ONE_LEFT
	}

	/// <summary>Where a dialogue line is delivered.</summary>
	public enum DialogueChannel { ChatWindow, Emote, FloatingText, ChatLog }

	/// <summary>Values that fill dialogue placeholders.</summary>
	public class DialogueContext
	{
		public string Player, Comrade, Boss, Killer, Town, Wage;
		/// <summary>Internal boss name for BOSS_x lookups (e.g. "EyeOfCthulhu").</summary>
		public string BossKey;
	}
}
