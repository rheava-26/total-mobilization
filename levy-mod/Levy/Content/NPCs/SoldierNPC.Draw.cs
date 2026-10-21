using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Levy.Common;
using Levy.Common.Config;
using Levy.Common.UI;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Draws the soldier body through the vanilla player renderer (client only).
	///
	/// Sprite-batch contract (read from Main.cs / LegacyPlayerRenderer.cs / PlayerDrawLayers.cs, tModLoader 1.4.4.9):
	/// * Main.DrawNPCs runs inside a batch begun by DoDraw_DrawNPCsOverTiles / DoDraw_DrawNPCsBehindTiles as
	///   Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform).
	/// * IPlayerRenderer.DrawPlayer(camera, player, position, rotation, origin, shadow, scale) itself never calls Begin/End. Only
	///   DrawPlayerFull (the DrawPlayers path) does: Begin(Immediate, AlphaBlend, camera.Sampler, None, camera.Rasterizer, null,
	///   camera.GameViewMatrix.TransformationMatrix) ... End().
	/// * The renderer's final stage, PlayerDrawLayers.DrawPlayer_RenderAllLayers, does not go through the sprite batch at all: it
	///   uploads a SpriteDrawBuffer and issues DrawIndexedPrimitives directly, applying dye shaders per sprite. That only works
	///   when an Immediate Begin has just bound the sprite effect, the view matrix and the render states. Inside a Deferred batch
	///   the queued NPC sprites are not flushed yet and the device state is whatever the previous flush left.
	/// So this file ends the NPC batch, draws the body inside its own Immediate batch (same sampler, rasterizer and matrix the
	/// vanilla player pass uses), ends it, and restarts the NPC batch with exactly the parameters DoDraw used.
	///
	/// Main.DrawNPCs deactivates any NPC whose draw throws, which would delete the soldier. Nothing here may throw: on the
	/// first failure the renderer path is disabled for the session and bodies fall back to the placeholder sprite.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Hash of the visible gear last copied into the dummy (type/prefix of every slot, sidearm flag, grade).</summary>
		private int _drawGearHash = int.MinValue;
		/// <summary>Game tick the dummy's frame counters were last advanced on; animation advances once per tick, not per draw.</summary>
		private uint _drawAnimTick = uint.MaxValue;
		private static bool _dyeRefreshBroken;
		private static bool _rendererBroken;

		partial void Draw_PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, ref bool drawn)
		{
			if (Main.dedServ || Record == null || !NPC.active) return;
			if (!IsAlive) { drawn = true; return; } // dying bodies draw nothing while the roster handles the death
			if (_rendererBroken) return;             // placeholder texture draws instead
			EnsureDummy();
			uint tick = Main.GameUpdateCount;
			bool newTick = tick != _drawAnimTick;
			if (newTick) UISystem.SoldiersOnScreen++; // once per tick per body, whatever the draw rate
			try
			{
				RefreshDummyGear();
				SyncDummyTransform();
				Dummy.gfxOffY = NPC.gfxOffY;
				Dummy.lastVisualizedSelectedItem = Dummy.inventory[0]; // the renderer draws this, not inventory[selectedItem]
				PoseDummy();
				if (newTick)
				{
					_drawAnimTick = tick;
					AnimateDummy();
				}
				DrawDummy(spriteBatch, screenPos);
				drawn = true;
			}
			catch (Exception e)
			{
				_rendererBroken = true;
				Levy.Instance.Logger.Error("Levy: drawing a soldier through the player renderer failed; soldiers fall back to the placeholder sprite: " + e);
			}
		}

		partial void Draw_PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (Main.dedServ || Record == null || !IsAlive) return;
			Nameplates.Draw(this, spriteBatch, screenPos);
		}

		/// <summary>Re-equips the dummy when the visible gear changed. Allocation-free unless something changed.</summary>
		private void RefreshDummyGear()
		{
			var g = Record.Gear;
			int h = UsingSidearm ? 1 : 0;
			h = h * 31 + (int)Grade;
			h = GearHash(h, g.Weapon); h = GearHash(h, g.Sidearm);
			h = GearHash(h, g.Head); h = GearHash(h, g.Body); h = GearHash(h, g.Legs);
			for (int i = 0; i < g.Accessories.Length; i++) h = GearHash(h, g.Accessories[i]);
			for (int i = 0; i < g.Dyes.Length; i++) h = GearHash(h, g.Dyes[i]);
			if (h == _drawGearHash) return;
			_drawGearHash = h;
			EquipDummy();
			if (_dyeRefreshBroken) return;
			try { Dummy.UpdateDyes(); }
			catch (Exception e)
			{
				_dyeRefreshBroken = true;
				Levy.Instance.Logger.Warn("Levy: dye refresh failed on a soldier body; dyes stay off: " + e);
			}
		}

		private static int GearHash(int h, Item item)
		{
			if (item == null) return h * 31;
			h = h * 31 + item.type;
			return h * 31 + item.prefix;
		}

		/// <summary>
		/// Sets the inputs Player.PlayerFrame / WingFrame / the use-style code read: facing, movement flags, item animation,
		/// item rotation and location. Runs every draw (cheap, allocation-free) so the pose never lags the synced state.
		/// Only animation inputs are written; wingTime/jump and the stat fields stay untouched (the Movement agent may own them).
		/// </summary>
		private void PoseDummy()
		{
			var p = Dummy;
			bool airborne = NPC.velocity.Y != 0f;
			p.controlLeft = NPC.velocity.X < 0f;
			p.controlRight = NPC.velocity.X > 0f;
			p.controlJump = airborne; // wings animate while airborne, flap while rising (see AnimateDummy)

			Item held = p.inventory[0];
			bool holdsItem = held != null && !held.IsAir;
			int anim = ItemAnimation;
			int animMax = Math.Max(1, ItemAnimationMax);
			int dir = NPC.direction == 0 ? 1 : NPC.direction;
			bool aimed = false;
			if (holdsItem)
			{
				int style = held.useStyle;
				bool aimStyle = style == ItemUseStyleID.Shoot || style == ItemUseStyleID.Rapier;
				bool rangedHeld = held.useAmmo > 0 || (held.mana > 0 && held.shoot > 0); // guns/bows/magic, not spears
				if (anim <= 0 && style == ItemUseStyleID.Shoot && rangedHeld
					&& (State == SoldierState.Engage || State == SoldierState.Shaken || State == SoldierState.Routed))
				{
					// keep the gun/bow/staff shouldered between shots while engaging
					anim = LevyConstants.Draw.RangedPoseHoldAnimation;
					animMax = Math.Max(animMax, anim);
				}
				if (anim > 0 && aimStyle)
				{
					float c = MathF.Cos(AimRotation);
					if (c != 0f) dir = c > 0f ? 1 : -1; // shooters face the target, like a player facing the cursor
					aimed = true;
				}
			}
			p.direction = dir;
			p.itemAnimation = anim;
			p.itemAnimationMax = animMax;
			// vanilla ItemCheck clears the composite arms every tick before the use style re-enables them
			p.SetCompositeArmFront(enabled: false, Player.CompositeArmStretchAmount.Full, 0f);
			p.SetCompositeArmBack(enabled: false, Player.CompositeArmStretchAmount.Full, 0f);

			if (holdsItem && anim > 0)
			{
				if (aimed)
				{
					// vanilla Shoot: itemRotation = Atan2(aim.Y * direction, aim.X * direction)
					p.itemRotation = MathF.Atan2(MathF.Sin(AimRotation) * dir, MathF.Cos(AimRotation) * dir);
				}
				Rectangle frame = Item.GetDrawHitbox(held.type, p);
				p.ItemCheck_ApplyUseStyle(p.HeightOffsetHitboxCenter, held, frame);
			}
			else
			{
				p.itemRotation = 0f;
				p.itemLocation = p.position;
			}
		}

		/// <summary>Advances leg/body/wing frames once per game tick (PlayerFrame reads the pose set by <see cref="PoseDummy"/>).</summary>
		private void AnimateDummy()
		{
			var p = Dummy;
			p.PlayerFrame(); // also resolves head/body/legs slots and visible accessories (sets p.wings from armor[].wingSlot)
			if (p.wings > 0) p.WingFrame(p.controlJump && NPC.velocity.Y < 0f);
			else { p.wingFrame = 0; p.wingFrameCounter = 0; }
		}

		/// <summary>Ends the Deferred NPC batch, draws the tint and the body in an Immediate batch, and restarts the NPC batch.</summary>
		private void DrawDummy(SpriteBatch sb, Vector2 screenPos)
		{
			sb.End();
			try
			{
				sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
				try
				{
					if (Shaken || State == SoldierState.Shaken) DrawShakenTint(sb, screenPos);
					if (Record != null && Record.Mutinous && LevyConfig.Get.WageMutiny) DrawMutinyTint(sb, screenPos);
					DrawDashShadows(); // dash afterimages behind the body (SoldierNPC.MovementFx.cs)
					var pos = new Vector2(Dummy.position.X, Dummy.position.Y + NPC.gfxOffY);
					Main.PlayerRenderer.DrawPlayer(Main.Camera, Dummy, pos, Dummy.fullRotation, Dummy.fullRotationOrigin, 0f, 1f);
					DrawSwordSlash(sb);
				}
				finally { sb.End(); }
			}
			finally
			{
				// exactly what DoDraw_DrawNPCsOverTiles / DoDraw_DrawNPCsBehindTiles began with
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
			}
		}

		// ------------------------------------------------------------------------------------------------------------
		// Big sword slash (Night's Edge / Excalibur / True Excalibur / Terra Blade / Horseman's Blade families)
		//
		// In vanilla these swords are useStyle Swing + noMelee: the large golden/purple/green arc is NOT the held item sprite but a
		// separate projectile (aiStyle 190). Player.ItemCheck_Shoot spawns it as NewProjectile(MountedCenter, (direction,0), proj, ...,
		// ai0 = direction*gravDir, ai1 = itemAnimationMax, ai2 = GetAdjustedItemScale). That projectile reads Main.player[owner], so a soldier
		// (whose owner would be the commander) never spawns it (WeaponUse.StartSwing deliberately skips IsHeldSlashProjectile). The held
		// item sprite itself is already drawn exactly like a player's by DrawPlayer (Swing pose + GetAdjustedItemScale); only the arc was
		// missing. It is re-drawn here from the same inputs the projectile uses: Projectile.AI_190_NightsEdge (rotation / scale / centre)
		// and Main.DrawProj_NightsEdge / _Excalibur / _TrueExcalibur / _TerraBlade2 / _TheHorsemansBlade (layers and colours). The star
		// sparkles (Main.DrawPrettyStarSparkle is private) are left out; the sprite layers are the visible arc. Draw only: no hitbox, no
		// projectile, no reach change.
		// ------------------------------------------------------------------------------------------------------------
		private const int ProjNightsEdge = 972, ProjExcalibur = 982, ProjTrueExcalibur = 983, ProjTerraBlade = 984, ProjHorsemansBlade = 997;

		/// <summary>The arc projectile type(s) a held item's swing throws in vanilla (Player.ItemCheck_Shoot), or 0 when it has none.</summary>
		private static int SlashProjectile(Item held, out int second)
		{
			second = 0;
			switch (held.type)
			{
				case ItemID.NightsEdge: return ProjNightsEdge;
				case ItemID.TrueNightsEdge: return ProjNightsEdge; // its own projectile 973 is the flown blade; the arc is the Night's Edge one
				case ItemID.Excalibur: return ProjExcalibur;
				case ItemID.TrueExcalibur: second = ProjExcalibur; return ProjTrueExcalibur; // vanilla throws both arcs (the 982 one with no damage)
				case ItemID.TerraBlade: return ProjTerraBlade;
				case ItemID.TheHorsemansBlade: return ProjHorsemansBlade;
			}
			switch (held.shoot)
			{
				case ProjNightsEdge: case ProjExcalibur: case ProjTrueExcalibur: case ProjTerraBlade: case ProjHorsemansBlade: return held.shoot;
			}
			return 0;
		}

		private void DrawSwordSlash(SpriteBatch sb)
		{
			Player p = Dummy;
			Item held = p.inventory[0];
			int anim = ItemAnimation;
			if (held == null || held.IsAir || anim <= 0 || held.useStyle != ItemUseStyleID.Swing) return;
			int proj = SlashProjectile(held, out int second);
			if (proj == 0) return;
			int animMax = Math.Max(1, ItemAnimationMax);
			// Projectile.localAI[0] is 1 on its first tick and reaches ai1 (= itemAnimationMax) on its last; ItemAnimation counts animMax..1.
			float t = Math.Clamp(animMax - anim + 1, 1, animMax) / (float)animMax;
			float aiDir = p.direction >= 0 ? 1f : -1f;
			float itemScale = p.GetAdjustedItemScale(held);
			Vector2 center = p.RotatedRelativePoint(p.MountedCenter) - new Vector2(aiDir, 0f) - Main.screenPosition; // AI_190: Center = MountedCenter - velocity
			DrawSlashArc(sb, proj, center, aiDir, t, itemScale, p.fullRotation);
			if (second != 0) DrawSlashArc(sb, second, center, aiDir, t, itemScale, p.fullRotation);
		}

		private static void DrawSlashArc(SpriteBatch sb, int type, Vector2 center, float aiDir, float t, float itemScale, float fullRotation)
		{
			Main.instance.LoadProjectile(type);
			Texture2D tex = TextureAssets.Projectile[type].Value;
			if (tex == null) return;
			// AI_190_NightsEdge: rotation / scale
			float rotation = MathF.PI * aiDir * t + (aiDir > 0f ? 0f : MathF.PI) + aiDir * MathF.PI + fullRotation;
			float growth = 0.2f, baseScale = 1f;
			switch (type)
			{
				case ProjExcalibur: case ProjHorsemansBlade: case ProjTerraBlade: growth = 0.6f; break;
				case ProjTrueExcalibur: growth = 1f; baseScale = 1.2f; break;
			}
			float num = (baseScale + t * growth) * itemScale * 1.1f; // DrawProj_*: proj.scale * 1.1
			Rectangle body = tex.Frame(1, 4);
			Rectangle core = tex.Frame(1, 4, 0, 3);
			Vector2 origin = body.Size() / 2f;
			SpriteEffects fx = aiDir >= 0f ? SpriteEffects.None : SpriteEffects.FlipVertically;
			float fade = Utils.Remap(t, 0f, 0.6f, 0f, 1f) * Utils.Remap(t, 0.6f, 1f, 1f, 0f); // num3
			float lit = Lighting.GetColor((center + Main.screenPosition).ToTileCoordinates()).ToVector3().Length() / (float)Math.Sqrt(3.0);
			if (type == ProjTerraBlade) lit = 0.5f + lit * 0.5f;
			lit = Utils.Remap(lit, 0.2f, 1f, 0f, 1f);
			const float trim = 0.975f;

			Color back, c2, c3;
			switch (type)
			{
				case ProjNightsEdge: back = new Color(40, 20, 60); c2 = new Color(80, 40, 180); c3 = Color.White; break;
				case ProjExcalibur: back = new Color(180, 160, 60); c2 = new Color(255, 240, 150); c3 = new Color(255, 255, 80); break;
				case ProjTrueExcalibur:
					back = Color.Lerp(new Color(180, 50, 90), new Color(180, 30, 60), fade);
					c2 = Color.Lerp(new Color(255, 240, 150), new Color(255, 60, 170), fade);
					c3 = Color.Lerp(new Color(255, 255, 80), new Color(255, 60, 190), fade); break;
				case ProjTerraBlade: back = new Color(45, 124, 205); c2 = new Color(181, 230, 29); c3 = new Color(34, 177, 76); break;
				default: back = new Color(193, 43, 43); c2 = new Color(247, 115, 0); c3 = new Color(255, 202, 130); break; // Horseman's Blade
			}
			Color haze = Color.White * fade * 0.5f;
			haze.A = (byte)(haze.A * (1f - lit));
			Color haze2 = haze * lit * 0.5f;
			haze2.G = (byte)(haze2.G * lit);
			if (type == ProjNightsEdge) haze2.R = (byte)(haze2.R * (0.25f + lit * 0.75f));
			else haze2.B = (byte)(haze2.R * (0.25f + lit * 0.75f)); // vanilla derives B from R in the gold / red / green arcs
			float tail = rotation + aiDir * (MathF.PI / 4f) * -1f * (1f - t);

			if (type == ProjNightsEdge)
			{
				sb.Draw(tex, center, body, back * lit * fade, tail, origin, num * trim, fx, 0f);
				sb.Draw(tex, center, body, haze2 * 0.15f, rotation + aiDir * 0.01f, origin, num, fx, 0f);
				sb.Draw(tex, center, body, new Color(80, 30, 160) * lit * fade * 0.3f, rotation, origin, num * 0.8f, fx, 0f);
				sb.Draw(tex, center, body, c2 * lit * fade * 0.7f, rotation, origin, num * trim, fx, 0f);
				sb.Draw(tex, center, core, Color.White * 0.3f * fade * (1f - lit * 0.7f), rotation + aiDir * 0.01f, origin, num, fx, 0f);
				return;
			}
			float backScale = type == ProjTerraBlade ? num * 0.95f : num;
			sb.Draw(tex, center, body, back * lit * fade, tail, origin, backScale, fx, 0f);
			sb.Draw(tex, center, body, haze2 * 0.15f, rotation + aiDir * 0.01f, origin, num, fx, 0f);
			sb.Draw(tex, center, body, c3 * lit * fade * 0.3f, rotation, origin, num, fx, 0f);
			sb.Draw(tex, center, body, c2 * lit * fade * 0.5f, rotation, origin, num * trim, fx, 0f);
			sb.Draw(tex, center, core, Color.White * 0.6f * fade, rotation + aiDir * 0.01f, origin, num, fx, 0f);
			sb.Draw(tex, center, core, Color.White * 0.5f * fade, rotation + aiDir * -0.05f, origin, num * 0.8f, fx, 0f);
			sb.Draw(tex, center, core, Color.White * 0.4f * fade, rotation + aiDir * -0.1f, origin, num * 0.6f, fx, 0f);
		}

		private void DrawMutinyTint(SpriteBatch sb, Vector2 screenPos)
		{
			Rectangle r = NPC.Hitbox;
			r.Inflate(LevyConstants.Draw.ShakenTintInflatePx, LevyConstants.Draw.ShakenTintInflatePx);
			r.X -= (int)screenPos.X; r.Y -= (int)screenPos.Y;
			sb.Draw(TextureAssets.MagicPixel.Value, r, LevyConstants.Mutiny.Tint);
		}

		private void DrawShakenTint(SpriteBatch sb, Vector2 screenPos)
		{
			Rectangle r = NPC.Hitbox;
			r.Inflate(LevyConstants.Draw.ShakenTintInflatePx, LevyConstants.Draw.ShakenTintInflatePx);
			r.X -= (int)screenPos.X; r.Y -= (int)screenPos.Y;
			sb.Draw(TextureAssets.MagicPixel.Value, r, LevyConstants.Draw.ShakenTint);
		}
	}
}
