using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common;
using Levy.Common.Config;
using Levy.Common.Dialogue;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// The body of a soldier: a friendly NPC drawn with the player renderer. This file is the integration point (core);
	/// the brain, movement, combat, fear, damage intake and drawing live in partial files owned by other agents and
	/// plug in through the partial methods declared at the bottom. Logic runs on the server/single player; clients draw.
	/// </summary>
	public partial class SoldierNPC : ModNPC
	{
		public const int BodyWidth = 20, BodyHeight = 42;
		public const int DummySlot = 255; // Main.maxPlayers: the vanilla null-player slot, never a real player

		// ---- bound data ----
		public SoldierRecord Record;
		/// <summary>Free-standing Player used as the render model and the stat engine. Never in Main.player[] except inside EnemyTargeting's swap.</summary>
		public Player Dummy;

		// ---- brain state (set by Brain, read by everyone) ----
		public SoldierState State = SoldierState.Idle;
		public int StateTimer;
		public int TargetIndex = -1;
		public bool HasMoveGoal; public Vector2 MoveGoal;
		public bool WantsToFallThrough;
		public bool HoldPosition;           // Hold order: engage in place
		public int ThinkOffset;

		// ---- combat state (set by Combat) ----
		public WeaponProfile Weapon;        // profile of the item in hand (weapon or sidearm), null = unarmed
		public bool UsingSidearm;
		public float AimRotation;
		public int AttackCooldown;
		/// <summary>Held-item animation driven by Combat (server) and mirrored on clients: ticks remaining / total of the current use.</summary>
		public int ItemAnimation, ItemAnimationMax;
		/// <summary>Movement abilities read from gear (Movement agent fills via MovementAbilities.FromDummy after RefreshStats).</summary>
		public Common.Movement.MovementAbilities Abilities = new();
		public int Mana => Record?.Mana ?? 0;

		// ---- damage state (set by Damage) ----
		public int ImmuneTime;
		public int TicksSinceDamaged = 100000;
		public int PotionSickness;
		public int LastAttackerIndex = -1, LastAttackerType = -1;
		public string LastAttackerName = "";
		private bool _deathHandled;

		// ---- stats (RefreshStats) ----
		public int Defense;
		/// <summary>Flat defense bonus hook (Flavor: +4 near a barracks with A Guide to Fortification). Null = none.</summary>
		public static Func<SoldierNPC, int> ExtraDefense;
		/// <summary>Tick of the last melee hit this soldier landed (Combat sets; Brain steps back after it).</summary>
		public int LastMeleeHitTick = -1000;
		public float MoveSpeedMult = 1f;
		public bool StatsDirty = true;
		private int _statsTimer;
		private static bool _pipelineBroken;

		// ---- fear (meter on the record; helpers here) ----
		public float Fear { get => Record?.Fear ?? 0f; set { if (Record != null) Record.Fear = MathHelper.Clamp(value, 0f, 100f); } }
		public bool Shaken => Fear >= LevyConstants.Fear.ShakenThreshold;
		public bool IsRouted => State == SoldierState.Routed;

		// ---- convenience ----
		public bool IsMilitia => Record != null && Record.Kind == SoldierKind.Militia;
		public bool IsRegular => Record != null && Record.Kind == SoldierKind.Regular;
		public int CommanderIndex => Record?.CommanderIndex ?? -1;
		public Player Commander => CommanderIndex >= 0 && CommanderIndex < Main.maxPlayers && Main.player[CommanderIndex].active ? Main.player[CommanderIndex] : null;
		public BarracksTE Home => SettlementSystem.Instance.GetBarracks(Record?.HomeBarracksId ?? -1);
		public string SoldierName => Record?.Name ?? "Soldier";
		public Grade Grade => Record?.Grade ?? Grade.Levy;
		public float DamageMultiplier => LevyConstants.GradeDamageMult[(int)Grade] * LevyConfig.Get.SoldierDamageMultiplier * (Shaken ? LevyConstants.ShakenDamageMult : 1f);
		/// <summary>
		/// Owner index for projectiles the soldier spawns. Must be Main.myPlayer: Projectile.NewProjectile only assigns ai[], sends the
		/// spawn packet and runs NPC hit detection when owner == Main.myPlayer (255 on a dedicated server). Loot credit is separate: see <see cref="CreditPlayer"/>.
		/// </summary>
		public int ProjectileOwner => Main.myPlayer;
		/// <summary>Player index that receives loot/banner credit for this soldier's kills (commander, else the local player in single player, else -1).</summary>
		public int CreditPlayer => CommanderIndex >= 0 ? CommanderIndex : (Main.netMode == NetmodeID.SinglePlayer ? Main.myPlayer : -1);
		public bool IsAlive => NPC.active && NPC.life > 0 && !_deathHandled;

		public override string Texture => "Levy/Content/NPCs/SoldierNPC";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 1;
			NPCID.Sets.ActsLikeTownNPC[Type] = true;
			NPCID.Sets.NoTownNPCHappiness[Type] = true;
			NPCID.Sets.CantTakeLunchMoney[Type] = true;
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
			NPCID.Sets.ImmuneToRegularBuffs[Type] = false;
		}

		public override void SetDefaults()
		{
			NPC.width = BodyWidth; NPC.height = BodyHeight;
			NPC.friendly = true; NPC.townNPC = false;
			NPC.aiStyle = -1;
			NPC.damage = 0; NPC.defense = 0;
			NPC.lifeMax = LevyConstants.BaseHp; NPC.life = NPC.lifeMax;
			NPC.knockBackResist = 0f;
			NPC.npcSlots = 1f;
			NPC.HitSound = SoundID.PlayerHit; NPC.DeathSound = SoundID.PlayerKilled;
			NPC.lavaImmune = false; NPC.noGravity = false; NPC.noTileCollide = false;
			NPC.dontTakeDamageFromHostiles = false; NPC.immortal = false; NPC.dontTakeDamage = false;
			NPC.rarity = 0; NPC.value = 0f;
			NPC.scale = 1f;
			NPC.netAlways = true;
			EnsureDummy();
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) { }

		/// <summary>Attach a record to this body (server; clients receive a visual copy via ReceiveExtraAI).</summary>
		public void Bind(SoldierRecord record)
		{
			Record = record;
			record.BodyIndex = NPC.whoAmI;
			record.Dead = false;
			record.CommanderIndex = LevyUtil.FindPlayerByName(record.CommanderName);
			NPC.lifeMax = Math.Max(1, record.MaxHp);
			NPC.life = Math.Clamp(record.CurrentHp <= 0 ? NPC.lifeMax : record.CurrentHp, 1, NPC.lifeMax);
			NPC.GivenName = record.Name;
			ThinkOffset = NPC.whoAmI % LevyConstants.ThinkIntervalTicks;
			State = SoldierState.Idle; StateTimer = 0; TargetIndex = -1; HasMoveGoal = false; _deathHandled = false;
			HoldPosition = record.Order == SoldierOrder.Hold;
			EnsureDummy();
			ApplyLooks();
			StatsDirty = true;
			Brain_OnBound();
			Combat_OnGearChanged();
			NPC.netUpdate = true;
		}

		private void EnsureDummy()
		{
			if (Dummy != null) return;
			Dummy = new Player();
			Dummy.whoAmI = DummySlot;
			Dummy.active = true; Dummy.dead = false; Dummy.ghost = false;
			Dummy.width = BodyWidth; Dummy.height = BodyHeight;
			Dummy.direction = 1;
			Dummy.selectedItem = 0;
			Dummy.difficulty = 0;
		}

		private void ApplyLooks()
		{
			if (Record == null) return;
			Dummy.name = Record.Name;
			Dummy.Male = Record.Male; Dummy.skinVariant = Record.SkinVariant; Dummy.hair = Record.Hair;
			Dummy.hairColor = Record.HairColor; Dummy.skinColor = Record.SkinColor; Dummy.eyeColor = Record.EyeColor;
			Dummy.shirtColor = Record.ShirtColor; Dummy.underShirtColor = Record.UnderShirtColor; Dummy.pantsColor = Record.PantsColor; Dummy.shoeColor = Record.ShoeColor;
		}

		/// <summary>Copies gear into the dummy's equipment arrays (references, not clones).</summary>
		private Item _blankSlot;
		public void EquipDummy()
		{
			if (Record == null) return;
			var g = Record.Gear;
			// One shared air item fills the unused armor/dye slots; vanilla equip code only reads these slots, never writes them.
			_blankSlot ??= new Item();
			for (int i = 0; i < Dummy.armor.Length; i++) Dummy.armor[i] = _blankSlot;
			for (int i = 0; i < Dummy.dye.Length; i++) Dummy.dye[i] = _blankSlot;
			Dummy.armor[0] = g.Head; Dummy.armor[1] = g.Body; Dummy.armor[2] = g.Legs;
			int slots = Record.AccessorySlots;
			for (int i = 0; i < SoldierGear.AccessorySlots && i < slots; i++) Dummy.armor[3 + i] = g.Accessories[i];
			for (int i = 0; i < 3; i++) Dummy.dye[i] = g.Dyes[i];
			for (int i = 0; i < SoldierGear.AccessorySlots && i < slots; i++) Dummy.dye[3 + i] = g.Dyes[3 + i];
			Dummy.inventory[0] = UsingSidearm && g.HasSidearm ? g.Sidearm : (g.HasWeapon ? g.Weapon : (_blankSlot ??= new Item()));
			Dummy.selectedItem = 0;
			Dummy.statLifeMax = Dummy.statLifeMax2 = Math.Max(1, Record.MaxHp);
			Dummy.statManaMax = Dummy.statManaMax2 = Record.MaxMana;
		}

		/// <summary>Recomputes defense/damage/movement from gear through the dummy's vanilla stat pipeline (with a manual fallback).</summary>
		public void RefreshStats()
		{
			StatsDirty = false;
			if (Record == null) return;
			EquipDummy();
			bool ok = false;
			if (LevyConfig.Get.UseVanillaStatPipeline && !_pipelineBroken)
			{
				try
				{
					Dummy.position = NPC.position; Dummy.velocity = NPC.velocity;
					Dummy.ResetEffects();
					Dummy.UpdateEquips(DummySlot);
					Dummy.UpdateArmorSets(DummySlot);
					Defense = Dummy.statDefense;
					MoveSpeedMult = Math.Max(0.5f, Dummy.moveSpeed);
					Abilities = Common.Movement.MovementAbilities.FromDummy(Dummy);
					ok = true;
				}
				catch (Exception e)
				{
					_pipelineBroken = true;
					Levy.Instance.Logger.Warn("Levy: vanilla stat pipeline failed on a soldier, falling back to manual stats: " + e);
				}
			}
			if (!ok)
			{
				int def = 0; foreach (var it in Record.Gear.AllItems()) if (!it.accessory || true) def += it.defense;
				Defense = def; MoveSpeedMult = 1f; Abilities = new Common.Movement.MovementAbilities();
			}
			if (ExtraDefense != null) { try { Defense += ExtraDefense(this); } catch { } }
			NPC.defense = Defense;
			int maxHp = Math.Max(1, Record.MaxHp);
			if (NPC.lifeMax != maxHp) { NPC.life = (int)Math.Clamp(NPC.life * (float)maxHp / Math.Max(1, NPC.lifeMax), 1, maxHp); NPC.lifeMax = maxHp; }
		}

		/// <summary>Weapon damage after the dummy's stat pipeline (crit excluded). Manual fallback uses item damage.</summary>
		public int WeaponDamage(Item item)
		{
			if (item == null || item.IsAir) return 0;
			if (!_pipelineBroken && LevyConfig.Get.UseVanillaStatPipeline)
			{
				try { Dummy.inventory[0] = item; return Math.Max(1, Dummy.GetWeaponDamage(item)); } catch { _pipelineBroken = true; }
			}
			return Math.Max(1, item.damage);
		}

		/// <summary>Keeps the dummy where the body is. Called every tick before Draw/partials run.</summary>
		public void SyncDummyTransform()
		{
			Dummy.position = NPC.position; Dummy.velocity = NPC.velocity;
			Dummy.direction = NPC.direction == 0 ? 1 : NPC.direction;
			Dummy.gravDir = 1f; Dummy.active = true; Dummy.dead = false; Dummy.ghost = false;
			Dummy.statLife = NPC.life; Dummy.statLifeMax2 = Math.Max(1, NPC.lifeMax);
			Dummy.whoAmI = DummySlot;
		}

		public override void AI()
		{
			if (Record == null) { if (LevyUtil.IsServerOrSingle) { NPC.active = false; NPC.netUpdate = true; } return; }
			EnsureDummy();
			SyncDummyTransform();
			if (LevyUtil.IsClient)
			{
				// clients only animate; the server owns the brain. Mirror the held-item animation countdown so swings look smooth
				// between net updates (SendExtraAI carries ItemAnimation/ItemAnimationMax on every netUpdate).
				if (ItemAnimation > 0) ItemAnimation--;
				if (AttackCooldown > 0) AttackCooldown--;
				Movement_ClientAnimate();
				return;
			}
			if (StatsDirty || ++_statsTimer >= 60) { _statsTimer = 0; RefreshStats(); }
			Damage_Update();
			if (!IsAlive) return;
			if ((int)Main.GameUpdateCount % LevyConstants.ThinkIntervalTicks == ThinkOffset) Brain_Think();
			Fear_Update();
			Movement_Update();
			Combat_Update();
			Record.CurrentHp = NPC.life;
			SyncDummyTransform();
		}

		public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
		{
			// NPC defense is half as effective as player defense in Classic; soldiers use the player's per-mode value.
			modifiers.DefenseEffectiveness *= LevyUtil.ModeDefenseEffectiveness() / 0.5f;
		}

		public override bool CheckDead()
		{
			if (_deathHandled) return true;
			_deathHandled = true;
			if (LevyUtil.IsServerOrSingle) KillAnchoredProjectiles(); // minions, lashes and boomerangs go with their soldier
			if (LevyUtil.IsServerOrSingle && Record != null)
			{
				Record.CurrentHp = 0;
				RosterSystem.Instance.OnBodyDied(this, LastAttackerType, LastAttackerName);
			}
			return true;
		}

		public override bool PreKill() => false; // no vanilla loot; the roster handles drops
		public override bool CheckActive() => false; // never despawn by distance
		public override bool? CanFallThroughPlatforms() => WantsToFallThrough ? true : null;
		public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
		public override bool? CanBeHitByItem(Player player, Item item) => false;
		public override bool? CanBeHitByProjectile(Projectile projectile) => projectile.friendly && !projectile.hostile ? false : null;
		public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;
		public override bool NeedSaving() => false;
		public override bool UsesPartyHat() => false;

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (Record == null) return false;
			bool drawn = false;
			Draw_PreDraw(spriteBatch, screenPos, drawColor, ref drawn);
			return !drawn; // if nothing drew the body, fall back to the placeholder texture
		}

		public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (Record == null) return;
			Draw_PostDraw(spriteBatch, screenPos, drawColor);
		}

		// ---- chat ----
		public override bool CanChat() => Record != null && IsRegular && State != SoldierState.Engage && State != SoldierState.Routed;
		public override string GetChat() => DialogueSystem.ChatLine(this);
		public override void SetChatButtons(ref string button, ref string button2)
		{
			button = LevyUtil.T("UI.ChatOrders");
			if (AcceptsAmmo(Main.LocalPlayer.HeldItem)) button2 = LevyUtil.T("UI.ChatHandAmmo");
		}

		/// <summary>True when this soldier's ranged weapon can actually use the held ammo (not gel, coins, or the wrong ammo id).</summary>
		public bool AcceptsAmmo(Item stack)
		{
			if (stack == null || stack.IsAir || stack.ammo <= 0 || stack.IsACoin) return false;
			var w = Record?.Gear.Weapon;
			if (w == null || w.IsAir || w.useAmmo <= 0) return false;
			return stack.ammo == w.useAmmo;
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton) { Common.UI.UISystem.Instance.ToggleRadial(); return; }
			var held = Main.LocalPlayer.HeldItem;
			if (!AcceptsAmmo(held)) return;
			int type = held.type, count = held.stack;
			held.TurnToAir(); // the handing client consumes first (server-authoritative add), so the stack is never duplicated
			if (LevyUtil.IsClient) { var p = Common.Net.LevyPackets.New(Common.Net.LevyPacket.HandAmmo); p.Write(NPC.whoAmI); p.Write(type); p.Write(count); p.Send(); }
			else { int left = GiveAmmo(type, count); if (left > 0) Main.LocalPlayer.QuickSpawnItem(Main.LocalPlayer.GetSource_Misc("Levy:HandAmmo"), type, left); }
		}

		/// <summary>Adds up to <paramref name="count"/> of ammo type to the pouch (server). Returns the amount that did not fit or did not match the weapon.</summary>
		public int GiveAmmo(int type, int count)
		{
			if (Record == null || type <= 0 || type >= ItemLoader.ItemCount || count <= 0) return count;
			var w = Record.Gear.Weapon;
			if (w == null || w.IsAir || w.useAmmo <= 0 || ContentSamples.ItemsByType[type].ammo != w.useAmmo) return count;
			var pouch = Record.Gear.Ammo;
			if (!pouch.IsAir && pouch.type != type) return count;
			int room = LevyConstants.AmmoPouchMax - (pouch.IsAir ? 0 : pouch.stack);
			int take = Math.Min(room, count); if (take <= 0) return count;
			if (pouch.IsAir) { pouch.SetDefaults(type); pouch.stack = take; } else pouch.stack += take;
			Combat_OnGearChanged();
			return count - take;
		}

		/// <summary>Gear or grade changed from outside (promotion, crystals, armory issue): re-read the weapon profile and stats.</summary>
		public void NotifyGearChanged() { StatsDirty = true; Combat_OnGearChanged(); NPC.netUpdate = true; }

		/// <summary>Orders arrive here (server) from the roster.</summary>
		public void ReceiveOrder(SoldierOrder order)
		{
			if (Record == null) return;
			Record.Order = order; HoldPosition = order == SoldierOrder.Hold;
			if (order == SoldierOrder.Hold) Record.HoldPos = NPC.Center;
			Brain_OnOrder(order);
			NPC.netUpdate = true;
		}

		// ---- net sync: clients need enough to draw and label ----
		public override void SendExtraAI(BinaryWriter writer)
		{
			bool has = Record != null; writer.Write(has); if (!has) return;
			writer.Write(Record.Id); writer.Write(Record.Name ?? ""); writer.Write((byte)Record.Kind); writer.Write((byte)Record.Grade); writer.Write((byte)Record.Temperament); writer.Write(Record.Veteran);
			writer.Write((byte)State); writer.Write((short)TargetIndex); writer.Write(Record.Fear); writer.Write(UsingSidearm); writer.Write(AimRotation); writer.Write((short)ItemAnimation); writer.Write((short)ItemAnimationMax);
			writer.Write(Record.Hair); writer.Write(Record.SkinVariant); writer.Write(Record.Male);
			writer.WriteRGB(Record.HairColor); writer.WriteRGB(Record.SkinColor); writer.WriteRGB(Record.EyeColor); writer.WriteRGB(Record.ShirtColor); writer.WriteRGB(Record.UnderShirtColor); writer.WriteRGB(Record.PantsColor); writer.WriteRGB(Record.ShoeColor);
			writer.Write(Record.CommanderName ?? ""); writer.Write(Record.HomeBarracksId);
			Record.Gear.WriteVisual(writer);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			if (!reader.ReadBoolean()) { Record = null; return; }
			Record ??= new SoldierRecord();
			Record.Id = reader.ReadInt32(); Record.Name = reader.ReadString(); Record.Kind = (SoldierKind)reader.ReadByte(); Record.Grade = (Grade)reader.ReadByte(); Record.Temperament = (Temperament)reader.ReadByte(); Record.Veteran = reader.ReadBoolean();
			int prevAnim = ItemAnimation;
			State = (SoldierState)reader.ReadByte(); TargetIndex = reader.ReadInt16(); Record.Fear = reader.ReadSingle(); UsingSidearm = reader.ReadBoolean(); AimRotation = reader.ReadSingle(); ItemAnimation = reader.ReadInt16(); ItemAnimationMax = reader.ReadInt16();
			bool newUse = ItemAnimation > 0 && ItemAnimation == ItemAnimationMax && prevAnim != ItemAnimation;
			Record.Hair = reader.ReadInt32(); Record.SkinVariant = reader.ReadInt32(); Record.Male = reader.ReadBoolean();
			Record.HairColor = reader.ReadRGB(); Record.SkinColor = reader.ReadRGB(); Record.EyeColor = reader.ReadRGB(); Record.ShirtColor = reader.ReadRGB(); Record.UnderShirtColor = reader.ReadRGB(); Record.PantsColor = reader.ReadRGB(); Record.ShoeColor = reader.ReadRGB();
			Record.CommanderName = reader.ReadString(); Record.HomeBarracksId = reader.ReadInt32();
			Record.Gear.ReadVisual(reader);
			Record.BodyIndex = NPC.whoAmI; Record.CommanderIndex = LevyUtil.FindPlayerByName(Record.CommanderName);
			NPC.GivenName = Record.Name;
			EnsureDummy(); ApplyLooks(); EquipDummy();
			if (newUse && !Main.dedServ)
			{
				var held = Dummy.inventory[0];
				if (held != null && !held.IsAir && held.UseSound.HasValue) Terraria.Audio.SoundEngine.PlaySound(held.UseSound.Value, NPC.Center);
			}
		}

		// =====================================================================================================
		// Partial method contracts. Each is implemented in exactly one other file (see PLAN.md §4). Unimplemented
		// parts compile to nothing, so every agent can build in isolation; integration greps that all exist.
		// =====================================================================================================
		partial void Brain_Think();                          // SoldierNPC.Brain.cs   — every 6 ticks, staggered
		partial void Brain_OnOrder(SoldierOrder order);      // SoldierNPC.Brain.cs
		partial void Brain_OnBound();                        // SoldierNPC.Brain.cs   — record just bound (spawn/respawn)
		partial void Movement_Update();                      // SoldierNPC.Movement.cs — every tick (server)
		partial void Movement_ClientAnimate();               // SoldierNPC.Movement.cs — every tick (client): leg/jump animation flags only
		partial void Movement_OnTeleported();                // SoldierNPC.Movement.cs
		partial void Combat_Update();                        // SoldierNPC.Combat.cs  — every tick (server)
		partial void Combat_OnGearChanged();                 // SoldierNPC.Combat.cs  — re-read Weapon profile, StatsDirty = true
		partial void Fear_Update();                          // SoldierNPC.Fear.cs    — every tick (server)
		partial void Fear_OnDamaged(int damage, int attackerIndex); // SoldierNPC.Fear.cs
		partial void Damage_Update();                        // SoldierNPC.Damage.cs  — every tick (server): immunity, contact damage, regen, potions
		partial void Draw_PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, ref bool drawn); // SoldierNPC.Draw.cs
		partial void Draw_PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor);               // SoldierNPC.Draw.cs
	}
}
