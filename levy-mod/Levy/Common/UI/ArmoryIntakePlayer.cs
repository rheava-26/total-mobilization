using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// Shift-click an inventory item while an Armory is open: the whole stack goes into its storage, stacking onto the stacks already there, as with a chest (<see cref="ArmoryIntake.TakesByHand"/>:
	/// anything but a favorited item or a coin). Those keep their normal shift-click.
	/// </summary>
	public class ArmoryIntakePlayer : ModPlayer
	{
		private static ArmoryTE OpenArmory(Item item, int context)
		{
			if (Main.dedServ || !Main.playerInventory) return null;
			if (context != ItemSlot.Context.InventoryItem && context != ItemSlot.Context.InventoryAmmo) return null;
			var te = UISystem.CurrentArmory;
			if (te == null || !UIActions.IsLive(te) || !ArmoryIntake.TakesByHand(item)) return null;
			return te;
		}

		public override bool HoverSlot(Item[] inventory, int context, int slot)
		{
			if (Player.whoAmI != Main.myPlayer || inventory == null || slot < 0 || slot >= inventory.Length) return false;
			if (!ItemSlot.ShiftInUse || OpenArmory(inventory[slot], context) == null) return false;
			Main.cursorOverride = CursorOverrideID.InventoryToChest;
			return true;
		}

		public override bool ShiftClickSlot(Item[] inventory, int context, int slot)
		{
			if (Player.whoAmI != Main.myPlayer || inventory == null || slot < 0 || slot >= inventory.Length) return false;
			var it = inventory[slot];
			var te = OpenArmory(it, context);
			if (te == null) return false;
			var before = new SlotSnap[te.Storage.Length];
			for (int i = 0; i < before.Length; i++) before[i] = new SlotSnap(te.Storage[i]);
			var warned = it.Clone(); // what was put in (the deposit takes the stack): items that alter the world get the warning
			var left = te.Deposit(it);
			bool moved = left == null || left.IsAir || left.stack <= 0 || !ReferenceEquals(left, it) || left.stack != it.stack;
			inventory[slot] = left == null || left.IsAir || left.stack <= 0 ? new Item() : left;
			if (Main.netMode == NetmodeID.MultiplayerClient)
				for (int i = 0; i < te.Storage.Length && i < before.Length; i++) if (SlotSnap.Differs(before, te.Storage, i)) UIActions.SendArmorySlot(te, 0, i);
			if (moved) { SoundEngine.PlaySound(SoundID.Grab); AlterWarning.Note(warned); }
			return true;
		}
	}
}
