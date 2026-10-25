using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common.Dialogue;
using Levy.Common.Util;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// What a commissioned officer says when the player right-clicks him: the town NPC's chat window, with a line that fits the moment (a fight on, a boss near, the
	/// officer wounded, a boss just down). Nothing fits, or the roll goes the other way: null, and vanilla's own line in the officer's town voice stands. The lines live in
	/// Dialogue/en-US.txt (OFFICER_CHAT_*). Client only: the chat window is the local player's.
	/// </summary>
	public static class OfficerChat
	{
		/// <summary>A filled contextual line for this officer's town NPC, or null when the moment has no context (or the roll skips it).</summary>
		public static string ContextLine(NPC town)
		{
			if (Main.dedServ || town == null) return null;
			SoldierNPC avatar = OfficerSystem.Instance.AvatarOf(town);
			if (avatar?.Record == null) return null; // off duty or between avatars: a plain civilian's chat

			Vector2 at = avatar.NPC.Center;
			NPC boss = NearestBoss(at, ChatBossTiles * LevyConstants.TilePx);
			DialogueTrigger trigger;
			var ctx = new DialogueContext { Player = avatar.Record.CommanderName };
			if (boss != null) { trigger = DialogueTrigger.OFFICER_CHAT_BOSS; ctx.Boss = boss.GivenOrTypeName; }
			else if (AnyHostileNear(at, ChatFightTiles * LevyConstants.TilePx)) trigger = DialogueTrigger.OFFICER_CHAT_FIGHT;
			else if (avatar.NPC.life < avatar.NPC.lifeMax * ChatHurtFrac) trigger = DialogueTrigger.OFFICER_CHAT_HURT;
			else if (OfficerSystem.Instance.BossFellRecently(ChatVictoryTicks)) trigger = DialogueTrigger.OFFICER_CHAT_VICTORY;
			else return null;

			if (Main.rand.NextFloat() >= ChatContextChance) return null;
			return DialogueSystem.PickLine(trigger, avatar.Record, ctx);
		}

		private static NPC NearestBoss(Vector2 at, float rangePx)
		{
			NPC best = null; float bestD = rangePx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !n.boss) continue;
				float d = Vector2.Distance(at, n.Center);
				if (d < bestD) { bestD = d; best = n; }
			}
			return best;
		}

		private static bool AnyHostileNear(Vector2 at, float rangePx)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && LevyUtil.IsHostileTarget(n) && Vector2.Distance(at, n.Center) < rangePx) return true;
			}
			return false;
		}
	}
}
