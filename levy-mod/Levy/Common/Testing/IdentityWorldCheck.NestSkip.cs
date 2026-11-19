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
	/// <summary>Test seam of the nested hit-pass check: the one projectile it names heals its owner on every hit (Player.Heal, or a bare statLife +=) and records the owner's life the hit saw. Inert otherwise.</summary>
	public sealed class NestSkipTestHeal : GlobalProjectile
	{
		internal static int Proj = -1, Heal, Mode, Hits; internal static readonly List<int> Seen = new();
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Heal <= 0 || projectile.whoAmI != Proj) return;
			Player o = Main.player[projectile.owner]; Hits++; Seen.Add(o.statLife);
			if (Mode == 1) o.Heal(Heal); else o.statLife += Heal;
		}
	}

	public partial class IdentityWorldCheck
	{
		/// <summary>
		/// THE NESTED HIT PASS (OwnerPuppet.CanSkipNested). A levy's shot with three hit passes an update (extraUpdates 2, a hit every pass) heals its owner on every hit; the same shot, from the same state, runs eight updates with the
		/// nested swap skipped and with it kept. The levy's life after, the life each hit saw, the hits, and the commander's own life must be equal; with the skip on the hit passes must have been skipped, with it off none.
		/// Both heal paths (Player.Heal, clamped at the max; a bare statLife +=, clamped only when the levy is given the gain), with room to heal and at the cap.
		/// </summary>
		private static void NestSkipCheck()
		{
			bool was = OwnerPuppet.NestSkipOn; int my = Main.myPlayer;
			try
			{
				SetupOriginArena(); ClearSoldiers();
				Item w = new Item(); w.SetDefaults(ItemID.Musket);
				SoldierRecord rec = MakeOriginRecord(w, 0); RosterSystem.Instance.Regulars.Add(rec);
				Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
				int body = RosterSystem.Instance.SpawnBody(rec, at);
				if (body < 0 || !rec.HasBody) { Check("nested hit pass: the levy spawned", false); return; }
				Main.npc[body].Bottom = at; KeepCommander(); Step(2);
				NPC b = Main.npc[body];
				int fails = 0, cases = 0, skipsOn = 0, skipsOff = 0; string first = null;
				foreach (int mode in new[] { 1, 2 })
					foreach (int startFrac in new[] { 20, 94 })
					{
						var r = new (int life, int routed, int hits, List<int> seen, int skips, int cmdLife, int cmdMax)[2];
						for (int arm = 0; arm < 2; arm++)
						{
							OwnerPuppet.NestSkipOn = arm == 1;
							KeepCommander(); Player c = Main.player[0]; int cl = c.statLife, cm = c.statLifeMax2;
							b.life = Math.Max(1, b.lifeMax * startFrac / 100); if (rec != null) rec.CurrentHp = b.life;
							SpawnOriginTarget(b.Bottom); NPC tg = _sweepTarget; int t = tg.whoAmI;
							Main.myPlayer = 0;
							int pi = Projectile.NewProjectile(new EntitySource_Misc("NestSkip"), tg.Center, Vector2.Zero, ProjectileID.Bullet, 10, 0f, 0);
							Projectile p = Main.projectile[pi];
							p.GetGlobalProjectile<SoldierProjectileTag>().SoldierIndex = body;
							p.penetrate = -1; p.maxPenetrate = -1; p.extraUpdates = 2; p.usesLocalNPCImmunity = true; p.localNPCHitCooldown = 0; p.usesIDStaticNPCImmunity = false; /* Calamity gives bullets a per-type cooldown counted in game ticks */ p.tileCollide = false; p.timeLeft = 600; p.friendly = true; p.hostile = false;
							NestSkipTestHeal.Proj = pi; NestSkipTestHeal.Heal = 17; NestSkipTestHeal.Mode = mode; NestSkipTestHeal.Hits = 0; NestSkipTestHeal.Seen.Clear();
							int routed0 = OwnerPuppet.RoutedLife, sk0 = OwnerPuppet.NestSkips;
							for (int k = 0; k < 8 && p.active; k++)
							{
								tg.Center = p.Center; tg.velocity = Vector2.Zero; tg.life = tg.lifeMax; p.velocity = Vector2.Zero;
								global::Levy.Common.Targeting.SoldierTargeting.TestForgetNeverCache(); p.Update(pi); // the target was set up inside this tick: its per-tick "never a target" answer is taken afresh
							}
							r[arm] = (b.life, OwnerPuppet.RoutedLife - routed0, NestSkipTestHeal.Hits, NestSkipTestHeal.Seen.ToList(), OwnerPuppet.NestSkips - sk0, c.statLife - cl, c.statLifeMax2 - cm);
							NestSkipTestHeal.Proj = -1; NestSkipTestHeal.Heal = 0; p.active = false; tg.active = false; _sweepTarget = null; Main.myPlayer = my;
						}
						cases++; skipsOff += r[0].skips; skipsOn += r[1].skips;
						string lbl = (mode == 1 ? "Player.Heal" : "statLife +=") + " from " + startFrac + "% life";
						Note($"nested hit pass, {lbl}: kept -> levy life {r[0].life}, routed {r[0].routed}, hits {r[0].hits}, life seen [{string.Join(",", r[0].seen.Take(12))}]; skipped -> levy life {r[1].life}, routed {r[1].routed}, hits {r[1].hits}, skips {r[1].skips}, life seen [{string.Join(",", r[1].seen.Take(12))}]");
						// the life a later hit of the same update reads: the same with Player.Heal (clamped at the max on the spot); a bare += can read above the max while the gain waits for the update's end (the levy's total is clamped the same)
						bool seenOk = mode == 2 || r[0].seen.SequenceEqual(r[1].seen);
						if (mode == 2 && !r[0].seen.SequenceEqual(r[1].seen)) Note($"nested hit pass, {lbl}: a later hit of the same update read the unclamped life with the skip (above the max until the update's end); the levy's life after is clamped the same");
						bool ok = r[0].hits >= 8 && r[0].hits == r[1].hits && r[0].life == r[1].life && seenOk && r[0].cmdLife == 0 && r[1].cmdLife == 0 && r[0].cmdMax == 0 && r[1].cmdMax == 0;
						if (!ok) { fails++; first ??= $"{lbl}: kept life {r[0].life} hits {r[0].hits}, skipped life {r[1].life} hits {r[1].hits}, commander life moved {r[0].cmdLife}/{r[1].cmdLife}"; }
					}
				Check($"nested hit pass: a levy's lifesteal is the same with the nested swap skipped and kept ({cases} cases: Player.Heal and a bare statLife +=, with room and at the cap; the levy's life after, the hits, the commander's life, and with Player.Heal every life a hit saw){(first != null ? " FIRST: " + first : "")}", fails == 0 && cases == 4);
				Check($"nested hit pass: skipped with the skip on ({skipsOn} hit passes) and never with it off ({skipsOff})", skipsOn > 0 && skipsOff == 0);
			}
			catch (Exception e) { Check("nested hit pass threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { if (_sweepTarget != null) _sweepTarget.active = false; _sweepTarget = null; OwnerPuppet.NestSkipOn = was; Main.myPlayer = my; NestSkipTestHeal.Proj = -1; NestSkipTestHeal.Heal = 0; ClearShots(); }
		}
	}
}
