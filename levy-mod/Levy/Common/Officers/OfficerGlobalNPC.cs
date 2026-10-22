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
		/// <summary>Per-officer kit state created by <see cref="OfficerKit.CreateState"/>.</summary>
		public object KitState => Rt.KitState;
		private bool _engaged;

		/// <summary>Copies of the entity (Bestiary samples, NPC.Clone) get their own runtime; sharing the brain state between two NPCs would cross their wires.</summary>
		public override GlobalNPC Clone(NPC from, NPC to)
		{
			var c = (OfficerGlobalNPC)MemberwiseClone();
			c.Rt = new OfficerRuntime();
			c._engaged = false;
			return c;
		}

		public OfficerBody EnsureBody(NPC npc) => Rt.Body ??= new OfficerBody(npc);

		/// <summary>Fresh runtime for a new commission (brain timers cleared, the kit's state recreated).</summary>
		public void ResetRuntime()
		{
			var body = Rt.Body;
			Rt = new OfficerRuntime { Body = body };
			_engaged = false;
		}

		// =====================================================================================================
		// AI takeover
		// =====================================================================================================
		public override bool PreAI(NPC npc)
		{
			if (!Enrolled) { if (_engaged) Disengage(npc); return true; }
			if (!OfficerRegistry.TryGet(npc.type, out OfficerDef def)) { if (_engaged) Disengage(npc); return true; } // Housekeeping clears the orphan on the server
			int ci = LevyUtil.FindPlayerByName(Commander);
			if (ci < 0) { if (_engaged) Disengage(npc); return true; } // commander offline: the town gets him back until they return
			if (!_engaged) { _engaged = true; if (Rt.KitState == null) Rt.KitState = def.Kit?.CreateState(); }
			OfficerBody body = EnsureBody(npc);
			body.SyncDummyTransform();
			// Clients run no brain: the server streams the NPC every tick (netAlways) and the vanilla frame code animates it from velocity / ai[0].
			if (LevyUtil.IsClient) return false;
			OfficerBrain.Tick(npc, this, def, Main.player[ci]);
			return false;
		}

		/// <summary>
		/// Hands the NPC back to the vanilla town AI: clears the AI state the brain borrowed (ai[0] doubles as the vanilla animation state), stops the per-tick
		/// stream and, on the server, walks a stray far from his house home.
		/// </summary>
		public void Disengage(NPC npc)
		{
			_engaged = false;
			if (npc == null || !npc.active) return;
			npc.ai[0] = 0f; npc.ai[1] = 60f; npc.ai[2] = 0f; npc.ai[3] = 0f;
			npc.localAI[3] = 0f; npc.frameCounter = 0.0;
			npc.velocity.X = 0f;
			npc.netAlways = false;
			Rt.Reset();
			if (LevyUtil.IsServerOrSingle)
			{
				SnapHome(npc);
				npc.netUpdate = true;
			}
		}

		/// <summary>A dismissed officer far from his house is placed beside it; the vanilla AI would otherwise wander at his post until night (and then teleport him anyway).</summary>
		private static void SnapHome(NPC npc)
		{
			if (npc.homeless || npc.homeTileX <= 0 || npc.homeTileY <= 0) return;
			Vector2 home = new Vector2(npc.homeTileX * 16f + 8f, npc.homeTileY * 16f);
			if (LevyUtil.WithinTiles(npc.Center, home, LevyConstants.Officer.HomeSnapDistTiles)) return;
			if (!FollowerTravel.FindStandingSpot(home, LevyConstants.Officer.SpotRadiusTiles, out Vector2 spot)) return;
			OfficerBrain.Place(npc, spot);
		}

		// =====================================================================================================
		// Death
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
		// Draw: a small tag so the player can tell officers from civilians
		// =====================================================================================================
		public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (!Enrolled || Main.dedServ) return;
			string text = LevyUtil.T("Officers.Tag", Slot + 1);
			Vector2 pos = npc.Top - screenPos + new Vector2(0f, -18f);
			Utils.DrawBorderString(spriteBatch, text, pos, new Color(255, 215, 80), 0.6f, 0.5f, 1f);
		}
	}
}
