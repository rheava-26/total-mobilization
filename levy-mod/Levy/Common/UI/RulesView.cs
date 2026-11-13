using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// The Rules tab of the armory panel: the armory's rule blocks, top to bottom, each a card with its numbers, a live preview line, and buttons to move or remove it.
	/// Cards are rebuilt only when the list's shape changes (a block added, removed or moved, a mode or category picked); number edits keep their widgets. Every edit goes through <see cref="Edited"/>,
	/// which the armory panel turns into a (debounced) packet to the server. The preview lines refresh a few times a second from the armory's stock (<see cref="ArmoryTE.CountStock"/>).
	/// </summary>
	public class RulesView : UIElement
	{
		private const int CardWidth = 640, HeaderH = 28, PreviewH = 20, Pad = 6;

		private readonly ArmoryPanel _owner;
		private readonly UIList _list = new();
		private readonly UIScrollbar _bar = new();
		private readonly ChoicePopup _popup = new();
		private readonly Label _hint;
		private readonly List<(int index, Label label)> _previews = new();
		private readonly List<NumberField> _fields = new();
		private int _refreshTick;

		private ArmoryRules Rules => _owner.Te?.Rules;
		private ArmoryTE Te => _owner.Te;

		public bool Typing { get { foreach (var f in _fields) if (f.Typing) return true; return false; } }

		public RulesView(ArmoryPanel owner)
		{
			_owner = owner;
			Width.Set(LevyConstants.UI.ArmoryPanelWidth - 20, 0f); Height.Set(LevyConstants.UI.ArmoryBarTop - 36 - 4, 0f);
			float x = 0;
			foreach (var (kind, key) in new[] { (RuleKind.ClassBalance, "AddClass"), (RuleKind.Category, "AddCategory"), (RuleKind.ItemList, "AddList") })
			{
				var k = kind;
				var b = new LevyButton(LevyUtil.T("Rules." + key), LevyConstants.UI.ButtonTextScale, 150, 24, () => AddBlock(k), LevyUtil.T("Rules." + key + "Hint"));
				b.Left.Set(x, 0f); Append(b); x += 156;
			}
			var reset = new LevyButton(LevyUtil.T("Rules.Reset"), LevyConstants.UI.ButtonTextScale, 90, 24, ResetRules, LevyUtil.T("Rules.ResetHint"));
			reset.Left.Set(Width.Pixels - 90, 0f); Append(reset);
			_hint = new Label(LevyUtil.T("Rules.OrderNote"), 0.72f);
			_hint.Left.Set(0f, 0f); _hint.Top.Set(28f, 0f);
			Append(_hint);

			_list.Top.Set(46f, 0f); _list.Width.Set(CardWidth + 4, 0f); _list.Height.Set(Height.Pixels - 46f, 0f); _list.ListPadding = 6f;
			_bar.Left.Set(CardWidth + 10, 0f); _bar.Top.Set(46f, 0f); _bar.Height.Set(Height.Pixels - 46f, 0f);
			_list.SetScrollbar(_bar);
			Append(_list); Append(_bar);
		}

		// ------------------------------------------------------------------ edits

		/// <summary>Any number or toggle changed: the rules are marked changed, the panel schedules the packet, and the previews refresh.</summary>
		private void Edited()
		{
			_owner.RulesEdited();
			_refreshTick = 0;
		}

		/// <summary>The list's shape changed (or a choice that changes a card's layout was made): the cards are rebuilt.</summary>
		private void Reshape()
		{
			Edited();
			float pos = _bar.ViewPosition;
			Build();
			_bar.ViewPosition = pos;
		}

		private void AddBlock(RuleKind kind)
		{
			var r = Rules; if (r == null) return;
			var b = r.AddBlock(kind);
			if (b == null) return;
			if (kind == RuleKind.Category) { b.Mode = CatMode.Weight; b.Cat = "bow"; b.Other = "gun"; }
			SoundEngine.PlaySound(SoundID.MenuTick);
			Reshape();
			_bar.ViewPosition = float.MaxValue;
		}

		private void ResetRules()
		{
			var r = Rules; if (r == null) return;
			r.Blocks = ArmoryRules.Default().Blocks; // the per-item overrides are kept: they are set from the item panel
			Reshape();
		}

		// ------------------------------------------------------------------ building

		/// <summary>Rebuilds every card from the armory's rule list.</summary>
		public void Build()
		{
			_list.Clear(); _previews.Clear(); _fields.Clear(); _classStock.Clear();
			if (_popup.Open) _popup.Remove();
			var r = Rules;
			if (r == null) return;
			for (int i = 0; i < r.Blocks.Count; i++) _list.Add(BuildCard(r, i));
			_list.Add(BuildLimitsCard(r));
			_list.Add(BuildHandSetCard(r));
			_list.Recalculate();
			Refresh();
		}

		private UIElement BuildCard(ArmoryRules rules, int index)
		{
			var b = rules.Blocks[index];
			int bodyH = BodyHeight(b);
			var card = new UIPanel();
			card.SetPadding(0);
			card.Width.Set(CardWidth, 0f); card.Height.Set(HeaderH + bodyH + PreviewH + 2 * Pad, 0f);
			card.BackgroundColor = b.On ? new Color(44, 58, 108) * 0.92f : new Color(30, 34, 52) * 0.9f;

			// header: on/off, number + title, move up / down, remove
			var on = new LevyButton(LevyUtil.T(b.On ? "Rules.On" : "Rules.Off"), LevyConstants.UI.ButtonTextScale, 44, 22, () => { b.On = !b.On; Reshape(); }, LevyUtil.T("Rules.OnHint")) { Lit = b.On };
			on.Left.Set(Pad, 0f); on.Top.Set(Pad, 0f); card.Append(on);
			var title = new Label((index + 1) + ".  " + LevyUtil.T("Rules.Kind_" + b.Kind), 0.9f);
			title.Left.Set(Pad + 54, 0f); title.Top.Set(Pad + 3, 0f); card.Append(title);
			int idx = index;
			var rm = new LevyButton("X", LevyConstants.UI.ButtonTextScale, 24, 22, () => { rules.Remove(idx); Reshape(); }, LevyUtil.T("Rules.RemoveHint"));
			rm.Left.Set(CardWidth - 24 - Pad, 0f); rm.Top.Set(Pad, 0f); card.Append(rm);
			var dn = new LevyButton("v", LevyConstants.UI.ButtonTextScale, 24, 22, () => { rules.Move(idx, 1); Reshape(); }, LevyUtil.T("Rules.DownHint")) { Enabled = index < rules.Blocks.Count - 1 };
			dn.Left.Set(CardWidth - 52 - Pad, 0f); dn.Top.Set(Pad, 0f); card.Append(dn);
			var up = new LevyButton("^", LevyConstants.UI.ButtonTextScale, 24, 22, () => { rules.Move(idx, -1); Reshape(); }, LevyUtil.T("Rules.UpHint")) { Enabled = index > 0 };
			up.Left.Set(CardWidth - 80 - Pad, 0f); up.Top.Set(Pad, 0f); card.Append(up);

			float y = HeaderH + Pad;
			switch (b.Kind)
			{
				case RuleKind.Category: BuildCategory(card, b, y); break;
				case RuleKind.ItemList: BuildItemList(card, b, y); break;
				default: BuildClasses(card, b, y); break;
			}
			var pv = new Label("", 0.72f);
			pv.Left.Set(Pad + 2, 0f); pv.Top.Set(HeaderH + Pad + bodyH + 3, 0f); card.Append(pv);
			_previews.Add((index, pv));
			return card;
		}

		/// <summary>The loadout limits card: the most sidearms a levy who is not a summoner may carry.</summary>
		private UIElement BuildLimitsCard(ArmoryRules rules)
		{
			var card = new UIPanel();
			card.SetPadding(0);
			card.Width.Set(CardWidth, 0f); card.Height.Set(HeaderH + 28 + Pad, 0f);
			card.BackgroundColor = new Color(30, 44, 72) * 0.92f;
			var title = new Label(LevyUtil.T("Rules.LimitsTitle"), 0.9f); title.Left.Set(Pad + 4, 0f); title.Top.Set(Pad + 3, 0f); card.Append(title);
			float y = HeaderH + Pad - 2;
			var l1 = new Label(LevyUtil.T("Rules.SidearmLimit"), 0.85f); l1.Left.Set(Pad + 4, 0f); l1.Top.Set(y + 4, 0f); card.Append(l1);
			Field(card, 300, y, 112, 0, LevyConstants.MaxSidearms, 1, true, () => rules.SidearmLimit, v => { rules.SidearmLimit = (int)v; rules.Changed(); });
			var n1 = new Label(LevyUtil.T("Rules.SidearmLimitNote"), 0.7f); n1.Left.Set(420, 0f); n1.Top.Set(y + 6, 0f); card.Append(n1);
			return card;
		}

		/// <summary>Every item the player marked Preferred, Never or a set rank, listed here so it can be cleared without the item being in the armory.</summary>
		private UIElement BuildHandSetCard(ArmoryRules rules)
		{
			var shown = new List<(string key, ItemOverride o)>();
			foreach (var kv in rules.Overrides) if (kv.Value.Rank != RankOverrideMode.Auto) shown.Add((kv.Key, kv.Value));
			int rows = Math.Max(1, (shown.Count + 12) / 13);
			var card = new UIPanel();
			card.SetPadding(0);
			card.Width.Set(CardWidth, 0f); card.Height.Set(HeaderH + 18 + rows * 40 + Pad + 4, 0f);
			card.BackgroundColor = new Color(30, 44, 72) * 0.92f;
			var title = new Label(LevyUtil.T("Rules.HandSetTitle"), 0.9f); title.Left.Set(Pad + 4, 0f); title.Top.Set(Pad + 3, 0f); card.Append(title);
			if (shown.Count > 0)
			{
				var all = new LevyButton(LevyUtil.T("Rules.HandSetClear"), LevyConstants.UI.ButtonTextScale, 90, 22, () =>
				{
					foreach (var (_, o) in shown) o.Rank = RankOverrideMode.Auto;
					rules.Prune(); Reshape();
				}, LevyUtil.T("Rules.HandSetClearHint"));
				all.Left.Set(CardWidth - 90 - Pad, 0f); all.Top.Set(Pad, 0f); card.Append(all);
			}
			var note = new Label(LevyUtil.T(shown.Count > 0 ? "Rules.HandSetNote" : "Rules.HandSetNone"), 0.72f); note.Left.Set(Pad + 4, 0f); note.Top.Set(HeaderH + 2, 0f); card.Append(note);
			for (int i = 0; i < shown.Count; i++)
			{
				var (key, o) = shown[i];
				int type = ArmoryRules.TypeOf(key);
				string badge = o.Rank == RankOverrideMode.Prefer ? "P" : o.Rank == RankOverrideMode.Never ? "N" : o.Value.ToString("0.#");
				string what = LevyUtil.T(o.Rank == RankOverrideMode.Prefer ? "Rules.HandSetPrefer" : o.Rank == RankOverrideMode.Never ? "Rules.HandSetNever" : "Rules.HandSetSet", o.Value.ToString("0.#"));
				var cell = new HandSetCell(type, badge, what, () => { o.Rank = RankOverrideMode.Auto; rules.Prune(); Reshape(); });
				cell.Left.Set(Pad + 4 + (i % 13) * 38, 0f); cell.Top.Set(HeaderH + 18 + (i / 13) * 40, 0f);
				card.Append(cell);
			}
			return card;
		}

		private static int BodyHeight(RuleBlock b)
		{
			switch (b.Kind)
			{
				case RuleKind.Category: return 28;
				case RuleKind.ItemList: return 40 * Math.Max(1, (b.Items.Count + 1 + 12) / 13) + 4;
				default: return 16 + WeaponTags.ClassKeys.Length * 26;
			}
		}

		private NumberField Field(UIElement parent, float x, float y, float w, float min, float max, float step, bool whole, Func<float> get, Action<float> set, float offAt = float.NegativeInfinity, string offText = "")
		{
			var f = new NumberField(w, min, max, step, whole, get, v => { set(v); Edited(); }) { OffAt = offAt, OffText = offText };
			f.Left.Set(x, 0f); f.Top.Set(y, 0f);
			parent.Append(f); _fields.Add(f);
			return f;
		}

		private DropButton Drop(UIElement parent, float x, float y, float w, Func<string> current, Action<DropButton> open, string hint = null)
		{
			var d = new DropButton(w, current, open, hint);
			d.Left.Set(x, 0f); d.Top.Set(y, 0f);
			parent.Append(d);
			return d;
		}

		private void BuildCategory(UIElement card, RuleBlock b, float y)
		{
			float x = Pad;
			Drop(card, x, y, 120, () => LevyUtil.T("Rules.Mode_" + b.Mode), btn => Choose(btn, ModeChoices(b)), LevyUtil.T("Rules.ModeHint_" + b.Mode)); x += 128;
			if (b.Mode == CatMode.Min) { Field(card, x, y, 104, 0, 999, 1, true, () => b.Count, v => b.Count = (int)v); x += 112; }
			Drop(card, x, y, 120, () => WeaponTags.Title(b.Cat), btn => Choose(btn, CatChoices(b.Cat, c => { b.Cat = c; Reshape(); })), LevyUtil.T("Rules.CategoryHint")); x += 128;
			switch (b.Mode)
			{
				case CatMode.Weight:
					var times = new Label("x", 0.9f); times.Left.Set(x, 0f); times.Top.Set(y + 4, 0f); card.Append(times); x += 14;
					Field(card, x, y, 112, 0.1f, 20f, 0.25f, false, () => b.Weight, v => b.Weight = v);
					break;
				case CatMode.Over:
					var over = new Label(LevyUtil.T("Rules.OverWord"), 0.9f); over.Left.Set(x, 0f); over.Top.Set(y + 4, 0f); card.Append(over); x += 48;
					Drop(card, x, y, 120, () => WeaponTags.Title(b.Other), btn => Choose(btn, CatChoices(b.Other, c => { b.Other = c; Reshape(); })), LevyUtil.T("Rules.OtherHint"));
					break;
			}
		}

		private void BuildItemList(UIElement card, RuleBlock b, float y)
		{
			Drop(card, Pad, y, 100, () => LevyUtil.T("Rules.List_" + b.List), btn => Choose(btn, new List<(string, Action, bool)>
			{
				(LevyUtil.T("Rules.List_Never"), () => { b.List = ListMode.Never; Reshape(); }, b.List == ListMode.Never),
				(LevyUtil.T("Rules.List_Only"), () => { b.List = ListMode.Only; Reshape(); }, b.List == ListMode.Only),
			}), LevyUtil.T("Rules.ListModeHint"));
			float x0 = Pad + 108;
			int cells = b.Items.Count + 1;
			for (int i = 0; i < cells; i++)
			{
				int col = i % 13, row = i / 13;
				float cx = x0 + col * 38, cy = y - 2 + row * 40;
				UIElement cell;
				if (i < b.Items.Count)
				{
					int type = ArmoryRules.TypeOf(b.Items[i]);
					string key = b.Items[i];
					cell = type > 0 ? new TypeCell(type, () => { b.Items.Remove(key); Reshape(); }) : new TypeCell(ItemID.None, () => { b.Items.Remove(key); Reshape(); });
				}
				else
				{
					cell = new AddTypeCell(t =>
					{
						string key = ArmoryRules.KeyOf(t);
						if (key != null && !b.Items.Contains(key) && b.Items.Count < ArmoryRules.MaxItemsPerList) { b.Items.Add(key); Reshape(); }
					});
				}
				cell.Left.Set(cx, 0f); cell.Top.Set(cy, 0f);
				card.Append(cell);
			}
		}

		private void BuildClasses(UIElement card, RuleBlock b, float y)
		{
			string[] cols = { "ColWeight", "ColMin", "ColMax", "ColFixed" };
			float[] xs = { 110, 224, 338, 452 };
			for (int c = 0; c < 4; c++)
			{
				var h = new Label(LevyUtil.T("Rules." + cols[c]), 0.72f);
				h.Left.Set(xs[c] + 30, 0f); h.Top.Set(y - 2, 0f); card.Append(h);
			}
			var stockHead = new Label(LevyUtil.T("Rules.ColStock"), 0.72f); stockHead.Left.Set(566, 0f); stockHead.Top.Set(y - 2, 0f); card.Append(stockHead);
			float ry = y + 14;
			foreach (var cls in WeaponTags.ClassKeys)
			{
				string id = cls;
				var name = new Label(WeaponTags.DisplayName(id), 0.85f);
				name.Left.Set(Pad + 2, 0f); name.Top.Set(ry + 4, 0f); card.Append(name);
				Func<ClassEntry> e = () => b.EntryFor(id, true);
				Field(card, xs[0], ry, 104, 0, 100, 0.5f, false, () => b.EntryFor(id, false)?.Weight ?? 0f, v => e().Weight = v, 0f, LevyUtil.T("Rules.Auto"));
				Field(card, xs[1], ry, 104, 0, 999, 1, true, () => b.EntryFor(id, false)?.Min ?? 0, v => e().Min = (int)v);
				Field(card, xs[2], ry, 104, 0, 999, 1, true, () => b.EntryFor(id, false)?.Max ?? -1, v => e().Max = (int)v, -1f, LevyUtil.T("Rules.NoLimit"));
				Field(card, xs[3], ry, 104, 0, 999, 1, true, () => b.EntryFor(id, false)?.Fixed ?? -1, v => e().Fixed = (int)v, -1f, LevyUtil.T("Rules.Off"));
				var stock = new Label("", 0.8f); stock.Left.Set(570, 0f); stock.Top.Set(ry + 4, 0f); card.Append(stock);
				_classStock.Add((id, stock));
				ry += 26;
			}
		}

		private readonly List<(string cls, Label label)> _classStock = new();

		// ------------------------------------------------------------------ choice lists

		private List<(string, Action, bool)> ModeChoices(RuleBlock b)
		{
			var l = new List<(string, Action, bool)>();
			foreach (CatMode m in new[] { CatMode.Weight, CatMode.Over, CatMode.Include, CatMode.Min })
			{
				var mode = m;
				l.Add((LevyUtil.T("Rules.Mode_" + m), () => { b.Mode = mode; Reshape(); }, b.Mode == m));
			}
			return l;
		}

		private static List<(string, Action, bool)> CatChoices(string current, Action<string> pick)
		{
			var l = new List<(string, Action, bool)>();
			foreach (var c in WeaponTags.Categories) { string id = c; l.Add((WeaponTags.Title(id), () => pick(id), id == current)); }
			return l;
		}

		private void Choose(UIElement anchor, List<(string, Action, bool)> options)
		{
			var a = anchor.GetDimensions(); var me = GetDimensions();
			var items = new List<(string label, Action pick, bool on)>();
			foreach (var (label, pick, on) in options) items.Add((label, pick, on));
			_popup.Show(this, a.X - me.X, a.Y - me.Y + a.Height + 2f, Math.Max(a.Width, 120f), items);
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		// ------------------------------------------------------------------ previews

		/// <summary>Recomputes the preview lines and the class stock numbers.</summary>
		public void Refresh()
		{
			var r = Rules; var te = Te;
			if (r == null || te == null) return;
			var stock = te.CountStock();
			int levies = te.ServedCount;
			foreach (var (index, label) in _previews)
			{
				if (index >= r.Blocks.Count) continue;
				var s = RuleStatus.Describe(r, index, stock, levies);
				label.SetText(s.Text);
				label.TextColor = s.Warn ? new Color(255, 214, 110) : new Color(190, 205, 235);
			}
			foreach (var (cls, label) in _classStock) label.SetText(stock.Class(cls).ToString());
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (--_refreshTick <= 0) { _refreshTick = 20; Refresh(); }
		}
	}
}
