using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// The reforge panel beside the armory: which weapons (levies' hands, recent deposits, hand-picked in the grid, a class or category), what to roll for (a row of the four floors, always in view, or one named
	/// prefix from its own list), a spending cap,
	/// and a live preview ("12 weapons, about 3 gold") above the Reforge button. It edits a <see cref="ReforgePlan"/> and hands it to <see cref="ArmoryEditNet.RunReforge"/>; nothing here rolls anything itself.
	/// </summary>
	public class ReforgePanel : UIPanel
	{
		public const int PanelWidth = 272, PanelHeight = 462;
		private const int Pad = 10;

		private readonly ArmoryPanel _owner;
		private readonly ChoicePopup _popup = new();
		private readonly LevyButton[] _modes = new LevyButton[4];
		private readonly LevyButton _pickToggle, _pickClear, _run, _close;
		private readonly LevyButton[] _floors = new LevyButton[LevyConstants.UI.ReforgeFloorNameKey.Length];
		private readonly DropButton _recent, _class, _cat, _modifier, _cap;
		private readonly Label _recentLabel, _classLabel, _catLabel, _goalLabel, _modifierLabel, _capLabel, _modeNote;
		private readonly Label _pv1, _pv2, _pv3, _pv4;

		/// <summary>The plan being edited; kept while the armory screen lives.</summary>
		public ReforgePlan Plan { get; private set; } = new ReforgePlan();
		/// <summary>True while a click on a weapon cell picks it instead of lifting it: the Picked mode is chosen and the pick toggle is on.</summary>
		public bool PickActive { get; private set; }

		private ReforgePreview _preview = new();
		private int _previewTick;

		private ArmoryTE Te => _owner.Te;

		public ReforgePanel(ArmoryPanel owner)
		{
			_owner = owner;
			SetPadding(0);
			Width.Set(PanelWidth, 0f); Height.Set(PanelHeight, 0f);
			BackgroundColor = new Color(33, 43, 79) * 0.94f;

			var title = new Label(LevyUtil.T("Reforge.Title"), 1f); title.Left.Set(Pad, 0f); title.Top.Set(8, 0f); Append(title);
			_close = new LevyButton("X", LevyConstants.UI.ButtonTextScale, 24, 22, () => owner.CloseReforge());
			_close.Left.Set(PanelWidth - 32, 0f); _close.Top.Set(6, 0f); Append(_close);

			float y = 36;
			Lbl("Reforge.Weapons", y); y += 20;
			ReforgeMode[] order = { ReforgeMode.Issued, ReforgeMode.Recent, ReforgeMode.Picked, ReforgeMode.Group };
			for (int i = 0; i < 4; i++)
			{
				var mode = order[i];
				var b = new LevyButton(LevyUtil.T("Reforge.Mode_" + mode), LevyConstants.UI.ButtonTextScale, 122, 24, () => SetMode(mode), LevyUtil.T("Reforge.ModeHint_" + mode));
				b.Left.Set(Pad + (i % 2) * 128, 0f); b.Top.Set(y + (i / 2) * 28, 0f); Append(b); _modes[i] = b;
			}
			y += 62;

			// the row under the mode buttons depends on the mode: recent window, or pick controls
			_recentLabel = Lbl("Reforge.RecentWithin", y + 4);
			_recent = new DropButton(120, () => RecentText(Plan.RecentDays), d => Choose(d, RecentChoices()), LevyUtil.T("Reforge.RecentHint"));
			_recent.Left.Set(PanelWidth - Pad - 120, 0f); _recent.Top.Set(y, 0f); Append(_recent);
			_pickToggle = new LevyButton("", LevyConstants.UI.ButtonTextScale, 140, 24, TogglePick, LevyUtil.T("Reforge.PickHint"));
			_pickToggle.Left.Set(Pad, 0f); _pickToggle.Top.Set(y, 0f); Append(_pickToggle);
			_pickClear = new LevyButton("", LevyConstants.UI.ButtonTextScale, 104, 24, ClearPicks, LevyUtil.T("Reforge.PickClearHint"));
			_pickClear.Left.Set(PanelWidth - Pad - 104, 0f); _pickClear.Top.Set(y, 0f); Append(_pickClear);
			_modeNote = new Label("", 0.76f); _modeNote.Left.Set(Pad, 0f); _modeNote.Top.Set(y + 4, 0f); Append(_modeNote);
			y += 34;

			_classLabel = Lbl("Reforge.Class", y + 4);
			_class = new DropButton(160, () => Plan.ClassKey == null ? LevyUtil.T("Reforge.Any") : WeaponTags.DisplayName(Plan.ClassKey), d => Choose(d, ClassChoices()), LevyUtil.T("Reforge.ClassHint"));
			_class.Left.Set(PanelWidth - Pad - 160, 0f); _class.Top.Set(y, 0f); Append(_class);
			y += 30;
			_catLabel = Lbl("Reforge.Category", y + 4);
			_cat = new DropButton(160, () => Plan.Category == null ? LevyUtil.T("Reforge.Any") : WeaponTags.Title(Plan.Category), d => Choose(d, CategoryChoices()), LevyUtil.T("Reforge.CategoryHint"));
			_cat.Left.Set(PanelWidth - Pad - 160, 0f); _cat.Top.Set(y, 0f); Append(_cat);
			y += 36;

			// what to roll for: the floors are a row of buttons that never leaves view; a named modifier is a separate list below it
			_goalLabel = Lbl("Reforge.Goal", y);
			y += 20;
			int nf = _floors.Length; float fw = (PanelWidth - 2 * Pad - (nf - 1) * 4) / (float)nf;
			for (int i = 0; i < nf; i++)
			{
				int idx = i;
				var b = new LevyButton(FloorName(i), LevyConstants.UI.ButtonTextScale, fw, 24, () => { Plan.SetFloor(idx); Refresh(true); }, LevyUtil.T("Reforge.FloorHint_" + i));
				b.Left.Set(Pad + i * (fw + 4), 0f); b.Top.Set(y, 0f); Append(b); _floors[i] = b;
			}
			y += 30;
			_modifierLabel = Lbl("Reforge.Modifier", y + 4);
			_modifier = new DropButton(160, ModifierText, d => Choose(d, ModifierChoices()), LevyUtil.T("Reforge.ModifierHint"));
			_modifier.Left.Set(PanelWidth - Pad - 160, 0f); _modifier.Top.Set(y, 0f); Append(_modifier);
			y += 30;
			_capLabel = Lbl("Reforge.Cap", y + 4);
			_cap = new DropButton(160, () => Plan.CapCopper <= 0 ? LevyUtil.T("Reforge.NoCap") : LevyUtil.Coins(Plan.CapCopper), d => Choose(d, CapChoices()), LevyUtil.T("Reforge.CapHint"));
			_cap.Left.Set(PanelWidth - Pad - 160, 0f); _cap.Top.Set(y, 0f); Append(_cap);
			y += 40;

			_pv1 = new Label("", 0.95f); _pv1.Left.Set(Pad, 0f); _pv1.Top.Set(y, 0f); Append(_pv1);
			_pv2 = new Label("", 0.76f); _pv2.Left.Set(Pad, 0f); _pv2.Top.Set(y + 24, 0f); Append(_pv2);
			_pv3 = new Label("", 0.76f); _pv3.Left.Set(Pad, 0f); _pv3.Top.Set(y + 42, 0f); Append(_pv3);
			_pv4 = new Label("", 0.76f); _pv4.Left.Set(Pad, 0f); _pv4.Top.Set(y + 60, 0f); Append(_pv4);

			_run = new LevyButton(LevyUtil.T("Reforge.Run"), LevyConstants.UI.ButtonTextScale, PanelWidth - 2 * Pad, 28, Run, LevyUtil.T("Reforge.RunHint"));
			_run.Left.Set(Pad, 0f); _run.Top.Set(PanelHeight - 38, 0f); Append(_run);
			Refresh(true);
		}

		private Label Lbl(string key, float y)
		{
			var l = new Label(LevyUtil.T(key), 0.8f);
			l.Left.Set(Pad, 0f); l.Top.Set(y, 0f); Append(l);
			return l;
		}

		/// <summary>The armory screen was pointed at another armory (or closed): picks refer to slots of the old one.</summary>
		public void Rebind()
		{
			Plan.Picks.Clear();
			PickActive = false;
			if (_popup.Open) _popup.Remove();
			Refresh(true);
		}

		// ------------------------------------------------------------------ edits

		private void SetMode(ReforgeMode mode)
		{
			Plan.Mode = mode;
			if (mode != ReforgeMode.Picked) PickActive = false;
			else PickActive = true;
			SoundEngine.PlaySound(SoundID.MenuTick);
			Refresh(true);
		}

		private void TogglePick() { PickActive = !PickActive; Refresh(true); }
		private void ClearPicks() { Plan.Picks.Clear(); Refresh(true); }

		/// <summary>A weapon cell was clicked while picking.</summary>
		public void TogglePicked(byte region, int slot)
		{
			Plan.TogglePick(region, slot);
			SoundEngine.PlaySound(SoundID.MenuTick);
			Refresh(true);
		}

		private void Run()
		{
			if (Te == null) return;
			ArmoryEditNet.RunReforge(Te, Plan.Clone());
			SoundEngine.PlaySound(SoundID.Item37);
			_previewTick = 0; // the numbers change as soon as the server answers
		}

		// ------------------------------------------------------------------ choices

		private static string RecentText(int days) => LevyUtil.T(days == 1 ? "Reforge.Day" : "Reforge.Days", days);

		private List<(string, Action, bool)> RecentChoices()
		{
			var l = new List<(string, Action, bool)>();
			foreach (int d in ReforgePlan.RecentChoices) { int days = d; l.Add((RecentText(days), () => { Plan.RecentDays = days; Refresh(true); }, Plan.RecentDays == days)); }
			return l;
		}

		private List<(string, Action, bool)> ClassChoices()
		{
			var l = new List<(string, Action, bool)> { (LevyUtil.T("Reforge.Any"), () => { Plan.ClassKey = null; Refresh(true); }, Plan.ClassKey == null) };
			foreach (var c in WeaponTags.ClassKeys) { string id = c; l.Add((WeaponTags.DisplayName(id), () => { Plan.ClassKey = id; Refresh(true); }, Plan.ClassKey == id)); }
			return l;
		}

		private List<(string, Action, bool)> CategoryChoices()
		{
			var l = new List<(string, Action, bool)> { (LevyUtil.T("Reforge.Any"), () => { Plan.Category = null; Refresh(true); }, Plan.Category == null) };
			foreach (var c in WeaponTags.Categories) { string id = c; l.Add((WeaponTags.Title(id), () => { Plan.Category = id; Refresh(true); }, Plan.Category == id)); }
			return l;
		}

		private List<(string, Action, bool)> CapChoices()
		{
			var l = new List<(string, Action, bool)>();
			foreach (long c in ReforgePlan.CapChoices) { long cap = c; l.Add((cap <= 0 ? LevyUtil.T("Reforge.NoCap") : LevyUtil.Coins(cap), () => { Plan.CapCopper = cap; Refresh(true); }, Plan.CapCopper == cap)); }
			return l;
		}

		private static string FloorName(int i) => LevyUtil.T("UI2." + LevyConstants.UI.ReforgeFloorNameKey[i]);

		private string ModifierText() => Plan.Goal == ReforgeGoal.Prefix ? ArmoryTE.PrefixName(Plan.PrefixId) : LevyUtil.T("Reforge.ModifierNone");

		/// <summary>Only the named modifiers the targeted weapons can take (with how many of them can), most widely available first. The floors live in their own row and are never in this list.</summary>
		private List<(string, Action, bool)> ModifierChoices()
		{
			var l = new List<(string, Action, bool)>();
			if (Te != null)
			{
				foreach (var (pid, n) in Te.ValidPrefixesFor(Plan))
				{
					int id = pid;
					l.Add((LevyUtil.T("Reforge.PrefixOption", ArmoryTE.PrefixName(id), n), () => { Plan.SetPrefix(id); Refresh(true); }, Plan.Goal == ReforgeGoal.Prefix && Plan.PrefixId == id));
				}
			}
			if (l.Count == 0) l.Add((LevyUtil.T("Reforge.ModifierNoneAvailable"), null, false));
			return l;
		}

		private void Choose(UIElement anchor, List<(string, Action, bool)> options)
		{
			var a = anchor.GetDimensions(); var me = GetDimensions();
			var items = new List<(string label, Action pick, bool on)>();
			foreach (var (label, pick, on) in options) items.Add((label, pick, on));
			float w = Math.Max(a.Width, 150f);
			_popup.Show(this, Math.Max(0f, a.X - me.X + a.Width - w), a.Y - me.Y + a.Height + 2f, w, items);
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		// ------------------------------------------------------------------ view

		private void Refresh(bool force)
		{
			var plan = Plan;
			for (int i = 0; i < 4; i++) _modes[i].Lit = (int)plan.Mode == i; // the buttons are laid out in enum order
			bool recent = plan.Mode == ReforgeMode.Recent, picked = plan.Mode == ReforgeMode.Picked;
			SetShown(_recentLabel, recent); SetShown(_recent, recent);
			SetShown(_pickToggle, picked); SetShown(_pickClear, picked);
			SetShown(_modeNote, !recent && !picked);
			_modeNote.SetText(LevyUtil.T("Reforge.ModeNote_" + plan.Mode));
			_pickToggle.Set(LevyUtil.T(PickActive ? "Reforge.PickOn" : "Reforge.PickOff"), true); _pickToggle.Lit = PickActive;
			_pickClear.Set(LevyUtil.T("Reforge.PickClear", plan.Picks.Count), plan.Picks.Count > 0);
			bool narrow = !picked;
			_class.Enabled = _cat.Enabled = narrow;
			_classLabel.TextColor = _catLabel.TextColor = narrow ? Color.White : new Color(120, 126, 150);
			for (int i = 0; i < _floors.Length; i++) _floors[i].Lit = plan.Goal == ReforgeGoal.Floor && plan.FloorIndex == i;
			_modifier.Lit = plan.Goal == ReforgeGoal.Prefix;
			if (force) _previewTick = 0;
		}

		private void SetShown(UIElement el, bool shown)
		{
			if (shown && !HasChild(el)) Append(el);
			else if (!shown && HasChild(el)) RemoveChild(el);
		}

		private void RefreshPreview()
		{
			var te = Te;
			if (te == null) return;
			var payer = te.LinkedBarracks();
			_preview = te.PreviewReforge(Plan, payer);
			var pv = _preview;
			bool blindIssued = Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient && Plan.Mode == ReforgeMode.Issued;
			_pv1.SetText(blindIssued ? LevyUtil.T("Reforge.PvIssuedClient") : LevyUtil.T("Reforge.PvLine", pv.Weapons, LevyUtil.Coins(pv.EstCost)));
			var parts = new List<string> { LevyUtil.T("Reforge.PvSelected", pv.Selected) };
			if (pv.AlreadyMet > 0) parts.Add(LevyUtil.T("Reforge.PvMet", pv.AlreadyMet));
			if (pv.NotApplicable > 0) parts.Add(LevyUtil.T("Reforge.PvNA", pv.NotApplicable));
			_pv2.SetText(blindIssued ? "" : string.Join(" ", parts));
			_pv3.SetText(pv.NoPayer ? LevyUtil.T("UI2.ReforgeNoBarracks") : LevyUtil.T("UI.Treasury", LevyUtil.Coins(pv.Treasury)));
			_pv3.TextColor = !pv.NoPayer && pv.EstCost > pv.Treasury ? new Color(255, 190, 120) : Color.White;
			_pv4.SetText(Plan.CapCopper > 0 ? LevyUtil.T("Reforge.PvCap", LevyUtil.Coins(Plan.CapCopper)) : "");
			_run.Set(LevyUtil.T("Reforge.Run"), !pv.NoPayer && pv.Treasury > 0 && (pv.Weapons > 0 || blindIssued));
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true;
			if (--_previewTick <= 0) { _previewTick = 30; RefreshPreview(); }
		}
	}
}
