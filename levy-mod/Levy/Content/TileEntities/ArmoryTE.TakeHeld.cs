using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Content.TileEntities
{
	public enum TakeHeldResult : byte
	{
		/// <summary>The item left the levy: <c>taken</c> is the one real item, with its prefix and stack, and nothing else holds it any more.</summary>
		Ok,
		/// <summary>This armory does not serve a levy with that id (he left, was discharged, or is in reserve).</summary>
		NotServed,
		/// <summary>The slot no longer holds that item (the levy swapped gear, or another click got there first): nothing moved.</summary>
		SlotChanged,
	}

	/// <summary>
	/// TAKING WHAT A LEVY CARRIES, straight from the armory screen. The item cells of gear a levy carries used to be read-only; a click on one now moves that very item to the player.
	/// One authority does it (single player, host or server): it checks the levy is served by this armory and the slot still holds the type the player clicked, empties the slot and its hand lock,
	/// and lets the armory give the levy whatever it would issue next (<see cref="IssueGear"/>: his next-best pick from the racks, or nothing when the racks hold nothing he can use). The item
	/// is the same instance, not a copy, so prefix, stack and mod data travel with it and a second click on the same cell finds the slot changed and moves nothing: it cannot be duplicated or lost.
	/// </summary>
	public partial class ArmoryTE
	{
		/// <summary>Moves the item in <paramref name="slot"/> (SoldierGear.Slot*) of levy <paramref name="soldierId"/> out of his kit. <paramref name="fallback"/> is what he holds in that slot afterwards (air when the armory has nothing for him).</summary>
		public TakeHeldResult TakeHeld(int soldierId, int slot, int expectType, out Item taken, out Item fallback)
		{
			taken = null; fallback = null;
			SoldierRecord rec = null;
			foreach (var r in ServedSoldiers()) if (r != null && r.Id == soldierId) { rec = r; break; }
			if (rec?.Gear == null) return TakeHeldResult.NotServed;
			var g = rec.Gear;
			Item held = g.Get(slot);
			if (IsAir(held) || held.type != expectType) return TakeHeldResult.SlotChanged;
			taken = held;
			g.Set(slot, new Item());
			g.SetLocked(slot, false); // the hand assignment goes with the item: the armory may fill the slot again
			IssueGear(rec); // his next-best pick from the racks (and the rest of his kit, as on any visit)
			var now = g.Get(slot); fallback = now;
			if (rec.HasBody && Main.npc[rec.BodyIndex].ModNPC is global::Levy.Content.NPCs.SoldierNPC body) body.NotifyGearChanged();
			_inUseTick = uint.MaxValue; // the next panel frame rebuilds the list
			MarkDirty();
			if (LevyUtil.IsServer) RosterSystem.Instance?.SendRosterSnapshot();
			return TakeHeldResult.Ok;
		}

		/// <summary>
		/// Hands a taken item to a player. <paramref name="direct"/> (single player and the self-checks, where the player's pack is the authority): into the pack (stacking first), the rest dropped at the player's feet.
		/// Not direct (a server, which cannot write a client's pack): dropped at the player's feet with no pickup delay, so the client's own pickup puts it in the pack at once. Either way it is the one item, exactly once.
		/// </summary>
		public static void GiveToPlayer(Player p, Item it, bool direct)
		{
			if (p == null || it == null || it.IsAir || it.stack <= 0) return;
			if (direct)
			{
				const int PackSlots = 50; // inventory 0..49: hotbar and pack, never coins, ammo or the cursor
				for (int i = 0; i < PackSlots && it.stack > 0; i++)
				{
					var s = p.inventory[i];
					if (s == null || s.IsAir || s.type != it.type || s.prefix != it.prefix || s.stack >= s.maxStack) continue;
					ItemLoader.TryStackItems(s, it, out _);
				}
				if (it.stack > 0)
					for (int i = 0; i < PackSlots; i++)
						if (p.inventory[i] == null || p.inventory[i].IsAir) { it.newAndShiny = true; p.inventory[i] = it; return; }
				if (it.stack <= 0) return;
			}
			p.QuickSpawnClonedItem(p.GetSource_Misc("Levy:TakeHeld"), it, it.stack);
		}

		/// <summary>Server: sends this armory's in-use list to one client.</summary>
		public void SendInUseTo(int client)
		{
			var p = global::Levy.Common.Net.LevyPackets.New(global::Levy.Common.Net.LevyPacket.ArmoryInUseList);
			WriteInUse(p);
			p.Send(client);
		}
	}
}
