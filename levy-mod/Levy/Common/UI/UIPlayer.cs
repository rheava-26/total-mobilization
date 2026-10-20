using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Targeting;
using Levy.Common.Util;

namespace Levy.Common.UI
{
	/// <summary>Commander hotkeys. Runs only for the local player on a client / in single player.</summary>
	public class UIPlayer : ModPlayer
	{
		/// <summary>Per-character UI state: the barracks panel size the player dragged it to (0 = never resized, use the default). Saved with the character.</summary>
		public int BarracksW, BarracksH;

		public override void SaveData(TagCompound tag)
		{
			if (BarracksW > 0 && BarracksH > 0) { tag["barracksW"] = BarracksW; tag["barracksH"] = BarracksH; }
		}

		public override void LoadData(TagCompound tag)
		{
			BarracksW = tag.GetInt("barracksW");
			BarracksH = tag.GetInt("barracksH");
		}

		/// <summary>
		/// Focus-attack designate mode (local player only). While active the mouse is claimed so the held item does not fire. The next left-click
		/// on a hostile NPC locks it as the squad's focus target; a click on a critter or bare ground sets the focus-attack position there.
		/// Escape or right-click cancels.
		/// </summary>
		public override void PreUpdate()
		{
			if (!UISystem.FocusDesignating) return;
			if (Main.dedServ || Main.gameMenu || Player.whoAmI != Main.myPlayer || Player.dead) { EndDesignate(); return; }

			bool otherUiHovered = Player.mouseInterface; // set earlier this frame by any panel under the cursor
			Player.mouseInterface = true; // blocks item use / mining while designating
			if (Main.GameUpdateCount - UISystem.FocusDesignateStartTick <= LevyConstants.FocusDesignateGraceTicks) return;

			bool escape = Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape);
			if (escape || (Main.mouseRight && Main.mouseRightRelease)) { CancelDesignate(); return; }
			if (otherUiHovered || !PlayerInput.Triggers.JustPressed.MouseLeft) return;

			int hit = HostileUnderCursor();
			UISystem.Instance.EndFocusDesignate();
			if (hit >= 0) UIActions.FocusTarget(hit); // a hostile: lock it
			else UIActions.FocusAttack(true, Main.MouseWorld); // a critter or bare ground: attack that spot
		}

		private static void EndDesignate() => UISystem.Instance.EndFocusDesignate();

		private static void CancelDesignate()
		{
			EndDesignate();
			Main.NewText(LevyUtil.T("UI2.FocusCancelled"), 200, 160, 120);
		}

		/// <summary>The legal hostile NPC whose (padded) hitbox contains the mouse, nearest to its centre; -1 if none.</summary>
		private static int HostileUnderCursor()
		{
			Vector2 mouse = Main.MouseWorld;
			int best = -1; float bestSq = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (n == null || !n.active || !LevyUtil.IsHostileTarget(n) || SoldierTargeting.IsNeverTarget(n)) continue;
				var box = n.Hitbox;
				box.Inflate(LevyConstants.FocusClickPadPx, LevyConstants.FocusClickPadPx);
				if (!box.Contains(mouse.ToPoint())) continue;
				float d = Vector2.DistanceSquared(mouse, n.Center);
				if (d < bestSq) { bestSq = d; best = i; }
			}
			return best;
		}

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (Main.dedServ || Main.gameMenu || Player.whoAmI != Main.myPlayer) return;

			if (UISystem.FollowKey?.JustPressed == true) UIActions.Order(SoldierOrder.Follow, false);
			if (UISystem.HoldKey?.JustPressed == true) UIActions.Order(SoldierOrder.Hold, false);
			if (UISystem.ReturnKey?.JustPressed == true) UIActions.Order(SoldierOrder.ReturnToBarracks, false);
			if (UISystem.HoldFireKey?.JustPressed == true) UIActions.HoldFire(false);
			if (UISystem.RallyKey?.JustPressed == true) UIActions.Rally();
			if (UISystem.ArmyStripKey?.JustPressed == true) UISystem.Instance.ToggleArmyStrip();
			if (UISystem.MobilizeKey?.JustPressed == true)
			{
				var te = UIActions.NearestBarracks(Player.Center, LevyConstants.UI.MobilizeKeyRadiusTiles);
				if (te == null) Main.NewText(LevyUtil.T("UI2.NoBarracksNearby"), 200, 160, 120);
				else UIActions.ToggleMobilize(te);
			}
		}
	}
}
