using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Levy.Common;
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
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Hash of the visible gear last copied into the dummy (type/prefix of every slot, sidearm flag, grade).</summary>
		private int _drawGearHash = int.MinValue;
		/// <summary>Game tick the dummy's frame counters were last advanced on; animation advances once per tick, not per draw.</summary>
		private uint _drawAnimTick = uint.MaxValue;
		private static bool _dyeRefreshBroken;

		partial void Draw_PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, ref bool drawn)
		{
			if (Main.dedServ || Record == null || !NPC.active) return;
			if (!IsAlive) { drawn = true; return; } // dying bodies draw nothing while the roster handles the death
			EnsureDummy();
			UISystem.SoldiersOnScreen++;

			RefreshDummyGear();
			SyncDummyTransform();
			Dummy.gfxOffY = NPC.gfxOffY;
			Dummy.lastVisualizedSelectedItem = Dummy.inventory[0]; // the renderer draws this, not inventory[selectedItem]
			PoseDummy();
			uint tick = Main.GameUpdateCount;
			if (tick != _drawAnimTick)
			{
				_drawAnimTick = tick;
				AnimateDummy();
			}
			DrawDummy(spriteBatch, screenPos);
			drawn = true;
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
		/// </summary>
		private void PoseDummy()
		{
			var p = Dummy;
			bool airborne = NPC.velocity.Y != 0f;
			p.controlLeft = NPC.velocity.X < 0f;
			p.controlRight = NPC.velocity.X > 0f;
			p.controlJump = airborne;
			p.wingTime = airborne ? 1f : 0f;
			p.jump = 0;

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
				if (anim <= 0 && style == ItemUseStyleID.Shoot && Weapon != null && Weapon.IsRanged
					&& (State == SoldierState.Engage || State == SoldierState.Shaken))
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
					var pos = new Vector2(Dummy.position.X, Dummy.position.Y + NPC.gfxOffY);
					Main.PlayerRenderer.DrawPlayer(Main.Camera, Dummy, pos, Dummy.fullRotation, Dummy.fullRotationOrigin, 0f, 1f);
				}
				finally { sb.End(); }
			}
			finally
			{
				// exactly what DoDraw_DrawNPCsOverTiles / DoDraw_DrawNPCsBehindTiles began with
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
			}
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
