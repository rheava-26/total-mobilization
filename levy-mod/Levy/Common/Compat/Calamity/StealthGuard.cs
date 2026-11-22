using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Calamity
{
	/// <summary>
	/// THE STEALTH GUARD: a levy's rogue work spends and fills the LEVY's stealth bar, never the commander's.
	/// <para/>Every shot a levy makes is owned by the commander's player slot (single player: kill credit, loot), so Calamity code that runs while the shot flies, hits or dies and reads or writes "the owner's" stealth
	/// (<c>Main.player[projectile.owner].Calamity()</c>: a thrown weapon that spends the bar on release or a stealth strike, one that refills it on a hit, one that sets it from a charge, StealthStrikeAvailable deciding a levy's
	/// strike) found the COMMANDER's CalamityPlayer. Her bar was emptied by the levies' throws while she stood still waiting for it, and the levies' throws were judged against her bar instead of their own.
	/// <para/>The guard sits in <see cref="Levy.Common.Owner.UseGuard"/>, around every pass of an army round owned by a real player (Projectile.Update, Kill, Damage), around a levy's AI pass and an army spawn:
	/// for a levy's modded shot and a levy's own AI pass the commander's stealth state is LENT the levy's for the length of the pass (the bar, its size, the strike-cost flags, the strike-this-frame flag, the acceleration
	/// of an Eclipse Mirror or a Dark God's Sheath, the temporary bar of a charged holdout), whatever the pass changed is the levy's (added to the levy dummy's own CalamityPlayer), and the commander's own state is put back
	/// exactly. A modded army round that is no levy's (a machine's) sees an empty bar, as a player out of rogue armor has; any other pass only puts her state back. Read by reflection: no compile-time reference to
	/// Calamity; any missing member switches the guard off.
	/// Env LEVY_STEALTHGUARD=0 (lab) switches it off (the unguarded behaviour, for the A/B in the stealth check).
	/// </summary>
	internal static class StealthGuard
	{
		public static bool Disabled = Environment.GetEnvironmentVariable("LEVY_STEALTHGUARD") == "0";
		/// <summary>Lab counters: lends opened, passes that changed the commander's bar (put back), and the stealth the passes moved to levies.</summary>
		public static long Lends, Restores, Changed; public static double MovedToLevies;

		private static bool _init, _ok; private static int _index = -1; private static Type _cpT;
		private static Func<ModPlayer, float> _gBar, _gMax, _gTemp, _gAcc; private static Action<ModPlayer, float> _sBar, _sMax, _sTemp, _sAcc;
		private static Func<ModPlayer, bool> _gThis, _gHalf, _g75, _g90; private static Action<ModPlayer, bool> _sThis, _sHalf, _s75, _s90;
		private static Func<ModPlayer, int> _gFlat; private static Action<ModPlayer, int> _sFlat;

		/// <summary>Calamity is loaded and every member the guard needs was found.</summary>
		public static bool Ready { get { Init(); return _ok; } }

		private static void Init()
		{
			if (_init) return;
			_init = true;
			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal) || cal.Code == null) return;
				_cpT = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
				if (_cpT == null) return;
				ModPlayer proto = ModContent.GetContent<ModPlayer>().FirstOrDefault(m => m.GetType() == _cpT);
				if (proto == null) return;
				_index = proto.Index;
				bool ok = Bind("rogueStealth", out _gBar, out _sBar) & Bind("rogueStealthMax", out _gMax, out _sMax) & Bind("temporaryStealthMax", out _gTemp, out _sTemp) & Bind("stealthAcceleration", out _gAcc, out _sAcc)
					& Bind("stealthStrikeThisFrame", out _gThis, out _sThis) & Bind("stealthStrikeHalfCost", out _gHalf, out _sHalf) & Bind("stealthStrike75Cost", out _g75, out _s75) & Bind("stealthStrike90Cost", out _g90, out _s90)
					& Bind("flatStealthLossReduction", out _gFlat, out _sFlat);
				_ok = ok && _index >= 0;
			}
			catch (Exception e) { _ok = false; Levy.Instance?.Logger.Warn("Levy: stealth guard off (Calamity's stealth fields not found): " + e.Message); }
		}

		private static bool Bind<T>(string name, out Func<ModPlayer, T> get, out Action<ModPlayer, T> set)
		{
			get = null; set = null;
			FieldInfo f = _cpT.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (f == null || f.FieldType != typeof(T) || f.IsInitOnly) return false;
			ParameterExpression mp = Expression.Parameter(typeof(ModPlayer)), v = Expression.Parameter(typeof(T));
			Expression field = Expression.Field(Expression.Convert(mp, _cpT), f);
			get = Expression.Lambda<Func<ModPlayer, T>>(field, mp).Compile();
			set = Expression.Lambda<Action<ModPlayer, T>>(Expression.Assign(field, v), mp, v).Compile();
			return true;
		}

		/// <summary>The player's CalamityPlayer, or null.</summary>
		internal static ModPlayer Of(Player p)
		{
			if (!Ready || p == null) return null;
			var mps = p.ModPlayers;
			if (_index >= mps.Length) return null;
			ModPlayer m = mps[_index];
			return m != null && m.GetType() == _cpT ? m : null;
		}

		internal struct State { public float Bar, Max, Temp, Acc; public int Flat; public bool This, Half, C75, C90; }

		private static State Read(ModPlayer m) => new State { Bar = _gBar(m), Max = _gMax(m), Temp = _gTemp(m), Acc = _gAcc(m), Flat = _gFlat(m), This = _gThis(m), Half = _gHalf(m), C75 = _g75(m), C90 = _g90(m) };
		private static void Write(ModPlayer m, in State s) { _sBar(m, s.Bar); _sMax(m, s.Max); _sTemp(m, s.Temp); _sAcc(m, s.Acc); _sFlat(m, s.Flat); _sThis(m, s.This); _sHalf(m, s.Half); _s75(m, s.C75); _s90(m, s.C90); }

		/// <summary>One open guard: the commander's state as the pass found it, and (for a lend) the levy's CalamityPlayer and the values lent.</summary>
		public struct Scope
		{
			internal ModPlayer Her, Levy; internal State Saved, Lent; internal bool Open, IsLend; internal SoldierNPC PrevBody;
		}

		// the levy whose state the commander's CalamityPlayer holds right now (a nested pass of the same levy leaves it as it is)
		private static ModPlayer _lentHer; private static SoldierNPC _lentBody; private static int _lendDepth;

		/// <summary>No stealth bar at all (a player out of rogue armor): what a machine's round sees as "the owner's" bar.</summary>
		private static readonly State Empty = new State { Acc = 1f };

		/// <summary>
		/// Opens the guard for a pass of army code owned by <paramref name="owner"/> (a real player). <paramref name="body"/>: the soldier whose work it is (its bar is lent to the owner for the pass). Without one,
		/// <paramref name="nobody"/> lends an empty bar (an army round that is no soldier's: a machine's, which has no stealth), otherwise the owner's state is only put back. Close it with <see cref="Close"/> in a finally.
		/// </summary>
		public static Scope Open(Player owner, SoldierNPC body, bool nobody = false)
		{
			var s = new Scope();
			if (Disabled) return s;
			ModPlayer her = Of(owner);
			if (her == null) return s;
			if (_lendDepth > 0 && ReferenceEquals(_lentHer, her))
			{
				if (body == null || ReferenceEquals(body, _lentBody)) return s; // inside a lend already: the outer pass puts her state back and hands the change on
			}
			ModPlayer levy = body?.Dummy != null && !ReferenceEquals(body.Dummy, owner) ? Of(body.Dummy) : null;
			if (levy != null && ReferenceEquals(levy, her)) levy = null;
			s.Her = her; s.Open = true;
			if (levy != null || (body == null && nobody))
			{
				s.Saved = Read(her);
				s.Levy = levy; s.IsLend = true; s.Lent = levy != null ? Read(levy) : Empty;
				Write(her, s.Lent);
				s.PrevBody = _lentBody; if (_lendDepth == 0) _lentHer = her; _lentBody = body; _lendDepth++;
				Lends++;
			}
			else s.Saved = new State { Bar = _gBar(her), Temp = _gTemp(her), Acc = _gAcc(her), This = _gThis(her) }; // only put back: the four fields a pass writes (the bar's size and the cost flags come from her equip pass)
			return s;
		}

		/// <summary>Hands what the pass did to the lent bar on to the levy and puts the owner's own stealth state back exactly.</summary>
		public static void Close(ref Scope s)
		{
			if (!s.Open) return;
			s.Open = false;
			ModPlayer her = s.Her;
			try
			{
				if (s.IsLend && s.Levy == null) { Write(her, s.Saved); Restores++; } // an empty bar lent to a machine's round: what it did is nobody's
				else if (s.IsLend)
				{
					State after = Read(her);
					State now = Read(s.Levy); // the levy's own bar may have moved during the pass (its gear frame): the pass's change is added to it
					float d = after.Bar - s.Lent.Bar;
					if (d != 0f) { now.Bar = Math.Max(0f, now.Bar + d); MovedToLevies += d; }
					if (after.This && !s.Lent.This) now.This = true;
					if (after.Acc != s.Lent.Acc) now.Acc = after.Acc;
					if (after.Temp != s.Lent.Temp) now.Temp = after.Temp;
					_sBar(s.Levy, now.Bar); _sThis(s.Levy, now.This); _sAcc(s.Levy, now.Acc); _sTemp(s.Levy, now.Temp);
					Write(her, s.Saved);
					Restores++;
				}
				else
				{
					bool moved = false;
					if (_gBar(her) != s.Saved.Bar) { _sBar(her, s.Saved.Bar); moved = true; }
					if (_gThis(her) != s.Saved.This) { _sThis(her, s.Saved.This); moved = true; }
					if (_gAcc(her) != s.Saved.Acc) { _sAcc(her, s.Saved.Acc); moved = true; }
					if (_gTemp(her) != s.Saved.Temp) { _sTemp(her, s.Saved.Temp); moved = true; }
					if (moved) Changed++;
				}
			}
			finally
			{
				if (s.IsLend) { _lendDepth--; _lentBody = s.PrevBody; if (_lendDepth <= 0) { _lendDepth = 0; _lentHer = null; _lentBody = null; } }
			}
		}

		/// <summary>A tick boundary: no pass survives one (an exception that skipped a Close).</summary>
		internal static void ResetTick() { _lendDepth = 0; _lentHer = null; _lentBody = null; }
	}
}
