using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Creative;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Content.NPCs;

namespace Levy.Common.Util
{
	/// <summary>
	/// What a hostile projectile does to a levy: the damage it would do to a player standing there with the levy's stats. Same principle as <see cref="HostileContact"/> for contact.
	/// <para/>The cause this fixes (read from the decompiled game): a projectile that hits a PLAYER goes through Projectile.Damage to Player.Hurt, which runs <c>CombinedHooks.ModifyHitByProjectile</c>
	/// (the projectile's own <c>ModifyHitPlayer</c> hooks, then the difficulty multiplier <c>Main.GameModeInfo.EnemyDamageMultiplier</c>: x2 in Expert, x3 in Master) before the defense formula. The same projectile
	/// hitting a friendly NPC goes through <c>modifiers.ToHitInfo</c> instead and none of that runs, so a levy took the projectile's bare damage (the x2 every player hit gets aside) minus its defense. A mod that
	/// stores its boss shots at the classic damage and leaves the difficulty scaling to that player hook (Calamity does: its Calamitas Clone dart is 22 and "88" is the Expert number) hit levies for the floor of 1.
	/// <para/>The hostile-shot hook of <see cref="Content.NPCs.SoldierNPC"/> calls <see cref="Modify"/> to fold into the NPC hit exactly what the player path folds into the player hit: the projectile's
	/// <c>ModifyHitPlayer</c> hooks (Calamity's flat and multiplicative projectile DR, Whispering Death; every ModProjectile that scales its damage there), the cold-resistance cut and the difficulty multiplier
	/// (with the same <c>reflected</c> and <c>PlayerHurtDamageIgnoresDifficultyScaling</c> exemptions). The defense subtraction is unchanged: it already uses the player's per-mode value
	/// (<see cref="SoldierNPC.ModifyIncomingHit"/>). <see cref="OnHit"/> runs the player-side on-hit debuffs (<c>Projectile.StatusPlayer</c> and the projectile's <c>OnHitPlayer</c> hooks) against a scratch
	/// player that carries the levy's debuff immunities, and gives the levy the debuffs that player would have caught (an NPC that refuses a buff still refuses it).
	/// <para/>NOT run: PlayerLoader.ModifyHitByProjectile and ModPlayer.ModifyHurt (they read real-player state: dodges, reflects, accessories that kill or turn the projectile; a levy's own accessory
	/// reactions run in <see cref="SoldierNPC.GearOnHurt"/>). Shots fired by a hostile Levy body (Warbands) keep the damage <see cref="SoldierNPC.HostileShotDamage"/> already set for them.
	/// <c>LEVY_PROJPARITY=0</c> restores the earlier damage (A/B switch for the lab and the world check); <c>LEVY_PROJPARITY_DEBUFFS=0</c> keeps the damage rule but skips the debuffs.
	/// </summary>
	public static class HostileProjectile
	{
		public static readonly bool On = Environment.GetEnvironmentVariable("LEVY_PROJPARITY") != "0";
		public static readonly bool Debuffs = On && Environment.GetEnvironmentVariable("LEVY_PROJPARITY_DEBUFFS") != "0";

		/// <summary>Harness telemetry: hits the rule shaped, hits where a projectile hook threw, debuffs given.</summary>
		public static long Shaped, HookErrors, DebuffsGiven;
		private static bool _warned;
		private static Player _scratch;

		/// <summary>The enemy damage multiplier a player's hit takes from a projectile (CombinedHooks.ModifyHitByProjectile).</summary>
		public static float DifficultyMultiplier(Projectile proj)
		{
			if (proj.reflected || ProjectileID.Sets.PlayerHurtDamageIgnoresDifficultyScaling[proj.type]) return 1f;
			float m = Main.GameModeInfo.EnemyDamageMultiplier;
			if (Main.GameModeInfo.IsJourneyMode)
			{
				var power = CreativePowerManager.Instance.GetPower<CreativePowers.DifficultySliderPower>();
				if (power.GetIsUnlocked()) m = power.StrengthMultiplierToGiveNPCs;
			}
			return m;
		}

		/// <summary>True for a shot a hostile Levy body fired (its damage is already sized for the player side, SoldierNPC.HostileShotDamage).</summary>
		private static bool OwnShot(Projectile proj) => proj.TryGetGlobalProjectile<SoldierProjectileTag>(out var tag) && tag.HostileShot;

		/// <summary>
		/// Folds into <paramref name="m"/> what the player path folds into a player's hit from <paramref name="proj"/>. <paramref name="who"/> is the levy's gear dummy (or a stand-in).
		/// Called from the NPC's ModifyHitByProjectile, after the x2 every hostile projectile gets against a player.
		/// </summary>
		public static void Modify(Projectile proj, Player who, ref NPC.HitModifiers m)
		{
			if (!On || proj == null || who == null || OwnShot(proj)) return;
			Player.HurtModifiers hm = new Player.HurtModifiers { DamageSource = PlayerDeathReason.ByProjectile(-1, proj.whoAmI) };
			bool cancelled = false;
			try
			{
				ProjectileLoader.ModifyHitPlayer(proj, who, ref hm); // the projectile's own scaling: Calamity's flat / multiplicative DR, Whispering Death, every ModProjectile.ModifyHitPlayer
				cancelled = hm.ToHurtInfo(1, 0, 0.5f, 0f, true).Cancelled;
			}
			catch (Exception e)
			{
				HookErrors++;
				if (!_warned) { _warned = true; Levy.Instance?.Logger.Warn($"[HostileProjectile] {proj.Name} threw {e.GetType().Name} in its player hit hooks ({e.Message}); the difficulty rule still applies"); }
			}
			if (who.resistCold && proj.coldDamage) hm.IncomingDamageMultiplier *= 0.7f; // CombinedHooks.ModifyHitByProjectile
			hm.SourceDamage *= DifficultyMultiplier(proj);
			m.SourceDamage = m.SourceDamage.CombineWith(hm.SourceDamage);
			m.TargetDamageMultiplier *= hm.IncomingDamageMultiplier.Value;
			m.FinalDamage = m.FinalDamage.CombineWith(hm.FinalDamage);
			m.ArmorPenetration += hm.ArmorPenetration.Value;
			m.ScalingArmorPenetration += hm.ScalingArmorPenetration.Value;
			if (cancelled) m.FinalDamage *= 0f; // a hook that turned the hit away: the floor of 1 is all an NPC strike can be
			Shaped++;
		}

		/// <summary>
		/// The debuffs a player would have caught from this hit, given to the levy. Runs the projectile's player-side hit effects on a scratch player that carries the levy's
		/// debuff immunities, then moves what it caught onto the body; <see cref="NPC.AddBuff"/> applies the body's own immunities.
		/// </summary>
		public static void OnHit(Projectile proj, SoldierNPC levy, int damageDone)
		{
			if (!Debuffs || proj == null || levy == null || levy.NPC == null || !levy.NPC.active || OwnShot(proj) || LevyUtil.IsClient) return;
			Player dummy = levy.Dummy;
			Player prev = Main.player[Main.maxPlayers];
			try
			{
				Player s = _scratch ??= new Player { whoAmI = Main.maxPlayers, active = true, width = 20, height = 42 };
				Array.Clear(s.buffType); Array.Clear(s.buffTime);
				if (dummy != null) Array.Copy(dummy.buffImmune, s.buffImmune, Math.Min(dummy.buffImmune.Length, s.buffImmune.Length)); else Array.Clear(s.buffImmune);
				s.statLife = s.statLifeMax = s.statLifeMax2 = 100000; s.dead = false; s.immune = false; s.creativeGodMode = false;
				s.position = levy.NPC.position; s.width = levy.NPC.width; s.height = levy.NPC.height;
				Main.player[Main.maxPlayers] = s;
				proj.StatusPlayer(Main.maxPlayers);
				var info = new Player.HurtInfo { DamageSource = PlayerDeathReason.ByProjectile(-1, proj.whoAmI), Damage = Math.Max(1, damageDone), CooldownCounter = -1, Dodgeable = false, PvP = false, SoundDisabled = true, DustDisabled = true };
				ProjectileLoader.OnHitPlayer(proj, s, in info);
				for (int i = 0; i < s.buffType.Length; i++)
				{
					int type = s.buffType[i], time = s.buffTime[i];
					if (type <= 0 || time <= 0) continue;
					levy.NPC.AddBuff(type, time);
					DebuffsGiven++;
				}
			}
			catch (Exception e)
			{
				HookErrors++;
				if (!_warned) { _warned = true; Levy.Instance?.Logger.Warn($"[HostileProjectile] {proj.Name} threw {e.GetType().Name} in its player-side hit effects ({e.Message})"); }
			}
			finally { Main.player[Main.maxPlayers] = prev; }
		}
	}
}
