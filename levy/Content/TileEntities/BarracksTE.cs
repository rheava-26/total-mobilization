using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common;
using Levy.Common.Soldiers;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// A settlement = this barracks plus the town NPCs living near it. Owned by the Tiles agent (placement/validity/net),
	/// read and written by the Settlement/Roster/Economy systems through the public fields below.
	/// </summary>
	public class BarracksTE : ModTileEntity
	{
		// ---- persistent ----
		public string TownName = "";
		public long Treasury;
		public int Manpower;
		public bool AutoMuster = true;
		public bool Mobilized;
		public bool TaxToTreasury;
		public int RecruitingDriveDay = -1;
		public int HazardPayDay = -1;
		public bool TooltipShown;

		// ---- runtime ----
		public readonly List<SoldierRecord> Militia = new();
		public int ReinforcementsPending;
		public int MusterTimer;
		public int ManpowerCapCached;
		public int MilitiaLostThisEvent;
		public bool NoOneLeftAnnounced;

		/// <summary>World position where bodies walk out (bottom-centre of the 3x3, one tile above the floor).</summary>
		public Vector2 MusterPoint => new Vector2((Position.X + 1.5f) * 16f, (Position.Y + 3) * 16f - 1f);
		public Vector2 Center => new Vector2((Position.X + 1.5f) * 16f, (Position.Y + 1.5f) * 16f);

		public override bool IsTileValidForEntity(int x, int y) => false; // Tiles agent replaces
		public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate) => -1; // Tiles agent replaces

		public override void SaveData(TagCompound tag)
		{
			tag["name"] = TownName ?? ""; tag["treasury"] = Treasury; tag["manpower"] = Manpower; tag["auto"] = AutoMuster; tag["mob"] = Mobilized; tag["tax"] = TaxToTreasury;
			tag["rdDay"] = RecruitingDriveDay; tag["hzDay"] = HazardPayDay; tag["tip"] = TooltipShown;
		}

		public override void LoadData(TagCompound tag)
		{
			TownName = tag.GetString("name"); Treasury = tag.GetLong("treasury"); Manpower = tag.GetInt("manpower"); AutoMuster = tag.GetBool("auto"); Mobilized = tag.GetBool("mob");
			TaxToTreasury = tag.GetBool("tax"); RecruitingDriveDay = tag.GetInt("rdDay"); HazardPayDay = tag.GetInt("hzDay"); TooltipShown = tag.GetBool("tip");
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(TownName ?? ""); writer.Write(Treasury); writer.Write(Manpower); writer.Write(AutoMuster); writer.Write(Mobilized); writer.Write(TaxToTreasury); writer.Write(ManpowerCapCached);
		}

		public override void NetReceive(BinaryReader reader)
		{
			TownName = reader.ReadString(); Treasury = reader.ReadInt64(); Manpower = reader.ReadInt32(); AutoMuster = reader.ReadBoolean(); Mobilized = reader.ReadBoolean(); TaxToTreasury = reader.ReadBoolean(); ManpowerCapCached = reader.ReadInt32();
		}
	}
}
