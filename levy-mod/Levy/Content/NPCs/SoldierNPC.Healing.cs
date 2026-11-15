using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Weapons;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// HEALING on the player's rules. A levy's life regeneration is <c>Player.UpdateLifeRegen</c> run on its dummy: the time since the last hit, standing still or moving, sitting or sleeping, the gear's lifeRegen
	/// (Band of Regeneration, Charm of Myths, Calamity's and Thorium's regeneration), potion and food buffs (Regeneration, Well Fed), the Campfire and the Heart Lantern (the dummy runs the pass as the local player, with the scene flags
	/// of the levy's own stations), honey, Shiny Stone, Crimson armor and the Palladium and Nebula set boosts. Nothing is flat. Healing potions are drunk at the pace a player can drink them: the potion sickness the dummy's own
	/// <c>Player.ApplyPotionDelay</c> gives (vanilla 60 s, 45 s for a Restoration Potion, less with a Philosopher's Stone and whatever an item or a mod changes through its potion-delay hooks).
	/// <para/>Cost: a levy at full life with a spent regeneration clock does nothing; one that is hurt runs the pass once every <see cref="RegenBatchTicks"/> ticks and credits the ticks in between at the rate the pass just gave, which is
	/// the same total (the rate only changes at the clock's thresholds, five seconds apart or more).
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Ticks per regeneration pass; the ticks in between are credited from the pass's own result.</summary>
		internal const int RegenBatchTicks = 4;
		/// <summary>Player.lifeRegen the stat pass left on the dummy (gear and mod regeneration), put back before every regeneration pass. Never negative: damage over time is the hit path's.</summary>
		private int _regenBase;
		/// <summary>The part of <see cref="_buffLifeRegen"/> that is already inside Player.lifeRegen (what the mods' PostUpdateEquips added in the gear frame), so it is not counted twice.</summary>
		private int _modRegenDelta;
		/// <summary>Ticks until a mana potion may be drunk again (a mana potion has no potion sickness; its use time is the only gap).</summary>
		private int _manaPotionGap;
		/// <summary>Harness telemetry: HP the natural regeneration gave.</summary>
		internal int RegenHpTotal;
		/// <summary>Harness switch: LEVY_REGEN=flat runs the old flat regeneration (1 HP a second after 5 s) for a before/after reading.</summary>
		internal static bool FlatRegenLab = Environment.GetEnvironmentVariable("LEVY_REGEN") == "flat";

		/// <summary>Stations the worn gear itself provides (Calamity's The Camper lights the Campfire and the Heart Lamp for its wearer): added to the stations the levy stands beside.</summary>
		private StationFlags _gearStations;

		/// <summary>
		/// What gear hands its wearer through buffs and the scene flags, which on a levy would be thrown away with the dummy's vanilla buffs at the next frame: the Campfire and the Heart Lamp (their regeneration is the regeneration pass's
		/// station flags) and the food buffs (Well Fed, Plenty Satisfied, Exquisitely Stuffed move to the body, where ApplyPotionBuffs reads them).
		/// </summary>
		private void GearBuffsToBody(Player d, bool campfire0, bool lantern0)
		{
			var scene = Main.SceneMetrics; StationFlags f = StationFlags.None;
			if (d.HasBuff(BuffID.Campfire) || (scene.HasCampfire && !campfire0)) f |= StationFlags.Campfire;
			if (d.HasBuff(BuffID.HeartLamp) || (scene.HasHeartLantern && !lantern0)) f |= StationFlags.HeartLantern;
			_gearStations = f;
			if (LevyUtil.IsClient) return;
			foreach (int food in new[] { BuffID.WellFed, BuffID.WellFed2, BuffID.WellFed3 })
				if (d.HasBuff(food)) { NPC.AddBuff(food, 180); StatsDirty = true; }
		}

		/// <summary>Called by the stat pass after the equips: the regeneration the gear left on the dummy.</summary>
		private void NoteRegenBase(Player d) => _regenBase = Math.Max(0, d.lifeRegen);

		private static readonly MethodInfo ApplyPotionDelayMethod = typeof(Player).GetMethod("ApplyPotionDelay", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Item) }, null);

		/// <summary>Vanilla resets <c>PotionDelayModifier</c> every tick before the equips run (the Philosopher's Stone sets it from last pass's flag); the dummy runs the pass without that line, so every pass starts here.</summary>
		private static void ResetPotionDelayModifier(Player d)
		{
			d.PotionDelayModifier = StatModifier.Default;
			if (d.pStone) d.PotionDelayModifier *= Player.PhilosopherStoneDurationMultiplier;
		}

		/// <summary>Mana only: the Star in a Bottle's extra mana. The life half moved into <see cref="Regen"/>.</summary>
		private void StationMana()
		{
			if ((Stations & StationFlags.StarInBottle) != 0 && Record != null && Record.MaxMana > 0 && Record.Mana < Record.MaxMana)
			{
				if (++_stationManaTick >= LevyConstants.StationStarManaIntervalTicks) { _stationManaTick = 0; Record.Mana++; }
			}
			else _stationManaTick = 0;
		}

		private void Regen()
		{
			NPC.friendlyRegen = 0; // vanilla heals every friendly NPC 1 HP a 181 ticks (NPC.CheckLifeRegen); a levy heals by the player's rules only
			StationMana();
			if (_manaPotionGap > 0) _manaPotionGap--;
			Drown(); // the breath meter (config; off by default)
			if (!IsAlive) return;
			Player d = Dummy;
			if (d == null || Record == null || OfficerMode || !PipelineOk || FlatRegenLab) { FlatRegen(); return; }
			if (TicksSinceDamaged == 0) { d.lifeRegenTime = 0f; d.lifeRegenCount = 0; } // Player.Hurt: lifeRegenTime = 0
			if (((int)Main.GameUpdateCount + NPC.whoAmI) % RegenBatchTicks != 0) return;
			if (NPC.life >= NPC.lifeMax)
			{
				if (d.lifeRegenTime < 3600f) d.lifeRegenTime = Math.Min(3600f, d.lifeRegenTime + RegenBatchTicks); // the clock keeps running at full life, so the first wound after a rest heals at the full rate
				return;
			}
			int gain = RegenPass(d);
			if (gain <= 0) return;
			if (Record.StoodDown)
			{
				var home = Home;
				if (home != null && LevyUtil.WithinTiles(NPC.Center, home.Center, LevyConstants.RegenHomeBonusTiles)) gain *= LevyConstants.RegenHpPerIntervalAtHome; // the barracks' own bonus on top of the player's rate
			}
			NPC.life = Math.Min(NPC.lifeMax, NPC.life + gain);
			RegenHpTotal += gain;
			Record.CurrentHp = NPC.life;
			NPC.netUpdate = true;
		}

		/// <summary>Runs Player.UpdateLifeRegen on the dummy for this and the next <see cref="RegenBatchTicks"/> - 1 ticks; returns the life it gives (never negative).</summary>
		private int RegenPass(Player d)
		{
			d.statLifeMax2 = Math.Max(1, NPC.lifeMax); d.statLife = NPC.life;
			d.lifeRegen = _regenBase + Math.Max(0, _buffLifeRegen - _modRegenDelta) + SetRegenUnits(); // the gear's, the potions' and food's, the set boosts'
			d.honey = (Stations & StationFlags.Honey) != 0;
			d.velocity = NPC.velocity;
			bool wasSitting = d.sitting.isSitting, wasSleeping = d.sleeping.isSleeping;
			if (Seated && !wasSitting) d.sitting.isSitting = true; // the seated pose is only drawn on a client; the regeneration rule reads it on the server too
			if (_lzSleepPosed) d.sleeping.isSleeping = true;
			int count0 = d.lifeRegenCount, hp0 = d.statLife; float time0 = d.lifeRegenTime;
			var scene = Main.SceneMetrics; bool camp0 = scene.HasCampfire, lantern0 = scene.HasHeartLantern;
			try
			{
				StationFlags stations = Stations | _gearStations;
				scene.HasCampfire = (stations & StationFlags.Campfire) != 0;
				scene.HasHeartLantern = (stations & StationFlags.HeartLantern) != 0;
				InFrame(() => d.UpdateLifeRegen());
			}
			finally { scene.HasCampfire = camp0; scene.HasHeartLantern = lantern0; d.sitting.isSitting = wasSitting; d.sleeping.isSleeping = wasSleeping; }
			if (d.statLife < hp0 || d.lifeRegenCount < count0 && d.statLife == hp0) { d.statLife = hp0; d.lifeRegenCount = Math.Max(0, count0); return 0; } // damage over time is not this pass's: nothing is taken here
			int perTick = (d.lifeRegenCount - count0) + 120 * (d.statLife - hp0);
			float dt = d.lifeRegenTime - time0;
			if (dt > 0f) { d.lifeRegenTime += dt * (RegenBatchTicks - 1); if (!d.shinyStone && d.lifeRegenTime > 3600f) d.lifeRegenTime = 3600f; }
			if (perTick > 0) d.lifeRegenCount += perTick * (RegenBatchTicks - 1);
			while (d.lifeRegenCount >= 120) { d.lifeRegenCount -= 120; if (d.statLife < d.statLifeMax2) d.statLife++; }
			int gain = d.statLife - hp0;
			d.statLife = NPC.life;
			return Math.Max(0, gain);
		}

		/// <summary>The old flat rule (1 HP a second after five undamaged seconds, 2 HP at the home barracks): only for a body with no stat pipeline (an officer, a pipeline that failed) and the lab switch.</summary>
		private void FlatRegen()
		{
			// the station units (Campfire, Heart Lantern, honey) and the buff units still count, in the player's units: 120 = 1 HP
			int units = StationBuffSystem.LifeRegen(Stations) + Math.Max(0, _buffLifeRegen) + SetRegenUnits();
			if (units > 0 && NPC.life < NPC.lifeMax)
			{
				_stationLifeCount += units;
				if (_stationLifeCount >= LevyConstants.StationLifeRegenPerHp)
				{
					_stationLifeCount -= LevyConstants.StationLifeRegenPerHp;
					NPC.life = Math.Min(NPC.lifeMax, NPC.life + 1);
					if (Record != null) Record.CurrentHp = NPC.life;
					NPC.netUpdate = true;
				}
			}
			else _stationLifeCount = 0;
			if (NPC.life >= NPC.lifeMax || TicksSinceDamaged <= LevyConstants.RegenDelayTicks) { _regenCounter = 0; return; }
			if (++_regenCounter < (Dummy != null && Dummy.crimsonRegen ? (int)(LevyConstants.RegenIntervalTicks / LevyConstants.CrimsonRegenSpeedup) : LevyConstants.RegenIntervalTicks)) return;
			_regenCounter = 0;
			int amount = LevyConstants.RegenHpPerInterval;
			if (Record != null && Record.StoodDown)
			{
				var home = Home;
				if (home != null && LevyUtil.WithinTiles(NPC.Center, home.Center, LevyConstants.RegenHomeBonusTiles)) amount = LevyConstants.RegenHpPerIntervalAtHome;
			}
			NPC.life = Math.Min(NPC.lifeMax, NPC.life + amount);
			if (Record != null) Record.CurrentHp = NPC.life;
			NPC.netUpdate = true;
		}

		// ------------------------------------------------------------------------------------------------------
		// Potion sickness
		// ------------------------------------------------------------------------------------------------------
		/// <summary>Ticks of potion sickness after drinking <paramref name="potion"/>: what <c>Player.ApplyPotionDelay</c> gives the dummy (the item's own delay, the Philosopher's Stone and every potion-delay hook).</summary>
		internal int PotionDelayFor(Item potion)
		{
			Player d = Dummy;
			if (d == null || potion == null || potion.IsAir) return LevyConstants.PotionSicknessTicks;
			if (ApplyPotionDelayMethod == null) return (int)d.PotionDelayModifier.ApplyTo(LevyConstants.PotionSicknessTicks);
			int delay = 0;
			try
			{
				d.potionDelay = 0;
				ApplyPotionDelayMethod.Invoke(d, new object[] { potion });
				delay = d.potionDelay; d.potionDelay = 0;
				d.ClearBuff(BuffID.PotionSickness);
			}
			catch (Exception e) { if (_accessoryProcWarned.Add(-BuffID.PotionSickness)) Levy.Instance?.Logger.Warn("Levy: potion delay failed on a soldier: " + e.Message); delay = LevyConstants.PotionSicknessTicks; }
			return Math.Max(delay, Math.Max(1, potion.useTime)); // a delay a hook removed still leaves the drink's own use time
		}

		// ------------------------------------------------------------------------------------------------------
		// One pass of a Player method on the dummy, as the local player (the same stand-in the gear frame uses)
		// ------------------------------------------------------------------------------------------------------
		/// <summary>Runs <paramref name="pass"/> with the dummy standing in slot 255 as the machine's player, at the body's position, exactly as <c>GearFrame</c> runs the mods' hooks. A hook that throws is logged once and the pass ends.</summary>
		private void InFrame(Action pass)
		{
			Player d = Dummy;
			int savedWho = d.whoAmI; Vector2 savedCenter = d.Center;
			object thorium = null;
			FrameSlot fs = default;
			var prev = GearUpkeep.Body; GearUpkeep.Body = this;
			int spawnStart = GearUpkeep.Spawned.Count;
			try
			{
				d.Center = NPC.Center;
				fs = EnterFrameSlot();
				thorium = OwnerPuppet.SwapThorium(d, true);
				RunHook(pass);
			}
			finally
			{
				ExitFrameSlot(fs);
				d.whoAmI = savedWho; d.Center = savedCenter;
				OwnerPuppet.RestoreThorium(thorium);
				GearUpkeep.Body = prev;
				AdoptUpkeepSpawns(spawnStart);
			}
		}
	}
}
