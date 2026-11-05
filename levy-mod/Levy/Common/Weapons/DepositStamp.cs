using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// The world's own clock, in game ticks that pass with the day (sleeping runs it faster, a day is <see cref="TicksPerDay"/>). It counts up for as long as the world is played and is saved with the world,
	/// so a deposit time recorded in one session means the same thing in the next. Server / single player.
	/// </summary>
	public class LevyClock : ModSystem
	{
		public const long TicksPerDay = 86400;
		private static long _ticks; private static long? _forced;

		/// <summary>World ticks now.</summary>
		public static long Now => _forced ?? _ticks;
		/// <summary>Probe / self-check seam: pins the clock (null releases it).</summary>
		internal static void Force(long? now) { _forced = now; }

		public override void PostUpdateWorld() { if (Main.netMode != Terraria.ID.NetmodeID.MultiplayerClient) _ticks += Math.Max(1L, (long)Math.Round(Main.dayRate)); }
		public override void OnWorldUnload() { _ticks = 0; }
		public override void SaveWorldData(TagCompound tag) { tag["levyClock"] = _ticks; }
		public override void LoadWorldData(TagCompound tag) { _ticks = tag.ContainsKey("levyClock") ? tag.GetLong("levyClock") : 0; }
	}

	/// <summary>
	/// When a weapon entered an armory, in <see cref="LevyClock"/> ticks. It lives on the item itself, so it follows it between slots, into a levy's hand and back, is saved with the armory's items and travels in its
	/// network sync. 0 = never stamped (no recency boost); <see cref="Adopted"/> = already in the armory when the stamps were introduced (no boost either). The armory stamps what it finds (<c>ArmoryTE.StampNew</c>);
	/// <see cref="WeaponRank"/> reads it through <see cref="Boost"/>.
	/// </summary>
	public class DepositStamp : GlobalItem
	{
		/// <summary>A stamp that means "was here before stamps existed": counts as old.</summary>
		public const long Adopted = 1;
		public long Tick;

		public override bool InstancePerEntity => true;
		public override bool AppliesToEntity(Item entity, bool lateInstantiation) => lateInstantiation && entity.damage > 0 && !entity.accessory;
		public override GlobalItem Clone(Item from, Item to) { var c = (DepositStamp)base.Clone(from, to); c.Tick = Tick; return c; }
		public override void SaveData(Item item, TagCompound tag) { if (Tick != 0) tag["levyDep"] = Tick; }
		public override void LoadData(Item item, TagCompound tag) { Tick = tag.ContainsKey("levyDep") ? tag.GetLong("levyDep") : 0; }
		public override void NetSend(Item item, BinaryWriter writer) { writer.Write(Tick); }
		public override void NetReceive(Item item, BinaryReader reader) { Tick = reader.ReadInt64(); }

		/// <summary>Records <paramref name="stamp"/> (default: now) on a weapon that has none. Returns true if it stamped.</summary>
		public static bool Stamp(Item it, long stamp = -1)
		{
			if (it == null || it.IsAir || !it.TryGetGlobalItem(out DepositStamp s) || s.Tick != 0) return false;
			s.Tick = stamp >= 0 ? stamp : Math.Max(2, LevyClock.Now);
			return true;
		}

		/// <summary>Probe / self-check seam: sets the stamp outright.</summary>
		internal static void Set(Item it, long tick) { if (it != null && it.TryGetGlobalItem(out DepositStamp s)) s.Tick = tick; }

		/// <summary>The stamp of an item (0 if it has none).</summary>
		public static long Of(Item it) => it != null && it.TryGetGlobalItem(out DepositStamp s) ? s.Tick : 0;

		/// <summary>The recency multiplier of an item right now (1 = none).</summary>
		public static float Boost(Item it)
		{
			long t = Of(it);
			return t <= Adopted ? 1f : WeaponRank.RecencyBoost(LevyClock.Now - t);
		}
	}
}
