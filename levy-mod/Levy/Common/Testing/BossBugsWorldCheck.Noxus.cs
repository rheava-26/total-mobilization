using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Common.Globals;
using Levy.Common.Recording;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WRATH OF THE GODS (NoxusBoss) RECONNAISSANCE part of the boss-bugs check (LEVY_BOSSBUGS_PART=noxus): runs only when the mod is loaded. Logged as "[BossBugs] OBSERVED noxus ..." lines
	/// plus a few liveness checks (it spawns, nothing throws). It reports and does not fix: for each of the mod's three fight NPCs (the Avatar's rift, the Avatar of Emptiness, Nameless Deity) it records
	/// whether Levy's systems see it (BossRule, the recorder's fight-boss test, the targeting slot), whether levies lock onto it, whether it can be hurt, what it does to the army and what its AI throws.
	/// Only code identifiers of the mod are named. A cutscene or subworld boss is spawned straight in the main world by NPC.NewNPC (its own item and scene need a player and a subworld a headless server cannot enter).
	/// </summary>
	public partial class BossBugsWorldCheck
	{
		private static void NoxusPart()
		{
			if (!ModLoader.TryGetMod("NoxusBoss", out Mod nox)) { Note("NoxusBoss is not loaded: the noxus part is skipped"); return; }
			Arena(FloorY, Half);
			Note("noxus: mod " + nox.Version + ", subworld library " + (ModLoader.HasMod("SubworldLibrary") ? "loaded" : "NOT loaded") + ", Calamity " + (ModLoader.HasMod("CalamityMod") ? "loaded" : "not loaded"));
			NoxusSubworldNote(nox);
			NoxusBuffProbe();
			string only = Environment.GetEnvironmentVariable("LEVY_NOXUS_PART") ?? "all";
			foreach (string cls in new[] { "AvatarRift", "AvatarOfEmptiness", "NamelessDeityBoss" })
			{
				if (!Part(only, cls)) continue;
				try { NoxusFight(nox, cls); } catch (Exception e) { Check("noxus " + cls + " driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
				finally { Cleanup(); NoxusReset(nox); }
			}
		}

		/// <summary>The recorder's buff-name lookup over every loaded buff type: which ones throw (the FightRecorder.WriteBuffs IndexOutOfRange seen with the Wrath of the Gods set loaded).</summary>
		private static void NoxusBuffProbe()
		{
			int n = BuffLoader.BuffCount, bad = 0; var rows = new List<string>();
			for (int t = 1; t < n; t++)
			{
				try { string name = Lang.GetBuffName(t); if (name == null) throw new NullReferenceException("null name"); }
				catch (Exception e) { bad++; if (rows.Count < 12) rows.Add((t < BuffID.Count ? "vanilla#" + t : (BuffLoader.GetBuff(t) is ModBuff mb ? mb.Mod.Name + "/" + mb.Name : "#" + t)) + ":" + e.GetType().Name); }
			}
			Note($"noxus: Lang.GetBuffName over {n - 1} buff types: {bad} throw [{string.Join(",", rows)}]; BuffLoader.BuffCount {n}; Player.MaxBuffs {Player.MaxBuffs}, buffType length {new Player().buffType.Length}");
			Check("the recorder's buff-name lookup works for every buff type", bad == 0);
		}

		private static void NoxusSubworldNote(Mod nox)
		{
			try
			{
				Type garden = nox.Code.GetType("NoxusBoss.Core.World.Subworlds.EternalGardenUpdateSystem");
				var p = garden?.GetProperty("WasInSubworldLastUpdateFrame", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				Note("noxus: in the Eternal Garden subworld right now: " + (p?.GetValue(null) ?? "unknown") + " (the headless check never enters it: levies are main-world NPCs and the roster stays in the main save)");
			}
			catch (Exception e) { Note("noxus: subworld probe threw " + e.GetType().Name); }
		}

		/// <summary>Puts the mod's static fight state back (a boss that died or was removed mid-scene).</summary>
		private static void NoxusReset(Mod nox)
		{
			try
			{
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC?.Mod == nox) n.active = false; }
				for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && p.ModProjectile?.Mod == nox) p.active = false; }
			}
			catch (Exception) { }
		}

		private static void NoxusFight(Mod nox, string cls)
		{
			if (!nox.TryFind(cls, out ModNPC mn)) { Check("noxus " + cls + " exists", false); return; }
			int type = mn.Type; string label = "noxus " + cls;
			SetBossesTarget(true);
			PlaceCommander(-120);
			Main.GameMode = GameModeID.Master;
			var levies = new List<SoldierNPC>();
			Item gun = new Item(); gun.SetDefaults(ItemID.Minishark);
			Item ammo = new Item(); ammo.SetDefaults(ItemID.MusketBall); ammo.stack = 3996;
			for (int k = 0; k < 8; k++)
			{
				var rec = new SoldierRecord
				{
					Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. BossBugs",
					CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
				};
				rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = SoldierOrder.Follow;
				Item w = gun.Clone(); Item a = ammo.Clone();
				rec.Gear.Weapon = w; rec.Gear.Ammo = a; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); rec.LoadoutLocked = true;
				RosterSystem.Instance.Regulars.Add(rec);
				int body = RosterSystem.Instance.SpawnBody(rec, new Vector2((Cx - 120 + (k - 4) * 3) * 16f, _floorY * 16f));
				if (body < 0 || !rec.HasBody) continue;
				Main.npc[body].lifeMax = Main.npc[body].life = 4000; rec.CurrentHp = 4000;
				levies.Add(Main.npc[body].ModNPC as SoldierNPC);
			}
			Check(label + ": eight armed levies spawn", levies.Count == 8);
			Step(2);

			var thrown = new Dictionary<string, int>(); int aiCalls = 0, lockedAi = 0, invalid = 0, badIndex = 0;
			On_NPC.hook_UpdateNPC det = (On_NPC.orig_UpdateNPC orig, NPC self, int i) =>
			{
				try { orig(self, i); }
				catch (Exception e) { if (self.type == type) { string k = e.GetType().Name + " " + (e.StackTrace ?? "").Split('\n')[0].Trim(); thrown[k] = thrown.GetValueOrDefault(k) + 1; } throw; }
			};
			On_NPC.UpdateNPC += det;
			try
			{
				_watched.Add(type);
				EnemyTargeting.AiProbe = n =>
				{
					if (n.type != type) return;
					aiCalls++;
					if (!Locked(n)) return;
					lockedAi++;
					if (!n.HasValidTarget) invalid++;
					if (n.target < 0 || n.target >= Main.maxPlayers) badIndex++;
				};
				NPC boss = SpawnNpc(type, new Vector2((Cx + 40) * 16f, (_floorY - 30) * 16f));
				Check(label + " spawns", boss != null && boss.type == type);
				if (boss == null) return;
				int lifeMax0 = boss.lifeMax;
				bool isMain = BossRule.IsMainBoss(boss); BossRule.TryGetRoot(boss, out _, out int rootType);
				Note($"{label}: type {type}, lifeMax {lifeMax0}, defense {boss.defense}, damage {boss.damage}, boss={boss.boss}, friendly={boss.friendly}, dontTakeDamage={boss.dontTakeDamage}, immortal={boss.immortal}, hide={boss.hide}, noTileCollide={boss.noTileCollide}, noGravity={boss.noGravity}, chaseable={boss.chaseable}, aiStyle={boss.aiStyle}");
				Note($"{label}: Levy sees it: BossRule.IsMainBoss={isMain} rootType={rootType} inLiveBosses(after 1 tick)=?, FightRecorder.IsFightBoss={FightRecorder.IsFightBoss(boss)}, ShouldBeCountedAsBoss={NPCID.Sets.ShouldBeCountedAsBoss[type]}, UsesNewTargetting={NPCID.Sets.UsesNewTargetting[type]}, native={EnemyTargeting.UsesNativeTargeting(boss)}, flyerSlot={EnemyTargeting.UsesFlyerSlot(boss)}, moddedNativeUnproven={EnemyTargeting.ModdedNativeUnproven(type)}, bossHeadTexture={NPCID.Sets.BossHeadTextures[type]}");
				boss.timeLeft = 99999;
				int cmdHits = 0, alive = 0, lockedTicks = 0, dontTake = 0, minLife = boss.life, liveBossTicks = 0, untargetable = 0; bool gone = false; int firstLockTick = -1, tick = 0;
				bool recOnAtStart = FightRecorder.On, recSeen = false;
				_perTick = () =>
				{
					tick++;
					// the stand-in commander is not what is measured (a test server has no player file, so a death here ends in WorldGen.SaveAndQuit and a crash): keep it alive and count the hits it takes
					Player cmd = Main.player[0]; cmd.statLifeMax = cmd.statLifeMax2 = 999999; if (cmd.statLife < 900000) { cmdHits++; } cmd.statLife = cmd.statLifeMax2; cmd.dead = false;
					if (!boss.active || boss.type != type) { gone = true; return; }
					alive++; boss.timeLeft = Math.Max(boss.timeLeft, 3000);
					if (Locked(boss)) { lockedTicks++; if (firstLockTick < 0) firstLockTick = tick; }
					if (boss.dontTakeDamage || boss.immortal) dontTake++;
					if (BossRule.LiveBosses.Contains(boss)) liveBossTicks++;
					if (!boss.CanBeChasedBy()) untargetable++;
					minLife = Math.Min(minLife, boss.life);
					if (FightRecorder.BossSeen) recSeen = true;
				};
				const int N = 1200;
				Step(N);
				_perTick = null;
				long lost = levies.Sum(l => l.NPC.active ? (long)(l.NPC.lifeMax - l.NPC.life) : l.NPC.lifeMax);
				int levAlive = levies.Count(l => l.NPC.active);
				Note($"{label}: after {N} ticks: boss alive {alive} ticks (gone={gone}), life {(boss.active ? boss.life : -1)}/{lifeMax0} (min {minLife}), damage taken {lifeMax0 - minLife}, immune ticks {dontTake}, CanBeChasedBy false ticks {untargetable}, locked on a levy {lockedTicks} ticks (first at {firstLockTick}), in BossRule.LiveBosses {liveBossTicks} ticks");
				Note($"{label}: army: {levAlive} of {levies.Count} levies alive, life lost {lost}; AI calls {aiCalls}, locked {lockedAi}, locked with no valid target {invalid}, out-of-range target index {badIndex}, AI errors [{string.Join("; ", thrown.Select(kv => kv.Value + "x " + kv.Key))}]");
				Note($"{label}: the commander took damage on {cmdHits} ticks"); Note($"{label}: recorder: On at start {recOnAtStart}, BossSeen during the fight {recSeen}, FightRecorder.On now {FightRecorder.On}");
				Check(label + ": its AI throws nothing", thrown.Count == 0);
				Check(label + ": the boss is still present at the end (or died fighting)", !gone || minLife <= 0);
				boss.active = false;
			}
			finally { On_NPC.UpdateNPC -= det; _watched.Remove(type); EnemyTargeting.AiProbe = null; }
		}
	}
}
