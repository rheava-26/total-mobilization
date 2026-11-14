using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Officers;
using Levy.Common.Systems;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Boot self-check of the pylon network, logged as "[PylonLink]" lines when a world loads on a dedicated server. Builds two barracks (each with an armory, a pylon and two town NPCs)
	/// 400 tiles apart, then asserts: with a pylon at each they link (barracks and armories, both directions); with a pylon at only one they do not; the Officers panel's candidate list
	/// holds the townsfolk of both towns; CanCommission accepts a far-town NPC from the near barracks, refuses a commander who is away from it, and refuses an NPC on no network.
	/// Everything it makes is removed again in the same call; nothing is saved.
	/// </summary>
	public class PylonLinkCheck : ModSystem
	{
		// OnWorldLoad, not a tick: an idle dedicated server (no client) does not step the world, and the check builds its own fixtures anyway.
		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1") return; // test boots only: the check plants fixtures in the loaded world, never in a player's server
			global::Levy.Common.Util.LoadSafe.Run(Run);
		}

		private void Run()
		{
			int pass = 0, fail = 0;
			void Check(string name, bool ok) { if (ok) pass++; else fail++; Mod.Logger.Info($"[PylonLink] {(ok ? "PASS" : "FAIL")} {name}"); }
			var made = new List<TileEntity>(); var npcs = new List<NPC>();
			try
			{
				const int idA = 900001, idB = 900002, idC = 900003, idArA = 900011, idArB = 900012;
				int ax = 500, bx = 900, y = 300; // 400 tiles apart
				BarracksTE Bar(int id, int x, string name) { var t = new BarracksTE { Position = new Point16(x, y), TownName = name, Treasury = 50_000_000 }; t.ID = id; TileEntity.ByID[id] = t; made.Add(t); return t; }
				ArmoryTE Arm(int id, int x) { var t = new ArmoryTE { Position = new Point16(x + 4, y) }; t.ID = id; TileEntity.ByID[id] = t; made.Add(t); return t; }
				var A = Bar(idA, ax, "PylonCheckWest"); var B = Bar(idB, bx, "PylonCheckEast");
				var armA = Arm(idArA, ax); var armB = Arm(idArB, bx);
				NPC Town(int type, BarracksTE home)
				{
					for (int i = Main.maxNPCs - 2; i >= 0; i--)
					{
						var n = Main.npc[i]; if (n.active) continue;
						n.SetDefaults(type); n.active = true; n.townNPC = true; n.homeless = false; n.homeTileX = home.Position.X + 2; n.homeTileY = home.Position.Y + 2; n.life = n.lifeMax;
						npcs.Add(n); return n;
					}
					return null;
				}
				NPC gA = Town(NPCID.Guide, A), zA = Town(NPCID.BestiaryGirl, A), nB = Town(NPCID.Nurse, B), dB = Town(NPCID.Demolitionist, B);
				Check("fixture built (2 barracks, 2 armories, 4 town NPCs)", gA != null && zA != null && nB != null && dB != null);
				TeleportPylonInfo Py(int x) => new TeleportPylonInfo { PositionInTiles = new Point16(x, y), TypeOfPylon = TeleportPylonType.SurfacePurity };
				var sys = SettlementSystem.Instance;
				var player = new Player { name = "PylonCheckCommander", active = true };
				void StandAt(BarracksTE t) => player.position = t.Center - new Vector2(player.width / 2f, player.height / 2f);
				StandAt(A);

				// no pylons: two lone settlements
				SettlementSystem.PylonListOverride = new List<TeleportPylonInfo>();
				Check("no pylons: barracks are separate settlements", sys.NetworkedBarracks(A).Count == 1 && sys.NetworkedBarracks(B).Count == 1);
				Check("no pylons: far-town NPC is not housed on the network", !sys.IsHousedOnNetwork(A, nB));
				Check("no pylons: CanCommission refuses a far-town NPC (NotHoused)", OfficerSystem.Instance.CanCommission(player, A, nB) == OfficerCheck.NotHoused);

				// a pylon by the west barracks only: still not linked
				SettlementSystem.PylonListOverride = new List<TeleportPylonInfo> { Py(ax + 6) };
				Check("pylon at one town only: not linked", sys.NetworkedBarracks(A).Count == 1 && sys.NetworkedBarracks(B).Count == 1 && sys.NetworkedArmories(armA).Count == 1);

				// a pylon by each: linked
				SettlementSystem.PylonListOverride = new List<TeleportPylonInfo> { Py(ax + 6), Py(bx + 6) };
				var netA = sys.NetworkedBarracks(A); var netB = sys.NetworkedBarracks(B);
				Check("pylons at both towns: barracks link both ways", netA.Count == 2 && netA[1] == B && netB.Count == 2 && netB[1] == A);
				Check("pylons at both towns: armories link both ways", sys.NetworkedArmories(armA).Contains(armB) && sys.NetworkedArmories(armB).Contains(armA));
				Check("pylons at both towns: network funds pool both treasuries", EconomySystem.NetworkFunds(A) == A.Treasury + B.Treasury);
				var cand = sys.NetworkTownNpcs(A);
				Check("network candidates list both towns, own town first", cand.Count == 4 && cand[0].Town == A && cand[1].Town == A && cand[2].Town == B && cand[3].Town == B);
				Check("far-town NPCs are housed on the network", sys.IsHousedOnNetwork(A, nB) && sys.IsHousedOnNetwork(B, gA));
				Check("CanCommission accepts a far-town NPC from this barracks", OfficerSystem.Instance.CanCommission(player, A, nB) == OfficerCheck.Ok);
				Check("CanCommission accepts an own-town NPC", OfficerSystem.Instance.CanCommission(player, A, gA) == OfficerCheck.Ok);
				StandAt(B);
				Check("CanCommission: commander at the other networked barracks may commission from the network", OfficerSystem.Instance.CanCommission(player, A, nB) == OfficerCheck.Ok);
				player.position = new Vector2((ax + 200) * 16f, y * 16f); // 200 tiles from both barracks and from both pylons
				Check("CanCommission: commander away from every barracks and pylon is too far", OfficerSystem.Instance.CanCommission(player, A, nB) == OfficerCheck.TooFar);
				player.position = new Vector2((bx + 6) * 16f, y * 16f); // beside the east pylon, 8 tiles from the east barracks
				SettlementSystem.PylonListOverride = new List<TeleportPylonInfo> { Py(ax + 6), Py(bx + 6) };
				Check("CanCommission: commander beside a networked pylon commissions", OfficerSystem.Instance.CanCommission(player, A, gA) == OfficerCheck.Ok);
				StandAt(A);

				// a town with a pylon and NO barracks, on the network: its townsfolk are recruitable, and the commander may stand at its pylon
				{
					int dx = 1250;
					SettlementSystem.PylonValidOverride = _ => true;
					var Dn = new Player { name = "PylonCheckCommanderD", active = true };
					NPC pn = null;
					for (int i = Main.maxNPCs - 2; i >= 0 && pn == null; i--)
					{
						var cand0 = Main.npc[i]; if (cand0.active) continue;
						cand0.SetDefaults(NPCID.Dryad); cand0.active = true; cand0.townNPC = true; cand0.homeless = false; cand0.homeTileX = dx + 3; cand0.homeTileY = y + 2; cand0.life = cand0.lifeMax; pn = cand0; npcs.Add(cand0);
					}
					SettlementSystem.PylonListOverride = new List<TeleportPylonInfo> { Py(ax + 6), Py(bx + 6), Py(dx) };
					sys.InvalidateTownNpcs();
					Dn.position = new Vector2(dx * 16f, y * 16f);
					Check("pylon town with no barracks: its townsperson is housed on the network", pn != null && sys.IsHousedOnNetwork(A, pn));
					var pc = sys.NetworkTownNpcs(A);
					Check("pylon town with no barracks: the officer list carries her, with no barracks and the pylon named", pn != null && pc.Exists(t => ReferenceEquals(t.Npc, pn) && t.Town == null && !string.IsNullOrEmpty(t.Pylon)));
					Check("pylon town with no barracks: CanCommission accepts her from the commander standing at her pylon", pn != null && OfficerSystem.Instance.CanCommission(Dn, A, pn) == OfficerCheck.Ok);
					Check("pylon town with no barracks: the pylon is a recruiting office for the settlement", sys.InRecruitingRange(Dn.Center, A, LevyConstants.Officer.ActionRangeTiles));
					SettlementSystem.PylonValidOverride = t => t.PositionInTiles.X != dx;  // her pylon has too few townsfolk to be active
					sys.InvalidateTownNpcs();
					Check("pylon town whose pylon is not active: not on the network (NotHoused)", pn != null && OfficerSystem.Instance.CanCommission(Dn, A, pn) == OfficerCheck.NotHoused);
					SettlementSystem.PylonValidOverride = null;
					SettlementSystem.PylonListOverride = new List<TeleportPylonInfo> { Py(ax + 6), Py(bx + 6) };
					sys.InvalidateTownNpcs();
					Check("pylon town with no pylon on the network: not housed on it (NotHoused)", pn != null && OfficerSystem.Instance.CanCommission(Dn, A, pn) == OfficerCheck.NotHoused);
					SettlementSystem.PylonListOverride = new List<TeleportPylonInfo> { Py(ax + 6), Py(bx + 6) };
				}
				StandAt(A);

				// a third, unlinked town
				var C = Bar(idC, 1500, "PylonCheckFar"); var cNpc = Town(NPCID.Nurse, C);
				Check("an unlinked third town is refused (NotHoused)", cNpc != null && OfficerSystem.Instance.CanCommission(player, A, cNpc) == OfficerCheck.NotHoused);
			}
			catch (Exception e) { Check("threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				SettlementSystem.PylonListOverride = null; SettlementSystem.PylonValidOverride = null;
				foreach (var t in made) TileEntity.ByID.Remove(t.ID);
				foreach (var n in npcs) { n.active = false; n.townNPC = false; }
			}
			Mod.Logger.Info($"[PylonLink] self-check pass={pass} fail={fail}");
		}
	}
}
