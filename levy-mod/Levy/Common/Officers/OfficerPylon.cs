using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Levy.Common.Officers
{
	/// <summary>
	/// OFFICERS STILL COUNT FOR THEIR TOWN'S PYLON. Vanilla lets a Teleportation Pylon work only while at least two town NPCs stand in the pylon's scan area (TeleportPylonsSystem.DoesPositionHaveEnoughNPCs reads where each
	/// town NPC IS, not where it lives). An officer marches with the commander, so the town he lives in loses him: with two or three officers out, the home pylon refuses the commander's trip back.
	/// An active officer (enrolled, avatar marching) whose HOME is inside this pylon's scan area but who is standing outside it lowers the pylon's NPC requirement by one, which is the same as counting him. An officer already
	/// standing in the area is counted by vanilla and not subtracted again. <c>LEVY_PYLONOFFICER=0</c> turns it off (lab A/B).
	/// </summary>
	public class OfficerPylonCount : GlobalPylon
	{
		public static readonly bool On = Environment.GetEnvironmentVariable("LEVY_PYLONOFFICER") != "0";
		/// <summary>Lab: forces the rule off (true) or leaves it to <see cref="On"/> (null).</summary>
		internal static bool? LabOff;
		/// <summary>Lab: requirements lowered so far.</summary>
		public static long Lowered;

		/// <summary>Is a tile (the pylon's, an NPC's home) inside the scan rectangle centred on the pylon?</summary>
		private static bool InScan(Point16 pylon, float tileX, float tileY)
		{
			float hw = Main.buffScanAreaWidth / 2f, hh = Main.buffScanAreaHeight / 2f;
			return Math.Abs(tileX - (pylon.X + 1f)) <= hw && Math.Abs(tileY - (pylon.Y + 1.5f)) <= hh;
		}

		/// <summary>How many active officers live in this pylon's area and are away from it.</summary>
		public static int OfficersAway(Point16 pylon)
		{
			int n = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC t = Main.npc[i];
				if (!t.active || !t.townNPC || t.homeless) continue;
				OfficerGlobalNPC g = OfficerSystem.Data(t);
				if (g == null || !g.Enrolled || !OfficerSystem.Instance.HasLiveAvatar(t)) continue;
				if (!InScan(pylon, t.homeTileX, t.homeTileY)) continue; // lives elsewhere
				Vector2 at = t.Center / 16f;
				if (InScan(pylon, at.X, at.Y)) continue; // standing in the area: vanilla counts him
				n++;
			}
			return n;
		}

		public override bool? ValidTeleportCheck_PreNPCCount(TeleportPylonInfo pylonInfo, ref int defaultNecessaryNPCCount)
		{
			if (defaultNecessaryNPCCount <= 0 || (LabOff ?? !On)) return null;
			int away = OfficersAway(pylonInfo.PositionInTiles);
			if (away <= 0) return null;
			Lowered++;
			defaultNecessaryNPCCount = Math.Max(0, defaultNecessaryNPCCount - away);
			return null; // vanilla's own count, against the lowered requirement
		}
	}
}
