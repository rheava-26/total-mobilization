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
				Empowerment emp = EmpowermentLoader.GetEmpowerment(type);
				if (emp == null || type == EmpowermentLoader.NotImplementedType || lvl == 0) continue;
				if (lvl > emp.MaxLevel) lvl = emp.MaxLevel;

				if (emp is EmpowermentProlongation)
				{
					// Prolongation adds time to everything already running instead of being a stat of its own.
					uint extra = (uint)(lvl * 30 + 30);
					for (int t = 0; t < SoldierThoriumGlobalNPC.EmpSlots; t++)
						if (ast.TicksLeft(t, now) > 0) { ast.EmpUntil[t] += extra; applied = true; }
					continue;
				}
				if (dur <= 0) continue;
				byte cur = ast.LevelOf(type, now);
				if (cur > lvl) continue; // a lower tier never overwrites a stronger one that is still playing
				uint until = now + (uint)dur;
				if (cur == lvl && ast.EmpUntil[type] >= until) { applied = true; continue; } // already at least this long
				statsChanged |= ast.SetEmp(type, lvl, until);
				applied = true;
			}
			if (!applied) return;
			uint left = 0;
			for (int t = 0; t < SoldierThoriumGlobalNPC.EmpSlots; t++) left = Math.Max(left, ast.TicksLeft(t, now));
			if (left > 0) a.NPC.AddBuff(ModContent.BuffType<BardEmpowermentBuff>(), (int)Math.Min(left, int.MaxValue));
			if (statsChanged) { a.StatsDirty = true; a.NPC.netUpdate = true; }
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
