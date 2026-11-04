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
	/// (<see cref="Enrolled"/> and the commander online) this class takes over the NPC's AI: vanilla town behavior is skipped and the officer's avatar (a soldier in
	/// officer mode, SoldierNPC.Officer.cs) drives the NPC instead, while vanilla still draws and animates it. Ending the commission, or the commander leaving, hands
	/// the NPC back to the vanilla AI (<see cref="PreAI"/> sees the avatar gone and calls <see cref="EndPark"/>).
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
		/// <summary>True while an avatar is driving this town NPC as a puppet (it turned off the NPC's gravity and tile collision, which no packet carries). <see cref="PreAI"/> and <see cref="EndPark"/> hand them back. Set on every machine by SoldierNPC.Officer_ParkTownNpc.</summary>
		public bool Parked;

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
		// The avatar (a soldier in officer mode) drives the town NPC every tick (SoldierNPC.Officer_ParkTownNpc: velocity, facing, position) and does the
		// fighting/kit. The town NPC keeps its vanilla update and draw, so it is what the player sees: its stock frames follow the velocity the avatar feeds it.
		// It is kept alive and inert, and the player can still shop with it.
		// =====================================================================================================
		public override bool PreAI(NPC npc)
		{
			if (Enrolled && OfficerSystem.Instance.HasLiveAvatar(npc))
				return false; // skip the vanilla town AI: this NPC is parked. Its velocity is the avatar's (do not zero it: the stock walk/jump frames read it)
			if (Parked) EndPark(npc); // the avatar is gone (dismissed, fell, commander offline): back to stock physics
			return true; // enrolled but no avatar yet (commander offline, or just commissioned), or not an officer: live as a normal civilian
		}

		/// <summary>
		/// Vanilla counts every town NPC near a player (NPC.CheckActive adds its npcSlots to Player.townNPCs) and cuts the spawn rate and the spawn cap as that count
		/// climbs: three of them near a player all but turn enemy spawns off. A commissioned officer is the army, not a civilian, so while a live avatar marches for him he is
		/// left out of that count: fielding officers leaves the spawn rate exactly as it was. (CheckActive does nothing else for a town NPC, which never despawns; he still
		/// draws, shops and chats as himself.) An officer with no avatar (commander offline, off duty) is a plain civilian again and counts as one.
		/// </summary>
		public override bool CheckActive(NPC npc)
		{
			if (Enrolled && OfficerSystem.Instance.HasLiveAvatar(npc)) return false;
			return true;
		}

		/// <summary>Talking to an officer: when the moment has a context (a fight, a boss, a wound, a boss just down) he answers in it; otherwise vanilla's own line for his town voice stands. Client.</summary>
		public override void GetChat(NPC npc, ref string chat)
		{
			if (!Enrolled || Main.dedServ) return;
			string line = OfficerChat.ContextLine(npc);
			if (line != null) chat = line;
		}

		/// <summary>Hands a puppeted town NPC back to the stock town physics. Runs on every machine (the flags it undoes are not synced); a no-op unless an avatar parked it.</summary>
		public void EndPark(NPC npc)
		{
			if (!Parked) return;
			Parked = false;
			npc.noGravity = false;
			npc.noTileCollide = false;
			npc.velocity.X = 0f; // the avatar's march speed is not a civilian's; the town AI picks its own pace from here
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
		// Draw: deliberately no PreDraw. While a live avatar marches, VANILLA draws the parked town NPC like any other (its real frames, party hat, texture
		// packs, shimmer variants); the avatar's own body is invisible and carries only the nameplate (SoldierNPC.Draw.cs).
		// =====================================================================================================

		/// <summary>A small tag over an enrolled officer that is NOT currently deployed (its avatar carries the name while marching); red and marked off duty while he is unpaid.</summary>
		public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (!Enrolled || Main.dedServ) return;
			if (OfficerSystem.Instance.HasLiveAvatar(npc)) return; // the avatar's nameplate shows the given name instead
			// An officer benched for want of pay stays where the town AI leaves him, tagged so it reads "unpaid, off duty" and not "he quit".
			bool offDuty = OfficerSystem.IsSuspended(this);
			string text = LevyUtil.T(offDuty ? "Officers.TagOffDuty" : "Officers.Tag", Slot + 1);
			Vector2 pos = npc.Top - screenPos + new Vector2(0f, -18f);
			Utils.DrawBorderString(spriteBatch, text, pos, offDuty ? new Color(210, 120, 110) : new Color(255, 215, 80), 0.6f, 0.5f, 1f);
		}
	}
}
