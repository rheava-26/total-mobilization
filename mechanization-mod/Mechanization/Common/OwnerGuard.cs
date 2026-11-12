using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Globals;              // SoldierProjectileTag

namespace Mechanization.Common
{
	/// <summary>
	/// A round a drone or a turret fires is owned by the real player (kill credit, loot, banners), so any code that reads or writes "the owner" during that round's life lands on the player: a held-weapon
	/// shot that keeps writing <c>owner.heldProj / itemAnimation / itemTime</c>, a modded hook that kicks <c>owner.velocity</c> or flips Calamity's <c>mouseWorldListener</c>. The player's own weapon stops working
	/// for the moment that happens. This puts the player's use state, heading, speed and arms back exactly as they were after every call into a machine's round (its spawn, each tick of its life, its death),
	/// so the round keeps its full behaviour and the player is never touched. A round counts as a machine's when Levy tagged it with <see cref="SoldierProjectileTag.DroneShot"/> (its fragments inherit it).
	/// </summary>
	public class OwnerGuard : ModSystem
	{
		private struct Snap
		{
			public Player P; public bool Live;
			public int HeldProj, ItemAnimation, ItemAnimationMax, ItemTime, ItemTimeMax, ReuseDelay, Direction, SelectedItem;
			public bool Channel, UseItem, UseTile, ReleaseUse, NoItems;
			public float ItemRotation; public Vector2 ItemLocation, Velocity;
			public Player.CompositeArmData Front, Back;
			public bool[] Listeners; // the mouseWorldListener of each mod player that has one (see ListenerSet)
			public object Set;
		}

		private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		/// <summary>One mod player that has a <c>mouseWorldListener</c> flag, with its compiled reader and writer (no reflection, no boxing).</summary>
		private sealed class ListenerSlot
		{
			public ModPlayer Owner; public int Index; public Func<ModPlayer, bool> Get; public Action<ModPlayer, bool> Set;
		}

		/// <summary>The few mod players of one player that carry the flag, found once; rebuilt when the player's mod player list changes.</summary>
		private sealed class ListenerSet
		{
			public int Count; public ListenerSlot[] Slots = Array.Empty<ListenerSlot>();
		}

		private static readonly ConditionalWeakTable<Player, ListenerSet> _sets = new();
		private static readonly Dictionary<Type, (Func<ModPlayer, bool> get, Action<ModPlayer, bool> set)> _accessors = new();

		private static (Func<ModPlayer, bool>, Action<ModPlayer, bool>) AccessorsFor(Type t)
		{
			if (_accessors.TryGetValue(t, out var a)) return a;
			FieldInfo f = t.GetField("mouseWorldListener", Any);
			if (f != null && f.FieldType == typeof(bool))
			{
				ParameterExpression mp = Expression.Parameter(typeof(ModPlayer)), v = Expression.Parameter(typeof(bool));
				Expression field = Expression.Field(Expression.Convert(mp, t), f);
				a = (Expression.Lambda<Func<ModPlayer, bool>>(field, mp).Compile(), f.IsInitOnly ? null : Expression.Lambda<Action<ModPlayer, bool>>(Expression.Assign(field, v), mp, v).Compile());
				if (a.get != null && a.Item2 == null) a = (null, null);
			}
			_accessors[t] = a;
			return a;
		}

		private static ListenerSet ListenersOf(Player p)
		{
			var mps = p.ModPlayers;
			int n = mps.Length;
			if (_sets.TryGetValue(p, out ListenerSet set))
			{
				bool fresh = set.Count == n;
				for (int i = 0; fresh && i < set.Slots.Length; i++) fresh = ReferenceEquals(mps[set.Slots[i].Index], set.Slots[i].Owner);
				if (fresh) return set;
			}
			var slots = new List<ListenerSlot>();
			for (int i = 0; i < n; i++)
			{
				ModPlayer mp = mps[i];
				var (get, setter) = AccessorsFor(mp.GetType());
				if (get != null) slots.Add(new ListenerSlot { Owner = mp, Index = i, Get = get, Set = setter });
			}
			set = new ListenerSet { Count = n, Slots = slots.ToArray() };
			_sets.Remove(p); _sets.Add(p, set);
			return set;
		}

		private static Snap Take(int owner)
		{
			var s = new Snap();
			if (owner < 0 || owner >= Main.maxPlayers) return s;
			Player p = Main.player[owner];
			if (p == null) return s; // no "active" test: the guard must hold whatever the slot is doing
			s.P = p; s.Live = true;
			s.HeldProj = p.heldProj; s.ItemAnimation = p.itemAnimation; s.ItemAnimationMax = p.itemAnimationMax; s.ItemTime = p.itemTime; s.ItemTimeMax = p.itemTimeMax; s.ReuseDelay = p.reuseDelay;
			s.Direction = p.direction; s.SelectedItem = p.selectedItem; s.Channel = p.channel; s.UseItem = p.controlUseItem; s.UseTile = p.controlUseTile; s.ReleaseUse = p.releaseUseItem; s.NoItems = p.noItems;
			s.ItemRotation = p.itemRotation; s.ItemLocation = p.itemLocation; s.Velocity = p.velocity; s.Front = p.compositeFrontArm; s.Back = p.compositeBackArm;
			ListenerSet set = ListenersOf(p); s.Set = set;
			if (set.Slots.Length > 0)
			{
				var flags = new bool[set.Slots.Length];
				for (int i = 0; i < flags.Length; i++) flags[i] = set.Slots[i].Get(set.Slots[i].Owner);
				s.Listeners = flags;
			}
			return s;
		}

		private static void Put(in Snap s)
		{
			if (!s.Live) return;
			Player p = s.P;
			p.heldProj = s.HeldProj; p.itemAnimation = s.ItemAnimation; p.itemAnimationMax = s.ItemAnimationMax; p.itemTime = s.ItemTime; p.itemTimeMax = s.ItemTimeMax; p.reuseDelay = s.ReuseDelay;
			p.direction = s.Direction; p.selectedItem = s.SelectedItem; p.channel = s.Channel; p.controlUseItem = s.UseItem; p.controlUseTile = s.UseTile; p.releaseUseItem = s.ReleaseUse; p.noItems = s.NoItems;
			p.itemRotation = s.ItemRotation; p.itemLocation = s.ItemLocation; p.velocity = s.Velocity; p.compositeFrontArm = s.Front; p.compositeBackArm = s.Back;
			if (s.Listeners == null) return;
			ListenerSlot[] slots = ((ListenerSet)s.Set).Slots;
			for (int i = 0; i < slots.Length && i < s.Listeners.Length; i++) slots[i].Set(slots[i].Owner, s.Listeners[i]);
		}

		/// <summary>Runs <paramref name="body"/> (a machine spawning its rounds) and puts the owner's use state back afterwards.</summary>
		public static void Hold(int owner, Action body)
		{
			Snap s = TakeT(owner);
			try { body(); } finally { PutT(s); }
		}

		// Measurement (the world check turns it on): time spent in Take + Put, in Stopwatch ticks, and how many snapshots that was.
		internal static bool Measure; internal static long GuardTicks; internal static int GuardSnaps;
		private static Snap TakeT(int owner)
		{
			if (!Measure) return Take(owner);
			long t = System.Diagnostics.Stopwatch.GetTimestamp(); Snap s = Take(owner); GuardTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t; GuardSnaps++; return s;
		}
		private static void PutT(in Snap s)
		{
			if (!Measure) { Put(s); return; }
			long t = System.Diagnostics.Stopwatch.GetTimestamp(); Put(s); GuardTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t;
		}

		/// <summary>Cost of one snapshot + restore of <paramref name="owner"/>'s use state, in microseconds, over <paramref name="n"/> round trips (the check reports it).</summary>
		internal static double BenchMicros(int owner, int n)
		{
			for (int i = 0; i < 200; i++) Put(Take(owner)); // warm the JIT and the caches
			long t = System.Diagnostics.Stopwatch.GetTimestamp();
			for (int i = 0; i < n; i++) Put(Take(owner));
			return (System.Diagnostics.Stopwatch.GetTimestamp() - t) * 1e6 / System.Diagnostics.Stopwatch.Frequency / n;
		}

		/// <summary>How many round updates the guard has wrapped (the check reads it).</summary>
		internal static int GuardedUpdates, Undone;

		private static bool IsMachineRound(Projectile p) =>
			p.active && p.owner >= 0 && p.owner < Main.maxPlayers && p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.DroneShot;

		public override void Load()
		{
			On_Projectile.Update += UpdateDetour;
			On_Projectile.Kill += KillDetour;
		}

		public override void Unload()
		{
			On_Projectile.Update -= UpdateDetour;
			On_Projectile.Kill -= KillDetour;
		}

		private static void UpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			if (!IsMachineRound(self)) { orig(self, i); return; }
			Snap s = TakeT(self.owner); GuardedUpdates++;
			try { orig(self, i); } finally { if (s.Live && (s.P.heldProj != s.HeldProj || s.P.itemTime != s.ItemTime || s.P.itemAnimation != s.ItemAnimation)) Undone++; PutT(s); }
		}

		private static void KillDetour(On_Projectile.orig_Kill orig, Projectile self)
		{
			if (!IsMachineRound(self)) { orig(self); return; }
			Snap s = TakeT(self.owner);
			try { orig(self); } finally { PutT(s); }
		}
	}
}
