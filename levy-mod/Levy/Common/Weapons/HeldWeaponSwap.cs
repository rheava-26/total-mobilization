using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
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
	/// child (Last Prism / Charged Blaster beam) stays owner 255 and joins the chain. Projectile.Damage of chain projectiles is NOT run under the dummy: vanilla only damages
	/// for owner == Main.myPlayer, so for that one call the projectile is handed to the real player (what every other soldier shot is owned by) and a boss-killing hit resolves
	/// its whole death cascade in the real player context (see <see cref="RunDamage"/>).
	/// SLOT 255 DISCIPLINE: Swap in try, Unswap in finally, EnsureSlotRestored in the finally of every wrapper plus PreUpdateProjectiles/PostUpdateProjectiles.
	/// </summary>
	public static class HeldWeapons
	{
		private const int Slot = SoldierNPC.DummySlot;
		private static readonly Dictionary<int, int> _holder = new();
		private static readonly Dictionary<int, uint> _fed = new(), _spawned = new();
		private static bool[] _before;

		internal static void Reset() { _holder.Clear(); _fed.Clear(); _spawned.Clear(); _altNext.Clear(); _countStamp.Clear(); ArkSwings.Reset(); BiomeBlades.Reset(); RealWhips.Reset(); }

		private static readonly Dictionary<int, uint> _countStamp = new();
		/// <summary>Once a tick per soldier: the dummy's <c>ownedProjectileCounts</c> recounted from the projectiles tagged to this soldier (what Player.Update does for a real player).</summary>
		private static void CountOwned(SoldierNPC body)
		{
			uint now = Main.GameUpdateCount; int idx = body.NPC.whoAmI;
			if (_countStamp.TryGetValue(idx, out uint st) && st == now) return;
			_countStamp[idx] = now;
			int[] c = body.Dummy.ownedProjectileCounts;
			Array.Clear(c, 0, c.Length);
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile q = Main.projectile[i];
				if (!q.active || q.type >= c.Length || !q.TryGetGlobalProjectile(out SoldierProjectileTag t) || t.SoldierIndex != idx) continue;
				c[q.type]++;
			}
		}

		// =====================================================================================================
		// Combat side: spawn the holder once, then just keep feeding it.
		// =====================================================================================================
		/// <summary>Called by WeaponUse every tick the soldier wants to fire a held weapon (range and line of sight already passed in Combat).</summary>
		internal static bool TryFire(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			if (ModShotKinds.Of(item) == ModShotKind.ArkSwing) return ArkSwings.TryFire(s, item, profile, target); // an Ark is swung and parried use by use, not held
			if (ModShotKinds.Of(item) == ModShotKind.BiomeBlade) return BiomeBlades.TryFire(s, item, profile, target); // a Biome Blade attunes to the biome and uses that attunement's blade (BiomeBlades)
			if (item.shoot <= 0) return WeaponUse.Dud(s, item);
			if (!WeaponUse.HasResources(s, item, profile)) { DudTrace.Note(item, "held-nores"); return false; }
			int idx = s.NPC.whoAmI;
			uint now = Main.GameUpdateCount;
			_fed[idx] = now;
			if (DudTrace.On) { float dt = Aiming.TilesToHitbox(s.NPC.Center, target); DudTrace.Note(item, dt < 4 ? "fed-dist<4" : dt < 8 ? "fed-dist4-8" : dt < 16 ? "fed-dist8-16" : "fed-dist>16"); }

			Vector2 toTarget = target.Center - s.NPC.Center;
			if (toTarget == Vector2.Zero) toTarget = Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction);
			s.AimRotation = toTarget.ToRotation();
			if (Math.Abs(toTarget.X) > 0.01f) s.NPC.direction = toTarget.X < 0f ? -1 : 1;
			if (s.ItemAnimation < LevyConstants.HeldPoseHoldTicks) s.ItemAnimation = s.ItemAnimationMax = LevyConstants.HeldPoseHoldTicks;
			if ((now & 15u) == 0u) s.NPC.netUpdate = true;
			s.AttackCooldown = 1; // fed every tick; the weapon's own cadence lives in the held AI

			Projectile live = LiveHolder(s, item);
			if (live != null)
			{
				// the right-click is wanted while the left holdout is held: a player lets go and right-clicks (two uses can't overlap), so the levy lets go now and the next use is the alt (HeldAlt)
				if (item.type >= ItemID.Count && now >= s.AltReadyTick && live.TryGetGlobalProjectile(out HeldWeaponProjectile lg) && lg.ModHolder && !lg.AltHolder && !lg.Released && !lg.BoundChild && lg.Ark == ArkRole.None && lg.Biome == 0
					&& HeldAlt.Want(s, item, target))
				{ lg.CycleRelease = true; lg.Released = true; lg.ReleasedAt = lg.HeldTicks; lg.Linger = 2; _altNext[idx] = now; }
				return true;
			}
			if (_spawned.TryGetValue(idx, out uint last) && now - last < (uint)Math.Max(LevyConstants.HeldRespawnMinTicks, WeaponUse.SwingFloor(item))) return false;
			if (ModShotKinds.StartWait(item, s.Record.Mana, s.Record.MaxMana)) { DudTrace.Note(item, "held-start-wait"); return false; } // a drain-in-AI holdout starts on a full pool (ModShotKinds.StartFractionTable)
			_spawned[idx] = now;

			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier));
			Vector2 dir = Vector2.Normalize(toTarget);
			AltPolicies.OnHoldoutSpawn(s, item, target); // the held right button (variant) is set before the holdout exists
			int holderType = ModShotKinds.HolderType(item);
			// the right-click of a held modded weapon is a use of its own, made the way the left one is (the item's hooks with the dummy's alt button down) and held in this levy's chain like it (WeaponUse.TryAltAttack hands it here)
			bool alt = item.type >= ItemID.Count && ((_altNext.TryGetValue(idx, out uint an) && now - an < 60u) || HeldAlt.Want(s, item, target));
			_altNext.Remove(idx);
			// owner 255 is deliberate (see class summary); the holder's real owner-gated code runs under the wrapper's Main.myPlayer swap.
			// A MODDED holdout is made by its item's own Shoot hook (Calamity's guns pass the holdout its gun type / mode in ai[], and some fire real shots from the hook itself): run the hook, with the dummy as the player.
			// A holder the mod spawns for whoever HOLDS the item (ModShotKinds.HoldSpawned: the item's use is shut) is spawned here directly, at the levy, as the mod spawns it for a player.
			int ownerSlot = OwnerSlotFor(item);
			bool madeAny = false;
			Projectile p = item.type >= ItemID.Count && (alt || !ModShotKinds.HoldSpawned(item)) ? SpawnViaHooks(s, item, dir * Math.Max(1f, item.shootSpeed), damage, ownerSlot, alt, out madeAny) : null;
			if (alt) HeldAlt.Fired(s, item, p != null || madeAny);
			if (p == null && madeAny)
			{
				// the hook made the use's shots itself and no holder (the Surge Driver's right-button bullets, a variant fired straight from the hook): they are ordinary soldier shots (SpawnViaHooks tagged them), and the next use waits
				// the respawn gap like any other
				DudTrace.Note(item, "held-hook-shots");
				return true;
			}
			int i = p?.whoAmI ?? -1;
			if (p == null && alt) return false; // an alt whose hook made nothing: the left use goes next tick
			if (p == null)
			{
				bool swappedForSpawn = OwnerPuppet.FacingFixOn && ownerSlot == Slot && !EnemyTargeting.IsSwapped; // the holder's spawn hooks (every mod's OnSpawn) read slot 255: the levy's dummy stands there for the spawn, as it does for its AI
				try
				{
					if (swappedForSpawn) EnemyTargeting.Swap(s);
					bool idle = ModShotKinds.HoldSpawned(item) && holderType != item.shoot;
					i = Projectile.NewProjectile(idle ? s.Dummy.GetSource_ItemUse(item) : s.NPC.GetSource_FromAI(), s.NPC.Center, idle ? dir : dir * Math.Max(1f, item.shootSpeed), holderType, damage, item.knockBack, ownerSlot);
				}
				finally { if (swappedForSpawn) { EnemyTargeting.Unswap(); EnemyTargeting.EnsureSlotRestored(); } }
				if (i < 0 || i >= Main.maxProjectiles) return WeaponUse.Dud(s, item);
				p = Main.projectile[i];
			}
			if (!p.active || (p.type != holderType && !_altHolder)) { DudTrace.Note(item, "held-spawn-dud"); _altHolder = false; return WeaponUse.Dud(s, item); }
			DudTrace.Note(item, "held-spawn");
			if (DudTrace.On && ProjectileID.Sets.NeedsUUID[p.type]) DudTrace.Note(item, "uuid who=" + p.whoAmI + " id=" + p.identity + " uuid=" + p.projUUID + " lookup=" + Projectile.GetByUUID(p.owner, p.whoAmI) + " owner=" + p.owner);
			SetUpHolder(s, item, p, dir);
			if (p.TryGetGlobalProjectile(out HeldWeaponProjectile ag)) { ag.AltHolder = _altHolder; ag.SpawnRight = AltPolicies.MouseRightHeld(s); }
			_altHolder = false;
			// a weapon whose Shoot hook makes more than one holdout (Wingman holds one gun in each hand) has them all in the chain, fed and ended together: a second holdout left outside the chain reads the empty slot 255 and dies on its first tick
			foreach (Projectile extra in _extraHolders) if (extra.active) SetUpHolder(s, item, extra, dir);
			_extraHolders.Clear();
			_holder[idx] = i;
			return true;
		}

		private static readonly List<Projectile> _extraHolders = new();
		/// <summary>Levies that let go of their held holdout for a right-click: their next spawn is the alt use (by NPC index, the tick it was decided).</summary>
		private static readonly Dictionary<int, uint> _altNext = new();
		/// <summary>The holder SpawnViaHooks just returned is the alt use's own projectile (another type than the left holder: Chronomancer's Scythe's spinning scythe).</summary>
		private static bool _altHolder;

		private static void SetUpHolder(SoldierNPC s, Item item, Projectile p, Vector2 dir)
		{
			JoinChain(s, p, item);
			if (ModShotKinds.ChainExtras(item)) FixIdentity(p);
			if (p.TryGetGlobalProjectile(out HeldWeaponProjectile hg))
			{
				hg.ModHolder = item.type >= ItemID.Count && p.aiStyle != ProjAIStyleID.HeldProjectile; hg.ReleaseOnKill = hg.ModHolder && OwnerLockProbe.FiresOnKill(p.type); hg.HoldMax = LevyConstants.HeldModHoldTicks; hg.HeldTicks = 0;
				if (hg.ModHolder && ModShotKinds.ChargeRelease(item) is ChargePlan cp) { hg.CycleRelease = true; hg.ReleaseOnKill = false; hg.HoldMax = cp.HoldTicks; hg.Linger = cp.LingerTicks; }
				if (hg.CycleRelease && RogueGrenade.Is(item)) { hg.Grenade = true; hg.HoldMax = RogueGrenade.CapTicks; } // the Doomsday Device: let go on a full charge, held inside the grace for a full bar (RogueGrenade)
				if (hg.ModHolder && ModShotKinds.CursorScriptOf(item) is CursorScript cs) hg.Script = cs;
			}
			p.direction = dir.X < 0f ? -1 : 1;
		}

		/// <summary>
		/// Runs a modded item's ModifyShootStats + Shoot hooks with the soldier's dummy as the player (slot 255 swapped to it, Main.myPlayer = 255 so the spawn keeps its ai[] and owner) and returns the projectile of the
		/// item's own shoot type the hook made (the holdout), or null when the hook made none (the caller then spawns the plain holder). A hook that returns true leaves the default shot to us: spawned the same way.
		/// </summary>
		private static Projectile SpawnViaHooks(SoldierNPC s, Item item, Vector2 vel, int damage, int ownerSlot, bool alt, out bool madeAny)
		{
			madeAny = false; _altHolder = false;
			if (s.Dummy == null || item.shoot <= 0) return null;
			_extraHolders.Clear();
			int holderType = ModShotKinds.HolderType(item);
			List<Projectile> made = alt ? HeldAlt.Shoot(s, item, vel, damage, ownerSlot, out bool threw) : ShootHooks(s, item, vel, damage, ownerSlot, false, null, out threw);
			Projectile holder = null;
			foreach (Projectile c in made)
			{
				if (c.type == holderType && c.owner == ownerSlot && holder == null) holder = c; // the holdout joins the chain in TryFire
				else if (c.type == holderType && c.owner == ownerSlot && ModShotKinds.Of(item) == ModShotKind.Holdout) _extraHolders.Add(c); // a second holdout of the same weapon (TryFire puts it in the chain too)
				// the alt's own held projectile (it reads its owner's buttons or pins its use state, as the left holder does: Chronomancer's Scythe's spinning scythe) is held in the chain the same way; an alt that only fires
				// ordinary shots (Drataliornus's flames) leaves them ordinary soldier shots
				else if (alt && holder == null && c.owner == ownerSlot && c.ModProjectile != null && !c.hostile && (OwnerLockProbe.ReadsOwnerButton(c.type) || OwnerLockProbe.ReadsUseButtons(c.type) || OwnerLockProbe.LocksOwner(c.type))) { holder = c; _altHolder = true; }
				else WeaponUse.TagSoldierProjectile(s, c, item.DamageType ?? DamageClass.Default); // anything else the hook fired is an ordinary soldier shot
			}
			madeAny = !threw && made.Count > 0;
			if (threw) { if (holder != null) holder.Kill(); foreach (Projectile x in _extraHolders) if (x.active) x.Kill(); _extraHolders.Clear(); _altHolder = false; }
			return threw ? null : holder;
		}

		/// <summary>
		/// The projectiles an item's own use makes for a soldier: ModifyShootStats then Shoot (what Player.ItemCheck_Shoot calls) run with the dummy swapped into slot 255 as the player holding the item (Main.myPlayer = the owner slot so
		/// the spawns keep their ai[] and owner, the use button down, the right button when <paramref name="alt"/>). When <paramref name="custom"/> is given it makes the projectiles instead of the item's hooks (an Ark's parry, which the hook
		/// refuses while any other soldier's parry is out: they all share owner 255). Returns every projectile made, ascending by slot, and whether a hook threw; they are still owned by the owner slot and untagged.
		/// </summary>
		internal static List<Projectile> ShootHooks(SoldierNPC s, Item item, Vector2 vel, int damage, int ownerSlot, bool alt, Action<IEntitySource, Vector2> custom, out bool threw)
		{
			threw = false;
			var made = new List<Projectile>();
			Player d = s.Dummy;
			if (d == null) return made;
			bool[] before = _before ??= new bool[Main.maxProjectiles];
			for (int k = 0; k < Main.maxProjectiles; k++) before[k] = Main.projectile[k].active;
			int savedWho = d.whoAmI, savedMy = Main.myPlayer, savedSel = d.selectedItem, savedMana = d.statMana, savedManaMax = d.statManaMax2, savedAlt = d.altFunctionUse;
			Item saved0 = d.inventory[0]; bool savedUse = d.controlUseItem, savedChannel = d.channel;
			Vector2 pos = s.NPC.Center, v = vel; int type = item.shoot, dmg = damage; float kb = item.knockBack;
			bool swapped = false;
			UseCycle.Scope cycle = default;
			int masked = 0;
			try
			{
				masked = MaskSiblings(s, item, ownerSlot);
				EnemyTargeting.Swap(s); // dummy -> Main.player[255], at the soldier
				swapped = true;
				if (ownerSlot != Slot) SoldierMinions.AliasDummy(ownerSlot, d); // a UUID-bound holdout lives in the throwaway slot below 255 (see OwnerSlotFor)
				d.whoAmI = ownerSlot; Main.myPlayer = ownerSlot;
				d.inventory[0] = item; d.selectedItem = 0;
				cycle = UseCycle.Enter(s, d, item); // the use state Player.ItemCheck shoots in (itemAnimation, itemTime, ItemUsesThisAnimation, reuseDelay)
				d.statManaMax2 = Math.Max(1, s.Record.MaxMana); d.statMana = Math.Clamp(s.Record.Mana, 0, d.statManaMax2);
				d.controlUseItem = true; d.channel = true;
				d.altFunctionUse = alt ? 2 : 0;
				if (custom != null) custom(new EntitySource_ItemUse_WithAmmo(d, item, 0), v);
				else
				{
					CombinedHooks.ModifyShootStats(d, item, ref pos, ref v, ref type, ref dmg, ref kb);
					var source = new EntitySource_ItemUse_WithAmmo(d, item, 0);
					if (CombinedHooks.Shoot(d, item, source, pos, v, type, dmg, kb)) Projectile.NewProjectile(source, pos, v, type, dmg, kb, ownerSlot);
				}
			}
			catch (Exception) { threw = true; }
			finally
			{
				UnmaskSiblings(masked);
				cycle.Dispose();
				d.whoAmI = savedWho; Main.myPlayer = savedMy; d.selectedItem = savedSel; d.inventory[0] = saved0; d.controlUseItem = savedUse; d.channel = savedChannel; d.altFunctionUse = savedAlt;
				if (item.mana > 0) s.Record.Mana = Math.Clamp(d.statMana, 0, Math.Max(0, s.Record.MaxMana));
				d.statMana = savedMana; d.statManaMax2 = savedManaMax;
				if (swapped) EnemyTargeting.Unswap();
				SoldierMinions.EnsureAliasRestored();
				EnemyTargeting.EnsureSlotRestored();
			}
			for (int k = 0; k < Main.maxProjectiles; k++)
			{
				Projectile c = Main.projectile[k];
				if (c.active && !before[k]) made.Add(c);
			}
			return made;
		}

		private static readonly List<Projectile> _masked = new();
		/// <summary>Lab counter: other levies' holdouts hidden from a Shoot hook's "one of mine is already out" test.</summary>
		internal static int SiblingMasks;
		/// <summary>Lab A/B switch (env LEVY_SIBLINGMASK=0): other levies' holdouts stay visible to a Shoot hook, as before.</summary>
		private static readonly bool SiblingMaskOff = Environment.GetEnvironmentVariable("LEVY_SIBLINGMASK") == "0";

		/// <summary>
		/// Every levy's held chain shares one owner slot (255), so a modded weapon whose Shoot hook refuses while "one of my holdouts is out" (<c>Main.projectile.Any(n =&gt; n.type == Item.shoot &amp;&amp; n.owner == player.whoAmI)</c>: the Ontological
		/// Despoiler, Fetid Emesis and their kind) sees every OTHER levy's holdout as its own and makes nothing, and the fallback holder skips the hook (its mode, its ai[] and any swap). For the length of this levy's Shoot call the
		/// other levies' live chain projectiles of the item's own shoot type are given another owner value, so the hook sees exactly what a player holding the weapon sees: their own holdouts. Put back in the same finally.
		/// Returns how many were masked (0 = nothing to undo).
		/// </summary>
		private static int MaskSiblings(SoldierNPC s, Item item, int ownerSlot)
		{
			_masked.Clear();
			if (SiblingMaskOff || item == null || item.type < ItemID.Count || item.shoot <= 0) return 0;
			int idx = s.NPC.whoAmI, other = ownerSlot == Slot ? Slot - 1 : Slot, holderType = ModShotKinds.HolderType(item);
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile q = Main.projectile[i];
				if (!q.active || q.type != holderType || q.owner != ownerSlot || !q.TryGetGlobalProjectile(out SoldierProjectileTag t) || t.SoldierIndex == idx) continue;
				q.owner = other; _masked.Add(q);
			}
			SiblingMasks += _masked.Count;
			return _masked.Count == 0 ? 0 : ownerSlot + 1;
		}

		private static void UnmaskSiblings(int token)
		{
			if (token == 0) return;
			foreach (Projectile q in _masked) q.owner = token - 1;
			_masked.Clear();
		}

		/// <summary>
		/// The owner slot of a soldier's held chain for this item: 255 (the dummy slot), except for a modded weapon whose holdout needs a projectile UUID (<c>ProjectileID.Sets.NeedsUUID</c>): its children find it
		/// through <c>Projectile.GetByUUID(owner, ...)</c>, which answers -1 for owner 255 (it only accepts owners below 255), so Yharim's Crystal beams and Starmageddon's stars read <c>Main.projectile[-1]</c> and die.
		/// Those chains live in <see cref="SoldierMinions.UuidOwnerSlot"/> (the slot the Stardust Dragons use), with the dummy aliased into it for each AI call.
		/// </summary>
		internal static int OwnerSlotFor(Item item) => ModShotKinds.ChainExtras(item) && item.shoot > 0 && item.shoot < ProjectileID.Sets.NeedsUUID.Length && ProjectileID.Sets.NeedsUUID[item.shoot] ? SoldierMinions.UuidOwnerSlot : Slot;
		/// <summary>
		/// Vanilla hands every projectile <c>identity = index</c> and registers it in <c>Main.projectileIdentity[owner, index]</c>; <c>Projectile.GetByUUID(owner, uuid)</c> reads that table and Calamity's holdouts pass their own
		/// <c>whoAmI</c> as the uuid. Observed in the lab: a chain holdout had identity 1 at index 0, so the lookup answered -1 and its children (Yharim's Crystal beams, Starmageddon's stars) read Main.projectile[-1]. A chain projectile
		/// that needs a UUID is put back to identity = index with its table entry and projUUID.
		/// </summary>
		internal static void FixIdentity(Projectile p)
		{
			if (p == null || !p.active || p.type <= 0 || p.type >= ProjectileID.Sets.NeedsUUID.Length || !ProjectileID.Sets.NeedsUUID[p.type] || p.owner < 0 || p.owner >= 255) return;
			p.identity = p.whoAmI;
			p.projUUID = p.whoAmI;
			Main.projectileIdentity[p.owner, p.whoAmI] = p.whoAmI;
		}

		/// <summary>True for a projectile owned by one of the two chain slots.</summary>
		internal static bool IsChainOwner(int owner) => owner == Slot || owner == SoldierMinions.UuidOwnerSlot;

		/// <summary>This soldier's live holder for <paramref name="item"/>, or null (one per soldier).</summary>
		internal static Projectile LiveHolder(SoldierNPC s, Item item)
		{
			int idx = s.NPC.whoAmI;
			if (!_holder.TryGetValue(idx, out int pi)) return null;
			if (pi >= 0 && pi < Main.maxProjectiles)
			{
				Projectile p = Main.projectile[pi];
				if (p.active && p.TryGetGlobalProjectile(out HeldWeaponProjectile g) && (p.type == ModShotKinds.HolderType(item) || g.AltHolder) && g.Chain && g.ItemType == item.type
					&& ReferenceEquals(g.Record, s.Record) && p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex == idx)
					return p;
				if (DudTrace.On) DudTrace.Note(item, "livecheck-fail active=" + p.active + " type=" + (p.type == item.shoot) + " chain=" + (p.TryGetGlobalProjectile(out HeldWeaponProjectile g2) && g2.Chain) + " itemT=" + (g2 != null && g2.ItemType == item.type) + " rec=" + (g2 != null && ReferenceEquals(g2.Record, s.Record)) + " tag=" + (p.TryGetGlobalProjectile(out SoldierProjectileTag tg2) && tg2.SoldierIndex == idx));
			}
			_holder.Remove(idx);
			return null;
		}

		/// <summary>Marks a projectile as part of this soldier's held-weapon chain: soldier tag, chain flag, item, record. Owner is left alone (255).</summary>
		internal static void JoinChain(SoldierNPC s, Projectile p, Item item)
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
		internal static SoldierNPC BodyOf(Projectile p, HeldWeaponProjectile g)
		{
			if (!p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) return null;
			int idx = tag.SoldierIndex;
			if (idx < 0 || idx >= Main.maxNPCs) return null;
			NPC n = Main.npc[idx];
			if (!n.active || n.ModNPC is not SoldierNPC s || s.Record == null || s.Dummy == null || !s.IsAlive) return null;
			if (g.Record != null && !ReferenceEquals(g.Record, s.Record)) return null; // the NPC slot was reused by another soldier
			return s;
		}

		private static readonly bool DbgNaN = Environment.GetEnvironmentVariable("LEVY_DEADHOOKS_DEBUG") == "1";

		internal static void RunAi(Action<Projectile> orig, Projectile p, HeldWeaponProjectile g)
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

			bool holder = p.aiStyle == ProjAIStyleID.HeldProjectile || g.ModHolder;
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
			bool manaWeapon = false, release = false, aliased = false; int aliasWho = Slot;
			object calPlayer = null; Vector2 savedDelta = default; float savedRot = 0f;
			bool savedMouseLeft = false, mouseSet = false;
			bool outer = OuterSwapBody == body.NPC.whoAmI + 1 && EnemyTargeting.IsSwapped; // the whole ProjectileAI of this chain projectile already stands under the levy's dummy (OpenOuterSwap): this call is one step inside it
			try
			{
				if (!outer) { EnemyTargeting.Swap(body); swapped = true; } // dummy -> Main.player[255]
				if (!holder || !authority)
				{
					// beams and client copies: only the anchor; a lash also lends its dummy the item and the use animation for the call (RealWhips.AiScope)
					if (RealWhips.IsWhipProjectile(p))
					{
						long tw = RealWhips.Begin();
						// a modded lash makes side projectiles from its AI (Thorium's Thrombosis drops blood) and counts what it already has out through its owner's ownedProjectileCounts: the dummy's are kept, and
						// what the AI made is an ordinary soldier shot
						bool moddedLash = authority && item.type >= ItemID.Count;
						bool[] bef = null;
						if (moddedLash)
						{
							CountOwned(body);
							bef = _before ??= new bool[Main.maxProjectiles];
							for (int i = 0; i < Main.maxProjectiles; i++) bef[i] = Main.projectile[i].active;
						}
						using (new RealWhips.AiScope(body, p, item, authority)) { orig(p); if (p.active) RealWhips.NoteReach(p, item); }
						if (bef != null) AdoptChildren(body, p, item, bef);
						if (tw != 0L) { RealWhips.End(ref RealWhips.AiTicks, tw); RealWhips.AiCalls++; }
					}
					else orig(p);
					return;
				}

				// ---- holder on the server / single player: set the dummy up as a player holding the use button ----
				uint now = Main.GameUpdateCount;
				int idx = body.NPC.whoAmI;
				uint since = _fed.TryGetValue(idx, out uint f) ? now - f : uint.MaxValue;
				bool ark = g.Ark != ArkRole.None || g.Biome != 0; // an Ark's blade or parry lives its own life (a swing, a 340-tick parry cooldown): it is not fed, and it is not cut when combat pauses
				if (!ark && since > (uint)LevyConstants.HeldIdleKillTicks) { p.Kill(); return; }
				bool feed = ark || since <= (uint)LevyConstants.HeldFeedGraceTicks;

				savedChannel = d.channel; savedUse = d.controlUseItem; savedNoItems = d.noItems; savedAnimMax = d.itemAnimationMax;
				savedMana = d.statMana; savedManaMax = d.statManaMax2; savedSelected = d.selectedItem;
				saved0 = d.inventory[0]; saved54 = d.inventory[54];
				savedDamage = d.GetDamage(DamageClass.Generic);
				touched = true;

				d.noItems = false;
				d.miscCounter = (int)(Main.GameUpdateCount % 7200u); // a held AI's "every 10 ticks" costs and cadences read the owner's miscCounter, which a never-updated dummy leaves frozen (a beam then drained its mana every tick)
				d.selectedItem = 0;
				d.inventory[0] = item;
				// a modded charge-and-release holdout is held for a charge, then let go (its shot is made when it ends)
				release = g.ReleaseOnKill && feed && ++g.HeldTicks > g.HoldMax;
				// a charge-by-button holdout (ModShotKinds.ChargeRelease) is held for its charge, the button is let go for the rest of its life (it fires on the release and keeps its recoil pose), and it is ended after the linger so the next one is cast
				if (g.CycleRelease && g.Grenade)
				{
					// a grenade: let go on the full charge (and the full bar, see RogueGrenade.ReleaseNow); once thrown it flies, ricochets and falls by itself like a player's, and the hand is free for the next one
					// as soon as Calamity's own gate allows (localAI[1] = 5 a few ticks into the flight). It is never cut short.
					if (feed && !g.Released) { ++g.HeldTicks; if (RogueGrenade.ReleaseNow(body, p, g.HeldTicks)) g.Released = true; }
					release = g.Released;
					if (g.Released && _holder.TryGetValue(idx, out int hi) && hi == p.whoAmI && RogueGrenade.HandFree(p)) _holder.Remove(idx);
				}
				else if (g.CycleRelease && feed)
				{
					++g.HeldTicks;
					// let go when the hold runs out, or (a holdout that counts its own charge) when the charge it is held for is reached
					if (!g.Released && (g.HeldTicks > g.HoldMax || ChargeReached(body, p, g, item))) { g.Released = true; g.ReleasedAt = g.HeldTicks; }
					release = g.Released;
					if (g.Released && g.HeldTicks >= g.ReleasedAt + g.Linger) { p.Kill(); return; }
				}
				else if (g.ModHolder && !g.CycleRelease && feed && ModShotKinds.VariantAtSpawn(item) && AltPolicies.MouseRightHeld(body) && Math.Round(p.ai[2]) == 0)
				{
					// the right variant is wanted (the Hellborn has a shot loaded) but this holdout was made with the left one (its mode, ai[2], is 0: spinning): let go, and the next holdout is made with the right button
					// (ModShotKinds.VariantAtSpawn). The way back needs nothing: a fired shot holdout turns back into the spin by itself once the right button is up.
					g.CycleRelease = true; g.Released = true; g.ReleasedAt = g.HeldTicks; g.Linger = 12; VariantReleases++;
				}
				else if (g.ModHolder && !g.ReleaseOnKill && !g.CycleRelease && feed && g.Biome == 0 && !ark && !g.BoundChild && !ModShotKinds.ChainExtras(item) && !StallOff) // never a bound child (Rancor's circle charges 180 ticks) nor a chain-extras weapon
				{
					// a holdout with no charge plan that has been held a long while and made nothing: it loads or charges while the button is down and fires when it is LET GO (a player lets go; a pinned target never pauses the feed, so it
					// was held for ever and never fired). The button is let go once, as a charge plan would, and the holdout lives out its release.
					if (++g.HeldTicks > LevyConstants.HeldStallReleaseTicks && g.Made == 0) { g.CycleRelease = true; g.Released = true; g.ReleasedAt = g.HeldTicks; g.Linger = LevyConstants.HeldStallLingerTicks; StallReleases++; }
				}
				if (ModShotKinds.ChainExtras(item)) CountOwned(body); // vanilla counts a player's own projectiles in Player.Update; the dummy never runs it, and holdouts and their children test ownedProjectileCounts (Rancor's circle ends when it sees no holdout)
				bool button = g.Biome != 0 ? BiomeBlades.WantsButton(body, p, g) : ark ? ArkSwings.WantsButton(p, g) : feed && !release; // the thrown scissors read the use button: held until the snap window opens, then let go
				d.channel = button;
				d.controlUseItem = button;
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
				if (g.Script != null && TargetOf(body) is NPC scriptTarget) cursor = g.Script.At(scriptTarget.Center, Main.GameUpdateCount);
				// a held AI that turns "owner to cursor" into a direction (Calamity's Auger and Holofibre holdouts: Utils.DirectionTo(Owner.Center, cursor)) divides by the distance: a cursor that is not a number, or sits on the owner, gives a NaN
				// direction and a thrown ArithmeticException every tick. The cursor is always a valid point away from the soldier, on the way to its target (or the way it faces).
				if (!float.IsFinite(cursor.X) || !float.IsFinite(cursor.Y) || (cursor - body.NPC.Center).LengthSquared() < 16f * 16f)
					cursor = body.NPC.Center + (float.IsFinite(dir.X) && float.IsFinite(dir.Y) && dir.LengthSquared() > 0.0001f ? Vector2.Normalize(dir) : Vector2.UnitX * (body.NPC.direction == 0 ? 1 : body.NPC.direction)) * Math.Max(LevyConstants.HeldAimMinDistPx, 64f);
				Main.mouseX = (int)(cursor.X - Main.screenPosition.X);
				Main.mouseY = (int)(cursor.Y - Main.screenPosition.Y);
				if (p.owner != Slot && !ReferenceEquals(Main.player[p.owner], d)) { SoldierMinions.AliasDummy(p.owner, d); aliasWho = d.whoAmI; d.whoAmI = p.owner; aliased = true; } // a UUID-bound chain: the dummy also stands in the chain's own slot
				Main.myPlayer = p.owner; // the holder only fires for owner == Main.myPlayer
				if (g.ModHolder) { savedMouseLeft = Main.mouseLeft; Main.mouseLeft = button; mouseSet = true; } // some holdouts read the raw left button as "still held"
				if (g.ModHolder)
				{
					// Calamity reads the cursor from the owner's own CalamityPlayer (MountedCenter + stored delta), not from Main.mouse: put it on the aim point for this call
					calPlayer = AltPolicies.CalamityPlayerOf(d);
					if (calPlayer != null) { (savedDelta, savedRot) = AltPolicies.GetCalAimPublic(calPlayer); AltPolicies.SetCalAimPublic(calPlayer, cursor - d.MountedCenter); }
				}

				if (g.Biome != 0) BiomeBlades.PreAi(body, p, g, button);
				before = _before ??= new bool[Main.maxProjectiles];
				for (int i = 0; i < Main.maxProjectiles; i++) before[i] = Main.projectile[i].active;
				if (!DbgNaN) orig(p); else { try { orig(p); } catch (ArithmeticException ex) { ModContent.GetInstance<Levy>()?.Logger.Info($"[DeadHooks] NaN in {p.ModProjectile?.Name} ai: cursor={cursor} dir={dir} dummyCenter={d.Center} mounted={d.MountedCenter} soldier={body.NPC.Center} calAim={(calPlayer != null ? AltPolicies.GetCalAimPublic(calPlayer).Item1.ToString() : "-")} ex={ex.StackTrace?.Split((char)10)[1]?.Trim()}"); } }
				if (release && p.active && !g.CycleRelease) { g.HeldTicks = 0; p.Kill(); }
				if (ChargeDbg && _holdDbgLines < 300 && (_dbgHold == null || p.ModProjectile?.Name == _dbgHold)) { _holdDbgLines++; ModContent.GetInstance<Levy>()?.Logger.Info($"[HoldoutWorld] OBSERVED   dbg hold {p.ModProjectile?.Name} t={Main.GameUpdateCount % 100000} held={g.HeldTicks} button={button} rel={release} cyc={g.CycleRelease} made={g.Made} ai=({p.ai[0]:0.#},{p.ai[1]:0.#},{p.ai[2]:0.#}) lai=({p.localAI[0]:0.#},{p.localAI[1]:0.#}) tl={p.timeLeft} active={p.active} right={AltPolicies.GetMouseRight(d)} animMax={d.itemAnimationMax}"); } // the release: the charged shot is made while the soldier is still the owner (dummy swapped in, cursor on the target)
			}
			finally
			{
				Main.myPlayer = savedMyPlayer; Main.mouseX = savedMouseX; Main.mouseY = savedMouseY;
				if (aliased) { d.whoAmI = aliasWho; SoldierMinions.EnsureAliasRestored(); }
				if (calPlayer != null) AltPolicies.SetCalAimPublic(calPlayer, savedDelta, savedRot);
				if (mouseSet) Main.mouseLeft = savedMouseLeft;
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
						if (g.Biome != 0) BiomeBlades.PostAi(body, p, g);
						if (before != null) AdoptChildren(body, p, item, before);
					}
				}
				catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn("Levy: held weapon cleanup failed: " + e.Message); }
				finally
				{
					if (!outer)
					{
						if (swapped) EnemyTargeting.Unswap();
						EnemyTargeting.EnsureSlotRestored(); // never leave a dummy in Main.player[255]
					}
				}
			}
		}

		/// <summary>Lab counters: releases of an ai[0]-charge plan, uncharged (let go at once) and charged; stall releases (a holdout with no plan that made nothing in HeldStallReleaseTicks, let go once).</summary>
		internal static int TapReleases, ChargeReleases, StallReleases, VariantReleases;
		/// <summary>Lab A/B switch (env LEVY_HOLDSTALL=0): a holdout with no charge plan is held for as long as the levy fights, as before.</summary>
		private static readonly bool StallOff = Environment.GetEnvironmentVariable("LEVY_HOLDSTALL") == "0";
		private static readonly bool ChargeDbg = Environment.GetEnvironmentVariable("LEVY_HOLDOUT_DEBUG") == "1";
		private static int _holdDbgLines; private static readonly string _dbgHold = Environment.GetEnvironmentVariable("LEVY_HOLDOUT_DEBUGHOLD");

		/// <summary>
		/// A holdout with an ai[0] charge plan (<see cref="ChargePlan.Stages"/>) has reached the charge it is held for. The charge is picked once, on the first held tick, from the holdout's mode (ai[2]) and the situation:
		/// the first stage whose condition holds, else none (the button is let go at once: an uncharged shot).
		/// </summary>
		private static bool ChargeReached(SoldierNPC body, Projectile p, HeldWeaponProjectile g, Item item)
		{
			if (ModShotKinds.ChargeRelease(item) is not ChargePlan plan || plan.Stages == null) return false;
			if (!g.ChargeDecided)
			{
				g.ChargeDecided = true; g.ChargeTo = 0f;
				if (plan.Stages.TryGetValue((int)Math.Round(p.ai[2]), out var stages))
					foreach (var (cond, ai0) in stages) if (StageWanted(body, cond)) { g.ChargeTo = ai0; break; }
				if (ChargeDbg) { NPC dt = TargetOf(body); ModContent.GetInstance<Levy>()?.Logger.Info($"[HoldoutWorld] OBSERVED   dbg charge levy={body.NPC.whoAmI} ai2={p.ai[2]} ai0={p.ai[0]} heldTicks={g.HeldTicks} target={(dt == null ? "none" : dt.FullName + " v=" + dt.velocity.Length().ToString("0.0") + " d=" + (Vector2.Distance(dt.Center, body.NPC.Center) / 16f).ToString("0.0"))} -> chargeTo={g.ChargeTo}"); }
			}
			bool done = g.ChargeTo <= 0f || p.ai[0] >= g.ChargeTo;
			if (done) { if (g.ChargeTo <= 0f) TapReleases++; else ChargeReleases++; }
			return done;
		}

		private static bool StageWanted(SoldierNPC body, string cond)
		{
			if (string.IsNullOrEmpty(cond)) return false;
			if (cond == "always") return true;
			NPC t = TargetOf(body);
			var x = new AltCtx(body, t);
			int c = cond.IndexOf(':');
			string k = c < 0 ? cond : cond.Substring(0, c);
			float v = c < 0 ? 0f : float.TryParse(cond.Substring(c + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 0f;
			switch (k)
			{
				case "tough": return x.IsTough(600f);
				case "crowd": return x.Crowd(8f) >= (int)v;
				case "fast": return t != null && t.velocity.Length() >= v;
				case "far": return t != null && x.Dist >= v;
			}
			return false;
		}

		/// <summary>The soldier (NPC index + 1) whose dummy stands in slot 255 for a whole <c>ProjectileLoader.ProjectileAI</c> call of one of its chain projectiles, or 0 (see <see cref="OpenOuterSwap"/>).</summary>
		[ThreadStatic] internal static int OuterSwapBody;

		/// <summary>
		/// A chain projectile's WHOLE AI call, not only its vanilla body: <c>ProjectileLoader.ProjectileAI</c> runs the PreAI of every global and of the projectile, the AI body, then every PostAI. The held-weapon wrapper used to stand on the
		/// vanilla body alone (and on a lash's or a modded holdout's whole call), so for a beam or any other chain child the other mods' PreAI / PostAI and a modded child's own AI read slot 255 as the empty player: facing right, standing at the
		/// world's corner, whichever way the levy faced. The dummy stands in the slot for the whole call; the wrapper inside it (<see cref="RunAi"/>) sees it already there and leaves it alone.
		/// </summary>
		internal static void OpenOuterSwap(Action<Projectile> orig, Projectile p, HeldWeaponProjectile g)
		{
			SoldierNPC body = LevyUtil.IsServerOrSingle ? BodyOf(p, g) : null;
			if (body == null || p.owner != Slot || EnemyTargeting.IsSwapped) { orig(p); return; }
			int prev = OuterSwapBody;
			try
			{
				EnemyTargeting.Swap(body);
				OuterSwapBody = body.NPC.whoAmI + 1;
				orig(p);
			}
			finally
			{
				OuterSwapBody = prev;
				EnemyTargeting.Unswap();
				EnemyTargeting.EnsureSlotRestored(); // never leave a dummy in Main.player[255]
			}
		}

		/// <summary>
		/// Projectile.Damage of a chain projectile (holder contact damage, thick-laser beam). Vanilla only damages for owner == Main.myPlayer, so for the length of the call the projectile is
		/// handed to the REAL player (<see cref="SoldierMinions.TrueMyPlayer"/>: what <see cref="WeaponUse.OwnerFor"/> gives every other soldier shot) and given back to 255 in the finally.
		/// The dummy is deliberately NOT swapped into Main.player[255] and Main.myPlayer is left alone. A hit that kills a boss runs the whole death cascade (NPC.checkDead, NPCLoot, every mod's
		/// OnKill, event and boss-log bookkeeping) synchronously inside orig, and it has to see the real player in the slot, exactly as for a normal soldier shot; with a fake player standing in
		/// slot 255 that cascade hung the game.
		/// The damage number is unaffected: it is the projectile's own damage field, which the AI pass sets (RunAi, which keeps the dummy swap; the Last Prism beam copies the holder's every tick),
		/// and chain projectiles are npcProj, so vanilla gates every owner-derived term that could change it (crit, luck, banner buff, on-hit effects, ModPlayer hooks) behind !npcProj.
		/// </summary>
		internal static void RunDamage(On_Projectile.orig_Damage orig, Projectile p, HeldWeaponProjectile g)
		{
			SoldierNPC body = BodyOf(p, g);
			if (body == null || !LevyUtil.IsServerOrSingle) { orig(p); return; }
			int savedOwner = p.owner, savedType = p.type;
			Item item0 = g.Whip ? WeaponUse.HeldItem(body) : null;
			ArkSwings.DamageScope ark = g.Ark != ArkRole.None ? new ArkSwings.DamageScope(body, p, g) : default; // an Ark's blade reads its owner's centre: the real player stands at the soldier for the call
			BiomeBlades.DamageScope bb = g.Biome != 0 ? new BiomeBlades.DamageScope(body, p, g) : default; // a Biome Blade's too, with the soldier's blade in the real player's hand
			RealWhips.DamageScope wh = g.Whip && item0 != null ? new RealWhips.DamageScope(body, item0) : default; // a lash's hit writes the owner (the minion focus, the item's on-hit hooks): the commander is put back as it was
			// a rogue weapon's hit that writes its owner's stealth bar (the Doomsday Device gives back 30% of it on a plain hit) writes the LEVY's bar: the real player's bar is lent the levy's values for the call
			StealthLend lend = g.Grenade ? StealthLend.Begin(body) : default;
			OwnerPuppet.Lend facing = default; // a beam's hit pass reads its owner like every other soldier shot's does: the levy's facing and place, not the commander's (a lash lends its own, RealWhips.DamageScope)
			// a hit that loads its owner's meter (the row's named field: the Hellborn's spin loads hellbornShots) loads the LEVY's: his value is lent to the real player for the call and taken back after
			string meter = g.ModHolder && LevyUtil.IsServerOrSingle ? AltPolicies.MeterOf(g.ItemType) : null;
			Player real = meter != null && SoldierMinions.TrueMyPlayer >= 0 && SoldierMinions.TrueMyPlayer <= Main.maxPlayers ? Main.player[SoldierMinions.TrueMyPlayer] : null; // slot 255 on a dedicated server (the null player the hit pass runs under)
			bool lentMeter = false;
			try
			{
				p.owner = SoldierMinions.TrueMyPlayer;
				if (!g.Whip) facing = OwnerPuppet.LendTransform(body);
				if (real != null) lentMeter = AltPolicies.SwapMeter(real, body.Dummy, meter);
				orig(p);
			}
			finally
			{
				if (lentMeter) AltPolicies.SwapMeter(real, body.Dummy, meter);
				facing.Dispose();
				lend.End();
				if (p.type == savedType) p.owner = savedOwner; // undo only our own edit: if the cascade killed p and recycled its slot, the new projectile keeps its owner
				ark.Dispose();
				bb.Dispose();
				if (g.Whip && item0 != null) wh.Dispose();
				EnemyTargeting.EnsureSlotRestored(); // never leave a dummy in Main.player[255]
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
				if (DudTrace.On) DudTrace.NoteType(c.type, "adopted@" + c.owner + (c.friendly ? "+f" : "-f") + "+dmg" + (c.damage > 0 ? "1" : "0"));
				if (holder.TryGetGlobalProjectile(out HeldWeaponProjectile mg)) mg.Made++;
				if (c.aiStyle == ProjAIStyleID.HeldProjectile || c.aiStyle == ProjAIStyleID.ThickLaser) JoinChain(body, c, item);
				else if (ArkSwings.AdoptChild(body, holder, c, item)) { }
				else if (BiomeBlades.AdoptChild(body, holder, c, item)) { }
				else if (ModShotKinds.ChainExtras(item) && c.ModProjectile != null && ((holder.TryGetGlobalProjectile(out HeldWeaponProjectile pg) && pg.BoundChild) || OwnerLockProbe.BoundToHolder(c) || PointsAt(c, holder)))
				{
					// bound to its holder (reads the button / finds its parent by UUID) or made by a bound child (Starmageddon's stars, made by the star centre): stays in the chain, same owner, dummy and button as the holdout
					JoinChain(body, c, item);
					FixIdentity(c);
					if (c.TryGetGlobalProjectile(out HeldWeaponProjectile cg)) { cg.ModHolder = true; cg.BoundChild = true; if (holder.TryGetGlobalProjectile(out HeldWeaponProjectile hh)) cg.Script = hh.Script; }
					DudTrace.Note(item, "child-in-chain:" + (c.ModProjectile?.Name ?? c.type.ToString()));
				}
				else
				{
					// a child the holdout made as a non-damaging helper (friendly off: the Eternity hex, which orbits the target and summons the homing bolts) stays one: made friendly as an ordinary soldier shot, it struck the
					// target on its first tick, died (it pierces once) and never cast its bolts (the levy's Eternity did a fifth of a player's damage). A helper that turns itself friendly later does so in its own AI.
					bool helper = !c.friendly && !c.hostile && c.ModProjectile != null && !HelperFriendlyOld;
					WeaponUse.TagSoldierProjectile(body, c, cls);
					if (helper) { c.friendly = false; HelpersKept++; }
				}
			}
		}

		/// <summary>Lab counter: non-friendly helper children of a held chain kept non-friendly. Switch: LEVY_HELPERFRIENDLY_OLD=1 makes them friendly like any soldier shot, as before.</summary>
		internal static int HelpersKept;
		private static readonly bool HelperFriendlyOld = Environment.GetEnvironmentVariable("LEVY_HELPERFRIENDLY_OLD") == "1";

		/// <summary>A child that carries its holder's slot in one of its ai[] (Dark Spark's beams: the prism's index, through the UUID table, in ai[1]) is bound to that holder: it reads it every tick.</summary>
		private static bool PointsAt(Projectile c, Projectile holder)
		{
			int w = holder.whoAmI;
			for (int k = 0; k < c.ai.Length; k++) if ((int)Math.Round(c.ai[k]) == w && w > 0) return true;
			return false;
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
		/// <summary>A modded holdout run as the holder (server side only; see ModShotKinds). ReleaseOnKill: it makes its shot when it ends, so it is held for HeldModHoldTicks and then released.</summary>
		public bool ModHolder, ReleaseOnKill;
		/// <summary>A charge-by-button holdout (see ModShotKinds.ChargeRelease): held HoldMax ticks, released, ended Linger ticks later.</summary>
		public bool CycleRelease;
		/// <summary>A held-then-thrown grenade (RogueGrenade, the Doomsday Device): let go on its full charge, then left to fly; Released once the button has been let go.</summary>
		public bool Grenade, Released;
		/// <summary>A projectile a holdout's AI made that stays bound to it (see OwnerLockProbe.BoundToHolder): it and what it spawns stay in the chain.</summary>
		public bool BoundChild;
		public int HeldTicks, HoldMax, Linger;
		/// <summary>A charge-by-button holdout: the hold tick it was let go at, and (an ai[0] charge plan) the charge it is held for (0 = let go at once), picked on its first held tick.</summary>
		public int ReleasedAt;
		public bool ChargeDecided; public float ChargeTo;
		/// <summary>Projectiles this holder's AI made (counted when they are adopted): a holdout with no charge plan that made none in its hold is let go once (the stall release).</summary>
		public int Made;
		/// <summary>The holder is a right-click use's own projectile, of another type than the item's left holder (Chronomancer's Scythe's spinning scythe).</summary>
		public bool AltHolder;
		/// <summary>The variant (right) button was held when this holder was made.</summary>
		public bool SpawnRight;
		/// <summary>An Ark's swung blade or parry holdout (or a child bound to the blade): see <see cref="ArkSwings"/>.</summary>
		public ArkRole Ark;
		/// <summary>A Biome Blade's attunement blade (1) or a child bound to it (2): see <see cref="BiomeBlades"/>. Server side only.</summary>
		public byte Biome;
		/// <summary>The Ark's reach and throw numbers, for its blade (server side only).</summary>
		internal ArkSpec ArkSpec;
		/// <summary>The parry has struck an enemy (it empowers once); and whether that enemy could hurt the soldier by touch (the parry's immunity window needs it).</summary>
		public bool ParryHit, ParryContact, SnapCounted;
		/// <summary>A soldier's lash: the item's real whip projectile run as a chain projectile (see <see cref="RealWhips"/>). Server side only.</summary>
		public bool Whip;
		/// <summary>A scripted cursor path (see ModShotKinds.CursorScriptOf) this holdout and its bound children follow instead of the plain aim point.</summary>
		public CursorScript Script;
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

		private bool _drawSwapped;
		/// <summary>
		/// A chain projectile is owned by slot 255, and Calamity's Arks (and other holdouts) DRAW from <c>Main.player[owner].Center</c>: at draw time that slot holds the null player, so the blade and the parry were
		/// drawn where it stands (the world's corner), not on the soldier. The soldier's dummy stands in the slot for the length of this one draw (at the soldier, like every AI call), so the sprite is drawn on him.
		/// Draw-time only; the slot is put back in PostDraw (and by EnsureSlotRestored every frame).
		/// </summary>
		public override bool PreDraw(Projectile projectile, ref Color lightColor) => BeginDraw(projectile, false);

		private bool BeginDraw(Projectile projectile, bool lab)
		{
			_drawSwapped = false;
			if (!Chain || (Main.dedServ && !lab) || !CombatFix.On(CombatFix.Draw) || projectile.owner != SoldierNPC.DummySlot || EnemyTargeting.IsSwapped) return true;
			if (!projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex < 0 || tag.SoldierIndex >= Main.maxNPCs) return true;
			if (Main.npc[tag.SoldierIndex].ModNPC is not SoldierNPC s || s.Dummy == null || !s.IsAlive) return true;
			try
			{
				EnemyTargeting.Swap(s); _drawSwapped = true; CombatFix.DrawSwaps++;
				// a lash is drawn along the control points the whip code works out from its owner's use animation and arm: the dummy holds the item and the animation for the draw too
				if (RealWhips.IsWhipProjectile(projectile))
				{
					Item it = WeaponUse.HeldItem(s);
					if (it == null || it.IsAir || it.type != ItemType) it = ContentSamples.ItemsByType.TryGetValue(ItemType, out Item smp) ? smp : null;
					if (it != null) _whipDraw = new RealWhips.AiScope(s, projectile, it, false);
				}
			}
			catch (Exception) { EnemyTargeting.EnsureSlotRestored(); }
			return true;
		}

		private RealWhips.AiScope? _whipDraw;

		/// <summary>Lab: the reach (px, from the first control point) the whip code gives this lash in the state its draw sees: the dummy in the owner slot, as <see cref="ProbeDraw"/>. -1 when it could not be read.</summary>
		internal static float ProbeWhipDrawReach(Projectile projectile)
		{
			if (!projectile.TryGetGlobalProjectile(out HeldWeaponProjectile g) || !g.Chain || !RealWhips.IsWhipProjectile(projectile)) return -1f;
			var pts = new List<Vector2>(48);
			try
			{
				g.BeginDraw(projectile, true);
				if (!g._drawSwapped) return -1f;
				Projectile.FillWhipControlPoints(projectile, pts);
			}
			catch (Exception) { return -1f; }
			finally { g.PostDraw(projectile, default); }
			float r = 0f;
			for (int i = 1; i < pts.Count; i++) { float d = Vector2.Distance(pts[0], pts[i]); if (!float.IsNaN(d)) r = Math.Max(r, d); else return -2f; }
			return r;
		}

		/// <summary>Lab only (a dedicated server never draws): runs the draw swap on this chain projectile exactly as PreDraw / PostDraw do, and reports where the projectile's owner slot stands during the draw (distance to its soldier in px) and whether the slot was put back.</summary>
		internal static bool ProbeDraw(Projectile projectile, out float ownerToSoldierPx, out bool restored)
		{
			ownerToSoldierPx = -1f; restored = true;
			if (!projectile.TryGetGlobalProjectile(out HeldWeaponProjectile g) || !g.Chain) return false;
			Player before = Main.player[projectile.owner];
			g.BeginDraw(projectile, true);
			bool swapped = g._drawSwapped;
			if (projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0 && tag.SoldierIndex < Main.maxNPCs)
				ownerToSoldierPx = Vector2.Distance(Main.player[projectile.owner].Center, Main.npc[tag.SoldierIndex].Center);
			g.PostDraw(projectile, default);
			restored = ReferenceEquals(Main.player[projectile.owner], before);
			return swapped;
		}

		public override void PostDraw(Projectile projectile, Color lightColor)
		{
			if (!_drawSwapped) return;
			_drawSwapped = false;
			if (_whipDraw.HasValue) { _whipDraw.Value.Dispose(); _whipDraw = null; }
			EnemyTargeting.Unswap();
			EnemyTargeting.EnsureSlotRestored();
		}

		/// <summary>An Ark's parry stab connected: remembers whether the enemy has contact damage (the parry's immunity window is only granted against that).</summary>
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Chain && Ark == ArkRole.Parry && !ParryHit) { ParryHit = true; ParryContact = target.damage > 0; }
			if (Chain && Whip) RealWhips.OnHit(projectile, target);
			if (Chain && ModHolder) Made++; // a holder's own hit counts as what it made (the Hellborn's spin deals its damage by touch): the stall release is only for a holder that has done nothing at all
		}

		/// <summary>A lash's hit line is taken from its soldier, not from the commander who owns the projectile.</summary>
		public override bool? CanHitNPC(Projectile projectile, NPC target) => Chain && Whip && !RealWhips.CanHit(projectile, target) ? false : null;

		/// <summary>The soldier's dummy must not keep pointing at a dead holder (the dummy's held-projectile layer would draw whatever reuses that slot).</summary>
		public override void OnKill(Projectile projectile, int timeLeft)
		{
			if (!Chain || !projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag)) return;
			int idx = tag.SoldierIndex;
			if (idx < 0 || idx >= Main.maxNPCs) return;
			if (Main.npc[idx].ModNPC is SoldierNPC s && s.Dummy != null && s.Dummy.heldProj == projectile.whoAmI) s.Dummy.heldProj = -1;
		}
	}

	/// <summary>
	/// Installs the Projectile.AI / Projectile.Damage wrappers (nested chain calls pass straight through) and keeps slot 255 clean around the projectile phase. The Damage detour also serves the
	/// driven minions of <see cref="SoldierMinions"/>, so there is exactly one Projectile.Damage hook in the mod.
	/// </summary>
	public class HeldWeaponSystem : ModSystem
	{
		public override void Load()
		{
			On_Projectile.AI += AiDetour;
			On_Projectile.VanillaAI += VanillaAiDetour;
			// A MODDED projectile's AI (PreAI / AI / PostAI) is run by ProjectileLoader.ProjectileAI, not by Projectile.AI(): a modded holder (ModShotKinds.Holdout) is wrapped there.
			try
			{
				var mi = typeof(ProjectileLoader).GetMethod("ProjectileAI", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(Projectile) }, null);
				if (mi != null) MonoModHooks.Add(mi, (Action<Action<Projectile>, Projectile>)LoaderAiDetour);
				else Mod.Logger.Warn("Levy: ProjectileLoader.ProjectileAI not found: modded holdouts are not held");
			}
			catch (Exception e) { Mod.Logger.Warn("Levy: could not hook ProjectileLoader.ProjectileAI: " + e.Message); }
			On_Projectile.Damage += DamageDetour;
			On_Projectile.Update += ChainUpdateDetour;
			On_Projectile.Kill += KillDetour;
		}

		public override void Unload()
		{
			On_Projectile.AI -= AiDetour;
			On_Projectile.VanillaAI -= VanillaAiDetour;
			On_Projectile.Damage -= DamageDetour;
			On_Projectile.Update -= ChainUpdateDetour;
			On_Projectile.Kill -= KillDetour;
			HeldWeapons.Reset();
		}

		/// <summary>A chain projectile's death (Projectile.Kill: its OnKill hooks, the effects and split shots a mod makes when a beam or a lash ends) reads its owner too: the levy's dummy stands in slot 255 for the call.</summary>
		private static void KillDetour(On_Projectile.orig_Kill orig, Projectile self)
		{ _hl_KillDetour_6++;
			if (!OwnerPuppet.FacingFixOn || self.owner != SoldierNPC.DummySlot || !self.active || _wrapDepth != 0 || EnemyTargeting.IsSwapped || !LevyUtil.IsServerOrSingle || !self.TryGetGlobalProjectile(out HeldWeaponProjectile g) || !g.Chain) { orig(self); return; }
			SoldierNPC body = HeldWeapons.BodyOf(self, g);
			if (body == null) { orig(self); return; }
			try { EnemyTargeting.Swap(body); orig(self); }
			finally { EnemyTargeting.Unswap(); EnemyTargeting.EnsureSlotRestored(); }
		} private static int _hl_KillDetour_6;

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

		/// <summary>
		/// Re-entrancy guard shared by both detours: above zero while a chain wrapper (or a driven minion's Damage wrapper) is inside orig on this thread. A boss-death cascade runs synchronously
		/// inside the hit that caused it; if anything in it re-enters Projectile.AI / Projectile.Damage for another chain projectile or minion, that nested call goes straight to orig instead of
		/// opening another swap / re-owning / strike window inside the first one, so the wrappers can never recurse into each other (a beam's Damage and a minion's Damage count the same).
		/// </summary>
		[ThreadStatic] private static int _wrapDepth;

		/// <summary>True when this call must be wrapped: a chain projectile (owner 255, flagged in its global) and not a nested call. The cheap owner test runs first.</summary>
		private static bool ShouldWrap(Projectile p, out HeldWeaponProjectile g)
		{
			g = null;
			return HeldWeapons.IsChainOwner(p.owner) && _wrapDepth == 0 && p.TryGetGlobalProjectile(out g) && g.Chain;
		}

		private static void LoaderAiDetour(Action<Projectile> orig, Projectile self)
		{ _hl_LoaderAiDetour_4++;
			global::Levy.Common.Util.HookProbe.HeldLoader++;
			// a lash (vanilla or modded) is wrapped here too: Projectile.Update runs every projectile's AI through ProjectileLoader.ProjectileAI, and the vanilla whip AI reads its owner's use animation and arm
			if (!HeldWeapons.IsChainOwner(self.owner) || _wrapDepth != 0 || !self.TryGetGlobalProjectile(out HeldWeaponProjectile g) || !g.Chain) { orig(self); return; }
			if (!(RealWhips.IsWhipProjectile(self) || (self.ModProjectile != null && g.ModHolder))) { if (OwnerPuppet.FacingFixOn) HeldWeapons.OpenOuterSwap(orig, self, g); else orig(self); return; } // a beam or a holder's other child: the vanilla body is wrapped inside (VanillaAiDetour), the rest of the call stands here
			_wrapDepth++;
			try { HeldWeapons.RunAi(orig, self, g); }
			finally { _wrapDepth--; }
		} private static int _hl_LoaderAiDetour_4;

		/// <summary>
		/// Projectile.Update ends with <c>if (owner &lt; 255 &amp;&amp; !Main.player[owner].active) Kill();</c>: a chain that lives in the throwaway slot below 255 (a UUID-bound holdout, see <see cref="HeldWeapons.OwnerSlotFor"/>) would be
		/// killed by that line every tick, because the slot's Player is only the soldier's dummy while the AI runs. Its whole Update runs with the dummy standing in the slot (what the dragons' Update wrapper does).
		/// </summary>
		private static void ChainUpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{ _hl_ChainUpdateDetour_2++;
			if (self.owner == SoldierNPC.DummySlot || !self.active || !HeldWeapons.IsChainOwner(self.owner) || !LevyUtil.IsServerOrSingle || !self.TryGetGlobalProjectile(out HeldWeaponProjectile g) || !g.Chain
				|| !self.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex < 0 || tag.SoldierIndex >= Main.maxNPCs || Main.npc[tag.SoldierIndex].ModNPC is not SoldierNPC s || s.Dummy == null || !s.IsAlive)
			{ orig(self, i); return; }
			bool aliased = false;
			try
			{
				if (!ReferenceEquals(Main.player[self.owner], s.Dummy)) { SoldierMinions.AliasDummy(self.owner, s.Dummy); aliased = true; }
				orig(self, i);
			}
			finally { if (aliased) SoldierMinions.EnsureAliasRestored(); }
		} private static int _hl_ChainUpdateDetour_2;

		/// <summary>
		/// Projectile.AI is a one-line stub (<c>ProjectileLoader.ProjectileAI(this)</c>) that the JIT inlines into Projectile.Update, so a detour on it is never reached once Update has been tiered up (observed: 0 calls over thousands of projectile updates
		/// while a detour on <c>Projectile.VanillaAI</c>, the body the loader calls for a vanilla projectile, saw every one). The same wrapper therefore also sits on VanillaAI; whichever fires first opens the swap and the other passes straight through (_wrapDepth).
		/// </summary>
		private static void VanillaAiDetour(On_Projectile.orig_VanillaAI orig, Projectile self)
		{ _hl_VanillaAiDetour_5++;
			global::Levy.Common.Util.HookProbe.HeldVanilla++;
			if (!ShouldWrap(self, out HeldWeaponProjectile g)) { orig(self); return; }
			_wrapDepth++;
			try { HeldWeapons.RunAi(pp => orig(pp), self, g); }
			finally { _wrapDepth--; }
		} private static int _hl_VanillaAiDetour_5;

		private static void AiDetour(On_Projectile.orig_AI orig, Projectile self)
		{ _hl_AiDetour_1++;
			global::Levy.Common.Util.HookProbe.HeldAi++;
			if (!ShouldWrap(self, out HeldWeaponProjectile g)) { orig(self); return; }
			_wrapDepth++;
			try { HeldWeapons.RunAi(pp => orig(pp), self, g); }
			finally { _wrapDepth--; }
		} private static int _hl_AiDetour_1;

		/// <summary>
		/// The ONE Projectile.Damage detour of the mod. A chain projectile (held-weapon beam) is handed to the real player for the call; a driven minion in the middle of its own Update
		/// (SoldierMinions.RunUpdate has the dummy swapped in) arms the strike window instead, so the kill cascade of a lethal hit resolves in the real-player context while the call's damage
		/// arithmetic keeps running against the dummy, unchanged (see SoldierMinions.RunDamage / RunStrike). Everything else is vanilla.
		/// </summary>
		private static void DamageDetour(On_Projectile.orig_Damage orig, Projectile self)
		{ _hl_DamageDetour_3++;
			if (ShouldWrap(self, out HeldWeaponProjectile g))
			{
				_wrapDepth++;
				try { HeldWeapons.RunDamage(orig, self, g); }
				finally { _wrapDepth--; }
			}
			else if (WantsMinionWindow(self))
			{
				_wrapDepth++;
				try { SoldierMinions.RunDamage(orig, self); }
				finally { _wrapDepth--; }
			}
			else orig(self);
		} private static int _hl_DamageDetour_3;

		/// <summary>True for a driven minion inside its own Update on the server / single player, and not nested in another wrapper. The frame test is a single reference compare and is false for almost every call.</summary>
		private static bool WantsMinionWindow(Projectile p) => SoldierMinions.IsFrameProjectile(p) && _wrapDepth == 0 && LevyUtil.IsServerOrSingle;
	}
}
