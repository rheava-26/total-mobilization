using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Net;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Flavor
{
	/// <summary>
	/// Flavor: event-book study (flag setting, packet, announcement), the Fortification defense hook, the Nurse's price and the
	/// saved "Guide hint shown" bit. The flags themselves live on <see cref="SettlementSystem"/> (persisted by its owner);
	/// this system only sets them through <c>TrySetBook</c>.
	/// </summary>
	public class FlavorSystem : ModSystem
	{
		public static FlavorSystem Instance => ModContent.GetInstance<FlavorSystem>();

		public static readonly Color NoticeColor = new Color(220, 190, 120);
		public static readonly string[] BookKeys = { "Conscription", "Drill", "Plunder", "Fortification", "Discipline" };

		/// <summary>True once the Guide has said the hint line. Saved with the world; a client only changes its own copy.</summary>
		public bool GuideHintShown;

		private static readonly Func<SoldierNPC, int> FortificationHook = s =>
			(SettlementSystem.Instance.BookFortification && s.Home != null && LevyUtil.WithinTiles(s.NPC.Center, s.Home.Center, LevyConstants.FortificationRadius))
				? LevyConstants.FortificationDefense : 0;

		public override void Load()
		{
			LevyPackets.Register(LevyPacket.BookUsed, HandleBookUsed);
			SoldierNPC.ExtraDefense = FortificationHook;
		}

		public override void Unload()
		{
			SoldierNPC.ExtraDefense = null;
		}

		// ---------------------------------------------------------------- books

		public static bool IsValidKey(string key) => Array.IndexOf(BookKeys, key) >= 0 || GuideSystem.IsGuideKey(key);

		public static bool IsStudied(string key)
		{
			if (GuideSystem.IsGuideKey(key)) return GuideSystem.IsStudied(key); // the progression guides live on GuideSystem
			SettlementSystem s = SettlementSystem.Instance;
			if (s == null) return false;
			return key switch
			{
				"Conscription" => s.BookConscription,
				"Drill" => s.BookDrill,
				"Plunder" => s.BookPlunder,
				"Fortification" => s.BookFortification,
				"Discipline" => s.BookDiscipline,
				_ => false,
			};
		}

		/// <summary>Mirrors a flag the server announced (clients only; the server owns the truth).</summary>
		private static void SetStudiedLocal(string key)
		{
			if (GuideSystem.IsGuideKey(key)) { GuideSystem.SetLocal(key); return; }
			SettlementSystem s = SettlementSystem.Instance;
			if (s == null) return;
			switch (key)
			{
				case "Conscription": s.BookConscription = true; break;
				case "Drill": s.BookDrill = true; break;
				case "Plunder": s.BookPlunder = true; break;
				case "Fortification": s.BookFortification = true; break;
				case "Discipline": s.BookDiscipline = true; break;
			}
		}

		/// <summary>
		/// Server and single player: sets the world flag and announces it. False when the key is unknown or the book was already
		/// studied (so the item is not consumed). A second call for the same book is a harmless no-op.
		/// </summary>
		public static bool Study(string key)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || !IsValidKey(key)) return false;
			if (!(GuideSystem.IsGuideKey(key) ? GuideSystem.TrySet(key) : SettlementSystem.Instance.TrySetBook(key))) return false;
			LevyUtil.Chat(LevyUtil.T("Flavor.Studied", BookName(key), EffectText(key)), NoticeColor);
			if (Main.netMode == NetmodeID.Server) SendFlag(key);
			return true;
		}

		/// <summary>Client to server: the book was consumed here.</summary>
		public static void SendBookUsed(string key)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient) return;
			ModPacket packet = LevyPackets.New(LevyPacket.BookUsed);
			packet.Write(key);
			packet.Send();
		}

		/// <summary>Server to every client: this flag is now set (same packet id, opposite direction).</summary>
		private static void SendFlag(string key)
		{
			ModPacket packet = LevyPackets.New(LevyPacket.BookUsed);
			packet.Write(key);
			packet.Send(-1, -1);
		}

		private static void HandleBookUsed(BinaryReader reader, int fromWhoAmI)
		{
			string key = reader.ReadString();
			if (!IsValidKey(key)) return;
			if (Main.netMode == NetmodeID.Server) Study(key);
			else if (Main.netMode == NetmodeID.MultiplayerClient) SetStudiedLocal(key);
		}

		private static string BookName(string key) => Language.GetTextValue("Mods.Levy.Items.GuideTo" + key + ".DisplayName");

		private static string EffectText(string key)
		{
			switch (key)
			{
				case "Conscription": return LevyUtil.T("Flavor.Effect.Conscription", LevyConstants.ManpowerPerNpcConscription);
				case "Drill": return LevyUtil.T("Flavor.Effect.Drill", (int)Math.Round((1f - LevyConstants.DrillMilitiaAimMult) * 100f));
				case "Plunder": return LevyUtil.T("Flavor.Effect.Plunder", (int)Math.Round((LevyConstants.PlunderCoinMult - 1f) * 100f));
				case "Fortification": return LevyUtil.T("Flavor.Effect.Fortification", LevyConstants.FortificationRadius, LevyConstants.FortificationDefense);
				case "Discipline": return LevyUtil.T("Flavor.Effect.Discipline", (int)Math.Round((1f - LevyConstants.DisciplineAimErrorMult) * 100f));
				case "Firepower": return LevyUtil.T("Flavor.Effect.Firepower", LevyConstants.FirepowerBaseHp, LevyConstants.FirepowerBaseMana, LevyConstants.FirepowerBuffShareSlots);
				case "Healthcare": return LevyUtil.T("Flavor.Effect.Healthcare", LevyConstants.HealthcareHpCap);
				case "TheArcaneArts": return LevyUtil.T("Flavor.Effect.TheArcaneArts", LevyConstants.ArcaneManaCap);
				case "Expertise": return LevyUtil.T("Flavor.Effect.Expertise", LevyConstants.ExpertiseAccessorySlots);
				default: return "";
			}
		}

		// ---------------------------------------------------------------- nurse

		/// <summary>
		/// The vanilla Nurse's price for healing <paramref name="missingHp"/>: missing life times a multiplier for how far the world has
		/// progressed (x2 in Expert and Master). Her happiness discount is per-player and is not applied.
		/// </summary>
		public static long NurseHealPrice(int missingHp)
		{
			if (missingHp <= 0) return 0;
			long multiplier = 1;
			if (NPC.downedGolemBoss) multiplier = 200;
			else if (NPC.downedPlantBoss) multiplier = 150;
			else if (NPC.downedMechBossAny) multiplier = 100;
			else if (Main.hardMode) multiplier = 60;
			else if (NPC.downedBoss3 || NPC.downedQueenBee) multiplier = 25;
			else if (NPC.downedBoss2) multiplier = 10;
			else if (NPC.downedBoss1) multiplier = 3;
			if (Main.expertMode) multiplier *= 2;
			return Math.Max(1L, missingHp * multiplier);
		}

		/// <summary>The healing potion sound, heard by everyone near <paramref name="position"/> (broadcast from a dedicated server).</summary>
		public static void PlayHealSound(Vector2 position)
		{
			if (Main.netMode == NetmodeID.Server)
			{
				if (SoundID.IndexByName != null && SoundID.IndexByName.TryGetValue("Item4", out ushort index))
					NetMessage.PlayNetSound(new NetMessage.NetSoundInfo(position, index), -1, -1);
			}
			else
			{
				SoundEngine.PlaySound(SoundID.Item4, position);
			}
		}

		// ---------------------------------------------------------------- persistence and join sync

		public override void SaveWorldData(TagCompound tag)
		{
			if (GuideHintShown) tag["guideHintShown"] = true;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			GuideHintShown = tag.GetBool("guideHintShown");
		}

		public override void ClearWorld()
		{
			GuideHintShown = false;
		}

		public override void NetSend(BinaryWriter writer)
		{
			SettlementSystem s = SettlementSystem.Instance;
			writer.Write((byte)new BitsByte(GuideHintShown, s.BookConscription, s.BookDrill, s.BookPlunder, s.BookFortification, s.BookDiscipline));
		}

		public override void NetReceive(BinaryReader reader)
		{
			// Flags only ever turn on, so a late world-data message never un-shows a hint or un-studies a book.
			BitsByte bits = reader.ReadByte();
			SettlementSystem s = SettlementSystem.Instance;
			GuideHintShown |= bits[0];
			s.BookConscription |= bits[1];
			s.BookDrill |= bits[2];
			s.BookPlunder |= bits[3];
			s.BookFortification |= bits[4];
			s.BookDiscipline |= bits[5];
		}
	}
}
