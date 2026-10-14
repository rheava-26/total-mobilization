using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common;
using Levy.Common.Config;
using Levy.Common.Dialogue;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.Tiles;

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

		private bool _dirty;

		/// <summary>World position where bodies walk out (bottom-centre of the 3x3, one tile above the floor).</summary>
		public Vector2 MusterPoint => new Vector2((Position.X + 1.5f) * 16f, (Position.Y + 3) * 16f - 1f);
		public Vector2 Center => new Vector2((Position.X + 1.5f) * 16f, (Position.Y + 1.5f) * 16f);

		public override bool IsTileValidForEntity(int x, int y)
		{
			if (!WorldGen.InWorld(x, y)) return false;
			Tile tile = Main.tile[x, y];
			return tile.HasTile && tile.TileType == ModContent.TileType<BarracksTile>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;
		}

		/// <summary>Placement hook (processedCoordinates: i,j are the top-left). Clients ask the server; the server/SP places and initialises.</summary>
		public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				NetMessage.SendTileSquare(Main.myPlayer, i, j, LevyConstants.BarracksWidth, LevyConstants.BarracksHeight);
				NetMessage.SendData(MessageID.TileEntityPlacement, number: i, number2: j, number3: Type);
				return -1;
			}
			int id = Place(i, j);
			if (ByID.TryGetValue(id, out var te) && te is BarracksTE b) {
				b.InitNew();
				b.Sync();
			}
			return id;
		}

		/// <summary>Server received a client's placement request: the entity was just placed; NetPlaceEntityAttempt syncs it right after this.</summary>
		public override void OnNetPlace() => InitNew();

		/// <summary>Fresh settlement: random name, config default for auto-muster, a full manpower pool.</summary>
		internal void InitNew()
		{
			try { TownName = DialogueSystem.RandomTownName() ?? ""; }
			catch (Exception e) { Levy.Instance.Logger.Warn("Levy: RandomTownName failed: " + e.Message); TownName = ""; }
			try { AutoMuster = LevyConfig.Get?.AutoMusterDefault ?? true; }
			catch (Exception) { AutoMuster = true; }
			try { ManpowerCapCached = Math.Max(0, SettlementSystem.Instance?.ManpowerCap(this) ?? 0); }
			catch (Exception e) { Levy.Instance.Logger.Warn("Levy: ManpowerCap failed: " + e.Message); ManpowerCapCached = 0; }
			Manpower = ManpowerCapCached;
		}

		/// <summary>The barracks tile was broken: the militia stands down and its regulars re-base to the nearest other barracks (or go homeless).</summary>
		public override void OnKill()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			try { SettlementSystem.Instance?.StandDown(this); }
			catch (Exception e) { Levy.Instance.Logger.Warn("Levy: StandDown on barracks kill failed: " + e.Message); }

			var roster = RosterSystem.Instance;
			if (roster == null) return;
			BarracksTE newHome = NearestOtherBarracks();
			// Snapshot: Rebase may touch the list.
			var regulars = roster.Regulars.ToArray();
			foreach (var rec in regulars) {
				if (rec == null || rec.HomeBarracksId != ID) continue;
				try { roster.Rebase(rec, newHome); }
				catch (Exception e) { Levy.Instance.Logger.Warn("Levy: Rebase on barracks kill failed: " + e.Message); }
			}
		}

		private BarracksTE NearestOtherBarracks()
		{
			BarracksTE best = null; float bestD = float.MaxValue;
			foreach (var te in ByID.Values) {
				if (te is not BarracksTE b || ReferenceEquals(b, this) || b.ID == ID) continue;
				float d = Vector2.DistanceSquared(b.Center, Center);
				if (d < bestD) { bestD = d; best = b; }
			}
			return best;
		}

		/// <summary>Queue a sync to clients (sent once on the next server tick).</summary>
		public void MarkDirty() => _dirty = true;

		/// <summary>Sync now (server only; no-op elsewhere).</summary>
		public void Sync()
		{
			_dirty = false;
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.TileEntitySharing, number: ID, number2: Position.X, number3: Position.Y);
		}

		public override void Update()
		{
			if (_dirty) Sync();
		}

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
