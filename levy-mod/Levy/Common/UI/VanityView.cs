using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Common.Vanity;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// The Vanity screen of the armory panel: one row per vanity set, top to bottom. Each row has head, body and legs vanity slots, a dye slot for each, and who wears it (everyone, a class, or named levies).
	/// A new set adds a row. The items stay in the row and dress every levy it targets; nothing is stats. Every edit goes through <see cref="Edited"/>, which the armory panel turns into a (debounced) packet to the server.
	/// </summary>
	public class VanityView : UIElement
	{
		private const int CardWidth = 640, CardHeight = 104, Pad = 6;
		private static readonly string[] SlotKeys = { "Head", "Body", "Legs", "DyeHead", "DyeBody", "DyeLegs" };

		private readonly ArmoryPanel _owner;
		private readonly UIList _list = new();
		private readonly UIScrollbar _bar = new();
		private readonly ChoicePopup _popup = new();
		private readonly Label _empty;
		private readonly LevyButton _add;

		private ArmoryVanity Vanity => _owner.Te?.Vanity;
		private ArmoryTE Te => _owner.Te;

		public VanityView(ArmoryPanel owner)
		{
			_owner = owner;
			Width.Set(LevyConstants.UI.ArmoryPanelWidth - 20, 0f); Height.Set(LevyConstants.UI.ArmoryBarTop - 36 - 4, 0f);
			_add = new LevyButton(LevyUtil.T("Vanity.AddSet"), LevyConstants.UI.ButtonTextScale, 150, 24, AddSet, LevyUtil.T("Vanity.AddSetHint"));
			Append(_add);
			var note = new Label(LevyUtil.T("Vanity.Note"), 0.72f);
			note.Left.Set(160, 0f); note.Top.Set(5, 0f);
			Append(note);

			_list.Top.Set(34f, 0f); _list.Width.Set(CardWidth + 4, 0f); _list.Height.Set(Height.Pixels - 34f, 0f); _list.ListPadding = 6f;
			_bar.Left.Set(CardWidth + 10, 0f); _bar.Top.Set(34f, 0f); _bar.Height.Set(Height.Pixels - 34f, 0f);
			_list.SetScrollbar(_bar);
			Append(_list); Append(_bar);
			_empty = new Label(LevyUtil.T("Vanity.Empty"), 0.9f);
			_empty.Left.Set(8, 0f); _empty.Top.Set(48, 0f);
		}

		// ------------------------------------------------------------------ edits

		private void Edited() => _owner.VanityEdited();

		private void Reshape()
		{
			Edited();
			float pos = _bar.ViewPosition;
			Build();
			_bar.ViewPosition = pos;
		}

		private void AddSet()
		{
			var v = Vanity; if (v == null) return;
			var s = v.AddSet();
			if (s == null) return;
			SoundEngine.PlaySound(SoundID.MenuTick);
			Reshape();
			_bar.ViewPosition = float.MaxValue;
		}

		// ------------------------------------------------------------------ building

		/// <summary>Rebuilds every row from the armory's vanity list.</summary>
		public void Build()
		{
			_list.Clear();
			if (_popup.Open) _popup.Remove();
			var v = Vanity;
			if (v == null) return;
			if (v.Sets.Count == 0) { if (!HasChild(_empty)) Append(_empty); }
			else if (HasChild(_empty)) RemoveChild(_empty);
			for (int i = 0; i < v.Sets.Count; i++) _list.Add(BuildCard(v, i));
			_add.Enabled = v.Sets.Count < ArmoryVanity.MaxSets;
			_list.Recalculate();
		}

		private UIElement BuildCard(ArmoryVanity vanity, int index)
		{
			var set = vanity.Sets[index];
			var card = new UIPanel();
			card.SetPadding(0);
			card.Width.Set(CardWidth, 0f); card.Height.Set(CardHeight, 0f);
			card.BackgroundColor = set.On ? new Color(44, 58, 108) * 0.92f : new Color(30, 34, 52) * 0.9f;

			var on = new LevyButton(LevyUtil.T(set.On ? "Rules.On" : "Rules.Off"), LevyConstants.UI.ButtonTextScale, 44, 22, () => { set.On = !set.On; Reshape(); }, LevyUtil.T("Vanity.OnHint")) { Lit = set.On };
			on.Left.Set(Pad, 0f); on.Top.Set(Pad, 0f); card.Append(on);
			var title = new Label(LevyUtil.T("Vanity.SetTitle", index + 1), 0.9f);
			title.Left.Set(Pad + 54, 0f); title.Top.Set(Pad + 3, 0f); card.Append(title);

			var target = new DropButton(150, () => TargetText(set), d => Choose(d, TargetChoices(set)), LevyUtil.T("Vanity.TargetHint"));
			target.Left.Set(150, 0f); target.Top.Set(Pad - 1, 0f); card.Append(target);
			if (set.Target == VanityTargetKind.Class)
			{
				var cls = new DropButton(130, () => set.ClassKey == null ? LevyUtil.T("Reforge.Any") : WeaponTags.DisplayName(set.ClassKey), d => Choose(d, ClassChoices(set)), LevyUtil.T("Vanity.ClassHint"));
				cls.Left.Set(308, 0f); cls.Top.Set(Pad - 1, 0f); card.Append(cls);
			}
			else if (set.Target == VanityTargetKind.Levies)
			{
				var who = new LevyButton(LevyUtil.T("Vanity.ChooseLevies"), LevyConstants.UI.ButtonTextScale, 150, 24, () => ChooseLevies(set, card), LevyUtil.T("Vanity.LeviesHint"));
				who.Left.Set(308, 0f); who.Top.Set(Pad - 1, 0f); card.Append(who);
			}

			var rm = new LevyButton("X", LevyConstants.UI.ButtonTextScale, 24, 22, () => { vanity.Remove(index); Reshape(); }, LevyUtil.T("Vanity.RemoveHint"));
			rm.Left.Set(CardWidth - 24 - Pad, 0f); rm.Top.Set(Pad, 0f); card.Append(rm);
			var dn = new LevyButton("v", LevyConstants.UI.ButtonTextScale, 24, 22, () => { vanity.Move(index, 1); Reshape(); }, LevyUtil.T("Vanity.DownHint")) { Enabled = index < vanity.Sets.Count - 1 };
			dn.Left.Set(CardWidth - 52 - Pad, 0f); dn.Top.Set(Pad, 0f); card.Append(dn);
			var up = new LevyButton("^", LevyConstants.UI.ButtonTextScale, 24, 22, () => { vanity.Move(index, -1); Reshape(); }, LevyUtil.T("Vanity.UpHint")) { Enabled = index > 0 };
			up.Left.Set(CardWidth - 80 - Pad, 0f); up.Top.Set(Pad, 0f); card.Append(up);

			// head / body / legs vanity, then a dye for each
			const float SlotScale = 0.75f, SlotX0 = Pad, SlotPitch = 58, LabelY = 36, SlotY = 50;
			for (int i = 0; i < VanitySet.Slots; i++)
			{
				int slotIndex = i;
				float x = SlotX0 + i * SlotPitch + (i >= 3 ? 24 : 0);
				var label = new Label(LevyUtil.T("Vanity.Slot_" + SlotKeys[i]), 0.68f);
				label.Left.Set(x, 0f); label.Top.Set(LabelY, 0f); card.Append(label);
				var slot = new ItemSlotElement(set.Items, i, SlotScale)
				{
					Accepts = it => VanitySet.Fits(slotIndex, it),
					EmptyHint = LevyUtil.T("Vanity.SlotHint_" + SlotKeys[i]),
					OnChanged = (arr, at) => SlotChanged(set, at),
				};
				slot.Left.Set(x, 0f); slot.Top.Set(SlotY, 0f);
				card.Append(slot);
			}
			var summary = new Label(SummaryText(set), 0.76f);
			summary.Left.Set(SlotX0 + 6 * SlotPitch + 40, 0f); summary.Top.Set(SlotY + 8, 0f);
			card.Append(summary);
			return card;
		}

		/// <summary>One item to a slot: a stack taken from the cursor leaves its surplus on the cursor (or in the player's pockets), because the slot holds exactly one piece.</summary>
		private void SlotChanged(VanitySet set, int at)
		{
			var it = set.Items[at];
			if (it != null && !it.IsAir && it.stack > 1)
			{
				var extra = it.Clone(); extra.stack = it.stack - 1; it.stack = 1;
				if (Main.mouseItem == null || Main.mouseItem.IsAir) Main.mouseItem = extra;
				else Main.LocalPlayer.QuickSpawnItem(Main.LocalPlayer.GetSource_Misc("LevyVanity"), extra, extra.stack);
			}
			Edited();
		}

		private string SummaryText(VanitySet set)
		{
			int n = 0; foreach (var it in set.Items) if (it != null && !it.IsAir) n++;
			if (n == 0) return LevyUtil.T("Vanity.NothingYet");
			return set.Target switch
			{
				VanityTargetKind.All => LevyUtil.T("Vanity.WornByAll"),
				VanityTargetKind.Class => LevyUtil.T("Vanity.WornByClass", set.ClassKey == null ? "-" : WeaponTags.DisplayName(set.ClassKey, true)),
				_ => LevyUtil.T("Vanity.WornByLevies", set.Levies.Count),
			};
		}

		// ------------------------------------------------------------------ choices

		private static string TargetText(VanitySet set) => set.Target switch
		{
			VanityTargetKind.All => LevyUtil.T("Vanity.TargetAll"),
			VanityTargetKind.Class => LevyUtil.T("Vanity.TargetClass"),
			_ => LevyUtil.T("Vanity.TargetLevies", set.Levies.Count),
		};

		private List<(string, Action, bool)> TargetChoices(VanitySet set)
		{
			var l = new List<(string, Action, bool)>();
			void Add(VanityTargetKind k, string key) => l.Add((LevyUtil.T(key), () =>
			{
				set.Target = k;
				if (k == VanityTargetKind.Class && set.ClassKey == null) set.ClassKey = WeaponTags.ClassKeys[0];
				Reshape();
			}, set.Target == k));
			Add(VanityTargetKind.All, "Vanity.TargetAll");
			Add(VanityTargetKind.Class, "Vanity.TargetClass");
			Add(VanityTargetKind.Levies, "Vanity.TargetLeviesPlain");
			return l;
		}

		private List<(string, Action, bool)> ClassChoices(VanitySet set)
		{
			var l = new List<(string, Action, bool)>();
			foreach (var c in WeaponTags.ClassKeys) { string id = c; l.Add((WeaponTags.DisplayName(id), () => { set.ClassKey = id; Reshape(); }, set.ClassKey == id)); }
			return l;
		}

		/// <summary>The levies this armory dresses, by name: every regular whose home barracks links to it. A pick toggles that levy in the row; the list stays open for the next.</summary>
		private void ChooseLevies(VanitySet set, UIElement anchor)
		{
			var te = Te; var roster = RosterSystem.Instance;
			var options = new List<(string label, Action pick, bool on)>();
			if (te != null && roster != null)
			{
				foreach (var rec in roster.Regulars)
				{
					if (rec == null || !ReferenceEquals(VanityLookup.ArmoryFor(rec), te)) continue;
					var r = rec;
					options.Add((r.Name, () =>
					{
						if (!set.Levies.Remove(r.Id) && set.Levies.Count < VanitySet.MaxLevies) set.Levies.Add(r.Id);
						Edited(); ChooseLevies(set, anchor);
					}, set.Levies.Contains(r.Id)));
				}
			}
			if (options.Count == 0) options.Add((LevyUtil.T("Vanity.NoLevies"), null, false));
			var a = anchor.GetDimensions(); var me = GetDimensions();
			_popup.Show(this, Math.Max(0f, a.X - me.X + 308), Math.Max(0f, a.Y - me.Y + 28), 200, options);
		}

		private void Choose(UIElement anchor, List<(string, Action, bool)> options)
		{
			var a = anchor.GetDimensions(); var me = GetDimensions();
			var items = new List<(string label, Action pick, bool on)>();
			foreach (var (label, pick, on) in options) items.Add((label, pick, on));
			_popup.Show(this, a.X - me.X, a.Y - me.Y + a.Height + 2f, Math.Max(a.Width, 120f), items);
			SoundEngine.PlaySound(SoundID.MenuTick);
		}
	}
}
