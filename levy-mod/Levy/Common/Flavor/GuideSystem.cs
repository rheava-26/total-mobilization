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
	/// The five progression guides (Firepower, Healthcare, the Arcane Arts, Expertise, Engineering): army-wide, once per world, studied like the event books
	/// (<see cref="FlavorSystem.Study"/> routes a guide key here). This system owns the five world flags (saved with the world, synced to every
	/// client in the world-data message, only ever turning on) and every number they change: the HP and mana a MILITIAMAN is mustered with, the
	/// accessory slots, and the buff slots an Armory's potion row gains. The guides never touch a REGULAR's HP or mana: a regular mirrors the vanilla
	/// player (crystals and fruit, hard-capped at <see cref="LevyConstants.MaxSoldierHp"/> HP and <see cref="LevyConstants.MaxMana"/> mana) whatever is studied.
	/// Engineering changes no number: it records the unlock the Mechanization mod's combat-drone garrison reads.
	/// </summary>
	public class GuideSystem : ModSystem
	{
		public static GuideSystem Instance => ModContent.GetInstance<GuideSystem>();

		/// <summary>Book keys (the item is <c>GuideTo{key}</c>).</summary>
		public static readonly string[] Keys = { "Firepower", "Healthcare", "TheArcaneArts", "Expertise", "Engineering" };

		/// <summary>World flags. Persisted here; the server owns them.</summary>
		public bool Firepower, Healthcare, ArcaneArts, Expertise, Engineering;

		// ---------------------------------------------------------------- derived numbers (what the rest of the mod reads)

		/// <summary>
		/// The most HP Life Crystals and Life Fruit can raise a REGULAR to: the vanilla player's <see cref="LevyConstants.MaxSoldierHp"/>, whatever is studied (a guide never moves it).
		/// Lives here because the crystal feed (RosterSystem.CanAbsorbCrystal) reads it from here.
		/// </summary>
		public static int HpCap => LevyConstants.MaxSoldierHp;
		/// <summary>The most mana Mana Crystals can raise a REGULAR to: the vanilla player's <see cref="LevyConstants.MaxMana"/>, whatever is studied (a guide never moves it).</summary>
		public static int ManaCap => LevyConstants.MaxMana;

		/// <summary>
		/// The HP a MILITIAMAN is mustered with: <see cref="LevyConstants.BaseHp"/> plus the bonus of each studied guide (Firepower, Healthcare), held under
		/// <see cref="LevyConstants.MaxSoldierHp"/>. Militia take no crystals, so this is their whole ladder.
		/// </summary>
		public static int MilitiaHp
		{
			get
			{
				GuideSystem g = Instance;
				int hp = LevyConstants.BaseHp;
				if (g != null)
				{
					if (g.Firepower) hp += LevyConstants.FirepowerMilitiaHp;
					if (g.Healthcare) hp += LevyConstants.HealthcareMilitiaHp;
				}
				return Math.Min(hp, LevyConstants.MaxSoldierHp);
			}
		}

		/// <summary>
		/// The mana a MILITIAMAN is mustered with: <see cref="LevyConstants.BaseMana"/> plus the bonus of each studied guide (Firepower, the Arcane Arts), held under
		/// <see cref="LevyConstants.MaxMana"/>. Militia take no crystals, so this is their whole ladder.
		/// </summary>
		public static int MilitiaMana
		{
			get
			{
				GuideSystem g = Instance;
				int mana = LevyConstants.BaseMana;
				if (g != null)
				{
					if (g.Firepower) mana += LevyConstants.FirepowerMilitiaMana;
					if (g.ArcaneArts) mana += LevyConstants.ArcaneMilitiaMana;
				}
				return Math.Min(mana, LevyConstants.MaxMana);
			}
		}

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
				"Engineering" => g.Engineering,
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
				case "Engineering": g.Engineering = true; break;
			}
		}

		/// <summary>
		/// Server and single player: turns a guide on once per world. False when the key is unknown or the guide was already studied (so the item is not
		/// consumed). The new health and mana arrive on the militia already in the field (a militiaman at full stays at full; a regular's pools never change), and every soldier recomputes his stats.
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
					if (r.MaxHp != old.hp) // only a militiaman's maximum moves with a guide; a regular's body is left exactly as it is
					{
						npc.lifeMax = Math.Max(1, r.MaxHp);
						npc.life = Math.Clamp(npc.life + hpGain, 1, npc.lifeMax);
						r.CurrentHp = npc.life;
					}
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
			if (Engineering) tag["guideEngineering"] = true;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Firepower = tag.GetBool("guideFirepower");
			Healthcare = tag.GetBool("guideHealthcare");
			ArcaneArts = tag.GetBool("guideArcane");
			Expertise = tag.GetBool("guideExpertise");
			Engineering = tag.GetBool("guideEngineering");
		}

		public override void ClearWorld() => Firepower = Healthcare = ArcaneArts = Expertise = Engineering = false;

		public override void NetSend(BinaryWriter writer) => writer.Write((byte)new BitsByte(Firepower, Healthcare, ArcaneArts, Expertise, Engineering));

		public override void NetReceive(BinaryReader reader)
		{
			// Flags only ever turn on, so a late world-data message never un-studies a guide.
			BitsByte bits = reader.ReadByte();
			Firepower |= bits[0]; Healthcare |= bits[1]; ArcaneArts |= bits[2]; Expertise |= bits[3]; Engineering |= bits[4];
		}
	}
}
