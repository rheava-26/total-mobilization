using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// Per-tick driver shell for Levy soldiers carrying a Thorium class weapon. Holds only plain state and touches no ThoriumMod type: when ThoriumMod
	/// is absent (<see cref="ThoriumCompatSystem.Active"/> false) it never attaches to a soldier and never calls anything. When present it hands each
	/// tick to <see cref="ThoriumDriver"/> (the JIT-gated half). Runs after the soldier's own AI, server / single player only.
	/// Also carries what clients need to DRAW the music: a bard's Inspiration (current / max) and which empowerments a soldier is under, synced with the NPC.
	/// </summary>
	public class SoldierThoriumGlobalNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => lateInstantiation && ThoriumCompatSystem.Active && entity.ModNPC is SoldierNPC;

		/// <summary>The Inspiration pool has been filled for the first time (a bard starts with a full pool).</summary>
		public bool BardPoolInit;
		/// <summary>Ticks until this soldier's healer AI may cast again.</summary>
		public int HealCooldown;
		/// <summary>Main.GameUpdateCount until which a healer leaves this soldier alone (a heal is already on its way).</summary>
		public uint HealReservedUntil;

		// ---- synced for display (written by the server, read by every client's draw) ----
		/// <summary>A bard's current / maximum Inspiration (0 max = not a bard).</summary>
		public byte InspCur, InspMax;
		/// <summary>Bit n set = the soldier is under empowerment type n.</summary>
		public uint EmpMask;

		// ---- server only ----
		public const int EmpSlots = 32;
		/// <summary>Level and expiry tick of each empowerment type under which this soldier plays (null until the first one lands).</summary>
		public byte[] EmpLevel; public uint[] EmpUntil;
		/// <summary>Attack-speed bonus the current empowerments give, read by the driver to shorten the weapon's cooldown.</summary>
		public float EmpAttackSpeed;
		/// <summary>Earliest tick this soldier, as a bard, plays to the squad again.</summary>
		public uint EmpNextPlay;
		/// <summary>Tick of the last net update the display values asked for (throttle).</summary>
		public uint LastDisplayNet;
		/// <summary>Earliest tick this bard looks in his Armory for an Inspiration item again.</summary>
		public uint NextFeedTick;

		private bool _warned;

		/// <summary>Current level of an empowerment type on this soldier (0 = none / expired).</summary>
		public byte LevelOf(int type, uint now) => EmpLevel != null && type >= 0 && type < EmpSlots && EmpUntil[type] > now ? EmpLevel[type] : (byte)0;

		/// <summary>Remaining ticks of an empowerment type (0 = none).</summary>
		public uint TicksLeft(int type, uint now) => EmpLevel != null && type >= 0 && type < EmpSlots && EmpUntil[type] > now ? EmpUntil[type] - now : 0u;

		/// <summary>Records an empowerment (type, level, expiry) on this soldier. Returns true when the set of active empowerments or a level changed.</summary>
		public bool SetEmp(int type, byte level, uint until)
		{
			if (type < 0 || type >= EmpSlots) return false;
			if (EmpLevel == null) { EmpLevel = new byte[EmpSlots]; EmpUntil = new uint[EmpSlots]; }
			bool changed = EmpLevel[type] != level || (EmpMask & (1u << type)) == 0;
			EmpLevel[type] = level; EmpUntil[type] = until;
			EmpMask |= 1u << type;
			return changed;
		}

		/// <summary>Drops expired empowerments. Returns true when any lapsed (the stats must be recomputed).</summary>
		public bool Expire(uint now)
		{
			if (EmpMask == 0 || EmpLevel == null) return false;
			bool any = false;
			for (int t = 0; t < EmpSlots; t++)
			{
				if ((EmpMask & (1u << t)) == 0 || EmpUntil[t] > now) continue;
				EmpMask &= ~(1u << t); EmpLevel[t] = 0; any = true;
			}
			if (EmpMask == 0) EmpAttackSpeed = 0f;
			return any;
		}

		public override void PostAI(NPC npc)
		{
			if (!ThoriumCompatSystem.Active) return;
			if (!LevyUtil.IsServerOrSingle) return;
			if (npc.ModNPC is not SoldierNPC s || s.Record == null || !s.IsAlive || s.Dummy == null) return;
			try
			{
				ThoriumDriver.Drive(s, this);
			}
			catch (Exception e)
			{
				// A Thorium surprise must never take down the soldier's tick: report it once per soldier and carry on.
				if (!_warned) { _warned = true; Mod.Logger.Warn($"Levy/Thorium compat: {s.SoldierName} threw in the class driver: {e.Message}"); }
			}
		}

		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
		{
			binaryWriter.Write(InspCur); binaryWriter.Write(InspMax); binaryWriter.Write(EmpMask);
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
		{
			InspCur = binaryReader.ReadByte(); InspMax = binaryReader.ReadByte(); EmpMask = binaryReader.ReadUInt32();
		}

		public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (Main.dedServ || !ThoriumCompatSystem.Active) return;
			if (InspMax == 0 && EmpMask == 0) return;
			if (npc.ModNPC is not SoldierNPC s || s.Record == null || !s.IsAlive) return;
			try { ThoriumDriver.Draw(npc, this, spriteBatch, screenPos); }
			catch (Exception) { /* a draw glitch never matters more than the frame */ }
		}
	}
}
