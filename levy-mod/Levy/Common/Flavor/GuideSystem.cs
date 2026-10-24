using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Flavor
{
	/// <summary>
	/// The four progression guides (Firepower, Healthcare, the Arcane Arts, Expertise): army-wide, once per world, studied like the event books
	/// (<see cref="FlavorSystem.Study"/> routes a guide key here). This system owns the four world flags (saved with the world, synced to every
	/// client in the world-data message, only ever turning on) and every number they change: the base and cap of a soldier's HP and mana, the
	/// accessory slots, and the buff slots an Armory's potion row gains. <see cref="SoldierRecord"/> and the crystal feed read these;
	/// guides move the base and the cap and never touch how crystals add up beneath the cap. No cap here can sit above the hard soldier
	/// ceilings (<see cref="LevyConstants.MaxSoldierHp"/> HP, <see cref="LevyConstants.MaxMana"/> mana): <see cref="HpCap"/> and <see cref="ManaCap"/> are clamped to them.
	/// </summary>
	public class GuideSystem : ModSystem
	{
		public static GuideSystem Instance => ModContent.GetInstance<GuideSystem>();

		/// <summary>Book keys (the item is <c>GuideTo{key}</c>).</summary>
		public static readonly string[] Keys = { "Firepower", "Healthcare", "TheArcaneArts", "Expertise" };

		/// <summary>World flags. Persisted here; the server owns them.</summary>
		public bool Firepower, Healthcare, ArcaneArts, Expertise;

		// ---------------------------------------------------------------- derived numbers (what the rest of the mod reads)

		/// <summary>The HP a soldier starts from before crystals and fruit.</summary>
		public static int HpBase => Instance != null && Instance.Firepower ? LevyConstants.FirepowerBaseHp : LevyConstants.BaseHp;
		/// <summary>The most HP crystals and fruit can raise a soldier to. Never above <see cref="LevyConstants.MaxSoldierHp"/>.</summary>
		public static int HpCap => Math.Min(Instance != null && Instance.Healthcare ? LevyConstants.HealthcareHpCap : LevyConstants.DefaultHpCap, LevyConstants.MaxSoldierHp);
		/// <summary>The mana a soldier starts from before mana crystals.</summary>
		public static int ManaBase => Instance != null && Instance.Firepower ? LevyConstants.FirepowerBaseMana : LevyConstants.BaseMana;
		/// <summary>The most mana crystals can raise a soldier to. Never above <see cref="LevyConstants.MaxMana"/>.</summary>
		public static int ManaCap => Math.Min(Instance != null && Instance.ArcaneArts ? LevyConstants.ArcaneManaCap : LevyConstants.DefaultManaCap, LevyConstants.MaxMana);
		/// <summary>Accessory slots every soldier gains on top of his grade.</summary>
		public static int ExtraAccessorySlots => Instance != null && Instance.Expertise ? LevyConstants.ExpertiseAccessorySlots : 0;
		/// <summary>Buff slots added to every Armory's potion row (see <see cref="ArmoryTE.TargetPotionSlots"/>).</summary>
		public static int ArmoryBuffSlots => Instance != null && Instance.Firepower ? LevyConstants.FirepowerBuffSlots : 0;

		// ---------------------------------------------------------------- keys and flags

		public static bool IsGuideKey(string key) => Array.IndexOf(Keys, key) >= 0;

		public static bool IsStudied(string key)
		{
			GuideSystem g = Instance;
			if (g == null) return false;
			return key switch
			{
				"Firepower" => g.Firepower,
				"Healthcare" => g.Healthcare,
				"TheArcaneArts" => g.ArcaneArts,
				"Expertise" => g.Expertise,
				_ => false,
			};
		}

		/// <summary>Mirrors a flag the server announced (clients only; the server owns the truth).</summary>
		public static void SetLocal(string key)
		{
			GuideSystem g = Instance;
			if (g == null) return;
			switch (key)
			{
				case "Firepower": g.Firepower = true; break;
				case "Healthcare": g.Healthcare = true; break;
				case "TheArcaneArts": g.ArcaneArts = true; break;
				case "Expertise": g.Expertise = true; break;
			}
		}

		/// <summary>
		/// Server and single player: turns a guide on once per world. False when the key is unknown or the guide was already studied (so the item is not
		/// consumed). The new headroom arrives as health and mana (a soldier at full stays at full), and every soldier recomputes his stats.
		/// </summary>
		public static bool TrySet(string key)
		{
			if (!LevyUtil.IsServerOrSingle || !IsGuideKey(key) || IsStudied(key)) return false;

			// Everyone the change can touch: the saved Regulars and every living body (militia have no saved record).
			var records = new HashSet<SoldierRecord>();
			RosterSystem roster = RosterSystem.Instance;
			if (roster != null)
			{
				foreach (SoldierRecord r in roster.Regulars) if (r != null) records.Add(r);
				foreach (SoldierNPC body in roster.ActiveBodies()) if (body.Record != null) records.Add(body.Record);
			}
			var before = new Dictionary<SoldierRecord, (int hp, int mana)>(records.Count);
			foreach (SoldierRecord r in records) before[r] = (r.MaxHp, r.MaxMana);

			SetLocal(key);

			foreach (SoldierRecord r in records)
			{
				(int hp, int mana) old = before[r];
				int hpGain = Math.Max(0, r.MaxHp - old.hp), manaGain = Math.Max(0, r.MaxMana - old.mana);
				if (manaGain > 0) r.Mana = Math.Clamp(r.Mana + manaGain, 0, r.MaxMana);
				if (r.HasBody && Main.npc[r.BodyIndex].ModNPC is SoldierNPC body)
				{
					NPC npc = body.NPC;
					npc.lifeMax = Math.Max(1, r.MaxHp);
					npc.life = Math.Clamp(npc.life + hpGain, 1, npc.lifeMax);
					r.CurrentHp = npc.life;
					body.StatsDirty = true; // the accessory slots and shared buffs are re-read on the next refresh
					npc.netUpdate = true;
				}
				else if (!r.Dead && hpGain > 0) r.CurrentHp = Math.Clamp(r.CurrentHp + hpGain, 1, r.MaxHp);

				// Expertise opens a new accessory slot: each Regular draws one from his home Armory at once (a promotion does the same), rather than waiting for the next top-up.
				if (key == "Expertise" && r.Kind == SoldierKind.Regular && !r.Dead && !r.Reserve) // a soldier in reserve draws nothing from the racks until he is recalled
				{
					BarracksTE home = SettlementSystem.Instance.GetBarracks(r.HomeBarracksId);
					ArmoryTE armory = home == null ? null : SettlementSystem.Instance.LinkedArmory(home);
					if (armory != null && armory.IssueGear(r) && r.HasBody && Main.npc[r.BodyIndex].ModNPC is SoldierNPC issued) issued.NotifyGearChanged();
				}
			}

			// Firepower adds buff slots: every Armory grows its potion row now (clients get the new length through the tile-entity sync).
			if (key == "Firepower")
				foreach (TileEntity te in TileEntity.ByID.Values)
					if (te is ArmoryTE armory && armory.FitPotions()) armory.Sync();

			// WorldData carries NetSend, so one resend pushes the new flag to every client; the snapshot refreshes their HP bars.
			if (LevyUtil.IsServer) NetMessage.SendData(MessageID.WorldData);
			roster?.SendRosterSnapshot();
			return true;
		}

		// ---------------------------------------------------------------- persistence and join sync

		public override void SaveWorldData(TagCompound tag)
		{
			if (Firepower) tag["guideFirepower"] = true;
			if (Healthcare) tag["guideHealthcare"] = true;
			if (ArcaneArts) tag["guideArcane"] = true;
			if (Expertise) tag["guideExpertise"] = true;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Firepower = tag.GetBool("guideFirepower");
			Healthcare = tag.GetBool("guideHealthcare");
			ArcaneArts = tag.GetBool("guideArcane");
			Expertise = tag.GetBool("guideExpertise");
		}

		public override void ClearWorld() => Firepower = Healthcare = ArcaneArts = Expertise = false;

		public override void NetSend(BinaryWriter writer) => writer.Write((byte)new BitsByte(Firepower, Healthcare, ArcaneArts, Expertise));

		public override void NetReceive(BinaryReader reader)
		{
			// Flags only ever turn on, so a late world-data message never un-studies a guide.
			BitsByte bits = reader.ReadByte();
			Firepower |= bits[0]; Healthcare |= bits[1]; ArcaneArts |= bits[2]; Expertise |= bits[3];
		}
	}
}
