using System;
using System.Collections.Generic;
using System.IO;

namespace Levy.Common.Weapons
{
	/// <summary>Which weapons a reforge run works on.</summary>
	public enum ReforgeMode : byte
	{
		/// <summary>Weapons in levies' hands right now.</summary>
		Issued,
		/// <summary>Weapons that entered the armory within <see cref="ReforgePlan.RecentDays"/> world days (see <see cref="DepositStamp"/>).</summary>
		Recent,
		/// <summary>Weapons picked by hand in the armory grid and on the issue racks.</summary>
		Picked,
		/// <summary>Every weapon the class and category filter lets through (no filter = every weapon the armory holds).</summary>
		Group,
	}

	/// <summary>What a reforge run rerolls toward.</summary>
	public enum ReforgeGoal : byte
	{
		/// <summary>Today's floor: the top slice of the prefixes each weapon can roll (<see cref="ReforgePlan.FloorIndex"/>).</summary>
		Floor,
		/// <summary>One named prefix (<see cref="ReforgePlan.PrefixId"/>); a weapon that cannot take it is left alone.</summary>
		Prefix,
	}

	/// <summary>
	/// One reforge run, as the player set it up: the target set, the goal and the spending cap. Pure data with a clamped wire form (<see cref="ToBytes"/> / <see cref="FromBytes"/>), so a multiplayer client can send it
	/// and the server never trusts it. The selection and the rolling live in <c>ArmoryTE.Reforge.cs</c>.
	/// </summary>
	public sealed class ReforgePlan
	{
		public const int MaxPicks = 512, MaxRecentDays = 365, FormatVersion = 1, MinBytes = 24;
		/// <summary>The selectable look-back windows for <see cref="ReforgeMode.Recent"/>, in world days.</summary>
		public static readonly int[] RecentChoices = { 1, 3, 7, 30 };
		/// <summary>The selectable spending caps in copper (0 = no cap): 1 gold, 5 gold, 20 gold, 50 gold, 1 platinum, 5 platinum, 20 platinum.</summary>
		public static readonly long[] CapChoices = { 0, 10000, 50000, 200000, 500000, 1000000, 5000000, 20000000 };

		public ReforgeMode Mode = ReforgeMode.Issued;
		public int RecentDays = 1;
		/// <summary>Class id and category id that narrow every mode but Picked; null = any.</summary>
		public string ClassKey, Category;
		public ReforgeGoal Goal = ReforgeGoal.Floor;
		public int FloorIndex;
		public int PrefixId;
		/// <summary>The most this run may spend in copper; 0 = no cap.</summary>
		public long CapCopper;
		/// <summary>Hand-picked weapons: armory region (0 = storage, 1 = weapon rack) and slot.</summary>
		public List<(byte region, short slot)> Picks = new();

		public ReforgePlan Clone()
		{
			var c = (ReforgePlan)MemberwiseClone();
			c.Picks = new List<(byte, short)>(Picks);
			return c;
		}

		public bool IsPicked(byte region, int slot) => Picks.Contains((region, (short)slot));

		/// <summary>Picks the weapon if it is not picked, unpicks it if it is. False when the pick list is full.</summary>
		public bool TogglePick(byte region, int slot)
		{
			var key = (region, (short)slot);
			if (Picks.Remove(key)) return true;
			if (Picks.Count >= MaxPicks) return false;
			Picks.Add(key);
			return true;
		}

		public byte[] ToBytes()
		{
			using var ms = new MemoryStream();
			using (var w = new BinaryWriter(ms))
			{
				w.Write((byte)FormatVersion);
				w.Write((byte)Mode); w.Write(RecentDays);
				w.Write(ClassKey ?? ""); w.Write(Category ?? "");
				w.Write((byte)Goal); w.Write((byte)FloorIndex); w.Write(PrefixId); w.Write(CapCopper);
				int n = Math.Min(Picks.Count, MaxPicks);
				w.Write((ushort)n);
				for (int i = 0; i < n; i++) { w.Write(Picks[i].region); w.Write(Picks[i].slot); }
			}
			return ms.ToArray();
		}

		/// <summary>Reads a plan written by <see cref="ToBytes"/>. Never throws: bad data yields the default plan, and every field is clamped.</summary>
		public static ReforgePlan FromBytes(byte[] data)
		{
			var p = new ReforgePlan();
			if (data == null || data.Length < MinBytes || data.Length > 8 * 1024) return p;
			try
			{
				using var r = new BinaryReader(new MemoryStream(data));
				if (r.ReadByte() != FormatVersion) return p;
				byte mode = r.ReadByte(); p.Mode = mode <= (byte)ReforgeMode.Group ? (ReforgeMode)mode : ReforgeMode.Issued;
				p.RecentDays = Math.Clamp(r.ReadInt32(), 1, MaxRecentDays);
				string cls = r.ReadString(), cat = r.ReadString();
				p.ClassKey = cls.Length == 0 || cls.Length > 24 ? null : cls;
				p.Category = cat.Length == 0 || cat.Length > 24 ? null : cat;
				byte goal = r.ReadByte(); p.Goal = goal <= (byte)ReforgeGoal.Prefix ? (ReforgeGoal)goal : ReforgeGoal.Floor;
				p.FloorIndex = Math.Clamp((int)r.ReadByte(), 0, LevyConstants.UI.ReforgeFloorTopFraction.Length - 1);
				p.PrefixId = Math.Max(0, r.ReadInt32());
				p.CapCopper = Math.Clamp(r.ReadInt64(), 0L, 100_000_000_000L);
				int n = Math.Min((int)r.ReadUInt16(), MaxPicks);
				n = Math.Min(n, (int)((data.Length - r.BaseStream.Position) / 3));
				for (int i = 0; i < n; i++)
				{
					byte region = r.ReadByte(); short slot = r.ReadInt16();
					if (region > 1 || slot < 0 || p.Picks.Contains((region, slot))) continue;
					p.Picks.Add((region, slot));
				}
			}
			catch (Exception) { return new ReforgePlan(); }
			return p;
		}
	}
}
