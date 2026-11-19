using Terraria.ModLoader;
using Levy.Common.Soldiers;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// Round two of the rift self-check: the real growth, ice and fog-creature reads (with NoxusBoss), the growth tiers from the intensity, each new pool gated on its state,
	/// the morale slide by day, the first-sight bursts (said once, deferred through a boss fight, silent for what a world already had), the Blood Moon rising, and the scare rule.
	/// Runs inside <see cref="RiftMoodCheck"/>'s mood section (the overrides are put back there).
	/// </summary>
	public partial class RiftMoodCheck
	{
		private void SkyChecks(SoldierRecord pat, DialogueContext ctx)
		{
			RiftPhase? phase0 = RiftMood.TestPhase; int? days0 = RiftMood.TestDays;

			// ---- the real reads
			if (ModLoader.HasMod(RiftSky.ModName))
			{
				float inten = RiftSky.Intensity;
				Check($"the real growth reads its first step at boot (intensity {inten:F3}, tier {RiftMood.Tier}); no ice; the fog creatures are found (tree {RiftSky.FogwoodType}, mirror {RiftSky.MirrorwalkerType})",
					inten > 0.1f && inten < 0.2f && RiftMood.Tier == 1 && !RiftSky.IceCover && !RiftSky.IceAll && RiftSky.FogwoodType >= 0 && RiftSky.MirrorwalkerType >= 0);
			}
			else Check($"without NoxusBoss the growth, ice and fog reads are off (intensity {RiftSky.Intensity}, types {RiftSky.FogwoodType}/{RiftSky.MirrorwalkerType})", RiftSky.Intensity == 0f && !RiftSky.IceCover && !RiftSky.IceAll && RiftSky.FogwoodType < 0);

			// ---- tiers from the intensity steps NoxusBoss uses
			float[] steps = { 0.143f, 0.286f, 0.429f, 0.571f, 0.714f, 0.857f, 1f };
			int[] want = { 1, 2, 2, 3, 3, 4, 4 };
			bool tiersOk = true; string got = "";
			for (int i = 0; i < steps.Length; i++) { int t = RiftMood.TierOf(steps[i]); got += t; if (t != want[i]) tiersOk = false; }
			Check($"the growth tiers over NoxusBoss's intensity steps are 1,2,2,3,3,4,4 ({got})", tiersOk);

			// ---- each pool only in its state
			RiftMood.TestPhase = RiftPhase.Settled; RiftMood.TestDays = 4;
			RiftSky.TestIntensity = 0.143f;
			int g1 = Count3(pat, ctx, out int g2, out int g3);
			RiftSky.TestIntensity = 0.3f;
			int h1 = Count3(pat, ctx, out int h2, out int h3);
			RiftSky.TestIntensity = 1f;
			int k1 = Count3(pat, ctx, out int k2, out int k3);
			Check($"growth talk follows the tier: tier 1 none ({g1}/{g2}/{g3}), tier 2 the first only ({h1}/{h2}/{h3}), tier 4 the last only ({k1}/{k2}/{k3}) in 200", g1 + g2 + g3 == 0 && h1 > 30 && h2 + h3 == 0 && k3 > 30 && k1 + k2 == 0);
			RiftSky.TestIntensity = 0.143f;

			int ice0 = CountPool(DialogueTrigger.RIFT_ICE, pat, ctx, 200) + CountPool(DialogueTrigger.RIFT_FROZEN, pat, ctx, 200);
			RiftSky.TestIceCover = true;
			int ice1 = CountPool(DialogueTrigger.RIFT_ICE, pat, ctx, 200), fr1 = CountPool(DialogueTrigger.RIFT_FROZEN, pat, ctx, 200);
			RiftSky.TestIceAll = true;
			int ice2 = CountPool(DialogueTrigger.RIFT_ICE, pat, ctx, 200), fr2 = CountPool(DialogueTrigger.RIFT_FROZEN, pat, ctx, 200);
			Check($"ice talk: none before ({ice0}), frozen ponds once still water freezes ({ice1}/{fr1}), the frozen sea once all of it does ({ice2}/{fr2})", ice0 == 0 && ice1 > 20 && fr1 == 0 && fr2 > 20 && ice2 == 0);
			RiftSky.TestIceCover = RiftSky.TestIceAll = null;

			RiftSky.TestFog = false; int fog0 = CountPool(DialogueTrigger.RIFT_FOG, pat, ctx, 200);
			RiftSky.TestFog = true; int fog1 = CountPool(DialogueTrigger.RIFT_FOG, pat, ctx, 200);
			RiftSky.TestFog = null;
			RiftMood.TestBlood = false; int bl0 = CountPool(DialogueTrigger.RIFT_BLOOD, pat, ctx, 200);
			RiftMood.TestBlood = true; int bl1 = CountPool(DialogueTrigger.RIFT_BLOOD, pat, ctx, 200);
			RiftMood.TestBlood = null;
			Check($"fog talk only in the fog ({fog0} then {fog1}); a Blood Moon under the rift takes over most chat ({bl0} then {bl1} in 200)", fog0 == 0 && fog1 > 80 && bl0 == 0 && bl1 > 110);

			// ---- the morale slide
			int[] dayAt = { 3, LevyConstants.RiftGrimFromDay, LevyConstants.RiftWornFromDay };
			int[] dark = new int[3], grim = new int[3], worn = new int[3];
			for (int i = 0; i < 3; i++)
			{
				RiftMood.TestDays = dayAt[i];
				dark[i] = CountPool(DialogueTrigger.RIFT_DARK, pat, ctx, 300); grim[i] = CountPool(DialogueTrigger.RIFT_GRIM, pat, ctx, 300); worn[i] = CountPool(DialogueTrigger.RIFT_WORN, pat, ctx, 300);
			}
			Note($"morale slide in 300 chats (dark/grim/worn): day {dayAt[0]} {dark[0]}/{grim[0]}/{worn[0]}, day {dayAt[1]} {dark[1]}/{grim[1]}/{worn[1]}, day {dayAt[2]} {dark[2]}/{grim[2]}/{worn[2]}");
			Check("the morale slide: dark jokes first, grim ones from the seventh dawn, worn-down talk from the twelfth, the dark jokes thinning as it goes",
				grim[0] == 0 && worn[0] == 0 && dark[0] > 60 && grim[1] > 40 && worn[1] == 0 && worn[2] > 50 && dark[2] < dark[1] && dark[1] < dark[0]);

			// ---- the first-sight bursts
			bool? boss0 = RosterFacts.TestBoss;
			RiftMood.TestPhase = RiftPhase.Settled; RiftMood.TestDays = 4; RosterFacts.TestBoss = false;
			RiftMood.Reset(); RiftSky.TestIntensity = 0.6f; RiftSky.TestIceCover = true; RiftMood.TestBlood = true;
			int b0 = RiftMood.BurstsFired;
			RiftMood.Tick(5000); RiftMood.Tick(5001); RiftMood.Tick(5002);
			Check($"a world first seen already grown, frozen and under a Blood Moon says nothing for it ({RiftMood.BurstsFired - b0} bursts; tier seen {RiftMood.TierSeen}, ice seen {RiftMood.IceSeen})", RiftMood.BurstsFired == b0 && RiftMood.TierSeen == 3 && RiftMood.IceSeen == 1);
			RiftMood.TestBlood = false; RiftMood.Tick(5003);
			RosterFacts.TestBoss = true; RiftSky.TestIntensity = 0.9f; RiftSky.TestIceAll = true; RiftMood.Tick(5004); RiftMood.Tick(5005);
			int during = RiftMood.BurstsFired - b0;
			RosterFacts.TestBoss = false; RiftMood.Tick(5006);
			bool grown = RiftMood.LastBurst == DialogueTrigger.RIFT_FROZEN || RiftMood.LastBurst == DialogueTrigger.RIFT_GROW3;
			int afterFight = RiftMood.BurstsFired - b0;
			RiftMood.Tick(5007); RiftMood.Tick(5008);
			Check($"the rift growing and the sea freezing during a boss fight wait for it to end, then speak once each ({during} during, {afterFight} after, {RiftMood.BurstsFired - b0} later)", during == 0 && afterFight == 2 && grown && RiftMood.BurstsFired - b0 == 2);
			RiftMood.TestBlood = true; RiftMood.Tick(5009); RiftMood.Tick(5010);
			Check($"a Blood Moon rising under the rift is one burst from RIFT_BLOOD ({RiftMood.BurstsFired - b0 - 2}, {RiftMood.LastBurst})", RiftMood.BurstsFired - b0 == 3 && RiftMood.LastBurst == DialogueTrigger.RIFT_BLOOD);
			RiftMood.TestPhase = RiftPhase.None; RiftMood.TestBlood = false; RiftMood.Tick(5011); RiftMood.TestBlood = true; RiftMood.Tick(5012);
			Check("with no rift mood a Blood Moon is just a Blood Moon (no burst)", RiftMood.BurstsFired - b0 == 3);
			RosterFacts.TestBoss = boss0; RiftMood.TestBlood = null; RiftSky.TestIntensity = null; RiftSky.TestIceCover = RiftSky.TestIceAll = null; RiftMood.Reset();

			// ---- the scare rule
			bool scare = RiftMood.IsScare(1, 0f, 1, 1f, false) && !RiftMood.IsScare(1, 1f, 1, 1f, false) && !RiftMood.IsScare(1, 0f, 1, 2f, false) && !RiftMood.IsScare(1, 0f, 0, 0f, true)
				&& RiftMood.IsScare(2, 0f, 0, 0f, true) && !RiftMood.IsScare(2, 0f, 0, 0f, false) && !RiftMood.IsScare(2, 0f, 2, 0f, false) && !RiftMood.IsScare(0, 0f, 2, 0f, false);
			Check("a scare is a fog tree going from its creep to its scream, or a mirror figure vanishing alive (not killed, not a tree dying)", scare);
			string sc = DialogueSystem.Pick(DialogueTrigger.RIFT_SCARE, pat, ctx, null, true, -1, out _, false);
			Check($"the bursts are never thinned and draw from their pools ({sc})", sc != null && RiftMood.AllowFloating((byte)DialogueTrigger.RIFT_SCARE) && RiftMood.AllowFloating((byte)DialogueTrigger.RIFT_GROW2) && RiftMood.AllowFloating((byte)DialogueTrigger.RIFT_BLOOD));

			RiftMood.TestPhase = phase0; RiftMood.TestDays = days0;
		}

		private static int Count3(SoldierRecord pat, DialogueContext ctx, out int b, out int c)
		{
			int a = 0; b = c = 0;
			for (int i = 0; i < 200; i++)
			{
				DialogueSystem.Pick(DialogueTrigger.IDLE, pat, ctx, null, true, -1, out DialogueLine pl, false);
				if (In(DialogueTrigger.RIFT_GROW1, pl)) a++; else if (In(DialogueTrigger.RIFT_GROW2, pl)) b++; else if (In(DialogueTrigger.RIFT_GROW3, pl)) c++;
			}
			return a;
		}
	}
}
