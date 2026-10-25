using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ThoriumMod;
using ThoriumMod.Empowerments;
using ThoriumMod.Items;
using Levy.Common.Net;
using Levy.Common.Systems;
using Levy.Content.Buffs;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// Bard EMPOWERMENTS on Levy soldiers (and their commander). Thorium applies an instrument's empowerments only to real Players; here, when a bard soldier plays a note,
	/// <see cref="Play"/> reads the instrument's own empowerment list (and its virtual rules: <c>CanApplyEmpowerment</c>, <c>ModifyEmpowermentPool</c>, <c>ModifyEmpowerment</c>,
	/// <c>GetEmpowermentRange</c>), then applies the empowerments with Thorium's own range (500 px + the bard's range boost) and duration (300 ticks + the bard's duration bonuses) to every
	/// ally in reach: the bard himself, every soldier of the same commander, and the commander player. Allies are uniform.
	/// <list type="bullet">
	/// <item>On a soldier the empowerment is recorded in <see cref="SoldierThoriumGlobalNPC"/> and a <see cref="BardEmpowermentBuff"/> hung on the body (icon, timer, synced to clients).
	/// Its stats come from Thorium itself: <see cref="ApplyStats"/> runs each live empowerment's own <c>Update(thoriumPlayer, level)</c> on the soldier's dummy Player during the buff pass,
	/// so every stat the empowerment gives (damage, flat damage, crit, defense, move speed, life regen...) is exactly Thorium's number. Three stats Levy does not read from Thorium's
	/// fields are bridged by hand: damage reduction becomes the dummy's endurance, life regeneration feeds Levy's regen, attack speed shortens the weapon cooldown (see ThoriumDriver).</item>
	/// <item>On the commander player the empowerment timer is written into the player's own Thorium empowerment data, exactly as Thorium's loader does (in multiplayer the server sends the
	/// client a <see cref="LevyPacket.CompanionEffect"/> and the client writes it).</item>
	/// </list>
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class EmpowerCompat
	{
		/// <summary>Ticks between a bard soldier's empowering pulses (an instrument plays far more often; the buff lasts 5+ seconds).</summary>
		public const int PlayCadenceTicks = 30;

		/// <summary>A bard soldier just played <paramref name="bi"/>: empower every ally in reach.</summary>
		public static void Play(SoldierNPC s, BardItem bi, SoldierThoriumGlobalNPC st)
		{
			Player d = s.Dummy;
			if (d == null || bi.Empowerments == null || bi.Empowerments.Count == 0) return;
			uint now = Main.GameUpdateCount;
			if (now < st.EmpNextPlay) return;
			st.EmpNextPlay = now + PlayCadenceTicks;

			ThoriumPlayer btp = d.GetModPlayer<ThoriumPlayer>();
			if (btp.accPitchHelper) return; // the tuning accessory turns empowerments off, as in Thorium
			short baseDur = (short)Math.Clamp((int)((300 + btp.bardBuffDuration + bi.DurationBonus) * btp.bardBuffDurationX), 1, short.MaxValue);
			float baseRange = 500 + btp.bardRangeBoost;
			Vector2 from = s.NPC.Center;

			foreach (SoldierNPC a in RosterSystem.Instance.ActiveBodies())
			{
				if (a == null || !a.IsAlive || a.Dummy == null) continue;
				if (a != s && !CompatAllies.SameCommander(s, a)) continue;
				if (!a.NPC.TryGetGlobalNPC(out SoldierThoriumGlobalNPC ast)) continue;
				if (!InRange(bi, d, a.Dummy, baseRange, from, a.NPC.Center)) continue;
				ApplyToSoldier(bi, btp, d, a, ast, baseDur, now);
			}

			Player cmd = CompatAllies.CommanderOf(s);
			if (cmd != null && InRange(bi, d, cmd, baseRange, from, cmd.Center)) ApplyToPlayer(bi, btp, d, cmd, baseDur);
		}

		private static bool InRange(BardItem bi, Player bard, Player target, float baseRange, Vector2 from, Vector2 to)
		{
			float range = baseRange;
			try { bi.GetEmpowermentRange(bard, target, ref range); } catch (Exception) { }
			return range > 0f && Vector2.DistanceSquared(from, to) <= range * range;
		}

		/// <summary>The (type, level) pairs this instrument gives <paramref name="target"/>, after its own virtual rules.</summary>
		private static System.Collections.Generic.List<(byte type, byte level)> PoolFor(BardItem bi, Player bard, Player target)
		{
			var pool = new EmpowermentPool();
			EmpowermentList list = bi.Empowerments;
			for (int i = 0; i < list.Count; i++)
			{
				var group = list[i];
				for (int j = 0; j < group.Count; j++)
				{
					bool can = true;
					try { can = bi.CanApplyEmpowerment(bard, target, i, j); } catch (Exception) { }
					if (can) pool.Add(group[j]);
				}
			}
			try { bi.ModifyEmpowermentPool(bard, target, pool); } catch (Exception) { }
			return pool.ToList();
		}

		private static void ApplyToSoldier(BardItem bi, ThoriumPlayer btp, Player bard, SoldierNPC a, SoldierThoriumGlobalNPC ast, short baseDur, uint now)
		{
			ThoriumPlayer ttp = a.Dummy.GetModPlayer<ThoriumPlayer>();
			bool statsChanged = false, applied = false;
			foreach (var (type, level) in PoolFor(bi, bard, a.Dummy))
			{
				byte lvl = level; short dur = baseDur;
				try { bi.ModifyEmpowerment(btp, ttp, type, ref lvl, ref dur); } catch (Exception) { }
				applied |= Grant(ast, type, lvl, dur, now, ref statsChanged);
			}
			Finish(a, ast, applied, statsChanged, now);
		}

		/// <summary>
		/// Records one empowerment (type, level, duration) on a soldier. Returns true when it took (or was already at least that strong and long).
		/// Prolongation adds time to everything already running instead of being a stat of its own.
		/// </summary>
		private static bool Grant(SoldierThoriumGlobalNPC ast, byte type, byte lvl, short dur, uint now, ref bool statsChanged)
		{
			Empowerment emp = EmpowermentLoader.GetEmpowerment(type);
			if (emp == null || type == EmpowermentLoader.NotImplementedType || lvl == 0) return false;
			if (lvl > emp.MaxLevel) lvl = emp.MaxLevel;

			if (emp is EmpowermentProlongation)
			{
				bool any = false;
				uint extra = (uint)(lvl * 30 + 30);
				for (int t = 0; t < SoldierThoriumGlobalNPC.EmpSlots; t++)
					if (ast.TicksLeft(t, now) > 0) { ast.EmpUntil[t] += extra; any = true; }
				return any;
			}
			if (dur <= 0 || type >= SoldierThoriumGlobalNPC.EmpSlots) return false;
			byte cur = ast.LevelOf(type, now);
			if (cur > lvl) return false; // a lower tier never overwrites a stronger one that is still playing
			uint until = now + (uint)dur;
			if (cur == lvl && ast.EmpUntil[type] >= until) return true; // already at least this long
			statsChanged |= ast.SetEmp(type, lvl, until);
			return true;
		}

		/// <summary>After a soldier took empowerments: hang the Empowered buff for the longest one still running and flag his stats for a refresh.</summary>
		private static void Finish(SoldierNPC a, SoldierThoriumGlobalNPC ast, bool applied, bool statsChanged, uint now)
		{
			if (!applied) return;
			uint left = 0;
			for (int t = 0; t < SoldierThoriumGlobalNPC.EmpSlots; t++) left = Math.Max(left, ast.TicksLeft(t, now));
			if (left > 0) a.NPC.AddBuff(ModContent.BuffType<BardEmpowermentBuff>(), (int)Math.Min(left, int.MaxValue));
			if (statsChanged) { a.StatsDirty = true; a.NPC.netUpdate = true; }
		}

		// ---- a PLAYER's instrument (the commander plays; his soldiers and officer avatars are empowered like any ally) ----

		/// <summary>
		/// The empowerments the player's own instrument gives, worked out on the player's machine exactly as Thorium works them out for the player: the item's pool after its virtual rules,
		/// level and duration after <c>ModifyEmpowerment</c> and the bard's duration bonuses. <paramref name="range"/> is Thorium's reach (500 px + the bard's range boost, after the item's own rule).
		/// Empty when the player carries the tuning accessory or the item reaches nobody.
		/// </summary>
		public static System.Collections.Generic.List<(byte type, byte level, short dur)> ResolveForPlayer(BardItem bi, Player bard, out float range)
		{
			var res = new System.Collections.Generic.List<(byte type, byte level, short dur)>();
			range = 0f;
			ThoriumPlayer btp = bard.GetModPlayer<ThoriumPlayer>();
			if (btp.accPitchHelper || bi.Empowerments == null) return res;
			range = 500 + btp.bardRangeBoost;
			try { bi.GetEmpowermentRange(bard, bard, ref range); } catch (Exception) { }
			if (range <= 0f) return res;
			short baseDur = (short)Math.Clamp((int)((300 + btp.bardBuffDuration + bi.DurationBonus) * btp.bardBuffDurationX), 1, short.MaxValue);
			foreach (var (type, level) in PoolFor(bi, bard, bard))
			{
				byte lvl = level; short dur = baseDur;
				try { bi.ModifyEmpowerment(btp, btp, type, ref lvl, ref dur); } catch (Exception) { }
				if (lvl == 0) continue;
				res.Add((type, lvl, dur));
			}
			return res;
		}

		/// <summary>
		/// Server / single player: lay a player's resolved empowerments on every soldier AND officer avatar of that commander within <paramref name="range"/> of <paramref name="from"/>.
		/// The same grant as a bard soldier's note (so the effects are identical), minus the per-target virtual rules, which were applied on the player's machine.
		/// </summary>
		public static void ApplyResolved(Vector2 from, string commander, float range, System.Collections.Generic.IReadOnlyList<(byte type, byte level, short dur)> list)
		{
			if (list == null || list.Count == 0 || range <= 0f || Main.netMode == NetmodeID.MultiplayerClient) return;
			uint now = Main.GameUpdateCount;
			float r2 = range * range;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.life <= 0 || n.ModNPC is not SoldierNPC a || a.Record == null || !a.IsAlive) continue;
				if (!string.Equals(a.Record.CommanderName ?? "", commander ?? "", StringComparison.Ordinal)) continue;
				if (Vector2.DistanceSquared(from, n.Center) > r2) continue;
				if (!n.TryGetGlobalNPC(out SoldierThoriumGlobalNPC ast)) continue;
				bool statsChanged = false, applied = false;
				for (int k = 0; k < list.Count; k++) applied |= Grant(ast, list[k].type, list[k].level, list[k].dur, now, ref statsChanged);
				Finish(a, ast, applied, statsChanged, now);
			}
		}

		private const int MaxRequestEntries = 16;

		/// <summary>Client (multiplayer): ask the server to lay these empowerments on the local player's soldiers.</summary>
		public static void SendRequest(float range, System.Collections.Generic.List<(byte type, byte level, short dur)> list)
		{
			int n = Math.Min(list.Count, MaxRequestEntries);
			ModPacket pkt = LevyPackets.New(LevyPacket.CompanionRequest);
			pkt.Write(CompanionRequest.KindEmpower); pkt.Write((ushort)Math.Clamp(range, 0f, 65000f)); pkt.Write((byte)n);
			for (int i = 0; i < n; i++) { pkt.Write(list[i].type); pkt.Write(list[i].level); pkt.Write(list[i].dur); }
			pkt.Send();
		}

		/// <summary>Server: decode an empowerment request (payload after the kind byte). The sender is the commander; everything is bounded before it is applied.</summary>
		public static void OnRequest(BinaryReader r, int fromWhoAmI)
		{
			float range = r.ReadUInt16(); int n = Math.Min((int)r.ReadByte(), MaxRequestEntries);
			var list = new System.Collections.Generic.List<(byte type, byte level, short dur)>(n);
			for (int i = 0; i < n; i++) { byte t = r.ReadByte(), l = r.ReadByte(); short d = r.ReadInt16(); list.Add((t, l, (short)Math.Clamp((int)d, 0, 3600))); }
			if (Main.netMode != NetmodeID.Server || !CompanionRequest.AllowEmpower(fromWhoAmI)) return;
			Player p = Main.player[fromWhoAmI];
			if (p == null || !p.active || p.dead) return;
			ApplyResolved(p.Center, p.name, Math.Min(range, 4000f), list);
		}

		private static void ApplyToPlayer(BardItem bi, ThoriumPlayer btp, Player bard, Player cmd, short baseDur)
		{
			ThoriumPlayer ttp = cmd.GetModPlayer<ThoriumPlayer>();
			foreach (var (type, level) in PoolFor(bi, bard, cmd))
			{
				byte lvl = level; short dur = baseDur;
				try { bi.ModifyEmpowerment(btp, ttp, type, ref lvl, ref dur); } catch (Exception) { }
				Empowerment emp = EmpowermentLoader.GetEmpowerment(type);
				if (emp == null || type == EmpowermentLoader.NotImplementedType || lvl == 0) continue;
				if (Main.netMode == NetmodeID.Server)
				{
					ModPacket pkt = LevyPackets.New(LevyPacket.CompanionEffect);
					pkt.Write(CompanionFx.KindEmpower); pkt.Write((byte)cmd.whoAmI); pkt.Write(type); pkt.Write(lvl); pkt.Write(dur);
					CompanionFx.SendTo(pkt, cmd.whoAmI);
				}
				else WriteTimer(ttp, type, lvl, dur);
			}
		}

		private static System.Reflection.FieldInfo _dataField;

		/// <summary>A player's Thorium empowerment timers (the field is internal to Thorium, so it is read by reflection once).</summary>
		private static EmpowermentData DataOf(ThoriumPlayer tp)
		{
			_dataField ??= typeof(ThoriumPlayer).GetField("Empowerments", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			return _dataField?.GetValue(tp) as EmpowermentData;
		}

		/// <summary>Runs on the machine that owns the player: the same timer write Thorium's <c>EmpowermentLoader.ApplyEmpowerment</c> makes, minus the combat text.</summary>
		private static void WriteTimer(ThoriumPlayer ttp, byte type, byte level, short duration)
		{
			Empowerment emp = EmpowermentLoader.GetEmpowerment(type);
			EmpowermentData data = DataOf(ttp);
			EmpowermentTimer timer = null;
			if (emp == null || data == null || !data.Timers.TryGetValue(type, out timer) || timer == null) return;
			if (level > emp.MaxLevel) level = emp.MaxLevel;
			bool higher = timer.level < level;
			if (!((higher && timer.level > 0) || (timer.delay == 0 && (higher || (timer.level == level && timer.timer < duration))))) return;
			short delay = (short)Math.Min(duration / 2, 60);
			short dur2 = duration; byte lvl2 = level;
			emp.ModifyApplication(ttp, ref lvl2, ref dur2, ref delay);
			timer.level = lvl2; timer.timer = dur2; timer.delay = delay;
			if (timer.timer > 0 && !data.ActiveEmpowerments.Contains(type)) data.ActiveEmpowerments.Add(type);
		}

		/// <summary>Client side of the empower kind (payload after the kind byte: byte player, byte type, byte level, short duration).</summary>
		public static void OnPacket(BinaryReader r)
		{
			int who = r.ReadByte(); byte type = r.ReadByte(); byte level = r.ReadByte(); short dur = r.ReadInt16();
			if (who != Main.myPlayer || who >= Main.maxPlayers) return;
			WriteTimer(Main.player[who].GetModPlayer<ThoriumPlayer>(), type, level, dur);
		}

		/// <summary>
		/// SoldierNPC.ExtraBuffStats: lays the soldier's live empowerments on his dummy by running each one's own <c>Update</c>, then bridges the stats Levy reads elsewhere.
		/// Returns the life-regen units the empowerments added.
		/// </summary>
		public static int ApplyStats(SoldierNPC s, Player d)
		{
			if (!s.NPC.TryGetGlobalNPC(out SoldierThoriumGlobalNPC st)) return 0;
			if (st.EmpMask == 0 || st.EmpLevel == null) { st.EmpAttackSpeed = 0f; return 0; }
			uint now = Main.GameUpdateCount;
			ThoriumPlayer tp = d.GetModPlayer<ThoriumPlayer>();
			float endBefore = tp.thoriumEndurance, speedBefore = d.GetAttackSpeed(DamageClass.Generic);
			int regenBefore = d.lifeRegen;
			for (int t = 0; t < SoldierThoriumGlobalNPC.EmpSlots; t++)
			{
				byte lvl = st.LevelOf(t, now);
				if (lvl == 0) continue;
				Empowerment emp = EmpowermentLoader.GetEmpowerment((byte)t);
				if (emp == null || emp is EmpowermentProlongation) continue;
				try { emp.Update(tp, lvl); } catch (Exception) { }
			}
			d.endurance += tp.thoriumEndurance - endBefore;                                    // damage reduction
			st.EmpAttackSpeed = Math.Max(0f, d.GetAttackSpeed(DamageClass.Generic) - speedBefore); // attack speed (applied to the cooldown by the driver)
			return Math.Max(0, d.lifeRegen - regenBefore);                                      // life regeneration
		}

		/// <summary>Per tick: drop lapsed empowerments (recompute stats, re-sync the display).</summary>
		public static void Tick(SoldierNPC s, SoldierThoriumGlobalNPC st)
		{
			if (st.EmpMask == 0) return;
			if (st.Expire(Main.GameUpdateCount)) { s.StatsDirty = true; s.NPC.netUpdate = true; }
		}
	}
}
