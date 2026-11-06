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
	///    <c>Main.player[npc.target].dead</c>. aggro only biases the distance, so it cannot exclude the only player. So for the NPC update window (<see cref="SpectatorNpcWindow"/>: ModSystem.PreUpdateNPCs to PostUpdateNPCs,
	///    which also covers NPC.SpawnNPC) a spectator is set dead+ghost, exactly what every enemy and boss AI already handles. A boss with nobody else to fight behaves as if the target died (it leaves).
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

		internal void Hide() { if (_hidden) return; _hidden = true; _savedDead = Player.dead; _savedGhost = Player.ghost; Player.dead = true; Player.ghost = true; }
		internal void Unhide() { if (!_hidden) return; _hidden = false; Player.dead = _savedDead; Player.ghost = _savedGhost; }
		internal bool Hidden => _hidden;

		public override void PreUpdate()
		{
			Unhide(); // safety net: never let the targeting flags reach the player's own update
			bool on = Active;
			if (on && !_was) Anchor = Player.Center;
			_was = on;
			if (!on || Player.whoAmI != Main.myPlayer) return;
			// no tile interaction except a Static Camera under the cursor
			if (Player.controlUseTile && !OverStaticCamera()) Player.controlUseTile = false;
		}

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

		public override void PostHurt(Player.HurtInfo info)
		{
			LastHurtTick = Main.GameUpdateCount;
			if (Active) Player.ClearBuff(ModContent.BuffType<SpectatorBuff>());
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

		public override void PostUpdate()
		{
			if (!Active || Player.whoAmI != Main.myPlayer || Player.dead) return;
			Vector2 v = Player.velocity;
			Vector2 c = Player.Center;
			Tether(Anchor, ref c, ref v, out Vector2 pull);
			Player.velocity = v;
			if (pull != Vector2.Zero) { Player.velocity += pull; Player.fallStart = (int)(Player.position.Y / 16f); } // being pulled back must not count as a fall
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

	/// <summary>Brackets the NPC update (spawning, targeting, AI) so spectators are invisible to it. See <see cref="SpectatorPlayer"/>.</summary>
	public class SpectatorNpcWindow : ModSystem
	{
		public override void PreUpdateNPCs()
		{
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.active && p.TryGetModPlayer(out SpectatorPlayer s) && s.Active) s.Hide();
			}
		}

		public override void PostUpdateNPCs()
		{
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.TryGetModPlayer(out SpectatorPlayer s)) s.Unhide();
			}
		}
	}
}
