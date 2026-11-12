using System;
using System.Collections.Generic;
using System.Reflection;
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
			public bool[] Listeners; // every mod player's mouseWorldListener
		}

		private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private static readonly Dictionary<Type, FieldInfo> _listener = new();

		private static FieldInfo ListenerField(ModPlayer mp)
		{
			Type t = mp.GetType();
			if (!_listener.TryGetValue(t, out FieldInfo f)) _listener[t] = f = t.GetField("mouseWorldListener", Any);
			return f;
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
			List<bool> listeners = null;
			foreach (ModPlayer mp in p.ModPlayers) { FieldInfo f = ListenerField(mp); if (f != null) (listeners ??= new List<bool>()).Add((bool)f.GetValue(mp)); }
			s.Listeners = listeners?.ToArray();
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
			int n = 0;
			foreach (ModPlayer mp in p.ModPlayers) { FieldInfo f = ListenerField(mp); if (f != null && n < s.Listeners.Length) f.SetValue(mp, s.Listeners[n++]); }
		}

		/// <summary>Runs <paramref name="body"/> (a machine spawning its rounds) and puts the owner's use state back afterwards.</summary>
		public static void Hold(int owner, Action body)
		{
			Snap s = Take(owner);
			try { body(); } finally { Put(s); }
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
			Snap s = Take(self.owner); GuardedUpdates++;
			try { orig(self, i); } finally { if (s.Live && (s.P.heldProj != s.HeldProj || s.P.itemTime != s.ItemTime || s.P.itemAnimation != s.ItemAnimation)) Undone++; Put(s); }
		}

		private static void KillDetour(On_Projectile.orig_Kill orig, Projectile self)
		{
			if (!IsMachineRound(self)) { orig(self); return; }
			Snap s = Take(self.owner);
			try { orig(self); } finally { Put(s); }
		}
	}
}
