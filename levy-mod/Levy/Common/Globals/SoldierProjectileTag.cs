using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Levy.Common.Globals
{
	/// <summary>
	/// Per-projectile tag shared by Weapons (sets SoldierIndex on soldier shots), Targeting/LootCredit (reads it),
	/// Boss rule (reads SourceNpcIndex on hostile boss shots) and Damage (killer attribution).
	/// </summary>
	public class SoldierProjectileTag : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		/// <summary>NPC whoAmI of the soldier that fired this projectile, or -1.</summary>
		public int SoldierIndex = -1;
		/// <summary>NPC type/index that spawned this (hostile) projectile, from the entity source, or -1.</summary>
		public int SourceNpcType = -1, SourceNpcIndex = -1;
		/// <summary>Set by the boss rule when this projectile has been redirected at a spite target.</summary>
		public bool Redirected;

		public bool IsSoldierShot => SoldierIndex >= 0;

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (source is EntitySource_Parent { Entity: NPC npc })
			{
				SourceNpcType = npc.type; SourceNpcIndex = npc.whoAmI;
			}
		}

		/// <summary>
		/// Terra Blade's beam (AI_191) switches ownerHitCheck on for its first 6 ticks, which gates every hit on a line of sight from Main.player[owner] (the commander)
		/// to the enemy. A soldier's beam starts at the soldier, so that check is turned back off.
		/// </summary>
		public override void PostAI(Projectile projectile)
		{
			if (SoldierIndex >= 0 && projectile.type == ProjectileID.TerraBlade2Shot) projectile.ownerHitCheck = false;
		}

		public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
		{
			binaryWriter.Write((short)SoldierIndex); binaryWriter.Write((short)SourceNpcIndex);
		}
		public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
		{
			SoldierIndex = binaryReader.ReadInt16(); SourceNpcIndex = binaryReader.ReadInt16();
			if (SourceNpcIndex >= 0 && SourceNpcIndex < Main.maxNPCs) SourceNpcType = Main.npc[SourceNpcIndex].type;
		}
	}
}
