using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common;
using Levy.Common.Config;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// TILE HAZARDS (config <c>SoldiersTakeTileHazards</c>, on by default). Vanilla hurts only players with touch tiles (Player.GetHurtTile / ApplyTouchDamage), so with the setting on a levy that overlaps one takes what
	/// a player would: <see cref="TileID.Sets.TouchDamageImmediate"/> tiles (spikes, wooden spikes, jungle / corruption / crimson thorns, cactus in a hostile world) strike through the ordinary defense formula,
	/// <see cref="TileID.Sets.TouchDamageHot"/> tiles (meteorite, hellstone, ...) set Burning, <see cref="TileID.Sets.TouchDamageBleeding"/> tiles set Bleeding. Immunity frames and the Fire Gauntlet / Obsidian fireWalk rule are the player's.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Harness override: -1 follows the config, 0 off, 1 on.</summary>
		internal static int TileHazardsOverride = -1;
		private static bool TileHazardsOn
		{
			get
			{
				if (TileHazardsOverride >= 0) return TileHazardsOverride == 1;
				try { return LevyConfig.Get.SoldiersTakeTileHazards; } catch (Exception) { return false; }
			}
		}

		/// <summary>Harness telemetry: touch-damage strikes taken and the damage they did.</summary>
		internal int HazardHits, HazardDamageTotal;

		/// <summary>The rule: a tile hurts when its type has any touch damage set (the same sets Collision.CanTileHurt reads). Used by the boot self-check.</summary>
		internal static bool HurtsOnTouch(int tileType)
			=> tileType >= 0 && tileType < TileID.Count
				&& (TileID.Sets.TouchDamageImmediate[tileType] > 0 || TileID.Sets.TouchDamageHot[tileType] || TileID.Sets.TouchDamageBleeding[tileType]);

		private void TileHazards()
		{
			if (!TileHazardsOn || LevyUtil.IsClient || OfficerMode || !IsAlive || Dummy == null) return;
			if ((Main.GameUpdateCount + (uint)NPC.whoAmI) % (uint)LevyConstants.TileHazardCheckTicks != 0) return;
			Collision.HurtTile hit = Collision.HurtTiles(NPC.position, NPC.width, NPC.height, Dummy);
			if (hit.type < 0 || TileID.Sets.Suffocate[hit.type]) return; // suffocation tiles are the player's alone
			int type = hit.type;
			// Player.ApplyTouchDamage ends with the same rule: a tile in TouchDamageDestroyTile (jungle, corruption and crimson thorns) is broken by the touch. The one way the army ever breaks a tile.
			if (TileID.Sets.TouchDamageDestroyTile[type]) BreakTouchedTile(hit.x, hit.y, type);
			if (TileID.Sets.TouchDamageHot[type]) NPC.AddBuff(BuffID.OnFire, LevyConstants.TileHazardBurnTicks);
			if (TileID.Sets.TouchDamageBleeding[type]) NPC.AddBuff(BuffID.Bleeding, Main.rand.Next(LevyConstants.TileHazardBleedMinTicks, LevyConstants.TileHazardBleedMaxTicks));
			int damage = TileID.Sets.TouchDamageImmediate[type];
			if (damage <= 0) return;
			TakeHazardDamage(Main.DamageVar(damage, 0f), type);
		}

		/// <summary>Harness telemetry: touch-broken tiles.</summary>
		internal int HazardBreaks;

		/// <summary>The vanilla contact break: WorldGen.KillTile on the touched tile, and on a server the same kill-tile-no-item packet a client sends for its own touch. Only ever called for a TouchDamageDestroyTile tile.</summary>
		private void BreakTouchedTile(int x, int y, int type)
		{
			if (!WorldGen.InWorld(x, y, 1)) return;
			Tile t = Main.tile[x, y];
			if (!t.HasTile || t.TileType != type) return;
			WorldGen.KillTile(x, y, false, false, false);
			HazardBreaks++;
			if (Main.netMode == NetmodeID.Server && !Main.tile[x, y].HasTile) NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 4, x, y);
		}

		private void TakeHazardDamage(int damage, int tileType)
		{
			if (TryShadowDodge()) return;
			LastAttackerIndex = -1; LastAttackerType = LevyConstants.EnvKillerHazard; LastAttackerName = LevyUtil.T("Damage.KillerHazard");
			int done;
			_contactStriking = true;
			PendingContactScale = IncomingSetScale();
			try { done = NPC.SimpleStrikeNPC(damage, 0, false, 0f, null, false, 0f, true); }
			finally { _contactStriking = false; PendingContactScale = 1f; }
			HazardHits++; HazardDamageTotal += done;
			AfterHit(done, -1);
		}
	}
}
