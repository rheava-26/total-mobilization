using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// THE PLAYER USE CYCLE AROUND A LEVY'S SHOT. A player's shot happens inside <c>Player.ItemCheck</c>: the use starts (<c>ApplyItemAnimation</c>: itemAnimation = itemAnimationMax = the animation,
	/// reuseDelay = the item's, ItemUsesThisAnimation = 0), then on every tick where itemTime is 0 the owner code calls <c>ItemCheck_Shoot</c> (CanShoot, ModifyShootStats, Shoot) and only after the shot
	/// <c>ApplyItemTime</c> sets itemTime to the use time and counts the use. So a weapon's own Shoot reads: itemAnimation = the animation on the first shot, then animation - useTime, - 2 useTime...;
	/// itemTime = 0; ItemUsesThisAnimation = the shots already made this animation. Calamity's Nucleosynthesis picks its beam from exactly that (16, 12, 8, 4: Solar, Nebula, Vortex, Stardust, else no shot),
	/// Cosmic Kunai ends its stealth salvo when ItemUsesThisAnimation reaches useLimitPerAnimation.
	/// <para/>The levy's own clock is <see cref="SoldierNPC.ItemAnimation"/> / <see cref="SoldierNPC.UsesThisAnimation"/> (started by <see cref="WeaponUse.FinishUse"/> after the first shot, counted down by
	/// SoldierNPC.Combat before the next attack, so the k-th shot of an animation is fired at animation - k * useTime, as for a player). <see cref="Enter"/> puts the dummy in the state that shot is
	/// fired in, for the length of the hook calls, and puts the dummy's own values back afterwards.
	/// </summary>
	public static class UseCycle
	{
		/// <summary>The animation a new use of <paramref name="item"/> runs for a levy (what <see cref="WeaponUse.FinishUse"/> starts): the item's useAnimation, raised to the melee swing floor where that applies.</summary>
		public static int AnimationFor(Item item) => item == null ? 1 : Math.Max(Math.Max(1, item.useAnimation), WeaponUse.SwingFloor(item));

		/// <summary>Whether this shot starts a new use (no animation running), as opposed to the 2nd..nth shot of a running one.</summary>
		public static bool StartsUse(SoldierNPC s) => s == null || s.ItemAnimation <= 0;

		/// <summary>Whether a shot now continues an animation the levy already fired in (the burst's 2nd..nth shot), which a player does without a new use start: no mana is paid and no use check is made.</summary>
		public static bool MidBurst(SoldierNPC s, Item item) => s != null && item != null && s.ItemAnimation > 0 && s.UsesThisAnimation > 0 && ReferenceEquals(WeaponUse.HeldItem(s), item);

		private static Action<Player, int> _setUses;
		private static bool _setUsesResolved;

		/// <summary><c>Player.ItemUsesThisAnimation</c> has a private setter; this writes it (no-op when the property is missing from this tModLoader).</summary>
		public static void SetUsesThisAnimation(Player p, int value)
		{
			if (!_setUsesResolved)
			{
				_setUsesResolved = true;
				try
				{
					MethodInfo set = typeof(Player).GetProperty("ItemUsesThisAnimation", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetSetMethod(true);
					if (set != null) _setUses = (Action<Player, int>)Delegate.CreateDelegate(typeof(Action<Player, int>), set);
				}
				catch (Exception) { _setUses = null; }
			}
			_setUses?.Invoke(p, value);
		}

		/// <summary>The dummy in the use state of the levy's current shot of <paramref name="item"/>; dispose to put the dummy's own state back.</summary>
		public static Scope Enter(SoldierNPC s, Player d, Item item) => new Scope(s, d, item, false);

		/// <summary>The dummy in the use state of the tick the levy's running animation is on now (the per-tick swing hooks): itemAnimation = the levy's, itemTime = what is left of the use time.</summary>
		public static Scope EnterTick(SoldierNPC s, Player d, Item item) => new Scope(s, d, item, true);

		/// <summary>
		/// <c>ItemLoader.UseAnimation</c>, which Player.ApplyItemAnimation runs as every use starts (before the animation is set and before the shot): weapons reset their own per-swing state there
		/// (Greatsword of Judgement's swing clock and beam flag). The caller has the dummy standing in as the player. Never throws.
		/// </summary>
		public static void StartUseHook(Player d, Item item)
		{
			if (d == null || item == null || item.IsAir) return;
			try { ItemLoader.UseAnimation(item, d); } catch (Exception) { }
		}

		public readonly struct Scope : IDisposable
		{
			private readonly Player _d;
			private readonly int _anim, _animMax, _time, _timeMax, _reuse, _uses;

			public Scope(SoldierNPC s, Player d, Item item, bool currentTick)
			{
				_d = d;
				if (d == null || item == null) { _anim = _animMax = _time = _timeMax = _reuse = _uses = 0; return; }
				_anim = d.itemAnimation; _animMax = d.itemAnimationMax; _time = d.itemTime; _timeMax = d.itemTimeMax; _reuse = d.reuseDelay; _uses = d.ItemUsesThisAnimation;
				bool fresh = !currentTick && StartsUse(s);
				int anim = fresh ? AnimationFor(item) : Math.Max(0, s.ItemAnimation);
				d.itemAnimation = anim;
				d.itemAnimationMax = fresh ? anim : Math.Max(anim, s.ItemAnimationMax);
				d.itemTime = currentTick ? Math.Max(0, s.AttackCooldown) : 0; // a shot is made on the tick itemTime reached 0 (ApplyItemTime sets it after); between shots it counts down
				d.itemTimeMax = Math.Max(1, item.useTime);
				d.reuseDelay = Math.Max(0, item.reuseDelay); // ApplyItemAnimation sets it at the use start; it only counts down once the animation is over
				SetUsesThisAnimation(d, fresh ? 0 : Math.Max(0, s.UsesThisAnimation));
			}

			public void Dispose()
			{
				if (_d == null) return;
				_d.itemAnimation = _anim; _d.itemAnimationMax = _animMax; _d.itemTime = _time; _d.itemTimeMax = _timeMax; _d.reuseDelay = _reuse;
				SetUsesThisAnimation(_d, _uses);
			}
		}
	}
}
