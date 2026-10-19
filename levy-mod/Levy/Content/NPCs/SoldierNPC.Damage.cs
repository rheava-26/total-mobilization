using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Dialogue;
using Levy.Common.Fear;
using Levy.Common.Globals;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Damage intake for a soldier body (server / single player): immune frames, enemy contact damage, hostile-projectile
	/// bookkeeping (attacker, fear, barks), natural regen, armory potions, hit effects and lava attribution.
	/// <para/>Vanilla facts this file leans on (Terraria 1.4.4.9 / tML 2026.08, read from the decompiled source):
	/// <list type="bullet">
	/// <item>Player.Update_NPCCollision: contact damage = Main.DamageVar(npc.damage * meleeMultiplier, -luck) with no extra mode
	/// factor (NPC.ScaleStats already multiplied npc.damage by the mode's EnemyDamageMultiplier). NPC.GetMeleeCollisionData widens
	/// the attacker rectangle / raises the multiplier for a few enemies. The Empress (636) cannot hurt while ai[0] is 0 or 10.</item>
	/// <item>Projectile.Damage: a hostile projectile hurts a PLAYER for Main.DamageVar(damage, -luck) * 2 in every mode, but hits
	/// a friendly NPC through modifiers.ToHitInfo(damage, ...) at x1. ModifyHitByProjectile doubles SourceDamage so soldiers die
	/// like players. Vanilla writes npc.immune[owner] (5-10 ticks) BEFORE OnHitByProjectile and has no global immunity for NPCs;
	/// <see cref="SoldierProjectileImmunity"/> (GlobalNPC.CanBeHitByProjectile) supplies the player-style window.</item>
	/// <item>NPC.StrikeNPC calls HitEffect BEFORE checkDead, and OnHitByProjectile runs AFTER StrikeNPC, so the killer of a
	/// projectile death is recorded in ModifyHitByProjectile (pre-strike) and lava is recognised inside HitEffect.</item>
	/// <item>NPC.Collision_LavaCollision: sets immune[255] = 30, adds Burning and strikes 50 (StrikeNPCNoInteraction) every 30 ticks;
	/// friendly NPCs are not exempt. NPCs take no fall damage anywhere in NPC.cs.</item>
	/// </list>
	/// </summary>
	public partial class SoldierNPC
	{
		private int _regenCounter;
		private int _buffPotionCooldown;
		private int _armoryRetry;
		/// <summary>Our own SimpleStrikeNPC is running: HitEffect must not re-attribute the hit.</summary>
		private bool _contactStriking;
		/// <summary>ModifyHitByProjectile ran this tick: HitEffect must not treat the strike as lava.</summary>
		private bool _projectileStriking;

		partial void Damage_Update()
		{
			if (ImmuneTime > 0) ImmuneTime--;
			if (TicksSinceDamaged < LevyConstants.TicksSinceDamagedCap) TicksSinceDamaged++;
			if (PotionSickness > 0) PotionSickness--;
			if (_buffPotionCooldown > 0) _buffPotionCooldown--;
			if (_armoryRetry > 0) _armoryRetry--;
			_projectileStriking = false;
			if (!IsAlive) return;
			if (ImmuneTime == 0) ContactDamage();
			if (!IsAlive) return;
			Regen();
			Potions();
		}

		// ------------------------------------------------------------------------------------------------------
		// Contact damage from hostile NPCs (mirrors Player.Update_NPCCollision; vanilla has no NPC-vs-NPC contact)
		// ------------------------------------------------------------------------------------------------------
		private void ContactDamage()
		{
			Rectangle mine = NPC.Hitbox;
			float cx = NPC.Center.X, cy = NPC.Center.Y;
			float halfW = NPC.width * 0.5f + LevyConstants.ContactBroadPhasePx, halfH = NPC.height * 0.5f + LevyConstants.ContactBroadPhasePx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC a = Main.npc[i];
				if (!a.active || a.friendly || a.damage <= 0 || i == NPC.whoAmI) continue;
				if (a.type == NPCID.HallowBoss && (a.ai[0] == 0f || a.ai[0] == 10f)) continue;
				if (Math.Abs(a.Center.X - cx) > a.width * 0.5f + halfW || Math.Abs(a.Center.Y - cy) > a.height * 0.5f + halfH) continue;
				if (!NPCLoader.CanHitNPC(a, NPC)) continue;
				int slot = -1; float mult = 1f;
				Rectangle rect = a.Hitbox;
				NPC.GetMeleeCollisionData(mine, i, ref slot, ref mult, ref rect);
				if (!mine.Intersects(rect)) continue;

				int dir = a.Center.X < cx ? 1 : -1;
				int raw = Math.Max(1, Main.DamageVar(a.damage * mult));
				LastAttackerIndex = i; LastAttackerType = a.type; LastAttackerName = a.GivenOrTypeName;
				int done;
				_contactStriking = true;
				try { done = NPC.SimpleStrikeNPC(raw, dir, false, 0f, null, false, 0f, true); } // ModifyIncomingHit applies the player defense formula; the server broadcasts the strike
				finally { _contactStriking = false; }
				AfterHit(done, i);
				return; // one contact hit per tick; immune frames gate the rest
			}
		}

		/// <summary>Shared bookkeeping after any attributed hit (contact or hostile projectile).</summary>
		private void AfterHit(int damageDone, int attackerIndex)
		{
			ImmuneTime = LevyConstants.ImmuneTicksOnHit;
			TicksSinceDamaged = 0;
			_regenCounter = 0;
			if (attackerIndex >= 0 && attackerIndex < Main.maxNPCs && Main.npc[attackerIndex].active) SoldierTargeting.NoteAttacker(attackerIndex);
			if (Record != null) Record.CurrentHp = Math.Max(0, NPC.life);
			if (!IsAlive) return; // the roster and FearSystem.OnComradeDied own the death
			Fear_OnDamaged(damageDone, attackerIndex);
			FearSystem.OnComradeHit(this, damageDone);
			DialogueSystem.Bark(this, NPC.life * 4 < NPC.lifeMax ? DialogueTrigger.LOW_HP : DialogueTrigger.HURT);
			NPC.netUpdate = true;
		}

		// ------------------------------------------------------------------------------------------------------
		// Hostile projectiles (vanilla already strikes friendly NPCs; we fix the damage number and do the bookkeeping)
		// ------------------------------------------------------------------------------------------------------
		public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers)
		{
			if (!projectile.hostile) return;
			modifiers.SourceDamage *= LevyConstants.HostileProjectileDamageMult;
			if (LevyUtil.IsClient) return;
			// Attribution must exist before StrikeNPC reaches checkDead; OnHitByProjectile only fires afterwards.
			RecordProjectileAttacker(projectile);
			_projectileStriking = true;
		}

		public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			_projectileStriking = false;
			if (!projectile.hostile || LevyUtil.IsClient) return;
			RecordProjectileAttacker(projectile);
			AfterHit(damageDone, LastAttackerIndex);
		}

		private void RecordProjectileAttacker(Projectile projectile)
		{
			int idx = -1, type = -1;
			if (projectile.TryGetGlobalProjectile<SoldierProjectileTag>(out var tag)) { idx = tag.SourceNpcIndex; type = tag.SourceNpcType; }
			if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active && (type <= 0 || Main.npc[idx].type == type))
			{
				NPC src = Main.npc[idx];
				LastAttackerIndex = idx; LastAttackerType = src.type; LastAttackerName = src.GivenOrTypeName;
			}
			else if (type > 0)
			{
				LastAttackerIndex = -1; LastAttackerType = type; LastAttackerName = Lang.GetNPCNameValue(type);
			}
			else
			{
				LastAttackerIndex = -1; LastAttackerType = -1; LastAttackerName = projectile.Name;
			}
		}

		// ------------------------------------------------------------------------------------------------------
		// Hit effects + lava attribution (HitEffect runs inside StrikeNPC before checkDead, on every machine)
		// ------------------------------------------------------------------------------------------------------
		public override void HitEffect(NPC.HitInfo hit)
		{
			if (LevyUtil.IsServerOrSingle && !_contactStriking && !_projectileStriking && NPC.lavaWet && NPC.immune[DummySlot] == LevyConstants.LavaStrikeImmuneTicks)
			{
				// Vanilla lava strike: no attacker. Players get immune frames from it too.
				LastAttackerIndex = -1; LastAttackerType = -1; LastAttackerName = LevyUtil.T("Damage.KillerLava");
				ImmuneTime = LevyConstants.ImmuneTicksOnHit;
				TicksSinceDamaged = 0; _regenCounter = 0;
				if (Record != null) Record.CurrentHp = Math.Max(0, NPC.life);
				if (IsAlive) DialogueSystem.Bark(this, NPC.life * 4 < NPC.lifeMax ? DialogueTrigger.LOW_HP : DialogueTrigger.HURT);
			}
			if (Main.dedServ) return;
			bool dead = NPC.life <= 0;
			int count = dead ? LevyConstants.DeathDustCount : LevyConstants.HitDustCount;
			float push = hit.HitDirection * (dead ? 2.5f : 1.5f);
			for (int i = 0; i < count; i++)
			{
				int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, push, -1f);
				if (dead) Main.dust[d].velocity *= 1.5f;
			}
		}

		// ------------------------------------------------------------------------------------------------------
		// Natural regen
		// ------------------------------------------------------------------------------------------------------
		private int _stationLifeCount, _stationManaTick;

		/// <summary>
		/// Potion buffs live on the NPC (NPC.AddBuff), but the stat pipeline reads the dummy Player, which never runs UpdateBuffs, so each active buff's
		/// stat effect is applied to the dummy by hand (like StationBuffSystem.Apply; stations are flags, never NPC buffs, so nothing is applied twice).
		/// Returns the life-regen units to feed StationRegen. Call after UpdateEquips/UpdateArmorSets, before the stats are read.
		/// </summary>
		private int ApplyPotionBuffs(Player d)
		{
			int regen = 0;
			for (int i = 0; i < NPC.maxBuffs; i++)
			{
				if (NPC.buffType[i] <= 0 || NPC.buffTime[i] <= 0) continue;
				switch (NPC.buffType[i])
				{
					case BuffID.Ironskin: d.statDefense += LevyConstants.IronskinDefense; break;
					case BuffID.Swiftness: d.moveSpeed += LevyConstants.SwiftnessMoveSpeed; break;
					case BuffID.Wrath: d.GetDamage(DamageClass.Generic) += LevyConstants.WrathDamage; break;
					case BuffID.Rage: d.GetCritChance(DamageClass.Generic) += LevyConstants.RageCrit; break;
					case BuffID.Endurance: d.endurance += LevyConstants.EnduranceReduction; break;
					case BuffID.Regeneration: regen += LevyConstants.RegenerationLifeRegen; break;
					case BuffID.MagicPower: d.GetDamage(DamageClass.Magic) += LevyConstants.MagicPowerDamage; break;
					case BuffID.ManaRegeneration: d.manaRegenBuff = true; break;
					case BuffID.AmmoReservation: d.ammoPotion = true; break;
					case BuffID.Archery: d.archery = true; break;
					case BuffID.Summoning: d.maxMinions++; break;
				}
			}
			return regen;
		}

		/// <summary>Armory-side stations: Campfire / Heart Lantern add to a vanilla-style lifeRegen counter (1 HP per 120 units, applied from the first tick like vanilla); Star in a Bottle adds extra mana.</summary>
		private void StationRegen()
		{
			if (Stations == StationFlags.None && _buffLifeRegen <= 0) { _stationLifeCount = _stationManaTick = 0; return; }
			int units = StationBuffSystem.LifeRegen(Stations) + Math.Max(0, _buffLifeRegen);
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
			if ((Stations & StationFlags.StarInBottle) != 0 && Record != null && Record.MaxMana > 0 && Record.Mana < Record.MaxMana)
			{
				if (++_stationManaTick >= LevyConstants.StationStarManaIntervalTicks) { _stationManaTick = 0; Record.Mana++; }
			}
			else _stationManaTick = 0;
		}

		private void Regen()
		{
			StationRegen();
			if (NPC.life >= NPC.lifeMax || TicksSinceDamaged <= LevyConstants.RegenDelayTicks) { _regenCounter = 0; return; }
			if (++_regenCounter < LevyConstants.RegenIntervalTicks) return;
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
		// Potions from the nearest armory. Heal/mana: issue slot 0 first, then bulk storage. Buffs: issue slot 1 ONLY
		// (intentionally never bulk storage - the buff slot is the limited buff economy, so choosing the army's buffs stays a real decision).
		// ------------------------------------------------------------------------------------------------------
		private void Potions()
		{
			if (Record == null || _armoryRetry > 0) return;
			bool wantHeal = PotionSickness == 0 && NPC.life * 100 < NPC.lifeMax * LevyConstants.PotionDrinkBelowPercent;
			// Magic/summon soldiers top up an empty mana pool from Mana / Restoration / Super Mana potions.
			bool wantMana = PotionSickness == 0 && Record.MaxMana > 0 && Record.Mana * 100 < Record.MaxMana * LevyConstants.ManaPotionDrinkBelowPercent;
			bool wantBuff = _buffPotionCooldown == 0 && (State == SoldierState.Engage || TargetIndex >= 0);
			if (!wantHeal && !wantMana && !wantBuff) return;
			var armory = SettlementSystem.Instance.NearestArmory(NPC.Center, LevyConstants.ResupplyArmoryRadius);
			if (armory == null) { _armoryRetry = LevyConstants.PotionRetryTicks; return; }
			bool drank = false;
			if (wantHeal || wantMana)
			{
				// At most one restorative per check (PotionSickness then gates the next); heal is tried before mana. Only a potion that serves the need is removed.
				Item potion = null;
				if (wantHeal) potion = armory.TakeServingPotion(0, it => it.healLife > 0);
				if (potion == null && wantMana) potion = armory.TakeServingPotion(0, it => it.healMana > 0);
				if (potion != null && !potion.IsAir)
				{
					if (potion.healLife > 0) NPC.life = Math.Min(NPC.lifeMax, NPC.life + potion.healLife);
					if (potion.healMana > 0) Record.Mana = Math.Min(Record.MaxMana, Record.Mana + potion.healMana);
					if (potion.buffType > 0) { NPC.AddBuff(potion.buffType, Math.Max(1, potion.buffTime)); StatsDirty = true; }
					PotionSickness = LevyConstants.PotionSicknessTicks;
					drank = true;
				}
				else _armoryRetry = LevyConstants.PotionRetryTicks;
			}
			if (wantBuff)
			{
				var potion = armory.TakeServingPotion(1, it => it.buffType > 0 && it.healLife <= 0 && it.healMana <= 0, allowStorage: false); // a pure buff potion, not a restorative; issue-slot-only
				if (potion != null && !potion.IsAir)
				{
					NPC.AddBuff(potion.buffType, Math.Max(1, potion.buffTime));
					StatsDirty = true;
					_buffPotionCooldown = LevyConstants.BuffPotionIntervalTicks;
					drank = true;
				}
				else _buffPotionCooldown = LevyConstants.PotionRetryTicks;
			}
			if (drank)
			{
				Record.CurrentHp = NPC.life;
				// CONTRACT-REQUEST: LevyPackets has no world-effect packet, so the drink sound is heard in single player only;
				// multiplayer clients see the HP jump through the SyncNPC that netUpdate triggers.
				SoundEngine.PlaySound(SoundID.Item3, NPC.Center);
				NPC.netUpdate = true;
			}
		}
	}

	/// <summary>
	/// Player-style projectile immunity for soldier bodies. Vanilla only tracks per-owner npc.immune[] for NPCs, which hostile
	/// projectiles write AFTER the hit (5-10 ticks) and which lava also uses (slot 255); this gate gives the body one immune window
	/// for every hostile projectile while <see cref="SoldierNPC.ImmuneTime"/> runs, like Player.immune does for a player.
	/// Cooldown slots (Moon Lord / Empress projectiles bypass a player's immunity) are folded into the single window.
	/// </summary>
	public sealed class SoldierProjectileImmunity : GlobalNPC
	{
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == ModContent.NPCType<SoldierNPC>();

		public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile)
		{
			if (projectile.hostile && npc.ModNPC is SoldierNPC s && s.ImmuneTime > 0) return false;
			return null;
		}
	}
}
