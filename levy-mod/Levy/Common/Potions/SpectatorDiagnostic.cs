using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Common.Config;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Potions
{
	/// <summary>
	/// Spectator diagnostic. Off by default and free while off (the hot paths test one static bool). `/levyspec` prints one report; `/levyspec on` logs the same report about once a second until `/levyspec off`.
	/// Each report goes to the client log (every field) and a one-line summary per boss to chat. It answers, for every active boss and boss part: who it targets (the spectator, a levy through slot 255, a levy by NPC index, or a player),
	/// what the allowlist, the boss gate and the downed check say and why, the config and buff state, which levy bodies are live candidates and why the rest are not, and whether Levy's search hooks, its PreAI and Calamity's own targeting call
	/// ran for that NPC in the last second. The Calamity count comes from the IL hook in <see cref="CalamityTargetingCompat"/>; that call does not pass through <c>NPC.TargetClosest</c>.
	/// </summary>
	public class SpectatorDiagnostic : ModSystem
	{
		private static bool _on;
		private static readonly int[] _detour = new int[Main.maxNPCs + 1], _preAi = new int[Main.maxNPCs + 1], _hidden = new int[Main.maxNPCs + 1], _calTarget = new int[Main.maxNPCs + 1], _compete = new int[Main.maxNPCs + 1];
		private static readonly string[] _detourKind = new string[Main.maxNPCs + 1], _competeWhy = new string[Main.maxNPCs + 1];
		private static readonly bool[] _competeHit = new bool[Main.maxNPCs + 1];

		// ---- notes called from the hooks (only when EnemyTargeting.DiagOn) ----
		internal static void NoteDetour(NPC n, string kind) { if (Ok(n)) { _detour[n.whoAmI]++; _detourKind[n.whoAmI] = kind; } }
		internal static void NoteCompete(NPC n, bool hit, string why) { if (Ok(n)) { _compete[n.whoAmI]++; _competeHit[n.whoAmI] = hit; _competeWhy[n.whoAmI] = why; } }
		internal static void NotePreAI(NPC n) { if (Ok(n)) _preAi[n.whoAmI]++; }
		internal static void NoteHidden(NPC n) { if (Ok(n)) _hidden[n.whoAmI]++; }
		internal static void NoteCalamityTargeting(NPC n) { if (EnemyTargeting.DiagOn && Ok(n)) _calTarget[n.whoAmI]++; }
		private static bool Ok(NPC n) => n != null && n.whoAmI >= 0 && n.whoAmI < Main.maxNPCs;

		public static bool On => _on;

		private static void ClearCounts()
		{
			Array.Clear(_detour); Array.Clear(_preAi); Array.Clear(_hidden); Array.Clear(_calTarget); Array.Clear(_compete);
		}

		public static void Set(bool on)
		{
			_on = on; EnemyTargeting.DiagOn = on;
			if (on) ClearCounts();
		}

		public override void Unload() { _on = false; EnemyTargeting.DiagOn = false; }
		public override void OnWorldUnload() { if (_on) Set(false); }

		public override void PostUpdateNPCs()
		{
			if (!_on || Main.GameUpdateCount % 60 != 0) return;
			try { Report(chat: false); } catch (Exception e) { Levy.Instance?.Logger.Warn("levyspec: report failed: " + e); _on = false; EnemyTargeting.DiagOn = false; }
			ClearCounts();
		}

		// ---- report ----
		private static bool IsBossOrPart(NPC n)
		{
			if (n.boss || NPCID.Sets.ShouldBeCountedAsBoss[n.type]) return true;
			if (n.realLife >= 0 && n.realLife < Main.maxNPCs && Main.npc[n.realLife].active && Main.npc[n.realLife].boss) return true;
			foreach (int t in LevyConstants.Targeting.BossPartTypes) if (t == n.type) return true;
			return BossRule.IsMainBoss(LevyUtil.RootNpc(n));
		}

		private static string NameOf(NPC n) => n.FullName + " (type " + n.type + (n.ModNPC != null ? ", " + n.ModNPC.FullName : "") + ")";

		private static bool IsDummy(Player p) => global::Levy.Common.Owner.SoldierOwner.IsDummy(p);

		private static string TargetText(NPC n)
		{
			int t = n.target;
			if (t < 0) return "none (" + t + ")";
			if (t < Main.maxPlayers)
			{
				Player p = Main.player[t];
				string who = SpectatorPlayer.IsSpectator(p) ? "THE SPECTATOR" : (IsDummy(p) ? "a levy dummy" : "a player");
				return $"player slot {t} = {who} '{p.name}' (myPlayer={Main.myPlayer}, active={p.active}, dead={p.dead}, ghost={p.ghost})";
			}
			if (t == SoldierNPC.DummySlot)
			{
				var data = LevyNpcData.Of(n);
				SoldierNPC s = EnemyTargeting.LiveBody(data.SoldierTarget);
				Player slot = Main.player[SoldierNPC.DummySlot];
				return $"slot 255 (levy swap slot): lock={data.SoldierTarget}" + (s != null ? $" = levy '{s.Record?.Name}'" : " = NO live levy (an empty slot 255: the vanilla null player)") + $", slot255 holds {(slot == null ? "null" : (IsDummy(slot) ? "a levy dummy now" : "the null player"))}";
			}
			if (t >= 300) { int i = t - 300; return $"NPC target index {i}" + (i < Main.maxNPCs && Main.npc[i].active ? " = " + Main.npc[i].FullName : " (inactive)"); }
			return "out-of-range " + t;
		}

		private static string RosterText(NPC boss, StringBuilder lines)
		{
			var roster = RosterSystem.Instance;
			var bodies = EnemyTargeting.Bodies;
			int recs = 0, reserve = 0, dead = 0, nobody = 0, inList = 0;
			if (roster != null)
				foreach (SoldierRecord r in roster.Regulars)
				{
					recs++;
					string why = null;
					if (r.Reserve) { reserve++; why = "in reserve (no body)"; }
					else if (r.Dead) { dead++; why = "dead, respawn timer " + r.RespawnTimer; }
					else if (!r.HasBody) { nobody++; why = "no body in the world (BodyIndex " + r.BodyIndex + ", order " + r.Order + ", stood down " + r.StoodDown + ")"; }
					else
					{
						NPC b = Main.npc[r.BodyIndex];
						SoldierNPC sb = b.ModNPC as SoldierNPC;
						bool listed = false;
						for (int i = 0; i < bodies.Count; i++) if (bodies[i].NPC.whoAmI == b.whoAmI) { listed = true; break; }
						if (listed) { inList++; if (lines != null && inList <= 6) lines.Append($"      candidate '{r.Name}' order={r.Order} dist={Vector2.Distance(boss.Center, b.Center):0}px (no range limit through the swap)\n"); continue; }
						why = "has a body but is not a candidate (alive=" + sb?.IsAlive + ", dummy=" + (sb?.Dummy != null) + ")";
					}
					if (lines != null && why != null) lines.Append($"      excluded '{r.Name}': {why}\n");
				}
			return $"records={recs} candidates={bodies.Count} (regular levies in list {inList}; reserve {reserve}, dead {dead}, no body {nobody}); other candidate bodies (officers/summons/companion) {bodies.Count - inList}";
		}

		private static string CalamityText(NPC n)
		{
			if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return "Calamity not loaded";
			var sb = new StringBuilder();
			try
			{
				Type w = cal.Code.GetType("CalamityMod.World.CalamityWorld");
				if (w != null)
					sb.Append($"rev={w.GetField("revenge", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)} death={w.GetField("death", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)} ");
				if (cal.TryFind("CalamityVanillaAIOverrideNPC", out GlobalNPC g) && n.TryGetGlobalNPC(g, out GlobalNPC inst))
				{
					object ov = inst.GetType().GetField("AIOverride", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
					sb.Append("AIOverride=" + (ov == null ? "none (vanilla AI)" : ov.GetType().FullName + " ACTIVE (replaces the vanilla AI; its own targeting does not use NPC.TargetClosest)"));
				}
				else sb.Append("override global not found");
				int ci = Array.IndexOf(ModLoader.Mods, cal), li = Array.IndexOf(ModLoader.Mods, Levy.Instance);
				sb.Append($"; mod load order: Calamity #{ci}, Levy #{li} (a lower number runs its PreAI first{(ci < li ? ": Calamity's AI override runs BEFORE Levy's slot-255 swap" : "")})");
			}
			catch (Exception e) { sb.Append("(reflection failed: " + e.Message + ")"); }
			return sb.ToString();
		}

		private static string ShortTarget(NPC n)
		{
			int t = n.target;
			if (t == SoldierNPC.DummySlot) return EnemyTargeting.LiveBody(LevyNpcData.Of(n).SoldierTarget) != null ? "LEVY" : "slot255/none";
			if (t >= 0 && t < Main.maxPlayers) return SpectatorPlayer.IsSpectator(Main.player[t]) ? "SPECTATOR" : "player" + t;
			return t.ToString();
		}

		public static void Report(bool chat)
		{
			LevyConfig cfg = LevyConfig.Get;
			var log = new StringBuilder();
			Player me = Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers ? Main.player[Main.myPlayer] : null;
			SpectatorPlayer.InvalidateCache();
			bool spectate = SpectatorPlayer.AnyActive;
			bool hasBuff = me != null && me.HasBuff<global::Levy.Content.Buffs.SpectatorBuff>();
			bool hiddenNow = me != null && me.TryGetModPlayer(out SpectatorPlayer sp) && sp.Hidden;
			log.Append($"levyspec @tick {Main.GameUpdateCount}: netMode={Main.netMode} myPlayer={Main.myPlayer} local whoAmI={me?.whoAmI} name='{me?.name}' dedServ={Main.dedServ} gameMenu={Main.gameMenu}\n");
			log.Append($"  config: loaded={cfg != null} EnemiesTargetSoldiers={cfg?.EnemiesTargetSoldiers} BossesTargetSoldiers={cfg?.BossesTargetSoldiers}; EnemyTargeting.Enabled={EnemyTargeting.Enabled}\n");
			log.Append($"  spectator: AnyActive={spectate} buff on local player={hasBuff} IsSpectator(local)={SpectatorPlayer.IsSpectator(me)} hidden-now={hiddenNow}\n");
			log.Append($"  swap state: IsSwapped={EnemyTargeting.IsSwapped}, Main.player[255] is {(IsDummy(Main.player[SoldierNPC.DummySlot]) ? "a levy dummy (LEAK)" : "the null player")}; Levy hooks counting={EnemyTargeting.DiagOn}, Calamity CalamityTargeting hook={(CalamityTargetingCompat.Installed ? "installed" : "absent")}, Calamity PreAI hook={(CalamityTargetingCompat.AiHookInstalled ? "installed" : "absent")}\n");
			NPC undowned = EnemyTargeting.FindUndownedBoss();
			log.Append($"  undowned boss alive: {(undowned == null ? "none" : NameOf(undowned))}\n");
			int shown = 0;
			var chatLines = new List<string>();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.townNPC || n.ModNPC is SoldierNPC || !IsBossOrPart(n)) continue;
				shown++;
				bool allow = EnemyTargeting.IsAllowlisted(n);
				bool unlocked = EnemyTargeting.BossAggroUnlocked(n);
				bool downed = BossDownedSystem.IsBossDowned(n);
				string key = BossDownedSystem.BossDownedKey(n);
				var d = LevyNpcData.Of(n);
				string reason;
				if (!EnemyTargeting.Enabled) reason = "Enabled is false (EnemiesTargetSoldiers off and no spectator)";
				else if (!downed) reason = "NOT downed in this world (key " + key + "): the first kill is always solo";
				else if (!unlocked) reason = "downed, but BossesTargetSoldiers is off and nobody is spectating";
				else if (BossPersistenceGlobalNPC.IsDaytimeExcluded(n)) reason = "daytime-excluded (Skeletron / Prime)";
				else if (!allow) reason = "boss gate open but AI style " + n.aiStyle + " is swap-incompatible or the type is excluded";
				else reason = "allowlisted";
				log.Append($"  [{n.whoAmI}] {NameOf(n)} boss={n.boss} aiStyle={n.aiStyle} life={n.life}/{n.lifeMax} realLife={n.realLife}\n");
				log.Append($"      target: {TargetText(n)}; soldierLock={d.SoldierTarget}\n");
				log.Append($"      IsAllowlisted={allow} BossAggroUnlocked={unlocked} downed={downed} -> {reason}\n");
				log.Append($"      hides spectator from it={EnemyTargeting.HidesSpectatorFrom(n)}; hidden-window used last second={_hidden[i]}x\n");
				log.Append($"      last second: Levy search detour {_detour[i]}x{(_detour[i] > 0 ? " (" + _detourKind[i] + ")" : "")}, Compete {_compete[i]}x{(_compete[i] > 0 ? " last=" + (_competeHit[i] ? "levy" : "player") + " (" + _competeWhy[i] + ")" : "")}, Levy PreAI {_preAi[i]}x, Calamity CalamityTargeting {_calTarget[i]}x{(CalamityTargetingCompat.Installed ? "" : " (hook absent: unknown)")}\n");
				log.Append($"      calamity: {CalamityText(n)}\n");
				log.Append("      levies: " + RosterText(n, log) + "\n");
				chatLines.Add($"{n.FullName}#{i}: target={ShortTarget(n)} allow={allow} unlocked={unlocked} downed={downed} detour={_detour[i]} calTarget={_calTarget[i]}");
			}
			if (shown == 0) log.Append("  no boss or boss part is active\n");
			Levy.Instance?.Logger.Info(log.ToString().TrimEnd());
			if (chat && !Main.dedServ)
			{
				Main.NewText($"levyspec: netMode {Main.netMode}, spectator {spectate}, config enemies/bosses {cfg?.EnemiesTargetSoldiers}/{cfg?.BossesTargetSoldiers}, {EnemyTargeting.Bodies.Count} levy bodies. Full report in client.log.", 180, 220, 255);
				foreach (string l in chatLines) Main.NewText("  " + l, 200, 200, 200);
				if (shown == 0) Main.NewText("  no boss active", 200, 200, 200);
			}
		}
	}

	/// <summary>/levyspec: spectator diagnostic. No argument prints one report; "on" logs one about once a second; "off" stops.</summary>
	public class LevySpecCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "levyspec";
		public override string Description => "Spectator targeting report. /levyspec prints one; /levyspec on or off logs one every second to client.log.";
		public override void Action(CommandCaller caller, string input, string[] args)
		{
			string a = args.Length > 0 ? args[0].ToLowerInvariant() : "";
			if (a == "on") { SpectatorDiagnostic.Set(true); caller.Reply("levyspec ON: logging every second to client.log. /levyspec off to stop.", Color.LightSkyBlue); return; }
			if (a == "off") { SpectatorDiagnostic.Set(false); caller.Reply("levyspec OFF.", Color.LightSkyBlue); return; }
			SpectatorDiagnostic.Report(chat: true); // the per-second call counts are only gathered while on
		}
	}
}
