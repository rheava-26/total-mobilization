namespace Levy.Common
{
	public enum Grade : byte { Levy = 0, ManAtArms = 1, Champion = 2 }
	public enum Temperament : byte { Greenhorn = 0, Sellsword = 1, Zealot = 2, Cynic = 3 }
	public enum SoldierKind : byte { Regular = 0, Militia = 1 }

	/// <summary>Brain states. Fear states only occur while a main boss is alive.</summary>
	public enum SoldierState : byte { Idle = 0, Follow = 1, Patrol = 2, Engage = 3, Haul = 4, Resupply = 5, Shaken = 6, Routed = 7, ReturnHome = 8, Dead = 9, Desert = 10, SearchDestroy = 11, FreeRein = 12, Mutiny = 13 }

	/// <summary>Standing orders for Regulars. Militia always patrol/engage at their settlement. HoldBanners: stand at the nearest Holding Banner (SoldierNPC.Banner.cs); with none placed it is Hold.</summary>
	/// <summary>SearchDestroy roams outward from the settlement / commander hunting enemies (bounded leash, standoff rules intact).
	/// Regroup is Search & Destroy until a boss fight is on, then Free Rein for the levies in that fight (SoldierNPC.Regroup.cs); a levy that respawns under it appears beside the commander.
	/// FreeRein is the same hunt unleashed: it ranges across the whole region on its own, tied to no player and no short anchor (standoff rules intact).</summary>
	public enum SoldierOrder : byte { Follow = 0, Hold = 1, Patrol = 2, ReturnToBarracks = 3, SearchDestroy = 4, FreeRein = 5, GoTo = 6, HoldBanners = 7, Regroup = 8 }

	public enum WeaponFamily : byte { None = 0, Gun = 1, Bow = 2, Magic = 3, Broadsword = 4, Shortsword = 5, Spear = 6, Unsupported = 7, Summon = 8, Whip = 9, Boomerang = 10, Yoyo = 11, Flail = 12, Sentry = 13, HeldWeapon = 14, Thrown = 15, ThrownMelee = 16 }

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
		NO_ONE_LEFT, MUTINY,
		OFFICER_MARCH, NURSE_MARCH,
		OFFICER_CHAT_FIGHT, OFFICER_CHAT_BOSS, OFFICER_CHAT_HURT, OFFICER_CHAT_VICTORY,
		OFFICER_CHAT_MARCH, OFFICER_CHAT_HOLD, OFFICER_CHAT_IDLE,
		MUTINY_BEGIN, MUTINY_REFUSE, MUTINY_CALM, // appended: the mutineers' lines now live in the dialogue file too
		DRINK, // appended: a floating line over a levy when the army is served a round (see DrinkCheers)
		FLIGHT_SHOUT, FLIGHT_FEAR, TAKEOFF, LANDING, // appended: a winged levy's shout in the air, its fear mid-flight, a complaint before take-off, relief on landing (SoldierNPC.FlightTalk.cs)
		FIRESIDE, PASTIME, PASTIME_NIGHT, // appended: talk by a campfire, talk at a kite or a cheer, and talk under the stars (Free Rein pastimes, SoldierNPC.Leisure.cs)
		LIFE_CRYSTAL, CHEST_OPENED, // appended: the commander uses a life crystal or fruit / opens a chest near the army (CommanderReactions)
		ARMORY_GOT, ARMORY_WANT, ARMORY_BEST, ARMORY_WORST, // appended: a levy was handed better gear (floating, SoldierNPC.GearTalk.cs); right-click chat by where his kit ranks in the army
		DRINK_ALE, DRINK_STOUT, DRINK_RUM, DRINK_FIZZ, DRINK_ICEBERG, // appended: the DRINK line for one particular round (DrinkCheers.Begin knows which); DRINK is the fallback
		ECLIPSE_END, // appended: a Solar Eclipse just ended with the army near its commander (CommanderReactions)
		// appended: the rift sky of Wrath of the Gods (RiftMood). While it hangs over the world the calm and joking pools are routed to these.
		RIFT_PANIC, RIFT_SHOCK, RIFT_ABSENT, RIFT_COLD, RIFT_LATER, RIFT_RESPECT, RIFT_IDLE, RIFT_CAMP,
		RIFT_DARK, RIFT_POWER, RIFT_DUKE, // appended: dark humour once the army settles, weary talk of its own strength, and the shock when the sky swallows a boss
		RIFT_BLOOD, RIFT_GROW1, RIFT_GROW2, RIFT_GROW3, RIFT_ICE, RIFT_FROZEN, RIFT_FOG, RIFT_SCARE, RIFT_GRIM, RIFT_WORN, // appended (RiftMood.Sky.cs): a Blood Moon under the rift, the rift growing, water freezing, the fog and its scares, the long slide of morale
		RIFT_HUSH, RIFT_ANGRY, RIFT_BOSS, RIFT_TOWN, RIFT_CLASS, RIFT_HABIT, RIFT_FLIGHT, // appended (and flying under it) (with the class talk and each temperament's small habits): the silences of a Blood Moon under the rift, the women's anger on one, boss fights under the rift, and talk of the town NPCs
		BOSS_SCENE // appended: a scripted reaction in a Wrath of the Gods fight (WotgScenes); the line itself comes from a SCENE_<FIGHT>_<MOMENT> pool
	}

	/// <summary>Where a dialogue line is delivered.</summary>
	public enum DialogueChannel { ChatWindow, Emote, FloatingText, ChatLog }

	/// <summary>Values that fill dialogue placeholders.</summary>
	public class DialogueContext
	{
		public string Player, Comrade, Boss, Killer, Town, Wage;
		/// <summary>Internal boss name for BOSS_x lookups (e.g. "EyeOfCthulhu").</summary>
		public string BossKey;
		/// <summary>An explicit pool key (an officer's own OFFICER_&lt;Key&gt;_MARCH) the line is drawn from instead of the trigger's pool, when the table has that pool.</summary>
		public string PoolKey;
	}
}
