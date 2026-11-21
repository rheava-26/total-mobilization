using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Budget
{
	/// <summary>
	/// THE ARMY MAKES ROOM FOR A BOSS. Terraria has 200 NPC slots, and a segmented boss spawns every segment in one pass of its own AI: Calamity's Devourer of Gods needs 102 (head, 100 bodies, tail), the Aquatic Scourge 81,
	/// Storm Weaver 61. When a spawn finds no free slot <c>NPC.NewNPC</c> returns 200, the dummy 201st slot, and the boss's own AI indexes it (DoG: <c>Main.npc[segment].ModNPC&lt;DevourerofGodsBody&gt;().SegmentIndex</c>,
	/// a NullReferenceException on the first AI tick) so the boss is gone the moment it wakes. The army keeps only <see cref="CrowdBudget.ReservedNpc"/> (40) NPC slots free, and its drones fill everything above that,
	/// so a 40-levy army with its garrison drones leaves far fewer than 102 (Eve, 2026-11-20: "Can't summon DoG with 40 levies").
	/// A failed spawn of a BOSS (or of anything a boss spawns) now frees a slot and tries again, one slot at a time, taking from whatever matters least and never from the fight:
	///  1. critters; 2. ordinary hostile spawns (not a boss or a boss's add, never during an event), farthest first; 3. a companion mod's garrison (<see cref="SlotReleasers"/>: Mechanization's garrison drones);
	///  4. the army's own summoned units and conjured creatures; 5. a companion mod's machines packed into their saved records, farthest from the spawn first (<see cref="MachinePackers"/>: Mechanization's player-built drones and turrets,
	///     which keep position, health, facing, weapon and battery and come back when the boss is gone); 6. as the last resort the levy bodies farthest from the spawn stand off the field (their records untouched, <see cref="Squeeze"/> keeps them off until the boss is gone).
	/// Town NPCs, officers, anything of a boss, and the levies the render cap leaves room for are never taken. While the boss lives the drones and machines keep <see cref="ExtraReserve"/> more slots free.
	/// </summary>
	public static class BossSlots
	{
		/// <summary>LEVY_BOSSSLOTS=0 turns the room-making off (A/B switch for the lab: the boss spawn fails as it did).</summary>
		internal static readonly bool On = Environment.GetEnvironmentVariable("LEVY_BOSSSLOTS") != "0";

		/// <summary>The lowest slot a retry may use: 1. LEVY_BOSSSLOTS_MINSLOT=0 (lab) lets it use slot 0 again, to show what a worm chain does with a segment there.</summary>
		internal static readonly int MinSlot = int.TryParse(Environment.GetEnvironmentVariable("LEVY_BOSSSLOTS_MINSLOT"), out int ms) ? Math.Max(0, ms) : 1;

		/// <summary>A companion mod's way to give slots back: asked for n slots (and the lowest slot it may take: 1, never slot 0), it frees up to n now and returns how many it freed (Mechanization stands its garrison drones down, oldest first).</summary>
		public static readonly List<Func<int, int, int>> SlotReleasers = new();

		/// <summary>A companion mod's way to make room without losing anything: asked with the failed spawn's spot and the lowest slot it may take, it packs ONE machine (the farthest from the spot) into its saved record
		/// and returns how many slots it freed (0 when it has none to pack). The machine must stay packed while a boss lives and come back after (Mechanization: <c>MachineDormancy.PackFarthestForBoss</c>).</summary>
		public static readonly List<Func<Vector2, int, int>> MachinePackers = new();

		/// <summary>Extra NPC slots the drones leave free while a boss that needed the room is alive.</summary>
		public static int ExtraReserveSlots { get { try { return LevyConfig.Get?.BossRoomSlots ?? 30; } catch (Exception) { return 30; } } }
		public static int ExtraReserve;
		/// <summary>Levy bodies held off the field for the boss's segments (subtracted from the render cap until the boss is gone).</summary>
		public static int Squeeze;

		/// <summary>Lab counters: spawns that found no slot, slots freed in all, by kind, and failures nothing could answer.</summary>
		public static int Failed, Freed, FreedCritters, FreedHostiles, FreedReleased, FreedSummons, FreedPacked, FreedLevies, Unanswered;
		private static bool _busy;
		private static int _loggedFor = -1;

		private static bool _told;
		internal static void Reset() { _bossKnown = false; _told = false; ExtraReserve = 0; Squeeze = 0; _busy = false; _loggedFor = -1; }

		/// <summary>True when this spawn is a boss, or something a boss spawns (its segments, its adds): the spawns that may take a slot from the army's surroundings.</summary>
		internal static bool NeedsRoom(IEntitySource source, int type)
		{
			if (source is EntitySource_BossSpawn) return true;
			if (type > 0 && type < NPCID.Sets.ShouldBeCountedAsBoss.Length && NPCID.Sets.ShouldBeCountedAsBoss[type]) return true;
			if (type >= NPCID.Count && NPCLoader.GetNPC(type) is ModNPC mn && mn.NPC.boss) return true;
			if (type > 0 && type < NPCID.Count && IsVanillaBoss(type)) return true;
			if (source is EntitySource_Parent p && p.Entity is NPC parent && parent.active && !parent.friendly && !parent.townNPC && EnemyTargeting.IsBossClass(parent)) return true;
			return false;
		}

		private static bool IsVanillaBoss(int type)
		{
			// vanilla boss heads that spawn segments or guards from a plain world spawn (the Eater of Worlds, the Destroyer): the ShouldBeCountedAsBoss set covers the rest
			return type == NPCID.EaterofWorldsHead || type == NPCID.TheDestroyer || type == NPCID.WallofFlesh;
		}

		/// <summary>The detour body: the spawn, and while a boss spawn finds no room, one more slot made and one more try.</summary>
		internal static int Spawn(Func<int, int> orig, IEntitySource source, int x, int y, int type, int start)
		{
			int r = orig(start);
			if (r < Main.maxNPCs || _busy || !On || !LevyUtil.IsServerOrSingle || !NeedsRoom(source, type)) return r;
			Failed++;
			_busy = true;
			try
			{
				// NewNPC searches from index Start up, and Calamity's worms pass their own index (a segment can only land above its head): free slots under the head would never be found. The retries search from slot 1 (never 0: Calamity's worm segments read a previous-segment index of 0 as "none" and despawn).
				int freedNow = 0, futile = 0;
				if (start > MinSlot) r = orig(MinSlot);
				for (int guard = 0; guard < 180 && r >= Main.maxNPCs; guard++)
				{
					if (!FreeOne(new Vector2(x, y), MinSlot, out bool levy)) { Unanswered++; break; }
					freedNow++;
					r = orig(MinSlot);
					if (r >= Main.maxNPCs && levy && ++futile >= 3) break; // retiring levies is not helping this spawn: stop taking more of them
				}
				if (freedNow > 0)
				{
					ExtraReserve = ExtraReserveSlots;
					if (!_told) { _told = true; try { Terraria.Chat.ChatHelper.BroadcastChatMessage(Terraria.Localization.NetworkText.FromKey("Mods.Levy.Boss.RoomMade", Freed), new Color(200, 200, 120)); } catch (Exception) { } }
					int who = source is EntitySource_Parent sp && sp.Entity is NPC par ? par.whoAmI : -1;
					if (who != _loggedFor) { _loggedFor = who; Levy.Instance?.Logger.Info($"[BossSlots] a boss spawn (type {type}) found no free NPC slot: {freedNow} made so far (critters {FreedCritters}, hostiles {FreedHostiles}, garrison {FreedReleased}, summoned {FreedSummons}, machines packed {FreedPacked}, levies off the field {FreedLevies}), spawn {(r < Main.maxNPCs ? "succeeded" : "STILL FAILED")}"); }
				}
			}
			finally { _busy = false; }
			return r;
		}

		/// <summary>Frees exactly one NPC slot by the order above. False when nothing could be taken.</summary>
		private static bool FreeOne(Vector2 at, int from, out bool levy)
		{
			levy = false;
			int v = Farthest(at, from, IsCritter);
			if (v >= 0) { Drop(v); Freed++; FreedCritters++; return true; }
			if (!EventOn())
			{
				v = Farthest(at, from, IsFillerHostile);
				if (v >= 0) { Drop(v); Freed++; FreedHostiles++; return true; }
			}
			for (int i = 0; i < SlotReleasers.Count; i++)
			{
				int got = 0;
				try { got = SlotReleasers[i](1, from); } catch (Exception e) { Levy.Instance?.Logger.Warn("[BossSlots] a slot releaser threw: " + e.Message); }
				if (got > 0) { Freed += got; FreedReleased += got; return true; }
			}
			v = Farthest(at, from, IsArmyExtra);
			if (v >= 0) { Drop(v); Freed++; FreedSummons++; return true; }
			for (int i = 0; i < MachinePackers.Count; i++)
			{
				int got = 0;
				try { got = MachinePackers[i](at, from); } catch (Exception e) { Levy.Instance?.Logger.Warn("[BossSlots] a machine packer threw: " + e.Message); }
				if (got > 0) { Freed += got; FreedPacked += got; return true; }
			}
			v = FarthestLevy(at, from);
			if (v >= 0)
			{
				if (Main.npc[v].ModNPC is SoldierNPC s) { RosterSystem.RetireBody(s); Squeeze++; Freed++; FreedLevies++; levy = true; return true; }
			}
			return false;
		}

		private static void Drop(int i)
		{
			NPC n = Main.npc[i];
			n.life = 0; n.active = false; n.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);
		}

		private static int Farthest(Vector2 at, int from, Func<NPC, bool> pick)
		{
			int best = -1; float bd = -1f;
			for (int i = from; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !pick(n)) continue;
				float d = Vector2.DistanceSquared(n.Center, at);
				if (d > bd) { bd = d; best = i; }
			}
			return best;
		}

		private static int FarthestLevy(Vector2 at, int from)
		{
			int best = -1; float bd = -1f;
			for (int i = from; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC s || s.Hostile || s.Record == null || s.IsMilitia) continue;
				float d = Vector2.DistanceSquared(n.Center, at);
				if (d > bd) { bd = d; best = i; }
			}
			return best;
		}

		private static bool IsCritter(NPC n) => n.friendly && !n.townNPC && n.ModNPC == null && !n.boss && (NPCID.Sets.CountsAsCritter[n.type] || n.catchItem > 0);

		private static bool IsFillerHostile(NPC n)
		{
			if (n.friendly || n.townNPC || n.boss || n.dontTakeDamage || n.ModNPC is ITargetBody) return false;
			string mod = n.ModNPC?.Mod.Name;
			if (mod == "Levy" || mod == "Mechanization" || mod == "Warbands") return false;
			if (n.type < 0 || n.type >= NPCID.Sets.ShouldBeCountedAsBoss.Length || NPCID.Sets.ShouldBeCountedAsBoss[n.type]) return false;
			return !EnemyTargeting.IsBossClass(n);
		}

		/// <summary>The army's own extras: units an officer's kit summoned and creatures an officer conjured (never a levy, a militia man or an officer).</summary>
		private static bool IsArmyExtra(NPC n) => !n.townNPC && (n.ModNPC is SummonedUnitNPC || (n.TryGetGlobalNPC(out ConjuredGlobalNPC c) && c.Active));

		private static bool EventOn() => Main.invasionType != 0 || Main.eclipse || Main.pumpkinMoon || Main.snowMoon || Terraria.GameContent.Events.DD2Event.Ongoing || NPC.LunarApocalypseIsUp;

		/// <summary>True while any boss (or a boss's part) is alive, read from a scan that is at most half a second old (a companion mod's "stand down during a boss fight" rule asks every tick). Server / single player.</summary>
		public static bool BossAlive
		{
			get
			{
				uint now = Main.GameUpdateCount;
				if (!_bossKnown || now < _bossStamp || now - _bossStamp >= 30u) { _bossKnown = true; _bossStamp = now; _bossCached = AnyBoss(); }
				return _bossCached;
			}
		}
		private static bool _bossKnown, _bossCached; private static uint _bossStamp;

		/// <summary>True while any boss (or a boss's part) is alive.</summary>
		private static bool AnyBoss()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !n.friendly && (n.boss || (n.type > 0 && n.type < NPCID.Sets.ShouldBeCountedAsBoss.Length && NPCID.Sets.ShouldBeCountedAsBoss[n.type]))) return true; }
			return false;
		}

		internal static void Tick()
		{
			if (ExtraReserve == 0 && Squeeze == 0) return;
			if (Main.GameUpdateCount % 30 != 0) return;
			if (!AnyBoss()) { ExtraReserve = 0; Squeeze = 0; _loggedFor = -1; }
		}
	}

	/// <summary>Installs the NPC.NewNPC detour that lets a boss spawn find room (<see cref="BossSlots"/>) and ends the squeeze once the boss is gone.</summary>
	public class BossSlotsSystem : ModSystem
	{
		public override void Load() { On_NPC.NewNPC += NewNpcDetour; }
		public override void Unload() { On_NPC.NewNPC -= NewNpcDetour; BossSlots.SlotReleasers.Clear(); BossSlots.MachinePackers.Clear(); BossSlots.Reset(); }
		public override void OnWorldLoad() => BossSlots.Reset();
		public override void OnWorldUnload() => BossSlots.Reset();
		public override void PostUpdateNPCs() { if (LevyUtil.IsServerOrSingle) BossSlots.Tick(); }

		private static int NewNpcDetour(On_NPC.orig_NewNPC orig, IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)
		{ _hl_NewNpcDetour_1++;
			return BossSlots.Spawn(st => orig(source, X, Y, Type, st, ai0, ai1, ai2, ai3, Target), source, X, Y, Type, Start);
		} private static int _hl_NewNpcDetour_1;
	}
}
