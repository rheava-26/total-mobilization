using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Common.UI
{
	/// <summary>
	/// The levy inventory window, opened from the talk window's Gear button. It shows everything a levy carries:
	/// weapon, sidearm, ammo pouch, grappling hook, head / body / legs, accessories (as many as his grade opens) and the vanity dyes.
	/// Click a slot holding an item from your pack to swap it in (the slot locks so the Armory leaves it alone); click a filled slot with an empty
	/// cursor to take the item. "Take all" empties the gear slots into your pack. The window walks the levy up to you (TalkCall) and closes when you
	/// move away or close your pack. All changes go through <see cref="UIActions"/>: single player edits the record, a multiplayer client edits its display copy and sends
	/// SoldierGearSet / SoldierDyeSet for the server to validate.
	/// </summary>
	public class LevyInventorySystem : ModSystem
	{
		public static LevyInventorySystem Instance => ModContent.GetInstance<LevyInventorySystem>();
		/// <summary>Id of the soldier whose window is open, or -1.</summary>
		public static int OpenId { get; private set; } = -1;

		private UserInterface _ui;
		private LevyInventoryPanel _panel;
		private GameTime _lastTime;

		public override void PostSetupContent()
		{
			if (Main.dedServ) return;
			_ui = new UserInterface();
			_panel = new LevyInventoryPanel();
			_panel.Activate();
		}

		public override void Unload() { _ui = null; _panel = null; OpenId = -1; }
		public override void OnWorldUnload() { if (!Main.dedServ) Close(); }

		/// <summary>Open the window for a soldier. Ignores a record the local player does not command.</summary>
		public static void Open(SoldierRecord rec)
		{
			var s = Instance;
			if (Main.dedServ || s?._ui == null || rec == null || rec.Kind != SoldierKind.Regular || rec.CommanderName != Main.LocalPlayer.name) return;
			// the chat window is done: close it (the soldier stays beside us through the call-over) and show the pack next to the window
			if (Main.LocalPlayer.talkNPC >= 0) Main.LocalPlayer.SetTalkNPC(-1);
			Main.npcChatText = "";
			Main.playerInventory = true; Main.recBigList = false;
			UISystem.Instance.CloseSoldierLoadout();
			OpenId = rec.Id;
			s._panel.Bind(rec.Id);
			s._ui.SetState(s._panel);
			UIActions.CallOver(rec);
			s._panel.CallTimer = 0;
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public static void Close()
		{
			var s = Instance;
			if (s?._ui?.CurrentState == null) { OpenId = -1; return; }
			s._ui.SetState(null); OpenId = -1;
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ || _ui?.CurrentState == null) return;
			_lastTime = gameTime;
			var p = Main.LocalPlayer;
			var rec = _panel.Rec;
			if (rec == null || !Main.playerInventory || Main.gameMenu || p.dead) { Close(); return; }
			// the levy walks up to us; if we leave it behind (or it dies) the window goes
			var body = UIActions.BodyOf(rec);
			if (body == null || !body.NPC.active || Vector2.Distance(body.NPC.Center, p.Center) > LevyConstants.InventoryCloseTiles * LevyConstants.TilePx) { Close(); return; }
			_ui.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			if (_ui == null) return;
			int i = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
			if (i < 0) i = layers.Count;
			layers.Insert(i, new LegacyGameInterfaceLayer("Levy: Levy Inventory", () =>
			{
				if (_ui?.CurrentState != null) _ui.Draw(Main.spriteBatch, _lastTime ?? new GameTime());
				return true;
			}, InterfaceScaleType.UI));
		}
	}

	public class LevyInventoryPanel : UIState
	{
		public int SoldierId { get; private set; } = -1;
		/// <summary>Ticks since the last call-over; the panel re-sends the call while open so the levy stays beside the player.</summary>
		public int CallTimer;

		private DraggablePanel _panel;
		private Label _title, _sub, _status;
		private LevyButton _close, _takeAll;
		private readonly Item[] _view = new Item[SoldierGear.SlotCount];
		private readonly Item[] _dyeView = new Item[SoldierGear.DyeSlots];
		private readonly ItemSlotElement[] _slots = new ItemSlotElement[SoldierGear.SlotCount];
		private readonly ItemSlotElement[] _dyeSlots = new ItemSlotElement[SoldierGear.DyeSlots];
		private readonly Label[] _captions = new Label[SoldierGear.SlotCount];
		private readonly FrameBox[] _frames = new FrameBox[SoldierGear.SlotCount];
		private int _statusTimer, _refreshTimer;

		private const int W = 372, H = 418;
		private const float SlotScale = 0.85f, DyeScale = 0.7f;
		private const int Pitch = 50, DyePitch = 40, SlotSize = 44, DySize = 36;
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
			_panel.Width.Set(W, 0f); _panel.Height.Set(H, 0f);
			_panel.BackgroundColor = new Color(33, 43, 79) * 0.92f;

			_title = Put(new Label("", 1f), 12, 8);
			_close = Put(new LevyButton("X", LevyConstants.UI.ButtonTextScale, 26, 26, () => LevyInventorySystem.Close(), LevyUtil.T("UI2.Close")), W - 36, 6);
			_sub = Put(new Label("", 0.7f), 12, 34);

			Put(new Label(LevyUtil.T("Loadout.Arms"), 0.75f), 12, 56);
			Put(new Label(LevyUtil.T("Loadout.Armor"), 0.75f), 210, 56);
			Put(new Label(LevyUtil.T("Loadout.Accessories"), 0.75f), 12, 142);
			Put(new Label(LevyUtil.T("Loadout.ExtraSidearms"), 0.75f), 12, 214);
			Put(new Label(LevyUtil.T("Talk.Vanity"), 0.75f), 12, 280);

			AddSlot(SoldierGear.SlotWeapon, "Loadout.Weapon", 12, 74);
			AddSlot(SoldierGear.SlotSidearm, "Loadout.Sidearm", 62, 74);
			AddSlot(SoldierGear.SlotAmmo, "Loadout.Ammo", 112, 74);
			AddSlot(SoldierGear.SlotHook, "Talk.Hook", 162, 74);
			AddSlot(SoldierGear.SlotHead, "Loadout.Head", 210, 74);
			AddSlot(SoldierGear.SlotBody, "Loadout.Body", 260, 74);
			AddSlot(SoldierGear.SlotLegs, "Loadout.Legs", 310, 74);
			for (int i = 0; i < SoldierGear.AccessorySlots; i++) AddSlot(SoldierGear.SlotAcc0 + i, "Loadout.Accessory", 12 + i * Pitch, 160, remove: true);
			for (int x = 0; x < LevyConstants.ExtraSidearms; x++) AddSlot(SoldierGear.SlotSide2 + x, "Loadout.ExtraSidearm", 12 + x * Pitch, 232);
			for (int d = 0; d < SoldierGear.DyeSlots; d++) AddDye(d, 12 + d * DyePitch, 298);

			_status = Put(new Label("", 0.75f), 12, 352);
			_takeAll = Put(new LevyButton(LevyUtil.T("Talk.TakeAll"), 0.8f, 110, 26, TakeAll, LevyUtil.T("Talk.TakeAllHint")), 12, 378);
			var hint = Put(new Label(LevyUtil.T("Talk.LockHint"), 0.62f), 130, 378);
			hint.TextColor = new Color(200, 205, 225);
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
			var frame = new FrameBox(SlotSize) { IsLocked = () => Rec != null && Rec.Gear.IsLocked(s) };
			frame.Left.Set(left, 0f); frame.Top.Set(top, 0f);
			_frames[s] = frame;
			var cap = new Label(name, 0.6f);
			cap.Left.Set(left, 0f); cap.Top.Set(top + SlotSize + 1, 0f);
			_captions[s] = cap;
			if (!remove) AppendSlot(s);
		}

		private void AddDye(int d, float left, float top)
		{
			int idx = d;
			_dyeView[d] = new Item();
			string name = LevyUtil.T("Talk.Dye");
			var el = new ItemSlotElement(_dyeView, d, DyeScale)
			{
				EmptyHint = LevyUtil.T("Talk.DyeHint"),
				Accepts = it => SoldierGear.CanHoldDye(idx, it),
				OnRejected = _ => SetStatus(LevyUtil.T("Talk.NotADye"), new Color(255, 150, 120)),
				OnChanged = (_, i) => OnDyeChanged(i),
			};
			el.Left.Set(left, 0f); el.Top.Set(top, 0f);
			_dyeSlots[d] = el;
			if (d < 3) _panel.Append(el); // the three armor dyes always; accessory dyes follow the open accessory slots
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

		/// <summary>Point the window at a soldier and place it beside the pack.</summary>
		public void Bind(int soldierId)
		{
			SoldierId = soldierId;
			if (_panel == null) return;
			float sw = Main.screenWidth / Main.UIScale, sh = Main.screenHeight / Main.UIScale;
			_panel.Left.Set(Math.Max(0f, Math.Min(Math.Max(sw * 0.5f - W * 0.25f, 600f), sw - W - 10f)), 0f);
			_panel.Top.Set(Math.Max(0f, Math.Min(90f, sh - H - 10f)), 0f);
			_statusTimer = 0; _refreshTimer = 0; _status.SetText("");
			Refresh();
			Recalculate();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			var rec = Rec;
			if (rec == null) { LevyInventorySystem.Close(); return; }
			for (int s = 0; s < SoldierGear.SlotCount; s++) _view[s] = rec.Gear.Get(s) ?? new Item();
			for (int d = 0; d < SoldierGear.DyeSlots; d++) _dyeView[d] = rec.Gear.Dyes[d] ?? new Item();
			if (_statusTimer > 0 && --_statusTimer == 0) _status.SetText("");
			if (--_refreshTimer <= 0) { _refreshTimer = 15; Refresh(); }
			if (++CallTimer >= LevyConstants.TalkCallTicks / 2) { CallTimer = 0; UIActions.CallOver(rec); }
		}

		private void Refresh()
		{
			var rec = Rec; if (rec == null) return;
			_title.SetText(LevyUtil.T("Talk.Title", rec.Name));
			_sub.SetText(UIActions.Chevrons(rec.Grade) + " " + UIActions.GradeName(rec.Grade) + "   " + LevyUtil.T("Loadout.AccessorySlots", rec.AccessorySlots));
			int allowed = Math.Clamp(rec.AccessorySlots, 0, SoldierGear.AccessorySlots);
			for (int i = 0; i < SoldierGear.AccessorySlots; i++)
			{
				int s = SoldierGear.SlotAcc0 + i;
				if (i < allowed) AppendSlot(s); else RemoveSlot(s);
				var dye = _dyeSlots[3 + i];
				if (i < allowed) { if (dye.Parent == null) _panel.Append(dye); } else if (dye.Parent != null) dye.Remove();
			}
			for (int s = 0; s < SoldierGear.SlotCount; s++) if (_captions[s] != null) _captions[s].TextColor = rec.Gear.IsLocked(s) ? Gold : Color.White;
		}

		private void SetStatus(string text, Color color) { _status.SetText(text); _status.TextColor = color; _statusTimer = 150; }

		private void OnSlotChanged(int slot, string name)
		{
			var rec = Rec; if (rec == null) return;
			if (!UIActions.SetGearSlot(rec, slot, _view[slot])) { SetStatus(LevyUtil.T("Loadout.Rejected", name), new Color(255, 150, 120)); return; }
			bool locked = rec.Gear.IsLocked(slot);
			SetStatus(LevyUtil.T(locked ? "Loadout.Assigned" : "Loadout.Released", name), locked ? Gold : new Color(190, 200, 230));
			_refreshTimer = 0;
		}

		private void OnDyeChanged(int d)
		{
			var rec = Rec; if (rec == null) return;
			if (!UIActions.SetDyeSlot(rec, d, _dyeView[d])) { SetStatus(LevyUtil.T("Talk.NotADye"), new Color(255, 150, 120)); return; }
			SetStatus(LevyUtil.T(rec.Gear.Dyes[d].IsAir ? "Talk.DyeTaken" : "Talk.DyeSet"), new Color(190, 200, 230));
		}

		private void TakeAll()
		{
			var rec = Rec; if (rec == null) return;
			int n = 0;
			for (int s = 0; s < SoldierGear.SlotCount; s++) if (UIActions.TakeGearSlot(rec, s)) n++;
			SetStatus(n > 0 ? LevyUtil.T("Talk.TookAll", n) : LevyUtil.T("Talk.NothingToTake"), n > 0 ? Gold : new Color(190, 200, 230));
			_refreshTimer = 0;
		}

		/// <summary>Gold outline drawn over a slot while it is hand-assigned.</summary>
		private class FrameBox : UIElement
		{
			public Func<bool> IsLocked;
			private readonly int _size;

			public FrameBox(int size) { _size = size; Width.Set(size, 0f); Height.Set(size, 0f); IgnoresMouseInteraction = true; }

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				if (IsLocked == null || !IsLocked()) return;
				var pos = GetDimensions().Position();
				var px = Terraria.GameContent.TextureAssets.MagicPixel.Value;
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
