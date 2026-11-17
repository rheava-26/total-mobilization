using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- the commander after a puppet scope
		// OwnerPuppet moves the commander's transform onto the soldier (updates, hits, kills, draws) and puts her use buttons up for the length of a shot's update. Whatever it does, she must be bit-identical
		// afterwards: this snapshots every Player field the puppet touches (and the ones a weapon reads around it) with her in the worst state (firing her own weapon, moving, mid-swing), runs bursts of soldier-shot
		// updates, hits, kills and draw-anchor reads with every puppet bit on, and compares after each call. Shots: every modded type whose AI reads the use buttons or whose draw reads the owner (a flail, a holdout) up
		// to a cap, plus a few vanilla ones. Env: LEVY_PUPPET=0 skips, LEVY_PUPPET_TYPES=N (default 10 per kind).

		private sealed class CmdSnap
		{
			public Vector2 Pos, Vel, Old; public int Dir, Anim, AnimMax, ItemTime, Sel, HeldType, HeldStack, Life, LifeMax, MouseX, MouseY; public bool Channel, Use, Tile, Release, Active, Dead; public float LifeSteal;
			public Vector2 Screen;
			public static CmdSnap Take(Player p) => new CmdSnap
			{
				Pos = p.position, Vel = p.velocity, Old = p.oldPosition, Dir = p.direction, Anim = p.itemAnimation, AnimMax = p.itemAnimationMax, ItemTime = p.itemTime, Sel = p.selectedItem,
				HeldType = p.HeldItem?.type ?? -1, HeldStack = p.HeldItem?.stack ?? -1, Life = p.statLife, LifeMax = p.statLifeMax2, MouseX = Main.mouseX, MouseY = Main.mouseY, Screen = Main.screenPosition,
				Channel = p.channel, Use = p.controlUseItem, Tile = p.controlUseTile, Release = p.releaseUseItem, Active = p.active, Dead = p.dead, LifeSteal = p.lifeSteal,
			};
			public string Diff(CmdSnap o)
			{
				var d = new List<string>();
				void F<T>(string n, T a, T b) { if (!EqualityComparer<T>.Default.Equals(a, b)) d.Add($"{n} {a} -> {b}"); }
				F("position", Pos, o.Pos); F("velocity", Vel, o.Vel); F("oldPosition", Old, o.Old); F("direction", Dir, o.Dir); F("itemAnimation", Anim, o.Anim); F("itemAnimationMax", AnimMax, o.AnimMax); F("itemTime", ItemTime, o.ItemTime);
				F("selectedItem", Sel, o.Sel); F("heldType", HeldType, o.HeldType); F("heldStack", HeldStack, o.HeldStack); F("life", Life, o.Life); F("lifeMax", LifeMax, o.LifeMax); F("mouseX", MouseX, o.MouseX); F("mouseY", MouseY, o.MouseY);
				F("screen", Screen, o.Screen); F("channel", Channel, o.Channel); F("controlUseItem", Use, o.Use); F("controlUseTile", Tile, o.Tile); F("releaseUseItem", Release, o.Release); F("active", Active, o.Active); F("dead", Dead, o.Dead);
				F("lifeSteal", LifeSteal, o.LifeSteal);
				return string.Join("; ", d);
			}
		}

		/// <summary>The commander mid-fight with her own weapon: buttons down, swinging, moving, facing left, a weapon in hand.</summary>
		private static void DirtyCommander(Player c)
		{
			c.active = true; c.dead = false; c.statLifeMax = c.statLifeMax2 = 500; c.statLife = 321;
			c.position = _cmdAt - new Vector2(c.width / 2f, c.height / 2f); c.velocity = new Vector2(3.5f, -2.25f); c.oldPosition = c.position - new Vector2(3.5f, -2.25f); c.direction = -1;
			c.itemAnimation = 11; c.itemAnimationMax = 20; c.itemTime = 7; c.channel = true; c.controlUseItem = true; c.controlUseTile = true; c.releaseUseItem = false; c.lifeSteal = 12.5f;
			c.selectedItem = 0; c.inventory[0] = new Item(); c.inventory[0].SetDefaults(ItemID.CopperShortsword); c.inventory[0].stack = 1;
			Main.mouseX = 640; Main.mouseY = 360; Main.screenPosition = new Vector2(1234f, 777f);
		}

		private static void PuppetCommanderCheck()
		{
			if (Environment.GetEnvironmentVariable("LEVY_PUPPET") == "0") return;
			int cap = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PUPPET_TYPES"), out int cp) ? cp : 10;
			int savedMask = OwnerPuppet.Mask; Item savedInv0 = Main.player[0].inventory[0]; int savedSel = Main.player[0].selectedItem; Vector2 savedScr = Main.screenPosition; int mx = Main.mouseX, my = Main.mouseY;
			var types = new List<(int type, string label)>();
			try
			{
				var btn = new List<(int, string)>(); var draw = new List<(int, string)>();
				for (int t = ProjectileID.Count; t < ProjectileLoader.ProjectileCount; t++)
				{
					ModProjectile mp = ContentSamples.ProjectilesByType.TryGetValue(t, out Projectile s) ? s.ModProjectile : null; if (mp == null) continue;
					if (btn.Count < cap && OwnerLockProbe.ReadsUseButtons(t)) btn.Add((t, "buttons:" + mp.Mod.Name + "/" + mp.Name));
					if (draw.Count < cap && OwnerLockProbe.ReadsOwnerAtDraw(t)) draw.Add((t, "draw:" + mp.Mod.Name + "/" + mp.Name));
					if (btn.Count >= cap && draw.Count >= cap) break;
				}
				types.AddRange(btn); types.AddRange(draw.Where(d => btn.All(b => b.Item1 != d.Item1)));
				foreach (int vt in new[] { ProjectileID.Bullet, ProjectileID.RocketI, ProjectileID.WoodenArrowFriendly, ProjectileID.VampireHeal }) types.Add((vt, "vanilla:" + ProjectileID.Search.GetName(vt)));
				OwnerPuppet.Mask = 255;
				SetupOriginArena(); ClearSoldiers(); ReleaseCommanderButtons(); _cmdHolds = false;
				Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
				Item w = new Item(); w.SetDefaults(ItemID.Musket);
				SoldierRecord rec = MakeOriginRecord(w, 0); RosterSystem.Instance.Regulars.Add(rec);
				int body = RosterSystem.Instance.SpawnBody(rec, at);
				if (body < 0 || !rec.HasBody) { Check("commander after the puppet: the levy spawned", false); return; }
				Main.npc[body].Bottom = at; KeepCommander(); Step(2);
				Player c = Main.player[0]; int bad = 0, calls = 0, kinds = 0; string first = null;
				int swaps0 = OwnerPuppet.Swaps, rest0 = OwnerPuppet.Restores, btn0 = OwnerPuppet.ButtonPuppets, draw0 = OwnerPuppet.DrawPuppets;
				void Compare(CmdSnap before, string what)
				{
					calls++; string d = before.Diff(CmdSnap.Take(c));
					if (OwnerPuppet.DebugDepth != 0) d += (d.Length > 0 ? "; " : "") + "puppet depth " + OwnerPuppet.DebugDepth;
					if (d.Length > 0) { bad++; first ??= what + ": " + d; }
				}
				foreach (var (type, label) in types)
				{
					kinds++;
					try
					{
						DirtyCommander(c);
						int pi = Projectile.NewProjectile(new EntitySource_Misc("PuppetCheck"), Main.npc[body].Center, new Vector2(6f, -1f), type, 25, 1f, 0);
						if (pi < 0 || pi >= Main.maxProjectiles) continue;
						Projectile p = Main.projectile[pi]; p.GetGlobalProjectile<SoldierProjectileTag>().SoldierIndex = body;
						for (int k = 0; k < 40 && p.active; k++)
						{
							DirtyCommander(c); CmdSnap s0 = CmdSnap.Take(c);
							p.Update(pi); Compare(s0, label + " Update #" + k);
							if (!p.active) break;
							DirtyCommander(c); s0 = CmdSnap.Take(c);
							bool moved = false; try { moved = OwnerPuppet.BeginDraw(p, force: true); Main.GetPlayerArmPosition(p); } finally { if (moved) OwnerPuppet.EndDraw(); }
							Compare(s0, label + " draw anchor #" + k);
							if (k % 8 == 3) { DirtyCommander(c); s0 = CmdSnap.Take(c); try { p.Damage(); } catch (Exception) { } Compare(s0, label + " Damage #" + k); }
						}
						if (p.active) { DirtyCommander(c); CmdSnap s1 = CmdSnap.Take(c); try { p.Kill(); } catch (Exception) { } Compare(s1, label + " Kill"); }
						// a draw scope that is opened and never closed by its caller (an exception in the draw): the next scope must still restore her, and EndDraw twice is harmless
						DirtyCommander(c); CmdSnap s2 = CmdSnap.Take(c);
						Projectile q = Main.projectile[pi]; q.active = true; q.type = type; q.owner = 0;
						try { if (OwnerPuppet.BeginDraw(q, force: true)) { try { throw new InvalidOperationException("draw failed"); } finally { OwnerPuppet.EndDraw(); } } } catch (InvalidOperationException) { }
						OwnerPuppet.EndDraw(); q.active = false; Compare(s2, label + " draw that throws");
					}
					catch (Exception e) { bad++; first ??= label + " threw " + e.GetType().Name + ": " + e.Message; }
					ClearShots();
				}
				// a burst through the real world tick as well: the levy fires a real weapon with the commander mid-swing; her state must be what the tick put her in (the only writer is KeepCommander)
				Check($"commander after the puppet: bit-identical after {calls} soldier-shot updates, hits, kills and draw-anchor reads over {kinds} shot types (position, velocity, oldPosition, direction, itemAnimation, itemTime, buttons, held item, life, cursor){(first != null ? " FIRST: " + first : "")}", bad == 0 && calls > 0);
				int swaps = OwnerPuppet.Swaps - swaps0, rest = OwnerPuppet.Restores - rest0, bp = OwnerPuppet.ButtonPuppets - btn0, dp = OwnerPuppet.DrawPuppets - draw0;
				Note($"puppet scopes opened in this check: transform {swaps}, restored {rest}, buttons {bp}, draw {dp}; button-reading shot types {types.Count(t => t.label.StartsWith("buttons"))}, owner-drawing {types.Count(t => t.label.StartsWith("draw"))}");
				Check($"commander after the puppet: the scopes really ran (transform swaps {swaps} = restores {rest}, buttons {bp}, draw {dp})", swaps > 0 && swaps == rest && (types.All(t => !t.label.StartsWith("buttons")) || bp > 0) && (types.All(t => !t.label.StartsWith("draw")) || dp > 0));
			}
			catch (Exception e) { Check("commander after the puppet threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				OwnerPuppet.Mask = savedMask; OwnerPuppet.EndDraw();
				Player c = Main.player[0]; c.inventory[0] = savedInv0 ?? new Item(); c.selectedItem = savedSel; Main.screenPosition = savedScr; Main.mouseX = mx; Main.mouseY = my;
				ReleaseCommanderButtons(); ClearSoldiers();
			}
		}

		private static void ClearShots() { for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false; }
	}
}
