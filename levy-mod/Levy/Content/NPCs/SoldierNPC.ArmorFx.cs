using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Armor VISUAL effects that vanilla applies to a Player and a levy's dummy never got (client only, on-screen bodies only).
	/// <para/>The flags (armorEffectDrawShadow / Subtle / Outlines / OutlinesForbidden / ShadowLokis / ShadowBasilisk: Ninja, Shadow, Spooky-style sets, modded ItemLoader.ArmorSetShadows)
	/// are set by Player.SetArmorEffectVisuals, which vanilla runs from ResetEffects off head/body/legs. The dummy only ever resolved those fields in the stat pass, one second late,
	/// so the flags are refreshed here every tick from the same fields. The afterimage history (shadowPos) is Player.UpdateSocialShadow, stepped once per tick like Player.Update
	/// does, and drawn the way LegacyPlayerRenderer.DrawPlayerFull does. Armor glow masks are renderer layers and already worked; head-slot lights are Player.UpdateArmorLights.
	/// <para/>Cost: at most 3 trail copies per body (1 when a crowd is on screen); outlines / subtle jitter / Lokis only while the crowd is small.
	/// </summary>
	public partial class SoldierNPC
	{
		private const int ArmorFxCrowd = 60;
		private static bool _armorFxBroken;

		/// <summary>Once per game tick per drawn body, after PlayerFrame: refresh the flags, step the afterimage history, shed the armor lights.</summary>
		private void ArmorFxTick()
		{
			if (Main.dedServ || _armorFxBroken) return;
			try
			{
				Player p = Dummy;
				p.head = p.armor[0].headSlot; p.body = p.armor[1].bodySlot; p.legs = p.armor[2].legSlot;
				if (p.stealth == 1f) p.SetArmorEffectVisuals(p); // only ever sets flags (and runs ItemLoader.ArmorSetShadows for modded sets)
				if (p.armorEffectDrawShadow || p.armorEffectDrawShadowLokis || p.armorEffectDrawShadowBasilisk)
				{
					if (_fxShadowTicks <= 0) p.UpdateSocialShadow(); // the dash code steps it itself while dashing
					if (p.armorEffectDrawShadowBasilisk && p.basiliskCharge < 1f) p.basiliskCharge = 1f; // no charge meter on a levy: show the full trail
				}
				p.UpdateArmorLights();
			}
			catch (Exception e)
			{
				_armorFxBroken = true;
				Levy.Instance.Logger.Warn("Levy: armor visual effects failed and are disabled for this session: " + e.Message);
			}
		}

		/// <summary>True when the body's own armor draws the afterimage trail, so the dash code must not draw it a second time.</summary>
		private bool ArmorDrawsTrail => Dummy != null && !_armorFxBroken && (Dummy.armorEffectDrawShadow || Dummy.armorEffectDrawShadowLokis || Dummy.armorEffectDrawShadowBasilisk);

		/// <summary>Draws the afterimages / outlines behind the body (inside the Immediate batch, before the body). Never throws.</summary>
		private void DrawArmorFx(Vector2 pos)
		{
			if (_armorFxBroken) return;
			Player p = Dummy;
			if (!(p.armorEffectDrawShadow || p.armorEffectDrawShadowSubtle || p.armorEffectDrawOutlines || p.armorEffectDrawOutlinesForbidden
				|| p.armorEffectDrawShadowLokis || p.armorEffectDrawShadowBasilisk)) return;
			try
			{
				bool crowd = global::Levy.Common.UI.UISystem.SoldiersOnScreenLastFrame > ArmorFxCrowd;
				if (!crowd && (p.armorEffectDrawOutlines || p.armorEffectDrawOutlinesForbidden))
				{
					// DrawPlayerFull: four offset copies pulsing with ghostFade
					float speed = p.armorEffectDrawOutlines ? 0.075f : 0.025f;
					if (!Main.gamePaused) p.ghostFade += p.ghostDir * speed;
					if (p.ghostFade < 0.1f) { p.ghostDir = 1f; p.ghostFade = 0.1f; }
					else if (p.ghostFade > 0.9f) { p.ghostDir = -1f; p.ghostFade = 0.9f; }
					float o = p.ghostFade * 5f;
					for (int l = 0; l < 4; l++)
					{
						Vector2 off = l == 0 ? new Vector2(o, 0f) : l == 1 ? new Vector2(-o, 0f) : l == 2 ? new Vector2(0f, o) : new Vector2(0f, -o);
						Main.PlayerRenderer.DrawPlayer(Main.Camera, p, pos + off, p.fullRotation, p.fullRotationOrigin, p.ghostFade);
					}
				}
				int copies = crowd ? 1 : 3;
				if (p.armorEffectDrawShadowBasilisk) copies = Math.Min(copies, Math.Max(1, (int)(p.basiliskCharge * 3f)));
				if (p.armorEffectDrawShadow || p.armorEffectDrawShadowBasilisk)
				{
					for (int k = 0; k < copies; k++)
						if (TrailPoint(p, k)) Main.PlayerRenderer.DrawPlayer(Main.Camera, p, p.shadowPos[k], p.shadowRotation[k], p.shadowOrigin[k], 0.5f + 0.2f * k);
				}
				if (p.armorEffectDrawShadowLokis && !crowd)
				{
					for (int k = 0; k < 3; k++)
						if (TrailPoint(p, k))
							Main.PlayerRenderer.DrawPlayer(Main.Camera, p, Vector2.Lerp(p.shadowPos[k], pos, 0.5f), p.shadowRotation[k], p.shadowOrigin[k], MathHelper.Lerp(1f, 0.5f + 0.2f * k, 0.5f));
				}
				if (p.armorEffectDrawShadowSubtle && !crowd)
				{
					for (int k = 0; k < 2; k++) // vanilla draws 4 jittered copies; two read the same at levy size
					{
						var j = new Vector2(Main.rand.Next(-20, 21) * 0.1f, Main.rand.Next(-20, 21) * 0.1f);
						Main.PlayerRenderer.DrawPlayer(Main.Camera, p, pos + j, p.fullRotation, p.fullRotationOrigin, 0.9f);
					}
				}
			}
			catch (Exception e)
			{
				_armorFxBroken = true;
				Levy.Instance.Logger.Warn("Levy: armor afterimages failed and are disabled for this session: " + e.Message);
			}
		}

		/// <summary>An unset history slot, or one left far behind by a teleport / respawn, is skipped (it would draw a ghost at the world origin).</summary>
		private static bool TrailPoint(Player p, int k)
		{
			Vector2 s = p.shadowPos[k];
			return s != Vector2.Zero && Vector2.DistanceSquared(s, p.position) < 600f * 600f;
		}
	}
}
