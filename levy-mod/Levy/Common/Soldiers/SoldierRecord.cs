using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace Levy.Common.Soldiers
{
	/// <summary>
	/// A soldier is a saved record plus a body. Regulars persist in world data; militia records live only while mustered.
	/// </summary>
	public class SoldierRecord
	{
		// ---- identity ----
		public int Id;
		public string Name = "Pvt. Nobody";
		public SoldierKind Kind = SoldierKind.Regular;
		public Grade Grade = Grade.Levy;
		public Temperament Temperament = Temperament.Greenhorn;
		public bool Veteran;

		// ---- growth ----
		public int LifeCrystals, LifeFruit, ManaCrystals;
		public int CurrentHp = LevyConstants.BaseHp;
		public int Mana = LevyConstants.BaseMana;
		public int Kills, BossFightsSurvived;

		// ---- gear ----
		public SoldierGear Gear = new SoldierGear();

		// ---- home / commander ----
		/// <summary>TileEntity ID of the home barracks (-1 = homeless).</summary>
		public int HomeBarracksId = -1;
		public Point16 HomePos = Point16.NegativeOne;
		/// <summary>Owning player's name (Regulars belong to players). Militia: empty.</summary>
		public string CommanderName = "";

		// ---- pay ----
		public int DaysUnpaid;
		public bool StoodDown;
		public bool HazardPaid;

		// ---- orders ----
		public SoldierOrder Order = SoldierOrder.Follow;
		public bool HoldFire;
		/// <summary>Squad stance: hold a much wider loose formation (anti-AoE). Saved; off for old saves.</summary>
		public bool SpreadOut;
		public Vector2 HoldPos;
		public Point16 PatrolA = Point16.NegativeOne, PatrolB = Point16.NegativeOne;

		// ---- looks (rolled at enlistment) ----
		public int Hair; public int SkinVariant; public bool Male = true;
		public Color HairColor = new(215, 90, 55), SkinColor = new(255, 125, 90), EyeColor = new(105, 90, 75),
			ShirtColor = new(175, 165, 140), UnderShirtColor = new(160, 180, 215), PantsColor = new(255, 230, 175), ShoeColor = new(160, 105, 60);

		// ---- runtime (never saved) ----
		public int BodyIndex = -1;
		public int RespawnTimer;
		public bool Dead;
		public float Fear;
		public int CommanderIndex = -1;
		public HashSet<int> EngagedBosses = new();
		public int LastDeathTick;

		public bool HasBody => BodyIndex >= 0 && BodyIndex < Main.maxNPCs && Main.npc[BodyIndex].active
			&& Main.npc[BodyIndex].ModNPC is Content.NPCs.SoldierNPC s && ReferenceEquals(s.Record, this);

		public int MaxHp
		{
			get
			{
				int hp = LevyConstants.BaseHp + LevyConstants.HpPerLifeCrystal * LifeCrystals;
				if (Grade == Grade.Champion) hp += LevyConstants.HpPerLifeFruit * LifeFruit;
				return Math.Min(hp, LevyConstants.GradeHpCap[(int)Grade]);
			}
		}

		public int MaxMana => Math.Min(LevyConstants.BaseMana + LevyConstants.ManaPerCrystal * ManaCrystals, LevyConstants.MaxMana);
		public int AccessorySlots => LevyConstants.GradeSlots[(int)Grade];
		public int CommandPointCost => Kind == SoldierKind.Militia ? 1 : LevyConstants.GradeCpCost[(int)Grade];
		public string RankPrefix => Veteran ? "Sgt." : "Pvt.";

		/// <summary>Compact snapshot for clients (UI): identity, grade, hp, pay state, order, home, gear visual.</summary>
		public void WriteSnapshot(System.IO.BinaryWriter w)
		{
			w.Write(Id); w.Write(Name ?? ""); w.Write((byte)Kind); w.Write((byte)Grade); w.Write((byte)Temperament); w.Write(Veteran);
			w.Write(LifeCrystals); w.Write(LifeFruit); w.Write(ManaCrystals); w.Write(CurrentHp); w.Write(Kills); w.Write(BossFightsSurvived);
			w.Write(HomeBarracksId); w.Write(CommanderName ?? ""); w.Write(DaysUnpaid); w.Write(StoodDown); w.Write((byte)Order); w.Write(HoldFire); w.Write(SpreadOut);
			w.Write(Dead); w.Write(RespawnTimer); w.Write(Fear);
			w.Write(Hair); w.Write(SkinVariant); w.Write(Male);
			w.WriteRGB(HairColor); w.WriteRGB(SkinColor); w.WriteRGB(EyeColor); w.WriteRGB(ShirtColor); w.WriteRGB(UnderShirtColor); w.WriteRGB(PantsColor); w.WriteRGB(ShoeColor);
			Gear.WriteVisual(w);
		}

		public static SoldierRecord ReadSnapshot(System.IO.BinaryReader r)
		{
			var s = new SoldierRecord();
			s.Id = r.ReadInt32(); s.Name = r.ReadString(); s.Kind = (SoldierKind)r.ReadByte(); s.Grade = (Grade)r.ReadByte(); s.Temperament = (Temperament)r.ReadByte(); s.Veteran = r.ReadBoolean();
			s.LifeCrystals = r.ReadInt32(); s.LifeFruit = r.ReadInt32(); s.ManaCrystals = r.ReadInt32(); s.CurrentHp = r.ReadInt32(); s.Kills = r.ReadInt32(); s.BossFightsSurvived = r.ReadInt32();
			s.HomeBarracksId = r.ReadInt32(); s.CommanderName = r.ReadString(); s.DaysUnpaid = r.ReadInt32(); s.StoodDown = r.ReadBoolean(); s.Order = (SoldierOrder)r.ReadByte(); s.HoldFire = r.ReadBoolean(); s.SpreadOut = r.ReadBoolean();
			s.Dead = r.ReadBoolean(); s.RespawnTimer = r.ReadInt32(); s.Fear = r.ReadSingle();
			s.Hair = r.ReadInt32(); s.SkinVariant = r.ReadInt32(); s.Male = r.ReadBoolean();
			s.HairColor = r.ReadRGB(); s.SkinColor = r.ReadRGB(); s.EyeColor = r.ReadRGB(); s.ShirtColor = r.ReadRGB(); s.UnderShirtColor = r.ReadRGB(); s.PantsColor = r.ReadRGB(); s.ShoeColor = r.ReadRGB();
			s.Gear.ReadVisual(r);
			return s;
		}

		public TagCompound Save()
		{
			return new TagCompound
			{
				["id"] = Id, ["name"] = Name, ["kind"] = (byte)Kind, ["grade"] = (byte)Grade, ["temp"] = (byte)Temperament, ["vet"] = Veteran,
				["lc"] = LifeCrystals, ["lf"] = LifeFruit, ["mc"] = ManaCrystals, ["hp"] = CurrentHp, ["mana"] = Mana, ["kills"] = Kills, ["bosses"] = BossFightsSurvived,
				["gear"] = Gear.Save(), ["home"] = HomeBarracksId, ["homeX"] = (int)HomePos.X, ["homeY"] = (int)HomePos.Y, ["cmd"] = CommanderName ?? "",
				["unpaid"] = DaysUnpaid, ["stood"] = StoodDown, ["hazard"] = HazardPaid,
				["order"] = (byte)Order, ["holdFire"] = HoldFire, ["spread"] = SpreadOut, ["holdX"] = HoldPos.X, ["holdY"] = HoldPos.Y,
				["pAX"] = (int)PatrolA.X, ["pAY"] = (int)PatrolA.Y, ["pBX"] = (int)PatrolB.X, ["pBY"] = (int)PatrolB.Y,
				["hair"] = Hair, ["skinVar"] = SkinVariant, ["male"] = Male,
				["cHair"] = HairColor, ["cSkin"] = SkinColor, ["cEye"] = EyeColor, ["cShirt"] = ShirtColor, ["cUnder"] = UnderShirtColor, ["cPants"] = PantsColor, ["cShoe"] = ShoeColor,
			};
		}

		public static SoldierRecord Load(TagCompound t)
		{
			var r = new SoldierRecord
			{
				Id = t.GetInt("id"), Name = t.GetString("name"), Kind = (SoldierKind)t.GetByte("kind"), Grade = (Grade)t.GetByte("grade"),
				Temperament = (Temperament)t.GetByte("temp"), Veteran = t.GetBool("vet"),
				LifeCrystals = t.GetInt("lc"), LifeFruit = t.GetInt("lf"), ManaCrystals = t.GetInt("mc"), CurrentHp = t.GetInt("hp"), Mana = t.GetInt("mana"),
				Kills = t.GetInt("kills"), BossFightsSurvived = t.GetInt("bosses"),
				Gear = SoldierGear.Load(t.GetCompound("gear")), HomeBarracksId = t.GetInt("home"), HomePos = new Point16(t.GetInt("homeX"), t.GetInt("homeY")),
				CommanderName = t.GetString("cmd"), DaysUnpaid = t.GetInt("unpaid"), StoodDown = t.GetBool("stood"), HazardPaid = t.GetBool("hazard"),
				Order = (SoldierOrder)t.GetByte("order"), HoldFire = t.GetBool("holdFire"), SpreadOut = t.GetBool("spread"), HoldPos = new Vector2(t.GetFloat("holdX"), t.GetFloat("holdY")),
				PatrolA = new Point16(t.GetInt("pAX"), t.GetInt("pAY")), PatrolB = new Point16(t.GetInt("pBX"), t.GetInt("pBY")),
				Hair = t.GetInt("hair"), SkinVariant = t.GetInt("skinVar"), Male = t.GetBool("male"),
			};
			if (t.ContainsKey("cHair")) { r.HairColor = t.Get<Color>("cHair"); r.SkinColor = t.Get<Color>("cSkin"); r.EyeColor = t.Get<Color>("cEye"); r.ShirtColor = t.Get<Color>("cShirt"); r.UnderShirtColor = t.Get<Color>("cUnder"); r.PantsColor = t.Get<Color>("cPants"); r.ShoeColor = t.Get<Color>("cShoe"); }
			if (r.CurrentHp <= 0) r.CurrentHp = r.MaxHp;
			if (!t.ContainsKey("homeX")) r.HomePos = Point16.NegativeOne;
			return r;
		}
	}
}
