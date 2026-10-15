using System;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.UI;

namespace Levy.Common.UI
{
	/// <summary>Type/stack/prefix snapshot of a slot, to detect what a click changed.</summary>
	public readonly struct SlotSnap
	{
		public readonly int Type, Stack, Prefix;
		public SlotSnap(Item it) { Type = it?.type ?? 0; Stack = it?.stack ?? 0; Prefix = it?.prefix ?? 0; }
		public bool Changed(Item it) => it == null ? Type != 0 : (it.type != Type || it.stack != Stack || it.prefix != Prefix);
		public static bool Differs(SlotSnap[] snaps, Item[] arr, int i) => i < snaps.Length && snaps[i].Changed(arr[i]);
	}

	/// <summary>
	/// One armory slot drawn and handled with vanilla ItemSlot code. Uses the single-item overloads (slot 0) because the
	/// array overloads index 58-entry glow arrays by slot and the armory has 120. Chest context in single player; Bank
	/// context on multiplayer clients, because the chest context sends SyncChestItem for Main.chest[player.chest] (-1).
	/// </summary>
	public class ItemSlotElement : UIElement
	{
		private readonly Item[] _arr;
		private readonly int _index;
		private readonly float _scale;
		/// <summary>Optional filter: an item that fails it is bounced back into the mouse.</summary>
		public Func<Item, bool> Accepts;
		/// <summary>Called with the offending item when <see cref="Accepts"/> bounced a placement back to the mouse.</summary>
		public Action<Item> OnRejected;
		/// <summary>Called after a click changed this slot's content.</summary>
		public Action<Item[], int> OnChanged;
		/// <summary>Hover text shown while the slot is empty and the mouse holds nothing.</summary>
		public string EmptyHint;

		public ItemSlotElement(Item[] arr, int index, float scale)
		{
			_arr = arr; _index = index; _scale = scale;
			float size = 52f * scale;
			Width.Set(size, 0f); Height.Set(size, 0f);
		}

		public static int Context => Main.netMode == NetmodeID.MultiplayerClient ? ItemSlot.Context.BankItem : ItemSlot.Context.ChestItem;

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			if (_arr == null || _index < 0 || _index >= _arr.Length) return;
			_arr[_index] ??= new Item();
			float oldScale = Main.inventoryScale;
			Main.inventoryScale = _scale;
			int context = Context;
			Item inv = _arr[_index];
			if (IsMouseHovering)
			{
				Main.LocalPlayer.mouseInterface = true;
				var before = new SlotSnap(inv);
				ItemSlot.OverrideHover(ref inv, context);
				ItemSlot.LeftClick(ref inv, context);
				ItemSlot.RightClick(ref inv, context);
				// Only a click that changed the slot is vetted, so an item that is already in the slot is never yanked out for failing the filter.
				if (Accepts != null && inv != null && !inv.IsAir && before.Changed(inv) && !Accepts(inv))
				{
					var rejected = inv;
					Utils.Swap(ref inv, ref Main.mouseItem);
					SoundEngine.PlaySound(SoundID.MenuTick);
					OnRejected?.Invoke(rejected);
				}
				inv ??= new Item();
				ItemSlot.MouseHover(ref inv, context);
				_arr[_index] = inv;
				if (before.Changed(inv)) OnChanged?.Invoke(_arr, _index);
				if (inv.IsAir && Main.mouseItem.IsAir && !string.IsNullOrEmpty(EmptyHint)) Main.hoverItemName = EmptyHint;
			}
			var pos = GetDimensions().Position();
			ItemSlot.Draw(spriteBatch, ref inv, context, pos);
			_arr[_index] = inv;
			Main.inventoryScale = oldScale;
		}
	}
}
