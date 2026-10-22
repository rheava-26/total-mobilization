using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
	/// The officer menu, opened from the Barracks panel and docked beside it (like the soldier loadout panel). It is a large, focal panel: a row of three
	/// command-slot cards up top — each showing the serving officer's head, name, kit, wage and any unpaid-days warning with a Dismiss button — and below them a
	/// clear list of the town NPCs housed in this settlement who can be commissioned, each as a card with the officer's head, kit description, fee, wage and a
	/// Commission button that is greyed with the reason when it cannot be done. All actions go through <see cref="UIActions"/> (server-authoritative; the panel only
	/// reads the synced NPC data, so it can never create money or slots). The rows are rebuilt only when anything they show changes, checked a few times a second.
	/// </summary>
	public class OfficerPanel : UIState
	{
		private const int W = 500;
		private const int Pad = 16;                 // panel side margin
		private const int Portrait = 46;            // officer head icon size
		private const int SlotCardH = 66, CardGap = 8, SectionGap = 14;
		private const float TitleScale = 1.1f, SubScale = 0.7f, HeaderScale = 0.85f, NameScale = 0.88f, RoleScale = 0.68f, DescScale = 0.64f, SmallScale = 0.64f;
		private const int DescMaxLines = 5;
		private const int RefreshTicks = 12;
		private static readonly Color Gold = new(255, 215, 80), Dim = new(170, 175, 195), Faint = new(120, 127, 152), Unpaid = new(255, 150, 120);

		private DraggablePanel _panel;
		private Label _title, _sub;
		private LevyButton _close;
		private readonly List<UIElement> _dyn = new();
		private BarracksTE _te;
		private string _sig = "";
		private int _refreshTimer;
		private float _h = 240f;

		public BarracksTE Te => _te;

		public override void OnInitialize()
		{
			_panel = new DraggablePanel();
			_panel.SetPadding(0);
			_panel.Left.Set(LevyConstants.UI.PanelDefaultLeft + LevyConstants.UI.BarracksPanelWidth + 12, 0f); _panel.Top.Set(LevyConstants.UI.PanelDefaultTop, 0f);
			_panel.Width.Set(W, 0f); _panel.Height.Set(_h, 0f);
			_panel.BackgroundColor = new Color(33, 43, 79) * 0.9f;

			_title = Put(new Label("", TitleScale), Pad, 10);
			_close = Put(new LevyButton("X", LevyConstants.UI.ButtonTextScale, 26, 26, () => UISystem.Instance.CloseOfficers(), LevyUtil.T("UI2.Close")), W - 36, 8);
			_sub = Put(new Label("", SubScale), Pad, 40);
			_sub.TextColor = Dim;
			Append(_panel);
		}

		private T Put<T>(T el, float left, float top) where T : UIElement
		{
			el.Left.Set(left, 0f); el.Top.Set(top, 0f);
			_panel.Append(el);
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

		private List<Candidate> Candidates()
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

		/// <summary>Rebuilds the cards when the data they show changed (the signature covers names, slots, unpaid days, button states and the treasury threshold of each fee).</summary>
		private void Rebuild()
		{
			var player = Main.LocalPlayer;
			string me = player.name;
			var sys = OfficerSystem.Instance;
			int max = LevyConstants.Officer.MaxSlots;
			var slotNpcs = new NPC[max];
			var sb = new System.Text.StringBuilder();
			sb.Append(SettlementSystem.Instance.TownName(_te)).Append('|').Append(sys.UsedSlots(me));
			for (int s = 0; s < max; s++)
			{
				slotNpcs[s] = sys.InSlot(me, s);
				var g = OfficerSystem.Data(slotNpcs[s]);
				sb.Append('|').Append(slotNpcs[s] == null ? "-" : slotNpcs[s].whoAmI + ":" + slotNpcs[s].GivenName + ":" + g?.DaysUnpaid);
			}
			var cands = Candidates();
			foreach (var c in cands) sb.Append('|').Append(c.Npc.whoAmI).Append(':').Append((int)c.Check).Append(':').Append(c.Npc.GivenName);
			string sig = sb.ToString();
			if (sig == _sig) return;
			_sig = sig;

			foreach (var el in _dyn) el.Remove();
			_dyn.Clear();
			_title.SetText(LevyUtil.T("Officers.UI.Title", SettlementSystem.Instance.TownName(_te)));
			_sub.SetText(LevyUtil.T("Officers.UI.Sub", sys.UsedSlots(me), max));

			float cardW = W - 2 * Pad;
			float y = 66;
			var slotsHdr = PutDyn(new Label(LevyUtil.T("Officers.UI.SlotsHeader"), HeaderScale), Pad, y);
			slotsHdr.TextColor = Gold;
			y += 24;

			for (int s = 0; s < max; s++)
			{
				AddSlotCard(s, slotNpcs[s], Pad, y, cardW);
				y += SlotCardH + CardGap;
			}

			y += SectionGap - CardGap;
			var availHdr = PutDyn(new Label(LevyUtil.T("Officers.UI.Available"), HeaderScale), Pad, y);
			availHdr.TextColor = Gold;
			y += 26;

			if (cands.Count == 0)
			{
				var none = PutDyn(new Label(LevyUtil.T("Officers.UI.NoneAvailable"), SubScale), Pad, y);
				none.TextColor = Dim;
				y += 28;
			}
			foreach (var c in cands) y = AddCandidateCard(c, Pad, y, cardW) + CardGap;

			var hint = PutDyn(new Label(LevyUtil.T("Officers.UI.Hint"), 0.62f), Pad, y + 2);
			hint.TextColor = Faint;
			y += 26;

			_h = y + 8;
			_panel.Height.Set(_h, 0f);
			Recalculate();
		}

		// ---- cards ----------------------------------------------------------
		/// <summary>A command-slot card: the serving officer's head, name, kit, wage / unpaid warning and a Dismiss button, or an "empty" placeholder.</summary>
		private void AddSlotCard(int slot, NPC n, float x, float y, float w)
		{
			var sys = OfficerSystem.Instance;
			bool filled = n != null;
			UIPanel card = NewCard(x, y, w, SlotCardH,
				filled ? new Color(48, 60, 112) * 0.95f : new Color(28, 36, 64) * 0.8f,
				filled ? Gold * 0.55f : new Color(80, 88, 120) * 0.6f);

			if (!filled)
			{
				var lab = AddTo(card, new Label(LevyUtil.T("Officers.UI.SlotEmpty", slot + 1), 0.82f), 16, 14);
				lab.TextColor = Dim;
				var sub = AddTo(card, new Label(LevyUtil.T("Officers.UI.SlotEmptyHint"), SmallScale), 16, 36);
				sub.TextColor = Faint;
				return;
			}

			AddTo(card, new NpcHeadIcon(n, Portrait), 10, (SlotCardH - Portrait) / 2f);
			float tx = 10 + Portrait + 12;
			var g = OfficerSystem.Data(n);
			OfficerRegistry.TryGet(n.type, out OfficerDef def);
			string role = def != null ? LevyUtil.T("Officers.Kit." + def.Key + ".Name") : n.TypeName;

			var name = AddTo(card, new Label(OfficerSystem.NameOf(n), NameScale), tx, 8);
			name.TextColor = Gold;
			var roleLab = AddTo(card, new Label(LevyUtil.T("Officers.UI.SlotRole", slot + 1, role), RoleScale), tx, 30);
			roleLab.TextColor = Dim;

			Label sub2;
			if (g != null && g.DaysUnpaid > 0)
			{
				sub2 = AddTo(card, new Label(LevyUtil.T("Officers.UI.UnpaidWarn", g.DaysUnpaid), SmallScale), tx, 47);
				sub2.TextColor = Unpaid;
			}
			else
			{
				sub2 = AddTo(card, new Label(LevyUtil.T("Officers.UI.SlotWage", LevyUtil.Coins(def != null ? sys.Wage(def) : 0)), SmallScale), tx, 47);
				sub2.TextColor = Faint;
			}

			NPC captured = n;
			AddTo(card, new LevyButton(LevyUtil.T("Officers.UI.Dismiss"), 0.72f, 92, 26, () => { UIActions.OfficerDismiss(captured); _refreshTimer = 0; }, LevyUtil.T("Officers.UI.DismissHint", OfficerSystem.NameOf(n))), w - 12 - 92, (SlotCardH - 26) / 2f);
		}

		/// <summary>A commissionable-townsfolk card: head, name and kit, the wrapped kit description, the fee / wage, and a Commission button (greyed with the reason when unavailable). Returns the card's bottom y.</summary>
		private float AddCandidateCard(Candidate c, float x, float y, float w)
		{
			var sys = OfficerSystem.Instance;
			string name = OfficerSystem.NameOf(c.Npc);
			string role = LevyUtil.T("Officers.Kit." + c.Def.Key + ".Name");
			bool ok = c.Check == OfficerCheck.Ok;
			long fee = sys.Fee(c.Def), wage = sys.Wage(c.Def);

			float tx = 10 + Portrait + 12;
			string desc = LevyUtil.T("Officers.Kit." + c.Def.Key + ".Desc", c.Def.Kit?.DescriptionArgs ?? Array.Empty<object>()).Replace("\r", "").Replace("\n", " ");
			var lines = new List<string>(Wrap(desc, DescScale, w - tx - 14));
			float h = 30 + lines.Count * 15 + 20 + 8; // name row + desc lines + cost row + padding

			UIPanel card = NewCard(x, y, w, h,
				new Color(42, 54, 100) * 0.92f,
				ok ? new Color(120, 200, 130) * 0.6f : new Color(96, 104, 142) * 0.6f);

			AddTo(card, new NpcHeadIcon(c.Npc, Portrait), 10, 10);
			var head = AddTo(card, new Label(LevyUtil.T("Officers.UI.CandidateHead", name, role), NameScale), tx, 9);
			head.TextColor = Color.White;

			float ly = 32;
			foreach (string line in lines)
			{
				var l = AddTo(card, new Label(line, DescScale), tx, ly);
				l.TextColor = Dim;
				ly += 15;
			}
			var cost = AddTo(card, new Label(LevyUtil.T("Officers.UI.Cost", LevyUtil.Coins(fee), LevyUtil.Coins(wage)), SmallScale), tx, ly + 2);
			cost.TextColor = ok || c.Check == OfficerCheck.NoMoney ? Gold : Faint;

			string hintText = ok ? LevyUtil.T("Officers.UI.CommissionHint", LevyUtil.Coins(fee), LevyUtil.Coins(wage)) : LevyUtil.T("Officers.Fail." + c.Check);
			NPC captured = c.Npc; var te = _te;
			AddTo(card, new LevyButton(LevyUtil.T("Officers.UI.Commission"), 0.72f, 112, 28, () => { UIActions.OfficerCommission(te, captured); _refreshTimer = 0; }, hintText) { Enabled = ok }, w - 12 - 112, 12);
			return y + h;
		}

		// ---- building blocks ------------------------------------------------
		private UIPanel NewCard(float x, float y, float w, float h, Color bg, Color border)
		{
			var card = new UIPanel();
			card.SetPadding(0);
			card.Left.Set(x, 0f); card.Top.Set(y, 0f); card.Width.Set(w, 0f); card.Height.Set(h, 0f);
			card.BackgroundColor = bg; card.BorderColor = border;
			_panel.Append(card); _dyn.Add(card);
			return card;
		}

		private static T AddTo<T>(UIElement parent, T el, float left, float top) where T : UIElement
		{
			el.Left.Set(left, 0f); el.Top.Set(top, 0f);
			parent.Append(el);
			return el;
		}

		private T PutDyn<T>(T el, float left, float top) where T : UIElement
		{
			el.Left.Set(left, 0f); el.Top.Set(top, 0f);
			_panel.Append(el); _dyn.Add(el);
			return el;
		}

		private static IEnumerable<string> Wrap(string text, float scale, float maxW)
		{
			var font = FontAssets.MouseText.Value;
			string[] lines = Utils.WordwrapString(text, font, (int)(maxW / scale), DescMaxLines, out int count);
			for (int i = 0; i <= count && i < lines.Length; i++) if (!string.IsNullOrEmpty(lines[i])) yield return lines[i];
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
