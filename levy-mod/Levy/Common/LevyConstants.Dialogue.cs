namespace Levy.Common
{
	/// <summary>Dialogue numbers. The core budget numbers (floating interval, emote budget, repeat cooldown, idle chat) live in LevyConstants.cs.</summary>
	public static partial class LevyConstants
	{
		/// <summary>How long an emote bubble stays above a soldier.</summary>
		public const int DialogueEmoteBubbleTicks = 120;
		/// <summary>A PAYDAY/UNPAID/HOLD_FIRE line stays the answer to a right-click chat for this long.</summary>
		public const int DialogueRecentLineTicks = 60 * 120;
		/// <summary>Chance that a right-click chat is a NAMEDROP line (when no event or recent line applies).</summary>
		public const float DialogueNamedropChance = 0.3f;
		/// <summary>Chance a right-click chat is about where the levy's kit ranks in the army (when it ranks best, worst or well below the average).</summary>
		public const float DialogueArmoryChance = 0.45f;
		/// <summary>A levy speaks about new gear (ARMORY_GOT) at most once in this many ticks, and the whole army at most once in <see cref="GearTalkArmyTicks"/>.</summary>
		public const int GearTalkSoldierTicks = 60 * 240, GearTalkArmyTicks = 60 * 12;
		/// <summary>A new body keeps quiet about its gear for this long (an issue at muster is not a gift).</summary>
		public const int GearTalkGraceTicks = 60 * 20;
		/// <summary>The commander's reactions: life crystal (cooldown, speakers) and chest (cooldown, chance, reach in tiles).</summary>
		public const int CrystalReactCooldownTicks = 60 * 20, CrystalReactSpeakers = 3, ChestReactCooldownTicks = 60 * 40, ReactReachTiles = 36;
		public const float ChestReactChance = 0.6f;
		/// <summary>How many levies near their commander speak when a Solar Eclipse ends.</summary>
		public const int EclipseEndSpeakers = 3;
		/// <summary>The share of drink rounds whose cheer comes from the drink's own pool (DRINK_ALE and so on) when it has one; the rest use the general DRINK pool.</summary>
		public const float DrinkOwnPoolShare = 0.65f;
		/// <summary>Chance that an applicable biome or situation line is used instead of falling through to IDLE.</summary>
		public const float DialogueSituationChance = 0.65f;
		/// <summary>Selection weight of a personality line (temperament or Veteran) relative to an Any line.</summary>
		public const int DialoguePersonalityWeight = 3;
		/// <summary>Soldiers also show the text of emote triggers (ENGAGE, KILL, HURT...) as a small floating line, within the squad budget.</summary>
		public static readonly bool DialogueChatterText = true;
		/// <summary>Floating lines longer than this are split over two or three rows.</summary>
		public const int DialogueFloatingRowChars = 34;
		/// <summary>Floating text lives this long (vanilla combat text lives 60 ticks, too short to read a sentence).</summary>
		public const int DialogueFloatingLifeTicks = 60 * 3;
		/// <summary>Only clients within this many pixels of the speaker receive a floating line.</summary>
		public const float DialogueFloatingRangePx = 3200f;
		/// <summary>Combat text rises about this many pixels over its life (vanilla: 7 px/tick decaying by 0.92). A second line from the same speaker stacks above what has not risen out of the way yet.</summary>
		public const float DialogueFloatingRisePx = 80f;
		public const float DialogueFloatingRiseDecay = 0.92f;
		/// <summary>Most rows that may be stacked above one speaker's head.</summary>
		public const int DialogueFloatingMaxStackRows = 6;

		// ---- ambient chatter (Content/NPCs/SoldierNPC.Chatter.cs): a calm levy says a line to a comrade now and then ----
		/// <summary>The first line after a levy turns calm, then the gap between lines (ticks, random in range). Only while a player is within <see cref="ChatterPlayerTiles"/>.</summary>
		public const int ChatterFirstMinTicks = 60 * 10, ChatterFirstMaxTicks = 60 * 30, ChatterMinTicks = 60 * 40, ChatterMaxTicks = 60 * 90;
		/// <summary>After a fight ends the gap shortens to this range for <see cref="ChatterAfterFightWindowTicks"/>.</summary>
		public const int ChatterAfterFightMinTicks = 60 * 12, ChatterAfterFightMaxTicks = 60 * 30, ChatterAfterFightWindowTicks = 60 * 30;
		/// <summary>A levy hit this recently stays quiet. A squad (one commander's levies) shares a minimum gap between ambient lines.</summary>
		public const int ChatterQuietTicks = 60 * 3, ChatterSquadGapTicks = 360;
		public const float ChatterPlayerTiles = 70f, ChatterComradeTiles = 14f;
		/// <summary>Chance that an ambient line is a name-drop (an aimed line about a town NPC) instead of plain idle talk.</summary>
		public const float ChatterNamedropChance = 0.25f;

		// ---- the rift sky (Common/Dialogue/RiftMood.cs; Wrath of the Gods only) ----
		/// <summary>Dawns after the sun is covered that the army stays shaken (the tense mood); after that it settles into a lasting quiet. Shock lines stop, "what now" lines start from <see cref="RiftLaterFromDay"/>.</summary>
		public const int RiftTenseDays = 3, RiftLaterFromDay = 2;
		/// <summary>Weary talk of the army's own strength from this dawn on; dark humour from this one (the army has settled).</summary>
		public const int RiftPowerFromDay = 1, RiftDarkFromDay = 3;
		public const float RiftPowerChance = 0.25f, RiftDarkChance = 0.4f;
		/// <summary>The burst when the sky swallows a boss in front of the army: speakers near each commander, ticks between them, and how recently the boss must have been seen.</summary>
		public const int RiftDukeSpeakers = 4, RiftDukeStepTicks = 55, RiftDukeSeenTicks = 180;
		/// <summary>Levies within this many tiles of where the boss was swallowed saw it.</summary>
		public const float RiftDukeReachTiles = 60f;

		// ---- round two (RiftMood.Sky.cs) ----
		/// <summary>The long slide: from these dawns the jokes turn grim, then the army is worn down. The dark jokes thin out as the grim ones take over.</summary>
		public const int RiftGrimFromDay = 7, RiftWornFromDay = 12;
		public const float RiftDarkChanceLate = 0.2f, RiftDarkChanceWorn = 0.1f, RiftGrimChance = 0.35f, RiftWornChance = 0.4f;
		/// <summary>The rift's growth tiers by NoxusBoss's intensity (0.143 after Moon Lord, rising with Calamity's later bosses to 1): tier 2 from this value, tier 3, tier 4.</summary>
		public const float RiftTier2At = 0.25f, RiftTier3At = 0.55f, RiftTier4At = 0.8f;
		/// <summary>How often rift chat is about the Blood Moon (while one is up), the fog (while it is up), the rift's size (from tier 2), or the ice (once water freezes).</summary>
		public const float RiftBloodChance = 0.7f, RiftFogChance = 0.6f, RiftGrowChance = 0.35f, RiftIceChance = 0.3f;
		/// <summary>Speakers in each burst: the first sight of a bigger rift, the first frozen water, a Blood Moon rising under the rift, a scare in the fog.</summary>
		public const int RiftGrowSpeakers = 3, RiftIceSpeakers = 2, RiftBloodSpeakers = 3, RiftScareSpeakers = 3, RiftBurstStepTicks = 70;
		/// <summary>A scare in the fog: levies within this many tiles of it saw it, and the next scare waits this long.</summary>
		public const float RiftScareReachTiles = 60f;
		public const int RiftScareCooldownTicks = 60 * 20;
		/// <summary>A Blood Moon under the rift: this share of a levy's chat is only "...", and a woman (angry on a Blood Moon, as vanilla's women are) speaks the angry set this often.</summary>
		public const float RiftHushChance = 0.3f, RiftAngryChance = 0.7f;
		/// <summary>Under the rift a boss sighting is grim and irritated this often (else the usual boss lines). Rift chat names a town NPC this often (only lines whose NPC lives).</summary>
		public const float RiftBossChance = 0.75f, RiftTownChance = 0.18f;
		/// <summary>Rift chat about the classes (a levy's own and the others'), and a temperament's small habit (a prayer, a watch, counting coin), this often.</summary>
		public const float RiftClassChance = 0.3f, RiftHabitChance = 0.2f;
		/// <summary>The fog: an idle levy's follow slot drifts outward over this many ticks to 2 to 5 tiles (by the levy), never while it fights.</summary>
		public const int RiftFogDriftTicks = 60 * 40;
		public const float RiftFogDriftMinTiles = 2f, RiftFogDriftMaxTiles = 5f;
		/// <summary>Chat bar jitter: the offset changes every this many ticks.</summary>
		public const int ChatShakeStepTicks = 4;
		/// <summary>The burst right after the sun is covered: levies near each commander, one after another; then this long of worried talk in place of the rift chat.</summary>
		public const int RiftPanicSpeakers = 4, RiftPanicStepTicks = 45, RiftPanicTicks = 60 * 90;
		/// <summary>
		/// The loud first minute of the worried talk (<see cref="RiftPanicLoudTicks"/> after the covering ends): every calm levy near the commander shouts a RIFT_PANIC line every
		/// <see cref="RiftPanicChatterMinTicks"/> to <see cref="RiftPanicChatterMaxTicks"/> ticks, and the squad waits only <see cref="RiftPanicSquadGapTicks"/> between ambient lines
		/// (a levy who finds the squad just spoke tries again 10 to <see cref="RiftPanicRetryMaxTicks"/> ticks later). With ten levies near that is a new line every second or two.
		/// After it the usual gaps (<see cref="ChatterMinTicks"/>, <see cref="ChatterSquadGapTicks"/>) are back.
		/// </summary>
		public const int RiftPanicLoudTicks = 60 * 60, RiftPanicChatterMinTicks = 60 * 2, RiftPanicChatterMaxTicks = 60 * 7, RiftPanicSquadGapTicks = 60, RiftPanicRetryMaxTicks = 45;
		/// <summary>Panic lines are shouted and may run this long (every other rift line: 60 characters).</summary>
		public const int RiftPanicLineMaxChars = 80;
		/// <summary>A levy who was dead or in reserve when the sun went asks what happened, with this chance, until he has asked this many times.</summary>
		public const float RiftAwayChance = 0.7f;
		public const int RiftAwayLines = 2;
		/// <summary>How often a rift chat line is about the cold (snow falling or lying on the surface), the other classes, the first shock, or "what now".</summary>
		public const float RiftColdChance = 0.55f, RiftRespectChance = 0.2f, RiftShockChance = 0.45f, RiftLaterChance = 0.35f;
		/// <summary>Snow this deep on the surface (the mod's own 0 to 10 scale) counts as cold even when none is falling.</summary>
		public const float RiftSnowLyingHeight = 1.5f;
		/// <summary>Share of floating lines still spoken under the rift. Chatter: ambient, camp, drinks and reactions. Barks: combat shouts. Big: boss sightings, a comrade down, routs, victory.</summary>
		public const float RiftTenseChatterShare = 0.3f, RiftSettledChatterShare = 0.5f, RiftTenseBarkShare = 0.6f, RiftSettledBarkShare = 0.8f, RiftTenseBigShare = 0.85f;

		// ---- Wrath of the Gods fights (Common/Dialogue/WotgScenes.cs) ----
		/// <summary>After the boss has the floor (a scream, a rant, Solyn, subtitles) nothing scripted is said for this long. Scripted lines are at least <see cref="SceneLineGapTicks"/> apart army-wide; the speakers of one moment <see cref="SceneStepTicks"/> apart.</summary>
		public const int SceneVoiceGapTicks = 60, SceneLineGapTicks = 100, SceneStepTicks = 110;
		/// <summary>A moment's reaction not said within this long (the boss kept the floor) is dropped; a kill's waits longer.</summary>
		public const int SceneMomentExpireTicks = 60 * 8, SceneKillExpireTicks = 60 * 20;
		/// <summary>The worried line now and then while a fight goes on: every 20 to 40 seconds, dropped if it cannot be said within six.</summary>
		public const int SceneChatterMinTicks = 60 * 20, SceneChatterMaxTicks = 60 * 40, SceneChatterExpireTicks = 60 * 6;
		/// <summary>The boss's life share at which the army starts to hope.</summary>
		public const float SceneHopeLife = 0.15f;
		/// <summary>Speakers stand within this many tiles of their commander (or of where he fell).</summary>
		public const float SceneReachTiles = 60f;
		/// <summary>A commander's death is mourned at most this often; the army greets its commander home this long after the Nameless Deity's defeat is first seen.</summary>
		public const int SceneCommanderDownCooldownTicks = 60 * 30, SceneHomecomingDelayTicks = 60 * 8;

		// ---- boss lines that acknowledge the army (Common/Compat/Calamity/BossLines.cs) ----
		/// <summary>An army is on the field when at least this many standing regular levies are deployed.</summary>
		public const int BossLineArmyMin = 3;
	}
}
