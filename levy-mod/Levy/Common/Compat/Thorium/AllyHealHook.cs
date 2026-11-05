using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;
using ThoriumMod;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// A PLAYER's Thorium "heal allies" reaching his soldiers and officer avatars, by hooking the two routines every Thorium ally heal is built from instead of any weapon:
	/// <list type="bullet">
	/// <item><c>ProjectileHelper.ThoriumHeal(projectile, healAmount, radius, onHealEffects, bonusHealing, customHealing, canHealPlayer, specificPlayer, ...)</c> - the one place a heal projectile
	/// (bolt, burst, aura, healing orb) delivers its heal to the Players within <c>radius</c> of itself. After it runs for the local player's projectile and no one specific player (an area heal),
	/// the same heal - Thorium's own amount, the heal bonus when it applies, the weapon's custom rule run for the soldier - goes to the wounded soldiers within the same radius of the same projectile.</item>
	/// <item><c>ProjectileHelper.CanBeHealed(projectile, healer, target, radius, ...)</c> - the shared "may this Player be healed by this healer" test. Thorium's radiant weapons that heal allies WHEN THEY DEAL
	/// DAMAGE (Morning Dew, Life Disperser, Heretic Breaker, Iridescent Staff, ...) loop every Player through it from their on-hit code and spawn a heal on each one that passes, so a soldier - not a Player -
	/// never passes and, when the commander himself is unhurt, nothing happens at all. The first such test inside the hit of an ally-heal weapon (<see cref="OpenHitWindow"/>) is the cue: the soldiers in
	/// the sweep's range - the radius it passes, else <see cref="SweepDefaultRadius"/>, measured from the projectile or the healer - get the weapon's heal amount.</item>
	/// </list>
	/// Both are IL postfixes (<see cref="MonoModHooks.Modify"/>), so Thorium runs unchanged and nothing about the 19+ weapons is hard-coded. They act only where Thorium's own routine acts (the projectile's
	/// owner is the local player), and any failure is logged and leaves the heal as Thorium made it. In multiplayer the owning client asks the server (<see cref="CompanionRequest"/>).
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class AllyHealHook
	{
		/// <summary>Range of an on-hit sweep that passes no radius of its own (Thorium measures those with an inline distance test, 250-800 px, 450 most often).</summary>
		public const float SweepDefaultRadius = 450f;

		/// <summary>True once both hooks are in place.</summary>
		public static bool Installed { get; private set; }

		private static int _inThoriumHeal;
		private static bool _inSeek;
		/// <summary>Harness A/B only (LEVY_HARNESS and LEVY_NOSTRIKER set): do not recognise a levy's melee swing as a healer, to show what the sweep does without it.</summary>
		private static readonly bool NoStrikerAB = System.Environment.GetEnvironmentVariable("LEVY_HARNESS") != null && System.Environment.GetEnvironmentVariable("LEVY_NOSTRIKER") != null;
		private static uint _healStamp;

		// the on-hit window: which hit of which ally-heal weapon the sweeps that run this tick belong to
		private static uint _hitTick = uint.MaxValue;
		private static int _hitOwner = -1;
		private static int _hitBase;
		private static Projectile _hitProj;
		private static bool _sweepDone;

		public static void Install()
		{
			Installed = false;
			// A/B switch for the combat harness only (LEVY_HARNESS set): run the same scenario with and without these hooks.
			if (System.Environment.GetEnvironmentVariable("LEVY_HARNESS") != null && System.Environment.GetEnvironmentVariable("LEVY_NOALLYHEAL") != null) throw new InvalidOperationException("disabled by LEVY_NOALLYHEAL (A/B test)");
			Type helper = typeof(ThoriumPlayer).Assembly.GetType("ThoriumMod.Utilities.ProjectileHelper") ?? throw new MissingMemberException("ThoriumMod.Utilities.ProjectileHelper");
			MethodInfo heal = null, can = null;
			foreach (MethodInfo m in helper.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (m.Name == "ThoriumHeal") heal = m;
				else if (m.Name == "CanBeHealed") can = m;
			}
			if (heal == null) throw new MissingMethodException("ProjectileHelper.ThoriumHeal");
			if (can == null) throw new MissingMethodException("ProjectileHelper.CanBeHealed");
			CheckShape(heal, typeof(Projectile), typeof(int), typeof(float), typeof(bool), typeof(bool), null, typeof(Func<Player, bool>), typeof(int), typeof(bool), typeof(bool), typeof(bool));
			CheckShape(can, typeof(Projectile), typeof(Player), typeof(Player), typeof(float), typeof(Func<Player, bool>), typeof(int), typeof(bool));
			if (can.ReturnType != typeof(bool)) throw new MissingMethodException("ProjectileHelper.CanBeHealed no longer returns bool");

			MethodInfo healPost = typeof(AllyHealHook).GetMethod(nameof(ThoriumHealPost), BindingFlags.Static | BindingFlags.NonPublic);
			MethodInfo healPre = typeof(AllyHealHook).GetMethod(nameof(ThoriumHealPre), BindingFlags.Static | BindingFlags.NonPublic);
			MethodInfo canPost = typeof(AllyHealHook).GetMethod(nameof(CanBeHealedPost), BindingFlags.Static | BindingFlags.NonPublic);

			MonoModHooks.Modify(heal, il =>
			{
				var c = new ILCursor(il);
				c.Emit(OpCodes.Call, healPre); // before the first instruction: we are inside ThoriumHeal (its own CanBeHealed tests are not sweeps)
				int n = il.Method.Parameters.Count;
				while (c.TryGotoNext(MoveType.Before, i => i.MatchRet()))
				{
					c.MoveBeforeLabels();
					for (int p = 0; p < n; p++) c.Emit(OpCodes.Ldarg, il.Method.Parameters[p]);
					c.Emit(OpCodes.Call, healPost);
					c.GotoNext(MoveType.After, i => i.MatchRet());
				}
			});
			MonoModHooks.Modify(can, il =>
			{
				var c = new ILCursor(il);
				int n = il.Method.Parameters.Count;
				while (c.TryGotoNext(MoveType.Before, i => i.MatchRet()))
				{
					c.MoveBeforeLabels();
					for (int p = 0; p < n; p++) c.Emit(OpCodes.Ldarg, il.Method.Parameters[p]); // stack: result, args... -> result
					c.Emit(OpCodes.Call, canPost);
					c.GotoNext(MoveType.After, i => i.MatchRet());
				}
			});
			Installed = true;
		}

		/// <summary>The hooks assume Thorium's exact parameter list; anything else and the hook stays off (the caller logs it) rather than corrupt the evaluation stack.</summary>
		private static void CheckShape(MethodInfo m, params Type[] want)
		{
			ParameterInfo[] ps = m.GetParameters();
			if (ps.Length != want.Length) throw new MissingMethodException("ProjectileHelper." + m.Name + " now takes " + ps.Length + " parameters, the hook expects " + want.Length);
			for (int i = 0; i < ps.Length; i++)
				if (want[i] != null && ps[i].ParameterType != want[i]) throw new MissingMethodException("ProjectileHelper." + m.Name + " parameter " + i + " is " + ps[i].ParameterType.Name + ", the hook expects " + want[i].Name);
		}

		// ---- the on-hit window ----

		/// <summary>
		/// A hit by an ally-heal weapon (<paramref name="baseHeal"/> = its heal amount) is about to land: for the rest of this tick the first heal sweep its owner runs belongs to it.
		/// Exactly one of <paramref name="proj"/> (the weapon's projectile) or <paramref name="item"/> (a swing) is set.
		/// </summary>
		public static void OpenHitWindow(Projectile proj, Item item, int owner, int baseHeal)
		{
			if (!ThoriumCompatSystem.Active || owner != Main.myPlayer || baseHeal <= 0) return;
			_hitTick = Main.GameUpdateCount; _hitOwner = owner; _hitBase = baseHeal; _hitProj = proj; _sweepDone = false;
		}

		// ---- who cast it ----

		/// <summary>The soldier who fired this projectile (his shots are owned by the commander, so Thorium reads the commander as the healer), or null when the commander cast it.</summary>
		internal static SoldierNPC CasterOf(Projectile p) =>
			p != null && p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.IsSoldierShot ? EnemyTargeting.LiveBody(t.SoldierIndex) : null;

		/// <summary>The heal bonus of whoever cast: the soldier's own gear when a soldier did, else the commander's.</summary>
		internal static int BonusOf(Player owner, SoldierNPC caster)
		{
			Player who = caster?.Dummy ?? owner;
			return Math.Max(0, who.GetModPlayer<ThoriumPlayer>().healBonus);
		}

		// ---- ThoriumHeal: the area heal ----

		private static void ThoriumHealPre()
		{
			_inThoriumHeal++; _healStamp = Main.GameUpdateCount;
		}

		private static void ThoriumHealPost(Projectile projectile, int healAmount, float radius, bool onHealEffects, bool bonusHealing, object customHealing, Func<Player, bool> canHealPlayer, int specificPlayer, bool ignoreHealer, bool ignoreSetTarget, bool statistics)
		{
			if (_inThoriumHeal > 0) _inThoriumHeal--;
			try
			{
				if (!ThoriumCompatSystem.Active || projectile == null || !projectile.active || projectile.owner != Main.myPlayer || projectile.penetrate == 0) return;
				if (specificPlayer > -1 && specificPlayer < Main.maxPlayers) return; // a heal aimed at one particular Player (a Thorium heal proxy): there is no area to extend
				Player owner = Main.player[projectile.owner];
				if (owner != Main.LocalPlayer || radius <= 0f) return;
				SoldierNPC caster = CasterOf(projectile);
				int bonus = BonusOf(owner, caster);
				PlayerHealCompat.HealAround(owner, projectile.Center, radius, n =>
				{
					int heals = healAmount + (bonusHealing ? bonus : 0), selfHeals = 0;
					if (customHealing is Delegate custom)
					{
						// the weapon's own rule (it scales or caps the heal for the target) run for the soldier's stand-in Player, exactly as it runs for a Player
						Player target = n.ModNPC is SoldierNPC s && s.Dummy != null ? s.Dummy : owner;
						object[] args = { owner, target, heals, selfHeals };
						try { custom.DynamicInvoke(args); heals = (int)args[2]; } catch (Exception) { heals = healAmount + (bonusHealing ? bonus : 0); }
					}
					return PlayerHealCompat.ScaleForSoldier(heals);
				}, projectile, caster, false, caster == null ? null : () =>
				{
					// the COMMANDER as an ally of a soldier's heal: the same rule run for him (the weapon's own predicate and scaling, then the soldiers' potency)
					if (canHealPlayer != null && !canHealPlayer(owner)) return 0;
					int heals = healAmount + (bonusHealing ? bonus : 0), selfHeals = 0;
					if (customHealing is Delegate custom)
					{
						object[] args = { owner, owner, heals, selfHeals };
						try { custom.DynamicInvoke(args); heals = (int)args[2]; } catch (Exception) { heals = healAmount + (bonusHealing ? bonus : 0); }
					}
					return PlayerHealCompat.ScaleForSoldier(heals);
				});
			}
			catch (Exception e) { Warn("an area heal", e); }
		}

		// ---- CanBeHealed: the on-hit sweep, the wielder rule, the seek-heal bookkeeping ----

		private static bool CanBeHealedPost(bool result, Projectile projectile, Player healer, Player target, float radius, Func<Player, bool> canHealPlayer, int specificPlayer, bool ignoreHealer)
		{
			try
			{
				if (!ThoriumCompatSystem.Active || healer == null || _inSeek) return result;
				// a levy's melee swing runs Thorium's on-hit code with his stand-in as the healer (not the local player); everything else with the commander
				SoldierNPC striker = MeleeEffects.Striker;
				bool viaDummy = striker != null && !NoStrikerAB && ReferenceEquals(healer, striker.Dummy);
				if (healer != Main.LocalPlayer && !viaDummy) return result;
				if (_inThoriumHeal > 0 && _healStamp == Main.GameUpdateCount && !viaDummy)
				{
					// ThoriumHeal's own per-player test. Its postfix heals the soldiers; here only the rule for the wielder of a SOLDIER's heal
					return WielderRule(result, projectile, healer, target, radius, canHealPlayer, specificPlayer, ignoreHealer, viaDummy ? striker : null);
				}
				if (specificPlayer > -1 && specificPlayer < Main.maxPlayers) return WielderRule(result, projectile, healer, target, radius, canHealPlayer, specificPlayer, ignoreHealer, viaDummy ? striker : null);
				uint now = Main.GameUpdateCount;
				Player owner = Main.LocalPlayer;
				SoldierNPC caster = viaDummy ? striker : (OwnerPuppet.CasterOf(owner) ?? CasterOf(projectile));
				result = WielderRule(result, projectile, healer, target, radius, canHealPlayer, specificPlayer, ignoreHealer, viaDummy ? striker : null);

				// (0) a projectile choosing among Players with no radius of its own (a homing orb / bolt): note what it saw, so PostAI can steer it to a nearer wounded soldier
				if (projectile != null && radius <= 0f && projectile.TryGetGlobalProjectile(out PlayerCastGlobalProjectile sg))
				{
					if (sg.SeekTick != now) { sg.SeekTick = now; sg.SeekBest = float.MaxValue; }
					sg.SeekIgnoreHealer = ignoreHealer;
					if (result) sg.SeekBest = Math.Min(sg.SeekBest, Vector2.Distance(projectile.Center, target.Center));
				}

				// (1) a heal projectile that tests a radius around itself (a bolt that heals what it touches, a hit that heals allies in range): Thorium's own radius, around the projectile
				if (projectile != null && radius > 0f)
				{
					if (projectile.owner != owner.whoAmI || !projectile.TryGetGlobalProjectile(out PlayerCastGlobalProjectile g) || g.BaseHeal <= 0 || g.ContactTick == now) return result;
					g.ContactTick = now;
					int amount = PlayerHealCompat.ScaleForSoldier(g.BaseHeal + BonusOf(owner, caster));
					// a damage projectile (a hybrid's bolt) keeps all its hits for the enemy; only a pure heal projectile spends hits on the soldiers it heals
					PlayerHealCompat.HealAround(owner, projectile.Center, radius, _ => amount, projectile.damage > 0 ? null : projectile, caster, false, caster == null ? null : () => amount);
					return result;
				}

				// (2) a sweep with no radius of its own (an inline distance test) inside the hit of an ally-heal weapon: once per hit
				if (_sweepDone || _hitTick != now || _hitOwner < 0 || healer.whoAmI != _hitOwner) return result;
				if (projectile != null && projectile != _hitProj) return result;                 // another projectile's own targeting (a homing heal bolt) - not this hit's sweep
				_sweepDone = true;
				Sweep(projectile, healer, owner, caster, radius);
			}
			catch (Exception e) { Warn("an on-hit heal sweep", e); }
			return result;
		}

		/// <summary>
		/// Thorium never lets a healer heal himself unless the heal says so, and it sees the COMMANDER as the healer of everything his soldiers cast. For a soldier's heal the wielder is the soldier: the commander is an ally
		/// (healed by the Levy path, in his real position), and the soldier is the one a self-serving heal (an orb that heals its wielder, a heal aimed at the caster) can reach. Anything else is Thorium's own answer.
		/// </summary>
		private static bool WielderRule(bool result, Projectile projectile, Player healer, Player target, float radius, Func<Player, bool> canHealPlayer, int specificPlayer, bool ignoreHealer, SoldierNPC striker)
		{
			if (striker != null || !ReferenceEquals(target, Main.LocalPlayer) || !ReferenceEquals(healer, target)) return result;
			SoldierNPC caster = OwnerPuppet.CasterOf(healer) ?? CasterOf(projectile);
			if (caster == null) return result;
			bool explicitSelf = specificPlayer > -1 && specificPlayer < Main.maxPlayers && specificPlayer == target.whoAmI;
			if (!explicitSelf && ignoreHealer) return false; // the healer is not his own ally heal's target (the commander is served separately)
			NPC n = caster.NPC;
			if (!n.active || n.life <= 0 || n.life >= n.lifeMax) return false;
			if (canHealPlayer != null && caster.Dummy != null && !canHealPlayer(caster.Dummy)) return false;
			if (radius != 0f && projectile != null && Vector2.DistanceSquared(projectile.Center, n.Center) > radius * radius) return false;
			return true;
		}

		private static void Sweep(Projectile sweepProj, Player healer, Player owner, SoldierNPC caster, float radius)
		{
			Projectile anchor = sweepProj ?? _hitProj;
			Vector2 at = anchor != null && anchor.active ? anchor.Center : healer.Center;
			float r = radius > 0f ? radius : SweepDefaultRadius;
			int amount = PlayerHealCompat.ScaleForSoldier(_hitBase + BonusOf(owner, caster));
			PlayerHealCompat.HealAround(owner, at, r, _ => amount, null, caster, false, caster == null ? null : () => amount);
		}

		// ---- seek-and-heal projectiles: levies (and, for a soldier's cast, the commander) are candidates too ----

		/// <summary>
		/// A projectile that picked its target among Players this tick (Thorium's homing orbs and seek-heal bolts) also considers the wounded soldiers of the same commander - and, for a soldier's cast, the commander at his real
		/// position. When one is NEARER than anything Thorium found, the projectile is steered there instead (its velocity put back to what it was before its own steering, then Thorium's own speed and inertia), and on reaching it
		/// the heal is delivered the way that projectile delivers it (a HomingPro runs its own <c>Heal()</c>; any other seeker heals with its weapon's heal amount).
		/// </summary>
		internal static void SeekPost(Projectile p, PlayerCastGlobalProjectile g)
		{
			if (!ThoriumCompatSystem.Active || !p.active || p.owner != Main.myPlayer || p.penetrate == 0) return;
			Player owner = Main.LocalPlayer;
			if (owner == null || !owner.active || owner.dead || owner.whoAmI != p.owner) return;
			var hp = p.ModProjectile as ThoriumMod.Projectiles.Healer.HomingPro;
			if (hp != null && !hp.HomingOnPlayer) return;
			SoldierNPC caster = OwnerPuppet.CasterOf(owner) ?? CasterOf(p);
			float seen = g.SeekBest; // what Thorium's own pick found (our PlayerCanBeHomedInOn questions below must not change it)
			float best = Math.Min(hp != null ? hp.Distance : 750f, seen);
			uint now = Main.GameUpdateCount;
			NPC bestN = null; bool bestCmd = false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.life <= 0 || n.life >= n.lifeMax || n.ModNPC is not SoldierNPC s || s.Record == null || !s.IsAlive || !CompanionRequest.Serves(s, owner)) continue;
				if (ReferenceEquals(s, caster) && g.SeekIgnoreHealer) continue;
				float d = Vector2.Distance(p.Center, n.Center);
				if (d >= best) continue;
				if (hp != null)
				{
					// the projectile's own "may I home on this one" rule, asked about the soldier's stand-in (wounded, same team) - a heal that targets one particular Player says no
					Player dm = s.Dummy; if (dm == null) continue;
					int team = dm.team; dm.team = owner.team;
					bool ok; _inSeek = true;
					try { ok = hp.PlayerCanBeHomedInOn(owner, dm); } finally { dm.team = team; _inSeek = false; }
					if (!ok) continue;
				}
				else if (!Collision.CanHit(p.Center, 1, 1, n.Center, 1, 1)) continue;
				best = d; bestN = n; bestCmd = false;
			}
			if (caster != null)
			{
				OwnerPuppet.RealLife(owner, out int life, out int max);
				Vector2 c = OwnerPuppet.RealCenter(owner);
				float d = Vector2.Distance(p.Center, c);
				if (life < max && d < best && (hp != null || Collision.CanHit(p.Center, 1, 1, c, 1, 1))) { best = d; bestN = null; bestCmd = true; }
			}
			if (bestN == null && !bestCmd) return;

			Vector2 to = bestCmd ? OwnerPuppet.RealCenter(owner) : bestN.Center;
			float speed = hp != null ? Math.Max(0.5f, hp.Speed) : 7.5f, inertia = hp != null ? hp.Inertia : 20f;
			Vector2 want = (to - p.Center).SafeNormalize(Vector2.Zero) * speed;
			p.velocity = (g.PreVelocity * inertia + want) / (inertia + 1f);

			if (hp != null)
			{
				if (seen == float.MaxValue) hp.Heal(); // Thorium found nobody to home on, so it did not run the heal it runs for a target; this is that call
				return;
			}
			if (best > 40f || g.BaseHeal <= 0 || g.ContactTick == now) return;
			g.ContactTick = now;
			int amount = PlayerHealCompat.ScaleForSoldier(g.BaseHeal + BonusOf(owner, caster));
			if (bestCmd) PlayerHealCompat.HealAround(owner, to, 48f, _ => 0, p, caster, false, () => amount);
			else { int target = bestN.whoAmI; PlayerHealCompat.HealAround(owner, to, 48f, n2 => n2.whoAmI == target ? amount : 0, p, caster, false, null); }
		}

		private static bool _warned;
		private static void Warn(string what, Exception e)
		{
			if (_warned) return;
			_warned = true;
			Levy.Instance.Logger.Warn("Levy/Thorium compat: " + what + " threw (reported once): " + e.Message);
		}
	}
}
