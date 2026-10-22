using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.Projectiles;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Armor SET BONUS effects that vanilla runs through the wearing Player (its on-hit hooks, Player.Hurt, the buff handlers and the draw layers) and that therefore never
	/// happen for a soldier, whose projectiles are owned by the commander with npcProj set and whose hidden dummy Player never runs Update / UpdateBuffs / Hurt.
	/// <para/>THE ROOT: Player.UpdateArmorSets matches the head / body / legs FIELDS, which only Player.Update and PlayerFrame (the draw pass) ever fill. The dummy runs neither on a server,
	/// so no set bonus (stat or special) was ever recognised there, and on a client only once the soldier had been on screen. <see cref="UpdateDummyArmorSets"/> resolves them first.
	/// <para/>What lives here, soldier by soldier (server / single player; clients only mirror the numbers they draw from):
	/// <list type="bullet">
	/// <item>Beetle Might / Defense and Solar Flare: vanilla draws the orbiting beetles and the flare shields as PLAYER DRAW LAYERS (not projectiles) off beetleOrbs / solarShields, and the
	/// damage side from the same counters. They are run here every tick on the soldier's own counters and written to the dummy for the draw; the count rides NPC.ai[0] to clients.</item>
	/// <item>On-hit sets (Palladium, Orichalcum, Titanium, Hallowed, Nebula, Frost, Spectre Hood) off <see cref="OnProjectileHit"/> / <see cref="OnMeleeHit"/>, with per-soldier cooldowns.</item>
	/// <item>Damage intake (Solar and Beetle reduction, Paladin's Shield, Hallowed dodge, thorns) off the hooks in SoldierNPC.Damage.cs.</item>
	/// </list>
	/// Deferred as player-centric or owned elsewhere: Vortex stealth, Shroomite stealth, Forbidden storm, Stardust Guardian and the Squire / Apprentice / Huntress / Monk sentry bonuses.
	/// </summary>
	public partial class SoldierNPC
	{
		private const int KindNone = 0, KindMight = 1, KindDefense = 2, KindSolar = 3;

		/// <summary>0 none, 1 Beetle Might, 2 Beetle Defense, 3 Solar Flare (decoded from NPC.ai[0] on clients) and the orb / shield count 0..3.</summary>
		private int _setKind, _setOrbs;
		/// <summary>The beetle / solar counter (vanilla beetleCounter / solarCounter) and Beetle Might's countdown.</summary>
		private float _setCounter;
		private int _beetleCountdown;

		/// <summary>Spectre Hood's lifeSteal pool: how much a magic hit may still heal (refills every tick, see <see cref="LevyConstants.HoodPoolRefill"/>).</summary>
		private float _hoodPool = LevyConstants.HoodPoolMax;

		/// <summary>Ticks left on each timer (all authority only): Hallowed ready / cooldown, Palladium regen, Titanium storm and its hit gap, Orichalcum and Nebula gaps.</summary>
		private int _holyDodgeReady, _holyDodgeCooldown, _palladiumTicks, _titaniumGap, _petalGap, _nebulaGap;
		/// <summary>Ticks of Titanium storm left; the soldier's shards (SoldierTitaniumShard) live exactly as long as this is positive.</summary>
		public int TitaniumStormTicks;
		/// <summary>Nebula booster levels and the ticks left on the current level: [0] life, [1] damage.</summary>
		private readonly int[] _nebulaLevel = new int[2], _nebulaTicks = new int[2];

		/// <summary>Scale the next contact hit takes (set around the strike, read by <see cref="SoldierSetHitScale"/>); 1 otherwise.</summary>
		internal float PendingContactScale = 1f;
		/// <summary>The Paladin's Shield wearer that will take a share of the hit now being resolved, and the guard against a soak chain.</summary>
		private SoldierNPC _hitPaladin;
		private bool _soaking;

		// ------------------------------------------------------------------------------------------------------------
		// The root fix: resolve head / body / legs, then run the set pass safely
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>
		/// Runs Player.UpdateArmorSets on the dummy with its head / body / legs resolved from the armor slots (vanilla fills them in Player.Update, which the dummy never runs).
		/// Two vanilla branches are kept from misfiring on a body that is not a real player: the Solar Flare shader burst (it needs the client-only GameShaders and its counter is
		/// ours, so it is held at 0) and every "whoAmI == Main.myPlayer" branch (the Stardust Guardian spawn, Bee Gun achievement) which on a dedicated server is true of the
		/// dummy's slot 255, so the call borrows a slot nobody holds.
		/// </summary>
		private void UpdateDummyArmorSets()
		{
			Player d = Dummy;
			d.head = d.armor[0].headSlot;
			d.body = d.armor[1].bodySlot;
			d.legs = d.armor[2].legSlot;
			d.solarCounter = 0;
			// ResetEffects zeroed the orbit state the draw layers read; hand it back so the pass does not snap the beetles / flare shield home once a second (and gives the Solar dash its shield test).
			d.beetleOrbs = _setKind == KindMight || _setKind == KindDefense ? _setOrbs : 0;
			d.solarShields = _setKind == KindSolar ? _setOrbs : 0;
			int savedWho = d.whoAmI;
			try
			{
				d.whoAmI = DummySlot - 2;
				d.UpdateArmorSets(DummySlot);
			}
			finally { d.whoAmI = savedWho; }
		}

		// ------------------------------------------------------------------------------------------------------------
		// Per-tick upkeep (every machine): counters + timers on the authority, the numbers they draw from on clients
		// ------------------------------------------------------------------------------------------------------------
		public override void PostAI()
		{
			Player d = Dummy;
			if (d == null || Record == null) return;
			try
			{
				if (LevyUtil.IsClient) { int enc = (int)NPC.ai[0]; _setKind = enc / 10; _setOrbs = enc % 10; }
				else TickSetState(d);
				ApplySetVisuals(d);
			}
			catch (Exception e)
			{
				if (_accessoryProcWarned.Add(-ItemID.BeetleHusk)) Levy.Instance?.Logger.Warn("Levy: armor set upkeep failed for a soldier: " + e.Message);
			}
		}

		private void TickSetState(Player d)
		{
			if (_holyDodgeCooldown > 0) _holyDodgeCooldown--;
			if (_holyDodgeReady > 0 && (--_holyDodgeReady == 0 || !d.onHitDodge)) _holyDodgeReady = 0;
			if (_palladiumTicks > 0) _palladiumTicks--;
			if (_titaniumGap > 0) _titaniumGap--;
			if (_petalGap > 0) _petalGap--;
			if (_nebulaGap > 0) _nebulaGap--;
			if (TitaniumStormTicks > 0 && (!d.onHitTitaniumStorm || --TitaniumStormTicks < 0)) TitaniumStormTicks = 0;
			for (int i = 0; i < 2; i++)
			{
				if (_nebulaTicks[i] > 0 && --_nebulaTicks[i] == 0)
				{
					// Vanilla steps a booster down a level when its buff runs out and keeps the lower one for another full duration.
					_nebulaLevel[i]--;
					if (_nebulaLevel[i] > 0) _nebulaTicks[i] = LevyConstants.NebulaBoosterTicks;
					if (i == 1) StatsDirty = true;
				}
			}
			if (!d.setNebula) { _nebulaLevel[0] = _nebulaLevel[1] = 0; _nebulaTicks[0] = _nebulaTicks[1] = 0; }
			_hoodPool = Math.Min(Main.expertMode ? LevyConstants.HoodPoolMaxExpert : LevyConstants.HoodPoolMax, _hoodPool + (Main.expertMode ? LevyConstants.HoodPoolRefillExpert : LevyConstants.HoodPoolRefill));

			bool alive = IsAlive;
			int kind = !alive ? KindNone : d.beetleOffense ? KindMight : d.beetleDefense ? KindDefense : d.setSolar ? KindSolar : KindNone;
			if (kind != _setKind) { _setKind = kind; _setCounter = 0f; _beetleCountdown = 0; SetOrbs(0); }
			switch (_setKind)
			{
				case KindMight: StepBeetleMight(); break;
				case KindDefense: StepOrbBuilder(LevyConstants.BeetleDefenseTicksPerOrb, LevyConstants.BeetleMaxOrbs, false); break;
				case KindSolar: StepOrbBuilder(LevyConstants.SolarTicksPerShield, LevyConstants.SolarMaxShields, true); break;
			}
			float enc = _setKind * 10 + _setOrbs;
			if (NPC.ai[0] != enc) { NPC.ai[0] = enc; NPC.netUpdate = true; }
		}

		private void SetOrbs(int n)
		{
			if (n == _setOrbs) return;
			_setOrbs = n;
			StatsDirty = true; // Beetle Might's melee bonus and the Solar dash read the count in RefreshStats
		}

		/// <summary>Beetle Might (Player.UpdateArmorSets): the counter bleeds 3 a tick plus countdown/20, melee damage feeds it, and 400 / 1600 / 5200 show 1 / 2 / 3 orbs.</summary>
		private void StepBeetleMight()
		{
			_setCounter -= LevyConstants.BeetleMightDecayPerTick;
			_setCounter -= _beetleCountdown / LevyConstants.BeetleMightCountdownDivisor;
			_beetleCountdown++;
			if (_setCounter < 0f) _setCounter = 0f;
			if (_setCounter > LevyConstants.BeetleMightCap) _setCounter = LevyConstants.BeetleMightCap;
			int n = _setCounter > LevyConstants.BeetleMightOrb3 ? 3 : _setCounter > LevyConstants.BeetleMightOrb2 ? 2 : _setCounter > LevyConstants.BeetleMightOrb1 ? 1 : 0;
			if (n < _setOrbs) _beetleCountdown = 0;
			else if (n > _setOrbs) _setCounter += LevyConstants.BeetleMightLevelUpBonus;
			SetOrbs(n);
		}

		/// <summary>Beetle Defense / Solar Flare: one more orb or shield every <paramref name="ticks"/> ticks, up to <paramref name="max"/>.</summary>
		private void StepOrbBuilder(int ticks, int max, bool burst)
		{
			_setCounter += 1f;
			if (_setCounter < ticks) return;
			if (_setOrbs < max)
			{
				SetOrbs(_setOrbs + 1);
				_setCounter = 0f;
				if (burst && !Main.dedServ)
				{
					for (int i = 0; i < 16; i++)
					{
						Dust dust = Main.dust[Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Torch, 0f, 0f, 100)];
						dust.noGravity = true; dust.scale = 1.7f; dust.fadeIn = 0.5f; dust.velocity *= 5f;
					}
				}
			}
			else _setCounter = ticks;
		}

		/// <summary>
		/// Writes the orbit state onto the dummy for the vanilla beetle / solar draw layers (they read beetleOrbs, beetlePos, solarShields and solarShieldPos) and runs vanilla's
		/// motion for them (the bodies of the beetles jitter around the wearer; the flare shield circles at his shoulder).
		/// </summary>
		private void ApplySetVisuals(Player d)
		{
			bool beetle = _setKind == KindMight || _setKind == KindDefense;
			d.beetleOffense = _setKind == KindMight;
			d.beetleDefense = _setKind == KindDefense;
			d.beetleOrbs = beetle ? _setOrbs : 0;
			d.solarShields = _setKind == KindSolar ? _setOrbs : 0;
			if (Main.dedServ) return;

			if (beetle)
			{
				d.beetleFrameCounter++;
				if (d.beetleFrameCounter >= 1) { d.beetleFrameCounter = 0; d.beetleFrame++; if (d.beetleFrame > 2) d.beetleFrame = 0; }
				for (int l = d.beetleOrbs; l < 3; l++) d.beetlePos[l] = Vector2.Zero;
				for (int m = 0; m < d.beetleOrbs; m++)
				{
					d.beetlePos[m] += d.beetleVel[m];
					d.beetleVel[m].X += Main.rand.Next(-100, 101) * 0.005f;
					d.beetleVel[m].Y += Main.rand.Next(-100, 101) * 0.005f;
					float x = d.beetlePos[m].X, y = d.beetlePos[m].Y, len = (float)Math.Sqrt(x * x + y * y);
					if (len > 100f)
					{
						len = 20f / len; x *= -len; y *= -len;
						d.beetleVel[m].X = (d.beetleVel[m].X * 9f + x) / 10f;
						d.beetleVel[m].Y = (d.beetleVel[m].Y * 9f + y) / 10f;
					}
					else if (len > 30f)
					{
						len = 10f / len; x *= -len; y *= -len;
						d.beetleVel[m].X = (d.beetleVel[m].X * 19f + x) / 20f;
						d.beetleVel[m].Y = (d.beetleVel[m].Y * 19f + y) / 20f;
					}
					if (d.beetleVel[m].Length() > 2f) d.beetleVel[m] *= 0.9f;
					d.beetlePos[m] -= d.velocity * 0.25f;
				}
			}
			if (d.solarShields > 0)
			{
				float turn = (Main.GameUpdateCount % 300u) / 100f * MathHelper.TwoPi;
				for (int n = d.solarShields; n < 3; n++) d.solarShieldPos[n] = Vector2.Zero;
				for (int n = 0; n < d.solarShields; n++)
				{
					d.solarShieldPos[n] += d.solarShieldVel[n];
					Vector2 v = (turn + n * (MathHelper.TwoPi / d.solarShields)).ToRotationVector2() * 6f;
					v.X = d.direction * 20;
					d.solarShieldVel[n] = (v - d.solarShieldPos[n]) * 0.2f;
				}
			}
		}

		/// <summary>The stat side of the set bonuses vanilla applies through buffs: called from ApplyPotionBuffs with the dummy mid-refresh (after the set pass, before the stats are read).</summary>
		private void ApplySetStats(Player d)
		{
			if (_setKind == KindMight && _setOrbs > 0)
			{
				d.GetDamage(DamageClass.Melee) += LevyConstants.BeetleMightBonusPerOrb * _setOrbs;
				d.GetAttackSpeed(DamageClass.Melee) += LevyConstants.BeetleMightBonusPerOrb * _setOrbs;
			}
			if (_nebulaLevel[1] > 0) d.GetDamage(DamageClass.Generic) += LevyConstants.NebulaDamagePerLevel * _nebulaLevel[1];
			if (d.shadowArmor) d.moveSpeed *= LevyConstants.ShadowArmorRunSpeed;
			if (_setKind == KindSolar && _setOrbs > 0) d.dashType = 3; // Player.UpdateArmorSets gives the Solar Flare dash while a shield is up; the shield count was reset by ResetEffects
		}

		/// <summary>Extra life-regeneration units a tick (120 = 1 HP) from Palladium's rapid healing and the Nebula life boosters; fed to StationRegen with the potion and station units.</summary>
		private int SetRegenUnits() => (_palladiumTicks > 0 ? LevyConstants.PalladiumRegenUnits : 0) + _nebulaLevel[0] * LevyConstants.NebulaLifeUnitsPerLevel;

		// ------------------------------------------------------------------------------------------------------------
		// Dealing damage: the on-hit set bonuses vanilla skips for npcProj shots and for a swing that is not a real item use
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>Called for every enemy hit by one of this soldier's projectiles (server / single player), from SoldierProjectileTag.OnHitNPC.</summary>
		public void OnProjectileHit(Projectile source, NPC target, int damageDone)
		{
			if (LevyUtil.IsClient || Dummy == null || Record == null || target == null) return;
			SpectreWrathOnHit(source, target, damageDone);
			SpectreHealOnHit(source, target, damageDone);
			SetHitEffects(target, damageDone, source);
		}

		/// <summary>Called for every enemy hit by this soldier's melee swing (server / single player), from the swing in SoldierNPC.Combat.cs.</summary>
		public void OnMeleeHit(NPC target, int damageDone)
		{
			if (LevyUtil.IsClient || Dummy == null || Record == null || target == null) return;
			SetHitEffects(target, damageDone, null);
		}

		/// <summary>Projectiles vanilla keeps out of Player.OnHit (Projectile.TryDoingOnHitEffects): the petal, leaf shot, stardust bolts, Titanium shards.</summary>
		private static bool IsOnHitExcluded(int type) => type == ProjectileID.FlowerPetal || type == ProjectileID.CrystalLeafShot || type == ProjectileID.StardustCellMinionShot
			|| type == 729 || type == 977 || type == ProjectileID.TitaniumStormShard || type == ModContent.ProjectileType<SoldierTitaniumShard>();

		private void SetHitEffects(NPC target, int damageDone, Projectile source)
		{
			Player d = Dummy;
			if (target.friendly || target.lifeMax <= 5) return; // vanilla's own gate for every on-hit effect: a real enemy, not a critter-sized target
			bool proj = source != null;
			DamageClass dc = proj ? (source.DamageType ?? DamageClass.Default) : DamageClass.Melee;
			bool melee = dc.CountsAsClass(DamageClass.Melee), ranged = dc.CountsAsClass(DamageClass.Ranged), magic = dc.CountsAsClass(DamageClass.Magic);

			// Projectile.StatusNPC skips npcProj shots: Frost armor's frostburn on melee / ranged shots (a melee swing already ran Player.StatusToNPC).
			if (proj && d.frostBurn && (melee || ranged) && !target.immortal)
				target.AddBuff(LevyConstants.FrostburnBuff, 60 * Main.rand.Next(LevyConstants.FrostburnMinSeconds, LevyConstants.FrostburnMaxSeconds));

			// Beetle Might: melee damage feeds the counter (x3 / x2 / x1 by orb count for a projectile, plain damage for a swing).
			if (_setKind == KindMight && melee && !target.immortal)
			{
				int mult = !proj ? 1 : _setOrbs == 0 ? 3 : _setOrbs == 1 ? 2 : 1;
				_setCounter += damageDone * mult;
				_beetleCountdown = 0;
			}

			// Nebula armor: a magic hit sometimes drops a booster, which a soldier simply absorbs.
			if (magic && d.setNebula && _nebulaGap == 0 && Main.rand.Next(LevyConstants.NebulaChanceDenominator) == 0)
			{
				_nebulaGap = LevyConstants.NebulaCooldownTicks;
				NebulaBooster(Main.rand.Next(3));
			}

			if (proj && IsOnHitExcluded(source.type)) return;
			// Player.OnHit_Inner
			if (d.onHitTitaniumStorm && _titaniumGap == 0 && target.type != NPCID.TargetDummy && !target.SpawnedFromStatue)
			{
				_titaniumGap = LevyConstants.TitaniumHitGapTicks;
				TitaniumStormTicks = LevyConstants.TitaniumStormTicks;
				AddTitaniumShard();
			}
			if (d.onHitDodge && _holyDodgeCooldown == 0 && _holyDodgeReady == 0) _holyDodgeReady = LevyConstants.HallowedReadyTicks;
			if (d.onHitRegen) _palladiumTicks = LevyConstants.PalladiumRegenTicks;
			if (d.onHitPetal && _petalGap == 0)
			{
				_petalGap = LevyConstants.OrichalcumPetalCooldownTicks;
				LaunchPetal(target);
			}
		}

		/// <summary>A Nebula booster by kind: 0 life, 1 damage (a level up to 3, 480 ticks, stepping down as it runs out), 2 mana (a mouthful of mana).</summary>
		private void NebulaBooster(int kind)
		{
			if (!Main.dedServ)
			{
				for (int i = 0; i < 8; i++)
				{
					Dust dust = Main.dust[Dust.NewDust(NPC.position, NPC.width, NPC.height, kind == 0 ? DustID.HealingPlus : DustID.Enchanted_Pink, 0f, -2f, 100, default, 1.1f)];
					dust.noGravity = true;
				}
			}
			if (kind == 2)
			{
				if (Record != null && Record.MaxMana > 0) { Record.Mana = Math.Min(Record.MaxMana, Record.Mana + 20); NPC.netUpdate = true; }
				return;
			}
			_nebulaLevel[kind] = Math.Min(LevyConstants.NebulaMaxLevel, _nebulaLevel[kind] + 1);
			_nebulaTicks[kind] = LevyConstants.NebulaBoosterTicks;
			if (kind == 1) StatsDirty = true;
		}

		/// <summary>Titanium storm: one more shard in this soldier's ring, up to the vanilla cap.</summary>
		private void AddTitaniumShard()
		{
			int type = ModContent.ProjectileType<SoldierTitaniumShard>(), n = 0;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.type == type && (int)p.ai[0] == NPC.whoAmI) n++;
			}
			if (n >= LevyConstants.TitaniumShardMax) return;
			int damage = Math.Max(1, (int)Math.Round(LevyConstants.TitaniumShardDamage * DamageMultiplier));
			WeaponUse.Spawn(this, NPC.Center, Vector2.Zero, type, damage, LevyConstants.TitaniumShardKnockback, DamageClass.Generic, NPC.whoAmI);
		}

		/// <summary>Orichalcum petal: vanilla throws it in from the edge of the screen at the struck enemy; here it starts a few hundred pixels off on the side he faces.</summary>
		private void LaunchPetal(NPC target)
		{
			int side = NPC.direction == 0 ? 1 : -NPC.direction;
			Vector2 from = target.Center + new Vector2(side * Main.rand.NextFloat(LevyConstants.OrichalcumPetalMinStartPx, LevyConstants.OrichalcumPetalMaxStartPx), Main.rand.NextFloat(-LevyConstants.OrichalcumPetalSpreadPx, LevyConstants.OrichalcumPetalSpreadPx));
			Vector2 aim = target.Center - from + new Vector2(Main.rand.Next(-50, 51), Main.rand.Next(-50, 51)) * 0.1f;
			Vector2 vel = aim.SafeNormalize(Vector2.UnitX) * LevyConstants.OrichalcumPetalSpeed;
			int damage = Math.Max(1, (int)Math.Round(LevyConstants.OrichalcumPetalDamage * DamageMultiplier));
			WeaponUse.Spawn(this, from, vel, ProjectileID.FlowerPetal, damage, 0f, DamageClass.Generic);
		}

		// ------------------------------------------------------------------------------------------------------------
		// Spectre Hood set bonus (Player.ghostHeal -> Projectile.ghostHeal -> ProjectileID.SpiritHeal)
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>
		/// Vanilla's ghostHeal for this body: a magic hit heals 20% of its damage (5% less for each target the shot already pierced), paid out of this soldier's own pool,
		/// and the heal rides a white bolt that flies back to HIM (vanilla picks the most hurt player in 3000 px; a soldier's hit heals the soldier who dealt it).
		/// </summary>
		public void SpectreHealOnHit(Projectile source, NPC target, int damageDone)
		{
			if (LevyUtil.IsClient || Dummy == null || !Dummy.ghostHeal) return;
			if (damageDone <= 1 || target.lifeMax <= 5 || target.immortal || !target.canGhostHeal || !source.friendly || source.hostile || !source.DamageType.CountsAsClass(DamageClass.Magic)) return;
			if (NPC.life >= NPC.lifeMax) return; // nothing to mend: keep the pool
			float fraction = LevyConstants.HoodHealFraction - source.numHits * LevyConstants.HoodHealPierceDecay;
			if (fraction <= 0f) return;
			float amount = damageDone * fraction;
			if ((int)amount <= 0 || _hoodPool <= 0f) return;
			_hoodPool -= amount;
			try
			{
				Projectile bolt = WeaponUse.Spawn(this, target.Center, Vector2.Zero, ModContent.ProjectileType<SoldierSpectreHeal>(), 1, 0f, DamageClass.Magic, NPC.whoAmI, amount);
				if (bolt != null) { bolt.damage = 0; bolt.friendly = false; } // a courier, not a hitter
			}
			catch (Exception e)
			{
				if (_accessoryProcWarned.Add(ItemID.SpectreHood)) Levy.Instance?.Logger.Warn("Levy: Spectre Hood bolt failed on a soldier: " + e.Message);
			}
		}

		// ------------------------------------------------------------------------------------------------------------
		// Taking damage: scale, dodge, thorns, shield share, orb loss
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>True while this soldier carries a Paladin's Shield (Paladin's / Frozen / Hero Shield) in one of his open accessory slots.</summary>
		public bool WearsPaladinShield
		{
			get
			{
				var g = Record?.Gear;
				if (g == null) return false;
				int slots = Math.Min(Record.AccessorySlots, g.Accessories.Length);
				for (int i = 0; i < slots; i++)
				{
					Item it = g.Accessories[i];
					if (it != null && !it.IsAir && (it.type == ItemID.PaladinsShield || it.type == ItemID.FrozenShield || it.type == ItemID.HeroShield)) return true;
				}
				return false;
			}
		}

		/// <summary>
		/// The multiplier a hit takes after the set bonuses (Player.ApplyVanillaHurtEffectModifiers): Solar shield 0.8, Beetle Defense 0.85 / 0.7 / 0.55 by orb, and 0.75 when a Paladin's
		/// Shield wearer is in range to share it. Call once per hit; remembers which Paladin will take the share (<see cref="SetsOnHurt"/>).
		/// </summary>
		internal float IncomingSetScale(bool allowPaladin = true)
		{
			_hitPaladin = null;
			float s = 1f;
			if (_setKind == KindSolar && _setOrbs > 0) s *= LevyConstants.SolarHitScale;
			if (_setKind == KindDefense && _setOrbs > 0) s *= 1f - LevyConstants.BeetleDefenseReductionPerOrb * _setOrbs;
			if (allowPaladin && !_soaking)
			{
				SoldierNPC p = FindPaladin();
				if (p != null) { s *= LevyConstants.PaladinAllyScale; _hitPaladin = p; }
			}
			return s;
		}

		/// <summary>The nearest other Paladin's Shield wearer of this soldier's commander within 800 px, above a quarter health and out of immune time (Player.OnHurt_Part2's pick).</summary>
		private SoldierNPC FindPaladin()
		{
			if (Record == null || Record.Mutinous) return null;
			SoldierNPC best = null; float bestDist = LevyConstants.PaladinRangePx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || i == NPC.whoAmI || n.ModNPC is not SoldierNPC p) continue;
				if (p.Record == null || p.Record.Mutinous || !p.IsAlive || p.ImmuneTime > 0 || p._soaking) continue;
				if (p.Record.CommanderName != Record.CommanderName) continue;
				if (p.NPC.life <= p.NPC.lifeMax * LevyConstants.PaladinHealthFloor || !p.WearsPaladinShield) continue;
				float dist = Vector2.Distance(p.NPC.Center, NPC.Center);
				if (dist < bestDist) { bestDist = dist; best = p; }
			}
			return best;
		}

		/// <summary>Hallowed armor's dodge (Player.ShadowDodge): if it is ready the hit is avoided, 80 ticks of immunity follow and the next one is 30 s away.</summary>
		internal bool TryShadowDodge()
		{
			if (_holyDodgeReady <= 0 || Dummy == null || !Dummy.onHitDodge) return false;
			_holyDodgeReady = 0;
			_holyDodgeCooldown = LevyConstants.HallowedCooldownTicks;
			ImmuneTime = LevyConstants.HallowedDodgeImmuneTicks;
			if (!Main.dedServ)
			{
				for (int i = 0; i < 30; i++)
				{
					Dust dust = Main.dust[Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, 0f, 0f, 100, default, 1.6f)];
					dust.noGravity = true; dust.velocity *= 2.5f;
				}
			}
			NPC.netUpdate = true;
			return true;
		}

		/// <summary>Thorns: contact damage thrown back at the attacker (Turtle armor 200% of the hit, the Cactus set a flat 15 / 30 / 45), as Player.Update_NPCCollision does for a player.</summary>
		private void ReflectThorns(NPC attacker, int rawDamage, int hitDirection)
		{
			Player d = Dummy;
			if (d == null || attacker == null || attacker.dontTakeDamage || attacker.friendly) return;
			float mult = d.turtleThorns ? LevyConstants.TurtleThornsMult : d.thorns;
			if (mult > 0f) StrikeBack(attacker, Math.Min(LevyConstants.ThornsDamageCap, (int)(rawDamage * mult)), hitDirection);
			if (d.cactusThorns) StrikeBack(attacker, Main.masterMode ? LevyConstants.CactusThornsDamageMaster : Main.expertMode ? LevyConstants.CactusThornsDamageExpert : LevyConstants.CactusThornsDamage, hitDirection);
		}

		private void StrikeBack(NPC attacker, int damage, int hitDirection)
		{
			if (damage <= 0 || !attacker.active) return;
			LootCredit.Credit(attacker, this);
			int done = attacker.SimpleStrikeNPC(damage, hitDirection, false, LevyConstants.ThornsKnockback, DamageClass.Generic, false, 0f, true);
			LootCredit.OnSoldierHit(attacker, this, done);
		}

		/// <summary>
		/// After a hit has landed (called from AfterHit): the Paladin's Shield wearer takes his third, a Solar shield bursts into a flare, a Beetle Defense orb pops
		/// (Player.OnHurt_Part2 / ConsumeSolarFlare).
		/// </summary>
		private void SetsOnHurt(int damageDone, int attackerIndex)
		{
			SoldierNPC paladin = _hitPaladin; _hitPaladin = null;
			if (paladin != null && damageDone > 0)
			{
				try { paladin.TakeSoak(Math.Max(1, (int)(damageDone * LevyConstants.PaladinShareFraction)), attackerIndex, this); }
				catch (Exception e) { if (_accessoryProcWarned.Add(ItemID.PaladinsShield)) Levy.Instance?.Logger.Warn("Levy: Paladin's Shield share failed: " + e.Message); }
			}
			if (_setKind == KindSolar && _setOrbs > 0)
			{
				SetOrbs(_setOrbs - 1);
				_setCounter = 0f;
				if (IsAlive) DetonateSolarFlare();
			}
			else if (_setKind == KindDefense && _setOrbs > 0)
			{
				SetOrbs(_setOrbs - 1);
				_setCounter = 0f;
			}
		}

		/// <summary>The Solar Flare burst (projectile 608, a 160 px blast of 150 melee damage with 15 knockback), detonated on the spot.</summary>
		private void DetonateSolarFlare()
		{
			try
			{
				int damage = Math.Max(1, (int)Math.Round(Dummy.GetTotalDamage(DamageClass.Melee).ApplyTo(LevyConstants.SolarFlareDamage) * DamageMultiplier));
				Projectile flare = WeaponUse.Spawn(this, NPC.Center, Vector2.Zero, LevyConstants.SolarFlareProjectile, damage, LevyConstants.SolarFlareKnockback, DamageClass.Melee);
				flare?.Kill();
			}
			catch (Exception e)
			{
				if (_accessoryProcWarned.Add(ItemID.SolarFlareBreastplate)) Levy.Instance?.Logger.Warn("Levy: Solar flare burst failed on a soldier: " + e.Message);
			}
		}

		/// <summary>The Paladin's share: this soldier takes <paramref name="amount"/> (through his own defense) for an ally, and is credited with nothing but the wound.</summary>
		private void TakeSoak(int amount, int attackerIndex, SoldierNPC ally)
		{
			if (amount <= 0 || !IsAlive || ImmuneTime > 0 || _soaking) return;
			LastAttackerIndex = ally.LastAttackerIndex; LastAttackerType = ally.LastAttackerType; LastAttackerName = ally.LastAttackerName; // the enemy that hit the ally is who hurt him
			int done;
			_soaking = true; _contactStriking = true;
			PendingContactScale = IncomingSetScale(false);
			try { done = NPC.SimpleStrikeNPC(amount, 0, false, 0f, null, false, 0f, true); }
			finally { PendingContactScale = 1f; _contactStriking = false; _soaking = false; }
			AfterHit(done, attackerIndex);
			if (Main.dedServ) return;
			// a gold thread from the shield to the soldier it is covering
			Vector2 from = NPC.Center, to = ally.NPC.Center;
			for (int i = 0; i < LevyConstants.PaladinBeamDust; i++)
			{
				Dust dust = Main.dust[Dust.NewDust(Vector2.Lerp(from, to, i / (float)LevyConstants.PaladinBeamDust), 0, 0, DustID.GoldFlame, 0f, 0f, 100, default, 1.2f)];
				dust.noGravity = true; dust.velocity *= 0.2f;
			}
		}
	}

	/// <summary>Applies the contact-hit scale (<see cref="SoldierNPC.PendingContactScale"/>) to a soldier's strike: the one place the vanilla hurt-effect multipliers (Solar, Beetle, Paladin) join the defense formula.</summary>
	public sealed class SoldierSetHitScale : GlobalNPC
	{
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == ModContent.NPCType<SoldierNPC>();

		public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
		{
			if (npc.ModNPC is SoldierNPC s && s.PendingContactScale != 1f) modifiers.FinalDamage *= s.PendingContactScale;
		}
	}
}
