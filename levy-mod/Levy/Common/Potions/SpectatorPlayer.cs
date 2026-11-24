using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.Buffs;
using Levy.Content.Tiles;

namespace Levy.Common.Potions
{
	/// <summary>
	/// The Spectator potion's rules.
	///  * Targeting. Vanilla picks targets in NPC.TargetClosest / TargetClosestUpgraded, which skip a player that is <c>!active || dead || ghost</c>, and boss/enemy AIs re-pick when
	///    <c>Main.player[npc.target].dead</c>. aggro only biases the distance, so it cannot exclude the only player. So around each hostile NPC's AI (<see cref="SpectatorNpcHide"/>: PreAI to PostAI; levies are skipped so they still see a living commander) a spectator is set dead+ghost, exactly what every enemy and boss AI already handles. Only NPCs the soldier swap cannot serve use this (see SpectatorNpcHide); for the rest EnemyTargeting excludes spectators from its search and the levies are the target (a spectator also unlocks boss aggro on soldiers for a boss already beaten in this world; an unbeaten boss is never affected: the buff cannot be drunk while one is alive and ends the moment one appears).
	///    The flags are restored right after, and again in PreUpdate as a safety net, so the player's own update never sees them.
	///  * Damage. Contact and projectile hits are refused (CanBeHitByNPC / CanBeHitByProjectile). Any damage that still lands (lava, fall, drowning) ends the buff.
	///  * Items. <see cref="CanUseItem"/> refuses damage items, summons, sentries and anything that touches the world (see <see cref="Blocked"/>), except the Observe items.
	///  * Tiles. Right-click interaction is cleared unless the cursor is on a Static Camera; noBuilding stops placing and breaking.
	///  * Tether. A soft wall <see cref="PotionConstants.TetherTiles"/> tiles around where the potion was drunk.
	/// </summary>
	public class SpectatorPlayer : ModPlayer
	{
		/// <summary>Game tick of the last damage taken (any source); -1 = never.</summary>
		public long LastHurtTick = -1;
		public Vector2 Anchor;
		private bool _was;
		private bool _hidden, _savedDead, _savedGhost;

		public bool Active => Player.active && Player.HasBuff<SpectatorBuff>();
		public static bool IsSpectator(Player p) => p != null && p.active && p.TryGetModPlayer(out SpectatorPlayer s) && s.Active;

		public bool RecentlyHurt => LastHurtTick >= 0 && Main.GameUpdateCount - LastHurtTick < PotionConstants.RecentDamageTicks;

		// ------------------------------------------------------------------ targeting window

		private int _hideDepth;
		internal void Hide() { if (_hideDepth++ > 0) return; _hidden = true; _savedDead = Player.dead; _savedGhost = Player.ghost; Player.dead = true; Player.ghost = true; }
		internal void Release() { if (_hideDepth > 0 && --_hideDepth == 0) Unhide(); }
		internal void Unhide() { _hideDepth = 0; if (!_hidden) return; _hidden = false; Player.dead = _savedDead; Player.ghost = _savedGhost; }

		internal static void InvalidateCache() => _anyStamp = uint.MaxValue;
		private static uint _anyStamp = uint.MaxValue; private static bool _any;
		/// <summary>True when any player has the Spectator buff (cached per tick). Enemy targeting opts in to the army when this is true.</summary>
		public static bool AnyActive
		{
			get
			{
				if (_anyStamp == Main.GameUpdateCount) return _any;
				_anyStamp = Main.GameUpdateCount; _any = false;
				for (int i = 0; i < Main.maxPlayers; i++) if (IsSpectator(Main.player[i])) { _any = true; break; }
				return _any;
			}
		}

		/// <summary>Hides every spectator from a vanilla search or AI (re-entrant: pair with <see cref="ReleaseAll"/>).</summary>
		public static void HideAll()
		{
			if (!AnyActive) return;
			for (int i = 0; i < Main.maxPlayers; i++) if (Main.player[i].TryGetModPlayer(out SpectatorPlayer sp) && sp.Active) sp.Hide();
		}

		public static void ReleaseAll()
		{
			if (!_any) return;
			for (int i = 0; i < Main.maxPlayers; i++) if (Main.player[i].TryGetModPlayer(out SpectatorPlayer sp)) sp.Release();
		}

		internal bool Hidden => _hidden;

		public override void PreUpdate()
		{
			Unhide(); // safety net: never let the targeting flags reach the player's own update
			bool on = Active;
			if (on && !_was) Anchor = Player.Center;
			bool local = Player.whoAmI == Main.myPlayer || (BetHarness && Player.whoAmI == 0);
			if (local && CheckForfeit(on)) { _was = false; return; }
			if (local) CheckBet(on || _was); // a buff cancelled or expired since the last tick still answers for what happened while it was on
			_was = on;
			if (!on || Player.whoAmI != Main.myPlayer) return;
			// The first kill of every boss is solo: an unbeaten boss in the world ends the buff at once.
			if (!Main.dedServ && global::Levy.Common.Targeting.EnemyTargeting.FindUndownedBoss() != null)
			{
				EndBuff();
				CombatText.NewText(Player.getRect(), new Color(255, 120, 100), global::Levy.Common.Util.LevyUtil.T("Potions.SpectatorBossEnds"));
				return;
			}
			// no tile interaction except a Static Camera under the cursor
			if (Player.controlUseTile && !OverStaticCamera()) Player.controlUseTile = false;
		}

		// ------------------------------------------------------------------ the forfeit

		private int _lastLeft;
		private bool _selfEnd;
		/// <summary>Ends the buff on the mod's own rules (damage taken, an unbeaten boss, a long teleport): not the player's doing, so not a forfeit.</summary>
		private void EndBuff() { _selfEnd = true; Player.ClearBuff(ModContent.BuffType<SpectatorBuff>()); }

		/// <summary>The most game ticks that can be left on the buff when it ran out by itself (it ticks down once per player update after this check).</summary>
		internal const int ExpirySlackTicks = 2;

		/// <summary>
		/// Once a tick for the local player. Removing the buff (right-click cancel or any removal that is not the mod's own rules or the timer running out) while a boss is alive kills the player at once.
		/// Returns true when it did. A buff that ran out of time had at most <see cref="ExpirySlackTicks"/> left when last seen and is not a forfeit.
		/// </summary>
		internal bool CheckForfeit(bool on)
		{
			if (on) { int i = Player.FindBuffIndex(ModContent.BuffType<SpectatorBuff>()); _lastLeft = i >= 0 ? Player.buffTime[i] : 0; return false; }
			bool removed = _was, self = _selfEnd, expired = _lastLeft <= ExpirySlackTicks;
			_selfEnd = false;
			if (!removed || self || expired || Player.dead || !SpectatorBet.AnyActiveBoss()) return false;
			_betArmySeen = false;
			SpectatorBet.Forfeit(Player);
			return true;
		}

		// ------------------------------------------------------------------ the bet

		private bool _betArmySeen;
		/// <summary>Self-check only: the stand-in commander in slot 0 answers for the bet as the local player does (a test server has no local player).</summary>
		internal static bool BetHarness;

		/// <summary>The army was on the field at some point of this boss fight under the Spectator (what makes its loss a lost bet).</summary>
		internal bool BetArmySeen => _betArmySeen;

		/// <summary>
		/// Once a tick, for the local player while the Spectator is on (or was on last tick): during a boss fight, an army that was fielded and is now wiped with no manpower left kills the player (<see cref="SpectatorBet"/>).
		/// Returns true when it did. Called from PreUpdate, so a wipe in the last tick's NPC pass is caught before the buff can be dropped.
		/// </summary>
		internal bool CheckBet(bool covered)
		{
			if (!covered || Player.dead) { _betArmySeen = false; ResetLeave(); return false; }
			if (SpectatorBet.AnyActiveBoss())
			{
				if (!_bossWas) { _bossWas = true; _fightStart = Main.GameUpdateCount; SpectatorBet.ResetLeave(); }
				_leaveTicks = 0; _goneAt = Main.GameUpdateCount;
				if (SpectatorBet.ArmyAlive(Player)) { _betArmySeen = true; return false; }
				if (!SpectatorBet.Lost(Player, _betArmySeen)) return false;
				_betArmySeen = false; ResetLeave();
				SpectatorBet.Kill(Player);
				return true;
			}
			if (!_bossWas) { _betArmySeen = false; return false; }
			// the boss is gone: after the grace a boss that left on its own (not killed, not for want of a target) is a lost bet
			if (++_leaveTicks < SpectatorBet.LeaveGraceTicks) return false;
			bool seen = _betArmySeen; uint start = _fightStart, gone = _goneAt;
			_betArmySeen = false; ResetLeave();
			LastLeave = SpectatorBet.LeaveVerdict(start, gone, SpectatorBet.BossKillTick, SpectatorBet.BossBlindTick, seen);
			if (LastLeave != SpectatorBet.LeaveResult.Lost) return false;
			SpectatorBet.KillLeft(Player);
			return true;
		}

		private bool _bossWas; private int _leaveTicks; private uint _fightStart, _goneAt;
		private void ResetLeave() { _bossWas = false; _leaveTicks = 0; }
		/// <summary>The verdict of the last boss that went (self-check).</summary>
		internal SpectatorBet.LeaveResult? LastLeave;
		/// <summary>The world was entered (a subworld transfer included): nothing carried over from the world before counts.</summary>
		public override void OnEnterWorld() { _betArmySeen = false; ResetLeave(); SpectatorBet.ResetLeave(); }

		public override void PostUpdateEquips()
		{
			if (Active) Player.noBuilding = true;
		}

		private static bool OverStaticCamera()
		{
			if (Main.dedServ) return false;
			Point t = Main.MouseWorld.ToTileCoordinates();
			if (!WorldGen.InWorld(t.X, t.Y, 5)) return false;
			Tile tile = Main.tile[t.X, t.Y];
			return tile.HasTile && tile.TileType == ModContent.TileType<StaticCameraTile>();
		}

		// ------------------------------------------------------------------ damage

		public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot) => !Active;
		public override bool CanBeHitByProjectile(Projectile proj) => !Active;

		// A Spectator deals no damage by any means. Player.ApplyDamageToNPC (every dash - Shield of Cthulhu, Solar flare, Calamity rams -, contact-damage mounts, auras and thorns-type
		// effects) exits early when CanHitNPC is false, and item swings ask CanHitNPCWithItem. Movement is untouched; only the damage is refused.
		public override bool CanHitNPC(NPC target) => !Active;
		public override bool? CanHitNPCWithItem(Item item, NPC target) => Active ? false : null;

		public override void PostHurt(Player.HurtInfo info)
		{
			LastHurtTick = Main.GameUpdateCount;
			if (Active) EndBuff();
		}

		// ------------------------------------------------------------------ items

		public override bool CanUseItem(Item item) => !Active || !Blocked(item);

		private static readonly HashSet<int> WorldTouching = new()
		{
			ItemID.Paintbrush, ItemID.PaintRoller, ItemID.PaintScraper, ItemID.SpectrePaintbrush, ItemID.SpectrePaintRoller, ItemID.SpectrePaintScraper,
			ItemID.EmptyBucket, ItemID.WaterBucket, ItemID.LavaBucket, ItemID.HoneyBucket, ItemID.BottomlessBucket, ItemID.BottomlessLavaBucket,
			ItemID.SuperAbsorbantSponge, ItemID.LavaAbsorbantSponge, 5302, 5303, 5304, 5364, // the remaining vanilla buckets and sponges (the ids Player.cs lists beside these)
			ItemID.Wrench, ItemID.BlueWrench, ItemID.GreenWrench, ItemID.YellowWrench, ItemID.MulticolorWrench, ItemID.WireCutter, ItemID.Actuator, ItemID.CombatWrench,
		};

		public static bool IsObserveItem(Item item) => item?.ModItem != null && item.ModItem.GetType().Namespace == "Levy.Content.Items.Observe";

		/// <summary>
		/// True for an item a Spectator may not use: anything with damage, summons and sentries, anything that places or breaks tiles or walls (blocks, picks, axes, hammers, wands, paint, buckets, wire tools),
		/// and bombs. The Observe items (GoPro, Static Camera, Mind Link) are always allowed.
		/// </summary>
		public static bool Blocked(Item item)
		{
			if (item == null || item.IsAir) return false;
			if (IsObserveItem(item)) return false;
			if (item.damage > 0) return true;
			if (item.sentry || item.DamageType.CountsAsClass<SummonDamageClass>()) return true;
			if (item.createTile >= 0 || item.createWall >= 0 || item.pick > 0 || item.axe > 0 || item.hammer > 0 || item.tileWand > 0 || item.mech) return true;
			if (WorldTouching.Contains(item.type) || ItemID.Sets.ItemsThatCountAsBombsForDemolitionistToSpawn[item.type]) return true;
			return false;
		}

		// ------------------------------------------------------------------ tether

		private Vector2 _lastCenter; private bool _haveLast;

		public override void PostUpdate()
		{
			if (!Active || Player.whoAmI != Main.myPlayer || Player.dead) { _haveLast = false; return; }
			// A teleport (Recall, Magic/Ice Mirror, Cell Phone, pylon, Wormhole, Teleportation Potion, Rod of Discord, a mod's own) is a one-tick jump no movement can make.
			// A short hop moves the tether anchor to where the Spectator now stands; a long one (see PotionConstants.TeleportEndTiles) ends the buff.
			if (_haveLast && TeleportResponse(_lastCenter, Player.Center, ref Anchor, out bool end))
			{
				if (end) { EndBuff(); _haveLast = false; return; }
			}
			_lastCenter = Player.Center; _haveLast = true;
			Vector2 v = Player.velocity;
			Vector2 c = Player.Center;
			Tether(Anchor, ref c, ref v, out Vector2 pull);
			Player.velocity = v;
			if (pull != Vector2.Zero) { Player.velocity += pull; Player.fallStart = (int)(Player.position.Y / 16f); } // being pulled back must not count as a fall
		}

		/// <summary>
		/// What a move from <paramref name="from"/> to <paramref name="to"/> in one tick means for the tether. Returns true when it was a teleport (at least <see cref="PotionConstants.TeleportDetectTiles"/>):
		/// <paramref name="end"/> is true when it covered <see cref="PotionConstants.TeleportEndTiles"/> or more (the buff ends); otherwise the anchor is moved to <paramref name="to"/>.
		/// </summary>
		public static bool TeleportResponse(Vector2 from, Vector2 to, ref Vector2 anchor, out bool end)
		{
			end = false;
			float tiles = Vector2.Distance(from, to) / 16f;
			if (tiles < PotionConstants.TeleportDetectTiles) return false;
			if (tiles >= PotionConstants.TeleportEndTiles) end = true; else anchor = to;
			return true;
		}

		/// <summary>
		/// Pure tether math. Inside the soft band the outward part of the velocity is damped; at the edge it is removed; beyond it <paramref name="pull"/> points back at the anchor
		/// (excess * <see cref="PotionConstants.TetherPullPerPx"/>, at most <see cref="PotionConstants.TetherMaxPull"/>).
		/// </summary>
		public static void Tether(Vector2 anchor, ref Vector2 center, ref Vector2 velocity, out Vector2 pull)
		{
			pull = Vector2.Zero;
			Vector2 off = center - anchor;
			float dist = off.Length();
			float edge = PotionConstants.TetherTiles * 16f, soft = PotionConstants.TetherSoftTiles * 16f;
			if (dist < edge - soft || dist < 0.001f) return;
			Vector2 dir = off / dist;
			float outward = Vector2.Dot(velocity, dir);
			if (outward > 0f)
			{
				float k = dist >= edge ? 1f : (dist - (edge - soft)) / soft; // 0 at the start of the band, 1 at the edge
				velocity -= dir * outward * k;
			}
			if (dist > edge) pull = -dir * Math.Min(PotionConstants.TetherMaxPull, (dist - edge) * PotionConstants.TetherPullPerPx);
		}
	}

	/// <summary>
	/// Fallback only: brackets the AI of hostile NPCs the soldier swap cannot serve so spectators are invisible to it (see <see cref="SpectatorPlayer"/>). Per NPC rather than around the whole NPC update, because the levies
	/// read their commander's dead flag and must keep seeing a living commander. A safety-net unhide runs after the NPC update.
	/// </summary>
	public class SpectatorNpcWindow : ModSystem
	{
		public override void PostUpdateNPCs() => UnhideAll();
		public override void PostUpdateProjectiles() { UnhideAll(); SpectatorHitGuard.Clear(); }

		internal static void UnhideAll()
		{
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.TryGetModPlayer(out SpectatorPlayer s)) s.Unhide();
			}
		}
	}

	/// <summary>
	/// A Spectator cannot be hurt by any enemy, including one that hurts without a hit. Vanilla asks a projectile or an NPC "can you hit this player" (<c>CanHitPlayer</c>) before the player's own refusal, and some bosses
	/// do their damage inside that question (the Brimstone Monster takes life straight off the player there and never makes a hit). So the question is answered "no" for a Spectator first, for every projectile and NPC.
	/// A hostile projectile's AI runs with the Spectator hidden from it (as an NPC's does, see <see cref="SpectatorNpcHide"/>), so a homing shot does not chase one either.
	/// </summary>
	public class SpectatorHitGuard : GlobalProjectile
	{
		public override bool CanHitPlayer(Projectile projectile, Player target) => !SpectatorPlayer.IsSpectator(target);

		public override bool PreAI(Projectile projectile)
		{
			if (!projectile.hostile || projectile.friendly || !SpectatorPlayer.AnyActive) return true;
			SpectatorPlayer.HideAll();
			_hid.Add(projectile.whoAmI);
			return true;
		}

		public override void PostAI(Projectile projectile)
		{
			if (_hid.Remove(projectile.whoAmI)) SpectatorPlayer.ReleaseAll();
		}

		private static readonly System.Collections.Generic.HashSet<int> _hid = new();
		internal static void Clear() { _hid.Clear(); }
	}

	public class SpectatorNpcGuard : GlobalNPC
	{
		public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => !SpectatorPlayer.IsSpectator(target);
		public override void OnKill(NPC npc) { if (SpectatorBet.IsBoss(npc)) SpectatorBet.NoteBossKilled(); }
	}

	public class SpectatorNpcHide : GlobalNPC
	{
		public override bool PreAI(NPC npc)
		{
			if (npc.friendly || npc.townNPC || npc.ModNPC is global::Levy.Content.NPCs.SoldierNPC) return true;
			// An NPC the soldier swap can serve gets its targets through EnemyTargeting (spectators are excluded from its search and the army fights instead).
			// Only the rest (swap-unsafe styles, no levies out) fall back to seeing the spectator as dead.
			if (global::Levy.Common.Targeting.EnemyTargeting.IsAllowlisted(npc) && global::Levy.Common.Targeting.EnemyTargeting.Bodies.Count > 0) return true;
			if (!global::Levy.Common.Targeting.EnemyTargeting.HidesSpectatorFrom(npc)) return true; // an unbeaten boss sees a spectator like any player
			if (!_hiding.Add(npc.whoAmI)) return true;
			if (SpectatorBet.IsBoss(npc)) SpectatorBet.NoteBossBlind(); // a boss that finds nobody to fight may leave: that is not the player's lost bet
			if (global::Levy.Common.Targeting.EnemyTargeting.DiagOn) SpectatorDiagnostic.NoteHidden(npc);
			SpectatorPlayer.HideAll();
			return true;
		}

		private static readonly System.Collections.Generic.HashSet<int> _hiding = new();

		public override void PostAI(NPC npc)
		{
			if (_hiding.Remove(npc.whoAmI)) SpectatorPlayer.ReleaseAll();
		}
	}
}
