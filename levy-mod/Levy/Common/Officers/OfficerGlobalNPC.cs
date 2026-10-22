using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Movement;
using Levy.Common.Util;

namespace Levy.Common.Officers
{
	/// <summary>
	/// The commission itself, stored on the town NPC: who commands him, in which slot, which barracks pays him. Saved with the NPC (town NPCs are always saved) and
	/// carried to clients in the NPC's extra-AI block, so every machine agrees on who is an officer without a packet of its own. While a commission is live
	/// (<see cref="Enrolled"/> and the commander online) this class takes over the NPC's AI: vanilla town behavior is skipped and <see cref="OfficerBrain"/> runs on
	/// the server instead. Ending the commission, or the commander leaving, hands the NPC back to the vanilla AI (<see cref="Disengage"/>).
	/// </summary>
	public class OfficerGlobalNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;
		/// <summary>Town NPCs only. lateInstantiation: townNPC is set in SetDefaults, which has run by then.</summary>
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => lateInstantiation && entity.townNPC;

		// ---- the commission (saved + synced) ----
		public bool Enrolled;
		public string Commander = "";
		/// <summary>Command slot, 0-based.</summary>
		public int Slot;
		/// <summary>Top-left tile of the barracks that commissioned (and pays) him. Positions survive saves; tile-entity IDs do not.</summary>
		public short BarX, BarY;
		public byte DaysUnpaid;

		// ---- runtime (never saved) ----
		public OfficerRuntime Rt = new();
		/// <summary>Per-officer kit state created by <see cref="OfficerKit.CreateState"/>. (Since the rework the live kit state lives on the avatar; kept for compatibility.)</summary>
		public object KitState => Rt.KitState;

		/// <summary>Copies of the entity (Bestiary samples, NPC.Clone) get their own runtime so a shared targeting body never crosses wires.</summary>
		public override GlobalNPC Clone(NPC from, NPC to)
		{
			var c = (OfficerGlobalNPC)MemberwiseClone();
			c.Rt = new OfficerRuntime();
			return c;
		}

		public OfficerBody EnsureBody(NPC npc) => Rt.Body ??= new OfficerBody(npc);

		/// <summary>Fresh runtime for a new commission.</summary>
		public void ResetRuntime()
		{
			var body = Rt.Body;
			Rt = new OfficerRuntime { Body = body };
		}

		// =====================================================================================================
		// AI takeover: PARK the real town NPC while a live avatar marches for this commission.
		// The avatar (a soldier in officer mode) co-locates the town NPC with itself every tick and does the fighting/kit;
		// the town NPC is only kept alive, invisible and inert so the player can still shop with it and its name survives.
		// =====================================================================================================
		public override bool PreAI(NPC npc)
		{
			if (!Enrolled) return true;
			if (OfficerSystem.Instance.HasLiveAvatar(npc))
			{
				npc.velocity = Vector2.Zero; // the avatar co-locates it each tick; this only stops stray drift between NPC updates
				return false;                // skip the vanilla town AI: this NPC is parked
			}
			return true; // enrolled but no avatar yet (commander offline, or just commissioned): live as a normal civilian until the avatar marches
		}

		// =====================================================================================================
		// Death: only reachable while the town NPC is NOT parked (parked NPCs are immortal). The commission dies with it.
		// =====================================================================================================
		public override void OnKill(NPC npc)
		{
			if (Enrolled) OfficerSystem.Instance.NotifyFallen(npc, this);
		}

		// =====================================================================================================
		// Save + sync
		// =====================================================================================================
		public override bool NeedSaving(NPC npc) => Enrolled;

		public override void SaveData(NPC npc, TagCompound tag)
		{
			if (!Enrolled) return;
			tag["lvOfficer"] = true;
			tag["cmd"] = Commander ?? "";
			tag["slot"] = Slot;
			tag["bx"] = (int)BarX; tag["by"] = (int)BarY;
			tag["unpaid"] = (int)DaysUnpaid;
		}

		public override void LoadData(NPC npc, TagCompound tag)
		{
			Enrolled = tag.GetBool("lvOfficer");
			if (!Enrolled) return;
			Commander = tag.GetString("cmd");
			Slot = tag.GetInt("slot");
			BarX = (short)tag.GetInt("bx"); BarY = (short)tag.GetInt("by");
			DaysUnpaid = (byte)System.Math.Clamp(tag.GetInt("unpaid"), 0, 255);
			npc.netAlways = true;
		}

		// Two bytes at minimum: a lone flag byte would be dropped by the extra-AI writer (it skips a one-byte block), and the clients would never hear of a dismissal.
		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
		{
			binaryWriter.Write(Enrolled); binaryWriter.Write((byte)0xA5);
			if (!Enrolled) return;
			binaryWriter.Write(Commander ?? ""); binaryWriter.Write((byte)Slot);
			binaryWriter.Write(BarX); binaryWriter.Write(BarY); binaryWriter.Write(DaysUnpaid);
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
		{
			Enrolled = binaryReader.ReadBoolean(); binaryReader.ReadByte();
			if (!Enrolled) { Commander = ""; return; }
			Commander = binaryReader.ReadString(); Slot = binaryReader.ReadByte();
			BarX = binaryReader.ReadInt16(); BarY = binaryReader.ReadInt16(); DaysUnpaid = binaryReader.ReadByte();
		}

		// =====================================================================================================
		// Draw: while a live avatar marches, the town NPC is hidden (the avatar draws its sprite and shows its name).
		// =====================================================================================================
		/// <summary>Hide the parked town NPC: its avatar draws the sprite at the same spot. Returning false skips the vanilla NPC draw.</summary>
		public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (Main.dedServ) return true;
			return !(Enrolled && OfficerSystem.Instance.HasLiveAvatar(npc));
		}

		/// <summary>A small tag over an enrolled officer that is NOT currently deployed (its avatar carries the name while marching).</summary>
		public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (!Enrolled || Main.dedServ) return;
			if (OfficerSystem.Instance.HasLiveAvatar(npc)) return; // the avatar's nameplate shows the given name instead
			string text = LevyUtil.T("Officers.Tag", Slot + 1);
			Vector2 pos = npc.Top - screenPos + new Vector2(0f, -18f);
			Utils.DrawBorderString(spriteBatch, text, pos, new Color(255, 215, 80), 0.6f, 0.5f, 1f);
		}
	}
}
