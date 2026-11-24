using System;
using System.Linq.Expressions;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Compat.Calamity
{
	/// <summary>
	/// Calamity's Chalice of the Blood God, read by reflection (Levy has no compile-time reference to CalamityMod; a missing member turns this off).
	/// <para/>What the Chalice does to a PLAYER (CalamityPlayer.ModifyHurtInfo_Calamity, OnHurt, ChaliceOfTheBloodGod.HandleBleedout): any hit above 5 damage is cut to 5, and the rest goes into a bleedout buffer that
	/// <c>HandleBleedout</c> then takes from the player's life a little every tick (0.83 percent of what is left each tick, about 40 percent a second; the last few points at once). The player is hurt for the full amount, a few seconds late.
	/// <para/>What it did to a LEVY: the hit sequence ran the same cut (the body took 5) and the buffer filled on the gear dummy (the "(-435)" text), but the only place the drain ran was the regeneration pass
	/// (<c>Player.UpdateLifeRegen</c> on the dummy, SoldierNPC.RegenPass), which throws away any life the dummy loses ("damage over time is not this pass's"). The buffer drained into nothing:
	/// a levy in the Chalice took 5 from a hit that would have cost a player 400 or more.
	/// <para/><see cref="Step"/> runs Calamity's own <c>HandleBleedout</c> on the dummy once a tick, with room to lose life without dying, and returns what the dummy lost so the body can take it.
	/// <see cref="Park"/> / <see cref="Unpark"/> keep the regeneration pass from draining the buffer a second time.
	/// </summary>
	public static class ChaliceBleed
	{
		private static bool _init, _ok;
		private static ModPlayer _base;
		private static Func<ModPlayer, double> _getBuf, _getPartial;
		private static Action<ModPlayer, double> _setBuf, _setPartial;
		private static Action<Player> _handle;
		/// <summary>Harness telemetry: ticks the drain ran, life it took from bodies.</summary>
		public static long Steps, Taken;

		private static void Init()
		{
			if (_init) return; _init = true;
			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
				Type cp = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
				Type chalice = cal.Code.GetType("CalamityMod.Items.Accessories.ChaliceOfTheBloodGod");
				if (cp == null || chalice == null) return;
				MethodInfo hb = chalice.GetMethod("HandleBleedout", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Player) }, null);
				if (hb == null || !cal.TryFind("CalamityPlayer", out ModPlayer b) || b == null) return;
				_base = b;
				_handle = (Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>), hb);
				var p = Expression.Parameter(typeof(ModPlayer), "p"); var v = Expression.Parameter(typeof(double), "v");
				Expression cast = Expression.Convert(p, cp);
				FieldInfo fb = cp.GetField("chaliceBleedoutBuffer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				FieldInfo fp = cp.GetField("chaliceDamagePointPartialProgress", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (fb == null || fp == null) return;
				_getBuf = Expression.Lambda<Func<ModPlayer, double>>(Expression.Field(cast, fb), p).Compile();
				_getPartial = Expression.Lambda<Func<ModPlayer, double>>(Expression.Field(cast, fp), p).Compile();
				_setBuf = Expression.Lambda<Action<ModPlayer, double>>(Expression.Assign(Expression.Field(cast, fb), v), p, v).Compile();
				_setPartial = Expression.Lambda<Action<ModPlayer, double>>(Expression.Assign(Expression.Field(cast, fp), v), p, v).Compile();
				_ok = true;
			}
			catch (Exception) { _ok = false; }
		}

		/// <summary>True when Calamity is loaded and the Chalice's members were found.</summary>
		public static bool Available { get { Init(); return _ok; } }

		/// <summary>The bleedout buffer the Chalice holds on <paramref name="d"/> (0 without Calamity).</summary>
		public static double Buffer(Player d)
		{
			Init();
			if (!_ok || d == null) return 0.0;
			try { return _getBuf(d.GetModPlayer(_base)); } catch (Exception) { return 0.0; }
		}

		/// <summary>Empties the buffer (a body that is rebuilt starts clean).</summary>
		public static void Clear(Player d)
		{
			Init();
			if (!_ok || d == null) return;
			try { ModPlayer m = d.GetModPlayer(_base); _setBuf(m, 0.0); _setPartial(m, 0.0); } catch (Exception) { }
		}

		/// <summary>Takes the buffer off the dummy for the length of a pass that must not drain it; hand the result to <see cref="Unpark"/>.</summary>
		public static (double buf, double part) Park(Player d)
		{
			Init();
			if (!_ok || d == null) return default;
			try
			{
				ModPlayer m = d.GetModPlayer(_base); double b = _getBuf(m), pt = _getPartial(m);
				if (b != 0.0 || pt != 0.0) { _setBuf(m, 0.0); _setPartial(m, 0.0); }
				return (b, pt);
			}
			catch (Exception) { return default; }
		}

		public static void Unpark(Player d, (double buf, double part) saved)
		{
			if (!_ok || d == null || (saved.buf == 0.0 && saved.part == 0.0)) return;
			try { ModPlayer m = d.GetModPlayer(_base); _setBuf(m, saved.buf); _setPartial(m, saved.part); } catch (Exception) { }
		}

		/// <summary>
		/// One tick of the drain. <paramref name="run"/> runs the given action the way the body runs a player pass on its dummy (SoldierNPC.InFrame). Returns the life the Chalice took this tick (0 when the buffer is empty
		/// or Calamity is not loaded). The dummy is given more life than the buffer so Calamity's own death path (KillMe) can never run; the body takes the loss and may die of it, as a player does.
		/// </summary>
		public static int Step(Player d, Action<Action> run)
		{
			Init();
			if (!_ok || d == null) return 0;
			try
			{
				ModPlayer m = d.GetModPlayer(_base);
				double buf = _getBuf(m);
				if (buf <= 0.0) return 0;
				int keep = d.statLife, room = (int)Math.Min(1_000_000_000.0, buf) + 1000;
				d.statLife = room;
				int lost;
				try { run(() => _handle(d)); lost = room - d.statLife; }
				finally { d.statLife = keep; }
				Steps++;
				if (lost > 0) Taken += lost;
				return Math.Max(0, lost);
			}
			catch (Exception) { return 0; }
		}
	}
}
