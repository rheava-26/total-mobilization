using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI;
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
	/// The item panel beside the armory: the weapon last pointed at, its stats, tier and stage, its rank term by term, and the hand edits for this armory: a rank override (a number, never use, always prefer)
	/// and its class and category tags. The text is redrawn from the live numbers a few times a second; the edits go through <see cref="ArmoryPanel.RulesEdited"/>.
	/// </summary>
	public class ItemInfoPanel : UIPanel
	{
		public const int PanelWidth = 256, PanelHeight = 462;
		private const int TextTop = 10, LineH = 16, ControlsTop = 262;

		private readonly ArmoryPanel _owner;
		private readonly TextBody _body = new();
		private readonly LevyButton _rankMode, _addCat, _reset, _close, _prefer, _preferSet;
		private readonly Label _gearNote;
		private bool _gearShown;
		private readonly NumberField _rankValue;
		private readonly DropButton _classDrop;
		private readonly Label _rankLabel, _classLabel, _catLabel, _note;
		private readonly ChoicePopup _popup = new();
		private readonly List<UIElement> _chips = new();
		private Item _item;
		private int _tick;
		private string _catSig = "";

		private ArmoryRules Rules => _owner.Te?.Rules;

		public int Type => _item?.type ?? 0;

		public ItemInfoPanel(ArmoryPanel owner)
		{
			_owner = owner;
			SetPadding(0);
			Width.Set(PanelWidth, 0f); Height.Set(PanelHeight, 0f);
			BackgroundColor = new Color(33, 43, 79) * 0.94f;

			_body.Left.Set(10, 0f); _body.Top.Set(TextTop, 0f); _body.Width.Set(PanelWidth - 20, 0f); _body.Height.Set(ControlsTop - TextTop - 6, 0f);
			Append(_body);

			_close = new LevyButton("X", LevyConstants.UI.ButtonTextScale, 24, 22, () => owner.CloseInfo());
			_close.Left.Set(PanelWidth - 32, 0f); _close.Top.Set(6, 0f);
			Append(_close);

			float y = ControlsTop;
			_rankLabel = Lbl(LevyUtil.T("Rules.InfoRankSet"), 10, y + 4);
			_rankMode = new LevyButton("", LevyConstants.UI.ButtonTextScale, 96, 24, CycleRankMode, LevyUtil.T("Rules.RankModeHint"));
			_rankMode.Left.Set(64, 0f); _rankMode.Top.Set(y, 0f); Append(_rankMode);
			_rankValue = new NumberField(100, 1, 100000000, 10, false, () => Ov(false)?.Value ?? 0f, v => { var o = Ov(true); if (o != null) { o.Value = v; Changed(); } });
			_rankValue.Left.Set(PanelWidth - 108, 0f); _rankValue.Top.Set(y, 0f);

			y += 32;
			_classLabel = Lbl(LevyUtil.T("Rules.InfoClass"), 10, y + 4);
			_classDrop = new DropButton(120, ClassText, d => OpenClassList(d), LevyUtil.T("Rules.ClassEditHint"));
			_classDrop.Left.Set(64, 0f); _classDrop.Top.Set(y, 0f); Append(_classDrop);

			y += 32;
			_catLabel = Lbl(LevyUtil.T("Rules.InfoCategories"), 10, y);
			_addCat = new LevyButton(LevyUtil.T("Rules.AddCategoryShort"), LevyConstants.UI.ButtonTextScale, 100, 24, OpenAddCategory, LevyUtil.T("Rules.AddCategoryTagHint"));
			_addCat.Left.Set(10, 0f); _addCat.Top.Set(ControlsTop + 146, 0f); Append(_addCat);
			_reset = new LevyButton(LevyUtil.T("Rules.ResetItem"), LevyConstants.UI.ButtonTextScale, 100, 24, ResetItem, LevyUtil.T("Rules.ResetItemHint"));
			_reset.Left.Set(PanelWidth - 110, 0f); _reset.Top.Set(ControlsTop + 146, 0f); Append(_reset);
			_note = Lbl(LevyUtil.T("Rules.ItemNote"), 10, ControlsTop + 178);

			// armor and accessories: one switch for the piece and one for its whole set (swapped in for the weapon controls while such an item is shown)
			_prefer = new LevyButton("", LevyConstants.UI.ButtonTextScale, 110, 24, TogglePrefer, LevyUtil.T("Rules.GearPreferHint"));
			_prefer.Left.Set(10, 0f); _prefer.Top.Set(ControlsTop, 0f);
			_preferSet = new LevyButton("", LevyConstants.UI.ButtonTextScale, 110, 24, TogglePreferSet, LevyUtil.T("Rules.GearPreferSetHint"));
			_preferSet.Left.Set(PanelWidth - 120, 0f); _preferSet.Top.Set(ControlsTop, 0f);
			_gearNote = new Label(LevyUtil.T("Rules.GearNote"), 0.8f);
			_gearNote.Left.Set(10, 0f); _gearNote.Top.Set(ControlsTop + 36, 0f);
		}

		/// <summary>True for an armor piece or accessory the armory can issue (and not a weapon).</summary>
		internal static bool IsGear(Item it)
		{
			if (it == null || it.IsAir || it.vanity) return false;
			if (WeaponClassifier.GetProfile(it).Supported) return false;
			return WeaponClassifier.IsArmorPiece(it, 0) || WeaponClassifier.IsArmorPiece(it, 1) || WeaponClassifier.IsArmorPiece(it, 2) || (it.accessory && WeaponClassifier.IsUsefulAccessory(it));
		}

		private bool IsArmor => _item != null && (WeaponClassifier.IsArmorPiece(_item, 0) || WeaponClassifier.IsArmorPiece(_item, 1) || WeaponClassifier.IsArmorPiece(_item, 2));
		private bool GearPreferred => Ov(false)?.Rank == RankOverrideMode.Prefer;

		private void TogglePrefer()
		{
			var o = Ov(true); if (o == null) return;
			o.Rank = o.Rank == RankOverrideMode.Prefer ? RankOverrideMode.Auto : RankOverrideMode.Prefer;
			Changed();
		}

		/// <summary>Marks (or clears) every armor piece of this item's set: all pieces that share its name stem.</summary>
		private void TogglePreferSet()
		{
			var r = Rules; if (r == null || _item == null || !IsArmor) return;
			bool on = !GearPreferred;
			string stem = ArmoryTE.StemOf(_item);
			foreach (var kv in ContentSamples.ItemsByType)
			{
				var s = kv.Value;
				if (s.vanity || !(WeaponClassifier.IsArmorPiece(s, 0) || WeaponClassifier.IsArmorPiece(s, 1) || WeaponClassifier.IsArmorPiece(s, 2))) continue;
				if (ArmoryTE.StemOf(s) != stem) continue;
				var o = r.OverrideFor(s.type, on);
				if (o == null) continue;
				o.Rank = on ? RankOverrideMode.Prefer : (o.Rank == RankOverrideMode.Prefer ? RankOverrideMode.Auto : o.Rank);
			}
			Changed();
		}

		/// <summary>Swaps the weapon controls for the gear ones (or back) when the shown item changes kind.</summary>
		private void SyncGearMode()
		{
			bool gear = IsGear(_item);
			if (gear == _gearShown) return;
			_gearShown = gear;
			UIElement[] weaponOnly = { _rankLabel, _rankMode, _classLabel, _classDrop, _catLabel, _addCat, _reset, _note };
			UIElement[] gearOnly = { _prefer, _preferSet, _gearNote };
			foreach (var e in weaponOnly) { if (gear) { if (HasChild(e)) RemoveChild(e); } else if (!HasChild(e)) Append(e); }
			foreach (var e in gearOnly) { if (!gear) { if (HasChild(e)) RemoveChild(e); } else if (!HasChild(e)) Append(e); }
			if (gear) { if (HasChild(_rankValue)) RemoveChild(_rankValue); foreach (var c in _chips) c.Remove(); _chips.Clear(); _catSig = ""; }
		}

		private Label Lbl(string text, float x, float y)
		{
			var l = new Label(text, 0.8f);
			l.Left.Set(x, 0f); l.Top.Set(y, 0f);
			Append(l);
			return l;
		}

		// ------------------------------------------------------------------ showing an item

		/// <summary>Points the panel at an item (a copy is kept, so it stays when the item is picked up).</summary>
		public void Show(Item it)
		{
			if (it == null || it.IsAir) return;
			if (_item != null && _item.type == it.type && _item.prefix == it.prefix) return;
			_item = it.Clone();
			_tick = 0; _catSig = "";
			if (_popup.Open) _popup.Remove();
		}

		private ItemOverride Ov(bool create) => _item == null ? null : Rules?.OverrideFor(_item.type, create);

		private void Changed()
		{
			Rules?.Prune();
			_owner.RulesEdited();
			_tick = 0;
		}

		// ------------------------------------------------------------------ the edits

		private void CycleRankMode()
		{
			var o = Ov(true); if (o == null) return;
			o.Rank = (RankOverrideMode)(((int)o.Rank + 1) % 4);
			if (o.Rank == RankOverrideMode.Set && o.Value <= 0f) o.Value = Math.Max(1f, (float)Math.Round(WeaponRank.Of(_item)));
			Changed();
		}

		private string ClassText()
		{
			var o = Ov(false);
			if (o?.Class == null) return LevyUtil.T("Rules.ClassAuto");
			return o.Class == "none" ? LevyUtil.T("Rules.ClassNone") : WeaponTags.DisplayName(o.Class);
		}

		private void OpenClassList(UIElement anchor)
		{
			var cur = Ov(false)?.Class;
			var l = new List<(string, Action, bool)>
			{
				(LevyUtil.T("Rules.ClassAuto"), () => SetClass(null), cur == null),
				(LevyUtil.T("Rules.ClassNone"), () => SetClass("none"), cur == "none"),
			};
			foreach (var c in WeaponTags.ClassKeys) { string id = c; l.Add((WeaponTags.DisplayName(id), () => SetClass(id), cur == id)); }
			Choose(anchor, l);
		}

		private void SetClass(string cls)
		{
			var o = Ov(true); if (o == null) return;
			o.Class = cls;
			Changed();
		}

		private void OpenAddCategory()
		{
			var cats = Rules?.CatsOf(_item) ?? Array.Empty<string>();
			var l = new List<(string, Action, bool)>();
			foreach (var c in WeaponTags.Categories)
			{
				string id = c;
				if (ArmoryRules.Has(cats, id)) continue;
				l.Add((WeaponTags.DisplayName(id, true), () => AddCat(id), false));
			}
			if (l.Count > 0) Choose(_addCat, l);
		}

		private void AddCat(string id)
		{
			var o = Ov(true); if (o == null) return;
			o.CatRemove.Remove(id);
			if (WeaponTags.DerivedCategory(_item) != id) o.CatAdd.Add(id);
			Changed();
		}

		private void RemoveCat(string id)
		{
			var o = Ov(true); if (o == null) return;
			if (!o.CatAdd.Remove(id)) o.CatRemove.Add(id);
			Changed();
		}

		private void ResetItem()
		{
			var r = Rules; if (r == null || _item == null) return;
			string k = ArmoryRules.KeyOf(_item.type);
			if (k != null) r.Overrides.Remove(k);
			Changed();
		}

		private void Choose(UIElement anchor, List<(string, Action, bool)> options)
		{
			var a = anchor.GetDimensions(); var me = GetDimensions();
			var items = new List<(string label, Action pick, bool on)>();
			foreach (var (label, pick, on) in options) items.Add((label, pick, on));
			_popup.Show(this, a.X - me.X, a.Y - me.Y + a.Height + 2f, Math.Max(a.Width, 110f), items);
		}

		// ------------------------------------------------------------------ text

		private static string BossName(float tier)
		{
			int t = (int)Math.Round(tier);
			if (Math.Abs(tier - t) < 0.01f && t >= 0 && t <= 18)
			{
				string k = "Rules.Boss_" + t;
				return LevyUtil.T(k);
			}
			return LevyUtil.T("Rules.BossOther", tier.ToString("0.#"));
		}

		private void Compose()
		{
			var r = Rules; var it = _item;
			if (r == null || it == null) return;
			var lines = new List<(string, Color)>();
			using var _rs = ArmoryRules.Enter(r);
			var white = Color.White; var dim = new Color(175, 190, 225); var warn = new Color(255, 214, 110);
			Color rare = it.master ? new Color(255, 170, 0) : it.expert ? Main.DiscoColor : ItemRarity.GetColor(it.rare);
			lines.Add((it.Name, rare));
			var p = WeaponClassifier.GetProfile(it);
			string cat = r.CatsOf(it).Length > 0 ? string.Join(", ", Array.ConvertAll(r.CatsOf(it), c => WeaponTags.DisplayName(c))) : LevyUtil.T("Rules.NoCategory");
			string cls = r.ClassKeyOf(it);
			lines.Add((LevyUtil.T("Rules.InfoKind", cat, cls == null ? LevyUtil.T("Rules.ClassNone") : WeaponTags.DisplayName(cls)), dim));
			if (IsGear(it))
			{
				lines.Clear();
				lines.Add((it.Name, rare));
				lines.Add((LevyUtil.T(IsArmor ? "Rules.GearKindArmor" : "Rules.GearKindAccessory", it.defense), dim));
				lines.Add((LevyUtil.T(GearPreferred ? "Rules.GearPreferred" : "Rules.GearNotPreferred"), GearPreferred ? warn : white));
				_body.Lines = lines;
				return;
			}
			if (!p.Supported) { lines.Add((LevyUtil.T("Rules.InfoUnsupported"), warn)); _body.Lines = lines; return; }
			lines.Add((LevyUtil.T("Rules.InfoStats", it.damage, it.useTime, it.crit + 4), white));
			if (it.mana > 0) lines.Add((LevyUtil.T("Rules.InfoMana", it.mana), white));
			else if (it.useAmmo > 0) lines.Add((LevyUtil.T("Rules.InfoAmmo"), white));
			float tier = ProgressionTier.Of(it.type), progress = WeaponRank.Progress();
			if (tier < 0f) lines.Add((LevyUtil.T("Rules.InfoTierNone"), dim));
			else if (progress >= tier) lines.Add((LevyUtil.T("Rules.InfoTierPast", BossName(tier), (progress - tier).ToString("0.#")), dim));
			else lines.Add((LevyUtil.T("Rules.InfoTierAhead", BossName(tier)), dim));
			lines.Add(("", white));
			var b = WeaponRank.Explain(it);
			if (b.Excluded && b.Override != RankOverrideMode.Never) lines.Add((LevyUtil.T("Rules.InfoBarred"), warn));
			string rankText = b.Excluded ? LevyUtil.T("Rules.RankNever") : b.Override == RankOverrideMode.Prefer ? LevyUtil.T("Rules.RankPrefer") : b.Final.ToString("0.#");
			lines.Add((LevyUtil.T("Rules.InfoRank", rankText), white));
			string baseWhat = b.Mode == WeaponRankMode.Damage ? LevyUtil.T("Rules.BaseDamage") : LevyUtil.T("Rules.BasePrice", LevyUtil.Coins(Math.Max(0, it.value)));
			lines.Add((LevyUtil.T("Rules.BreakBase", b.Base.ToString("0.#"), baseWhat), dim));
			if (b.Mode == WeaponRankMode.Balanced)
			{
				lines.Add((LevyUtil.T("Rules.BreakStage", b.Stage.ToString("0.##")), b.Stage < 1f ? warn : dim));
				lines.Add((LevyUtil.T("Rules.BreakRecency", b.Recency.ToString("0.##")), dim));
				lines.Add((LevyUtil.T("Rules.BreakFound", b.Found.ToString("0.##")), dim));
			}
			lines.Add((b.Rules != 1f ? LevyUtil.T("Rules.BreakRules", b.Rules.ToString("0.##"), b.RuleNote) : LevyUtil.T("Rules.BreakRulesNone"), dim));
			if (b.Negligible) lines.Add((LevyUtil.T("Rules.BreakArmored"), warn));
			if (b.Override == RankOverrideMode.Set) lines.Add((LevyUtil.T("Rules.BreakSet", b.OverrideValue.ToString("0.#")), warn));
			else if (b.Override == RankOverrideMode.Never) lines.Add((LevyUtil.T("Rules.BreakNever"), warn));
			else if (b.Override == RankOverrideMode.Prefer) lines.Add((LevyUtil.T("Rules.BreakPrefer"), warn));
			_body.Lines = lines;
		}

		/// <summary>Rebuilds the category chips when the item's tags changed.</summary>
		private void SyncChips()
		{
			var r = Rules; if (r == null || _item == null || _gearShown) return;
			var cats = r.CatsOf(_item);
			string sig = _item.type + ":" + string.Join(",", cats);
			if (sig == _catSig) return;
			_catSig = sig;
			foreach (var c in _chips) c.Remove();
			_chips.Clear();
			for (int i = 0; i < cats.Length && i < 4; i++)
			{
				string id = cats[i];
				var chip = new LevyButton(WeaponTags.DisplayName(id) + "  X", LevyConstants.UI.ButtonTextScale, 108, 22, () => RemoveCat(id), LevyUtil.T("Rules.RemoveCategoryHint"));
				chip.Left.Set(10 + (i % 2) * 114, 0f); chip.Top.Set(ControlsTop + 90 + (i / 2) * 26, 0f);
				Append(chip); _chips.Add(chip);
			}
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true;
			SyncGearMode();
			var o = Ov(false);
			var mode = o?.Rank ?? RankOverrideMode.Auto;
			if (_gearShown)
			{
				_prefer.Set(LevyUtil.T(GearPreferred ? "Rules.GearPreferOn" : "Rules.GearPreferOff"), true);
				_prefer.Lit = GearPreferred;
				_preferSet.Set(LevyUtil.T(GearPreferred ? "Rules.GearPreferSetClear" : "Rules.GearPreferSet"), true);
				_preferSet.Enabled = IsArmor;
				if (--_tick <= 0) { _tick = 15; Compose(); }
				return;
			}
			_rankMode.Set(LevyUtil.T("Rules.RankMode_" + mode), true);
			_rankMode.Lit = mode != RankOverrideMode.Auto;
			bool showValue = mode == RankOverrideMode.Set;
			if (showValue && !HasChild(_rankValue)) Append(_rankValue);
			else if (!showValue && HasChild(_rankValue)) RemoveChild(_rankValue);
			_reset.Enabled = o != null && !o.IsEmpty;
			_addCat.Enabled = _item != null;
			if (--_tick <= 0) { _tick = 15; Compose(); }
			SyncChips();
		}

		/// <summary>Plain text lines drawn inside the panel.</summary>
		private sealed class TextBody : UIElement
		{
			public List<(string text, Color color)> Lines = new();

			public TextBody() { IgnoresMouseInteraction = true; }

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				var d = GetDimensions();
				float y = d.Y;
				for (int i = 0; i < Lines.Count; i++)
				{
					var (text, color) = Lines[i];
					if (text.Length > 0) Utils.DrawBorderString(spriteBatch, text, new Vector2(d.X, y), i == 0 ? color : color, i == 0 ? 0.95f : 0.76f);
					y += i == 0 ? LineH + 3 : LineH;
				}
			}
		}
	}
}
