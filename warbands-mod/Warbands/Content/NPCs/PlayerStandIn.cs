using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Targeting;
using Levy.Common.Potions;

namespace Warbands.Content.NPCs
{
	/// <summary>
	/// A player, as a hostile soldier sees one. Levy's brain picks, paths to and aims at targets by NPC index, so each player gets an invisible NPC that sits exactly on them (position and velocity copied every tick).
	/// It cannot be hurt and nothing else targets it. A band member's blade that lands on it hurts the real player (<see cref="HostileMeleeHit"/>); its arrows and bolts are hostile projectiles and hit the player directly.
	/// Only exists while a band is out (WarbandsSystem).
	/// </summary>
	public class PlayerStandIn : ModNPC, IHostileSideTarget
	{
		public int PlayerIndex { get => (int)NPC.ai[0]; set => NPC.ai[0] = value; }
		public Player Who => PlayerIndex >= 0 && PlayerIndex < Main.maxPlayers ? Main.player[PlayerIndex] : null;

		public override string Texture => "Terraria/Images/NPC_0";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 1;
			NPCID.Sets.NPCBestiaryDrawModifiers value = new() { Hide = true };
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, value);
			NPCID.Sets.DontDoHardmodeScaling[Type] = true;
			NPCID.Sets.CantTakeLunchMoney[Type] = true;
		}

		public override void SetDefaults()
		{
			NPC.width = 20; NPC.height = 42;
			NPC.friendly = true; NPC.townNPC = false; NPC.aiStyle = -1;
			NPC.damage = 0; NPC.defense = 0; NPC.lifeMax = 1000; NPC.life = 1000;
			NPC.dontTakeDamage = true; NPC.dontTakeDamageFromHostiles = true; NPC.immortal = true; NPC.dontCountMe = true;
			NPC.noGravity = true; NPC.noTileCollide = true; NPC.knockBackResist = 0f;
			NPC.npcSlots = 0f; NPC.value = 0f; NPC.hide = false; NPC.netAlways = false;
		}

		public override bool CheckActive() => false;
		public override bool? CanBeHitByItem(Player player, Item item) => false;
		public override bool? CanBeHitByProjectile(Projectile projectile) => false;
		public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
		public override bool NeedSaving() => false;
		public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;
		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => false;

		/// <summary>False while the player is dead, a ghost, gone, or a Spectator (a Spectator cannot be targeted).</summary>
		public bool IsAlive
		{
			get
			{
				Player p = Who;
				return NPC.active && p != null && p.active && !p.dead && !p.ghost && !SpectatorPlayer.IsSpectator(p);
			}
		}

		public override void AI()
		{
			Player p = Who;
			if (p == null || !p.active) { if (Main.netMode != NetmodeID.MultiplayerClient) NPC.active = false; return; }
			NPC.width = p.width; NPC.height = p.height;
			NPC.Center = p.Center; NPC.velocity = p.velocity; NPC.gfxOffY = 0f;
			NPC.life = NPC.lifeMax;
			NPC.target = PlayerIndex;
		}

		/// <summary>A hostile soldier's blade swing reached this player: the player takes the hit (immune frames and defense as for any enemy hit).</summary>
		public void HostileMeleeHit(NPC attacker, int damage, int dir)
		{
			Player p = Who;
			if (!IsAlive || Main.netMode == NetmodeID.MultiplayerClient) return;
			var reason = PlayerDeathReason.ByNPC(attacker.whoAmI);
			if (Main.netMode == NetmodeID.Server) { p.Hurt(reason, damage, dir, out Player.HurtInfo info); NetMessage.SendPlayerHurt(p.whoAmI, info); }
			else p.Hurt(reason, damage, dir);
		}
	}
}
