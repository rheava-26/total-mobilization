using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader.IO;
using Levy.Common.Net;
using Levy.Common.Officers;
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
		public static bool CanFeed(SoldierRecord rec, int itemType) => RosterSystem.CanAbsorbCrystal(rec, itemType);

		/// <summary>
		/// Consumes one crystal from the local inventory and feeds it. The packet contract says the client consumes first, so
		/// RosterSystem.FeedCrystal is assumed NOT to touch the commander's inventory; the UI consumes in single player too.
		/// // CONTRACT-REQUEST: RosterSystem.FeedCrystal doc should state it never consumes items (the caller does).
		/// </summary>
		public static bool FeedCrystal(SoldierRecord rec, int itemType)
		{
			if (rec == null || !CanFeed(rec, itemType) || !Me.HasItem(itemType)) return false;
			if (Single)
			{
				if (!RosterSystem.Instance.FeedCrystal(rec, itemType)) return false;
				Me.ConsumeItem(itemType);
				SoundEngine.PlaySound(RosterSystem.CrystalSound(itemType, out _));
				return true;
			}
			Me.ConsumeItem(itemType);
			var p = LevyPackets.New(LevyPacket.FeedCrystal); p.Write(rec.Id); p.Write(itemType); p.Send();
			SoundEngine.PlaySound(RosterSystem.CrystalSound(itemType, out _));
			return true;
		}

		// ------------------------------------------------------------------ officers
		/// <summary>Asks to conscript a housed town NPC as an officer, paid from <paramref name="te"/>. The server (or single player) validates and charges.</summary>
		public static void OfficerCommission(BarracksTE te, NPC npc)
		{
			if (te == null || npc == null) return;
			if (Single) OfficerSystem.Instance.Commission(Me, te, npc);
			else { var p = LevyPackets.New(LevyPacket.OfficerCommand); p.Write(OfficerNet.ActionCommission); p.Write(te.ID); p.Write((short)npc.whoAmI); p.Send(); }
		}

		/// <summary>Sends one of the local commander's officers back to town life.</summary>
		public static void OfficerDismiss(NPC npc)
		{
			if (npc == null) return;
			if (Single) OfficerSystem.Instance.Dismiss(Me, npc);
			else { var p = LevyPackets.New(LevyPacket.OfficerCommand); p.Write(OfficerNet.ActionDismiss); p.Write(-1); p.Write((short)npc.whoAmI); p.Send(); }
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

		/// <summary>Squad-wide toggle: wide loose formation on / off (same plumbing as <see cref="HoldFire"/>).</summary>
		public static void SpreadOut(bool atBarracks)
		{
			if (Single) RosterSystem.Instance.ToggleSpreadOut(Me, atBarracks);
			else { var p = LevyPackets.New(LevyPacket.SpreadOut); p.Write(atBarracks); p.Send(); }
		}

		/// <summary>Sets (or, with -1 / the same NPC again, clears) the squad's shared focus target. Client to server authoritative.</summary>
		public static void FocusTarget(int npcId)
		{
			UISystem.FocusActive = npcId >= 0;
			if (Single) RosterSystem.Instance.SetFocusTarget(Me, npcId);
			else { var p = LevyPackets.New(LevyPacket.FocusTarget); p.Write(npcId); p.Send(); }
		}

		/// <summary>Sets (or clears) the squad's Focus Attack world position. Client to server authoritative (the server clears it when the same spot is designated again).</summary>
		public static void FocusAttack(bool set, Vector2 world)
		{
			UISystem.FocusActive = set;
			if (Single) RosterSystem.Instance.SetFocusAttack(Me, set, world);
			else { var p = LevyPackets.New(LevyPacket.FocusAttack); p.Write(set); p.Write(world.X); p.Write(world.Y); p.Send(); }
		}

		/// <summary>True when the local commander has a focus standing. Authoritative in single player; the client mirror in multiplayer.</summary>
		public static bool FocusStanding()
			=> Single ? RosterSystem.HasFocus(Me.name) : UISystem.FocusActive;

		/// <summary>Calls the squad off any standing focus: a locked NPC and/or an attack position. Idempotent.</summary>
		public static void ClearFocus()
		{
			UISystem.FocusActive = false;
			if (Single)
			{
				RosterSystem.Instance.SetFocusTarget(Me, -1);
				RosterSystem.Instance.SetFocusAttack(Me, false, default);
			}
			else
			{
				var p = LevyPackets.New(LevyPacket.FocusTarget); p.Write(-1); p.Send();
				var q = LevyPackets.New(LevyPacket.FocusAttack); q.Write(false); q.Write(0f); q.Write(0f); q.Send();
			}
		}

		public static void Rally()
		{
			if (Single) RosterSystem.Instance.Rally(Me);
			else LevyPackets.New(LevyPacket.Rally).Send();
		}

		public static void Rearm()
		{
			if (Single) RosterSystem.Instance.Rearm(Me);
			else LevyPackets.New(LevyPacket.Rearm).Send();
		}

		/// <summary>Full loadout reset for every Regular: kit returned, every hand lock cleared, fresh issue. Server authoritative.</summary>
		public static void ResetLoadouts()
		{
			if (Single) RosterSystem.Instance.ResetLoadouts(Me);
			else LevyPackets.New(LevyPacket.ResetLoadouts).Send();
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

		/// <summary>Sorts the armory's bulk storage: single player directly, a multiplayer client asks the server (which re-syncs the tile entity).</summary>
		public static void SortArmory(ArmoryTE te)
		{
			if (te == null || !IsLive(te)) return;
			if (Single) { te.SortStorage(); return; }
			var p = LevyPackets.New(LevyPacket.ArmorySort); p.Write(te.ID); p.Send();
		}

		/// <summary>
		/// Bulk reforge of the armory's weapons up to a floor (index into LevyConstants.UI.ReforgeFloorTopFraction), paid per roll from the linked barracks treasury.
		/// Single player runs it directly; a multiplayer client asks the server, which reports back in chat and re-syncs both tile entities.
		/// </summary>
		public static void ReforgeArmory(ArmoryTE te, int floorIndex)
		{
			if (te == null || !IsLive(te)) return;
			if (Single) { ArmoryTE.ReportReforge(Me, te.ReforgeToFloor(te.LinkedBarracks(), floorIndex)); return; }
			var p = LevyPackets.New(LevyPacket.ArmoryReforge); p.Write(te.ID); p.Write((byte)floorIndex); p.Send();
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

		// ------------------------------------------------------------------ soldier loadout
		/// <summary>
		/// Hand-equip: the loadout panel has already put <paramref name="item"/> into the slot (taken from the mouse, so it came out of
		/// the player's own inventory) or emptied it (the old item is now on the mouse). Single player applies it to the record at once;
		/// a multiplayer client applies the same change to its display copy and sends <see cref="LevyPacket.SoldierGearSet"/> so the
		/// server makes it real. An ammo stack above the pouch limit leaves the excess in the player's inventory. Returns false (and
		/// hands the item back to the player) when the slot refuses it.
		/// </summary>
		public static bool SetGearSlot(SoldierRecord rec, int slot, Item item)
		{
			if (rec == null) return false;
			item = item == null || item.IsAir ? new Item() : item;
			if (Single)
			{
				if (RosterNet.ApplyGearSlot(rec, slot, item, out var extra)) { GiveToPlayer(extra); return true; }
			}
			else if (rec.Gear.TrySet(slot, item, rec.AccessorySlots, out var extra))
			{
				GiveToPlayer(extra);
				var p = LevyPackets.New(LevyPacket.SoldierGearSet);
				p.Write(rec.Id); p.Write((byte)slot);
				ItemIO.Send(rec.Gear.Get(slot), p, writeStack: true);
				p.Send();
				return true;
			}
			GiveToPlayer(item);
			return false;
		}

		/// <summary>Hands an item to the local player: inventory first, dropped at their feet if the pack is full.</summary>
		private static void GiveToPlayer(Item it)
		{
			if (it == null || it.IsAir || it.stack <= 0) return;
			var left = Me.GetItem(Main.myPlayer, it, GetItemSettings.InventoryEntityToPlayerInventorySettings);
			if (left != null && !left.IsAir && left.stack > 0) Me.QuickSpawnItem(Me.GetSource_Misc("Levy:Loadout"), left, left.stack);
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

		public static bool MajoritySpreadOut()
		{
			int spread = 0, total = 0;
			foreach (var r in MyRegulars()) { total++; if (r.SpreadOut) spread++; }
			return total > 0 && spread * 2 > total;
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
		// In multiplayer every TileEntitySharing (MessageID 86) replaces the stored instance, so a ReferenceEquals test would close
		// the panel on each resync. Liveness is by id + position + type instead; the panel reads the current instance through ByID.
		public static bool IsLive(TileEntity te) => te != null && TileEntity.ByID.TryGetValue(te.ID, out var cur) && cur.GetType() == te.GetType() && cur.Position == te.Position;

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
		/// <summary>Quality colour of a grade (roster card border and tint): grey-blue Levy, blue Man-at-Arms, gold Champion.</summary>
		public static Microsoft.Xna.Framework.Color GradeColor(Grade g) => g switch { Grade.Champion => LevyConstants.UI.GradeColorChampion, Grade.ManAtArms => LevyConstants.UI.GradeColorManAtArms, _ => LevyConstants.UI.GradeColorLevy };
	}
}
