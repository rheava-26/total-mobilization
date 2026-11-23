using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common.Boss;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// ADAPT part of the Exo Mechs check: "Bosses adapt to armies" (Common/Boss/ArmyAwareExo.cs) on the real mechs, each scenario with the feature forced off and on in the same boot.
	///  * ARMS: Ares (beaten, so he takes the army) over a row of levies, the commander 200 tiles away. Off: his arms read the empty slot 255 (vanilla's null player at the world's corner) on Ares's levy lock. On: every
	///    arm reads a levy, and the point it reads stands on a levy.
	///  * ROCKET: an Apollo rocket above a levy in the same fight. Off: it curves toward the commander far away (Player.FindClosest). On: toward the levy.
	///  * VENTS: Thanatos over the levies. Off: with the commander out of his 2400 px scan no vent laser is fired at anyone. On: segments fire vent lasers at levies.
	///  * SOLO: Ares with no army and the feature on: nothing is lent or placed, and his arms read the commander as Calamity wrote them.
	/// </summary>
	public partial class ExoFixWorldCheck
	{
		private static bool? _adaptForce;
		private static int _adaptProbes, _adaptOnLevy, _adaptEmpty, _adaptCmd;

		private static void AdaptRestore()
		{
			ArmyAwareExo.Force = _adaptForce; ArmyAwareExo.ArmProbe = null;
			int ares = CalNpc("AresBody"), than = CalNpc("ThanatosHead");
			if (ares > 0) global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(ares, false);
			if (than > 0) global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(than, false);
		}

		private static void AdaptProbe(NPC arm, Player seen)
		{
			_adaptProbes++;
			if (seen == null || !seen.active) { _adaptEmpty++; return; }
			if (ReferenceEquals(seen, Main.player[0])) { _adaptCmd++; return; }
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC s && s.IsAlive && Vector2.Distance(n.Center, seen.Center) < 64f) { _adaptOnLevy++; return; } }
		}

		/// <summary>Calamity keeps the Exo Mechs' NPC slots in statics (CalamityGlobalNPC.draedonExoMechPrime, ...Worm, ...TwinGreen, ...TwinRed) and only checks that the slot is active: after ClearHostiles a levy that takes the slot reads as Ares. Forget them between scenarios.</summary>
		private static void ForgetExoSlots()
		{
			try
			{
				if (!Terraria.ModLoader.ModLoader.TryGetMod("CalamityMod", out var cal)) return;
				var g = cal.Code.GetType("CalamityMod.NPCs.CalamityGlobalNPC");
				foreach (string f in new[] { "draedonExoMechPrime", "draedonExoMechWorm", "draedonExoMechTwinGreen", "draedonExoMechTwinRed" }) g?.GetField(f, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.SetValue(null, -1);
			}
			catch (Exception) { }
		}

		private static List<SoldierNPC> AdaptRow(int n, float spanTiles)
		{
			var list = new List<SoldierNPC>();
			for (int i = 0; i < n; i++)
			{
				float x = Cx - spanTiles / 2f + spanTiles * i / Math.Max(1, n - 1);
				var s = Levy(new Vector2(x * 16f, FloorY * 16f), hp: 200000);
				if (s != null) { s.Record.Order = SoldierOrder.Hold; s.Record.HoldPos = s.NPC.Center; list.Add(s); }
			}
			return list;
		}

		private static void Adapt()
		{
			_adaptForce = ArmyAwareExo.Force;
			int ares = CalNpc("AresBody"), than = CalNpc("ThanatosHead"), rocket = CalProj("ApolloRocket"), laser = CalProj("ThanatosLaser");
			Check("army-aware Exo Mechs: all nine hooks are installed (four arms, three segment kinds, two homing projectiles)", ArmyAwareExo.Installed);
			if (ares <= 0 || than <= 0 || rocket <= 0 || laser <= 0 || !ArmyAwareExo.Installed) return;
			global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(ares, true); global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(than, true);
			PlaceCommander(-200);
			_perTick = () => { for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !n.friendly && n.ModNPC is not SoldierNPC) n.timeLeft = 99999; } };

			// ---- ARMS and ROCKET
			foreach (bool on in new[] { false, true })
			{
				ClearHostiles(); MakeRoom(40); ForgetExoSlots();
				ArmyAwareExo.Force = on; ArmyAwareExo.ResetCounters();
				var row = AdaptRow(8, 60f);
				NPC a = SpawnNpc(ares, new Vector2(Cx * 16f, (FloorY - 40) * 16f));
				if (a == null || row.Count < 4) { Check("ARMS: Ares and a row of levies spawn", false); return; }
				Step(90);
				_adaptProbes = _adaptOnLevy = _adaptEmpty = _adaptCmd = 0; ArmyAwareExo.ArmProbe = AdaptProbe;
				// the rocket: 30 tiles above the levy nearest the arena's centre, falling straight down; the commander is 200 tiles left
				SoldierNPC tgt = row.OrderBy(s => Math.Abs(s.NPC.Center.X - Cx * 16f)).First();
				Vector2 rAt = tgt.NPC.Center + new Vector2(0f, -30f * 16f);
				int ri = Projectile.NewProjectile(new Terraria.DataStructures.EntitySource_WorldEvent("ExoFixCheck"), rAt, new Vector2(0f, 6f), rocket, 100, 0f, Main.myPlayer, 0f, tgt.NPC.Center.Y);
				Step(20);
				ArmyAwareExo.ArmProbe = null;
				Projectile r = ri >= 0 && ri < Main.maxProjectiles && Main.projectile[ri].active && Main.projectile[ri].type == rocket ? Main.projectile[ri] : null;
				Vector2 toLevy = tgt.NPC.Center - (r?.Center ?? rAt), toCmd = Main.player[0].Center - (r?.Center ?? rAt);
				float cosLevy = r == null ? 0f : Vector2.Dot(Vector2.Normalize(r.velocity), Vector2.Normalize(toLevy)), cosCmd = r == null ? 0f : Vector2.Dot(Vector2.Normalize(r.velocity), Vector2.Normalize(toCmd));
				bool aresOnArmy = a.active && a.target == SoldierNPC.DummySlot;
				string st = $"Ares on the army {aresOnArmy}; arm calls {ArmyAwareExo.ArmCalls}, lent {ArmyAwareExo.ArmLent}, read the empty slot {ArmyAwareExo.ArmReadEmpty}; probes {_adaptProbes}: on a levy {_adaptOnLevy}, empty {_adaptEmpty}, commander {_adaptCmd}; rocket {(r == null ? "gone" : "alive")} cos to levy {cosLevy:0.00}, to commander {cosCmd:0.00}; homing calls {ArmyAwareExo.HomingCalls}, placed {ArmyAwareExo.HomingPlaced}";
				Note((on ? "feature on: " : "feature off: ") + st);
				if (!on)
				{
					Check("ARMS, feature off: with Ares on a levy his arms read the empty slot 255 (the behaviour the feature changes)", aresOnArmy && ArmyAwareExo.ArmCalls > 0 && _adaptProbes > 0 && _adaptEmpty * 10 >= _adaptProbes * 9 && ArmyAwareExo.ArmLent == 0, () => st);
					Check("ROCKET, feature off: an Apollo rocket curves toward the commander (Player.FindClosest)", r != null && cosCmd > cosLevy && ArmyAwareExo.HomingPlaced == 0, () => st);
				}
				else
				{
					Check("ARMS, feature on: every arm reads a levy, and the point it reads stands on a levy", aresOnArmy && ArmyAwareExo.ArmLent > 0 && _adaptProbes > 0 && _adaptOnLevy * 10 >= _adaptProbes * 9, () => st);
					Check("ROCKET, feature on: the rocket homes on the levy below it", r != null && cosLevy > cosCmd && ArmyAwareExo.HomingPlaced > 0, () => st);
				}
				Check($"stand-ins are gone after the fight's ticks ({(on ? "on" : "off")}): no levy stand-in is left in a player slot", !Enumerable.Range(0, Main.maxPlayers).Any(k => Main.player[k] != null && Main.player[k].active && k != 0 && Main.npc.Any(n => n.active && n.ModNPC is SoldierNPC sn && ReferenceEquals(sn.Dummy, Main.player[k]))));
			}

			// ---- VENTS
			foreach (bool on in new[] { false, true })
			{
				ClearHostiles(); MakeRoom(130); ForgetExoSlots();
				ArmyAwareExo.Force = on; ArmyAwareExo.ResetCounters();
				var row = AdaptRow(8, 60f);
				NPC t = SpawnNpc(than, new Vector2(Cx * 16f, (FloorY - 60) * 16f));
				if (t == null || row.Count < 4) { Check("VENTS: Thanatos and a row of levies spawn", false); return; }
				var seen = new HashSet<int>(); int lasers = 0, atLevy = 0, onArmy = 0;
				for (int k = 0; k < 1500 && lasers < 6; k++)
				{
					Step(1);
					if (t.active && t.target == SoldierNPC.DummySlot) onArmy++;
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile p = Main.projectile[i];
						if (!p.active || p.type != laser || !seen.Add(p.identity)) continue;
						lasers++;
						// Calamity's laser keeps its destination in its own Destination field (set from the spawn velocity on its first tick)
						Vector2 dest = p.velocity != Vector2.Zero ? p.velocity : p.Center;
						try { var f = p.ModProjectile.GetType().GetField("Destination"); if (f != null && f.GetValue(p.ModProjectile) is Vector2 dv && dv != Vector2.Zero) dest = dv; } catch (Exception) { }
						if (row.Any(s => s.NPC.active && Vector2.Distance(s.NPC.Center, dest) < 26f * 16f)) atLevy++;
					}
				}
				string st = $"lasers {lasers}, aimed within 26 tiles of a levy {atLevy}; vent calls {ArmyAwareExo.VentCalls}, stand-ins placed {ArmyAwareExo.VentPlaced}; ticks with Thanatos on the army {onArmy}";
				Note((on ? "VENTS feature on: " : "VENTS feature off: ") + st);
				if (!on) Check("VENTS, feature off: with Thanatos on the army no vent laser is aimed at a levy (Calamity's scan sees players only; the commander is out of reach)", onArmy > 0 && atLevy == 0 && ArmyAwareExo.VentPlaced == 0, () => st);
				else Check("VENTS, feature on: Thanatos's segments fire vent lasers at levies", ArmyAwareExo.VentPlaced > 0 && atLevy > 0, () => st);
			}

			// ---- SOLO
			{
				ClearHostiles(); MakeRoom(40); ForgetExoSlots();
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC) n.active = false; }
				ArmyAwareExo.Force = true; ArmyAwareExo.ResetCounters();
				PlaceCommander(-20);
				NPC a = SpawnNpc(ares, new Vector2(Cx * 16f, (FloorY - 40) * 16f));
				_adaptProbes = _adaptOnLevy = _adaptEmpty = _adaptCmd = 0; ArmyAwareExo.ArmProbe = AdaptProbe;
				Step(60);
				ArmyAwareExo.ArmProbe = null;
				string st = $"Ares target {a?.target}; arm calls {ArmyAwareExo.ArmCalls}, lent {ArmyAwareExo.ArmLent}; vent placed {ArmyAwareExo.VentPlaced}; homing placed {ArmyAwareExo.HomingPlaced}; probes {_adaptProbes}";
				Note("SOLO: " + st);
				Check("SOLO, feature on, no army: nothing is lent or placed (Ares and his arms fight the commander exactly as Calamity wrote them)", a != null && a.target == 0 && ArmyAwareExo.ArmLent == 0 && ArmyAwareExo.VentPlaced == 0 && ArmyAwareExo.HomingPlaced == 0 && _adaptProbes == 0, () => st);
				PlaceCommander(-200);
			}
		}
	}
}
