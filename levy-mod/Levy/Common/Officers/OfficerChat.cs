using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common.Dialogue;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// What a commissioned officer says when the player right-clicks him: the town NPC's chat window, with a line that fits the moment. Urgent moments come first (a boss
	/// near, a fight on, the officer wounded), then a boss just down, then how the officer is posted (marching with the commander, or holding a station), then plain
	/// at-ease talk. Each officer has his own voice: a pool per situation named OFFICER_&lt;officer Key&gt;_&lt;STATE&gt; (OFFICER_Nurse_FIGHT); an officer with no lines for a
	/// state speaks the generic OFFICER_CHAT_&lt;STATE&gt; pool instead. The roll goes the other way, or nothing fits: null, and vanilla's own line in the officer's town voice
	/// stands. The lines live in Dialogue/en-US.txt. Client only: the chat window is the local player's.
	/// </summary>
	public static class OfficerChat
	{
		public const string Boss = "BOSS", Fight = "FIGHT", Hurt = "HURT", Victory = "VICTORY", March = "MARCH", Hold = "HOLD", Idle = "IDLE";
		private const string GenericPrefix = "OFFICER_CHAT_";

		/// <summary>A filled line for this officer's town NPC, or null when the roll skips it (vanilla's voice then stands).</summary>
		public static string ContextLine(NPC town)
		{
			if (Main.dedServ || town == null) return null;
			SoldierNPC avatar = OfficerSystem.Instance.AvatarOf(town);
			if (avatar?.Record == null) return null; // off duty or between avatars: a plain civilian's chat
			OfficerDef def = OfficerRegistry.DefFor(town);
			if (def == null) return null;

			Vector2 at = avatar.NPC.Center;
			NPC boss = NearestBoss(at, ChatBossTiles * LevyConstants.TilePx);
			var ctx = new DialogueContext { Player = avatar.Record.CommanderName };
			string state;
			bool urgent = true;
			if (boss != null) { state = Boss; ctx.Boss = boss.GivenOrTypeName; }
			else if (AnyHostileNear(at, ChatFightTiles * LevyConstants.TilePx)) state = Fight;
			else if (avatar.NPC.life < avatar.NPC.lifeMax * ChatHurtFrac) state = Hurt;
			else
			{
				urgent = false;
				if (OfficerSystem.Instance.BossFellRecently(ChatVictoryTicks)) state = Victory;
				else
				{
					// Quiet. Mostly he talks about where he stands (marching with the commander, or holding a post); some of the time it is just small talk.
					SoldierOrder order = avatar.Record.Order;
					string posted = order == SoldierOrder.Follow ? March
						: order == SoldierOrder.ReturnToBarracks ? null
						: Hold;
					state = posted != null && Main.rand.NextFloat() < ChatPostureShare ? posted : Idle;
				}
			}

			if (Main.rand.NextFloat() >= (urgent ? ChatUrgentChance : ChatContextChance)) return null;
			return PickFor(def.Key, state, avatar.Record, ctx);
		}

		/// <summary>The pool key an officer speaks a state from: his own OFFICER_&lt;Key&gt;_&lt;STATE&gt; when that pool has lines, else the generic OFFICER_CHAT_&lt;STATE&gt;.</summary>
		internal static string ResolvePoolKey(string officerKey, string state)
		{
			string own = OwnPoolKey(officerKey, state);
			return own != null && DialogueTable.Get(own) != null ? own : GenericPrefix + state;
		}

		internal static string OwnPoolKey(string officerKey, string state)
		{
			return string.IsNullOrEmpty(officerKey) || string.IsNullOrEmpty(state) ? null : DialogueTable.OfficerPrefix + officerKey + "_" + state;
		}

		/// <summary>His own voice first (fresh lines, then a repeat rather than a stranger's voice), then the generic pool for the state. Null when none of it fits.</summary>
		private static string PickFor(string officerKey, string state, SoldierRecord rec, DialogueContext ctx)
		{
			string own = OwnPoolKey(officerKey, state);
			string generic = GenericPrefix + state;
			string line = null;
			if (own != null && DialogueTable.Get(own) != null)
				line = DialogueSystem.PickLine(own, rec, ctx) ?? DialogueSystem.PickLine(own, rec, ctx, ignoreRepeat: true);
			return line ?? DialogueSystem.PickLine(generic, rec, ctx) ?? DialogueSystem.PickLine(generic, rec, ctx, ignoreRepeat: true);
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
