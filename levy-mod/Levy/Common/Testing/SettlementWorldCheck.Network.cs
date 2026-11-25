using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Config;
using Levy.Common.Potions;
using Levy.Common.Systems;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Boot self-check of the shared manpower pool and the Recruitment Office, logged as "[SettlementWorld]" lines when a world loads on a dedicated server (LEVY_SELFCHECK_WORLD=1; fixtures only, removed again, nothing saved).
	/// Two barracks 400 tiles apart (3 and 4 housed townsfolk), a pylon town with no barracks (2), a lone barracks with no pylon (2), plus an Old Man, a Travelling Merchant, a town pet and a homeless NPC that must not count.
	/// Covers: the network head count across pylon-linked towns; the pool shared by every linked barracks (write on one, read on the other); the cap setting and the per-NPC setting; the Conscription ratio;
	/// an older world's per-barracks pools summed and clamped; a barracks with no pylon staying local; dawn regrowth once per pool; the daily drive once a day for the whole pool; the rush drive's price (4 x the wage) and
	/// availability with and without an Office; the Spectator's bet reading the shared pool; the save and net round trips; a new barracks joining without refilling the pool.
	/// </summary>
	public partial class SettlementWorldCheck : ModSystem
	{
		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1") return; // test boots only: the check plants fixtures in the loaded world, never in a player's server
			global::Levy.Common.Util.LoadSafe.Run(Network);
		}

		private void Network()
		{
			int pass = 0, fail = 0;
			void Check(string name, bool ok, Func<string> why = null) { if (ok) pass++; else fail++; Mod.Logger.Info($"[SettlementWorld] {(ok ? "PASS" : "FAIL")} {name}{(!ok && why != null ? " :: " + why() : "")}"); }
			var made = new List<TileEntity>(); var npcs = new List<NPC>();
			var cfg = LevyConfig.Get; int savedPer = cfg?.ManpowerPerTownNpc ?? 9, savedCap = cfg?.ManpowerCap ?? 300;
			var sys = SettlementSystem.Instance; bool savedConscription = sys.BookConscription;
			try
			{
				if (cfg == null) { Check("the config is loaded", false); return; }
				cfg.ManpowerPerTownNpc = 9; cfg.ManpowerCap = 300; sys.BookConscription = false;
				const int ax = 500, bx = 900, cx = 1250, dx = 1700, y = 300; // 400 tiles apart: no two settlement boxes touch
				BarracksTE Bar(int id, int x, string name) { var t = TestTileEntities.Add<BarracksTE>(id, new Point16(x, y)); t.TownName = name; t.Treasury = 50_000_000; made.Add(t); return t; }
				NPC Town(int type, int homeX, bool homeless = false)
				{
					for (int i = Main.maxNPCs - 2; i >= 0; i--)
					{
						var n = Main.npc[i]; if (n.active) continue;
						n.SetDefaults(type); n.active = true; n.townNPC = true; n.homeless = homeless; n.homeTileX = homeX; n.homeTileY = y + 2; n.life = n.lifeMax;
						npcs.Add(n); return n;
					}
					return null;
				}
				var A = Bar(910001, ax, "NetCheckWest"); var B = Bar(910002, bx, "NetCheckEast"); var D = Bar(910003, dx, "NetCheckLone");
				Town(NPCID.Guide, ax + 2); Town(NPCID.Nurse, ax + 3); Town(NPCID.Dryad, ax + 4);                                  // west: 3
				Town(NPCID.Merchant, bx + 2); Town(NPCID.Demolitionist, bx + 3); Town(NPCID.ArmsDealer, bx + 4); Town(NPCID.Clothier, bx + 5); // east: 4
				Town(NPCID.Wizard, cx + 2); Town(NPCID.Mechanic, cx + 3);                                                         // pylon town with no barracks: 2
				Town(NPCID.Painter, dx + 2); Town(NPCID.Stylist, dx + 3);                                                         // the lone barracks: 2
				Town(NPCID.OldMan, ax + 6); Town(NPCID.TravellingMerchant, ax + 7); Town(NPCID.TownCat, ax + 8); Town(NPCID.Angler, ax + 9, homeless: true); // none of these count
				TeleportPylonInfo Py(int x) => new TeleportPylonInfo { PositionInTiles = new Point16(x, y), TypeOfPylon = TeleportPylonType.SurfacePurity };
				List<TeleportPylonInfo> Pylons(params int[] xs) { var l = new List<TeleportPylonInfo>(); foreach (var x in xs) l.Add(Py(x)); return l; }
				void Net(params int[] pylonXs) { SettlementSystem.PylonListOverride = Pylons(pylonXs); sys.InvalidateTownNpcs(); sys.RefreshPool(); sys.InvalidateTownNpcs(); }
				SettlementSystem.PylonValidOverride = _ => true;
				Player player = null; // notices go to the log as a broadcast: a stand-in Player would be sent to a client slot that does not exist
				long wage = EconomySystem.LevyWage, daily = EconomySystem.RecruitingDrivePerPoint, rush = EconomySystem.RushRecruitingDrivePerPoint;
				int day = RosterSystem.Instance.DayNumber;

				// 1. no pylons: every barracks is its own pool, counted from its own townsfolk
				Net();
				Check("no pylons: no barracks is pooled", !A.OnPool && !B.OnPool && !D.OnPool);
				Check("no pylons: each barracks counts its own townsfolk (3, 4, 2)", sys.ManpowerNpcCount(A) == 3 && sys.ManpowerNpcCount(B) == 4 && sys.ManpowerNpcCount(D) == 2, () => $"{sys.ManpowerNpcCount(A)}, {sys.ManpowerNpcCount(B)}, {sys.ManpowerNpcCount(D)}");
				Check("no pylons: cap is 9 per townsperson (27, 36, 18)", sys.ManpowerCap(A) == 27 && sys.ManpowerCap(B) == 36 && sys.ManpowerCap(D) == 18, () => $"{sys.ManpowerCap(A)}, {sys.ManpowerCap(B)}, {sys.ManpowerCap(D)}");
				A.Manpower = 20; B.Manpower = 10; D.Manpower = 7;
				Check("no pylons: the pools are independent", A.Manpower == 20 && B.Manpower == 10 && D.Manpower == 7);

				// 2. an older world: per-barracks pools that are now network-linked are summed into one, clamped to the new cap
				Net(ax + 6, bx + 6);
				Check("pylons at both towns: both barracks join the shared pool, the lone one does not", A.OnPool && B.OnPool && !D.OnPool);
				Check("old save: the two pools are summed (20 + 10 = 30)", A.Manpower == 30 && B.Manpower == 30, () => $"A {A.Manpower}, B {B.Manpower}");
				Check("the network head count is 7 across the two linked towns, none of the excluded NPCs", sys.ManpowerNpcCount(A) == 7 && sys.ManpowerNpcCount(B) == 7, () => $"{sys.ManpowerNpcCount(A)}");
				Check("the network cap is 9 x 7 = 63", sys.ManpowerCap(A) == 63 && A.ManpowerCapCached == 63 && B.ManpowerCapCached == 63, () => $"{sys.ManpowerCap(A)} cached {A.ManpowerCapCached}/{B.ManpowerCapCached}");
				Check("the lone barracks is untouched (7 of 18)", D.Manpower == 7 && sys.ManpowerCap(D) == 18 && !D.OnPool);

				// 3. one shared pool: a write on either barracks is the same number on the other
				A.Manpower = 5;
				Check("writing the pool on one barracks reads the same on the other", B.Manpower == 5);
				B.Manpower -= 1;
				Check("a fallen regular's -1 on his home barracks is the network's -1", A.Manpower == 4 && B.Manpower == 4);
				Check("the lone barracks did not move", D.Manpower == 7);

				// 4. a pylon town with no barracks feeds the pool
				Net(ax + 6, bx + 6, cx);
				Check("a pylon town with no barracks adds its 2 townsfolk (9 in all)", sys.ManpowerNpcCount(A) == 9 && sys.ManpowerCap(A) == 81, () => $"{sys.ManpowerNpcCount(A)} cap {sys.ManpowerCap(A)}");
				Check("the pool did not change when the head count did", A.Manpower == 4 && B.Manpower == 4);

				// 5. config: the per-NPC setting and the cap
				cfg.ManpowerPerTownNpc = 40;
				Check("per-NPC 40 x 9 = 360 is held under the cap of 300", sys.ManpowerCap(A) == 300, () => sys.ManpowerCap(A).ToString());
				cfg.ManpowerCap = 100;
				Check("cap setting 100 holds the pool at 100", sys.ManpowerCap(A) == 100);
				cfg.ManpowerCap = 0;
				Check("cap setting 0 means no cap: 40 x 9 = 360", sys.ManpowerCap(A) == 360, () => sys.ManpowerCap(A).ToString());
				Check("the cap setting does not touch the lone barracks (40 x 2 = 80)", sys.ManpowerCap(D) == 80, () => sys.ManpowerCap(D).ToString());
				cfg.ManpowerPerTownNpc = 9; cfg.ManpowerCap = 300;
				A.Manpower = 70; sys.ManpowerCap(A);
				cfg.ManpowerCap = 50; int clamped = sys.ManpowerCap(A);
				Check("lowering the cap clamps the pool on every barracks", clamped == 50 && A.Manpower == 50 && B.Manpower == 50, () => $"cap {clamped}, A {A.Manpower}, B {B.Manpower}");
				cfg.ManpowerCap = 300; A.Manpower = 4;

				// 6. Conscription: 15/9 of the per-NPC setting, rounded
				int Con(int per) { cfg.ManpowerPerTownNpc = per; return sys.ManpowerPerNpcNow(true); }
				Check("Conscription ratio: 9 -> 15, 10 -> 17, 4 -> 7, 1 -> 2, 18 -> 30", Con(9) == 15 && Con(10) == 17 && Con(4) == 7 && Con(1) == 2 && Con(18) == 30, () => $"{Con(9)}, {Con(10)}, {Con(4)}, {Con(1)}, {Con(18)}");
				cfg.ManpowerPerTownNpc = 9; sys.BookConscription = true;
				Check("with the book the network cap is 15 x 9 = 135", sys.ManpowerCap(A) == 135, () => sys.ManpowerCap(A).ToString());
				sys.BookConscription = false;

				// 7. older world, pools too big for the cap: summed, then clamped
				Net(); A.Manpower = 50; B.Manpower = 60;
				Net(ax + 6, bx + 6, cx);
				Check("old save: 50 + 60 = 110 is clamped to the network cap of 81", A.OnPool && B.OnPool && A.Manpower == 81 && B.Manpower == 81, () => $"A {A.Manpower}, B {B.Manpower}");

				// 8. the pool regrows once at dawn from the whole network; the lone barracks regrows on its own
				A.Manpower = 10; D.Manpower = 7;
				sys.OnDawn(day + 1);
				Check("dawn: the shared pool gains 1 per 2 townsfolk once (9 -> +5), not once per barracks", A.Manpower == 15 && B.Manpower == 15, () => $"A {A.Manpower}, B {B.Manpower}");
				Check("dawn: the lone barracks gains its own +1", D.Manpower == 8, () => D.Manpower.ToString());

				// 9. the daily recruiting drive refills the shared pool, once a day for the whole network
				A.Manpower = 61; A.Treasury = 50_000_000; B.Treasury = 0; A.RecruitingDriveDay = B.RecruitingDriveDay = -1; long before = A.Treasury;
				bool ok = sys.RecruitingDrive(A, player);
				Check("daily drive: refills the shared pool to the cap (61 -> 81) at 2 x wage a point", ok && A.Manpower == 81 && B.Manpower == 81 && before - A.Treasury == 20 * daily, () => $"ok {ok}, pool {A.Manpower}, paid {before - A.Treasury} of {20 * daily}");
				A.Manpower = 70;
				Check("daily drive: the other barracks of the network cannot run a second one the same day", !sys.RecruitingDrive(B, player) && B.Manpower == 70);

				// 10. the Recruitment Office: each has its own rest of one in-game day; every rush drive since dawn doubles the price
				int rushWas = sys.RushDrivesToday; double clockWas = WorldClock.Now; sys.RushDrivesToday = 0;
				Check("rush price is 4 x the wage, twice the daily price", rush == 4 * wage && rush == 2 * daily && sys.RushPriceNow == rush, () => $"rush {rush}, wage {wage}, daily {daily}");
				Check("no Office: the settlement is not served", !sys.HasRecruitmentOffice(A) && !sys.HasRecruitmentOffice(B) && !sys.HasRecruitmentOffice(D));
				before = A.Treasury; bool noOffice = sys.RushRecruitingDrive(A, player);
				Check("no Office: a rush drive is refused and costs nothing", !noOffice && A.Manpower == 70 && A.Treasury == before && sys.RushDrivesToday == 0);
				var office = TestTileEntities.Add<RecruitmentOfficeTE>(910020, new Point16(cx + 10, y)); made.Add(office); // in the pylon town, far from both barracks
				Check("an Office anywhere on the pylon network serves the pooled barracks (both)", sys.HasRecruitmentOffice(A) && sys.HasRecruitmentOffice(B));
				Check("that Office does not serve the lone barracks", !sys.HasRecruitmentOffice(D));
				Check("a new Office is ready", office.RestTicks == 0);
				A.Treasury = 5 * rush + 1; B.Treasury = 0; before = A.Treasury;
				ok = sys.RushRecruitingDrive(A, player);
				Check("first rush drive of the day: with 5 points of money it buys 5 points at 4 x the wage", ok && A.Manpower == 75 && before - A.Treasury == 5 * rush, () => $"ok {ok}, pool {A.Manpower}, paid {before - A.Treasury} of {5 * rush}");
				Check("the Office now rests one in-game day and the day's count is 1", Math.Abs(office.RestTicks - 86400) < 0.5 && sys.RushDrivesToday == 1 && sys.RushPriceNow == 2 * rush, () => $"rest {office.RestTicks}, count {sys.RushDrivesToday}");
				Check("rush drive: leaves the daily drive's day alone (the daily drive ran today and still reads so)", A.RecruitingDriveDay == day && B.RecruitingDriveDay == day, () => $"A {A.RecruitingDriveDay}, B {B.RecruitingDriveDay}, today {day}");
				A.Treasury = 50_000_000; A.Manpower = 60; before = A.Treasury;
				Check("a resting Office runs nothing, spends nothing and does not change the price", !sys.RushRecruitingDrive(A, player) && !sys.RushDriveAt(office, player) && A.Manpower == 60 && A.Treasury == before && sys.RushDrivesToday == 1);
				WorldClock.Now += 3600 * 23;
				Check("23 in-game hours later it still rests (1 left)", Math.Abs(office.RestTicks - 3600) < 0.5 && !sys.RushRecruitingDrive(A, player));
				WorldClock.Now += 3600;
				Check("after one in-game day the Office is ready", office.RestTicks == 0);
				ok = sys.RushRecruitingDrive(A, player); // fills 60 -> 81 at the doubled price
				Check("the second rush drive of the day costs twice as much (21 points at 2 x 4 x wage)", ok && A.Manpower == 81 && before - A.Treasury == 21 * 2 * rush && sys.RushDrivesToday == 2, () => $"ok {ok}, pool {A.Manpower}, paid {before - A.Treasury} of {21 * 2 * rush}");
				var officeB = TestTileEntities.Add<RecruitmentOfficeTE>(910023, new Point16(cx + 14, y)); made.Add(officeB);
				A.Manpower = 40; before = A.Treasury;
				ok = sys.RushRecruitingDrive(A, player); // the first Office rests, the second is ready at once
				Check("a second Office runs at once (its own rest) at the next price: 41 points at 4 x 4 x wage", ok && A.Manpower == 81 && before - A.Treasury == 41 * 4 * rush && sys.RushDrivesToday == 3 && officeB.RestTicks > 0, () => $"ok {ok}, pool {A.Manpower}, paid {before - A.Treasury} of {41 * 4 * rush}, count {sys.RushDrivesToday}");
				A.Manpower = 40;
				Check("both Offices resting: the next drive is refused", !sys.RushRecruitingDrive(A, player) && A.Manpower == 40 && sys.RushDrivesToday == 3);
				var officeC = TestTileEntities.Add<RecruitmentOfficeTE>(910024, new Point16(cx + 18, y)); made.Add(officeC);
				A.Manpower = 81; B.Treasury = 50_000_000; before = B.Treasury;
				Check("at the cap there is nothing to buy: no money spent, no rest, no count", !sys.RushDriveAt(officeC, player) && B.Treasury == before && officeC.RestTicks == 0 && sys.RushDrivesToday == 3);
				A.Manpower = 50; B.Treasury = 8 * rush - 1; // a point costs 8 x rush now
				Check("too little money buys nothing: no rest, no count", !sys.RushDriveAt(officeC, player) && A.Manpower == 50 && officeC.RestTicks == 0 && sys.RushDrivesToday == 3);
				B.Treasury = 50_000_000; before = B.Treasury;
				ok = sys.RushDriveAt(officeC, player); // right-click on the third Office: 31 points at 8 x rush
				Check("right-click on a third Office: paid from the served barracks at the price after three drives (31 points at 8 x 4 x wage)", ok && A.Manpower == 81 && before - B.Treasury == 31 * 8 * rush && sys.RushDrivesToday == 4, () => $"ok {ok}, pool {A.Manpower}, paid {before - B.Treasury} of {31 * 8 * rush}");
				sys.OnDawn(day + 1);
				Check("dawn: the price is back to 4 x the wage; the Offices keep resting", sys.RushDrivesToday == 0 && sys.RushPriceNow == rush && officeB.RestTicks > 0 && officeC.RestTicks > 0);
				B.Treasury = 50_000_000; A.Manpower = 10; before = B.Treasury;
				WorldClock.Now += 86400; // a day on: all three are ready again
				ok = sys.RushDriveAt(office, player);
				Check("after dawn a drive costs the base price again (71 points at 4 x wage)", ok && before - B.Treasury == 71 * rush && A.Manpower == 81 && sys.RushDrivesToday == 1, () => $"ok {ok}, paid {before - B.Treasury} of {71 * rush}");
				TestTileEntities.Remove(officeB); made.Remove(officeB); TestTileEntities.Remove(officeC); made.Remove(officeC);
				sys.RushDrivesToday = 0; WorldClock.Now += 86400;
				A.Manpower = 10; A.Treasury = rush - 1; bool poor = sys.RushRecruitingDrive(A, player);
				Check("rush drive: a treasury short of one point's price buys nothing", !poor && A.Manpower == 10);
				var office2 = TestTileEntities.Add<RecruitmentOfficeTE>(910021, new Point16(dx + 8, y)); made.Add(office2); // inside the lone barracks's box
				Check("an Office in the lone barracks's box serves that barracks", sys.HasRecruitmentOffice(D));
				D.Manpower = 3; D.Treasury = 50_000_000; before = D.Treasury; ok = sys.RushRecruitingDrive(D, player);
				Check("lone barracks: rush refills its own pool (3 -> 18) at the day's price (4 x the wage)", ok && D.Manpower == 18 && before - D.Treasury == 15 * rush && A.Manpower == 10, () => $"ok {ok}, pool {D.Manpower}, paid {before - D.Treasury}, network pool {A.Manpower}");
				Check("the price is one count for the whole world: the lone barracks's drive made the next one cost twice", sys.RushDrivesToday == 1 && sys.RushPriceNow == 2 * rush);
				// right-click on an Office: it picks its Barracks itself and runs the drive
				Check("an Office in the pylon town serves the nearest pooled barracks (the east one)", ReferenceEquals(sys.BarracksServedBy(office), B), () => sys.BarracksServedBy(office)?.TownName ?? "null");
				Check("an Office in the lone barracks's box serves the lone barracks", ReferenceEquals(sys.BarracksServedBy(office2), D));
				var lost = TestTileEntities.Add<RecruitmentOfficeTE>(910022, new Point16(3000, y)); made.Add(lost);
				Check("an Office with no Barracks and no pylon in reach serves nothing, and right-click says so without spending", sys.BarracksServedBy(lost) == null && !sys.RushDriveAt(lost, null) && lost.RestTicks == 0);
				TestTileEntities.Remove(lost); made.Remove(lost);
				TestTileEntities.Remove(office); made.Remove(office);
				Check("Office removed from the network: the pooled barracks are no longer served", !sys.HasRecruitmentOffice(A) && !sys.HasRecruitmentOffice(B) && sys.HasRecruitmentOffice(D));
				TestTileEntities.Remove(office2); made.Remove(office2);
				B.Treasury = 50_000_000; sys.RushDrivesToday = rushWas; WorldClock.Now = clockWas;

				// 11. the Spectator's bet and the roster read the shared pool
				A.Manpower = 0; D.Manpower = 0;
				Check("Spectator: with the shared pool and the lone pool empty nobody can be fielded", !SpectatorBet.CanFieldMore(new Player { name = "NetCheckCommander", active = true }));
				B.Manpower = 1;
				Check("Spectator: one man in the shared pool, read through the other barracks, can still be fielded", SpectatorBet.CanFieldMore(new Player { name = "NetCheckCommander", active = true }) && A.Manpower == 1);
				B.Manpower = 0; D.Manpower = 2;
				Check("Spectator: a man in the lone pool counts too", SpectatorBet.CanFieldMore(new Player { name = "NetCheckCommander", active = true }));

				// 12. a barracks that leaves the network keeps what the pool held; the one that stays is unchanged
				A.Manpower = 30; D.Manpower = 0;
				Net(ax + 6, cx);
				Check("pylon removed from the east town: that barracks leaves the pool and keeps 30", A.OnPool && !B.OnPool && B.Manpower == 30 && A.Manpower == 30, () => $"A pooled {A.OnPool}/{A.Manpower}, B pooled {B.OnPool}/{B.Manpower}");
				A.Manpower = 12;
				Check("after leaving, the two no longer share (A 12, B 30)", B.Manpower == 30 && A.Manpower == 12);
				Check("a lone pooled barracks counts the network it is on: west 3 + pylon town 2 = 5", sys.ManpowerNpcCount(A) == 5, () => sys.ManpowerNpcCount(A).ToString());
				Net(ax + 6, bx + 6, cx);
				Check("pylon back: they join again, 12 + 30 = 42", A.OnPool && B.OnPool && A.Manpower == 42 && B.Manpower == 42, () => $"A {A.Manpower}, B {B.Manpower}");

				// 13. a barracks placed on the network joins at the pool's level: placing one never refills the pool
				A.Manpower = 17;
				var E = Bar(910004, ax + 20, "NetCheckNew"); E.Manpower = 0;
				sys.InitManpower(E);
				Check("a new linked barracks joins the pool at its level (17), not full", E.OnPool && E.Manpower == 17 && A.Manpower == 17 && B.Manpower == 17, () => $"E {E.OnPool}/{E.Manpower}, A {A.Manpower}");
				Check("and the head count did not change (still 9)", sys.ManpowerNpcCount(A) == 9, () => sys.ManpowerNpcCount(A).ToString());
				var F = Bar(910005, 2200, "NetCheckAlone"); F.Manpower = 0; Town(NPCID.Truffle, 2202); Town(NPCID.Steampunker, 2203);
				sys.InvalidateTownNpcs(); sys.InitManpower(F);
				Check("a new barracks with no pylon starts full on its own (18)", !F.OnPool && F.Manpower == 18, () => $"{F.OnPool}/{F.Manpower}");

				// 14. save and net round trips
				var tag = new TagCompound(); A.SaveData(tag);
				var A2 = TestTileEntities.Make<BarracksTE>(new Point16(ax, y)); A2.LoadData(tag);
				Check("save round trip: the pool flag and the pool survive", A2.OnPool && A2.Manpower == 17);
				var old = new TagCompound { ["manpower"] = 25 }; var A3 = TestTileEntities.Make<BarracksTE>(new Point16(ax, y)); A3.LoadData(old);
				Check("an older save has no pool flag: the barracks loads unpooled with its own manpower (25)", !A3.OnPool && A3.Manpower == 25);
				using (var ms = new MemoryStream())
				{
					using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) A.NetSend(w);
					ms.Position = 0; var A4 = TestTileEntities.Make<BarracksTE>(new Point16(ax, y));
					using (var r = new BinaryReader(ms)) A4.NetReceive(r);
					Check("net round trip: the pool flag, pool and cap reach a client", A4.OnPool && A4.Manpower == 17 && A4.ManpowerCapCached == A.ManpowerCapCached, () => $"{A4.OnPool}/{A4.Manpower}/{A4.ManpowerCapCached}");
				}

				// 15. the item, its recipe and every string the feature shows
				int officeItem = ModContent.ItemType<Content.Items.Placeables.RecruitmentOfficeItem>();
				Recipe rec = null; foreach (var r in Main.recipe) if (r != null && r.createItem != null && r.createItem.type == officeItem) { rec = r; break; }
				Check("Recruitment Office recipe: an Enchanted Sundial and materials, at a Work Bench", rec != null && rec.HasIngredient(ItemID.Sundial) && rec.HasTile(TileID.WorkBenches), () => rec == null ? "no recipe" : "missing sundial or bench");
				string[] keys = { "Items.RecruitmentOfficeItem.DisplayName", "Items.RecruitmentOfficeItem.Tooltip", "Tiles.RecruitmentOfficeTile.MapEntry", "UI.ManpowerNet", "UI.ManpowerNetShort", "UI2.RecruitingDriveHint", "UI2.RecruitingDriveHintNet",
					"Chat.RushRecruitingDrive", "Militia.NoRecruitmentOffice", "Militia.OfficeNoBarracks", "Militia.OfficeResting", "Militia.OfficeHoverReady", "Militia.OfficeHoverResting", "Guide.Pages.Recruiting.Title", "Guide.Pages.Recruiting.Body", "Configs.LevyConfig.ManpowerPerTownNpc.Label", "Configs.LevyConfig.ManpowerPerTownNpc.Tooltip", "Configs.LevyConfig.ManpowerCap.Label", "Configs.LevyConfig.ManpowerCap.Tooltip" };
				var missing = new List<string>();
				foreach (var k in keys) { string full = "Mods.Levy." + k; string v = Terraria.Localization.Language.Exists(full) ? Terraria.Localization.Language.GetTextValue(full) : null; if (string.IsNullOrWhiteSpace(v) || v == full || v.StartsWith("Mods.Levy.")) missing.Add(k); }
				Check("every string the pool and the Office show resolves", missing.Count == 0, () => string.Join(", ", missing));
				string net = global::Levy.Common.Util.LevyUtil.T("UI.ManpowerNet", 214, 300, 31);
				Check("the panel text reads 'Manpower 214 / 300 · 31 townsfolk across the pylon network'", net == "Manpower 214 / 300 · 31 townsfolk across the pylon network", () => net);
				string tip = global::Levy.Common.Util.LevyUtil.T("Items.RecruitmentOfficeItem.Tooltip");
				Check("the tooltip says what a rush drive is, its rest and what it costs", tip.Contains("rush recruiting drive") && tip.Contains("Right click") && tip.Contains("one in-game day") && tip.Contains("4 times the daily wage") && tip.Contains("doubled"), () => tip);
				string rushChat = global::Levy.Common.Util.LevyUtil.T("Chat.RushRecruitingDrive", "Town", 12, "1 gold", "2 gold");
				Check("the rush chat line says the price of the next drive", rushChat.Contains("12 recruits") && rushChat.Contains("next rush drive costs 2 gold"), () => rushChat);
				string resting = global::Levy.Common.Util.LevyUtil.T("Militia.OfficeResting", "13h 20m"), hoverR = global::Levy.Common.Util.LevyUtil.T("Militia.OfficeHoverReady", "1 gold"), hoverT = global::Levy.Common.Util.LevyUtil.T("Militia.OfficeHoverResting", "13h 20m");
				Check("the hover and the right-click text read as time left or price", resting.Contains("13h 20m") && hoverR.Contains("1 gold") && hoverT.Contains("13h 20m"), () => resting + " / " + hoverR + " / " + hoverT);
			}
			catch (Exception e) { Check("threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				SettlementSystem.PylonListOverride = null; SettlementSystem.PylonValidOverride = null;
				foreach (var t in made) TestTileEntities.Remove(t);
				foreach (var n in npcs) { n.active = false; n.townNPC = false; }
				if (cfg != null) { cfg.ManpowerPerTownNpc = savedPer; cfg.ManpowerCap = savedCap; }
				sys.BookConscription = savedConscription; sys.InvalidateTownNpcs();
			}
			Mod.Logger.Info($"[SettlementWorld] self-check pass={pass} fail={fail}");
		}
	}
}
