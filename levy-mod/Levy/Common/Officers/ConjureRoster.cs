using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Util;

namespace Levy.Common.Officers
{
	/// <summary>
	/// WHAT an officer conjures (the creature itself runs on its own AI: see <see cref="Levy.Content.NPCs.ConjuredGlobalNPC"/>).
	///  * THE CLOTHIER raises skeletons ON TIER: the ladder in <see cref="SkeletonPool"/> (Skeleton, Skeleton Archer, Angry Bones, Armored Skeleton, then the Rusty / Blue / Hell Armored Bones and the Skeleton Commando / Sniper /
	///    Tactical Skeleton once Plantera has fallen), read off how far the world has got (<see cref="Stage"/>).
	///  * THE ZOOLOGIST calls one of the STRONGEST creatures her Bestiary has unlocked (<see cref="BeastCandidates"/>): never a boss or miniboss, never a town NPC or critter, never a worm or a boss's part, never a creature whose
	///    life or contact damage is beyond what the world's stage allows (so a modded 1,000,000-life creature is never picked), and only creatures whose AI is one the army's targeting swap has been verified against, so it
	///    fights when friendly. With too few unlocked creatures to choose from she falls back to everything the stage allows.
	/// Vanilla and modded types are treated alike: nothing here names a mod.
	/// </summary>
	public static class ConjureRoster
	{
		// =====================================================================================================
		// Progression
		// =====================================================================================================
		/// <summary>How far the world has got, 0 (nothing downed) to 10 (Moon Lord). Indexes the caps in <see cref="LevyConstants.Summon.ConjureMaxLifeByStage"/>.</summary>
		public static int Stage()
		{
			if (NPC.downedMoonlord) return 10;
			if (NPC.downedAncientCultist) return 9;
			if (NPC.downedGolemBoss) return 8;
			if (NPC.downedPlantBoss) return 7;
			if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3) return 6;
			if (NPC.downedMechBossAny) return 5;
			if (Main.hardMode) return 4;
			if (NPC.downedBoss3) return 3;
			if (NPC.downedBoss2) return 2;
			if (NPC.downedBoss1) return 1;
			return 0;
		}

		// =====================================================================================================
		// The Clothier's skeletons
		// =====================================================================================================
		private static readonly int[][] _skeletonsByStage =
		{
			/* 0  nothing downed      */ new int[] { NPCID.Skeleton },
			/* 1  Eye of Cthulhu      */ new int[] { NPCID.Skeleton, NPCID.SkeletonArcher },
			/* 2  Eater / Brain       */ new int[] { NPCID.Skeleton, NPCID.SkeletonArcher },
			/* 3  Skeletron           */ new int[] { NPCID.AngryBones, NPCID.SkeletonArcher, NPCID.Skeleton },
			/* 4  Wall of Flesh       */ new int[] { NPCID.ArmoredSkeleton, NPCID.AngryBones, NPCID.SkeletonArcher },
			/* 5  a mechanical boss   */ new int[] { NPCID.ArmoredSkeleton, NPCID.AngryBones, NPCID.SkeletonArcher },
			/* 6  all three           */ new int[] { NPCID.ArmoredSkeleton, NPCID.SkeletonArcher },
			/* 7  Plantera            */ new int[]
			{
				NPCID.RustyArmoredBonesAxe, NPCID.RustyArmoredBonesFlail, NPCID.RustyArmoredBonesSword, NPCID.RustyArmoredBonesSwordNoArmor,
				NPCID.BlueArmoredBones, NPCID.BlueArmoredBonesMace, NPCID.BlueArmoredBonesNoPants, NPCID.BlueArmoredBonesSword,
				NPCID.HellArmoredBones, NPCID.HellArmoredBonesSpikeShield, NPCID.HellArmoredBonesMace, NPCID.HellArmoredBonesSword,
				NPCID.SkeletonCommando, NPCID.SkeletonSniper, NPCID.TacticalSkeleton,
			},
			/* 8  Golem               */ new int[]
			{
				NPCID.BlueArmoredBones, NPCID.BlueArmoredBonesMace, NPCID.BlueArmoredBonesNoPants, NPCID.BlueArmoredBonesSword,
				NPCID.HellArmoredBones, NPCID.HellArmoredBonesSpikeShield, NPCID.HellArmoredBonesMace, NPCID.HellArmoredBonesSword,
				NPCID.SkeletonCommando, NPCID.SkeletonSniper, NPCID.TacticalSkeleton,
			},
			/* 9  Lunatic Cultist     */ new int[]
			{
				NPCID.HellArmoredBones, NPCID.HellArmoredBonesSpikeShield, NPCID.HellArmoredBonesMace, NPCID.HellArmoredBonesSword,
				NPCID.SkeletonCommando, NPCID.SkeletonSniper, NPCID.TacticalSkeleton,
			},
			/* 10 Moon Lord           */ new int[]
			{
				NPCID.HellArmoredBones, NPCID.HellArmoredBonesSpikeShield, NPCID.HellArmoredBonesMace, NPCID.HellArmoredBonesSword,
				NPCID.SkeletonCommando, NPCID.SkeletonSniper, NPCID.TacticalSkeleton,
			},
		};

		/// <summary>The skeleton types the Clothier may raise at a stage.</summary>
		public static int[] SkeletonPool(int stage) => _skeletonsByStage[Math.Clamp(stage, 0, _skeletonsByStage.Length - 1)];

		/// <summary>One skeleton for the world as it stands now.</summary>
		public static int PickSkeleton()
		{
			var pool = SkeletonPool(Stage());
			return pool[Main.rand.Next(pool.Length)];
		}

		// =====================================================================================================
		// The Zoologist's creatures
		// =====================================================================================================
		/// <summary>What the roster knows of one creature type (read once from a fresh default NPC, in the current game mode).</summary>
		public sealed class Fact
		{
			public int Type, Life, Damage, Defense, AiStyle, Value; public string Name; public bool Modded;
			/// <summary>Life and damage in classic-mode numbers (what the caps are written in).</summary>
			public int ClassicLife, ClassicDamage;
			/// <summary>How strong it is: life weighed up by defense, plus its blow.</summary>
			public float Score => ClassicLife * (1f + Defense / 40f) + ClassicDamage * 25f;
			public override string ToString() => $"{Name} (life {ClassicLife}, damage {ClassicDamage}, def {Defense}, value {Value}{(Modded ? ", modded" : "")})";
		}

		/// <summary>The AI styles a conjured creature may run: the ones the army's own targeting swap was verified against (<see cref="LevyConstants.Targeting.SwapAiStyles"/>), minus the ones that cannot work as a fighter on land
		/// (worms need their segments, wall spiders need a wall, piranhas / jellyfish need water, antlions burrow in sand, mimics wait disguised, the flying fish only fly in rain).</summary>
		private static readonly HashSet<int> _aiOk = new(LevyConstants.Targeting.SwapAiStyles.Select(x => (int)x).Except(new int[]
		{
			NPCAIStyleID.Worm, NPCAIStyleID.Spider, NPCAIStyleID.Piranha, NPCAIStyleID.Jellyfish, NPCAIStyleID.Antlion, NPCAIStyleID.Mimic, NPCAIStyleID.BiomeMimic, NPCAIStyleID.FlyingFish,
		}));

		/// <summary>Types that are mini-bosses: strong, scripted, and not for an officer to conscript. Skipped unless <see cref="LevyConstants.Summon.ZoologistAllowMinibosses"/>.</summary>
		private static readonly HashSet<int> _minibosses = new()
		{
			NPCID.Mothron, NPCID.BloodNautilus, NPCID.MourningWood, NPCID.Pumpking, NPCID.Everscream, NPCID.IceQueen, NPCID.SantaNK1, NPCID.ElfCopter, NPCID.HeadlessHorseman,
			NPCID.PirateShip, NPCID.PirateCaptain, NPCID.MartianSaucerCore, NPCID.MartianSaucer, NPCID.MartianWalker, NPCID.DD2OgreT2, NPCID.DD2OgreT3, NPCID.DD2Betsy, NPCID.DD2DarkMageT1, NPCID.DD2DarkMageT3,
			NPCID.Paladin, NPCID.IceGolem, NPCID.SandElemental, NPCID.WyvernHead, NPCID.BigMimicCorruption, NPCID.BigMimicCrimson, NPCID.BigMimicHallow, NPCID.BigMimicJungle,
		};

		private static List<Fact> _facts; private static HashSet<int> _checklistBosses;

		/// <summary>The creature types BossChecklist (when loaded) calls a boss or miniboss. Empty when it is not there or its call fails.</summary>
		private static HashSet<int> ChecklistBosses()
		{
			if (_checklistBosses != null) return _checklistBosses;
			var set = new HashSet<int>();
			try
			{
				if (ModLoader.TryGetMod("BossChecklist", out Mod bc) && ModContent.GetInstance<Levy>() is Levy me
					&& bc.Call("GetBossInfoDictionary", me, "1.1") is Dictionary<string, Dictionary<string, object>> dict)
				{
					foreach (var entry in dict.Values)
					{
						bool flagged = entry.TryGetValue("isBoss", out object b) && b is bool bb && bb || entry.TryGetValue("isMiniboss", out object m) && m is bool mb && mb;
						if (flagged && entry.TryGetValue("npcIDs", out object ids) && ids is IEnumerable<int> list) foreach (int id in list) set.Add(id);
					}
				}
			}
			catch (Exception) { /* BossChecklist's API changed or is not ready: the flags below still hold */ }
			return _checklistBosses = set;
		}

		/// <summary>True when this type is a boss, a miniboss (unless allowed), a boss's part, a town NPC, a critter or any other thing that is not a creature to conjure. <paramref name="probe"/> is a fresh default NPC of the type.</summary>
		public static bool IsExcluded(NPC probe, out string why)
		{
			int t = probe.type; why = null;
			if (probe.boss || NPCID.Sets.ShouldBeCountedAsBoss[t]) { why = "boss"; return true; }
			if (NPCID.Sets.MPAllowedEnemies[t]) { why = "boss summon"; return true; }
			if (NPCID.Sets.BossHeadTextures[t] >= 0) { why = "boss icon"; return true; }
			if (Array.IndexOf(LevyConstants.Targeting.BossPartTypes, t) >= 0 || probe.realLife >= 0) { why = "boss part"; return true; }
			if (!LevyConstants.Summon.ZoologistAllowMinibosses && _minibosses.Contains(t)) { why = "miniboss"; return true; }
			if (ChecklistBosses().Contains(t)) { why = "BossChecklist boss"; return true; }
			if (probe.townNPC || probe.friendly || NPCID.Sets.CountsAsCritter[t] || NPCID.Sets.IsTownPet[t] || NPCID.Sets.ActsLikeTownNPC[t]) { why = "town / critter"; return true; }
			if (LevyUtil.IsProjectileNpc(t)) { why = "projectile NPC"; return true; }
			if (probe.dontTakeDamage || probe.immortal || probe.lifeMax <= 5 || probe.damage <= 0) { why = "no fight"; return true; }
			if (!_aiOk.Contains(probe.aiStyle)) { why = "ai " + probe.aiStyle; return true; }
			return false;
		}

		/// <summary>Every creature type that could ever be conjured, read once (modded types included). Excluded types are not in it.</summary>
		public static IReadOnlyList<Fact> AllFacts()
		{
			if (_facts != null) return _facts;
			var list = new List<Fact>();
			float lifeMul = Math.Max(1f, Main.GameModeInfo.EnemyMaxLifeMultiplier), dmgMul = Math.Max(1f, Main.GameModeInfo.EnemyDamageMultiplier);
			for (int type = 1; type < NPCLoader.NPCCount; type++)
			{
				try
				{
					var probe = new NPC();
					probe.SetDefaults(type);
					if (probe.type != type || IsExcluded(probe, out _)) continue;
					list.Add(new Fact
					{
						Type = type, Name = Lang.GetNPCNameValue(type), Life = probe.lifeMax, Damage = probe.damage, Defense = probe.defense, AiStyle = probe.aiStyle, Value = (int)probe.value,
						Modded = type >= NPCID.Count, ClassicLife = (int)(probe.lifeMax / lifeMul), ClassicDamage = (int)(probe.damage / dmgMul),
					});
				}
				catch (Exception) { /* a modded type that cannot be built outside a world is not a candidate */ }
			}
			return _facts = list;
		}

		/// <summary>Forgets the cached facts (the game mode changed under them, or a test wants a fresh read).</summary>
		public static void Reset() { _facts = null; _checklistBosses = null; }

		/// <summary>True when the Bestiary has this creature unlocked (seen, killed or chatted). False on any failure to read it.</summary>
		public static bool InBestiary(int type)
		{
			try
			{
				BestiaryEntry e = Main.BestiaryDB?.FindEntryByNPCID(type);
				if (e == null || e.UIInfoProvider == null) return false;
				return e.UIInfoProvider.GetEntryUICollectionInfo().UnlockState > BestiaryEntryUnlockState.NotKnownAtAll_0;
			}
			catch (Exception) { return false; }
		}

		/// <summary>
		/// The creatures the Zoologist may call at <paramref name="stage"/>, strongest first: everything under the stage's life and damage caps, and (when <paramref name="useBestiary"/>) unlocked in the Bestiary.
		/// <paramref name="usedBestiary"/> is false when the Bestiary had fewer than <see cref="LevyConstants.Summon.ZoologistMinBestiaryPool"/> unlocked candidates and the whole stage pool was used instead.
		/// </summary>
		public static List<Fact> BeastCandidates(int stage, bool useBestiary, out bool usedBestiary)
		{
			stage = Math.Clamp(stage, 0, LevyConstants.Summon.ConjureMaxLifeByStage.Length - 1);
			int lifeCap = LevyConstants.Summon.ConjureMaxLifeByStage[stage], dmgCap = LevyConstants.Summon.ConjureMaxDamageByStage[stage];
			var pool = AllFacts().Where(f => f.ClassicLife <= lifeCap && f.ClassicDamage <= dmgCap).ToList();
			usedBestiary = false;
			if (useBestiary)
			{
				var seen = pool.Where(f => InBestiary(f.Type)).ToList();
				if (seen.Count >= LevyConstants.Summon.ZoologistMinBestiaryPool) { pool = seen; usedBestiary = true; }
			}
			pool.Sort((a, b) => b.Score.CompareTo(a.Score));
			return pool;
		}

		/// <summary>One creature for the world as it stands now: a random one of the strongest few the Bestiary and the stage allow, or 0 when nothing qualifies.</summary>
		public static int PickBeast()
		{
			var c = BeastCandidates(Stage(), true, out _);
			if (c.Count == 0) return 0;
			int top = Math.Min(c.Count, LevyConstants.Summon.ZoologistPickFromTop);
			return c[Main.rand.Next(top)].Type;
		}
	}
}
