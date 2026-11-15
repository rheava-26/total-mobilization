using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;
using Levy.Common.Util;
using Levy.Common.Weapons;

namespace Levy.Common.UI
{
	/// <summary>
	/// THE DEPOSIT WARNING. Putting an item in the Armory that breaks blocks or otherwise alters the world (<see cref="WorldAlter"/>) opens a popup the player has to click away: it names the item, says what it does and that
	/// levies will never use it. "Don't show again for this item" remembers it for this character. It is only ever shown to the person who made the deposit (the local client), never from a script or a server action:
	/// <see cref="Note"/> is called from the shift-click, the Deposit junk button and a click that drops a held item into a cell.
	/// The warning state (<see cref="Pending"/>) is plain data so the self-check can read it without drawing anything; the window itself is built when the first frame that draws it needs it.
	/// </summary>
	public class AlterWarning : ModSystem
	{
		/// <summary>One item the open warning is about: its type, name and the one-line reason.</summary>
		public readonly struct Entry
		{
			public readonly int Type; public readonly string Name, Reason;
			public Entry(int type, string name, string reason) { Type = type; Name = name; Reason = reason; }
		}

		/// <summary>What the open warning lists (empty: none is open).</summary>
		public static readonly List<Entry> Pending = new();
		/// <summary>Times a warning was opened this session (self-check counter).</summary>
		public static int Opened;
		public static bool Active => Pending.Count > 0;
		public const int MaxRows = 6;

		private UserInterface _ui; private WarnState _state;

		/// <summary>The entries that would be warned about for these items: flagged, not dismissed on this character, one per item type.</summary>
		public static List<Entry> Collect(IEnumerable<Item> items, HashSet<string> dismissed)
		{
			var res = new List<Entry>(); var seen = new HashSet<int>();
			foreach (Item it in items)
			{
				if (it == null || it.IsAir || !seen.Add(it.type)) continue;
				string reason = WorldAlter.ReasonText(it); if (reason == null) continue;
				if (dismissed != null && dismissed.Contains(WorldAlter.KeyOf(it.type))) continue;
				res.Add(new Entry(it.type, it.Name, reason));
			}
			return res;
		}

		/// <summary>Warns about one deposited item (a clone is taken: the stack the deposit consumed is no longer there).</summary>
		public static void Note(Item deposited) { if (deposited != null && !deposited.IsAir) NoteMany(new[] { deposited.Clone() }); }

		/// <summary>Warns about the deposited items that alter the world. Local player only; nothing happens on a server.</summary>
		public static void NoteMany(IEnumerable<Item> deposited)
		{
			if (Main.dedServ || Main.gameMenu) return;
			if (NoteFor(deposited, Main.LocalPlayer?.GetModPlayer<AlterWarnPlayer>()?.Dismissed)) SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		/// <summary>The warning logic with the dismissed set passed in (the self-check drives it without a player). True when a warning is now open.</summary>
		public static bool NoteFor(IEnumerable<Item> deposited, HashSet<string> dismissed)
		{
			var fresh = Collect(deposited, dismissed);
			if (fresh.Count == 0) return false;
			foreach (Entry e in fresh) if (!Pending.Any(p => p.Type == e.Type)) Pending.Add(e);
			Opened++;
			return true;
		}

		/// <summary>Closes the warning; with <paramref name="never"/> the listed items are not warned about again on this character.</summary>
		public static void Dismiss(bool never)
		{
			if (never) { var pl = Main.LocalPlayer?.GetModPlayer<AlterWarnPlayer>(); if (pl != null) foreach (Entry e in Pending) pl.Dismissed.Add(WorldAlter.KeyOf(e.Type)); }
			Pending.Clear();
		}

		public override void OnWorldUnload() { Pending.Clear(); _state = null; _ui?.SetState(null); }

		public override void Load() { if (!Main.dedServ) _ui = new UserInterface(); }

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ || _ui == null) return;
			if (!Active) { if (_ui.CurrentState != null) { _ui.SetState(null); _state = null; } return; }
			if (_state == null || _state.Count != Pending.Count) { _state = new WarnState(); _state.Build(); _ui.SetState(_state); }
			Main.LocalPlayer.mouseInterface = true; // modal: nothing under it is clicked or used until it is answered
			_ui.Update(gameTime);
			{ Main.mouseLeft = false; Main.mouseRight = false; } // the window has taken this frame's click; the inventory drawn after the update must not see it
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int mouse = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
			if (mouse < 0) return;
			layers.Insert(mouse, new LegacyGameInterfaceLayer("Levy: Alter Warning", () =>
			{
				if (Active && _ui?.CurrentState != null) _ui.Draw(Main.spriteBatch, new GameTime());
				return true;
			}, InterfaceScaleType.UI));
		}

		/// <summary>The window: a dimmed screen, a panel in the middle, the items with their reasons, and two buttons.</summary>
		private sealed class WarnState : UIState
		{
			public int Count;

			public void Build()
			{
				RemoveAllChildren();
				Count = Pending.Count;
				int rows = Math.Min(MaxRows, Pending.Count); int extra = Pending.Count - rows;
				float w = 360f, h = 96f + rows * 34f + (extra > 0 ? 18f : 0f) + 36f;
				var dim = new UIPanel { BackgroundColor = Color.Black * 0.45f, BorderColor = Color.Transparent };
				dim.Width.Set(0, 1f); dim.Height.Set(0, 1f); Append(dim);
				var panel = new UIPanel { BackgroundColor = new Color(18, 24, 50) * 0.97f };
				panel.Width.Set(w, 0f); panel.Height.Set(h, 0f); panel.HAlign = 0.5f; panel.VAlign = 0.4f; panel.SetPadding(10f);
				Append(panel);
				var head = new UIText(LevyUtil.T(Pending.Count == 1 ? "UI2.AlterWarnTitleOne" : "UI2.AlterWarnTitleMany"), 0.9f) { HAlign = 0f };
				head.Top.Set(0, 0f); panel.Append(head);
				for (int i = 0; i < rows; i++)
				{
					var row = new Row(Pending[i]); row.Top.Set(28f + i * 34f, 0f); panel.Append(row);
				}
				if (extra > 0) { var more = new UIText(LevyUtil.T("UI2.AlterWarnMore", extra), 0.75f); more.Top.Set(28f + rows * 34f + 2f, 0f); panel.Append(more); }
				var ok = new LevyButton(LevyUtil.T("UI2.AlterWarnOk"), LevyConstants.UI.ButtonTextScale, 80f, 28f, () => Dismiss(false));
				ok.Left.Set(0f, 0f); ok.VAlign = 1f; panel.Append(ok);
				var never = new LevyButton(LevyUtil.T(Pending.Count == 1 ? "UI2.AlterWarnNeverOne" : "UI2.AlterWarnNeverMany"), LevyConstants.UI.ButtonTextScale, w - 100f - 20f, 28f, () => Dismiss(true));
				never.Left.Set(90f, 0f); never.VAlign = 1f; panel.Append(never);
				Recalculate();
			}
		}

		/// <summary>One line: the item's icon, its name, and what it does.</summary>
		private sealed class Row : UIElement
		{
			private readonly Entry _e; private Item _item;
			public Row(Entry e) { _e = e; Width.Set(0, 1f); Height.Set(32f, 0f); }
			protected override void DrawSelf(SpriteBatch sb)
			{
				var r = GetDimensions().ToRectangle();
				_item ??= new Item(_e.Type);
				Main.instance.LoadItem(_e.Type);
				var tex = TextureAssets.Item[_e.Type].Value;
				float scale = Math.Min(1f, 28f / Math.Max(tex.Width, tex.Height));
				sb.Draw(tex, new Vector2(r.X + 16f, r.Center.Y), null, Color.White, 0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
				Terraria.Utils.DrawBorderString(sb, _e.Name, new Vector2(r.X + 38f, r.Y + 0f), Color.White, 0.8f);
				Terraria.Utils.DrawBorderString(sb, _e.Reason, new Vector2(r.X + 38f, r.Y + 15f), new Color(255, 200, 120), 0.7f);
			}
		}
	}

	/// <summary>Per character: the items the player asked not to be warned about again (by item key).</summary>
	public class AlterWarnPlayer : ModPlayer
	{
		public readonly HashSet<string> Dismissed = new();
		public override void SaveData(TagCompound tag) { if (Dismissed.Count > 0) tag["alterWarnOff"] = Dismissed.ToList(); }
		public override void LoadData(TagCompound tag) { Dismissed.Clear(); foreach (string s in tag.GetList<string>("alterWarnOff")) Dismissed.Add(s); }
	}
}
