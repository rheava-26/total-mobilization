using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Config;
using Levy.Common.Dialogue;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// AMBIENT CHATTER. A calm Regular (no target, nothing hostile near, not hurt lately, not seated: seated levies chat from SoldierNPC.Sit.cs) says a line from the idle pools to a comrade standing near,
	/// every so often while a player is around. It talks faster for a short while after a fight ends. Lines come from the dialogue file's own pools (IDLE, IDLE_NIGHT, NAMEDROP, and VICTORY after a fight)
	/// through the ordinary picker, so the per-line repeat cooldown and the temperament filters apply. A squad also shares one clock so forty levies never talk over each other.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Lines said by ambient chatter and seated chat since boot (the harness reads it).</summary>
		internal static int ChatterLines;
		private int _chatterAt = -1, _chatterFightEnd = -100000, _chatterWasFighting;
		/// <summary>Per commander squad: the tick of the last ambient line, so the army reads as a crowd murmuring and not a wall of text.</summary>
		private static readonly System.Collections.Generic.Dictionary<string, int> ChatterSquadLast = new();

		/// <summary>Harness / config switch: -1 follows the config, 0 off, 1 on.</summary>
		internal static int ChatterOverride = -1;
		private static bool ChatterEnabled
		{
			get
			{
				if (ChatterOverride >= 0) return ChatterOverride == 1;
				try { return LevyConfig.Get.AmbientChatter; } catch (Exception) { return true; }
			}
		}

		/// <summary>Called each brain think while the levy is not engaged (<paramref name="fighting"/> false) or is (true), to keep the after-fight clock.</summary>
		private void Chatter_Note(bool fighting, int now)
		{
			if (fighting) { _chatterWasFighting = 1; _chatterFightEnd = now; }
			else if (_chatterWasFighting == 1) { _chatterWasFighting = 0; _chatterFightEnd = now; _chatterAt = Math.Min(_chatterAt < 0 ? int.MaxValue : _chatterAt, now + Main.rand.Next(LevyConstants.ChatterAfterFightMinTicks, LevyConstants.ChatterAfterFightMaxTicks)); }
		}

		/// <summary>The brain's idle branch: maybe say a line to a comrade.</summary>
		private void Chatter_Think(int now, float nearestHostileTiles)
		{
			if (!ChatterEnabled || !IsRegular || OfficerMode || Record == null || LevyUtil.IsClient) return;
			if (_sitMode != 0) return; // a seated or seat-walking levy chats through Sit_Chat
			if (_chatterAt < 0) { _chatterAt = now + Main.rand.Next(LevyConstants.ChatterFirstMinTicks, LevyConstants.ChatterFirstMaxTicks); return; }
			if (now < _chatterAt) return;
			bool afterFight = now - _chatterFightEnd < LevyConstants.ChatterAfterFightWindowTicks;
			int next = afterFight ? Main.rand.Next(LevyConstants.ChatterAfterFightMinTicks, LevyConstants.ChatterAfterFightMaxTicks) : Main.rand.Next(LevyConstants.ChatterMinTicks, LevyConstants.ChatterMaxTicks);
			_chatterAt = now + next;
			if (TargetIndex >= 0 || nearestHostileTiles < LevyConstants.SitWakeEnemyTiles || TicksSinceDamaged < LevyConstants.ChatterQuietTicks || BeingTalkedTo || MutinySystem.IsMutinous(Record) || Shaken) return;
			if (State != SoldierState.Idle && State != SoldierState.Follow) return;
			if (!AnyPlayerNear(LevyConstants.ChatterPlayerTiles)) return;
			string squad = Record.CommanderName ?? "";
			if (ChatterSquadLast.TryGetValue(squad, out int last) && now - last < LevyConstants.ChatterSquadGapTicks) { _chatterAt = now + Main.rand.Next(120, 360); return; }
			SoldierNPC partner = NearbyCalmRegular(LevyConstants.ChatterComradeTiles);
			var ctx = new DialogueContext { Comrade = partner?.SoldierName, Player = Record.CommanderName };
			string line = null;
			if (afterFight) line = DialogueSystem.PickLine(DialogueTrigger.VICTORY, Record, ctx);
			if (line == null && Main.rand.NextFloat() < LevyConstants.ChatterNamedropChance) line = DialogueSystem.PickLine(DialogueTrigger.NAMEDROP, Record, ctx);
			line ??= DialogueSystem.PickLine(Main.dayTime ? DialogueTrigger.IDLE : DialogueTrigger.IDLE_NIGHT, Record, ctx) ?? DialogueSystem.PickLine(DialogueTrigger.IDLE, Record, ctx);
			if (string.IsNullOrEmpty(line)) return;
			ChatterSquadLast[squad] = now; ChatterLines++;
			DialogueSystem.FloatingText(this, line, DialogueTrigger.IDLE);
			if (partner != null) DialogueSystem.Emote(partner, SitEmotes[Main.rand.Next(SitEmotes.Length)]);
		}

		/// <summary>The nearest other Regular within <paramref name="tiles"/> that is calm (no target), or null.</summary>
		private SoldierNPC NearbyCalmRegular(float tiles)
		{
			SoldierNPC best = null; float bd = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.whoAmI == NPC.whoAmI || n.ModNPC is not SoldierNPC s || !s.IsRegular || s.TargetIndex >= 0 || s.Record == null) continue;
				float d = Vector2.Distance(n.Center, NPC.Center) / 16f;
				if (d < bd && d <= tiles) { bd = d; best = s; }
			}
			return best;
		}
	}
}
