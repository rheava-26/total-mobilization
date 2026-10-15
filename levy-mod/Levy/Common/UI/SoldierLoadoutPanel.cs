using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Common.UI
{
	/// <summary>
	/// Per-soldier loadout: the soldier's weapon, sidearm, armor, accessories (as many as his grade allows) and ammo pouch, drawn as
	/// vanilla item slots. Click a slot with an item on the mouse to hand-assign it (the slot is then locked against Armory auto-issue);
	/// click it with an empty mouse to take the item out (the lock clears). Everything goes through <see cref="UIActions.SetGearSlot"/>.
	/// The panel is bound by soldier id and re-resolves the record every tick, because on a multiplayer client every roster snapshot
	/// replaces the record objects.
	/// </summary>
	public class SoldierLoadoutPanel : UIState
	{
		public int SoldierId { get; private set; } = -1;

		private DraggablePanel _panel;
		private Label _title, _sub, _status, _hint;
		private LevyButton _close;
		private readonly Item[] _view = new Item[SoldierGear.SlotCount];
		private readonly ItemSlotElement[] _slots = new ItemSlotElement[SoldierGear.SlotCount];
		private readonly LockFrame[] _frames = new LockFrame[SoldierGear.SlotCount];
		private readonly Label[] _captions = new Label[SoldierGear.SlotCount];
		private int _refreshTimer, _statusTimer;

		private const int W = 372, H = 266;
		private const float SlotScale = 0.85f;
		private const int Pitch = 50, SlotSize = 44;
		private static readonly Color Gold = new(255, 215, 80);

		public SoldierRecord Rec
		{
			get
			{
				if (SoldierId < 0) return null;
				var rec = RosterSystem.Instance?.FindById(SoldierId);
				return rec != null && rec.Kind == SoldierKind.Regular && rec.CommanderName == Main.LocalPlayer.name ? rec : null;
			}
		}

		public override void OnInitialize()
		{
			_panel = new DraggablePanel();
			_panel.SetPadding(0);
			_panel.Left.Set(LevyConstants.UI.PanelDefaultLeft + LevyConstants.UI.BarracksPanelWidth + 12, 0f); _panel.Top.Set(LevyConstants.UI.PanelDefaultTop, 0f);
			_panel.Width.Set(W, 0f); _panel.Height.Set(H, 0f);
			_panel.BackgroundColor = new Color(33, 43, 79) * 0.9f;

			_title = Put(new Label("", 1f), 12, 8);
			_close = Put(new LevyButton("X", LevyConstants.UI.ButtonTextScale, 26, 26, () => UISystem.Instance.CloseSoldierLoadout(), LevyUtil.T("UI2.Close")), W - 36, 6);
			_sub = Put(new Label("", 0.7f), 12, 34);

			Put(new Label(LevyUtil.T("Loadout.Arms"), 0.75f), 12, 56);
			Put(new Label(LevyUtil.T("Loadout.Armor"), 0.75f), 210, 56);
			Put(new Label(LevyUtil.T("Loadout.Accessories"), 0.75f), 12, 142);

			AddSlot(SoldierGear.SlotWeapon, "Loadout.Weapon", 12, 74);
			AddSlot(SoldierGear.SlotSidearm, "Loadout.Sidearm", 62, 74);
			AddSlot(SoldierGear.SlotAmmo, "Loadout.Ammo", 112, 74);
			AddSlot(SoldierGear.SlotHead, "Loadout.Head", 210, 74);
			AddSlot(SoldierGear.SlotBody, "Loadout.Body", 260, 74);
			AddSlot(SoldierGear.SlotLegs, "Loadout.Legs", 310, 74);
			for (int i = 0; i < SoldierGear.AccessorySlots; i++) AddSlot(SoldierGear.SlotAcc0 + i, "Loadout.Accessory", 12 + i * Pitch, 160, remove: true);

			_status = Put(new Label("", 0.75f), 12, 214);
			_hint = Put(new Label(LevyUtil.T("Loadout.Hint"), 0.65f), 12, 238);
			_hint.TextColor = new Color(200, 205, 225);

			Append(_panel);
		}

		private T Put<T>(T el, float left, float top) where T : UIElement
		{
			el.Left.Set(left, 0f); el.Top.Set(top, 0f);
			_panel.Append(el);
			return el;
		}

		private void AddSlot(int slot, string nameKey, float left, float top, bool remove = false)
		{
			string name = LevyUtil.T(nameKey);
			int s = slot;
			_view[s] = new Item();
			var el = new ItemSlotElement(_view, s, SlotScale)
			{
				EmptyHint = LevyUtil.T("Loadout.EmptyHint", name),
				Accepts = it => Rec != null && Rec.Gear.CanHold(s, it),
				OnRejected = _ => SetStatus(LevyUtil.T("Loadout.Rejected", name), new Color(255, 150, 120)),
				OnChanged = (_, i) => OnSlotChanged(i, name),
			};
			el.Left.Set(left, 0f); el.Top.Set(top, 0f);
			_slots[s] = el;
			var frame = new LockFrame(SlotSize) { IsLocked = () => Rec != null && Rec.Gear.IsLocked(s) };
			frame.Left.Set(left, 0f); frame.Top.Set(top, 0f);
			_frames[s] = frame;
			var cap = new Label(name, 0.6f);
			cap.Left.Set(left, 0f); cap.Top.Set(top + SlotSize + 1, 0f);
			_captions[s] = cap;
			if (!remove) AppendSlot(s);
		}

		private void AppendSlot(int s)
		{
			if (_slots[s].Parent != null) return;
			_panel.Append(_slots[s]); _panel.Append(_frames[s]); _panel.Append(_captions[s]);
		}

		private void RemoveSlot(int s)
		{
			if (_slots[s].Parent == null) return;
			_slots[s].Remove(); _frames[s].Remove(); _captions[s].Remove();
		}

		/// <summary>Point the panel at a soldier.</summary>
		public void Bind(int soldierId)
		{
			SoldierId = soldierId;
			if (_panel == null) return;
			float sw = Main.screenWidth / Main.UIScale, sh = Main.screenHeight / Main.UIScale;
			float left = LevyConstants.UI.PanelDefaultLeft + LevyConstants.UI.BarracksPanelWidth + 12;
			_panel.Left.Set(Math.Max(0f, Math.Min(left, sw - W - 10f)), 0f);
			_panel.Top.Set(Math.Max(0f, Math.Min(LevyConstants.UI.PanelDefaultTop, sh - H - 10f)), 0f);
			_refreshTimer = 0; _statusTimer = 0; _status.SetText("");
			Refresh();
			Recalculate();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			var rec = Rec;
			if (rec == null) { UISystem.Instance.CloseSoldierLoadout(); return; }
			// The slots draw straight from the record's items, so a roster snapshot or an auto-issue shows up at once.
			for (int s = 0; s < SoldierGear.SlotCount; s++) _view[s] = rec.Gear.Get(s) ?? new Item();
			if (_statusTimer > 0 && --_statusTimer == 0) _status.SetText("");
			if (--_refreshTimer <= 0) { _refreshTimer = 15; Refresh(); }
		}

		private void Refresh()
		{
			var rec = Rec; if (rec == null) return;
			_title.SetText(LevyUtil.T("Loadout.Title", rec.Name));
			_sub.SetText(UIActions.Chevrons(rec.Grade) + " " + UIActions.GradeName(rec.Grade) + "   " + LevyUtil.T("Loadout.AccessorySlots", rec.AccessorySlots));
			int allowed = Math.Clamp(rec.AccessorySlots, 0, SoldierGear.AccessorySlots);
			for (int i = 0; i < SoldierGear.AccessorySlots; i++)
			{
				int s = SoldierGear.SlotAcc0 + i;
				if (i < allowed) AppendSlot(s); else RemoveSlot(s);
			}
			for (int s = 0; s < SoldierGear.SlotCount; s++) if (_captions[s] != null) _captions[s].TextColor = rec.Gear.IsLocked(s) ? Gold : Color.White;
		}

		private void SetStatus(string text, Color color)
		{
			_status.SetText(text); _status.TextColor = color; _statusTimer = 150;
		}

		private void OnSlotChanged(int slot, string name)
		{
			var rec = Rec; if (rec == null) return;
			if (!UIActions.SetGearSlot(rec, slot, _view[slot])) { SetStatus(LevyUtil.T("Loadout.Rejected", name), new Color(255, 150, 120)); return; }
			bool locked = rec.Gear.IsLocked(slot);
			SetStatus(LevyUtil.T(locked ? "Loadout.Assigned" : "Loadout.Released", name), locked ? Gold : new Color(190, 200, 230));
			_refreshTimer = 0;
		}

		/// <summary>Gold outline drawn over a slot while it is hand-assigned.</summary>
		private class LockFrame : UIElement
		{
			public Func<bool> IsLocked;
			private readonly int _size;

			public LockFrame(int size) { _size = size; Width.Set(size, 0f); Height.Set(size, 0f); IgnoresMouseInteraction = true; }

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				if (IsLocked == null || !IsLocked()) return;
				var pos = GetDimensions().Position();
				var px = TextureAssets.MagicPixel.Value;
				int x = (int)pos.X, y = (int)pos.Y, s = _size;
				var c = Gold * 0.85f;
				spriteBatch.Draw(px, new Rectangle(x, y, s, 2), c);
				spriteBatch.Draw(px, new Rectangle(x, y + s - 2, s, 2), c);
				spriteBatch.Draw(px, new Rectangle(x, y, 2, s), c);
				spriteBatch.Draw(px, new Rectangle(x + s - 2, y, 2, s), c);
			}
		}
	}
}
