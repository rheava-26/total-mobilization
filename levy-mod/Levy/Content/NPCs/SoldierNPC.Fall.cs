using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common;
using Levy.Common.Config;
using Levy.Common.Movement;
using Levy.Common.Util;
using H = Levy.Common.LevyConstants.Hooks;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// FALLING. Fall damage on the player's rules (Player.Update, the velocity.Y == 0 block): tiles fallen since the last reset, free up to 25 plus the gear's extra fall, then 10 damage per tile through the
	/// ordinary defense formula. Resets: standing, rising, water/lava/honey, cobweb, a reel on a hook, low gravity high in the sky. Spared by any wings, a fall-immunity accessory (the dummy's noFallDmg),
	/// and fall-negating tiles. And the saving throw: a soldier that is falling toward lava, a long drop, or the void with a hook in its slot casts to catch itself.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Harness override: -1 follows the config, 0 off, 1 on.</summary>
		internal static int FallDamageOverride = -1;
		private static bool FallDamageOn
		{
			get
			{
				if (FallDamageOverride >= 0) return FallDamageOverride == 1;
				try { return LevyConfig.Get.SoldiersTakeFallDamage; } catch (Exception) { return false; }
			}
		}
		private int _fallStartRow = int.MinValue;
		/// <summary>Ticks left after a teleport in which no fall counts; ends early on landing.</summary>
		private int _fallImmuneTicks;
		private int _fallPlanRetry;
		internal int FallHits, FallDamageTotal, LastFallTiles, FallCatches;

		/// <summary>The player's altitude test (Player.Update num5 &lt; 0.8): up in the sky the gravity is thin and a fall does not hurt.</summary>
		private static bool ThinAir(float posY)
		{
			float n4 = Main.maxTilesX / 4200f; n4 *= n4;
			float k = (float)((posY / 16f - (60f + 10f * n4)) / (Main.worldSurface / 6.0));
			if (k < 0.25f) k = 0.25f; if (k > 1f) k = 1f;
			return k < 0.8f;
		}

		private void Fall_Update()
		{
			if (OfficerMode) return;
			NPC npc = NPC;
			int row = (int)(npc.position.Y / 16f);
			if (_fallImmuneTicks > 0)
			{
				_fallImmuneTicks--; _fallStartRow = row;
				if (npc.velocity.Y == 0f && _fallImmuneTicks < LevyConstants.Movement.TeleportFallImmunityTicks - 2) _fallImmuneTicks = 0; // landed
				return;
			}
			if (_fallStartRow == int.MinValue || !FallDamageOn) { _fallStartRow = row; return; }
			if (npc.velocity.Y == 0f)
			{
				int tiles = row - _fallStartRow;
				if (tiles > 0)
				{
					int x0 = (int)(npc.position.X / 16f), x1 = (int)((npc.position.X + npc.width) / 16f), y2 = (int)((npc.position.Y + npc.height + 1f) / 16f);
					for (int x = x0; x <= x1; x++)
					{
						if (!TileNav.InBounds(x, y2)) continue;
						Tile t = Main.tile[x, y2];
						if (t.HasTile && TileID.Sets.NegatesFallDamage[t.TileType]) { tiles = 0; break; }
					}
				}
				int free = 25 + (Abilities?.ExtraFall ?? 0);
				if (tiles > free && !(Abilities?.NoFallDamage ?? false) && !ThinAir(npc.position.Y)) TakeFallDamage((tiles - free) * 10, tiles);
				_fallStartRow = row;
			}
			bool web = Collision.StickyTiles(npc.position, npc.velocity, npc.width, npc.height).Y != -1f;
			bool hooked = _hkPhase == HookPhase.InFlight || _hkPhase == HookPhase.Reel || _hkPhase == HookPhase.Cast;
			if (npc.velocity.Y < 0f || npc.wet || npc.honeyWet || npc.lavaWet || web || hooked || _flying || _clmOn || (Dummy != null && Dummy.slowFall) || ThinAir(npc.position.Y)) _fallStartRow = row;
		}

		private void TakeFallDamage(int damage, int tiles)
		{
			if (!IsAlive || LevyUtil.IsClient) return;
			LastFallTiles = tiles;
			if (TryShadowDodge()) return;
			LastAttackerIndex = -1; LastAttackerType = LevyConstants.EnvKillerFall; LastAttackerName = LevyUtil.T("Damage.KillerFall");
			int done;
			_contactStriking = true;
			PendingContactScale = IncomingSetScale();
			try { done = NPC.SimpleStrikeNPC(damage, 0, false, 0f, null, false, 0f, true); }
			finally { _contactStriking = false; PendingContactScale = 1f; }
			FallHits++; FallDamageTotal += done;
			AfterHit(done, -1);
		}

		// ---------------------------------------------------------------------------------------------
		// the saving throw
		// ---------------------------------------------------------------------------------------------
		/// <summary>Where the current fall ends: ticks to the end, and what is there (lava, void, floor depth).</summary>
		private bool PredictFall(out int ticks, out bool bad)
		{
			NPC npc = NPC; ticks = 0; bad = false;
			float y = npc.position.Y, vy = npc.velocity.Y, x = npc.position.X, vx = npc.velocity.X, g = Gravity();
			int w = npc.width, h = npc.height;
			int startRow = (int)(y / 16f);
			for (int t = 1; t <= 120; t++)
			{
				vy = Math.Min(H.TerminalVelocityPx, vy + g);
				y += vy; x += vx;
				int bx0 = (int)(x / 16f), bx1 = (int)((x + w - 1f) / 16f), by0 = (int)(y / 16f), by1 = (int)((y + h - 1f) / 16f), fr = (int)((y + h) / 16f);
				if (!TileNav.InBounds(bx0, fr) || !TileNav.InBounds(bx1, fr)) { ticks = t; bad = true; return true; }
				for (int tx = bx0; tx <= bx1; tx++)
				{
					for (int ty = by0; ty <= by1; ty++) if (TileNav.HasLava(tx, ty)) { ticks = t; bad = true; return true; }
					if (TileNav.IsDeepWater(tx, by1)) { ticks = t; return true; }
				}
				for (int tx = bx0; tx <= bx1; tx++)
					if (TileNav.IsFloor(tx, fr))
					{
						ticks = t;
						int tiles = (int)(y / 16f) - startRow;
						int free = 25 + (Abilities?.ExtraFall ?? 0);
						bool hurts = FallDamageOn && !Abilities.NoFallDamage && !ThinAir(y);
						bad = hurts && NPC.life > 0 && (tiles - free) * 10 * 4 >= NPC.life; // a landing that would take a quarter of the life or more
						return true;
					}
			}
			ticks = 120; return false;
		}

		/// <summary>Called every movement tick: a falling soldier whose fall ends badly looks for something to grab.</summary>
		private void FallCatchCheck()
		{
			NPC npc = NPC;
			if (_hkPhase != HookPhase.Idle || !HookUsable || npc.velocity.Y < 2f || npc.wet || _flying) return;
			if (_fallPlanRetry > 0) { _fallPlanRetry--; return; }
			if (Main.GameUpdateCount < _hkNextAllowed && _hkFailStreak > 0) return;
			if ((int)((Main.GameUpdateCount + (uint)npc.whoAmI) % 3u) != 0) return;
			if (!PredictFall(out int ticks, out bool bad) || !bad || ticks < 7) return;
			if (!SoldierHooks.TakePlanBudget()) return;
			Point feet = TileNav.FeetTile(npc.Bottom);
			// something to stand on that is not below the start of the fall: a ledge, a platform, the far side
			if (!TileNav.NearestStandable(feet.X, feet.Y, 18, out Point found) || found.Y > feet.Y + 3) { _fallPlanRetry = 20; return; }
			Vector2 goal = TileNav.BottomCentre(found.X, found.Y);
			_fallPlanRetry = 12;
			try { HookStart(HookReason.Rescue, goal); if (_hkPhase != HookPhase.Idle) FallCatches++; }
			catch (Exception e)
			{
				if (GearUpkeep.WarnOnce(e)) Levy.Instance?.Logger.Warn("Levy: fall-catch failed on soldier '" + SoldierName + "': " + e);
				try { HookReset(true); } catch (Exception) { }
			}
		}
	}
}
