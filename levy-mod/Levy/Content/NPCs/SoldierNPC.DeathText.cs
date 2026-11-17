using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// The death text a levy would get if it were a player. The game builds a player's custom death text in the ModPlayer.PreKill hooks (Calamity names the debuff a player died of, "burned in brimstone fire", "died of the plague";
	/// other mods name their bosses and items). A scratch Player carrying the body's debuffs runs those same hooks with the reason a player's death would carry (damage over time: the vanilla "other" reason 8; a hit: the killing NPC),
	/// and a text another mod put into the reason is the levy's custom death text. Server and single player; null when nothing custom was produced.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>The body died of damage over time (set by <see cref="DebuffDot"/> just before the killing tick).</summary>
		internal bool DiedOfDot;
		private static Player _deathScratch;
		internal static long DeathTexts;
		internal static readonly bool DeathDebug = Environment.GetEnvironmentVariable("LEVY_DEATHDBG") == "1";

		/// <summary>True for the scratch players the damage-over-time step and the death text run on (their lethal tick must never be a local-player death).</summary>
		internal static bool IsScratch(Player p) => p != null && (ReferenceEquals(p, _dotScratch) || ReferenceEquals(p, _deathScratch));
		private static PlayerDeathReason _scratchReason; private static double _scratchDamage; private static int _scratchDir;
		/// <summary>The scratch player was told to die: vanilla's own reason for the tick (the "other" reason 8 for most damage over time, 9 for poison, 10 for electrified) is kept for <see cref="CustomDeathText"/>.</summary>
		internal static void NoteScratchKill(PlayerDeathReason reason, double dmg, int dir) { _scratchReason = reason; _scratchDamage = dmg; _scratchDir = dir; }

		/// <summary>A reason a mod wrote its own text into (PlayerDeathReason.ByCustomReason): it names no NPC, projectile, player or vanilla "other" cause. (The reason's own custom-text property reads empty for a networked text, so the absence of every source is the test.)</summary>
		internal static bool IsCustomReason(PlayerDeathReason r) => r != null && r.SourceNPCIndex < 0 && r.SourceOtherIndex < 0 && r.SourcePlayerIndex < 0 && r.SourceProjectileLocalIndex < 0;

		/// <summary>The custom death text for this body's death, with its name filled in; null when it has none.</summary>
		internal string CustomDeathText()
		{
			if (Record == null || LevyUtil.IsClient) return null;
			PlayerDeathReason reason;
			bool dot = DiedOfDot;
			NPC killer = !dot && LastAttackerIndex >= 0 && LastAttackerIndex < Main.maxNPCs && Main.npc[LastAttackerIndex].active ? Main.npc[LastAttackerIndex] : null;
			double damage = dot ? 10.0 : 100.0; int dir = 0;
			if (dot) { reason = _scratchReason ?? PlayerDeathReason.ByOther(8); if (_scratchReason != null) { damage = _scratchDamage; dir = _scratchDir; } _scratchReason = null; }
			else if (killer != null && killer.type >= Terraria.ID.NPCID.Count) reason = PlayerDeathReason.ByNPC(killer.whoAmI); // only another mod's creature can name its own death text; vanilla's is the themed line
			else return null;
			Player s = _deathScratch ??= new Player { whoAmI = Main.maxPlayers, active = true, width = 20, height = 42 };
			bool gate = SaveGate.Enter();
			Player prev = Main.player[Main.maxPlayers]; int prevMy = Main.myPlayer;
			var was = new bool[Main.maxNPCs];
			for (int i = 0; i < was.Length; i++) was[i] = Main.npc[i].active;
			string text = null;
			try
			{
				Main.player[Main.maxPlayers] = s; Main.myPlayer = Main.maxPlayers;
				s.ResetEffects();
				Array.Clear(s.buffType); Array.Clear(s.buffTime);
				int k = 0;
				for (int i = 0; i < NPC.maxBuffs && k < s.buffType.Length; i++) if (NPC.buffType[i] > 0 && NPC.buffTime[i] > 0 && Main.debuff[NPC.buffType[i]]) { s.buffType[k] = NPC.buffType[i]; s.buffTime[k] = NPC.buffTime[i]; k++; }
				s.name = Record.Name; s.statLifeMax = s.statLifeMax2 = Math.Max(1, NPC.lifeMax); s.statLife = 0; s.dead = false;
				s.position = NPC.position; s.width = NPC.width; s.height = NPC.height;
				if (k > 0) s.UpdateBuffs(Main.maxPlayers); // the mods' debuff flags (ModBuff.Update) are what their PreKill reads
				bool playSound = false, genGore = false;
				bool pk = PlayerLoader.PreKill(s, damage, dir, false, ref playSound, ref genGore, ref reason);
				if (DeathDebug) Levy.Instance?.Logger.Info($"[DeathText] debuffs {k} dot {dot} damage {damage} dir {dir} other {reason.SourceOtherIndex} custom {IsCustomReason(reason)} prekill {pk} text '{reason.GetDeathText(Record.Name)}'");
				if (IsCustomReason(reason)) { text = reason.GetDeathText(Record.Name).ToString(); DeathTexts++; }
			}
			catch (Exception e) { if (GearUpkeep.WarnOnce(e)) Levy.Instance?.Logger.Warn("Levy: the custom death text step threw (once per kind): " + e); text = null; }
			finally
			{
				Main.player[Main.maxPlayers] = prev; Main.myPlayer = prevMy; SaveGate.Exit(gate);
				// a ModPlayer's death hook may despawn things of its own (Calamity's Holy Inferno clears Providence): a levy's death text must not change the world
				for (int i = 0; i < was.Length; i++) if (was[i] && !Main.npc[i].active && Main.npc[i].life > 0) Main.npc[i].active = true;
			}
			return string.IsNullOrWhiteSpace(text) ? null : text;
		}
	}
}
