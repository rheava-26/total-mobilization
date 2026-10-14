using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.UI;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// Armory: 12x10 storage grid plus the issue rows (6 weapons with a sidearm slot, 3 armor sets, 4 ammo, 2 potions),
	/// "Deposit junk" and "Loot all". Single player edits the live tile-entity arrays; a multiplayer client sends
	/// ArmorySlotSet for each changed slot.
	/// </summary>
	public class ArmoryPanel : UIState
	{
		public ArmoryTE Te { get; private set; }

		private DraggablePanel _panel;
		private UIElement _grid, _issue;
		private LevyButton _depositJunk, _lootAll, _close;
		private Label _title;

		private const int GridLeft = 10, GridTop = 40, IssueLeft = 432, IssueWidth = 220;
		private static int Pitch => LevyConstants.UI.SlotPitch;
		private static float Scale => LevyConstants.UI.SlotScale;

		public override void OnInitialize()
		{
			_panel = new DraggablePanel();
			_panel.SetPadding(0);
			_panel.Left.Set(LevyConstants.UI.PanelDefaultLeft, 0f); _panel.Top.Set(LevyConstants.UI.PanelDefaultTop, 0f);
			_panel.Width.Set(LevyConstants.UI.ArmoryPanelWidth, 0f); _panel.Height.Set(LevyConstants.UI.ArmoryPanelHeight, 0f);
			_panel.BackgroundColor = new Color(33, 43, 79) * 0.9f;

			_title = new Label(LevyUtil.T("UI2.ArmoryTitle"), 1f);
			_title.Left.Set(12f, 0f); _title.Top.Set(10f, 0f);
			_panel.Append(_title);

			int w = LevyConstants.UI.ArmoryPanelWidth;
			_close = new LevyButton("X", LevyConstants.UI.ButtonTextScale, 26, 26, () => UISystem.Instance.CloseArmory(), LevyUtil.T("UI2.Close"));
			_close.Left.Set(w - 36, 0f); _close.Top.Set(6f, 0f);
			_panel.Append(_close);
			_lootAll = new LevyButton(LevyUtil.T("UI.LootAll"), LevyConstants.UI.ButtonTextScale, 104, 26, LootAll);
			_lootAll.Left.Set(w - 36 - 112, 0f); _lootAll.Top.Set(6f, 0f);
			_panel.Append(_lootAll);
			_depositJunk = new LevyButton(LevyUtil.T("UI.DepositJunk"), LevyConstants.UI.ButtonTextScale, 150, 26, DepositJunk);
			_depositJunk.Left.Set(w - 36 - 112 - 158, 0f); _depositJunk.Top.Set(6f, 0f);
			_panel.Append(_depositJunk);

			_grid = new UIElement();
			_grid.Left.Set(GridLeft, 0f); _grid.Top.Set(GridTop, 0f);
			_grid.Width.Set(LevyConstants.UI.ArmoryColumns * Pitch, 0f); _grid.Height.Set(LevyConstants.UI.ArmoryRows * Pitch, 0f);
			_panel.Append(_grid);

			_issue = new UIElement();
			_issue.Left.Set(IssueLeft, 0f); _issue.Top.Set(GridTop, 0f);
			_issue.Width.Set(IssueWidth, 0f); _issue.Height.Set(LevyConstants.UI.ArmoryRows * Pitch, 0f);
			_panel.Append(_issue);

			Append(_panel);
		}

		/// <summary>Point the panel at a tile entity and rebuild every slot element against its arrays.</summary>
		public void Bind(ArmoryTE te)
		{
			Te = te;
			if (_grid == null) return;
			_grid.RemoveAllChildren();
			_issue.RemoveAllChildren();
			if (te == null) return;

			for (int i = 0; i < te.Storage.Length && i < LevyConstants.UI.ArmoryColumns * LevyConstants.UI.ArmoryRows; i++)
			{
				var slot = MakeSlot(te.Storage, i, 0, null, null);
				slot.Left.Set((i % LevyConstants.UI.ArmoryColumns) * Pitch, 0f);
				slot.Top.Set((i / LevyConstants.UI.ArmoryColumns) * Pitch, 0f);
				_grid.Append(slot);
			}

			float y = 0f;
			AddLabel(LevyUtil.T("UI.IssueWeapons"), y); y += 18f;
			for (int i = 0; i < te.IssueWeapons.Length; i++)
			{
				bool sidearm = i == ArmoryTE.SidearmSlot;
				var slot = MakeSlot(te.IssueWeapons, i, 1, IsWeaponShaped, sidearm ? LevyUtil.T("UI2.Sidearm") : null);
				slot.Left.Set(i * Pitch, 0f); slot.Top.Set(y, 0f);
				_issue.Append(slot);
			}
			var side = new Label(LevyUtil.T("UI2.Sidearm"), 0.6f);
			side.Left.Set(ArmoryTE.SidearmSlot * Pitch, 0f); side.Top.Set(y + Pitch - 2f, 0f);
			_issue.Append(side);
			y += Pitch + 16f;

			AddLabel(LevyUtil.T("UI.IssueArmor"), y); y += 18f;
			for (int set = 0; set < 3; set++)
			{
				for (int part = 0; part < 3; part++)
				{
					int idx = set * 3 + part, p = part;
					if (idx >= te.IssueArmor.Length) break;
					var slot = MakeSlot(te.IssueArmor, idx, 2, it => IsArmorPart(it, p), null);
					slot.Left.Set(part * Pitch, 0f); slot.Top.Set(y + set * Pitch, 0f);
					_issue.Append(slot);
				}
			}
			y += 3 * Pitch + 6f;

			AddLabel(LevyUtil.T("UI.IssueAmmo"), y); y += 18f;
			for (int i = 0; i < te.IssueAmmo.Length; i++)
			{
				var slot = MakeSlot(te.IssueAmmo, i, 3, it => it.ammo > 0 && !it.IsACoin, null);
				slot.Left.Set(i * Pitch, 0f); slot.Top.Set(y, 0f);
				_issue.Append(slot);
			}
			y += Pitch + 6f;

			AddLabel(LevyUtil.T("UI.IssuePotions"), y); y += 18f;
			for (int i = 0; i < te.IssuePotions.Length; i++)
			{
				bool healing = i == 0;
				var slot = MakeSlot(te.IssuePotions, i, 4, healing ? (Func<Item, bool>)(it => it.healLife > 0) : (it => it.buffType > 0 && it.consumable), LevyUtil.T(healing ? "UI2.PotionHealing" : "UI2.PotionBuff"));
				slot.Left.Set(i * Pitch, 0f); slot.Top.Set(y, 0f);
				_issue.Append(slot);
				var tag = new Label(LevyUtil.T(healing ? "UI2.PotionHealing" : "UI2.PotionBuff"), 0.6f);
				tag.Left.Set(i * Pitch, 0f); tag.Top.Set(y + Pitch - 2f, 0f);
				_issue.Append(tag);
			}
			Recalculate();
		}

		private void AddLabel(string text, float y)
		{
			var l = new Label(text, 0.8f);
			l.Left.Set(0f, 0f); l.Top.Set(y, 0f);
			_issue.Append(l);
		}

		private ItemSlotElement MakeSlot(Item[] arr, int index, byte region, Func<Item, bool> accepts, string emptyHint)
		{
			var slot = new ItemSlotElement(arr, index, Scale) { Accepts = accepts, EmptyHint = emptyHint };
			slot.OnChanged = (a, i) => { if (Te != null) UIActions.SendArmorySlot(Te, region, i); };
			return slot;
		}

		// ------------------------------------------------------------------ classification
		/// <summary>Weapon-shaped: deals damage, is not a tool, armor, accessory, consumable or coin. Soldiers decide what they can use.</summary>
		public static bool IsWeaponShaped(Item it) => it != null && !it.IsAir && it.damage > 0 && !it.accessory && !it.consumable && it.headSlot < 0 && it.bodySlot < 0 && it.legSlot < 0
			&& it.pick == 0 && it.axe == 0 && it.hammer == 0 && !it.IsACoin;
		public static bool IsArmorPart(Item it, int part) => it != null && !it.IsAir && !it.vanity && (part == 0 ? it.headSlot >= 0 : part == 1 ? it.bodySlot >= 0 : it.legSlot >= 0);
		public static bool IsArmor(Item it) => it != null && !it.IsAir && !it.vanity && (it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0);
		public static bool IsJunk(Item it)
		{
			if (it == null || it.IsAir || it.stack <= 0 || it.favorited) return false;
			if (IsWeaponShaped(it)) return true;
			if (IsArmor(it)) return true;
			if (it.accessory && !it.vanity) return true;
			if (it.ammo > 0 && !it.IsACoin) return true;
			return false;
		}

		// ------------------------------------------------------------------ bulk buttons
		/// <summary>Moves every non-favourited weapon, armor piece, accessory and ammo stack from the main inventory (not the hotbar) into storage.</summary>
		private void DepositJunk()
		{
			if (Te == null || !UIActions.IsLive(Te)) return;
			var inv = Main.LocalPlayer.inventory;
			var before = Snapshot(Te.Storage);
			bool any = false;
			for (int i = LevyConstants.UI.InventoryMainStart; i < LevyConstants.UI.InventoryMainEnd && i < inv.Length; i++)
			{
				var it = inv[i];
				if (!IsJunk(it)) continue;
				var left = Te.Deposit(it);
				if (left == null || left.IsAir || left.stack <= 0) { inv[i] = new Item(); any = true; }
				else { if (!ReferenceEquals(left, it) || left.stack != it.stack) any = true; inv[i] = left; }
			}
			SendChanged(before, Te.Storage);
			if (any) SoundEngine.PlaySound(SoundID.Grab);
		}

		/// <summary>Takes everything in storage into the inventory (issue rows stay).</summary>
		private void LootAll()
		{
			if (Te == null || !UIActions.IsLive(Te)) return;
			var player = Main.LocalPlayer;
			var before = Snapshot(Te.Storage);
			bool any = false;
			for (int i = 0; i < Te.Storage.Length; i++)
			{
				var it = Te.Storage[i];
				if (it == null || it.IsAir) continue;
				var rest = player.GetItem(Main.myPlayer, it, GetItemSettings.LootAllSettings);
				Te.Storage[i] = rest ?? new Item();
				if (SlotSnap.Differs(before, Te.Storage, i)) any = true;
			}
			SendChanged(before, Te.Storage);
			if (any) SoundEngine.PlaySound(SoundID.Grab);
		}

		private static SlotSnap[] Snapshot(Item[] arr) { var s = new SlotSnap[arr.Length]; for (int i = 0; i < arr.Length; i++) s[i] = new SlotSnap(arr[i]); return s; }

		private void SendChanged(SlotSnap[] before, Item[] arr)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || Te == null) return;
			for (int i = 0; i < arr.Length; i++) if (SlotSnap.Differs(before, arr, i)) UIActions.SendArmorySlot(Te, 0, i);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (Te != null && !UIActions.IsLive(Te)) { UISystem.Instance.CloseArmory(); return; }
			bool anyStorage = false;
			if (Te != null) foreach (var it in Te.Storage) if (it != null && !it.IsAir) { anyStorage = true; break; }
			_lootAll.Enabled = anyStorage;
		}
	}
}
