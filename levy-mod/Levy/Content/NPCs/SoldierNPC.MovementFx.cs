using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Util;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Movement cosmetics: the dust, gore and sound a real player makes when it hovers on rocket boots, flaps wings, double-jumps, dashes and
	/// runs fast. A soldier is an NPC driven by a hidden dummy Player that never runs Player.Update, so none of that vanilla code fires on it.
	/// READ-ONLY with respect to movement: this reads the already-computed movement state and emits dust, gore and sound, nothing else.
	///
	/// Where the vanilla effect lives in a private Player method (RocketBootVisuals, WingAirVisuals, SpawnFastRunParticles) it is invoked on the
	/// dummy through a bound delegate, so every boot type, wing and dye looks exactly as it does on a player. Extra jumps call the public
	/// ExtraJump.OnStarted / ShowVisuals hooks of the jump the soldier actually spent. Dashes are ported from Player.DashMovement.
	///
	/// Single player reads the exact movement flags. A multiplayer client only sees synced velocity (the flight state lives on the server), so it
	/// infers thrust / dash / extra jump from velocity and the dummy's visible gear. A dedicated server returns at once.
	/// </summary>
	public partial class SoldierNPC
	{
		private int _fxJumpsPrev, _fxRocketHold, _fxWingHold, _fxBootSoundDelay, _fxTrailTicks, _fxShadowTicks;
		private bool _fxDashPrev, _fxFlapSound, _fxPrevAirborne;
		private float _fxPrevVy;
		private ExtraJump _fxTrailJump;

		private static uint _fxBudgetTick; private static int _fxBudgetUsed;
		private static bool _fxBroken, _fxBound, _fxShadowsBroken;
		private static Action<Player> _fxRocketBootVisuals, _fxWingAirVisuals, _fxFastRunParticles;

		/// <summary>Called every tick after the movement update (single player and multiplayer client; never a dedicated server).</summary>
		private void TickMovementFx()
		{
			if (Main.dedServ || _fxBroken || Dummy == null || Record == null || !IsAlive) return;
			try { MovementFx(); }
			catch (Exception e)
			{
				_fxBroken = true;
				Levy.Instance.Logger.Error("Levy: soldier movement visuals failed and are disabled for this session: " + e);
			}
		}

		private void MovementFx()
		{
			NPC npc = NPC; Player p = Dummy;
			if (_fxBootSoundDelay > 0) _fxBootSoundDelay--;
			if (p.runSoundDelay > 0) p.runSoundDelay--; // vanilla counts this down in Player.Update, which the dummy never runs

			// ---- read the movement state ----
			bool exact = LevyUtil.IsServerOrSingle; // single player: the real flags; multiplayer client: inferred from velocity
			bool airborne = exact ? _wasAirborne : npc.velocity.Y != 0f;
			bool thrustRocket, thrustWing, jumpFired = false, dashing;
			ExtraJump jump = null;
			if (exact)
			{
				bool thrustTick = _aerialVisualTicks == AerialVisualHoldTicks; // UpdateAerial counts it down first, so only a tick that thrust (or kicked) holds the full value
				thrustRocket = thrustTick && _usedRocket;
				thrustWing = (thrustTick && !_usedRocket) || _flying;
				if (_jumpsUsed > _fxJumpsPrev) { jumpFired = true; jump = SpentJump(_jumpsUsed - 1); }
				_fxJumpsPrev = _jumpsUsed;
				dashing = _dashTicks > 0;
			}
			else
			{
				bool kitWings = p.wings > 0, kitRocket = p.vanityRocketBoots > 0;
				bool rising = airborne && npc.velocity.Y < FxClientThrustMaxVy;
				thrustWing = rising && kitWings;
				thrustRocket = rising && !kitWings && kitRocket;
				jumpFired = airborne && _fxPrevAirborne && !kitWings && !kitRocket && npc.velocity.Y - _fxPrevVy < -FxClientJumpKickPx;
				dashing = !airborne && Math.Abs(npc.velocity.X) >= FxClientDashSpeedPx;
			}
			_fxPrevAirborne = airborne; _fxPrevVy = npc.velocity.Y;
			bool dashStart = dashing && !_fxDashPrev;
			_fxDashPrev = dashing;
			if (!airborne) { _fxRocketHold = 0; _fxWingHold = 0; _fxFlapSound = false; EndTrail(); }
			if (thrustRocket) _fxRocketHold = FxRocketHoldTicks;
			if (thrustWing) _fxWingHold = FxWingHoldTicks;
			if (dashing) _fxShadowTicks = FxDashShadowTail + 1;

			// ---- off screen: keep the trackers current, emit nothing ----
			if (!LevyUtil.IsOnScreen(npc.Hitbox)) { _fxRocketHold = 0; _fxWingHold = 0; _fxShadowTicks = 0; EndTrail(); return; }
			uint slot = Main.GameUpdateCount + (uint)npc.whoAmI;

			// ---- dash: the burst, the trail and the afterimages (Player.DashMovement) ----
			if (_fxShadowTicks > 0) { _fxShadowTicks--; p.UpdateSocialShadow(); }
			if (dashing)
			{
				int dt = p.dashType > 0 ? p.dashType : 1;
				if (dashStart && TakeFxBudget()) DashBurst(dt);
				if (TakeFxBudget()) DashTrail(dt, airborne);
			}

			// ---- extra jump: the puff and sound now, then the trail the jump streams while it lasts ----
			if (jumpFired && TakeFxBudget()) ExtraJumpBurst(jump ?? ExtraJump.CloudInABottle);
			if (_fxTrailTicks > 0)
			{
				_fxTrailTicks--;
				if (_fxTrailJump != null && slot % FxTrailEveryTicks == 0 && TakeFxBudget())
				{
					try { if (_fxTrailJump.CanShowVisuals(p)) _fxTrailJump.ShowVisuals(p); }
					catch (Exception e) { _fxTrailJump = null; Levy.Instance.Logger.Warn("Levy: extra jump trail visuals failed: " + e.Message); }
				}
				if (_fxTrailTicks == 0) EndTrail();
			}

			// ---- rocket boots: flame at the feet and the whoosh (Player.RocketBootVisuals) ----
			if (_fxRocketHold > 0)
			{
				_fxRocketHold--;
				if (slot % FxRocketEveryTicks == 0 && TakeFxBudget()) BootFlame();
				if (_fxBootSoundDelay <= 0)
				{
					int b = p.vanityRocketBoots;
					bool slow = b == 1 || b == 5 || b == 0; // Rocket / Hellfire Treads sound; Spectre, Fairy, Lightning, Frostspark and Terraspark use the other
					_fxBootSoundDelay = slow ? FxBootSoundSlowTicks : FxBootSoundFastTicks;
					SoundEngine.PlaySound(slow ? SoundID.Item13 : SoundID.Item24, npc.Center);
				}
			}

			// ---- wings: the dust some wings throw and the flap sound on the flap frame (Player.WingAirVisuals / Update) ----
			if (_fxWingHold > 0)
			{
				_fxWingHold--;
				if (slot % FxWingEveryTicks == 0 && TakeFxBudget()) { BindFx(); _fxWingAirVisuals?.Invoke(p); }
				int w = p.wings;
				if (w != 0 && w != 4 && w != 22 && w != 24 && w != 28 && w != 30 && w != 33 && w != 45)
				{
					bool flapFrame = p.wingFrame == ((w == 43 || w == 44) ? 4 : 3);
					if (flapFrame && !_fxFlapSound) SoundEngine.PlaySound(SoundID.Item32, npc.Center);
					_fxFlapSound = flapFrame;
				}
			}

			// ---- running fast on the ground: skid dust and the step sound (Player.SpawnFastRunParticles) ----
			if (!airborne && !dashing && !npc.wet && Math.Abs(npc.velocity.X) > FxRunMinSpeedPx && slot % FxRunEveryTicks == 0 && TakeFxBudget())
				RunDust();
		}

		/// <summary>Everything soldiers emit in one tick shares a small budget so a crowd cannot flood the dust pool.</summary>
		private static bool TakeFxBudget()
		{
			if (_fxBudgetTick != Main.GameUpdateCount) { _fxBudgetTick = Main.GameUpdateCount; _fxBudgetUsed = 0; }
			return _fxBudgetUsed++ < FxBudgetPerTick;
		}

		/// <summary>Binds the private vanilla visual methods once. A method that cannot be found stays null and its effect falls back or is skipped.</summary>
		private static void BindFx()
		{
			if (_fxBound) return;
			_fxBound = true;
			_fxRocketBootVisuals = BindVanilla("RocketBootVisuals");
			_fxWingAirVisuals = BindVanilla("WingAirVisuals");
			_fxFastRunParticles = BindVanilla("SpawnFastRunParticles");
		}

		private static Action<Player> BindVanilla(string name)
		{
			try
			{
				MethodInfo m = typeof(Player).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, System.Type.EmptyTypes, null);
				if (m == null) { Levy.Instance.Logger.Warn("Levy: Player." + name + " not found; its soldier visual falls back."); return null; }
				return (Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>), m);
			}
			catch (Exception e) { Levy.Instance.Logger.Warn("Levy: could not bind Player." + name + ": " + e.Message); return null; }
		}

		/// <summary>The extra jump the soldier spent: the n-th enabled jump in vanilla's own priority order (the order Player uses them in).</summary>
		private ExtraJump SpentJump(int index)
		{
			var ordered = ExtraJumpLoader.OrderedJumps;
			if (ordered == null) return null;
			int n = 0;
			for (int i = 0; i < ordered.Count; i++)
			{
				ExtraJump j = ordered[i];
				if (j == null || !Dummy.GetJumpState(j).Enabled) continue;
				if (n++ == index) return j;
			}
			return null;
		}

		private void EndTrail()
		{
			if (_fxTrailTicks == 0 && _fxTrailJump == null) return;
			_fxTrailTicks = 0; _fxTrailJump = null;
			Dummy.sandStorm = false; // Sandstorm in a Bottle's visuals raise it; the dummy has no Update to lower it
		}

		// ---- extra jump (ExtraJumpLoader.PerformJump) ----
		private void ExtraJumpBurst(ExtraJump jump)
		{
			Player p = Dummy;
			bool vanilla = jump is VanillaExtraJump;
			if (!vanilla) jump = ExtraJump.CloudInABottle; // a modded jump's hooks may do more than look: stand in the plain cloud
			bool playSound = true;
			jump.OnStarted(p, ref playSound);
			if (playSound) SoundEngine.PlaySound(SoundID.DoubleJump, NPC.Center);
			EndTrail();
			if (vanilla && !ReferenceEquals(jump, ExtraJump.CloudInABottle))
			{
				_fxTrailJump = jump;
				_fxTrailTicks = Math.Clamp((int)(FxTrailBaseTicks * jump.GetDurationMultiplier(p)), 1, FxTrailMaxTicks);
			}
		}

		// ---- rocket boots ----
		private void BootFlame()
		{
			BindFx();
			if (_fxRocketBootVisuals != null) { _fxRocketBootVisuals(Dummy); return; }
			// fallback: the plain Rocket Boots flame at each foot
			NPC npc = NPC;
			for (int i = 0; i < 2; i++)
			{
				int d = Dust.NewDust(new Vector2(npc.position.X + (i == 0 ? -4f : npc.width - 4f), npc.position.Y + npc.height - 10f), 8, 8, DustID.Torch, 0f, 0f, 100, default, 2.5f);
				Main.dust[d].noGravity = true;
				Main.dust[d].velocity += new Vector2(i == 0 ? -2f : 2f, 2f) - npc.velocity * 0.3f;
			}
		}

		// ---- running ----
		private void RunDust()
		{
			BindFx();
			Player p = Dummy;
			if (_fxFastRunParticles != null)
			{
				_fxFastRunParticles(p);
				if (p.runSoundDelay > 0) p.runSoundDelay = Math.Max(p.runSoundDelay, FxRunSoundTicks); // a crowd steps less often than one player
				return;
			}
			NPC npc = NPC;
			int d = Dust.NewDust(new Vector2(npc.position.X - 4f, npc.position.Y + npc.height), npc.width + 8, 4, DustID.Smoke, -npc.velocity.X * 0.5f, 0f, 50, default, 1.5f);
			Main.dust[d].velocity *= 0.2f;
		}

		// ---- dash (Player.DashMovement: burst on the first tick, trail every tick; dashType 1 Tabi / Master Ninja, 2 Shield of Cthulhu, 3 Solar Flare, 4 Crystal Assassin, 5 the Hallow-armor dash) ----
		private static int DashDustType(int dt) => dt switch
		{
			3 => DustID.Torch,
			4 => 229,
			5 => 68 + Main.rand.Next(3),
			_ => DustID.Smoke,
		};

		private void DashBurst(int dt)
		{
			if (dt == 2) return; // the Shield of Cthulhu throws no dust, only its afterimages
			Player p = Dummy;
			for (int i = 0; i < FxDashBurstDust; i++)
			{
				int d = Dust.NewDust(p.position, p.width, p.height, DashDustType(dt), 0f, 0f, 100, default, dt == 4 ? 1.2f : 2f);
				Dust dust = Main.dust[d];
				dust.position.X += Main.rand.Next(-5, 6); dust.position.Y += Main.rand.Next(-5, 6);
				dust.velocity *= 0.2f;
				dust.scale *= 1f + Main.rand.Next(20) * 0.01f;
				if (dt != 1) { dust.noGravity = true; dust.fadeIn = 0.5f; }
			}
			if (dt == 1)
			{
				for (int g = 0; g < 2; g++)
				{
					int id = Gore.NewGore(NPC.GetSource_FromThis(), new Vector2(p.position.X + p.width / 2 - 24f, p.position.Y + p.height / 2 - (g == 0 ? 34f : 14f)), default, Main.rand.Next(61, 64));
					Main.gore[id].velocity = new Vector2(Main.rand.Next(-50, 51), Main.rand.Next(-50, 51)) * 0.004f;
				}
			}
		}

		private void DashTrail(int dt, bool airborne)
		{
			Player p = Dummy;
			switch (dt)
			{
				case 1:
				case 5:
					for (int i = 0; i < 2; i++)
					{
						int type = DashDustType(dt);
						float scale = dt == 5 ? 1f : 1.4f;
						int d = airborne
							? Dust.NewDust(new Vector2(p.position.X, p.position.Y + p.height / 2 - 8f), p.width, 16, type, 0f, 0f, 100, default, scale)
							: Dust.NewDust(new Vector2(p.position.X, p.position.Y + p.height - 4f), p.width, 8, type, 0f, 0f, 100, default, scale);
						Main.dust[d].velocity *= dt == 5 ? 0.2f : 0.1f;
						Main.dust[d].scale *= 1f + Main.rand.Next(20) * 0.01f;
						if (dt == 5) { Main.dust[d].fadeIn = 0.5f + Main.rand.Next(20) * 0.01f; Main.dust[d].noGravity = true; }
					}
					break;
				case 3:
					for (int i = 0; i < 4; i++)
					{
						int d = Dust.NewDust(new Vector2(p.position.X, p.position.Y + 4f), p.width, p.height - 8, DustID.Torch, 0f, 0f, 100, default, 1.7f);
						Main.dust[d].velocity *= 0.1f;
						Main.dust[d].scale *= 1f + Main.rand.Next(20) * 0.01f;
						Main.dust[d].noGravity = true;
						if (Main.rand.NextBool(2)) Main.dust[d].fadeIn = 0.5f;
					}
					break;
				case 4:
					for (int i = 0; i < 2; i++)
					{
						int d = Dust.NewDust(new Vector2(p.position.X, p.position.Y + 4f), p.width, p.height - 8, 229, 0f, 0f, 100, default, 1.2f);
						Main.dust[d].velocity *= 0.1f;
						Main.dust[d].scale *= 1f + Main.rand.Next(20) * 0.01f;
						Main.dust[d].noGravity = true;
						if (Main.rand.NextBool(2)) Main.dust[d].fadeIn = 0.3f;
					}
					break;
			}
		}

		/// <summary>Draw-time afterimages of the last few dash positions, drawn behind the body in the same Immediate batch (called from SoldierNPC.Draw.cs).</summary>
		private void DrawDashShadows()
		{
			if (_fxShadowTicks <= 0 || _fxShadowsBroken) return;
			try
			{
				Player p = Dummy;
				for (int k = 0; k < p.shadowPos.Length; k++)
				{
					if (p.shadowPos[k] == Vector2.Zero) continue;
					Main.PlayerRenderer.DrawPlayer(Main.Camera, p, p.shadowPos[k], p.shadowRotation[k], p.shadowOrigin[k], 0.5f + 0.2f * k);
				}
			}
			catch (Exception e)
			{
				_fxShadowsBroken = true;
				Levy.Instance.Logger.Warn("Levy: dash afterimages failed and are disabled for this session: " + e.Message);
			}
		}
	}
}
