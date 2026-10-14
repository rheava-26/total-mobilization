using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader.IO;
using Levy.Common.Net;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// Every interface action goes through here. In single player the systems are called directly; on a multiplayer
	/// client the matching <see cref="LevyPacket"/> is sent with the payload layout documented in LevyPackets.cs.
	/// Never called on a dedicated server.
	/// </summary>
	public static class UIActions
	{
		private static bool Single => Main.netMode == NetmodeID.SinglePlayer;
		private static Player Me => Main.LocalPlayer;

		// ------------------------------------------------------------------ roster
		public static void Enlist(BarracksTE te, Grade grade)
		{
			if (te == null) return;
			if (Single) RosterSystem.Instance.Enlist(te, Me, grade);
			else { var p = LevyPackets.New(LevyPacket.Enlist); p.Write(te.ID); p.Write((byte)grade); p.Send(); }
		}

		public static void Discharge(SoldierRecord rec)
		{
			if (rec == null) return;
			if (Single) RosterSystem.Instance.Discharge(rec);
			else { var p = LevyPackets.New(LevyPacket.Discharge); p.Write(rec.Id); p.Send(); }
		}

		public static void Promote(SoldierRecord rec, Grade to)
		{
			if (rec == null) return;
			if (Single) RosterSystem.Instance.Promote(rec, to);
			else { var p = LevyPackets.New(LevyPacket.Promote); p.Write(rec.Id); p.Write((byte)to); p.Send(); }
		}

		public static void AssignPatrol(SoldierRecord rec, Point16 a, Point16 b)
		{
			if (rec == null) return;
			if (Single) RosterSystem.Instance.AssignPatrol(rec, a, b);
			else { var p = LevyPackets.New(LevyPacket.AssignPatrol); p.Write(rec.Id); p.Write(a.X); p.Write(a.Y); p.Write(b.X); p.Write(b.Y); p.Send(); }
		}

		public static void SetRebase(SoldierRecord rec, BarracksTE newHome)
		{
			if (rec == null || newHome == null) return;
			if (Single) RosterSystem.Instance.Rebase(rec, newHome);
			else { var p = LevyPackets.New(LevyPacket.SetRebase); p.Write(rec.Id); p.Write(newHome.ID); p.Send(); }
		}

		/// <summary>True when the record can still absorb one of this crystal (mirrors the growth caps so a client never wastes one).</summary>
		public static bool CanFeed(SoldierRecord rec, int itemType)
		{
			if (rec == null) return false;
			switch (itemType)
			{
				case ItemID.LifeCrystal:
				{
					int cap = rec.Grade == Grade.Levy ? LevyConstants.MaxLifeCrystalsLevy : rec.Grade == Grade.ManAtArms ? LevyConstants.MaxLifeCrystalsManAtArms : LevyConstants.MaxLifeCrystalsChampion;
					return rec.LifeCrystals < cap && rec.MaxHp < LevyConstants.GradeHpCap[(int)rec.Grade];
				}
				case ItemID.LifeFruit:
					return rec.Grade == Grade.Champion && rec.LifeFruit < LevyConstants.MaxLifeFruit && rec.MaxHp < LevyConstants.GradeHpCap[(int)rec.Grade];
				case ItemID.ManaCrystal:
					return rec.MaxMana < LevyConstants.MaxMana;
				default:
					return false;
			}
		}

		/// <summary>Consumes one crystal from the local inventory and feeds it (the packet contract says the client consumes first).</summary>
		public static bool FeedCrystal(SoldierRecord rec, int itemType)
		{
			if (rec == null || !CanFeed(rec, itemType) || !Me.HasItem(itemType)) return false;
			if (Single)
			{
				if (!RosterSystem.Instance.FeedCrystal(rec, itemType)) return false;
				Me.ConsumeItem(itemType);
				return true;
			}
			Me.ConsumeItem(itemType);
			var p = LevyPackets.New(LevyPacket.FeedCrystal); p.Write(rec.Id); p.Write(itemType); p.Send();
			return true;
		}

		// ------------------------------------------------------------------ orders
		public static void Order(SoldierOrder order, bool atBarracks)
		{
			if (Single) RosterSystem.Instance.IssueOrder(Me, order, atBarracks);
			else { var p = LevyPackets.New(LevyPacket.Order); p.Write((byte)order); p.Write(atBarracks); p.Send(); }
		}

		public static void HoldFire(bool atBarracks)
		{
			if (Single) RosterSystem.Instance.ToggleHoldFire(Me, atBarracks);
			else { var p = LevyPackets.New(LevyPacket.HoldFire); p.Write(atBarracks); p.Send(); }
		}

		public static void Rally()
		{
			if (Single) RosterSystem.Instance.Rally(Me);
			else LevyPackets.New(LevyPacket.Rally).Send();
		}

		/// <summary>
		/// Order for ONE soldier. Single player: the live body takes it directly (or the record keeps it for the respawn).
		/// Multiplayer client: there is no per-soldier order packet, so the squad Order packet is sent instead (all of the
		/// commander's regulars). // CONTRACT-REQUEST: LevyPacket.SoldierOrder (int soldierId, byte order) handled by Roster.
		/// </summary>
		public static void SoldierOrder(SoldierRecord rec, SoldierOrder order)
		{
			if (rec == null) return;
			if (Single)
			{
				var body = BodyOf(rec);
				if (body != null) body.ReceiveOrder(order);
				else { rec.Order = order; if (order == Common.SoldierOrder.Hold) rec.HoldPos = rec.HomePos == Point16.NegativeOne ? Me.Center : rec.HomePos.ToWorldCoordinates(); }
				return;
			}
			Order(order, atBarracks: true);
		}

		// ------------------------------------------------------------------ settlement
		public static void Mobilize(BarracksTE te)
		{
			if (te == null) return;
			if (Single) SettlementSystem.Instance.Mobilize(te, Me);
			else { var p = LevyPackets.New(LevyPacket.Mobilize); p.Write(te.ID); p.Send(); }
		}

		public static void StandDown(BarracksTE te)
		{
			if (te == null) return;
			if (Single) SettlementSystem.Instance.StandDown(te);
			else { var p = LevyPackets.New(LevyPacket.StandDown); p.Write(te.ID); p.Send(); }
		}

		public static void ToggleMobilize(BarracksTE te) { if (te == null) return; if (te.Mobilized) StandDown(te); else Mobilize(te); }

		public static void RecruitingDrive(BarracksTE te)
		{
			if (te == null) return;
			if (Single) SettlementSystem.Instance.RecruitingDrive(te, Me);
			else { var p = LevyPackets.New(LevyPacket.RecruitingDrive); p.Write(te.ID); p.Send(); }
		}

		public static void HazardPay(BarracksTE te)
		{
			if (te == null) return;
			if (Single) SettlementSystem.Instance.HazardPay(te, Me);
			else { var p = LevyPackets.New(LevyPacket.HazardPay); p.Write(te.ID); p.Send(); }
		}

		/// <summary>which: 0 AutoMuster, 1 TaxToTreasury. The flag flips locally at once; the tile entity sync confirms it.</summary>
		public static void BarracksToggle(BarracksTE te, byte which, bool value)
		{
			if (te == null) return;
			if (which == 0) te.AutoMuster = value; else if (which == 1) te.TaxToTreasury = value; else return;
			if (!Single) { var p = LevyPackets.New(LevyPacket.BarracksToggle); p.Write(te.ID); p.Write(which); p.Write(value); p.Send(); }
		}

		// ------------------------------------------------------------------ treasury
		public static void Deposit(BarracksTE te, long copper)
		{
			if (te == null || copper <= 0) return;
			if (Single) { RosterSystem.Instance.Deposit(Me, te, copper); return; }
			if (!Me.BuyItem(copper)) return; // the client removes the coins; the server books them
			var p = LevyPackets.New(LevyPacket.Deposit); p.Write(te.ID); p.Write(copper); p.Send();
		}

		public static void Withdraw(BarracksTE te, long copper)
		{
			if (te == null || copper <= 0) return;
			if (Single) { RosterSystem.Instance.Withdraw(Me, te, copper); return; }
			var p = LevyPackets.New(LevyPacket.Withdraw); p.Write(te.ID); p.Write(copper); p.Send();
		}

		/// <summary>All coins in the local inventory (hotbar, main, coin slots) in copper. The mouse slot is ignored.</summary>
		public static long InventoryCoins()
		{
			long n = Utils.CoinsCount(out bool overflow, Me.inventory, LevyConstants.UI.InventoryMouseSlot);
			return overflow ? long.MaxValue / 2 : n;
		}

		// ------------------------------------------------------------------ armory
		public static Item[] ArmoryRegion(ArmoryTE te, byte region)
		{
			if (te == null) return null;
			return region switch { 0 => te.Storage, 1 => te.IssueWeapons, 2 => te.IssueArmor, 3 => te.IssueAmmo, 4 => te.IssuePotions, _ => null };
		}

		/// <summary>Multiplayer client: tells the server what this slot now holds. No-op in single player (the arrays are live).</summary>
		public static void SendArmorySlot(ArmoryTE te, byte region, int slot)
		{
			if (Single || te == null) return;
			var arr = ArmoryRegion(te, region);
			if (arr == null || slot < 0 || slot >= arr.Length) return;
			var p = LevyPackets.New(LevyPacket.ArmorySlotSet);
			p.Write(te.ID); p.Write(region); p.Write((short)slot);
			ItemIO.Send(arr[slot], p, writeStack: true);
			p.Send();
		}

		// ------------------------------------------------------------------ queries (client-side views)
		public static SoldierNPC BodyOf(SoldierRecord rec)
		{
			if (rec == null || !rec.HasBody) return null;
			return Main.npc[rec.BodyIndex].ModNPC as SoldierNPC;
		}

		/// <summary>The local player's regulars (on clients the display copy the roster keeps).</summary>
		public static IEnumerable<SoldierRecord> MyRegulars()
		{
			string name = Me.name;
			foreach (var r in RosterSystem.Instance.Regulars)
				if (r != null && r.Kind == SoldierKind.Regular && r.CommanderName == name) yield return r;
		}

		public static bool MajorityHoldFire()
		{
			int hold = 0, total = 0;
			foreach (var r in MyRegulars()) { total++; if (r.HoldFire) hold++; }
			return total > 0 && hold * 2 > total;
		}

		public static int AvailableCp => RosterSystem.Instance.CommandPoints - RosterSystem.Instance.CommandPointsUsed;

		/// <summary>Nearest barracks within <paramref name="tiles"/>: the settlement system first, a plain scan as the fallback.</summary>
		public static BarracksTE NearestBarracks(Vector2 worldPos, float tiles)
		{
			var te = SettlementSystem.Instance.NearestBarracks(worldPos, tiles);
			if (te != null) return te;
			float best = tiles * tiles * LevyConstants.TilePx * LevyConstants.TilePx;
			foreach (var b in SettlementSystem.Instance.AllBarracks())
			{
				float d = Vector2.DistanceSquared(b.Center, worldPos);
				if (d <= best) { best = d; te = b; }
			}
			return te;
		}

		/// <summary>Nearest barracks other than <paramref name="except"/>, measured from <paramref name="worldPos"/>. Null if none.</summary>
		public static BarracksTE NearestOtherBarracks(Vector2 worldPos, BarracksTE except)
		{
			BarracksTE best = null; float bestD = float.MaxValue;
			foreach (var b in SettlementSystem.Instance.AllBarracks())
			{
				if (ReferenceEquals(b, except)) continue;
				float d = Vector2.DistanceSquared(b.Center, worldPos);
				if (d < bestD) { bestD = d; best = b; }
			}
			return best;
		}

		/// <summary>Two nearest patrol banners to a position within the search radius; false if fewer than two exist.</summary>
		public static bool FindPatrolPair(Vector2 worldPos, float tiles, out Point16 a, out Point16 b)
		{
			a = b = Point16.NegativeOne;
			PatrolBannerTE first = null, second = null; float d1 = float.MaxValue, d2 = float.MaxValue;
			float max = tiles * tiles * LevyConstants.TilePx * LevyConstants.TilePx;
			foreach (var te in TileEntity.ByID.Values)
			{
				if (te is not PatrolBannerTE pb) continue;
				float d = Vector2.DistanceSquared(pb.Position.ToWorldCoordinates(), worldPos);
				if (d > max) continue;
				if (d < d1) { second = first; d2 = d1; first = pb; d1 = d; }
				else if (d < d2) { second = pb; d2 = d; }
			}
			if (first == null || second == null) return false;
			a = first.Position; b = second.Position;
			return true;
		}

		/// <summary>True while the tile entity still exists in the world under the same ID.</summary>
		public static bool IsLive(TileEntity te) => te != null && TileEntity.ByID.TryGetValue(te.ID, out var cur) && ReferenceEquals(cur, te);

		public static string StateText(SoldierRecord rec)
		{
			if (rec == null) return "";
			var body = BodyOf(rec);
			if (body != null) return LevyUtil.T("UI2.State." + body.State);
			if (rec.Dead) return LevyUtil.T("UI2.DeadRespawn", System.Math.Max(0, rec.RespawnTimer) / 60);
			return LevyUtil.T("UI2.NotDeployed");
		}

		public static void GetHp(SoldierRecord rec, out int cur, out int max)
		{
			var body = BodyOf(rec);
			if (body != null) { cur = body.NPC.life; max = System.Math.Max(1, body.NPC.lifeMax); return; }
			max = System.Math.Max(1, rec?.MaxHp ?? 1);
			cur = rec == null || rec.Dead ? 0 : System.Math.Clamp(rec.CurrentHp, 0, max);
		}

		public static string GradeName(Grade g) => g switch { Grade.Champion => LevyUtil.T("UI.GradeChampion"), Grade.ManAtArms => LevyUtil.T("UI.GradeManAtArms"), _ => LevyUtil.T("UI.GradeLevy") };
		public static string Chevrons(Grade g) => new string('^', (int)g + 1);
	}
}
