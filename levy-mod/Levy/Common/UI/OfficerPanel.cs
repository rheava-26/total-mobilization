using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using Levy.Common.Officers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// The officer menu, opened from the Barracks panel and docked beside it (like the soldier loadout panel). Top: the commander's command slots, each showing its
	/// officer with a Dismiss button. Below: every registered kind of officer housed in this settlement who is not yet serving, with the kit text, the fee and wage, and a
	/// Commission button that is greyed with the reason when it cannot be done. All actions go through <see cref="UIActions"/> (server-authoritative; the panel only
	/// reads the synced NPC data, so it can never create money or slots). Rows are rebuilt when anything they show changes, checked a few times a second.
	/// </summary>
	public class OfficerPanel : UIState
	{
		private const int W = 460;
		private const int SlotRowH = 40, SlotTop = 58;
		private const int HeaderGap = 12;
		private const float DescScale = 0.62f, TextScale = 0.75f;
		private const int DescMaxLines = 5;
		private const int RefreshTicks = 12;
		private static readonly Color Gold = new(255, 215, 80), Dim = new(170, 175, 195);

		private DraggablePanel _panel;
		private Label _title, _sub;
		private LevyButton _close;
		private readonly List<UIElement> _dyn = new();
		private BarracksTE _te;
		private string _sig = "";
		private int _refreshTimer;
		private float _h = 200f;

		public BarracksTE Te => _te;

		public override void OnInitialize()
		{
			_panel = new DraggablePanel();
			_panel.SetPadding(0);
			_panel.Left.Set(LevyConstants.UI.PanelDefaultLeft + LevyConstants.UI.BarracksPanelWidth + 12, 0f); _panel.Top.Set(LevyConstants.UI.PanelDefaultTop, 0f);
			_panel.Width.Set(W, 0f); _panel.Height.Set(_h, 0f);
			_panel.BackgroundColor = new Color(33, 43, 79) * 0.9f;

			_title = Put(new Label("", 1f), 12, 8);
			_close = Put(new LevyButton("X", LevyConstants.UI.ButtonTextScale, 26, 26, () => UISystem.Instance.CloseOfficers(), LevyUtil.T("UI2.Close")), W - 36, 6);
			_sub = Put(new Label("", 0.68f), 12, 34);
			_sub.TextColor = Dim;
			Append(_panel);
		}

		private T Put<T>(T el, float left, float top) where T : UIElement
		{
			el.Left.Set(left, 0f); el.Top.Set(top, 0f);
			_panel.Append(el);
			return el;
		}

		private T PutDyn<T>(T el, float left, float top) where T : UIElement
		{
			el.Left.Set(left, 0f); el.Top.Set(top, 0f);
			_panel.Append(el); _dyn.Add(el);
			return el;
		}

		/// <summary>Point the panel at a barracks and dock it beside the barracks panel.</summary>
		public void Bind(BarracksTE te)
		{
			_te = te;
			if (_panel == null) return;
			float sw = Main.screenWidth / Main.UIScale, sh = Main.screenHeight / Main.UIScale;
			float left = Math.Max(LevyConstants.UI.PanelDefaultLeft + LevyConstants.UI.BarracksPanelWidth, BarracksPanel.DockRight) + 12;
			_panel.Left.Set(Math.Max(0f, Math.Min(left, sw - W - 10f)), 0f);
			_panel.Top.Set(Math.Max(0f, Math.Min(LevyConstants.UI.PanelDefaultTop, sh - _h - 10f)), 0f);
			_sig = ""; _refreshTimer = 0;
			Rebuild();
			Recalculate();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_te == null || !UIActions.IsLive(_te)) { UISystem.Instance.CloseOfficers(); return; }
			if (--_refreshTimer > 0) return;
			_refreshTimer = RefreshTicks;
			Rebuild();
		}

		// ------------------------------------------------------------------ rows
		private sealed class Candidate { public NPC Npc; public OfficerDef Def; public OfficerCheck Check; }

		private List<Candidate> Candidates(string me)
		{
			var list = new List<Candidate>();
			var player = Main.LocalPlayer;
			foreach (NPC n in SettlementSystem.Instance.TownNpcsOf(_te))
			{
				if (!OfficerRegistry.TryGet(n.type, out OfficerDef def) || OfficerSystem.Instance.IsEnrolled(n)) continue;
				list.Add(new Candidate { Npc = n, Def = def, Check = OfficerSystem.Instance.CanCommission(player, _te, n) });
			}
			return list;
		}

		/// <summary>Rebuilds the rows when the data they show changed (the signature covers names, slots, unpaid days, button states and the treasury threshold of each fee).</summary>
		private void Rebuild()
		{
			var player = Main.LocalPlayer;
			string me = player.name;
			var sys = OfficerSystem.Instance;
			int max = LevyConstants.Officer.MaxSlots;
			var slotNpcs = new NPC[max];
			var sb = new StringBuilder();
			sb.Append(SettlementSystem.Instance.TownName(_te)).Append('|').Append(sys.UsedSlots(me));
			for (int s = 0; s < max; s++)
			{
				slotNpcs[s] = sys.InSlot(me, s);
				var g = OfficerSystem.Data(slotNpcs[s]);
				sb.Append('|').Append(slotNpcs[s] == null ? "-" : slotNpcs[s].whoAmI + ":" + slotNpcs[s].GivenName + ":" + g?.DaysUnpaid);
			}
			var cands = Candidates(me);
			foreach (var c in cands) sb.Append('|').Append(c.Npc.whoAmI).Append(':').Append((int)c.Check).Append(':').Append(c.Npc.GivenName);
			string sig = sb.ToString();
			if (sig == _sig) return;
			_sig = sig;

			foreach (var el in _dyn) el.Remove();
			_dyn.Clear();
			_title.SetText(LevyUtil.T("Officers.UI.Title", SettlementSystem.Instance.TownName(_te)));
			_sub.SetText(LevyUtil.T("Officers.UI.Sub", sys.UsedSlots(me), max));

			float y = SlotTop;
			for (int s = 0; s < max; s++)
			{
				NPC n = slotNpcs[s];
				if (n == null)
				{
					var empty = PutDyn(new Label(LevyUtil.T("Officers.UI.SlotEmpty", s + 1), TextScale), 12, y + 10);
					empty.TextColor = Dim;
				}
				else
				{
					var g = OfficerSystem.Data(n);
					OfficerRegistry.TryGet(n.type, out OfficerDef def);
					string role = def != null ? LevyUtil.T("Officers.Kit." + def.Key + ".Name") : n.TypeName;
					string line = LevyUtil.T("Officers.UI.SlotLine", s + 1, OfficerSystem.NameOf(n), role);
					if (g != null && g.DaysUnpaid > 0) line += LevyUtil.T("Officers.UI.Unpaid", g.DaysUnpaid);
					var lab = PutDyn(new Label(line, TextScale), 12, y + 10);
					lab.TextColor = g != null && g.DaysUnpaid > 0 ? new Color(255, 150, 120) : Gold;
					NPC captured = n;
					PutDyn(new LevyButton(LevyUtil.T("Officers.UI.Dismiss"), 0.7f, 96, LevyConstants.UI.BarracksButtonH, () => { UIActions.OfficerDismiss(captured); _refreshTimer = 0; }, LevyUtil.T("Officers.UI.DismissHint", OfficerSystem.NameOf(n))), W - 12 - 96, y + 8);
				}
				y += SlotRowH;
			}

			y += HeaderGap - 6;
			var hdr = PutDyn(new Label(LevyUtil.T("Officers.UI.Available"), 0.85f), 12, y);
			hdr.TextColor = Gold;
			y += 26;

			if (cands.Count == 0)
			{
				var none = PutDyn(new Label(LevyUtil.T("Officers.UI.NoneAvailable"), 0.7f), 12, y);
				none.TextColor = Dim;
				y += 26;
			}
			foreach (var c in cands)
			{
				y = AddCandidate(c, y);
			}
			var hint = PutDyn(new Label(LevyUtil.T("Officers.UI.Hint"), 0.62f), 12, y + 2);
			hint.TextColor = Dim;
			y += 24;

			_h = y + 8;
			_panel.Height.Set(_h, 0f);
			Recalculate();
		}

		private float AddCandidate(Candidate c, float y)
		{
			var sys = OfficerSystem.Instance;
			string name = OfficerSystem.NameOf(c.Npc);
			string role = LevyUtil.T("Officers.Kit." + c.Def.Key + ".Name");
			var head = PutDyn(new Label(name + ", " + role, TextScale), 12, y);
			head.TextColor = Color.White;

			long fee = sys.Fee(c.Def), wage = sys.Wage(c.Def);
			bool ok = c.Check == OfficerCheck.Ok;
			string hintText = ok ? LevyUtil.T("Officers.UI.CommissionHint", LevyUtil.Coins(fee), LevyUtil.Coins(wage)) : LevyUtil.T("Officers.Fail." + c.Check);
			NPC captured = c.Npc; var te = _te;
			PutDyn(new LevyButton(LevyUtil.T("Officers.UI.Commission"), 0.7f, 110, LevyConstants.UI.BarracksButtonH, () => { UIActions.OfficerCommission(te, captured); _refreshTimer = 0; }, hintText) { Enabled = ok }, W - 12 - 110, y - 2);

			float lineY = y + 20;
			string desc = LevyUtil.T("Officers.Kit." + c.Def.Key + ".Desc", c.Def.Kit?.DescriptionArgs ?? Array.Empty<object>()).Replace("\r", "").Replace("\n", " ");
			foreach (string line in Wrap(desc, DescScale, W - 24 - 8))
			{
				var l = PutDyn(new Label(line, DescScale), 16, lineY);
				l.TextColor = Dim;
				lineY += 15;
			}
			var cost = PutDyn(new Label(LevyUtil.T("Officers.UI.Cost", LevyUtil.Coins(fee), LevyUtil.Coins(wage)), DescScale), 16, lineY + 1);
			cost.TextColor = ok || c.Check == OfficerCheck.NoMoney ? Gold : Dim;
			return lineY + 24;
		}

		private static IEnumerable<string> Wrap(string text, float scale, float maxW)
		{
			var font = FontAssets.MouseText.Value;
			string[] lines = Utils.WordwrapString(text, font, (int)(maxW / scale), DescMaxLines, out int count);
			for (int i = 0; i <= count && i < lines.Length; i++) if (!string.IsNullOrEmpty(lines[i])) yield return lines[i];
		}
	}
}
