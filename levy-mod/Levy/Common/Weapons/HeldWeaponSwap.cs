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

		internal static void Reset() { _holder.Clear(); _fed.Clear(); _spawned.Clear(); _countStamp.Clear(); ArkSwings.Reset(); BiomeBlades.Reset(); }

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

			if (LiveHolder(s, item) != null) return true;
			if (_spawned.TryGetValue(idx, out uint last) && now - last < (uint)LevyConstants.HeldRespawnMinTicks) return false;
			if (ModShotKinds.StartWait(item, s.Record.Mana, s.Record.MaxMana)) { DudTrace.Note(item, "held-start-wait"); return false; } // a drain-in-AI holdout starts on a full pool (ModShotKinds.StartFractionTable)
			_spawned[idx] = now;

			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier));
			Vector2 dir = Vector2.Normalize(toTarget);
			AltPolicies.OnHoldoutSpawn(s, item, target); // the held right button (variant) is set before the holdout exists
			// owner 255 is deliberate (see class summary); the holder's real owner-gated code runs under the wrapper's Main.myPlayer swap.
			// A MODDED holdout is made by its item's own Shoot hook (Calamity's guns pass the holdout its gun type / mode in ai[], and some fire real shots from the hook itself): run the hook, with the dummy as the player.
			int ownerSlot = OwnerSlotFor(item);
			Projectile p = item.type >= ItemID.Count ? SpawnViaHooks(s, item, dir * Math.Max(1f, item.shootSpeed), damage, ownerSlot) : null;
			int i = p?.whoAmI ?? -1;
			if (p == null)
			{
				i = Projectile.NewProjectile(s.NPC.GetSource_FromAI(), s.NPC.Center, dir * Math.Max(1f, item.shootSpeed), item.shoot, damage, item.knockBack, ownerSlot);
				if (i < 0 || i >= Main.maxProjectiles) return WeaponUse.Dud(s, item);
				p = Main.projectile[i];
			}
			if (!p.active || p.type != item.shoot) { DudTrace.Note(item, "held-spawn-dud"); return WeaponUse.Dud(s, item); }
			DudTrace.Note(item, "held-spawn");
			JoinChain(s, p, item);
			if (ModShotKinds.ChainExtras(item)) FixIdentity(p);
			if (DudTrace.On && ProjectileID.Sets.NeedsUUID[p.type]) DudTrace.Note(item, "uuid who=" + p.whoAmI + " id=" + p.identity + " uuid=" + p.projUUID + " lookup=" + Projectile.GetByUUID(p.owner, p.whoAmI) + " owner=" + p.owner);
			if (p.TryGetGlobalProjectile(out HeldWeaponProjectile hg))
			{
				hg.ModHolder = item.type >= ItemID.Count && p.aiStyle != ProjAIStyleID.HeldProjectile; hg.ReleaseOnKill = hg.ModHolder && OwnerLockProbe.FiresOnKill(item.shoot); hg.HoldMax = LevyConstants.HeldModHoldTicks; hg.HeldTicks = 0;
				if (hg.ModHolder && ModShotKinds.ChargeRelease(item) is ChargePlan cp) { hg.CycleRelease = true; hg.ReleaseOnKill = false; hg.HoldMax = cp.HoldTicks; hg.Linger = cp.LingerTicks; }
				if (hg.ModHolder && ModShotKinds.CursorScriptOf(item) is CursorScript cs) hg.Script = cs;
			}
			p.direction = dir.X < 0f ? -1 : 1;
			_holder[idx] = i;
			return true;
		}

		/// <summary>
		/// Runs a modded item's ModifyShootStats + Shoot hooks with the soldier's dummy as the player (slot 255 swapped to it, Main.myPlayer = 255 so the spawn keeps its ai[] and owner) and returns the projectile of the
		/// item's own shoot type the hook made (the holdout), or null when the hook made none (the caller then spawns the plain holder). A hook that returns true leaves the default shot to us: spawned the same way.
		/// </summary>
		private static Projectile SpawnViaHooks(SoldierNPC s, Item item, Vector2 vel, int damage, int ownerSlot)
		{
			if (s.Dummy == null || item.shoot <= 0) return null;
			List<Projectile> made = ShootHooks(s, item, vel, damage, ownerSlot, false, null, out bool threw);
			Projectile holder = null;
			foreach (Projectile c in made)
			{
				if (c.type == item.shoot && c.owner == ownerSlot && holder == null) holder = c; // the holdout joins the chain in TryFire
				else WeaponUse.TagSoldierProjectile(s, c, item.DamageType ?? DamageClass.Default); // anything else the hook fired is an ordinary soldier shot
			}
			if (threw && holder != null) holder.Kill();
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
			try
			{
				EnemyTargeting.Swap(s); // dummy -> Main.player[255], at the soldier
				swapped = true;
				if (ownerSlot != Slot) SoldierMinions.AliasDummy(ownerSlot, d); // a UUID-bound holdout lives in the throwaway slot below 255 (see OwnerSlotFor)
				d.whoAmI = ownerSlot; Main.myPlayer = ownerSlot;
				d.inventory[0] = item; d.selectedItem = 0;
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
				if (p.active && p.type == item.shoot && p.TryGetGlobalProjectile(out HeldWeaponProjectile g) && g.Chain && g.ItemType == item.type
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
			try
			{
				EnemyTargeting.Swap(body); // dummy -> Main.player[255]
				swapped = true;
				if (!holder || !authority) { orig(p); return; } // beams and client copies: only the anchor

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
				if (g.CycleRelease && feed)
				{
					++g.HeldTicks;
					release = g.HeldTicks > g.HoldMax;
					if (g.HeldTicks > g.HoldMax + g.Linger) { p.Kill(); return; }
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
				orig(p);
				if (release && p.active && !g.CycleRelease) { g.HeldTicks = 0; p.Kill(); } // the release: the charged shot is made while the soldier is still the owner (dummy swapped in, cursor on the target)
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
					if (swapped) EnemyTargeting.Unswap();
					EnemyTargeting.EnsureSlotRestored(); // never leave a dummy in Main.player[255]
				}
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
			ArkSwings.DamageScope ark = g.Ark != ArkRole.None ? new ArkSwings.DamageScope(body, p, g) : default; // an Ark's blade reads its owner's centre: the real player stands at the soldier for the call
			BiomeBlades.DamageScope bb = g.Biome != 0 ? new BiomeBlades.DamageScope(body, p, g) : default; // a Biome Blade's too, with the soldier's blade in the real player's hand
			try
			{
				p.owner = SoldierMinions.TrueMyPlayer;
				orig(p);
			}
			finally
			{
				if (p.type == savedType) p.owner = savedOwner; // undo only our own edit: if the cascade killed p and recycled its slot, the new projectile keeps its owner
				ark.Dispose();
				bb.Dispose();
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
				if (c.aiStyle == ProjAIStyleID.HeldProjectile || c.aiStyle == ProjAIStyleID.ThickLaser) JoinChain(body, c, item);
				else if (ArkSwings.AdoptChild(body, holder, c, item)) { }
				else if (BiomeBlades.AdoptChild(body, holder, c, item)) { }
				else if (ModShotKinds.ChainExtras(item) && c.ModProjectile != null && ((holder.TryGetGlobalProjectile(out HeldWeaponProjectile pg) && pg.BoundChild) || OwnerLockProbe.BoundToHolder(c)))
				{
					// bound to its holder (reads the button / finds its parent by UUID) or made by a bound child (Starmageddon's stars, made by the star centre): stays in the chain, same owner, dummy and button as the holdout
					JoinChain(body, c, item);
					FixIdentity(c);
					if (c.TryGetGlobalProjectile(out HeldWeaponProjectile cg)) { cg.ModHolder = true; cg.BoundChild = true; if (holder.TryGetGlobalProjectile(out HeldWeaponProjectile hh)) cg.Script = hh.Script; }
					DudTrace.Note(item, "child-in-chain:" + (c.ModProjectile?.Name ?? c.type.ToString()));
				}
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
		/// <summary>A modded holdout run as the holder (server side only; see ModShotKinds). ReleaseOnKill: it makes its shot when it ends, so it is held for HeldModHoldTicks and then released.</summary>
		public bool ModHolder, ReleaseOnKill;
		/// <summary>A charge-by-button holdout (see ModShotKinds.ChargeRelease): held HoldMax ticks, released, ended Linger ticks later.</summary>
		public bool CycleRelease;
		/// <summary>A projectile a holdout's AI made that stays bound to it (see OwnerLockProbe.BoundToHolder): it and what it spawns stay in the chain.</summary>
		public bool BoundChild;
		public int HeldTicks, HoldMax, Linger;
		/// <summary>An Ark's swung blade or parry holdout (or a child bound to the blade): see <see cref="ArkSwings"/>.</summary>
		public ArkRole Ark;
		/// <summary>A Biome Blade's attunement blade (1) or a child bound to it (2): see <see cref="BiomeBlades"/>. Server side only.</summary>
		public byte Biome;
		/// <summary>The Ark's reach and throw numbers, for its blade (server side only).</summary>
		internal ArkSpec ArkSpec;
		/// <summary>The parry has struck an enemy (it empowers once); and whether that enemy could hurt the soldier by touch (the parry's immunity window needs it).</summary>
		public bool ParryHit, ParryContact, SnapCounted;
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

		/// <summary>An Ark's parry stab connected: remembers whether the enemy has contact damage (the parry's immunity window is only granted against that).</summary>
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Chain && Ark == ArkRole.Parry && !ParryHit) { ParryHit = true; ParryContact = target.damage > 0; }
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

	/// <summary>
	/// Installs the Projectile.AI / Projectile.Damage wrappers (nested chain calls pass straight through) and keeps slot 255 clean around the projectile phase. The Damage detour also serves the
	/// driven minions of <see cref="SoldierMinions"/>, so there is exactly one Projectile.Damage hook in the mod.
	/// </summary>
	public class HeldWeaponSystem : ModSystem
	{
		public override void Load()
		{
			On_Projectile.AI += AiDetour;
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
		}

		public override void Unload()
		{
			On_Projectile.AI -= AiDetour;
			On_Projectile.Damage -= DamageDetour;
			On_Projectile.Update -= ChainUpdateDetour;
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
		{
			if (!HeldWeapons.IsChainOwner(self.owner) || _wrapDepth != 0 || self.ModProjectile == null || !self.TryGetGlobalProjectile(out HeldWeaponProjectile g) || !g.Chain || !g.ModHolder) { orig(self); return; }
			_wrapDepth++;
			try { HeldWeapons.RunAi(orig, self, g); }
			finally { _wrapDepth--; }
		}

		/// <summary>
		/// Projectile.Update ends with <c>if (owner &lt; 255 &amp;&amp; !Main.player[owner].active) Kill();</c>: a chain that lives in the throwaway slot below 255 (a UUID-bound holdout, see <see cref="HeldWeapons.OwnerSlotFor"/>) would be
		/// killed by that line every tick, because the slot's Player is only the soldier's dummy while the AI runs. Its whole Update runs with the dummy standing in the slot (what the dragons' Update wrapper does).
		/// </summary>
		private static void ChainUpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{
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
		}

		private static void AiDetour(On_Projectile.orig_AI orig, Projectile self)
		{
			if (!ShouldWrap(self, out HeldWeaponProjectile g)) { orig(self); return; }
			_wrapDepth++;
			try { HeldWeapons.RunAi(pp => orig(pp), self, g); }
			finally { _wrapDepth--; }
		}

		/// <summary>
		/// The ONE Projectile.Damage detour of the mod. A chain projectile (held-weapon beam) is handed to the real player for the call; a driven minion in the middle of its own Update
		/// (SoldierMinions.RunUpdate has the dummy swapped in) arms the strike window instead, so the kill cascade of a lethal hit resolves in the real-player context while the call's damage
		/// arithmetic keeps running against the dummy, unchanged (see SoldierMinions.RunDamage / RunStrike). Everything else is vanilla.
		/// </summary>
		private static void DamageDetour(On_Projectile.orig_Damage orig, Projectile self)
		{
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
		}

		/// <summary>True for a driven minion inside its own Update on the server / single player, and not nested in another wrapper. The frame test is a single reference compare and is false for almost every call.</summary>
		private static bool WantsMinionWindow(Projectile p) => SoldierMinions.IsFrameProjectile(p) && _wrapDepth == 0 && LevyUtil.IsServerOrSingle;
	}
}
