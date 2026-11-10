using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Targeting;
using Warbands.Common;

namespace Warbands.Content.NPCs
{
	/// <summary>
	/// One hostile soldier of a warband: an enemy NPC dressed in real gear, drawn through the player renderer like a Levy soldier. It walks on vanilla's Fighter AI, which Levy's EnemyTargeting already points at the
	/// player's levies as well as the player (hostile NPC with a Fighter aiStyle), and adds the band behaviour on top: archers and mages hold at range, step back when closed on, jump to juke, and fire real
	/// hostile projectiles at the nearest of the player and the player's levies (EnemyTargeting.Bodies). Stats come from BandStats; loot is a share of the gear it wears.
	/// The full Levy brain (SoldierNPC: weapon use, hooks, formations) is NOT reused yet; see warbands-mod/SEAMS.md for what it needs.
	/// </summary>
	public class BandSoldier : ModNPC
	{
		public byte Biome, StageIdx, MemberIdx; public int BandId;
		private BandMember _member; private BandRoster _roster;
		private int _weaponType, _ammoType, _fireInterval = 60, _shotDamage = 1;
		private int _fireTimer, _engagedUntil, _jukeTimer, _poseUntil; private float _aim;
		private float _projSpeed = 10f; private int _projType = ProjectileID.WoodenArrowHostile;
		private Player _dummy; private bool _drawBroken; private int _gearHash;
		public char Role => _member?.Role ?? 'E';
		public BandRoster Roster => _roster;

		public override string Texture => "Warbands/Content/NPCs/BandSoldier";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 1;
			NPCID.Sets.DontDoHardmodeScaling[Type] = true;
			NPCID.Sets.NPCBestiaryDrawModifiers value = new() { Hide = true };
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, value);
		}

		public override void SetDefaults()
		{
			NPC.width = 20; NPC.height = 42;
			NPC.aiStyle = NPCAIStyleID.Fighter; AIType = NPCID.Zombie; AnimationType = -1;
			NPC.damage = 10; NPC.defense = 0; NPC.lifeMax = 100; NPC.life = 100;
			NPC.knockBackResist = 0.35f; NPC.value = 0f;
			NPC.HitSound = SoundID.PlayerHit; NPC.DeathSound = SoundID.PlayerKilled;
			NPC.npcSlots = 0.5f;
		}

		public override bool CheckActive() => false; // a band stays out until it is beaten or the despawn below fires

		/// <summary>Server: dress, arm and size this soldier from its roster entry.</summary>
		public void Setup(BandRoster roster, int memberIdx, int stage, int bandId)
		{
			Biome = (byte)roster.Biome; StageIdx = (byte)stage; MemberIdx = (byte)memberIdx; BandId = bandId;
			Resolve();
			if (_member == null) return;
			int gear = 0;
			foreach (var a in _member.Armor) { int t = BandData.ItemType(a); if (t > 0) { var it = new Item(); it.SetDefaults(t); gear += it.defense; } }
			NPC.lifeMax = BandStats.Life(stage, Role); NPC.life = NPC.lifeMax;
			NPC.defense = BandStats.Defense(stage, gear);
			NPC.damage = Role == 'E' ? _shotDamage : Math.Max(1, _shotDamage / 3);
			NPC.netUpdate = true;
		}

		private void Resolve()
		{
			_roster = null; _member = null;
			foreach (var r in BandData.All) if ((byte)r.Biome == Biome && r.Stage <= StageIdx && (_roster == null || r.Stage > _roster.Stage)) _roster = r;
			if (_roster == null || MemberIdx >= _roster.Members.Length) return;
			_member = _roster.Members[MemberIdx];
			_weaponType = BandData.ItemType(_member.Weapon); _ammoType = BandData.ItemType(_member.Ammo);
			float weaponHit = 10f, hz = 1f;
			if (_weaponType > 0)
			{
				var w = new Item(); w.SetDefaults(_weaponType);
				weaponHit = w.damage;
				if (_ammoType > 0) { var am = new Item(); am.SetDefaults(_ammoType); weaponHit += am.damage; }
				int ut = Role == 'E' ? Math.Max(1, w.useAnimation) : Math.Max(1, w.useTime);
				// Members shoot slower than a player would: a band is not a drilled marksman.
				_fireInterval = Role == 'E' ? 40 : Math.Clamp((int)(ut * 1.6f), 24, 90);
				hz = 60f / _fireInterval;
				if (Role == 'R') { bool gun = w.useAmmo == AmmoID.Bullet; _projType = gun ? ProjectileID.BulletDeadeye : ProjectileID.WoodenArrowHostile; _projSpeed = gun ? 12f : 10f; }
				else if (Role == 'M') { _projType = ProjectileID.EyeLaser; _projSpeed = 9f; }
			}
			_shotDamage = BandStats.Hit(StageIdx, Role, weaponHit, Role == 'E' ? 1.5f : hz);
		}

		public override void SendExtraAI(BinaryWriter w) { w.Write(Biome); w.Write(StageIdx); w.Write(MemberIdx); w.Write(BandId); }
		public override void ReceiveExtraAI(BinaryReader r) { Biome = r.ReadByte(); StageIdx = r.ReadByte(); MemberIdx = r.ReadByte(); BandId = r.ReadInt32(); Resolve(); }

		// ---------------------------------------------------------------------------------------------------- AI
		public override bool PreAI()
		{
			if (_member == null) { Resolve(); if (_member == null) return true; }
			Player pl = NPC.target >= 0 && NPC.target < Main.maxPlayers + 1 ? Main.player[NPC.target] : null;
			if (pl == null || !pl.active || pl.dead) { NPC.TargetClosest(true); pl = Main.player[NPC.target]; }
			if (!pl.active || pl.dead) { NPC.EncourageDespawn(60); return true; }

			// the nearest of the target the fighter AI chose and every live levy body in reach
			Vector2 aimAt = pl.Center; float best = Vector2.DistanceSquared(NPC.Center, aimAt);
			var bodies = EnemyTargeting.Bodies;
			for (int i = 0; i < bodies.Count; i++)
			{
				var b = bodies[i];
				if (b == null || !b.IsAlive || b.NPC == null || !b.NPC.active) continue;
				float d = Vector2.DistanceSquared(NPC.Center, b.NPC.Center);
				if (d < best) { best = d; aimAt = b.NPC.Center; }
			}
			float dist = (float)Math.Sqrt(best), tiles = dist / 16f;
			NPC.direction = aimAt.X >= NPC.Center.X ? 1 : -1; NPC.spriteDirection = NPC.direction;
			if (Role == 'E' || Main.netMode == NetmodeID.MultiplayerClient) return true;

			float range = Role == 'R' ? 30f : 22f;
			bool los = tiles < range && Collision.CanHitLine(NPC.position, NPC.width, NPC.height, aimAt - new Vector2(4, 4), 8, 8);
			if (_fireTimer > 0) _fireTimer--;
			if (--_jukeTimer <= 0 && NPC.velocity.Y == 0f && tiles < 18f && Main.rand.NextBool(3)) { NPC.velocity.Y = -6.2f; NPC.velocity.X += NPC.direction * 1.5f; _jukeTimer = 70 + Main.rand.Next(90); }
			if (los && _fireTimer <= 0 && TryEngage())
			{
				Vector2 from = NPC.Center - new Vector2(0, 6);
				_aim = (aimAt - from).ToRotation() + Main.rand.NextFloat(-0.07f, 0.07f);
				Vector2 vel = _aim.ToRotationVector2() * _projSpeed;
				int projDamage = Math.Max(1, _shotDamage / (int)Levy.Common.LevyConstants.HostileProjectileDamageMult); // a hostile projectile hurts a player for twice its damage
				Projectile.NewProjectile(NPC.GetSource_FromAI(), from, vel, _projType, projDamage, 1f, Main.myPlayer);
				_fireTimer = _fireInterval; _poseUntil = (int)Main.GameUpdateCount + 14;
				NPC.netUpdate = true;
			}
			bool hold = los && tiles > 8f;
			bool back = tiles < 7f && NPC.velocity.Y == 0f;
			if (hold || back)
			{
				NPC.velocity.X = back ? -NPC.direction * 1.8f : NPC.velocity.X * 0.8f;
				if (NPC.velocity.Y < 10f) NPC.velocity.Y += 0.3f;
				return false;
			}
			return true;
		}

		/// <summary>The band's attack token: only <see cref="BandStats.EngageCap"/> members attack inside one window.</summary>
		private bool TryEngage()
		{
			uint now = Main.GameUpdateCount;
			if (_engagedUntil > now) { _engagedUntil = (int)now + BandStats.EngageWindowTicks; return true; }
			int size = 0, engaged = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (!n.active || n.type != Type || n.ModNPC is not BandSoldier o || o.BandId != BandId) continue;
				size++; if (o._engagedUntil > now) engaged++;
			}
			if (engaged >= BandStats.EngageCap(size)) return false;
			_engagedUntil = (int)now + BandStats.EngageWindowTicks;
			return true;
		}

		// ---------------------------------------------------------------------------------------------------- loot
		public override void OnKill()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || _member == null) return;
			var cfg = WarbandsConfig.Get; float share = (cfg?.LootSharePercent ?? 35f) / 100f;
			void Roll(string name, int stack, float chance) { int t = BandData.ItemType(name); if (t > 0 && Main.rand.NextFloat() < chance) Item.NewItem(NPC.GetSource_Loot(), NPC.Hitbox, t, stack); }
			Roll(_member.Weapon, 1, share);
			if (_member.Ammo != null) Roll(_member.Ammo, 15 + Main.rand.Next(26), Math.Min(1f, share * 1.5f));
			foreach (var a in _member.Armor) Roll(a, 1, share * 0.7f);
			NPC.value = 0f;
		}

		// ---------------------------------------------------------------------------------------------------- draw
		public override bool PreDraw(SpriteBatch sb, Vector2 screenPos, Color drawColor)
		{
			if (Main.dedServ || _member == null || _drawBroken || !NPC.active) return true;
			try
			{
				EnsureDummy();
				int h = HashCode.Combine(_member.Weapon, _member.Armor[0], _member.Armor[1], _member.Armor[2]);
				if (h != _gearHash) { _gearHash = h; Equip(); }
				var d = _dummy;
				d.position = NPC.position; d.velocity = NPC.velocity; d.direction = NPC.direction == 0 ? 1 : NPC.direction; d.gfxOffY = NPC.gfxOffY;
				d.controlLeft = NPC.velocity.X < -0.1f; d.controlRight = NPC.velocity.X > 0.1f; d.controlJump = NPC.velocity.Y != 0f;
				bool firing = Main.GameUpdateCount < _poseUntil;
				d.itemAnimation = firing ? 8 : 0; d.itemAnimationMax = 14; d.itemTime = d.itemAnimation;
				d.itemRotation = firing ? (float)Math.Atan2(Math.Sin(_aim) , Math.Cos(_aim) * d.direction) : 0f;
				d.lastVisualizedSelectedItem = d.inventory[0];
				d.PlayerFrame();
				sb.End();
				try
				{
					sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
					Main.PlayerRenderer.DrawPlayer(Main.Camera, d, new Vector2(d.position.X, d.position.Y + NPC.gfxOffY), d.fullRotation, d.fullRotationOrigin, 0f, 1f);
				}
				finally { sb.End(); sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform); }
				return false;
			}
			catch (Exception e)
			{
				_drawBroken = true;
				Mod.Logger.Warn("[Warbands] drawing a band soldier through the player renderer failed; the placeholder sprite is used: " + e.Message);
				return true;
			}
		}

		private void EnsureDummy()
		{
			if (_dummy != null) return;
			var rng = new Random(BandId * 31 + MemberIdx);
			_dummy = new Player { whoAmI = 255, active = true, direction = 1, difficulty = 0 };
			_dummy.width = 20; _dummy.height = 42;
			_dummy.Male = rng.Next(2) == 0; _dummy.skinVariant = 0; _dummy.hair = 1 + rng.Next(30);
			_dummy.hairColor = new Color(60 + rng.Next(140), 40 + rng.Next(80), 30 + rng.Next(40));
			_dummy.skinColor = new Color(232, 176, 140); _dummy.eyeColor = new Color(60, 40, 30);
			_dummy.shirtColor = new Color(120 + rng.Next(100), 60 + rng.Next(60), 50); _dummy.underShirtColor = new Color(160, 140, 120);
			_dummy.pantsColor = new Color(110, 90, 70); _dummy.shoeColor = new Color(70, 50, 40);
		}

		private void Equip()
		{
			var d = _dummy;
			for (int i = 0; i < 3; i++) { d.armor[i] = new Item(); int t = BandData.ItemType(_member.Armor[i]); if (t > 0) d.armor[i].SetDefaults(t); }
			d.inventory[0] = new Item(); if (_weaponType > 0) d.inventory[0].SetDefaults(_weaponType);
			d.selectedItem = 0;
		}
	}
}
