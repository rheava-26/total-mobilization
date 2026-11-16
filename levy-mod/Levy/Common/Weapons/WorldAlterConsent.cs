using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Net;
using Levy.Common.Util;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// THE PLAYER'S OPT-IN (Eve, 2026-11-16): world-altering weapons are allowed only after the player confirms the block-breaking warning for that weapon. The set holds the item keys (<see cref="WorldAlter.KeyOf"/>)
	/// the player confirmed in THIS world; <see cref="WorldAlter.Alters(Item)"/> answers "no" for them, so every pick path (weapon classifier, allocation, sidearms, ammo, drone arsenal, thrown-blast guard)
	/// treats a confirmed item like any other. Tools (pick, axe, hammer, drill) can never be confirmed: a levy has no use for one.
	/// The set is saved with the world, sent to every client on join and re-sent (with the world data) when the server grants a new item. A client's confirm travels as <see cref="LevyPacket.AlterConsent"/>;
	/// the server accepts only an item the scanner really flags.
	/// </summary>
	public class WorldAlterConsent : ModSystem
	{
		private static readonly HashSet<string> Keys = new();
		/// <summary>Bumped whenever the set changes (the pick caches that learned "unsupported" are dropped with it).</summary>
		public static int Version { get; private set; }

		public static bool Has(int itemType) => Keys.Count > 0 && Keys.Contains(WorldAlter.KeyOf(itemType));
		public static int Count => Keys.Count;
		public static IReadOnlyCollection<string> All => Keys;

		/// <summary>Records the player's yes for these item types (single player, host or server). True when the set changed. Anything that is not a flagged non-tool item is ignored.</summary>
		public static bool Grant(IEnumerable<int> types)
		{
			bool changed = false;
			foreach (int t in types)
			{
				if (t <= 0 || t >= ItemLoader.ItemCount) continue;
				Item sample = ContentSamples.ItemsByType.TryGetValue(t, out var s) ? s : null;
				if (sample == null || !WorldAlter.NeedsConsent(sample)) continue;
				changed |= Keys.Add(WorldAlter.KeyOf(t));
			}
			if (changed) Changed();
			return changed;
		}

		/// <summary>Takes the yes back (self-check cleanup).</summary>
		public static void Revoke(int itemType) { if (Keys.Remove(WorldAlter.KeyOf(itemType))) Changed(); }

		private static void Changed()
		{
			Version++;
			WeaponClassifier.ForgetProfiles();
			DroneArsenal.ForgetCaches();
			ThrownBlastGuardProjectile.RebuildConsentedShots();
		}

		/// <summary>The player's answer from the warning window: single player and host record it, a client asks the server, which re-checks it and tells everyone.</summary>
		public static void Confirm(IEnumerable<int> types)
		{
			var list = types.Distinct().ToList(); if (list.Count == 0) return;
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				ModPacket p = LevyPackets.New(LevyPacket.AlterConsent);
				p.Write((byte)list.Count); foreach (int t in list) p.Write(WorldAlter.KeyOf(t));
				p.Send();
				return;
			}
			if (Grant(list) && Main.netMode == NetmodeID.Server && !Main.gameMenu) NetMessage.SendData(MessageID.WorldData); // WorldData carries NetSend: every client learns the new set
		}

		public override void Load() => LevyPackets.Register(LevyPacket.AlterConsent, OnConsent);

		private static void OnConsent(BinaryReader r, int whoAmI)
		{
			int n = r.ReadByte(); var types = new List<int>();
			for (int i = 0; i < n; i++) { int t = WorldAlter.ItemFromKey(r.ReadString()); if (t > 0) types.Add(t); }
			if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active) return;
			if (Grant(types)) NetMessage.SendData(MessageID.WorldData);
		}

		public override void ClearWorld() { if (Keys.Count > 0) { Keys.Clear(); Changed(); } }

		public override void SaveWorldData(TagCompound tag) { if (Keys.Count > 0) tag["alterOk"] = Keys.OrderBy(k => k, System.StringComparer.Ordinal).ToList(); }

		public override void LoadWorldData(TagCompound tag)
		{
			Keys.Clear();
			foreach (string s in tag.GetList<string>("alterOk")) if (WorldAlter.ItemFromKey(s) > 0) Keys.Add(s);
			Changed();
		}

		public override void NetSend(BinaryWriter writer) { writer.Write((short)Keys.Count); foreach (string k in Keys) writer.Write(k); }

		public override void NetReceive(BinaryReader reader)
		{
			int n = reader.ReadInt16(); var fresh = new HashSet<string>();
			for (int i = 0; i < n; i++) fresh.Add(reader.ReadString());
			if (fresh.SetEquals(Keys)) return;
			Keys.Clear(); foreach (string k in fresh) Keys.Add(k);
			Changed();
		}
	}
}
