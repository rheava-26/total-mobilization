using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// The armory's rule list (see <see cref="ArmoryRules"/>): the preview counts the rules panel shows, and the one entry point that changes the list.
	/// Only the server (or single player) ever writes <see cref="Rules"/>; a multiplayer client sends its edited list to the server (ArmoryRulesSet) and receives the result with the tile entity.
	/// </summary>
	public partial class ArmoryTE
	{
		/// <summary>Set by the rules panel on a multiplayer client while an edit is typed or waiting to be sent: a tile entity sync arriving meanwhile must not overwrite it.</summary>
		public bool RulesHeldBack;

		/// <summary>
		/// Counts what this armory (and the armories on its network) could issue right now, by category and class, under its own rules: items the rules bar are not counted, and a hand-edited tag counts as the tag.
		/// Weapon rack, bulk storage and networked storage; a connected Magic Storage network is not counted. Read-only.
		/// </summary>
		public RuleStock CountStock()
		{
			var stock = new RuleStock();
			using var _rs = ArmoryRules.Enter(Rules);
			void Count(Item it)
			{
				if (IsAir(it) || it.maxStack > 1 || !IsIssuable(it)) return;
				stock.Add(Rules.CatsOf(it), Rules.ClassKeyOf(it));
			}
			for (int i = 0; i < SidearmSlot; i++) Count(IssueWeapons[i]);
			foreach (var it in Storage) Count(it);
			foreach (var o in OtherNetworked()) foreach (var it in o.Storage) Count(it);
			return stock;
		}

		/// <summary>The number of levies this armory serves, or -1 when this machine cannot know (a multiplayer client holds no served list).</summary>
		public int ServedCount => Main.netMode == NetmodeID.MultiplayerClient ? -1 : ServedSoldiers().Count;

		/// <summary>
		/// Server / single player: replaces the rule list with <paramref name="incoming"/> (already read and clamped by <see cref="ArmoryRules.FromBytes"/>), bumps the revision and queues a sync.
		/// The levies are re-dealt on the next issue or upgrade pass; nothing is taken from anyone at once.
		/// </summary>
		public void SetRules(ArmoryRules incoming)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || incoming == null) return;
			int rev = Rules.Rev + 1;
			incoming.Prune();
			Rules.CopyFrom(incoming);
			Rules.Rev = rev;
			Rules.Changed(false);
			MarkDirty();
		}
	}
}
