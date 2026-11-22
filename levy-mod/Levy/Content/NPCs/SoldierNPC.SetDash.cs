using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// THE SET-BONUS DASHES. Two armor sets give a dash that is itself the attack, and a levy's body (an NPC) never runs the player movement those dashes live in, so the dash is driven here on the body with the
	/// set's own numbers, the dummy standing in for everything the set reads (its stats, its cooldown, its dodge):
	/// <list type="bullet">
	/// <item>God Slayer / Auric Tesla (Calamity, its own God Slayer Dash key): <c>CalamityPlayer.godSlayer</c> and no <c>GodSlayerDash</c> cooldown on the dummy. A 75 tick dash toward the target, 32 px a tick easing to 11,
	/// steered back at the target the way a player holds the direction keys (0.175 rad a tick), bent by gravity. Every enemy the body passes is struck once per 12 ticks for 3000 base damage of the wearer's best class
	/// (Calamity's own <c>DirectStrike</c> projectile, so the hit runs the soldier-shot pipeline) and gets God Slayer Inferno. While the dash lasts the dummy reads as dashing to Calamity's dodge
	/// (<see cref="GodSlayerDodgeScope"/>), so a hit taken mid-dash is the God Slayer dodge. The 45 s cooldown is Calamity's own, on the dummy.</item>
	/// <item>Solar Flare (vanilla, double-tap a direction): while a shield is up the dummy has dashType 3. A ram dash at a close enemy: every enemy it passes takes 150 melee damage and a flare burst, the first spends a shield
	/// (Player.DashMovement, dash 3; Player.ConsumeSolarFlare).</item>
	/// </list>
	/// WHEN is the policy table (Common/Soldiers/SetAbilities.tsv: godslayer DASH, solarflare RAM). Authority only; clients see the body move and get the God Slayer visuals through <see cref="LevyPacket"/> SetAbilityFx.
	/// </summary>
	public partial class SoldierNPC
	{
		// ---- God Slayer dash (authority) ----
		private uint _gsStart; private int _gsTarget = -1, _gsLoop; private Vector2 _gsSteer; private bool _gsStruck, _gsGrav, _gsNoLoop;
		private byte[] _gsGap;
		// ---- Solar Flare ram (authority) ----
		private int _srTicks, _srDir; private float _srV, _srCapPx, _srDist; private bool _srSpent; private uint _srNext;
		private byte[] _srGap;
		// ---- visuals (every machine) ----
		private int _gsFxTicks;
		private uint _dashCheckTick;

		/// <summary>Checks: dashes started and enemies struck, army-wide and per levy; the place each God Slayer dash started and why ("rip", "dodge").</summary>
		public static int GodSlayerDashes, GodSlayerStrikes, SolarRams, SolarRamStrikes;
		public int GsDashesOwn, GsStrikesOwn, SrRamsOwn, SrStrikesOwn; public long GsDamageOwn, SrDamageOwn;
		public Vector2 GsLastStartAt; public string GsLastReason = "";
		/// <summary>The body is in a God Slayer dash now (authority).</summary>
		public bool GodSlayerDashing => _gsStart != 0;
		public bool SolarRamming => _srTicks > 0;
		/// <summary>Checks: the Solar Flare shields up now.</summary>
		internal int SolarShieldsLab => _setKind == KindSolar ? _setOrbs : 0;

		/// <summary>Called from TickSetBonusKey (combat, authority) before the set key: starts a dash when its policy says so. True when one started.</summary>
		private bool TryStartSetDash(NPC target)
		{
			if (_gsStart != 0 || _srTicks > 0 || Hostile || MindDirect || OfficerMode || Dummy == null) return false;
			if (_hkPhase != HookPhase.Idle && _hkPhase != HookPhase.Cooldown) return false;
			uint now = Main.GameUpdateCount;
			if (now == _dashCheckTick) return false;
			_dashCheckTick = now;
			return TryStartGodSlayer(target) || TryStartSolarRam(target);
		}

		// ------------------------------------------------------------------------------------------------------------
		// God Slayer
		// ------------------------------------------------------------------------------------------------------------
		private bool TryStartGodSlayer(NPC t)
		{
			SetAbilityPolicy pol = SetAbilityPolicies.For("godslayer");
			if (pol.Kind != SetAbilityKind.Dash || !GodSlayerCal.Ok) return false;
			Player d = Dummy;
			if (!GodSlayerCal.HasSet(d) || GodSlayerCal.OnCooldown(d)) return false;
			string why = null; Vector2 aim = default;
			if (t != null)
			{
				float tiles = Vector2.Distance(NPC.Center, t.Center) / 16f;
				bool tough = !pol.Flag("tough") || t.boss || t.lifeMax >= pol.Get("toughLife", 600f);
				if (tiles >= pol.Get("minDist", 3f) && tiles <= pol.Get("maxDist", 40f) && tough && Collision.CanHitLine(NPC.Center, 1, 1, t.Center, 1, 1)) { why = "rip"; aim = t.Center + t.velocity * 6f; }
			}
			if (why == null && pol.Flag("dodge") && (Main.GameUpdateCount + (uint)NPC.whoAmI) % 4u == 0u && HitComing((int)pol.Get("dodgeTicks", 24f)))
			{
				why = "dodge";
				aim = t != null ? t.Center : NPC.Center + new Vector2((NPC.direction == 0 ? 1 : NPC.direction) * 240f, -48f);
			}
			if (why == null) return false;
			StartGodSlayerDash(aim, t, why);
			return true;
		}

		/// <summary>A hostile projectile or body the threat field predicts will hit the body where it stands within <paramref name="ticks"/> ticks.</summary>
		private bool HitComing(int ticks)
		{
			try
			{
				var all = ThreatField.Snapshot();
				if (all.Count == 0) return false;
				var near = new System.Collections.Generic.List<Threat>(8);
				ThreatField.Near(all, NPC.Center, 128f, ticks, near);
				if (near.Count == 0) return false;
				return ThreatField.Hits(near, NPC.Center, NPC.width, NPC.height, new Maneuver { Kind = 0 }, Gravity(), ticks, out _, out _) > 0;
			}
			catch (Exception) { return false; }
		}

		private void StartGodSlayerDash(Vector2 aim, NPC t, string why)
		{
			Player d = Dummy;
			_gsStart = Main.GameUpdateCount; _gsTarget = t?.whoAmI ?? -1; _gsSteer = aim; _gsStruck = false; _gsLoop = 0;
			_gsNoLoop = SetAbilityPolicies.For("godslayer").Get("loop", 1f) == 0f;
			_gsGap ??= new byte[Main.maxNPCs]; Array.Clear(_gsGap);
			_gsGrav = NPC.noGravity; NPC.noGravity = true; // the dash carries its own gravity (a player's 0.4 a tick, no fall-speed cap)
			_dashTicks = 0; _dodgeTicks = 0; _dodgePend = 0;
			Vector2 dir = (aim - NPC.Center).SafeNormalize(new Vector2(NPC.direction == 0 ? 1 : NPC.direction, 0f)); // CalamityPlayer.HandleGodSlayerDash: toward the cursor at the dash's start speed
			NPC.velocity = dir * GodSlayerCal.StartSpeed;
			int face = NPC.velocity.X >= 0f ? 1 : -1; NPC.direction = face; NPC.spriteDirection = face;
			GodSlayerCal.AddCooldown(d); // MidDashEffects lays the cooldown on every tick of the dash; EndGodSlayerDash lays it again so it runs from the end
			NPC.netUpdate = true;
			GodSlayerDashes++; GsDashesOwn++; GsLastStartAt = NPC.Center; GsLastReason = why;
			SetAbilityFx(0);
			if (SetBonusLog.First("dash:godslayer")) SetBonusLog.Write("God Slayer dash by soldier '" + SoldierName + "' (" + why + ").");
		}

		/// <summary>Movement (authority): the dash owns the tick while it lasts. True while it does.</summary>
		private bool TickSetDash()
		{
			if (_gsStart != 0) return TickGodSlayerDash();
			if (_srTicks > 0) return TickSolarRam();
			return false;
		}

		private bool TickGodSlayerDash()
		{
			int t = (int)(Main.GameUpdateCount - _gsStart);
			int end = GodSlayerCal.ForceEndAt;
			if (t >= end || !IsAlive || _hkPhase == HookPhase.InFlight || _hkPhase == HookPhase.Reel) { EndGodSlayerDash(); return false; }
			if (_gsTarget >= 0)
			{
				NPC tg = Main.npc[_gsTarget];
				if (tg.active && tg.life > 0 && !tg.friendly) _gsSteer = tg.Center; else _gsTarget = -1;
			}
			// GodslayerArmorDash.MidDashEffects: the speed eases from 32 to 11; the heading turns toward the held direction keys (here: the 8-way direction to the target) at 0.175 rad a tick, else follows the velocity
			float progress = MathHelper.Clamp(t / (float)end, 0f, 1f);
			float ease = 1f - (float)Math.Pow(1f - progress, LevyConstants.GodSlayerDashEase);
			float speed = MathHelper.Lerp(GodSlayerCal.StartSpeed, LevyConstants.GodSlayerDashEndSpeed, ease);
			Vector2 off = _gsSteer - NPC.Center, held = Vector2.Zero;
			if (Math.Abs(off.X) > LevyConstants.GodSlayerSteerDeadzonePx) held.X = Math.Sign(off.X);
			if (Math.Abs(off.Y) > LevyConstants.GodSlayerSteerDeadzonePx) held.Y = Math.Sign(off.Y);
			Vector2 v = NPC.velocity;
			if (_gsNoLoop && Vector2.Dot(v, off) < 0f) held = Vector2.Zero; // the policy's loop=0: once through the target the keys are let go and the dash runs out straight
			Vector2 heading;
			if (held == Vector2.Zero) heading = v.SafeNormalize(new Vector2(NPC.direction == 0 ? 1 : NPC.direction, 0f));
			else
			{
				float cur = v == Vector2.Zero ? (NPC.direction < 0 ? MathHelper.Pi : 0f) : v.ToRotation();
				float diff = MathHelper.WrapAngle(held.ToRotation() - cur);
				// the target fell behind the dash: the loop back goes over the top (a player holds up, then back). The shorter way round can be down, where the floor takes the fall each tick and the dash slides on away.
				// The way round is picked once, when the target falls behind, and kept until it is ahead again.
				if (Vector2.Dot(v, off) < 0f && !_gsNoLoop)
				{
					if (_gsLoop == 0) _gsLoop = v.X >= 0f ? -1 : 1;
					diff = _gsLoop * Math.Max(Math.Abs(diff), LevyConstants.GodSlayerDashTurn);
				}
				else _gsLoop = 0;
				heading = (cur + MathHelper.Clamp(diff, -LevyConstants.GodSlayerDashTurn, LevyConstants.GodSlayerDashTurn)).ToRotationVector2();
			}
			NPC.velocity = heading * speed + new Vector2(0f, LevyConstants.GodSlayerDashGravity); // Player.Update adds its gravity after the dash sets the velocity
			if (NPC.velocity.X != 0f) { int face = Math.Sign(NPC.velocity.X); NPC.direction = face; NPC.spriteDirection = face; }
			GodSlayerStrikes_Tick();
			if (t % LevyConstants.GodSlayerDashNetTicks == 0) NPC.netUpdate = true;
			return true;
		}

		private void EndGodSlayerDash()
		{
			_gsStart = 0; _gsTarget = -1;
			NPC.noGravity = _gsGrav;
			if (NPC.velocity.Length() > LevyConstants.GodSlayerDashEndSpeed) NPC.velocity = NPC.velocity.SafeNormalize(Vector2.Zero) * LevyConstants.GodSlayerDashEndSpeed;
			if (Dummy != null) GodSlayerCal.AddCooldown(Dummy);
			NPC.netUpdate = true;
		}

		/// <summary>CalamityPlayer.ModDashMovement: every enemy in the body's box (half a tick of velocity ahead, 4 px around) that is not on its gap is struck.</summary>
		private void GodSlayerStrikes_Tick()
		{
			byte[] gap = _gsGap;
			for (int i = 0; i < gap.Length; i++) if (gap[i] > 0) gap[i]--;
			Rectangle box = DashBox();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (gap[i] > 0 || !DashCanStrike(n, box)) continue;
				gap[i] = (byte)LevyConstants.GodSlayerDashEnemyGapTicks;
				GodSlayerStrike(n);
				if (!IsAlive) return;
			}
		}

		private Rectangle DashBox()
		{
			float m = LevyConstants.DashStrikeMarginPx;
			return new Rectangle((int)(NPC.position.X + NPC.velocity.X * 0.5f - m), (int)(NPC.position.Y + NPC.velocity.Y * 0.5f - m), NPC.width + (int)(m * 2f), NPC.height + (int)(m * 2f));
		}

		/// <summary>An enemy a dash may strike: a live hostile target by the army's own rules (never the commander, a levy or a town NPC), in the box, and in reach through the tiles.</summary>
		private bool DashCanStrike(NPC n, Rectangle box)
		{
			if (n == null || !n.active || n.friendly || n.dontTakeDamage || n.life <= 0 || n.whoAmI == NPC.whoAmI || n.ModNPC is SoldierNPC) return false;
			if (!LevyUtil.IsHostileTarget(n) || SoldierTargeting.IsNeverTarget(n)) return false;
			if (!box.Intersects(n.Hitbox)) return false;
			return n.noTileCollide || Collision.CanHit(NPC.position, NPC.width, NPC.height, n.position, n.width, n.height);
		}

		private void GodSlayerStrike(NPC n)
		{
			Player d = Dummy;
			DamageClass cls = GodSlayerCal.BestClass(d);
			int damage = Math.Max(1, (int)(d.GetTotalDamage(cls).ApplyTo(GodSlayerCal.Damage) * DamageMultiplier));
			Projectile p = GodSlayerCal.StrikeType > 0 ? WeaponUse.Spawn(this, n.Center, Vector2.Zero, GodSlayerCal.StrikeType, damage, 0f, cls, n.whoAmI) : null; // Calamity's ram: a DirectStrike on the enemy, ai0 = the enemy
			if (p != null)
			{
				p.usesLocalNPCImmunity = true; p.localNPCHitCooldown = -1; // its one strike is never refused by the shared owner immunity the army's other shots set
				GsDamageOwn += damage;
			}
			else
			{
				LootCredit.Credit(n, this);
				int done = n.SimpleStrikeNPC(damage, NPC.velocity.X < 0f ? -1 : 1, false, 0f, cls, false, 0f, true);
				LootCredit.OnSoldierHit(n, this, done);
				GsDamageOwn += done;
			}
			if (GodSlayerCal.InfernoBuff > 0) n.AddBuff(GodSlayerCal.InfernoBuff, GodSlayerCal.InfernoTicks);
			int iframes = GodSlayerCal.IFrames; // Player.GiveImmuneTimeForCollisionAttack
			if (ImmuneTime < iframes) ImmuneTime = iframes;
			if (AllImmune < iframes) AllImmune = iframes;
			GodSlayerStrikes++; GsStrikesOwn++;
			if (!_gsStruck) { _gsStruck = true; SetAbilityFx(1); }
		}

		/// <summary>
		/// While the levy's God Slayer dash lasts, Calamity's dodge (CalamityPlayer.ConsumableDodge -> HandleDashDodges -> GodSlayerDodge) must see the dummy dashing: its dash id is the God Slayer dash, its dashDelay
		/// is below zero and it holds no hook. Opened around the player hit sequence (SoldierNPC.PlayerHit.cs) and put back after it.
		/// </summary>
		internal readonly struct GodSlayerDodgeScope : IDisposable
		{
			private readonly Player _d; private readonly bool _on; private readonly object _id; private readonly int _delay, _grap;
			public GodSlayerDodgeScope(SoldierNPC s)
			{
				_d = s?.Dummy; _on = false; _id = null; _delay = 0; _grap = -1;
				if (_d == null || s._gsStart == 0 || !GodSlayerCal.Ok) return;
				try
				{
					_id = GodSlayerCal.GetDashId(_d); _delay = _d.dashDelay; _grap = _d.grappling[0];
					GodSlayerCal.SetDashId(_d, GodSlayerCal.DashId); _d.dashDelay = -1; _d.grappling[0] = -1;
					_on = true;
				}
				catch (Exception) { }
			}
			public void Dispose()
			{
				if (!_on) return;
				try { GodSlayerCal.SetDashId(_d, _id); _d.dashDelay = _delay; _d.grappling[0] = _grap; } catch (Exception) { }
			}
		}

		// ------------------------------------------------------------------------------------------------------------
		// Solar Flare
		// ------------------------------------------------------------------------------------------------------------
		private bool TryStartSolarRam(NPC t)
		{
			if (t == null) return false;
			SetAbilityPolicy pol = SetAbilityPolicies.For("solarflare");
			if (pol.Kind != SetAbilityKind.Ram) return false;
			if (_setKind != KindSolar || _setOrbs < Math.Max(1, (int)pol.Get("minShields", 1f))) return false; // Player.UpdateArmorSets: the Solar Flare set has its dash (dashType 3) while a shield is up
			if (Main.GameUpdateCount < _srNext || NPC.wet) return false;
			float dx = t.Center.X - NPC.Center.X, adx = Math.Abs(dx) / 16f;
			if (adx < pol.Get("minDist", 1.5f) || adx > pol.Get("maxDist", 24f) || Math.Abs(t.Center.Y - NPC.Center.Y) > pol.Get("maxRise", 3f) * 16f) return false;
			if (!Collision.CanHit(NPC.position, NPC.width, NPC.height, t.position, t.width, t.height)) return false;
			_srDir = dx >= 0f ? 1 : -1;
			_srV = LevyConstants.SolarDashSpeed; // the set's dash replaces any accessory dash (UpdateArmorSets runs after the accessories)
			_srCapPx = Math.Abs(dx) + LevyConstants.SolarRamOvershootTiles * 16f; _srDist = 0f;
			_srTicks = 1; _srSpent = false;
			_srGap ??= new byte[Main.maxNPCs]; Array.Clear(_srGap);
			_srNext = Main.GameUpdateCount + (uint)Math.Max(20f, pol.Get("gap", 90f));
			_dashTicks = 0; _dodgeTicks = 0; _dodgePend = 0;
			NPC.velocity.X = _srDir * _srV; NPC.direction = _srDir; NPC.spriteDirection = _srDir;
			NPC.netUpdate = true;
			SolarRams++; SrRamsOwn++;
			if (SetBonusLog.First("dash:solar")) SetBonusLog.Write("Solar Flare dash by soldier '" + SoldierName + "'.");
			return true;
		}

		private bool TickSolarRam()
		{
			_srTicks++;
			bool stopped = _srTicks > 3 && Math.Abs(NPC.velocity.X) < Math.Max(2f, _srV * 0.4f); // the body met a wall
			float run = Math.Max(3f, Abilities?.MaxRunSpeed ?? 3f);
			float split = LevyConstants.SolarDashSplit, hi = LevyConstants.SolarDashHighDecay, lo = LevyConstants.SolarDashSlowDecay;
			_srDist += Math.Abs(NPC.velocity.X);
			float v = _srV > split ? _srV * hi : _srV * lo; // Player.DashMovement: x0.985 above the split speed, x0.94 above the run speed, then the run
			if (v <= run || stopped || NPC.wet || _srDist >= _srCapPx || _srTicks > LevyConstants.SolarRamMaxTicks || !IsAlive)
			{
				_srTicks = 0; NPC.velocity.X = _srDir * Math.Min(Math.Abs(NPC.velocity.X), run); NPC.netUpdate = true;
				return false;
			}
			_srV = v; NPC.velocity.X = _srDir * v;
			SolarRamStrikes_Tick();
			return true;
		}

		private void SolarRamStrikes_Tick()
		{
			byte[] gap = _srGap;
			for (int i = 0; i < gap.Length; i++) if (gap[i] > 0) gap[i]--;
			Rectangle box = DashBox();
			Player d = Dummy;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (gap[i] > 0 || !DashCanStrike(n, box)) continue;
				gap[i] = (byte)LevyConstants.SolarDashEnemyGapTicks;
				if (!_srSpent && _setKind == KindSolar && _setOrbs > 0) { _srSpent = true; SetOrbs(_setOrbs - 1); _setCounter = 0f; } // Player.ConsumeSolarFlare, once a dash
				int damage = Math.Max(1, (int)(d.GetTotalDamage(DamageClass.Melee).ApplyTo(LevyConstants.SolarDashDamage) * DamageMultiplier));
				float kb = d.GetTotalKnockback(DamageClass.Melee).ApplyTo(LevyConstants.SolarDashKnockback);
				bool crit = Main.rand.Next(100) < d.GetTotalCritChance(DamageClass.Melee);
				LootCredit.Credit(n, this);
				int done = n.SimpleStrikeNPC(damage, _srDir, crit, kb, DamageClass.Melee, true, 0f, true);
				LootCredit.OnSoldierHit(n, this, done);
				OnMeleeHit(n, done);
				if (IsAlive) DetonateSolarFlare(); // the flare burst vanilla spawns at the wearer for every enemy the dash strikes (projectile 608)
				if (ImmuneTime < LevyConstants.SolarDashIFrames) ImmuneTime = LevyConstants.SolarDashIFrames;
				if (AllImmune < LevyConstants.SolarDashIFrames) AllImmune = LevyConstants.SolarDashIFrames;
				SolarRamStrikes++; SrStrikesOwn++; SrDamageOwn += done;
				if (!IsAlive) return;
			}
		}

		// ------------------------------------------------------------------------------------------------------------
		// Visuals: the God Slayer dash (dust ring and trail, the Devourer's sounds), on screen only
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>Authority: play the effect here (single player) or tell every client (server). 0 the dash starts, 1 its first strike.</summary>
		private void SetAbilityFx(byte kind)
		{
			if (Main.netMode == NetmodeID.Server)
			{
				ModPacket p = global::Levy.Common.Net.LevyPackets.New(global::Levy.Common.Net.LevyPacket.SetAbilityFx);
				p.Write((short)NPC.whoAmI); p.Write(kind); p.Send();
			}
			else if (!Main.dedServ) PlaySetAbilityFx(kind);
		}

		internal void PlaySetAbilityFx(byte kind)
		{
			try
			{
				if (kind == 0) { _gsFxTicks = GodSlayerCal.ForceEndAt; if (GodSlayerCal.StartSound.HasValue) global::Levy.Common.Audio.ArmySoundThrottle.PlayAsArmy(GodSlayerCal.StartSound.Value, NPC.Center); }
				else if (kind == 1)
				{
					if (GodSlayerCal.ImpactSound.HasValue) global::Levy.Common.Audio.ArmySoundThrottle.PlayAsArmy(GodSlayerCal.ImpactSound.Value, NPC.Center);
					if (LevyUtil.IsOnScreen(NPC.Hitbox))
						for (int i = 0; i < LevyConstants.GodSlayerImpactDust; i++)
						{
							Dust dust = Dust.NewDustPerfect(NPC.Center, Main.rand.NextBool(3) ? 226 : 272, NPC.velocity.RotatedByRandom(MathHelper.ToRadians(15f)) * Main.rand.NextFloat(0.1f, 0.5f), 0, default, Main.rand.NextFloat(2.1f, 2.9f));
							dust.noGravity = false;
						}
				}
			}
			catch (Exception) { }
		}

		/// <summary>Every machine, every tick (PostAI): the dash's dust while it lasts.</summary>
		private void TickSetAbilityFx()
		{
			if (_gsFxTicks <= 0 || Main.dedServ) return;
			_gsFxTicks--;
			if (!LevyUtil.IsOnScreen(NPC.Hitbox)) return;
			int t = GodSlayerCal.ForceEndAt - _gsFxTicks;
			Vector2 v = NPC.velocity;
			Lighting.AddLight(NPC.Center, 0.55f, 0.2f, 0.7f);
			for (int i = 0; i < LevyConstants.GodSlayerDashRingDust; i++)
			{
				float rot = v.ToRotation() + t / 20f;
				float radius = 30f + (float)Math.Cos(t / 3f) * 24f;
				Vector2 at = NPC.Center + v * 0.8f + rot.ToRotationVector2().RotatedBy(i / 5f * MathHelper.TwoPi) * radius;
				Dust a = Dust.NewDustPerfect(at, Main.rand.NextBool(5) ? 181 : 295);
				a.noGravity = true; a.velocity = v * 0.5f; a.scale = Main.rand.NextFloat(2.7f, 3f);
				Dust b = Dust.NewDustPerfect(NPC.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-15f, 15f)) + v * 0.5f, Main.rand.NextBool(14) ? 180 : 295, -v.RotatedByRandom(MathHelper.ToRadians(30f)) * Main.rand.NextFloat(0.1f, 0.8f), 0, default, Main.rand.NextFloat(2.7f, 3.9f));
				b.noGravity = true;
			}
		}

		internal static void OnSetAbilityFxPacket(BinaryReader r, int from)
		{
			short idx = r.ReadInt16(); byte kind = r.ReadByte();
			if (Main.netMode != NetmodeID.MultiplayerClient || idx < 0 || idx >= Main.maxNPCs || !Main.npc[idx].active || Main.npc[idx].ModNPC is not SoldierNPC s) return;
			s.PlaySetAbilityFx(kind);
		}
	}

	/// <summary>Registers the set-ability visual packet.</summary>
	public class SetAbilityNet : ModSystem
	{
		public override void Load() => global::Levy.Common.Net.LevyPackets.Register(global::Levy.Common.Net.LevyPacket.SetAbilityFx, SoldierNPC.OnSetAbilityFxPacket);
	}

	/// <summary>
	/// Calamity's God Slayer dash, read by reflection (Levy has no compile-time reference to CalamityMod): the set flag, the cooldown, the dash's numbers (GodSlayerChestplate, GodslayerArmorDash), the best-class rule,
	/// the DirectStrike ram projectile, the God Slayer Inferno debuff and the Devourer's sounds. A member that cannot be found falls back to the numbers in LevyConstants; without the set flag or the cooldown it is off.
	/// </summary>
	internal static class GodSlayerCal
	{
		private static bool _init;
		private static Func<Player, bool> _flag;
		private static FieldInfo _cooldowns, _dashIdField; private static ModPlayer _cpBase;
		private static MethodInfo _addCooldown, _bestClass;
		public static bool Ok { get { Init(); return _flag != null && _cooldowns != null && _cpBase != null; } }
		public static string DashId = "", CooldownId = "GodSlayerDash";
		public static int ForceEndAt = LevyConstants.GodSlayerDashTicks, Damage = LevyConstants.GodSlayerDashDamage, IFrames = LevyConstants.GodSlayerDashIFrames, CooldownTicks = LevyConstants.GodSlayerDashCooldownTicks;
		public static int InfernoTicks = LevyConstants.GodSlayerInfernoTicks, InfernoBuff = -1, StrikeType;
		public static float StartSpeed = LevyConstants.GodSlayerDashStartSpeed;
		public static SoundStyle? StartSound, ImpactSound;

		private static void Init()
		{
			if (_init) return; _init = true;
			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal) || cal.Code == null) return;
				Type cp = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
				_flag = global::Levy.Common.Team.TeamItems.ModFlag("CalamityMod", "CalamityMod.CalPlayer.CalamityPlayer", "godSlayer");
				_cooldowns = cp?.GetField("cooldowns", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_dashIdField = cp?.GetField("dashID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_cpBase = ModContent.GetContent<ModPlayer>().FirstOrDefault(x => x.GetType() == cp);
				Type chest = cal.Code.GetType("CalamityMod.Items.Armor.GodSlayer.GodSlayerChestplate");
				int SP(string n, int fb) { try { return chest?.GetProperty(n, BindingFlags.Static | BindingFlags.Public)?.GetValue(null) is int v && v > 0 ? v : fb; } catch (Exception) { return fb; } }
				CooldownTicks = SP("DashCooldown", CooldownTicks); Damage = SP("DashDamage", Damage); IFrames = SP("DashIFrames", IFrames); InfernoTicks = SP("DashGodSlayerInfernoDuration", InfernoTicks);
				Type dash = cal.Code.GetType("CalamityMod.CalPlayer.Dashes.GodslayerArmorDash");
				DashId = dash?.GetProperty("ID", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as string ?? "";
				Type mgr = cal.Code.GetType("CalamityMod.CalPlayer.Dashes.PlayerDashManager");
				var find = mgr?.GetMethod("FindByID", BindingFlags.Public | BindingFlags.Static);
				if (find != null && DashId.Length > 0)
				{
					var args = new object[] { DashId, null };
					if ((bool)find.Invoke(null, args) && args[1] != null)
					{
						object eff = args[1];
						if (eff.GetType().GetProperty("ForceEndAt")?.GetValue(eff) is int fe && fe > 0) ForceEndAt = fe;
						try { if (eff.GetType().GetMethod("CalculateDashSpeed")?.Invoke(eff, new object[] { new Player() }) is float sp && sp > 0f) StartSpeed = sp; } catch (Exception) { }
					}
				}
				Type utils = cal.Code.GetType("CalamityMod.CalamityUtils");
				_addCooldown = utils?.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "AddCooldown" && m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(Player));
				_bestClass = utils?.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "GetBestClass" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Player));
				Type cd = cal.Code.GetType("CalamityMod.Cooldowns.GodSlayerDash");
				if (cd?.GetProperty("ID", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) is string cid && cid.Length > 0) CooldownId = cid;
				if (ModContent.TryFind("CalamityMod/DirectStrike", out ModProjectile ds)) StrikeType = ds.Type;
				if (ModContent.TryFind("CalamityMod/GodSlayerInferno", out ModBuff gi)) InfernoBuff = gi.Type;
				Type dog = cal.Code.GetType("CalamityMod.NPCs.DevourerofGods.DevourerofGodsHead");
				if (dog?.GetField("DeathAnimationSound", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) is SoundStyle s0) StartSound = s0;
				if (dog?.GetField("DeathExplosionSound", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) is SoundStyle s1) ImpactSound = s1;
				Levy.Instance?.Logger.Info($"Levy God Slayer dash: flag {_flag != null}, cooldown '{CooldownId}' {CooldownTicks} ticks, dash '{DashId}' {ForceEndAt} ticks from {StartSpeed} px, damage {Damage}, strike projectile {StrikeType}, inferno {InfernoBuff}.");
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("Levy God Slayer dash: Calamity members not found, levies will not use it: " + e.Message); _flag = null; }
		}

		public static bool HasSet(Player d) { try { return Ok && _flag(d); } catch (Exception) { return false; } }

		public static bool OnCooldown(Player d)
		{
			try { return _cooldowns.GetValue(d.GetModPlayer(_cpBase)) is System.Collections.IDictionary dict && dict.Contains(CooldownId); }
			catch (Exception) { return true; }
		}

		/// <summary>Ticks left on the dummy's God Slayer Dash cooldown, 0 when there is none, -1 when it cannot be read.</summary>
		public static int CooldownLeft(Player d)
		{
			try
			{
				if (!(_cooldowns.GetValue(d.GetModPlayer(_cpBase)) is System.Collections.IDictionary dict) || !dict.Contains(CooldownId)) return 0;
				object inst = dict[CooldownId];
				return inst?.GetType().GetField("timeLeft")?.GetValue(inst) is int left ? left : -1;
			}
			catch (Exception) { return -1; }
		}

		/// <summary>Checks: sets the ticks left on the dummy's cooldown. False when it has none or it cannot be written.</summary>
		public static bool SetCooldownLeft(Player d, int left)
		{
			try
			{
				if (!(_cooldowns.GetValue(d.GetModPlayer(_cpBase)) is System.Collections.IDictionary dict) || !dict.Contains(CooldownId)) return false;
				object inst = dict[CooldownId]; FieldInfo f = inst?.GetType().GetField("timeLeft");
				if (f == null) return false;
				f.SetValue(inst, left); return true;
			}
			catch (Exception) { return false; }
		}

		public static void AddCooldown(Player d)
		{
			try { _addCooldown?.Invoke(null, new object[] { d, CooldownId, CooldownTicks, true }); }
			catch (Exception e) { if (SetBonusLog.First("gs:cooldown-error")) SetBonusLog.Write("God Slayer cooldown could not be set: " + (e.InnerException ?? e).Message); }
		}

		public static DamageClass BestClass(Player d)
		{
			try { if (_bestClass?.Invoke(null, new object[] { d }) is DamageClass c) return c; } catch (Exception) { }
			return DamageClass.Generic;
		}

		public static object GetDashId(Player d) => _dashIdField?.GetValue(d.GetModPlayer(_cpBase));
		public static void SetDashId(Player d, object v) => _dashIdField?.SetValue(d.GetModPlayer(_cpBase), v);
	}
}
