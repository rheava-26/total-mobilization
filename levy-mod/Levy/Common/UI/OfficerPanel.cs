using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using Levy.Common.Officers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// The officer menu, opened from the Barracks panel and docked beside it (like the soldier loadout panel). A header (title, slot count, pylon link line) over one scrolling list: the command-slot
	/// cards (the serving officer's head, name, kit, wage or unpaid warning, Dismiss) and below them a card per town NPC of the pylon network who can be commissioned (head, name and kit, the kit's
	/// description, fee and wage, Commission, greyed with the reason when it cannot be done). Every text is wrapped to the card and every card is as tall as its text, so nothing clips; the list scrolls
	/// (wheel or bar) and the panel can be dragged by its background and resized from the grip in its bottom-right corner (the size is saved with the character). All actions go through
	/// <see cref="UIActions"/> (server-authoritative; the panel only reads the synced NPC data). The rows are rebuilt only when anything they show changes, checked a few times a second.
	/// </summary>
	public class OfficerPanel : UIState
	{
		public const int DefaultW = 600, MinW = 440, MaxW = 1000, MinH = 280;
		private const int Pad = 16;                 // panel side margin
		private const int DefaultHeaderH = 96;      // the header's height before its text is measured (the static layout checks use it)
		private const int BottomPad = 24, ScreenMargin = 10, MinListH = 120, ScrollW = 20, ScrollGap = 6;
		private const int Portrait = 46;            // officer head icon size
		private const int CardGap = 8, SectionGap = 14, CardPadY = 10, ButtonH = 30;
		public const float TitleScale = 1.15f, SubScale = 0.85f, HeaderScale = 0.95f, NameScale = 0.95f, RoleScale = 0.85f, DescScale = 0.85f, SmallScale = 0.82f, ButtonScale = 0.85f;
		private const int RefreshTicks = 12;
		private static readonly Color Gold = new(255, 215, 80), Dim = new(195, 200, 220), Faint = new(150, 157, 182), Unpaid = new(255, 150, 120);

		private DraggablePanel _panel;
		private LevyButton _close;
		private ResizeGrip _grip;
		private UIElement _header;
		private UIList _list;
		private UIScrollbar _bar;
		private UIElement _content;
		private Vector2 _lastScreen = new(-1f, -1f);
		private BarracksTE _te;
		private string _sig = "";
		private int _refreshTimer;
		private float _w = DefaultW, _wantH, _h = 240f, _gripW0, _gripH0;

		public BarracksTE Te => _te;

		public override void OnInitialize()
		{
			_panel = new DraggablePanel();
			_panel.SetPadding(0);
			_panel.Left.Set(LevyConstants.UI.PanelDefaultLeft + LevyConstants.UI.BarracksPanelWidth + 12, 0f); _panel.Top.Set(LevyConstants.UI.PanelDefaultTop, 0f);
			_panel.Width.Set(_w, 0f); _panel.Height.Set(_h, 0f);
			_panel.BackgroundColor = new Color(33, 43, 79) * 0.92f;

			_header = new UIElement { IgnoresMouseInteraction = true };
			_header.Left.Set(0f, 0f); _header.Top.Set(0f, 0f);
			_panel.Append(_header);
			_close = new LevyButton("X", LevyConstants.UI.ButtonTextScale, 26, 26, () => UISystem.Instance.CloseOfficers(), LevyUtil.T("UI2.Close"));
			_close.Top.Set(8, 0f);
			_panel.Append(_close);

			// The slots and the townsfolk scroll inside this list (mouse wheel or the bar beside it); the header above stays put.
			_list = new UIList { ListPadding = 0f };
			_list.ManualSortMethod = _ => { };
			_bar = new UIScrollbar();
			_panel.Append(_list);
			_panel.Append(_bar);
			_list.SetScrollbar(_bar);
			// the wheel also scrolls from the panel's margins and header; a wheel already handled by the list (or the bar) bubbles up here and must not scroll twice
			_panel.OnScrollWheel += (evt, el) => { if (!IsWithin(evt.Target, _list) && !IsWithin(evt.Target, _bar)) _bar.ViewPosition -= evt.ScrollWheelValue; };

			// resize grip: bottom-right corner, appended last so it sits on top of everything
			_grip = new ResizeGrip(LevyConstants.UI.ResizeGripSize, LevyUtil.T("Officers.UI.ResizeHint"));
			_grip.OnPress = () => { _gripW0 = _w; _gripH0 = _h; };
			_grip.OnDrag = (dx, dy) => Resize(_gripW0 + dx, _gripH0 + dy, false);
			_grip.OnRelease = () => Resize(_w, _h, true);
			_panel.Append(_grip);
			Append(_panel);
		}

		// ------------------------------------------------------------------ pure layout (also read by the boot check, OfficerKitCheck)
		/// <summary>The whole panel's height (header, list, bottom padding) for content of <paramref name="contentH"/> on a screen <paramref name="screenH"/> tall, auto-sized, with the default header.</summary>
		public static float PanelHeight(float contentH, float screenH) => DefaultHeaderH + ListHeight(contentH, screenH) + BottomPad;

		/// <summary>The screen margin the panel keeps top and bottom.</summary>
		public const int EdgeMargin = ScreenMargin;

		/// <summary>The tallest the panel may be on this screen at this UI scale: the whole height less a margin top and bottom.</summary>
		public static float MaxPanelHeight(float screenH) => Math.Max(DefaultHeaderH + MinListH + BottomPad, screenH - 2 * ScreenMargin);

		/// <summary>The list's height for content of <paramref name="contentH"/> on a screen <paramref name="screenH"/> tall (UI pixels), auto-sized, with the default header.</summary>
		public static float ListHeight(float contentH, float screenH) => ListHeight(contentH, screenH, DefaultHeaderH, 0f);

		/// <summary>
		/// The list's height under a header <paramref name="headerH"/> tall. Auto-sized (<paramref name="wantH"/> 0): the whole content when it fits, else what the screen leaves. Resized by the player
		/// (<paramref name="wantH"/> = the panel height dragged to): that height, held between <see cref="MinH"/> and the screen, less the header and the bottom padding. Never below <see cref="MinListH"/>
		/// unless the content is shorter.
		/// </summary>
		public static float ListHeight(float contentH, float screenH, float headerH, float wantH)
		{
			float max = Math.Max(headerH + MinListH + BottomPad, screenH - 2 * ScreenMargin);
			float room = (wantH > 0f ? Math.Clamp(wantH, MinH, max) : max) - headerH - BottomPad;
			room = Math.Max(MinListH, room);
			return wantH > 0f ? room : Math.Min(contentH, room);
		}

		/// <summary>The panel width the player can have on a screen <paramref name="screenW"/> wide: <paramref name="want"/> held between <see cref="MinW"/> and the screen (or <see cref="MaxW"/>).</summary>
		public static float FitWidth(float want, float screenW) => Math.Clamp(want <= 0f ? DefaultW : want, MinW, Math.Max(MinW, Math.Min(MaxW, screenW - 2 * ScreenMargin)));

		/// <summary>The width of the cards for a panel <paramref name="panelW"/> wide (the scrollbar sits to their right).</summary>
		public static float CardWidth(float panelW) => panelW - Pad - ScrollW - ScrollGap - 8;

		/// <summary>A candidate card's height: the head row (name and kit beside the Commission button), the description lines, the cost lines and padding; never shorter than the portrait.</summary>
		public static float CandidateCardHeight(int headLines, float nameLineH, int descLines, float descLineH, int costLines, float smallLineH)
			=> Math.Max(CardPadY * 2 + Portrait, CardPadY + Math.Max(headLines * nameLineH, ButtonH) + 4 + descLines * descLineH + 4 + costLines * smallLineH + CardPadY);

		/// <summary>A command-slot card's height: name, role and wage (or the wrapped unpaid warning) lines and padding; never shorter than the portrait.</summary>
		public static float SlotCardHeight(float nameLineH, int roleLines, float roleLineH, int subLines, float smallLineH)
			=> Math.Max(CardPadY * 2 + Portrait, CardPadY + nameLineH + roleLines * roleLineH + subLines * smallLineH + CardPadY);

		// ------------------------------------------------------------------ text measuring
		private static DynamicSpriteFont Font => FontAssets.MouseText.Value;

		/// <summary>The height of one line of mouse text at <paramref name="scale"/> (what a label of that scale takes, so stacked labels never overlap).</summary>
		private static float LineH(float scale) => (float)Math.Ceiling(Font.MeasureString("Ay").Y * scale);

		private static float TextW(string text, float scale) => Font.MeasureString(text ?? "").X * scale;

		/// <summary>Splits <paramref name="text"/> into lines no wider than <paramref name="maxW"/> at <paramref name="scale"/>. Every word is kept: there is no line cap.</summary>
		private static List<string> Wrap(string text, float scale, float maxW)
		{
			var res = new List<string>();
			text = (text ?? "").Replace("\r", "");
			foreach (string para in text.Split('\n'))
			{
				string[] lines = Utils.WordwrapString(para, Font, Math.Max(40, (int)(maxW / scale)), 64, out int count);
				for (int i = 0; i <= count && i < lines.Length; i++) if (!string.IsNullOrEmpty(lines[i])) res.Add(lines[i].TrimEnd());
			}
			if (res.Count == 0) res.Add("");
			return res;
		}

		/// <summary>Stacks the wrapped lines of <paramref name="text"/> as labels in <paramref name="parent"/> from (x, y); returns the y under the last line.</summary>
		private static float AddLines(UIElement parent, string text, float scale, float x, float y, float maxW, Color color)
		{
			float lh = LineH(scale);
			foreach (string line in Wrap(text, scale, maxW))
			{
				var l = AddTo(parent, new Label(line, scale), x, y);
				l.TextColor = color;
				y += lh;
			}
			return y;
		}

		private static bool IsWithin(UIElement el, UIElement ancestor)
		{
			for (; el != null; el = el.Parent) if (ReferenceEquals(el, ancestor)) return true;
			return false;
		}

		/// <summary>True while the cursor is over the panel; it owns the mouse wheel then (the hotbar must not scroll).</summary>
		public bool Hovered => _panel != null && _panel.ContainsPoint(Main.MouseScreen);

		private static Vector2 Screen => new Vector2(Main.screenWidth, Main.screenHeight) / Main.UIScale;

		/// <summary>Point the panel at a barracks and dock it beside the barracks panel, at the size the player last gave it.</summary>
		public void Bind(BarracksTE te)
		{
			_te = te;
			if (_panel == null) return;
			var scr = Screen;
			var ui = Main.LocalPlayer?.GetModPlayer<UIPlayer>();
			_w = FitWidth(ui != null && ui.OfficersW > 0 ? ui.OfficersW : DefaultW, scr.X);
			_wantH = ui != null && ui.OfficersH > 0 ? ui.OfficersH : 0f;
			float left = Math.Max(LevyConstants.UI.PanelDefaultLeft + LevyConstants.UI.BarracksPanelWidth, BarracksPanel.DockRight) + 12;
			_panel.Left.Set(Math.Max(0f, Math.Min(left, scr.X - _w - ScreenMargin)), 0f);
			_panel.Top.Set(Math.Max(0f, Math.Min(LevyConstants.UI.PanelDefaultTop, scr.Y - _h - ScreenMargin)), 0f);
			_sig = ""; _refreshTimer = 0; _lastScreen = new Vector2(-1f, -1f);
			Rebuild();
			Recalculate();
		}

		/// <summary>The grip: a new width and panel height, held to the screen; <paramref name="persist"/> saves them with the character (on release).</summary>
		private void Resize(float w, float h, bool persist)
		{
			var scr = Screen;
			float nw = FitWidth(w, scr.X), max = Math.Max(MinH, scr.Y - _panel.Top.Pixels - ScreenMargin);
			float nh = Math.Clamp(h, MinH, max);
			bool changed = Math.Abs(nw - _w) >= 1f || Math.Abs(nh - _h) >= 1f;
			_w = nw; _wantH = nh;
			if (changed || persist) { _sig = ""; Rebuild(); }
			if (persist && Main.LocalPlayer != null)
			{
				var ui = Main.LocalPlayer.GetModPlayer<UIPlayer>();
				ui.OfficersW = (int)_w; ui.OfficersH = (int)_wantH;
			}
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_te == null || !UIActions.IsLive(_te)) { UISystem.Instance.CloseOfficers(); return; }
			bool refit = Screen != _lastScreen; // resolution or UI scale changed: refit the panel to the new screen
			if (refit) _sig = "";
			if (--_refreshTimer > 0 && !refit) return;
			_refreshTimer = RefreshTicks;
			Rebuild();
		}

		// ------------------------------------------------------------------ rows
		private sealed class Candidate { public NPC Npc; public OfficerDef Def; public OfficerCheck Check; public BarracksTE Town; public string Pylon; }

		private List<Candidate> Candidates()
		{
			var list = new List<Candidate>();
			var player = Main.LocalPlayer;
			// every town on this barracks's pylon network, this one first (NetworkTownNpcs is grouped by town in network order)
			foreach (var t in SettlementSystem.Instance.NetworkTownNpcs(_te))
			{
				NPC n = t.Npc;
				if (!OfficerRegistry.TryGet(n.type, out OfficerDef def) || OfficerSystem.Instance.IsEnrolled(n)) continue;
				list.Add(new Candidate { Npc = n, Def = def, Town = t.Town, Pylon = t.Pylon, Check = OfficerSystem.Instance.CanCommission(player, _te, n) });
			}
			return list;
		}

		/// <summary>Rebuilds the header and cards when the data they show (or the panel size) changed (the signature covers names, slots, unpaid days, button states and the size).</summary>
		private void Rebuild()
		{
			var player = Main.LocalPlayer;
			string me = player.name;
			var sys = OfficerSystem.Instance;
			int max = sys.SlotCap(me);  // dynamic cap, floored at officers already serving so none is ever hidden
			var slotNpcs = new NPC[max];
			var sb = new System.Text.StringBuilder();
			sb.Append((int)_w).Append('x').Append((int)_wantH).Append('|');
			sb.Append(SettlementSystem.Instance.TownName(_te)).Append('|').Append(SettlementSystem.Instance.LinkLine(_te)).Append('|').Append(sys.UsedSlots(me));
			for (int s = 0; s < max; s++)
			{
				slotNpcs[s] = sys.InSlot(me, s);
				var g = OfficerSystem.Data(slotNpcs[s]);
				sb.Append('|').Append(slotNpcs[s] == null ? "-" : slotNpcs[s].whoAmI + ":" + slotNpcs[s].GivenName + ":" + g?.DaysUnpaid);
			}
			var cands = Candidates();
			foreach (var c in cands) sb.Append('|').Append(c.Town?.ID).Append(c.Pylon).Append('/').Append(c.Npc.whoAmI).Append(':').Append((int)c.Check).Append(':').Append(c.Npc.GivenName);
			string sig = sb.ToString();
			if (sig == _sig) return;
			_sig = sig;

			_lastScreen = Screen;
			_w = FitWidth(_w, _lastScreen.X);
			float scrolled = _bar.ViewPosition;
			float cardW = CardWidth(_w);

			// ---- header: title, slot count, link line, each wrapped to the panel
			_header.RemoveAllChildren();
			_header.Width.Set(_w, 0f);
			float textW = _w - 2 * Pad;
			float hy = AddLines(_header, LevyUtil.T("Officers.UI.Title", SettlementSystem.Instance.TownName(_te)), TitleScale, Pad, 10, textW - 34, Color.White) + 2;
			hy = AddLines(_header, LevyUtil.T("Officers.UI.Sub", sys.UsedSlots(me), max), SubScale, Pad, hy, textW, Dim);
			var lk = SettlementSystem.Instance.LinkOf(_te);
			hy = AddLines(_header, SettlementSystem.Instance.LinkLine(_te), SmallScale, Pad, hy, textW, lk.Linked ? Dim : Unpaid);
			float headerH = (float)Math.Ceiling(hy + 8);
			_header.Height.Set(headerH, 0f);
			_close.Left.Set(_w - 36, 0f);

			// ---- the list content
			_list.Clear();
			_content = new UIElement();
			_content.Width.Set(cardW, 0f);
			float y = 0;
			y = AddLines(_content, LevyUtil.T("Officers.UI.SlotsHeader"), HeaderScale, 0, y, cardW, Gold) + 4;
			for (int s = 0; s < max; s++) y = AddSlotCard(s, slotNpcs[s], 0, y, cardW) + CardGap;

			y += SectionGap - CardGap;
			y = AddLines(_content, LevyUtil.T("Officers.UI.Available"), HeaderScale, 0, y, cardW, Gold) + 4;
			if (cands.Count == 0) y = AddLines(_content, LevyUtil.T("Officers.UI.NoneAvailable"), SubScale, 0, y, cardW, Dim) + 6;

			BarracksTE lastTown = null; string lastPylon = null; bool multiTown = false;
			foreach (var c in cands) if (!ReferenceEquals(c.Town, _te)) { multiTown = true; break; }
			foreach (var c in cands)
			{
				if (multiTown && (!ReferenceEquals(c.Town, lastTown) || c.Pylon != lastPylon))
				{
					lastTown = c.Town; lastPylon = c.Pylon;
					string tn = c.Town == null ? LevyUtil.T("Officers.UI.PylonTownHeader", c.Pylon ?? "") : SettlementSystem.Instance.TownName(c.Town);
					string head = ReferenceEquals(c.Town, _te) ? tn : c.Town == null ? tn : LevyUtil.T("Officers.UI.TownHeader", tn);
					y = AddLines(_content, head, SubScale, 0, y, cardW, ReferenceEquals(c.Town, _te) ? Gold : Dim) + 2;
				}
				y = AddCandidateCard(c, 0, y, cardW) + CardGap;
			}

			y = AddLines(_content, LevyUtil.T("Officers.UI.Hint"), SmallScale, 0, y + 2, cardW, Faint) + 10; // the bottom margin keeps the last line clear of the list's edge

			float contentH = (float)Math.Ceiling(y);
			_content.Height.Set(contentH, 0f);
			float listH = ListHeight(contentH, _lastScreen.Y, headerH, _wantH);
			_list.Left.Set(Pad, 0f); _list.Top.Set(headerH, 0f); _list.Width.Set(cardW, 0f); _list.Height.Set(listH, 0f);
			_bar.Left.Set(Pad + cardW + ScrollGap, 0f); _bar.Top.Set(headerH, 0f); _bar.Height.Set(listH, 0f);
			_list.Add(_content);
			bool scrolls = contentH > listH + 0.5f;
			if (scrolls && !_panel.HasChild(_bar)) _panel.Append(_bar);
			else if (!scrolls && _panel.HasChild(_bar)) _panel.RemoveChild(_bar);
			_h = headerH + listH + BottomPad;
			_panel.Width.Set(_w, 0f); _panel.Height.Set(_h, 0f);
			_grip.Left.Set(_w - LevyConstants.UI.ResizeGripSize - 3f, 0f); _grip.Top.Set(_h - LevyConstants.UI.ResizeGripSize - 3f, 0f);
			if (_panel.HasChild(_grip)) { _panel.RemoveChild(_grip); } _panel.Append(_grip); // keep the grip on top
			// never let the panel's bottom or right edge fall off the screen (it may have been dragged, or the screen may have shrunk)
			_panel.Top.Set(Math.Max(0f, Math.Min(_panel.Top.Pixels, _lastScreen.Y - _h - ScreenMargin)), 0f);
			_panel.Left.Set(Math.Max(0f, Math.Min(_panel.Left.Pixels, _lastScreen.X - _w - ScreenMargin)), 0f);
			Recalculate();
			_list.Recalculate();
			_bar.ViewPosition = scrolled;
		}

		// ---- cards ----------------------------------------------------------
		/// <summary>A command-slot card: the serving officer's head, name, kit, wage / unpaid warning and a Dismiss button, or an "empty" placeholder. Returns the card's bottom y.</summary>
		private float AddSlotCard(int slot, NPC n, float x, float y, float w)
		{
			var sys = OfficerSystem.Instance;
			bool filled = n != null;
			float lhName = LineH(NameScale), lhSmall = LineH(SmallScale), lhRole = LineH(RoleScale);

			if (!filled)
			{
				float tw = w - 32;
				var a = Wrap(LevyUtil.T("Officers.UI.SlotEmpty", slot + 1), NameScale, tw);
				var b = Wrap(LevyUtil.T("Officers.UI.SlotEmptyHint"), SmallScale, tw);
				float eh = CardPadY + a.Count * lhName + b.Count * lhSmall + CardPadY;
				UIPanel empty = NewCard(x, y, w, eh, new Color(28, 36, 64) * 0.8f, new Color(80, 88, 120) * 0.6f);
				float ey = AddLines(empty, string.Join(" ", a), NameScale, 16, CardPadY, tw, Dim);
				AddLines(empty, string.Join(" ", b), SmallScale, 16, ey, tw, Faint);
				return y + eh;
			}

			var g = OfficerSystem.Data(n);
			OfficerRegistry.TryGet(n.type, out OfficerDef def);
			string role = def != null ? LevyUtil.T("Officers.Kit." + def.Key + ".Name") : n.TypeName;
			string dismiss = LevyUtil.T("Officers.UI.Dismiss");
			float btnW = Math.Max(92f, TextW(dismiss, ButtonScale) + 24f);
			float tx = 10 + Portrait + 12, textW = w - tx - btnW - 24;

			string subText; Color subColor;
			if (g != null && g.DaysUnpaid > 0) { subText = OfficerSystem.IsSuspended(g) ? LevyUtil.T("Officers.UI.OffDuty") : LevyUtil.T("Officers.UI.UnpaidWarn", g.DaysUnpaid); subColor = Unpaid; }
			else { subText = LevyUtil.T("Officers.UI.SlotWage", LevyUtil.Coins(def != null ? sys.Wage(def) : 0)); subColor = Faint; }
			string roleText = LevyUtil.T("Officers.UI.SlotRole", slot + 1, role);
			int roleLines = Wrap(roleText, RoleScale, textW).Count, subLines = Wrap(subText, SmallScale, textW).Count;
			float h = SlotCardHeight(lhName, roleLines, lhRole, subLines, lhSmall);

			UIPanel card = NewCard(x, y, w, h, new Color(48, 60, 112) * 0.95f, Gold * 0.55f);
			AddTo(card, new NpcHeadIcon(n, Portrait), 10, (h - Portrait) / 2f);
			var name = AddTo(card, new Label(OfficerSystem.NameOf(n), NameScale), tx, CardPadY);
			name.TextColor = Gold;
			float ty = AddLines(card, roleText, RoleScale, tx, CardPadY + lhName, textW, Dim);
			AddLines(card, subText, SmallScale, tx, ty, textW, subColor);

			NPC captured = n;
			AddTo(card, new LevyButton(dismiss, ButtonScale, btnW, ButtonH, () => { UIActions.OfficerDismiss(captured); _refreshTimer = 0; }, LevyUtil.T("Officers.UI.DismissHint", OfficerSystem.NameOf(n))), w - 12 - btnW, (h - ButtonH) / 2f);
			return y + h;
		}

		/// <summary>A commissionable-townsfolk card: head, name and kit, the wrapped kit description, the fee / wage, and a Commission button (greyed with the reason when unavailable). Returns the card's bottom y.</summary>
		private float AddCandidateCard(Candidate c, float x, float y, float w)
		{
			var sys = OfficerSystem.Instance;
			string name = OfficerSystem.NameOf(c.Npc);
			string role = LevyUtil.T("Officers.Kit." + c.Def.Key + ".Name");
			bool ok = c.Check == OfficerCheck.Ok;
			long fee = sys.Fee(c.Def), wage = sys.Wage(c.Def);
			string commission = LevyUtil.T("Officers.UI.Commission");
			float btnW = Math.Max(112f, TextW(commission, ButtonScale) + 24f);
			float lhName = LineH(NameScale), lhDesc = LineH(DescScale), lhSmall = LineH(SmallScale);

			float tx = 10 + Portrait + 12;
			float headW = w - tx - btnW - 24, bodyW = w - tx - 14;
			string headText = LevyUtil.T("Officers.UI.CandidateHead", name, role);
			string desc = LevyUtil.T("Officers.Kit." + c.Def.Key + ".Desc", c.Def.Kit?.DescriptionArgs ?? Array.Empty<object>()).Replace("\r", "").Replace("\n", " ");
			string costText = LevyUtil.T("Officers.UI.Cost", LevyUtil.Coins(fee), LevyUtil.Coins(wage));
			int headLines = Wrap(headText, NameScale, headW).Count, descLines = Wrap(desc, DescScale, bodyW).Count, costLines = Wrap(costText, SmallScale, bodyW).Count;
			float h = CandidateCardHeight(headLines, lhName, descLines, lhDesc, costLines, lhSmall);

			UIPanel card = NewCard(x, y, w, h,
				new Color(42, 54, 100) * 0.92f,
				ok ? new Color(120, 200, 130) * 0.6f : new Color(96, 104, 142) * 0.6f);

			AddTo(card, new NpcHeadIcon(c.Npc, Portrait), 10, CardPadY);
			AddLines(card, headText, NameScale, tx, CardPadY, headW, Color.White);
			float ly = CardPadY + Math.Max(headLines * lhName, ButtonH) + 4;
			ly = AddLines(card, desc, DescScale, tx, ly, bodyW, Dim) + 4;
			AddLines(card, costText, SmallScale, tx, ly, bodyW, ok || c.Check == OfficerCheck.NoMoney ? Gold : Faint);

			string hintText = ok ? LevyUtil.T("Officers.UI.CommissionHint", LevyUtil.Coins(fee), LevyUtil.Coins(wage)) : LevyUtil.T("Officers.Fail." + c.Check);
			NPC captured = c.Npc; var te = _te;
			AddTo(card, new LevyButton(commission, ButtonScale, btnW, ButtonH, () => { UIActions.OfficerCommission(te, captured); _refreshTimer = 0; }, hintText) { Enabled = ok }, w - 12 - btnW, CardPadY);
			return y + h;
		}

		// ---- building blocks ------------------------------------------------
		private UIPanel NewCard(float x, float y, float w, float h, Color bg, Color border)
		{
			var card = new UIPanel();
			card.SetPadding(0);
			card.Left.Set(x, 0f); card.Top.Set(y, 0f); card.Width.Set(w, 0f); card.Height.Set(h, 0f);
			card.BackgroundColor = bg; card.BorderColor = border;
			_content.Append(card);
			return card;
		}

		private static T AddTo<T>(UIElement parent, T el, float left, float top) where T : UIElement
		{
			el.Left.Set(left, 0f); el.Top.Set(top, 0f);
			parent.Append(el);
			return el;
		}

		/// <summary>Draws a town NPC's head portrait (via the game's own head texture) inside a faint frame; any failure degrades to just the frame, so it can never break the panel.</summary>
		private sealed class NpcHeadIcon : UIElement
		{
			private readonly NPC _npc;

			public NpcHeadIcon(NPC npc, int size)
			{
				_npc = npc;
				Width.Set(size, 0f); Height.Set(size, 0f);
				IgnoresMouseInteraction = true;
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				var r = GetDimensions().ToRectangle();
				var px = TextureAssets.MagicPixel.Value;
				spriteBatch.Draw(px, r, new Color(18, 22, 42) * 0.7f);
				try
				{
					if (_npc == null || !_npc.active) return;
					int idx = TownNPCProfiles.GetHeadIndexSafe(_npc);
					if (idx <= 0 || idx >= TextureAssets.NpcHead.Length) return;
					var asset = TextureAssets.NpcHead[idx];
					if (asset == null || !asset.IsLoaded) return;
					var tex = asset.Value;
					if (tex == null || tex.Width == 0 || tex.Height == 0) return;
					float scale = Math.Min((r.Width - 6f) / tex.Width, (r.Height - 6f) / tex.Height);
					var center = new Vector2(r.X + r.Width * 0.5f, r.Y + r.Height * 0.5f);
					spriteBatch.Draw(tex, center, null, Color.White, 0f, new Vector2(tex.Width * 0.5f, tex.Height * 0.5f), scale, SpriteEffects.None, 0f);
				}
				catch { /* the head is cosmetic; never let a texture issue break the menu */ }
			}
		}
	}
}
