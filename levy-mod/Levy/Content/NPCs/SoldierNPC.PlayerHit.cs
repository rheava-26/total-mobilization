using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Owner;
using Levy.Common.Util;
using Levy.Common.Weapons;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// THE PLAYER'S HIT SEQUENCE on a levy's gear dummy. Every hostile hit on a levy (contact and projectile) is decided by the sequence a real player's hit goes through, then applied to the body:
	/// <list type="number">
	/// <item><c>PlayerLoader.ModifyHurt</c> on the dummy: <c>CombinedHooks.ModifyHitByNPC / ModifyHitByProjectile</c> (the attacker's own ModifyHitPlayer hooks, every ModPlayer's ModifyHitByNPC / ModifyHitByProjectile
	/// such as Calamity's Tarragon and Bloodflare DR, Adrenaline DR and parries, the projectile difficulty multiplier) and every ModPlayer's ModifyHurt (energy shields via the <c>ModifyHurtInfo</c> event);</item>
	/// <item>the defense formula with the dummy's defense and the per-mode effectiveness (<c>HurtModifiers.ToHurtInfo</c>), which also yields the knockback and the immunity slot;</item>
	/// <item>the dodges: <c>PlayerLoader.FreeDodge</c> and <c>ConsumableDodge</c> (Calamity's dodge family);</item>
	/// <item>after the strike lands: <c>CombinedHooks.OnHitByNPC / OnHitByProjectile</c> and <c>Projectile.StatusPlayer</c> on the dummy (boss debuffs, Calamity's defense-damage flag), the debuffs the dummy caught moved onto the body, the knockback.</item>
	/// </list>
	/// Endurance (the dummy's, as a body field) and the set shields (solar, beetle, paladin) stay in the NPC's own intake after the formula, exactly where Player.Hurt applies them.
	/// <c>LEVY_PLAYERHIT=0</c> restores the earlier intake (A/B for the lab and the world check); <c>LEVY_HITDEBUFFS=0</c>, <c>LEVY_KNOCKBACK=0</c> and <c>LEVY_DEFDAMAGE=0</c> switch their part off alone.
	/// </summary>
	public partial class SoldierNPC
	{
		internal static readonly bool PlayerHitOn = Environment.GetEnvironmentVariable("LEVY_PLAYERHIT") != "0";
		internal static readonly bool HitDebuffsOn = PlayerHitOn && Environment.GetEnvironmentVariable("LEVY_HITDEBUFFS") != "0";
		internal static readonly bool KnockbackOn = PlayerHitOn && Environment.GetEnvironmentVariable("LEVY_KNOCKBACK") != "0";
		internal static readonly bool DefDamageOn = PlayerHitOn && Environment.GetEnvironmentVariable("LEVY_DEFDAMAGE") != "0";

		/// <summary>The strike being applied was decided by the player sequence (defense already subtracted in it): the NPC intake adds no defense.</summary>
		internal bool PreResolved;
		internal static long SeqRuns, SeqDodges, SeqErrors, DebuffMoves;

		internal struct HitResult { public bool Ran, Dodged; public int Damage; public Player.HurtInfo Info; public int DummyImmune; }
		internal HitResult LastProjectileResult;
		private int _knockTicks;
		/// <summary>The projectile whose hit GearOnHurt is replaying on the dummy (so the on-hit hooks see a projectile, as for a player).</summary>
		internal Projectile HurtProjectile;
		/// <summary>The sequence's source damage for the hit GearOnHurt is replaying (Calamity's defense damage is the damage the defense mitigated: source minus final).</summary>
		internal int SeqSourceDamage;
		private readonly System.Collections.Generic.Dictionary<int, int> _buffSnap = new();

		/// <summary>The immunity slot a hostile projectile's hit takes on a player (Projectile.Damage): Moon Lord's and the Empress's use slot 1, a ModProjectile names its own.</summary>
		internal static int ProjectileSlot(Projectile p)
		{
			int slot = -1;
			switch (p.type)
			{
				case 452: case 454: case 455: case 462: case 871: case 872: case 873: case 874: case 919: case 923: case 924: slot = 1; break;
			}
			if (p.ModProjectile != null) slot = p.ModProjectile.CooldownSlot;
			return slot;
		}

		/// <summary>The defense the sequence uses: the dummy's live number while the gear frame runs (Calamity's defense damage moves it every hit), else the body's.</summary>
		private int HitDefense()
		{
			Player d = Dummy;
			int def = !Hostile && d != null && UpkeepActive ? Math.Max(0, (int)d.statDefense) : NPC.defense;
			return Math.Max(0, def - DotDefenseDown);
		}

		private struct Opened { public FrameSlot Fs; public object Thorium; public SoldierNPC Prev; public int Spawn; public int SavedWho; public Vector2 SavedCenter; }

		private Opened OpenDummy()
		{
			Player d = Dummy;
			var o = new Opened { SavedWho = d.whoAmI, SavedCenter = d.Center, Prev = GearUpkeep.Body, Spawn = GearUpkeep.Spawned.Count };
			GearUpkeep.Body = this;
			d.Center = NPC.Center;
			o.Fs = EnterFrameSlot();
			o.Thorium = OwnerPuppet.SwapThorium(d, true);
			return o;
		}

		private void CloseDummy(Opened o, bool adopt)
		{
			Player d = Dummy;
			ExitFrameSlot(o.Fs);
			d.whoAmI = o.SavedWho; d.Center = o.SavedCenter;
			OwnerPuppet.RestoreThorium(o.Thorium);
			GearUpkeep.Body = o.Prev;
			if (adopt) AdoptUpkeepSpawns(o.Spawn, false);
		}

		/// <summary>
		/// Steps 1 to 3 for one hit. <paramref name="baseDamage"/> is what the player path hands Player.Hurt (contact: DamageVar of the attacker's damage; projectile: DamageVar of the projectile's damage, doubled).
		/// Returns Ran false when the sequence is off or threw (the caller then uses the earlier intake).
		/// </summary>
		internal HitResult PlayerSequence(NPC a, Projectile p, int baseDamage, int dir, int cooldownSlot)
		{
			var r = new HitResult();
			Player d = Dummy;
			if (!PlayerHitOn || d == null || LevyUtil.IsClient || Record == null) return r;
			Opened o = default; bool opened = false;
			try
			{
				o = OpenDummy(); opened = true;
				d.immune = false; d.immuneTime = 0; Array.Clear(d.hurtCooldowns);
				_buffSnap.Clear(); for (int i = 0; i < d.buffType.Length; i++) if (d.buffType[i] > 0) _buffSnap[d.buffType[i]] = d.buffTime[i];
				var reason = a != null ? PlayerDeathReason.ByNPC(a.whoAmI) : PlayerDeathReason.ByProjectile(-1, p.whoAmI);
				var hm = new Player.HurtModifiers { DamageSource = reason, CooldownCounter = cooldownSlot, Dodgeable = true, HitDirection = dir };
				PlayerLoader.ModifyHurt(d, ref hm);
				if (p != null) hm.ArmorPenetration += p.ArmorPenetration;
				if (Endurance > 0f) hm.FinalDamage *= 1f - Endurance; // Player.Hurt's ApplyVanillaHurtEffectModifiers: endurance (Endurance potion, Worm Scarf, Brain of Confusion, Calamity's DR)
				var info = hm.ToHurtInfo(Math.Max(1, baseDamage), HitDefense(), LevyUtil.ModeDefenseEffectiveness(), 4.5f, d.noKnockback);
				r.Info = info; r.Damage = info.Damage; r.Ran = true; SeqRuns++;
				if (info.Cancelled) r.Dodged = true;
				else if (info.Dodgeable && (PlayerLoader.FreeDodge(d, in info) || PlayerLoader.ConsumableDodge(d, in info))) r.Dodged = true;
				if (r.Dodged)
				{
					SeqDodges++; r.DummyImmune = d.immuneTime;
					int t = LevyConstants.ScaledImmune(d.immuneTime);
					if (t > 0) { if (t > ImmuneTime) ImmuneTime = t; if (t > AllImmune) AllImmune = t; }
				}
				d.immune = false; d.immuneTime = 0; Array.Clear(d.hurtCooldowns);
			}
			catch (Exception e)
			{
				SeqErrors++; r.Ran = false;
				if (GearUpkeep.WarnOnce(e)) Levy.Instance?.Logger.Warn("Levy: the player hit sequence threw on a soldier dummy (once per kind): " + e);
			}
			finally { if (opened) CloseDummy(o, false); }
			return r;
		}

		/// <summary>Step 4: the hit landed. The attacker's on-hit hooks run on the dummy, the debuffs it caught move onto the body, the knockback is taken.</summary>
		internal void PlayerOnHit(NPC a, Projectile p, in HitResult res, int dir)
		{
			Player d = Dummy;
			if (!res.Ran || res.Dodged || d == null || LevyUtil.IsClient || !IsAlive) return;
			if (HitDebuffsOn)
			{
				Opened o = default; bool opened = false;
				try
				{
					o = OpenDummy(); opened = true;
					var info = res.Info;
					if (!UpkeepActive) { if (a != null) CombinedHooks.OnHitByNPC(d, a, in info); else if (p != null) CombinedHooks.OnHitByProjectile(d, p, in info); } // the gear frame's Player.Hurt already ran them
					if (a != null) d.StatusFromNPC(a); else if (p != null) p.StatusPlayer(DummySlot);
					for (int i = 0; i < d.buffType.Length; i++)
					{
						int type = d.buffType[i], time = d.buffTime[i];
						if (type <= 0 || !Main.debuff[type]) continue;
						if (_buffSnap.TryGetValue(type, out int t0) && t0 >= time) continue;
						NPC.buffImmune[type] = d.buffImmune[type]; // the dummy's own gear immunity decides, as for a player
						NPC.AddBuff(type, time);
						d.DelBuff(i); i--; DebuffMoves++;
					}
				}
				catch (Exception e) { SeqErrors++; if (GearUpkeep.WarnOnce(e)) Levy.Instance?.Logger.Warn("Levy: the on-hit hooks threw on a soldier dummy (once per kind): " + e); }
				finally { if (opened) CloseDummy(o, true); }
			}
			if (KnockbackOn && res.Info.Knockback != 0f && dir != 0 && !OfficerMode)
			{
				NPC.velocity.X = res.Info.Knockback * dir;
				NPC.velocity.Y = res.Info.Knockback * -7f / 9f;
				_knockTicks = 12; NPC.netUpdate = true;
			}
		}

		/// <summary>The body is in a knockback: its steering waits (a few ticks) so the player's push is not cancelled the same tick.</summary>
		private bool Knock_Hold()
		{
			if (_knockTicks <= 0) return false;
			_knockTicks--;
			if (!IsAlive || NPC.wet || NPC.lavaWet) { _knockTicks = 0; return false; }
			if (_knockTicks < 9 && NPC.collideY && NPC.velocity.Y >= 0f) { _knockTicks = 0; return false; }
			return true;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Damage over time by the player's rules
		// ------------------------------------------------------------------------------------------------------------
		private static Player _dotScratch;
		private int _dotCount;
		/// <summary>Defense the body's debuffs take away (Ichor, Broken Armor ...), read from the game's own buff update on a scratch player.</summary>
		internal int DotDefenseDown;
		internal static long DotTicks, DotDamage;

		/// <summary>
		/// The debuffs on the body, run through the game's own player rules: a scratch Player holds the same debuffs, UpdateBuffs and UpdateLifeRegen run on it (vanilla's switch and every ModBuff / ModPlayer
		/// life-regen hook, Calamity's included) and the life it loses is the body's loss, one point per 120 units of negative regeneration as for a player. The NPC's own damage over time is switched
		/// off for a levy (<see cref="LevyDotSystem"/>), whose numbers differ per mod.
		/// </summary>
		internal void DebuffDot()
		{
			if (!HitDebuffsOn || LevyUtil.IsClient || !IsAlive) return;
			bool any = false;
			for (int i = 0; i < NPC.maxBuffs; i++) if (NPC.buffType[i] > 0 && NPC.buffTime[i] > 0 && Main.debuff[NPC.buffType[i]]) { any = true; break; }
			if (!any) { _dotCount = 0; DotDefenseDown = 0; return; }
			Player s = _dotScratch ??= new Player { whoAmI = Main.maxPlayers, active = true, width = 20, height = 42 };
			Player prev = Main.player[Main.maxPlayers]; int prevMy = Main.myPlayer;
			try
			{
				Main.player[Main.maxPlayers] = s; Main.myPlayer = Main.maxPlayers; // a player's own rules run for the local player
				s.ResetEffects();
				Array.Clear(s.buffType); Array.Clear(s.buffTime);
				int k = 0;
				for (int i = 0; i < NPC.maxBuffs && k < s.buffType.Length; i++) if (NPC.buffType[i] > 0 && NPC.buffTime[i] > 0 && Main.debuff[NPC.buffType[i]]) { s.buffType[k] = NPC.buffType[i]; s.buffTime[k] = NPC.buffTime[i]; k++; }
				s.statLifeMax = s.statLifeMax2 = Math.Max(1, NPC.lifeMax); s.statLife = Math.Clamp(NPC.life, 1, s.statLifeMax2); int life0 = s.statLife; s.dead = false; s.lifeRegenCount = _dotCount; s.lifeRegenTime = 0f;
				s.position = NPC.position; s.width = NPC.width; s.height = NPC.height;
				s.UpdateBuffs(Main.maxPlayers);
				s.UpdateLifeRegen();
				int loss = Math.Max(0, life0 - s.statLife); _dotCount = s.lifeRegenCount; DotDefenseDown = s.statDefense.Negative; DotTicks++;
				if (loss > 0)
				{
					DotDamage += loss;
					NPC.life -= loss; NPC.netUpdate = true;
					if (Record != null) Record.CurrentHp = Math.Max(0, NPC.life);
					if (NPC.life <= 0) { NPC.life = 0; NPC.checkDead(); }
				}
			}
			catch (Exception e) { SeqErrors++; if (GearUpkeep.WarnOnce(e)) Levy.Instance?.Logger.Warn("Levy: the damage-over-time step threw on a soldier (once per kind): " + e); }
			finally { Main.player[Main.maxPlayers] = prev; Main.myPlayer = prevMy; }
		}
	}

	/// <summary>Switches the NPC's own buff damage over time off for a levy body (vanilla NPC.UpdateNPC_BuffApplyDOTs): the player-rule step in SoldierNPC.DebuffDot replaces it.</summary>
	public sealed class LevyDotSystem : ModSystem
	{
		private delegate void Orig(NPC self);
		public override void Load()
		{
			if (!SoldierNPC.HitDebuffsOn) return;
			try
			{
				var m = typeof(NPC).GetMethod("UpdateNPC_BuffApplyDOTs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
				if (m == null) { Mod.Logger.Warn("Levy: NPC.UpdateNPC_BuffApplyDOTs not found; a levy's debuff damage stays the NPC's"); return; }
				MonoModHooks.Add(m, (Action<Orig, NPC>)Detour);
			}
			catch (Exception e) { Mod.Logger.Warn("Levy: damage-over-time hook failed: " + e.Message); }
		}
		private static void Detour(Orig orig, NPC self) { if (self.ModNPC is SoldierNPC) return; orig(self); }
	}
}
