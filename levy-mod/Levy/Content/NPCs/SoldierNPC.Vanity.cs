using Terraria;
using Levy.Common.Vanity;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// A levy's vanity (client draw path): the armory's vanity rows put head / body / legs pieces into the dummy's SOCIAL armor slots (armor[10..12]), which is where vanilla's Player.PlayerFrame reads them to
	/// replace what is drawn, and the rows' dyes into the dummy's dye slots (see <see cref="VanityDress"/>). Nothing here touches stats: the social slots are filled only around the draw
	/// (<see cref="ClearVanitySlots"/> empties them again), and every stat pass resolves head / body / legs from armor[0..2] itself (UpdateDummyArmorSets).
	/// </summary>
	public partial class SoldierNPC
	{
		private VanityLook _vanityLook;
		private uint _vanityTick;
		private int _vanityHash;

		/// <summary>Re-resolves what this levy wears from his armory's vanity rows, at most twice a second.</summary>
		private void ResolveVanity()
		{
			uint now = Main.GameUpdateCount;
			if (_vanityTick != 0 && now - _vanityTick < 30) return;
			_vanityTick = now == 0 ? 1 : now;
			_vanityLook = Hostile ? HostileVanity() : VanityLookup.LookFor(Record); // HOSTILE SEAM: a band's own vanity
			_vanityHash = _vanityLook?.Hash() ?? 0;
		}

		/// <summary>Part of the draw's gear hash: changes when the look does, so the dummy's dyes are re-read.</summary>
		private int VanityHashForDraw => _vanityHash;

		private void ApplyVanityDyes() { _blankSlot ??= new Item(); VanityDress.ApplyDyes(Dummy, _vanityLook, _blankSlot); }

		/// <summary>Fills the social slots with this levy's vanity for the draw.</summary>
		private void ApplyVanitySlots() { _blankSlot ??= new Item(); VanityDress.ApplySlots(Dummy, _vanityLook, _blankSlot); }

		/// <summary>Empties the social slots again once the body is drawn, so no stat pass ever sees them.</summary>
		private void ClearVanitySlots() { if (Dummy == null) return; _blankSlot ??= new Item(); VanityDress.ClearSlots(Dummy, _blankSlot); }
	}
}
