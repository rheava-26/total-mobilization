using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Dialogue;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// HELD-PROJECTILE WEAPONS (aiStyle HeldProjectile + channel: Phantasm, Vortex Beater, Last Prism, Charged Blaster Cannon, Laser Machinegun,
	/// Phantom Phoenix, Starlight, Solar Eruption...). The weapon's REAL vanilla held AI runs, anchored to the soldier, with its signature behaviour intact.
	///
	/// HOW IT IS ANCHORED. The holder is spawned with owner = <see cref="SoldierNPC.DummySlot"/> (255), tagged with the soldier. Every Projectile.AI call of a
	/// chain projectile is wrapped (On_Projectile.AI detour, try/finally): the soldier's Dummy is put into Main.player[255] through EnemyTargeting.Swap (the same
	/// swap enemies use), so the vanilla code that reads Main.player[owner] sees the soldier. For the holder's own AI the wrapper also fakes the few inputs the
	/// vanilla code reads from the local player's machine, all restored in the same finally:
	///  * Main.myPlayer = 255 (server/single player only) - the holder fires only when Main.myPlayer == owner;
	///  * Main.mouseX/mouseY - the fake cursor: vanilla aims at screenPosition + mouse, we point it at the soldier's target (lead + grade wobble);
	///  * the dummy's channel / controlUseItem (true while combat keeps feeding the holder), itemAnimationMax, selected item, mana, the pouch in ammo slot 54
	///    (the real pouch Item, so vanilla PickAmmo consumes the soldier's own ammo) and a damage multiplier for the soldier's grade.
	/// Projectiles the held AI spawns (arrows, the big phoenix, rockets) are re-owned and tagged exactly like every other soldier shot; a spawned held/thick-laser
	/// child (Last Prism / Charged Blaster beam) stays owner 255 and joins the chain. Projectile.Damage of chain projectiles is wrapped the same way, because vanilla
	/// only damages for owner == Main.myPlayer.
	/// SLOT 255 DISCIPLINE: Swap in try, Unswap in finally, EnsureSlotRestored in the finally of every wrapper plus PreUpdateProjectiles/PostUpdateProjectiles.
	/// </summary>
	public static class HeldWeapons
	{
		private const int Slot = SoldierNPC.DummySlot;
		private static readonly Dictionary<int, int> _holder = new();
		private static readonly Dictionary<int, uint> _fed = new(), _spawned = new();
		private static bool[] _before;

		internal static void Reset() { _holder.Clear(); _fed.Clear(); _spawned.Clear(); }

		// =====================================================================================================
		// Combat side: spawn the holder once, then just keep feeding it.
		// =====================================================================================================
		/// <summary>Called by WeaponUse every tick the soldier wants to fire a held weapon (range and line of sight already passed in Combat).</summary>
		internal static bool TryFire(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			if (item.shoot <= 0) return WeaponUse.Dud(s, item);
			if (!WeaponUse.HasResources(s, item, profile)) return false;
			int idx = s.NPC.whoAmI;
			uint now = Main.GameUpdateCount;
			_fed[idx] = now;

			Vector2 toTarget = target.Center - s.NPC.Center;
			if (toTarget == Vector2.Zero) toTarget = Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction);
			s.AimRotation = toTarget.ToRotation();
			if (Math.Abs(toTarget.X) > 0.01f) s.NPC.direction = toTarget.X < 0f ? -1 : 1;
			if (s.ItemAnimation < LevyConstants.HeldPoseHoldTicks) s.ItemAnimation = s.ItemAnimationMax = LevyConstants.HeldPoseHoldTicks;
			if ((now & 15u) == 0u) s.NPC.netUpdate = true;
			s.AttackCooldown = 1; // fed every tick; the weapon's own cadence lives in the held AI

			if (LiveHolder(s, item) != null) return true;
			if (_spawned.TryGetValue(idx, out uint last) && now - last < (uint)LevyConstants.HeldRespawnMinTicks) return false;
			_spawned[idx] = now;

			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier));
			Vector2 dir = Vector2.Normalize(toTarget);
			// owner 255 is deliberate (see class summary); the holder's real owner-gated code runs under the wrapper's Main.myPlayer swap.
			int i = Projectile.NewProjectile(s.NPC.GetSource_FromAI(), s.NPC.Center, dir * Math.Max(1f, item.shootSpeed), item.shoot, damage, item.knockBack, Slot);
			if (i < 0 || i >= Main.maxProjectiles) return WeaponUse.Dud(s, item);
			Projectile p = Main.projectile[i];
			if (!p.active || p.type != item.shoot) return WeaponUse.Dud(s, item);
			JoinChain(s, p, item);
			p.direction = dir.X < 0f ? -1 : 1;
			_holder[idx] = i;
			return true;
		}

		/// <summary>This soldier's live holder for <paramref name="item"/>, or null (one per soldier).</summary>
		internal static Projectile LiveHolder(SoldierNPC s, Item item)
		{
			int idx = s.NPC.whoAmI;
			if (!_holder.TryGetValue(idx, out int pi)) return null;
			if (pi >= 0 && pi < Main.maxProjectiles)
			{
				Projectile p = Main.projectile[pi];
				if (p.active && p.type == item.shoot && p.TryGetGlobalProjectile(out HeldWeaponProjectile g) && g.Chain && g.ItemType == item.type
					&& ReferenceEquals(g.Record, s.Record) && p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex == idx)
					return p;
			}
			_holder.Remove(idx);
			return null;
		}

		/// <summary>Marks a projectile as part of this soldier's held-weapon chain: soldier tag, chain flag, item, record. Owner is left alone (255).</summary>
		private static void JoinChain(SoldierNPC s, Projectile p, Item item)
		{
			p.npcProj = true;
			p.noDropItem = true;
			if (item.DamageType != null) p.DamageType = item.DamageType;
			if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) tag.SoldierIndex = s.NPC.whoAmI;
			if (p.TryGetGlobalProjectile(out HeldWeaponProjectile g)) { g.Chain = true; g.ItemType = item.type; g.Record = s.Record; }
			// every chain projectile shares owner 255: give piercing ones their own local immunity so soldiers do not share hit cooldowns
			if (p.penetrate != 1 && !p.usesLocalNPCImmunity && !p.usesIDStaticNPCImmunity) { p.usesLocalNPCImmunity = true; if (p.localNPCHitCooldown <= 0) p.localNPCHitCooldown = 10; }
			p.netUpdate = true;
		}

		// =====================================================================================================
		// Projectile.AI / Projectile.Damage wrappers.
		// =====================================================================================================
		private static SoldierNPC BodyOf(Projectile p, HeldWeaponProjectile g)
		{
			if (!p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) return null;
			int idx = tag.SoldierIndex;
			if (idx < 0 || idx >= Main.maxNPCs) return null;
			NPC n = Main.npc[idx];
			if (!n.active || n.ModNPC is not SoldierNPC s || s.Record == null || s.Dummy == null || !s.IsAlive) return null;
			if (g.Record != null && !ReferenceEquals(g.Record, s.Record)) return null; // the NPC slot was reused by another soldier
			return s;
		}

		internal static void RunAi(On_Projectile.orig_AI orig, Projectile p, HeldWeaponProjectile g)
		{
			bool authority = LevyUtil.IsServerOrSingle;
			SoldierNPC body = BodyOf(p, g);
			Item item = body != null ? WeaponUse.HeldItem(body) : null;
			if (authority && (body == null || item == null || item.IsAir || item.type != g.ItemType))
			{
				// soldier dead / dismissed / swapped weapons: the hold ends and the holder (and any beam it owns) goes
				p.Kill();
				return;
			}
			if (body == null) { p.timeLeft = Math.Max(p.timeLeft, 2); return; } // client that has not met the body yet: no AI against the null player

			bool holder = p.aiStyle == ProjAIStyleID.HeldProjectile;
			Player d = body.Dummy;
			bool swapped = false;
			bool touched = false;
			int savedMyPlayer = Main.myPlayer, savedMouseX = Main.mouseX, savedMouseY = Main.mouseY;
			bool savedChannel = false, savedUse = false, savedNoItems = false;
			int savedAnimMax = 0, savedMana = 0, savedManaMax = 0, savedSelected = 0;
			Item saved0 = null, saved54 = null;
			StatModifier savedDamage = default;
			Item pouch = null;
			int pouchStackBefore = 0;
			bool[] before = null;
			bool manaWeapon = false;
			try
			{
				EnemyTargeting.Swap(body); // dummy -> Main.player[255]
				swapped = true;
				if (!holder || !authority) { orig(p); return; } // beams and client copies: only the anchor

				// ---- holder on the server / single player: set the dummy up as a player holding the use button ----
				uint now = Main.GameUpdateCount;
				int idx = body.NPC.whoAmI;
				uint since = _fed.TryGetValue(idx, out uint f) ? now - f : uint.MaxValue;
				if (since > (uint)LevyConstants.HeldIdleKillTicks) { p.Kill(); return; }
				bool feed = since <= (uint)LevyConstants.HeldFeedGraceTicks;

				savedChannel = d.channel; savedUse = d.controlUseItem; savedNoItems = d.noItems; savedAnimMax = d.itemAnimationMax;
				savedMana = d.statMana; savedManaMax = d.statManaMax2; savedSelected = d.selectedItem;
				saved0 = d.inventory[0]; saved54 = d.inventory[54];
				savedDamage = d.GetDamage(DamageClass.Generic);
				touched = true;

				d.noItems = false;
				d.selectedItem = 0;
				d.inventory[0] = item;
				d.channel = feed;
				d.controlUseItem = feed;
				d.itemAnimationMax = Math.Max(1, CombinedHooks.TotalAnimationTime(Math.Max(1, item.useAnimation), d, item));
				manaWeapon = item.mana > 0;
				d.statManaMax2 = Math.Max(1, body.Record.MaxMana);
				d.statMana = Math.Clamp(body.Record.Mana, 0, d.statManaMax2);
				pouch = body.Record.Gear.Ammo;
				if (item.useAmmo > 0 && pouch != null && !pouch.IsAir && pouch.stack > 0 && pouch.ammo == item.useAmmo) { d.inventory[54] = pouch; pouchStackBefore = pouch.stack; }
				else pouch = null;
				d.GetDamage(DamageClass.Generic) = savedDamage.CombineWith(new StatModifier(1f, Math.Max(0.01f, body.DamageMultiplier)));

				Vector2 dir = AimDirection(body, g, item, pouch);
				body.AimRotation = dir.ToRotation();
				p.direction = dir.X < 0f ? -1 : 1;
				Vector2 cursor = body.NPC.Center + dir * Math.Max(LevyConstants.HeldAimMinDistPx, AimDistance(body));
				Main.mouseX = (int)(cursor.X - Main.screenPosition.X);
				Main.mouseY = (int)(cursor.Y - Main.screenPosition.Y);
				Main.myPlayer = Slot; // the holder only fires for owner == Main.myPlayer

				before = _before ??= new bool[Main.maxProjectiles];
				for (int i = 0; i < Main.maxProjectiles; i++) before[i] = Main.projectile[i].active;
				orig(p);
			}
			finally
			{
				Main.myPlayer = savedMyPlayer; Main.mouseX = savedMouseX; Main.mouseY = savedMouseY;
				try
				{
					if (touched)
					{
						if (manaWeapon) body.Record.Mana = Math.Clamp(d.statMana, 0, Math.Max(0, body.Record.MaxMana));
						d.channel = savedChannel; d.controlUseItem = savedUse; d.noItems = savedNoItems; d.itemAnimationMax = savedAnimMax;
						d.statMana = savedMana; d.statManaMax2 = savedManaMax; d.selectedItem = savedSelected;
						d.inventory[0] = saved0; d.inventory[54] = saved54;
						d.GetDamage(DamageClass.Generic) = savedDamage;
						if (pouch != null && (pouch.IsAir || pouch.stack <= 0))
						{
							if (!pouch.IsAir) pouch.TurnToAir();
							DialogueSystem.Bark(body, DialogueTrigger.OUT_OF_AMMO);
							body.NPC.netUpdate = true;
						}
						else if (pouch != null && pouch.stack != pouchStackBefore) body.NPC.netUpdate = true;
						if (before != null) AdoptChildren(body, p, item, before);
					}
				}
				catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn("Levy: held weapon cleanup failed: " + e.Message); }
				finally
				{
					if (swapped) EnemyTargeting.Unswap();
					EnemyTargeting.EnsureSlotRestored(); // never leave a dummy in Main.player[255]
				}
			}
		}

		/// <summary>Projectile.Damage of a chain projectile (holder contact damage, thick-laser beam): vanilla only damages for owner == Main.myPlayer, so the server/single player claims 255 for the call.</summary>
		internal static void RunDamage(On_Projectile.orig_Damage orig, Projectile p, HeldWeaponProjectile g)
		{
			SoldierNPC body = BodyOf(p, g);
			if (body == null || !LevyUtil.IsServerOrSingle) { orig(p); return; }
			int savedMyPlayer = Main.myPlayer;
			bool swapped = false;
			try
			{
				EnemyTargeting.Swap(body);
				swapped = true;
				Main.myPlayer = Slot;
				orig(p);
			}
			finally
			{
				Main.myPlayer = savedMyPlayer;
				if (swapped) EnemyTargeting.Unswap();
				EnemyTargeting.EnsureSlotRestored();
			}
		}

		/// <summary>New projectiles the held AI spawned: ordinary shots are re-owned and tagged like any soldier shot; held / thick-laser children stay in the chain.</summary>
		private static void AdoptChildren(SoldierNPC body, Projectile holder, Item item, bool[] before)
		{
			DamageClass cls = item.DamageType ?? DamageClass.Default;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile c = Main.projectile[i];
				if (!c.active || before[i] || i == holder.whoAmI) continue;
				if (c.aiStyle == ProjAIStyleID.HeldProjectile || c.aiStyle == ProjAIStyleID.ThickLaser) JoinChain(body, c, item);
				else WeaponUse.TagSoldierProjectile(body, c, cls);
			}
		}

		// =====================================================================================================
		// Aim
		// =====================================================================================================
		private static NPC TargetOf(SoldierNPC body)
		{
			int ti = body.TargetIndex;
			if (ti < 0 || ti >= Main.maxNPCs) return null;
			NPC t = Main.npc[ti];
			return t.active && t.life > 0 ? t : null;
		}

		private static float AimDistance(SoldierNPC body)
		{
			NPC t = TargetOf(body);
			return t == null ? LevyConstants.HeldAimMinDistPx : Vector2.Distance(body.NPC.Center, t.Center);
		}

		/// <summary>Unit direction the fake cursor points: at the soldier's target with lead (ammo shots, incl. arrow drop) plus the grade-based wobble (re-rolled every few ticks, not every tick, so beams do not flicker).</summary>
		private static Vector2 AimDirection(SoldierNPC body, HeldWeaponProjectile g, Item item, Item pouch)
		{
			Vector2 c = body.NPC.Center;
			NPC t = TargetOf(body);
			if (t == null)
			{
				if (g.AimDir == Vector2.Zero) g.AimDir = Vector2.UnitX * (body.NPC.direction == 0 ? 1 : body.NPC.direction);
				return g.AimDir;
			}
			NPC seg = Aiming.NearestSegment(t, c) ?? t;
			Vector2 dir;
			if (pouch != null)
			{
				int proj = WeaponUse.ResolveProjectile(body, item, pouch);
				float speed = Math.Max(LevyConstants.HeldLeadSpeedMin, item.shootSpeed * LevyConstants.HeldLeadSpeedFactor + pouch.shootSpeed);
				bool gravity = WeaponClassifier.ProjectileHasGravity(proj);
				Vector2 v = Aiming.SolveIntercept(c, speed, seg.Center, seg.velocity, gravity, gravity ? LevyConstants.ArrowGravity : 0f, gravity ? LevyConstants.ArrowGravityDelayTicks : 0);
				dir = v.LengthSquared() > 0.0001f ? Vector2.Normalize(v) : Vector2.UnitX;
			}
			else
			{
				Vector2 d = seg.Center - c;
				dir = d.LengthSquared() > 0.0001f ? Vector2.Normalize(d) : Vector2.UnitX * (body.NPC.direction == 0 ? 1 : body.NPC.direction);
			}
			if (!(body.Weapon?.HeldMelee ?? false))
			{
				if (++g.JitterTick >= LevyConstants.HeldAimJitterRefreshTicks)
				{
					g.JitterTick = 0;
					int grade = Math.Clamp((int)body.Grade, 0, LevyConstants.GradeAimJitterDeg.Length - 1);
					float deg = LevyConstants.GradeAimJitterDeg[grade] * WeaponUse.AimErrorMultiplier(body) * Aiming.LongRangeErrorScale(Vector2.Distance(c, seg.Center));
					g.JitterRad = MathHelper.ToRadians(Main.rand.NextFloat(-deg, deg));
				}
				if (g.JitterRad != 0f) dir = dir.RotatedBy(g.JitterRad);
			}
			g.AimDir = dir;
			return dir;
		}
	}

	/// <summary>Per-projectile chain data: whether this projectile belongs to a soldier's held weapon, which item, and the aim wobble state. Chain + item are synced so clients anchor the holder to the soldier too.</summary>
	public class HeldWeaponProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		public bool Chain;
		public int ItemType;
		/// <summary>Server-side identity of the soldier record that fired this (guards NPC-slot reuse). Never synced.</summary>
		public SoldierRecord Record;
		public Vector2 AimDir;
		public float JitterRad;
		public int JitterTick;

		public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
		{
			binaryWriter.Write(Chain);
			if (Chain) binaryWriter.Write(ItemType);
		}

		public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
		{
			Chain = binaryReader.ReadBoolean();
			if (Chain) ItemType = binaryReader.ReadInt32();
		}

		/// <summary>The soldier's dummy must not keep pointing at a dead holder (the dummy's held-projectile layer would draw whatever reuses that slot).</summary>
		public override void OnKill(Projectile projectile, int timeLeft)
		{
			if (!Chain || !projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag)) return;
			int idx = tag.SoldierIndex;
			if (idx < 0 || idx >= Main.maxNPCs) return;
			if (Main.npc[idx].ModNPC is SoldierNPC s && s.Dummy != null && s.Dummy.heldProj == projectile.whoAmI) s.Dummy.heldProj = -1;
		}
	}

	/// <summary>Installs the Projectile.AI / Projectile.Damage wrappers and keeps slot 255 clean around the projectile phase.</summary>
	public class HeldWeaponSystem : ModSystem
	{
		public override void Load()
		{
			On_Projectile.AI += AiDetour;
			On_Projectile.Damage += DamageDetour;
		}

		public override void Unload()
		{
			On_Projectile.AI -= AiDetour;
			On_Projectile.Damage -= DamageDetour;
			HeldWeapons.Reset();
		}

		public override void OnWorldLoad() => HeldWeapons.Reset();
		public override void OnWorldUnload() { HeldWeapons.Reset(); EnemyTargeting.EnsureSlotRestored(); }

		public override void PreUpdateProjectiles() => EnemyTargeting.EnsureSlotRestored();

		public override void PostUpdateProjectiles()
		{
			EnemyTargeting.EnsureSlotRestored();
			// a dummy whose holder is gone must not keep a stale held-projectile index
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC s || s.Dummy == null || s.Dummy.heldProj < 0) continue;
				int hp = s.Dummy.heldProj;
				if (hp >= Main.maxProjectiles || !Main.projectile[hp].active || !Main.projectile[hp].TryGetGlobalProjectile(out HeldWeaponProjectile g) || !g.Chain) s.Dummy.heldProj = -1;
			}
		}

		private static void AiDetour(On_Projectile.orig_AI orig, Projectile self)
		{
			if (self.owner == SoldierNPC.DummySlot && self.TryGetGlobalProjectile(out HeldWeaponProjectile g) && g.Chain) HeldWeapons.RunAi(orig, self, g);
			else orig(self);
		}

		private static void DamageDetour(On_Projectile.orig_Damage orig, Projectile self)
		{
			if (self.owner == SoldierNPC.DummySlot && self.TryGetGlobalProjectile(out HeldWeaponProjectile g) && g.Chain) HeldWeapons.RunDamage(orig, self, g);
			else orig(self);
		}
	}
}
