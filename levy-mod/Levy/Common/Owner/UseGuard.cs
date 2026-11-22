using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Calamity;
using Levy.Common.Globals;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Owner
{
	/// <summary>
	/// THE USE GUARD: the army never touches the real player's item use.
	/// <para/>Shots an army unit fires are owned by the real commander in single player (kill credit, loot, banners; <see cref="WeaponUse.OwnerFor"/>), so any code that reads or writes "the owner" while such a shot
	/// is spawned, flies, hits or dies lands on the commander: a held-weapon shot that keeps writing <c>owner.heldProj / itemAnimation / itemTime / channel</c>, a modded hook that kicks <c>owner.velocity</c>, a
	/// listener that flips Calamity's <c>mouseWorldListener</c>, a set-bonus hook that raises <c>noItems</c>. The commander is then stuck in the use pose with the weapon doing nothing, until the shot dies. Patching each
	/// weapon that does it as it turns up is a treadmill; this is the one rule instead: <b>whatever the army runs, the real player's use state, heading, speed and arms are put back exactly as they were</b>.
	/// <para/>Where it stands (every path that runs weapon or projectile code for an army unit):
	/// <list type="bullet">
	/// <item>every Projectile.Update, Kill and Damage of an army projectile (a soldier's shot, a minion's child, a drone or turret round) whose owner is a real player: the spawn's own hooks of a shot are inside
	/// <see cref="Hold"/> / the NPC pass below, its AI, movement, hit pass and death are inside the three projectile detours (HeldWeapons.RunAi / RunDamage, MiscProjSwap, SoldierMinions run inside them);</item>
	/// <item>the whole AI pass of a soldier or of a companion-mod machine (NPCLoader.AI): item use, real Shoot hooks (HeldWeapons.ShootHooks), spawn hooks, gear frames, owner frames;</item>
	/// <item>Projectile.NewProjectile whose source is an army body (spawns made outside its AI pass);</item>
	/// <item>drone and turret rounds: Mechanization's <c>OwnerGuard.Hold</c> is this guard's <see cref="Hold"/>.</item></list>
	/// A round counts as the army's when Levy tagged it (<see cref="SoldierProjectileTag"/>: a soldier's shot, a unit's, a driven minion child, a machine round; fragments inherit it). The real player's own weapon, its
	/// own projectiles and its own update are never inside a scope. Env LEVY_USEGUARD=0 switches the guard off (the unguarded behaviour, for the A/B in the player-safety sweep).
	/// </summary>
	public sealed class UseGuard : ModSystem
	{
		/// <summary>Lab A/B switch.</summary>
		public static bool Disabled = Environment.GetEnvironmentVariable("LEVY_USEGUARD") == "0";

		public struct Snap
		{
			public Player P; public bool Live;
			public int HeldProj, ItemAnimation, ItemAnimationMax, ItemTime, ItemTimeMax, ReuseDelay, Direction, SelectedItem, NoThrow, Frozen, Stoned, Webbed, TalkNpc;
			public bool Channel, UseItem, UseTile, ReleaseUse, NoItems, CCed;
			public float ItemRotation; public Vector2 ItemLocation, Velocity;
			public Player.CompositeArmData Front, Back;
			public bool[] Listeners; // the mouseWorldListener of each mod player that has one (see ListenerSet)
			public object Set;
		}

		private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		/// <summary>One mod player that has a <c>mouseWorldListener</c> flag, with its compiled reader and writer (no reflection, no boxing).</summary>
		private sealed class ListenerSlot { public ModPlayer Owner; public int Index; public Func<ModPlayer, bool> Get; public Action<ModPlayer, bool> Set; }
		/// <summary>The few mod players of one player that carry the flag, found once; rebuilt when the player's mod player list changes.</summary>
		private sealed class ListenerSet { public int Count; public ListenerSlot[] Slots = Array.Empty<ListenerSlot>(); }

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

		/// <summary>Writes down the use state of the player in slot <paramref name="owner"/> (a snapshot with Live = false when there is nothing to guard).</summary>
		public static Snap Take(int owner)
		{
			var s = new Snap();
			if (owner < 0 || owner >= Main.maxPlayers) return s;
			Player p = Main.player[owner];
			if (p == null) return s; // no "active" test: the guard must hold whatever the slot is doing
			s.P = p; s.Live = true;
			s.HeldProj = p.heldProj; s.ItemAnimation = p.itemAnimation; s.ItemAnimationMax = p.itemAnimationMax; s.ItemTime = p.itemTime; s.ItemTimeMax = p.itemTimeMax; s.ReuseDelay = p.reuseDelay;
			s.Direction = p.direction; s.SelectedItem = p.selectedItem; s.Channel = p.channel; s.UseItem = p.controlUseItem; s.UseTile = p.controlUseTile; s.ReleaseUse = p.releaseUseItem; s.NoItems = p.noItems;
			s.ItemRotation = p.itemRotation; s.ItemLocation = p.itemLocation; s.Velocity = p.velocity; s.Front = p.compositeFrontArm; s.Back = p.compositeBackArm;
			s.CCed = p.CCed; s.NoThrow = p.noThrow; s.Frozen = p.frozen ? 1 : 0; s.Stoned = p.stoned ? 1 : 0; s.Webbed = p.webbed ? 1 : 0; s.TalkNpc = p.talkNPC;
			ListenerSet set = ListenersOf(p); s.Set = set;
			if (set.Slots.Length > 0)
			{
				var flags = new bool[set.Slots.Length];
				for (int i = 0; i < flags.Length; i++) flags[i] = set.Slots[i].Get(set.Slots[i].Owner);
				s.Listeners = flags;
			}
			return s;
		}

		/// <summary>Counts how many fields a pass moved (for the lab counters; cheap compare of the use fields).</summary>
		private static bool Moved(in Snap s)
		{
			Player p = s.P;
			return p.heldProj != s.HeldProj || p.itemAnimation != s.ItemAnimation || p.itemAnimationMax != s.ItemAnimationMax || p.itemTime != s.ItemTime || p.itemTimeMax != s.ItemTimeMax || p.reuseDelay != s.ReuseDelay
				|| p.direction != s.Direction || p.selectedItem != s.SelectedItem || p.channel != s.Channel || p.controlUseItem != s.UseItem || p.controlUseTile != s.UseTile || p.releaseUseItem != s.ReleaseUse
				|| p.noItems != s.NoItems || p.itemRotation != s.ItemRotation || p.velocity != s.Velocity || p.noThrow != s.NoThrow || p.frozen != (s.Frozen != 0) || p.stoned != (s.Stoned != 0) || p.webbed != (s.Webbed != 0);
		}

		/// <summary>Puts the use state back exactly as <see cref="Take"/> saw it.</summary>
		public static void Put(in Snap s)
		{
			if (!s.Live) return;
			Player p = s.P;
			p.heldProj = s.HeldProj; p.itemAnimation = s.ItemAnimation; p.itemAnimationMax = s.ItemAnimationMax; p.itemTime = s.ItemTime; p.itemTimeMax = s.ItemTimeMax; p.reuseDelay = s.ReuseDelay;
			p.direction = s.Direction; p.selectedItem = s.SelectedItem; p.channel = s.Channel; p.controlUseItem = s.UseItem; p.controlUseTile = s.UseTile; p.releaseUseItem = s.ReleaseUse; p.noItems = s.NoItems;
			p.itemRotation = s.ItemRotation; p.itemLocation = s.ItemLocation; p.velocity = s.Velocity; p.compositeFrontArm = s.Front; p.compositeBackArm = s.Back;
			p.noThrow = s.NoThrow; p.frozen = s.Frozen != 0; p.stoned = s.Stoned != 0; p.webbed = s.Webbed != 0;
			if (p.talkNPC != s.TalkNpc) p.SetTalkNPC(s.TalkNpc, true);
			if (s.Listeners == null) return;
			ListenerSlot[] slots = ((ListenerSet)s.Set).Slots;
			for (int i = 0; i < slots.Length && i < s.Listeners.Length; i++) slots[i].Set(slots[i].Owner, s.Listeners[i]);
		}

		// ------------------------------------------------------------------------------------------ scopes
		/// <summary>Counters for the lab and the self-checks.</summary>
		public static int GuardedUpdates, Undone; public static long Scopes;
		/// <summary>Measurement (the world check turns it on): time spent in Take + Put, in Stopwatch ticks, and how many snapshots that was.</summary>
		public static bool Measure; public static long GuardTicks; public static int GuardSnaps;
		/// <summary>Where the guard found a pass that had moved the player (kept only while <see cref="Measure"/>): the pass kind and projectile type -> count.</summary>
		public static readonly Dictionary<string, int> Moves = new();

		private static Snap TakeT(int owner)
		{
			if (!Measure) return Take(owner);
			long t = System.Diagnostics.Stopwatch.GetTimestamp(); Snap s = Take(owner); GuardTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t; GuardSnaps++; return s;
		}

		private static void PutT(in Snap s, string kind, int type)
		{
			if (!Measure) { Put(s); return; }
			if (s.Live && Moved(s)) { Undone++; string k = kind + ":" + type; Moves[k] = Moves.TryGetValue(k, out int n) ? n + 1 : 1; }
			long t = System.Diagnostics.Stopwatch.GetTimestamp(); Put(s); GuardTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t;
		}

		/// <summary>Runs <paramref name="body"/> (army code that spawns or runs a round) and puts the owner's use state back afterwards.</summary>
		public static void Hold(int owner, Action body)
		{
			if (Disabled) { body(); return; }
			Snap s = TakeT(owner); Scopes++;
			var st = StealthOpen(owner, null);
			try { body(); } finally { StealthGuard.Close(ref st); PutT(s, "hold", 0); }
		}

		/// <summary>Cost of one snapshot + restore of <paramref name="owner"/>'s use state, in microseconds, over <paramref name="n"/> round trips.</summary>
		public static double BenchMicros(int owner, int n)
		{
			for (int i = 0; i < 200; i++) Put(Take(owner)); // warm the JIT and the caches
			long t = System.Diagnostics.Stopwatch.GetTimestamp();
			for (int i = 0; i < n; i++) Put(Take(owner));
			return (System.Diagnostics.Stopwatch.GetTimestamp() - t) * 1e6 / System.Diagnostics.Stopwatch.Frequency / n;
		}

		/// <summary>True when slot <paramref name="owner"/> holds a real player: not a Levy dummy, not past the player slots.</summary>
		private static bool RealOwner(int owner)
		{
			if (owner < 0 || owner >= Main.maxPlayers) return false;
			Player p = Main.player[owner];
			return p != null && !SoldierOwner.IsDummy(p) && !OwnerSlots.IsPooled(owner);
		}

		/// <summary>An army projectile: tagged by Levy as a soldier's or a unit's shot, a driven minion's child or a machine's round, and owned by a real player.</summary>
		public static bool IsArmyRound(Projectile p) =>
			p.active && RealOwner(p.owner) && p.TryGetGlobalProjectile(out SoldierProjectileTag t) && (t.IsSoldierShot || t.DroneShot || t.CommanderIndex >= 0 || t.Cast || t.DrivenChild);

		public override void Load()
		{
			On_Projectile.Update += UpdateDetour;
			On_Projectile.Kill += KillDetour;
			On_Projectile.Damage += DamageDetour;
			MonoModHooks.Add(typeof(NPCLoader).GetMethod("AI", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(NPC) }, null), (Action<Action<NPC>, NPC>)NpcAiDetour);
			On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float += NewProjectileDetour;
		}

		public override void Unload()
		{
			On_Projectile.Update -= UpdateDetour;
			On_Projectile.Kill -= KillDetour;
			On_Projectile.Damage -= DamageDetour;
			On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float -= NewProjectileDetour;
		}

		// how deep the soldier pass is inside the one owner: a nested scope on the same owner would only repeat what the outer one restores
		private static int _aiOwner = -1, _aiDepth;

		private static void UpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{ _hl_UpdateDetour_5++;
			if (Disabled || !IsArmyRound(self)) { orig(self, i); return; }
			long g0 = global::Levy.Common.Weapons.SwapProf.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
			Snap s = TakeT(self.owner); GuardedUpdates++;
			int type = self.type;
			long g1 = global::Levy.Common.Weapons.SwapProf.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
			var st = StealthOpen(self.owner, ShotBody(self), self.ModProjectile != null);
			try { orig(self, i); }
			finally
			{
				long g2 = global::Levy.Common.Weapons.SwapProf.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
				StealthGuard.Close(ref st);
				PutT(s, "update", type);
				if (global::Levy.Common.Weapons.SwapProf.On) { global::Levy.Common.Weapons.SwapProf.GuardSwap += (g1 - g0) + (System.Diagnostics.Stopwatch.GetTimestamp() - g2); global::Levy.Common.Weapons.SwapProf.GuardN++; }
			}
		} private static int _hl_UpdateDetour_5;

		/// <summary>
		/// Projectile types whose vanilla Kill reads the tile at the projectile's own position without a range check: the Ice Rod's block (<c>Main.tile[(int)position.X / 16, (int)position.Y / 16]</c>, looking for an ice tile
		/// to take away). Update drops a projectile that has LEFT the world without calling Kill, but a block that crosses the top edge in the same step its time runs out dies with a position above the world
		/// (-19.8 px: tile row -1) and the read throws IndexOutOfRange. A levy shoots upward at a boss that flies high, so it happens to the army far more than to a player.
		/// </summary>
		private static bool ProbesTileAtPosition(int type) => type == ProjectileID.IceBlock;

		/// <summary>
		/// Puts a dying projectile's position back inside the world's tile grid (nearest tile, nothing else changes) when its type reads the tile under it. The world's outer border holds no tiles, so the read finds an
		/// empty tile exactly as it finds one anywhere else the block dies without an ice tile under it, and the rest of Kill (effects, other mods' hooks, clearing the projectile) runs as usual.
		/// Returns true when the position was moved.
		/// </summary>
		internal static bool KeepProbeInWorld(Projectile p)
		{
			if (!ProbesTileAtPosition(p.type)) return false;
			float maxX = Main.maxTilesX * 16f - 1f, maxY = Main.maxTilesY * 16f - 1f;
			Vector2 at = p.position, to = new Vector2(Math.Clamp(at.X, 0f, maxX), Math.Clamp(at.Y, 0f, maxY));
			if (to == at) return false;
			p.position = to;
			Clamped++;
			return true;
		}
		/// <summary>How many dying projectiles were moved back inside the world (for the world check).</summary>
		public static int Clamped;

		private static void KillDetour(On_Projectile.orig_Kill orig, Projectile self)
		{ _hl_KillDetour_2++;
			if (ProbesTileAtPosition(self.type) && IsArmyRound(self)) KeepProbeInWorld(self);
			if (Disabled || !IsArmyRound(self)) { orig(self); return; }
			Snap s = TakeT(self.owner); int type = self.type;
			var st = StealthOpen(self.owner, ShotBody(self), self.ModProjectile != null);
			try { orig(self); } finally { StealthGuard.Close(ref st); PutT(s, "kill", type); }
		} private static int _hl_KillDetour_2;

		private static void DamageDetour(On_Projectile.orig_Damage orig, Projectile self)
		{ _hl_DamageDetour_1++;
			if (Disabled || !IsArmyRound(self)) { orig(self); return; }
			Snap s = TakeT(self.owner); int type = self.type;
			var st = StealthOpen(self.owner, ShotBody(self), self.ModProjectile != null);
			try { orig(self); } finally { StealthGuard.Close(ref st); PutT(s, "damage", type); }
		} private static int _hl_DamageDetour_1;

		/// <summary>A body of the army: a levy or officer (SoldierNPC) or a machine of the companion mod (a drone, a turret).</summary>
		private static bool IsArmyNpc(NPC n) => n.ModNPC is SoldierNPC || n.ModNPC?.Mod.Name == "Mechanization";

		private static void NpcAiDetour(Action<NPC> orig, NPC self)
		{ _hl_NpcAiDetour_4++;
			if (Disabled || !IsArmyNpc(self)) { orig(self); return; }
			int owner = SoldierMinions.TrueMyPlayer; // what WeaponUse.OwnerFor answers for every body: the real local player
			if (!RealOwner(owner) || (_aiDepth > 0 && _aiOwner == owner)) { orig(self); return; }
			Snap s = TakeT(owner); Scopes++;
			int prevOwner = _aiOwner; _aiOwner = owner; _aiDepth++;
			var st = StealthOpen(owner, self.ModNPC as SoldierNPC); // a levy's pass: its spawn hooks and whatever reads "the owner's" bar read its own (its gear frame works on its dummy directly); a machine's: her bar is put back
			try { orig(self); } finally { _aiDepth--; _aiOwner = prevOwner; StealthGuard.Close(ref st); PutT(s, "soldierAI", 0); }
		} private static int _hl_NpcAiDetour_4;

		private static int NewProjectileDetour(On_Projectile.orig_NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float orig, IEntitySource source, float x, float y, float sx, float sy, int type, int damage, float kb, int owner, float ai0, float ai1, float ai2)
		{ _hl_NewProjectileDetour_3++;
			if (Disabled || !RealOwner(owner) || (_aiDepth > 0 && _aiOwner == owner) || source is not EntitySource_Parent { Entity: NPC army } || !IsArmyNpc(army))
				return orig(source, x, y, sx, sy, type, damage, kb, owner, ai0, ai1, ai2);
			Snap s = TakeT(owner); Scopes++;
			var st = StealthOpen(owner, army.ModNPC as SoldierNPC);
			try { return orig(source, x, y, sx, sy, type, damage, kb, owner, ai0, ai1, ai2); } finally { StealthGuard.Close(ref st); PutT(s, "spawn", type); }
		} private static int _hl_NewProjectileDetour_3;

		public override void PreUpdateNPCs() { _aiDepth = 0; _aiOwner = -1; StealthGuard.ResetTick(); }

		/// <summary>The stealth side of a guarded pass (<see cref="StealthGuard"/>): a soldier's shot lends the soldier's stealth bar to the owner for the pass, anything else only puts the owner's back.</summary>
		private static StealthGuard.Scope StealthOpen(int owner, SoldierNPC body, bool nobody = false) => owner >= 0 && owner < Main.maxPlayers ? StealthGuard.Open(Main.player[owner], body, nobody) : default;

		/// <summary>The soldier whose modded shot this is (a levy's or an officer's: a mod's projectile code is where "the owner's" stealth is read and spent), or null (a vanilla round, a round that is not a soldier's: the owner's bar is only put back).</summary>
		private static SoldierNPC ShotBody(Projectile p) => p.ModProjectile != null && p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.IsSoldierShot ? global::Levy.Common.Targeting.EnemyTargeting.LiveBody(t.SoldierIndex) : null;
	}
}
