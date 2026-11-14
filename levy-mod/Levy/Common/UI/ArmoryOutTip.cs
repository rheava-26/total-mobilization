using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.UI
{
	/// <summary>
	/// The extra tooltip line on an item that is out of the armory (carried by a levy, lent to a drone). The armory panel sets the line for the item under the pointer; it is added to that item's tooltip in the
	/// same frame, and nowhere else. Nothing is stored on items.
	/// </summary>
	public class ArmoryOutTip : GlobalItem
	{
		private static string _text; private static Color _color; private static int _type, _prefix; private static uint _tick;

		/// <summary>Called by the armory panel while the pointer is on an out cell, with the clone it put into the hover slot.</summary>
		public static void Set(Item shown, string text, Color color)
		{
			_text = text; _color = color; _type = shown.type; _prefix = shown.prefix; _tick = Main.GameUpdateCount;
		}

		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			if (_text == null || item.type != _type || item.prefix != _prefix || Main.GameUpdateCount - _tick > 2u) return;
			tooltips.Add(new TooltipLine(Mod, "LevyInUse", _text) { OverrideColor = _color });
		}
	}
}
