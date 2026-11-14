using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Globals;
using Levy.Common.Owner;
using Levy.Common.Targeting;
using Levy.Content.NPCs;
using Levy.Content.Projectiles;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// A levy's whip is the item's own: the whip projectile the item's Shoot hook makes (vanilla aiStyle Whip with its control points, tip, crack and dust, or a modded whip's own projectile and hooks), run through the
	/// real weapon-use path the same way a player's swing is (<see cref="HeldWeapons.ShootHooks"/>: ModifyShootStats, then Shoot, with the soldier's dummy as the player holding the item).
	/// <para/><b>Anchoring.</b> The lash is spawned owned by the dummy slot and joined to the soldier's chain (<see cref="HeldWeapons.JoinChain"/>), so its AI runs with the soldier's dummy standing in Main.player[255]:
	/// the vanilla whip AI reads the owner's arm position, direction, whip range multiplier and use animation from that dummy, so the lash is thrown from the soldier's hand. For the length of each AI call the
	/// dummy also holds the item and shows the use animation the swing started (<see cref="AiScope"/>), because the whip ends when its owner's animation ends. The lash's damage pass is the chain's: the projectile is
	/// handed to the real commander for that one call (HeldWeapons.RunDamage); <see cref="DamageScope"/> puts every field the call could write on the commander (the use state and the minion focus a whip hit sets) back.
	/// <para/><b>Tag.</b> A whip's tag is the game's own: the debuff the whip projectile puts on the enemy (vanilla StatusNPC, or the modded whip's OnHitNPC) and the damage bonus every minion and minion shot gets against
	/// it. Nothing here reimplements the bonus. Levy only remembers which enemy was lashed (<see cref="LevyNpcData.ApplyTag"/>) so the levy's own minions prefer it.
	/// <para/>Falls back to the hand-made <see cref="SoldierWhip"/> when the real path is off (LEVY_WHIPREAL=0, or RealWeaponShoot off in the config) or the item's hooks make no whip.
	/// </summary>
	public static class RealWhips
	{
		/// <summary>Lab A/B switch: LEVY_WHIPREAL=0 runs the levy half on the old hand-made lash.</summary>
		public static bool Disabled = Environment.GetEnvironmentVariable("LEVY_WHIPREAL") == "0";
		/// <summary>Lab A/B switch: LEVY_WHIPRESTORE=0 leaves the commander as the lash's hit left it (the use guard's snapshot, the minion focus, life, mana and buffs are not put back), to show what the hit writes.</summary>
		public static bool NoRestore = Environment.GetEnvironmentVariable("LEVY_WHIPRESTORE") == "0";

		/// <summary>Lab counters: swings started on the real path, those that fell back, hooks that threw.</summary>
		public static int Real, Fallbacks, Threw;
		/// <summary>Measurement (the world check turns it on): Stopwatch ticks spent starting a lash, running its AI and its damage pass, and the call counts.</summary>
		public static bool Measure; public static long StartTicks, AiTicks, DamageTicks, ScopeTicks; public static int Starts, AiCalls, DamageCalls;

		public static void ResetCost() { StartTicks = AiTicks = DamageTicks = ScopeTicks = 0; Starts = AiCalls = DamageCalls = 0; }
		public static long Begin() => Measure ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
		public static void End(ref long acc, long t0) { if (t0 != 0L) acc += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }

		private const int Slot = SoldierNPC.DummySlot;
		private static readonly Dictionary<int, int> _live = new();

		internal static void Reset() { _live.Clear(); _reach.Clear(); _reachFinal.Clear(); }

		/// <summary>True for a whip projectile type (vanilla or modded: the IsAWhip set, or the whip AI).</summary>
		public static bool IsWhipType(int type, int aiStyle = -1) => (type > 0 && type < ProjectileID.Sets.IsAWhip.Length && ProjectileID.Sets.IsAWhip[type]) || aiStyle == ProjAIStyleID.Whip;
		public static bool IsWhipProjectile(Projectile p) => IsWhipType(p.type, p.aiStyle);

		/// <summary>True when the item's shot is a whip projectile.</summary>
		public static bool IsWhipItem(Item item)
		{
			if (item == null || item.IsAir || item.shoot <= 0 || item.shoot >= ProjectileLoader.ProjectileCount) return false;
			if (IsWhipType(item.shoot)) return true;
			return ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out Projectile sample) && sample.aiStyle == ProjAIStyleID.Whip;
		}

		/// <summary>This soldier's lash that is still out, or null.</summary>
		public static Projectile LiveFor(SoldierNPC s)
		{
			int idx = s.NPC.whoAmI;
			if (!_live.TryGetValue(idx, out int pi)) return null;
			if (pi >= 0 && pi < Main.maxProjectiles)
			{
				Projectile p = Main.projectile[pi];
				if (p.active && IsWhipProjectile(p) && p.TryGetGlobalProjectile(out HeldWeaponProjectile g) && g.Chain && g.Whip && ReferenceEquals(g.Record, s.Record)
					&& p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex == idx)
					return p;
			}
			_live.Remove(idx);
			return null;
		}

		/// <summary>
		/// Starts a lash through the item's own use. Returns false when the real path does not apply (the caller uses the hand-made lash); otherwise true, with <paramref name="fired"/> telling whether a lash started
		/// (false while the previous one is still out).
		/// </summary>
		internal static bool TryStart(SoldierNPC s, Item item, WeaponProfile profile, NPC target, out bool fired)
		{
			fired = false;
			if (Disabled || !LevyConfig.Get.RealWeaponShoot || s.Dummy == null || !IsWhipItem(item)) return false;
			if (LiveFor(s) != null) { s.AttackCooldown = 1; return true; } // the lash is out: the next one follows the moment it ends
			long t0 = Begin();
			Player d = s.Dummy;
			Vector2 dir = target.Center - s.NPC.Center;
			if (dir == Vector2.Zero) dir = Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction);
			dir = Vector2.Normalize(dir);
			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier * LevyConstants.WhipRealDamageMult));
			List<Projectile> made = HeldWeapons.ShootHooks(s, item, dir * Math.Max(1f, item.shootSpeed), damage, Slot, false, null, out bool threw);
			Projectile whip = null;
			foreach (Projectile c in made)
			{
				if (whip == null && c.owner == Slot && IsWhipProjectile(c)) whip = c;
				else WeaponUse.TagSoldierProjectile(s, c, item.DamageType ?? DamageClass.Default); // anything else the item's hook fired is an ordinary soldier shot
			}
			if (threw) Threw++;
			if (whip == null) { Fallbacks++; End(ref StartTicks, t0); return false; }
			HeldWeapons.JoinChain(s, whip, item);
			if (whip.TryGetGlobalProjectile(out HeldWeaponProjectile g)) g.Whip = true;
			whip.ownerHitCheck = false; // vanilla gates a whip's hit on a clear line from its owner; its owner is the commander here, so the line is taken from the soldier (HeldWeaponProjectile.CanHitNPC)
			_live[s.NPC.whoAmI] = whip.whoAmI;
			int anim = TotalAnimation(d, item), use = Math.Max(1, CombinedHooks.TotalUseTime(Math.Max(1, item.useTime), d, item));
			WeaponUse.FinishUse(s, item, dir);
			s.ItemAnimation = s.ItemAnimationMax = anim;
			s.AttackCooldown = Math.Max(1, use);
			Real++; Starts++;
			fired = true;
			End(ref StartTicks, t0);
			return true;
		}

		/// <summary>The use animation of the item for this dummy (a player's itemAnimationMax at the start of a use: melee speed and every speed bonus included).</summary>
		internal static int TotalAnimation(Player d, Item item) => Math.Max(1, CombinedHooks.TotalAnimationTime(Math.Max(1, item.useAnimation), d, item));

		// The lash's real reach, read off the vanilla control points the first time each whip is thrown: the brain's idea of the whip's range (the profile's estimate is a guess from shootSpeed alone: the real
		// reach swings from 180 to 530 px at the same shootSpeed, with the whip's segments and range multiplier).
		private static readonly Dictionary<int, float> _reach = new();
		private static readonly HashSet<int> _reachFinal = new();
		private static readonly List<Vector2> _pts = new(48);

		/// <summary>The measured reach of an item's lash in px (0 until the first swing has been seen).</summary>
		public static float MeasuredReach(int itemType) => _reachFinal.Contains(itemType) && _reach.TryGetValue(itemType, out float r) ? r : 0f;

		/// <summary>Called inside a lash's AI scope (the dummy stands in the owner slot): follows the tip's extension and, once the lash starts to retract, records the reach on the item's profile.</summary>
		internal static void NoteReach(Projectile p, Item item)
		{
			if (_reachFinal.Contains(item.type)) return;
			_pts.Clear();
			try { Projectile.FillWhipControlPoints(p, _pts); } catch (Exception) { return; }
			if (_pts.Count < 2) return;
			float r = 0f;
			for (int i = 1; i < _pts.Count; i++) { float d = Vector2.Distance(_pts[0], _pts[i]); if (!float.IsNaN(d)) r = Math.Max(r, d); }
			float prev = _reach.TryGetValue(item.type, out float v) ? v : 0f;
			if (r >= prev) { _reach[item.type] = r; return; }
			if (prev < 16f || r > prev * 0.9f) return; // still on the way out (or a hiccup)
			_reachFinal.Add(item.type);
			WeaponProfile prof = WeaponClassifier.GetProfile(item);
			if (prof != null && prof.Family == WeaponFamily.Whip)
			{
				prof.ThrustReachPx = prev;
				prof.RangeTiles = Math.Clamp((int)Math.Round(prev / LevyConstants.TilePx), LevyConstants.WhipRangeMinTiles, LevyConstants.WhipRangeMaxTiles);
			}
		}

		/// <summary>Kills this soldier's lash (dismissed, dead, or its weapon swapped).</summary>
		internal static void KillFor(SoldierNPC s)
		{
			Projectile p = LiveFor(s);
			if (p != null) p.Kill();
			_live.Remove(s.NPC.whoAmI);
		}

		/// <summary>
		/// For one AI call of a lash: the dummy holds the item and stands in the use animation the swing started. The vanilla whip ends when <c>Main.player[owner].itemAnimation == 0</c> and sizes its flight by
		/// itemAnimationMax; the dummy never runs ItemCheck, so its animation is lent for the call and put back. A client has no running brain: its lash lives by its own clock (animation held at 1).
		/// </summary>
		internal readonly struct AiScope : IDisposable
		{
			private readonly Player _d; private readonly int _anim, _animMax, _dir, _sel, _my; private readonly Item _inv0;
			public AiScope(SoldierNPC body, Projectile p, Item item, bool authority)
			{
				_d = body.Dummy; _anim = _d.itemAnimation; _animMax = _d.itemAnimationMax; _dir = _d.direction; _sel = _d.selectedItem; _inv0 = _d.inventory[0];
				_my = Main.myPlayer;
				if (authority) Main.myPlayer = p.owner; // a modded lash makes its side projectiles (Thorium's Thrombosis drops its blood) only when its owner is the local player; the dummy stands in that slot
				_d.inventory[0] = item; _d.selectedItem = 0;
				_d.itemAnimationMax = TotalAnimation(_d, item);
				_d.itemAnimation = authority ? Math.Max(1, body.ItemAnimation) : 1;
				if (p.velocity.X != 0f) _d.direction = p.velocity.X < 0f ? -1 : 1;
			}
			public void Dispose()
			{
				_d.itemAnimation = _anim; _d.itemAnimationMax = _animMax; _d.direction = _dir; _d.selectedItem = _sel; _d.inventory[0] = _inv0;
				Main.myPlayer = _my;
			}
		}

		/// <summary>
		/// For the lash's damage pass, which runs with the projectile handed to the real commander (the chain's rule: vanilla only damages for owner == Main.myPlayer, and a fake player in that slot hung the
		/// kill cascade of a boss). The vanilla whip finds its hit line through its owner: <c>FillWhipControlPoints</c> reads the owner's use animation, whip range, arm and body, so the commander is lent the
		/// soldier's for the call (the dummy's, exactly what the AI pass saw) and every one of them is put back, together with the use state (the use guard's snapshot) and the minion focus a whip hit sets.
		/// </summary>
		internal readonly struct DamageScope : IDisposable
		{
			private readonly UseGuard.Snap _snap; private readonly Player _real; private readonly int _focus, _anim, _animMax, _dir, _life, _mana; private readonly float _range, _grav, _gfx; private readonly Vector2 _pos; private readonly Rectangle _frame; private readonly long _t0;
			// one scope is open at a time (the chain's damage wrapper does not nest), so its copies of the commander's buffs live in two shared arrays instead of two allocations per call
			private static readonly int[] BuffType = new int[Player.MaxBuffs], BuffTime = new int[Player.MaxBuffs];
			public DamageScope(SoldierNPC body, Item item)
			{
				int o = SoldierMinions.TrueMyPlayer;
				_real = o >= 0 && o < Main.maxPlayers ? Main.player[o] : null;
				_t0 = Begin(); long ts = _t0;
				if (_real == null) { _snap = default; _focus = _anim = _animMax = _dir = _life = _mana = 0; _range = _grav = _gfx = 0f; _pos = default; _frame = default; return; }
				_life = _real.statLife; _mana = _real.statMana; Array.Copy(_real.buffType, BuffType, BuffType.Length); Array.Copy(_real.buffTime, BuffTime, BuffTime.Length);
				_snap = UseGuard.Disabled ? default : UseGuard.Take(o);
				_focus = _real.MinionAttackTargetNPC; _anim = _real.itemAnimation; _animMax = _real.itemAnimationMax; _dir = _real.direction; _range = _real.whipRangeMultiplier; _grav = _real.gravDir; _gfx = _real.gfxOffY; _pos = _real.position; _frame = _real.bodyFrame;
				Player d = body.Dummy;
				_real.itemAnimationMax = TotalAnimation(d, item); _real.itemAnimation = Math.Max(1, body.ItemAnimation);
				_real.whipRangeMultiplier = d.whipRangeMultiplier; _real.direction = body.NPC.direction == 0 ? 1 : body.NPC.direction; _real.gravDir = 1f; _real.gfxOffY = 0f;
				_real.position = body.NPC.position; _real.bodyFrame = d.bodyFrame;
				if (ts != 0L) ScopeTicks += System.Diagnostics.Stopwatch.GetTimestamp() - ts;
			}
			public void Dispose()
			{
				long ts = Begin();
				if (_real != null)
				{
					_real.itemAnimation = _anim; _real.itemAnimationMax = _animMax; _real.direction = _dir; _real.whipRangeMultiplier = _range; _real.gravDir = _grav; _real.gfxOffY = _gfx; _real.position = _pos; _real.bodyFrame = _frame;
					if (!NoRestore)
					{
						UseGuard.Put(_snap);
						_real.MinionAttackTargetNPC = _focus;
						// what the lash's on-hit hooks wrote on their "owner": a buff a hit handed out, a heal, a mana refund
						_real.statLife = _life; _real.statMana = _mana;
						Array.Copy(BuffType, _real.buffType, BuffType.Length); Array.Copy(BuffTime, _real.buffTime, BuffTime.Length);
					}
				}
				if (ts != 0L) ScopeTicks += System.Diagnostics.Stopwatch.GetTimestamp() - ts;
				if (_t0 != 0L) { End(ref DamageTicks, _t0); DamageCalls++; }
			}
		}

		/// <summary>The lash connected: the levy's own minions remember the enemy (the vanilla tag debuff and its damage bonus come from the whip projectile itself).</summary>
		internal static void OnHit(Projectile p, NPC target)
		{
			if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0) LevyNpcData.ApplyTag(target, tag.SoldierIndex);
		}

		/// <summary>The line the vanilla whip checks from its owner, taken from the soldier: false when a wall stands between the soldier and the enemy.</summary>
		internal static bool CanHit(Projectile p, NPC target)
		{
			if (!p.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex < 0 || tag.SoldierIndex >= Main.maxNPCs) return true;
			NPC body = Main.npc[tag.SoldierIndex];
			if (!body.active) return true;
			return Collision.CanHit(body.position, body.width, body.height, target.position, target.width, target.height);
		}
	}
}
