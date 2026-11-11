using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Vanity;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Targeting;
using Levy.Common.Movement;
using Levy.Content.NPCs;
using Warbands.Common;

namespace Warbands.Content.NPCs
{
	/// <summary>
	/// A band member that runs the REAL Levy soldier brain (movement with hooks, wings and dodging; weapon use; the firing-spot search and standoff) on the enemy side. Everything Levy-specific is the seam in
	/// levy-mod SoldierNPC.Hostile.cs: this class sets <see cref="SoldierNPC.Hostile"/>, supplies a synthetic record from the band roster (never in Levy's roster), and decides what a band wants (HostileThink):
	/// the warning, the attack token and rotating aggro, tactics by the foe's class, morale, the Recall potion, chatter. Stats come from BandStats; damage intake is vanilla NPC intake (no player i-frames).
	/// </summary>
	public class BandLevyNPC : SoldierNPC
	{
		public int BandId; public byte TypeIdx, MemberIdx, StageIdx;
		public BandRoster Roster => BandTypes.FromIndex(TypeIdx);
		public BandMember Member => Roster != null && MemberIdx < Roster.Members.Length ? Roster.Members[MemberIdx] : null;
		public char Role => Member?.Role ?? 'E';
		public int Group => MemberIdx & 1;
		/// <summary>Event bands: the sturdier named member that drops the event's loot.</summary>
		public bool IsCaptain => Member?.Captain == true;
		public const float CaptainLifeMult = 1.6f;
		private uint _skWithdrawUntil, _skNextAt, _hopAt; private int _leaveDir;
		public uint EngagedUntil;
		public float Morale = 100f;
		public string ShortName = "";
		public bool Panic, Recalling, HasRecall;
		private uint _recallAt, _recallDone, _lastHitTick; private int _lastLife = -1, _lastBlow, _lifeBeforeBlow;
		private float _shotScale = 1f, _budgetHit = 1f; private bool _oneTapped;
		private int _chip; private uint _classTalkAt;
		public static int Vanished, PanicEntries, RecallDrinks;

		/// <summary>NPC.rarity of every band soldier (vanilla rare creatures are 1 to 5): above zero is what the Lifeform Analyzer lists.</summary>
		public const int BandRarity = 2;
		public override bool Hostile => true;
		public override string HostilePlateTag => Roster?.DisplayName;
		public Vector2 PostureSpot; private uint _postureAt, _postureUntil, _clumpSince, _loiterAt; private Vector2 _loiterGoal; private bool _bantered, _passiveNet;
		private BandBrain MyBand => BandBrain.Get(BandId);
		/// <summary>A neutral band that nobody has provoked: the player's side leaves it alone (Levy's targeting reads this).</summary>
		public override bool HostilePassive { get { var b = MyBand; return b != null && b.Neutral && !b.Provoked; } }
		public override Color HostilePlateColor => HostilePassive ? new Color(232, 200, 120) : HostilePlateRed;
		private static readonly Dictionary<(int, char), VanityLook> _looks = new();
		/// <summary>The band's vanity over its gear: its mask or set, a robe for mages. Null keeps the real armor.</summary>
		public override VanityLook HostileVanity()
		{
			var r = Roster; if (r?.Vanity == null) return null;
			var key = (TypeIdx, Role);
			if (_looks.TryGetValue(key, out var hit)) return hit;
			var look = new VanityLook();
			string[] v = r.Vanity;
			string body = v[1] ?? (Role == 'M' ? "Robe" : null);
			void Put(int slot, string name, Func<Item, bool> fits)
			{
				int t = BandData.ItemType(name); if (t <= 0) return;
				var it = new Item(); it.SetDefaults(t); if (fits(it)) look.Items[slot] = it;
			}
			Put(VanitySet.Head, v[0], i => i.headSlot >= 0);
			Put(VanitySet.Body, body, i => i.bodySlot >= 0);
			Put(VanitySet.Legs, v[2], i => i.legSlot >= 0);
			if (!look.Any) look = null;
			_looks[key] = look;
			return look;
		}

		public override void SetStaticDefaults()
		{
			base.SetStaticDefaults();
			NPCID.Sets.ActsLikeTownNPC[Type] = false;
			NPCID.Sets.DontDoHardmodeScaling[Type] = true;
		}

		public override void SetDefaults()
		{
			base.SetDefaults();
			NPC.friendly = false; NPC.damage = 0; NPC.knockBackResist = 0.35f; NPC.npcSlots = 0.5f; NPC.value = 0f;
			NPC.rarity = BandRarity; // a rare creature: the Lifeform Analyzer reports it
			NPC.dontTakeDamageFromHostiles = false; NPC.immortal = false; NPC.dontTakeDamage = false;
		}

		public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) { } // BandStats already carries the mode multipliers
		public override bool CheckActive() => false;
		public override bool CheckDead() => true;
		public override bool PreKill() => true;
		public override bool? CanBeHitByItem(Player player, Item item) => null;
		public override bool? CanBeHitByProjectile(Projectile projectile) => null;
		public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
		public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;
		public override bool CanChat() => false;
		public override string GetChat() => "";
		public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) { } // an ordinary enemy: vanilla defense, vanilla hit immunity, no player-parity i-frames
		public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers) { }
		public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			var band = MyBand; if (band == null || Main.netMode == NetmodeID.MultiplayerClient) return;
			if (projectile.friendly && !band.Aggro) band.OnMemberHit(this, Math.Max(1, damageDone)); // hit by the player or a levy: a neutral band answers
			if (projectile.TryGetGlobalProjectile<SoldierProjectileTag>(out var tag) && tag.SoldierIndex >= 0 && !_bantered && Main.rand.NextBool(4)) { _bantered = true; band.Chat(this, "LEVY_BANTER"); }
		}
		public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
		{
			var band = MyBand; if (band != null && !band.Aggro && Main.netMode != NetmodeID.MultiplayerClient) band.OnMemberHit(this, Math.Max(1, damageDone));
		}

		// ---------------------------------------------------------------------------------------------------- setup
		private static readonly string[] First = { "Garrick", "Orla", "Brannoc", "Sedge", "Mavis", "Dorren", "Wulfric", "Teague", "Hob", "Ilsa", "Corwin", "Pell", "Radka", "Osk", "Tamsin", "Bryn", "Fenwick", "Lorcan", "Yara", "Maddoc", "Nessa", "Ivo", "Kestrel", "Rook", "Ansel", "Dagny", "Cato", "Odell", "Perrin", "Sorrel" };

		/// <summary>Server: dress, arm, size and bind this soldier from its band type. <paramref name="stage"/> is the band's own stage.</summary>
		public void Setup(BandRoster type, int memberIdx, int stage, int bandId, int startSize)
		{
			BandId = bandId; TypeIdx = (byte)Math.Max(0, BandTypes.IndexOf(type));
			MemberIdx = (byte)memberIdx; StageIdx = (byte)stage;
			var m = type.Members[memberIdx % type.Members.Length];
			string title = (m.Captain ? type.CaptainTitle : null) ?? type.RankTitle ?? "Soldier";
			var rng = new Random(bandId * 7919 + memberIdx * 31 + 5);
			ShortName = First[rng.Next(First.Length)];
			string full = title + " " + ShortName + (m.Captain ? " " + EventBands.Epithet(type.Event, rng) : "");
			var rec = new SoldierRecord { Name = full, Id = -(bandId * 16 + memberIdx + 1), Kind = SoldierKind.Regular, Grade = Grade.Levy };
			rec.Male = rng.Next(2) == 0; rec.Hair = 1 + rng.Next(30); rec.SkinVariant = 0;
			rec.HairColor = new Color(50 + rng.Next(150), 35 + rng.Next(80), 25 + rng.Next(40));
			rec.SkinColor = type.Skin.HasValue ? new Color(type.Skin.Value.R + rng.Next(-12, 13), type.Skin.Value.G + rng.Next(-12, 13), type.Skin.Value.B + rng.Next(-12, 13)) : new Color(232 - rng.Next(60), 176 - rng.Next(50), 140 - rng.Next(40)); rec.EyeColor = new Color(60, 40, 30);
			// the red of an enemy: shirt and sash in the band's red, a stripe in its tint
			rec.ShirtColor = new Color(170 + rng.Next(50), 40 + rng.Next(30), 40 + rng.Next(25)); rec.UnderShirtColor = new Color(90, 30, 30);
			rec.PantsColor = new Color(70 + rng.Next(40), 50, 45); rec.ShoeColor = new Color(60, 40, 35);
			if (m.Role == 'M') rec.ManaCrystals = 9;
			var g = rec.Gear;
			g.Weapon = Make(m.Weapon, WeaponStack(m.Weapon));
			if (m.Ammo != null) g.Ammo = Make(m.Ammo, 999);
			g.Head = Make(m.Armor[0]); g.Body = Make(m.Armor[1]); g.Legs = Make(m.Armor[2]);
			GiveKit(g, m, stage, memberIdx);
			HasRecall = rng.NextDouble() < 0.6;

			// stats from the band rules: life by stage and role, defense held to the stage line, damage scaled so the weapon's sustained damage stays under the stage cap
			int gearDef = 0; foreach (var it in new[] { g.Head, g.Body, g.Legs }) gearDef += it.defense;
			int life = BandStats.Life(stage, m.Role), defCap = BandStats.Defense(stage, gearDef);
			float weaponHit = Math.Max(1, g.Weapon.damage + (g.Ammo.IsAir ? 0 : g.Ammo.damage));
			int ut = Math.Max(14, m.Role == 'E' ? g.Weapon.useAnimation : g.Weapon.useTime);
			float hz = 60f / ut;
			_budgetHit = BandStats.Hit(stage, m.Role, weaponHit, m.Role == 'E' ? Math.Min(hz, 1.5f) : hz);
			_shotScale = _budgetHit / weaponHit;
			HostileBind(rec, life, defCap);
			if (m.Captain) { NPC.lifeMax = Math.Max(1, (int)Math.Round(NPC.lifeMax * CaptainLifeMult)); NPC.life = NPC.lifeMax; }
			NPC.lavaImmune = type.Biome == BandBiome.Underworld;
			NPC.GivenName = rec.Name;
			if (m.Captain) NPC.rarity = BandRarity + 1;
			NPC.netUpdate = true;
			_lastLife = NPC.life;
		}

		private static Item Make(string name, int stack = 1)
		{
			var it = new Item(); int t = BandData.ItemType(name); if (t > 0) it.SetDefaults(t); it.stack = Math.Max(1, stack); return it;
		}

		/// <summary>Mobility kit by stage: a hook on every member (every second one before the first boss), boots on blades, a double jump on shooters, wings from Hardmode. Levy's movement reads them from the gear like it does for a levy.</summary>
		private static void GiveKit(SoldierGear g, BandMember m, int stage, int idx)
		{
			int a = 0;
			if (m.Kit != null || m.Hook != null) // an event band's own kit (EventBands): exactly these accessories and this hook
			{
				if (m.Hook != null) g.Hook = Make(m.Hook);
				if (m.Kit != null) foreach (var k in m.Kit) if (a < g.Accessories.Length) g.Accessories[a++] = Make(k);
				return;
			}
			if (stage >= 1 || idx % 2 == 0) g.Hook = Make("GrapplingHook"); // a hook for every member from the first boss on (half of a pre-boss band): a band that can hook is a band that gets up the cliff
			if (stage >= 5 && idx % 2 == 1) g.Accessories[a++] = Make("LeafWings");
			else if (stage >= 3 && idx % 2 == 1) g.Accessories[a++] = Make("AngelWings");
			if (stage >= 1 && m.Role == 'E') g.Accessories[a++] = Make("HermesBoots");
			else if (stage >= 1 && m.Role != 'E' && idx % 2 == 0) g.Accessories[a++] = Make("CloudinaBottle");
		}

		/// <summary>A thrown consumable (spiky ball, shuriken) is carried as a big stack; every other weapon as one.</summary>
		private static int WeaponStack(string name)
		{
			int t = BandData.ItemType(name); if (t <= 0) return 1;
			var it = new Item(); it.SetDefaults(t); return it.consumable ? 999 : 1;
		}

		public string RandomMateName()
		{
			var band = BandBrain.Get(BandId); if (band == null) return null;
			string best = null; int seen = 0;
			foreach (var o in band.Members) { if (o == this || !o.NPC.active || o.NPC.life <= 0) continue; if (Main.rand.Next(++seen) == 0) best = o.ShortName; }
			return best;
		}

		// ---------------------------------------------------------------------------------------------------- damage numbers
		public override int HostileShotDamage(int raw) => Math.Max(1, (int)Math.Round(raw / Math.Max(0.05f, DamageMultiplier) * _shotScale / LevyConstants.HostileProjectileDamageMult));
		public override int HostileMeleeDamage(int raw) => Math.Max(1, (int)Math.Round(raw / Math.Max(0.05f, DamageMultiplier) * _shotScale));

		// ---------------------------------------------------------------------------------------------------- the band's decision
		public override bool HostileMayFire(NPC target)
		{
			var band = BandBrain.Get(BandId);
			if (band == null) return true;
			if (Recalling || band.Leaving) return false;
			if (Panic) return Main.rand.NextBool(3); // a routed body still fires, wildly (Levy applies the routed aim penalty)
			return band.TryToken(this);
		}

		public override void HostileThink(int now)
		{
			var band = BandBrain.Get(BandId);
			if (band == null) { base.HostileThink(now); return; }
			if (Recalling) { HostileStop(); return; }
			if (band.Leaving) { LeaveStep(now); return; }
			// a neutral band nobody has provoked goes about its business
			if (band.Neutral && !band.Provoked) { HostileDropTarget(); Loiter(band, now); return; }
			NPC near = HostileNearestEnemy(120f, out float nearTiles);
			// the Flying Dutchman's boarders stay with their ship
			if (band.Rally && Vector2.Distance(NPC.Center, band.Anchor) > 70f * 16f && (near == null || nearTiles > 20f)) { HostileDropTarget(); HostileIdleState(); HostileGoTo(band.Anchor); return; }
			if (near == null)
			{
				// nobody in sight: camp at the spot the band formed
				HostileIdleState(); HostileDropTarget();
				if (Vector2.Distance(NPC.Center, band.Anchor) > 8f * 16f) HostileGoTo(band.Anchor); else HostileStop();
				return;
			}
			if (!band.Aggro)
			{
				// the warning: spread out onto high ground at a distance, face them, hold fire (the band opens up only if they come too close, stay, or hit it)
				HostileDropTarget(); HostileIdleState();
				if (nearTiles < 70f) Posture(band, near, 22f, true, (uint)now); else HostileStop();
				NPC.direction = near.Center.X >= NPC.Center.X ? 1 : -1;
				return;
			}
			// the attacked side of a ranged band falls back for a moment while the other side keeps the fire up
			if (band.RetreatGroup == Group && (uint)now < band.RetreatUntil && Role != 'E')
			{
				HostileDropTarget(); HostileIdleState();
				float away = NPC.Center.X >= near.Center.X ? 1f : -1f;
				HostileGoTo(new Vector2(NPC.Center.X + away * 14f * 16f, NPC.Center.Y), false);
				return;
			}
			StallWatch(band, near, (uint)now);
			// first contact needs no separate step: a member with no shot at the foe goes to a firing position of its own (Pursue), spread around the foe, shooters on the high ground in view
			if (Roster?.Skirmish == true && Role == 'E' && !Panic && SkirmishWithdraw(near, (uint)now)) return;
			bool engaged = HostileEngage();
			NPC foe = HostileTarget ?? near;
			FoeClass cls = BandBrain.ClassOf(foe);
			Tactics(band, cls, foe, engaged, now);
			// the foe cannot be hit from here (no line, out of reach, or the way to it is blocked): do not stand and wait, go over, under or around the terrain to a firing position
			bool strike = engaged && CanStrike(foe);
			if (strike) { _noStrikeSince = 0; _pursuing = false; _purProgDist = 0f; }
			else if (_noStrikeSince == 0) _noStrikeSince = (uint)now;
			// the brain's own approach is trusted while it gets somewhere: a foe on level ground close by, or a gap that is closing; it is replaced when it is not
			if (engaged && !strike && (uint)now - _purProgAt >= 90u)
			{
				float dn = Vector2.Distance(NPC.Center, foe.Center) / 16f;
				_purProgressing = _purProgDist > 0f && _purProgDist - dn >= 2f; _purProgDist = dn; _purProgAt = (uint)now;
			}
			if (_pursuing && engaged && Math.Abs(foe.Center.X - NPC.Center.X) < 12f * 16f && Math.Abs(foe.Bottom.Y - NPC.Bottom.Y) < 5f * 16f && Collision.CanHitLine(NPC.position, NPC.width, NPC.height, foe.position, foe.width, foe.height)) _pursuing = false; // level, close and in the open: the brain's own melee or approach does it
			if (!Panic && !strike && (!engaged || _pursuing || ((uint)now - _noStrikeSince >= PursueAfterTicks && !_purProgressing)))
			{
				_pursuing = true;
				if (!engaged) HostileIdleState();
				Pursue(band, foe ?? near, (uint)now);
				return;
			}
			if (engaged && !Panic) Spread(band, cls, foe, (uint)now);
		}

		// ---------------------------------------------------------------------------------------------------- positioning
		/// <summary>A calm band: stay near where it formed, each member in its own spot, and wander a little.</summary>
		private void Loiter(BandBrain band, int now)
		{
			HostileIdleState();
			if ((uint)now >= _loiterAt)
			{
				_loiterAt = (uint)now + 60u * (4u + (uint)Main.rand.Next(6));
				float ox = ((MemberIdx % 5) - 2) * 3.5f + Main.rand.NextFloat(-6f, 6f);
				_loiterGoal = band.Anchor + new Vector2(ox * 16f, 0f);
			}
			if (Math.Abs(NPC.Center.X - _loiterGoal.X) > 20f) HostileGoTo(_loiterGoal); else HostileStop();
		}

		private static bool Standable(int x, int y)
		{
			if (x < 10 || x >= Main.maxTilesX - 10 || y < 10 || y >= Main.maxTilesY - 10) return false;
			Tile f = Main.tile[x, y];
			if (!f.HasUnactuatedTile || !Main.tileSolid[f.TileType] || f.LiquidAmount > 0) return false;
			for (int a = 1; a <= 3; a++) { Tile t = Main.tile[x, y - a]; if (t.HasUnactuatedTile && Main.tileSolid[t.TileType] || t.LiquidAmount > 0) return false; }
			return true;
		}

		/// <summary>A standing spot for this member around <paramref name="foe"/>: about <paramref name="range"/> tiles out on the side it is on (further out down the line), on the highest ground in view when <paramref name="high"/>, and not within five tiles of another member's spot.</summary>
		private Vector2? FindSpot(BandBrain band, NPC foe, float range, bool high)
		{
			// each member keeps to the side of the foe it is already on (nobody runs through the foe to reach a spot); the line is staggered in depth so spots differ
			int side = NPC.Center.X >= foe.Center.X ? 1 : -1, rank = MemberIdx;
			int cx = (int)(foe.Center.X / 16f + side * (range + (rank % 4) * 3.5f)), fy = (int)(foe.Bottom.Y / 16f);
			Vector2? best = null; float bestScore = float.MaxValue;
			for (int ox = -6; ox <= 6; ox++)
			{
				int x = cx + ox;
				for (int y = fy - (high ? 16 : 2); y <= fy + 3; y++)
				{
					if (!Standable(x, y)) continue;
					Vector2 head = new Vector2(x * 16f, y * 16f - 36f);
					if (!Collision.CanHitLine(head, 8, 8, foe.Center - new Vector2(4f, 4f), 8, 8)) continue;
					Vector2 spot = new Vector2(x * 16f + 8f, y * 16f);
					if (SpotBanned(new Point(x, y - 1), Main.GameUpdateCount)) continue; // a spot no route led to
					float score = (high ? y * 1.0f : Math.Abs(y - fy)) + Math.Abs(ox) * 0.4f;
					foreach (var o in band.Members) if (o != this && o.PostureSpot != default && Vector2.Distance(o.PostureSpot, spot) < 5f * 16f) score += 40f;
					if (score < bestScore) { bestScore = score; best = spot; }
					break; // the first floor from the top of this column that sees the foe is the highest one
				}
			}
			return best;
		}

		/// <summary>Go to this member's spot around the foe (re-picked every second or two).</summary>
		private bool _postureOk;
		private void Posture(BandBrain band, NPC foe, float range, bool high, uint now)
		{
			if (Walled(now)) { PostureSpot = default; _postureOk = false; return; }
			if (now >= _postureAt || PostureSpot == default)
			{
				_postureAt = now + 80u + (uint)(NPC.whoAmI % 20);
				var s = FindSpot(band, foe, range, high); if (s != null) PostureSpot = s.Value;
				_postureOk = s != null;
			}
			if (PostureSpot != default && Vector2.Distance(NPC.Bottom, PostureSpot) > 28f)
			{
				HostileIdleState();
				// by Levy's long-route planner (a spot across a pit or up a cliff is reached by hook, climb or wings, or given up on)
				HostileRouteTo(PostureSpot, out bool noWay);
				int st = HostileRouteStatus;
				if (noWay || st == 3 || st == 4) { _purBad.Add((TileNav.FeetTile(PostureSpot), now + 60u * 12)); PostureSpot = default; _postureOk = false; _postureAt = now + 20; NoteNoWay(now); }
			}
			else HostileStop();
		}

		/// <summary>In the fight: a member standing in a clump (another within four tiles for three-quarters of a second) is sent to its own spot, shooters to higher ground, melee members around the foe's other side.</summary>
		private void Spread(BandBrain band, FoeClass cls, NPC foe, uint now)
		{
			bool clumped = false;
			foreach (var o in band.Members) if (o != this && o.NPC.active && Vector2.Distance(o.NPC.Center, NPC.Center) < 4f * 16f) { clumped = true; break; }
			if (!clumped) _clumpSince = 0; else if (_clumpSince == 0) _clumpSince = now;
			// a skirmisher (goblins, ninjas) does not stand still: every few seconds it hops to fresh high ground
			bool hop = Roster?.Skirmish == true && Role != 'E' && now >= _hopAt;
			if (hop) _hopAt = now + 200u + (uint)Main.rand.Next(220);
			if ((clumped && now - _clumpSince > 45 || hop) && now >= _postureUntil)
			{
				_postureUntil = now + 100; _postureAt = 0;
				float range = Role == 'E' ? 6f : cls == FoeClass.Melee ? 15f : cls == FoeClass.Unknown ? 12f : 9f;
				var s = FindSpot(band, foe, range, Role != 'E'); if (s != null) PostureSpot = s.Value; else _postureUntil = 0;
			}
			if (now < _postureUntil && PostureSpot != default && Vector2.Distance(NPC.Bottom, PostureSpot) > 28f) HostileGoTo(PostureSpot);
		}

		// ---------------------------------------------------------------------------------------------------- pursuit: over, under and around terrain
		/// <summary>Ticks a member may have a target it cannot hit before it stops trusting the brain's own approach and routes to a firing position itself.</summary>
		public const uint PursueAfterTicks = 75;
		private uint _noStrikeSince, _purPickAt, _purStallAt, _purHopAt, _purProgAt, _dispAt; private float _purProgDist; private bool _purProgressing; private Vector2 _dispSpot; private bool _dispHas; private bool _pursuing, _purHasSpot; private Vector2 _purSpot;
		private readonly List<(Point tile, uint until)> _purBad = new();
		/// <summary>Telemetry (lab, self-check): spots given up on as unreachable, last-resort hops, firing positions chosen.</summary>
		public static int PursuitNoWays, PursuitHops, PursuitPicks, PursuitSpotsFlank;
		public bool PursuitActive => _pursuing;
		public string PursuitDebug() => $"pursuing={_pursuing} spot={(_purHasSpot ? ((int)(_purSpot.X / 16f)).ToString() + "," + ((int)(_purSpot.Y / 16f)) : "-")} rt={HostileRouteStatus} stall={_purConf} bans={_purBad.Count} prog={_purProgressing} disp={_dispHas} busy={HostileMovementBusy}";
		public Vector2 PursuitSpot => _purHasSpot ? _purSpot : default;

		/// <summary>This member can hit the foe from where it stands: the foe is inside the weapon's reach and nothing solid is on the line from the head to the foe. A blade: within its swing.</summary>
		public bool CanStrike(NPC foe)
		{
			if (foe == null || Weapon == null) return false;
			Vector2 mc = NPC.Center, fc = foe.Center;
			float d = Vector2.Distance(mc, fc) / 16f - foe.width * 0.5f / 16f;
			float reach = Weapon.IsMelee ? Math.Max(2.5f, Weapon.RangeTiles + 0.5f) : Weapon.RangeTiles * 0.95f;
			if (d > reach) return false;
			return Collision.CanHitLine(new Vector2(mc.X - 4f, NPC.position.Y + 6f), 8, 8, fc - new Vector2(4f, 4f), 8, 8);
		}

		private bool SpotBanned(Point t, uint now)
		{
			for (int i = _purBad.Count - 1; i >= 0; i--)
			{
				if (now >= _purBad[i].until) { _purBad.RemoveAt(i); continue; }
				if (Math.Abs(_purBad[i].tile.X - t.X) <= 3 && Math.Abs(_purBad[i].tile.Y - t.Y) <= 3) return true;
			}
			return false;
		}

		/// <summary>The best place to fight <paramref name="foe"/> from, anywhere in view of it that this member could stand: a shooter wants a clear line inside its weapon's reach, on higher ground when there is some, a good way from the other members' spots and on the side of the foe its place in the band suggests; a blade wants the ground beside the foe, odd members on the far side. Banned (unreachable) spots are skipped.</summary>
		private Vector2? PickPursuitSpot(BandBrain band, NPC foe, uint now)
		{
			bool blade = Weapon == null || Weapon.IsMelee;
			float reach = blade ? 0f : Weapon.RangeTiles * 0.9f;
			int fx = (int)(foe.Center.X / 16f), fy = (int)((foe.Bottom.Y - 1f) / 16f);
			int myx = (int)(NPC.Center.X / 16f), myy = (int)((NPC.Bottom.Y - 1f) / 16f);
			int R = blade ? 9 : (int)Math.Min(Math.Max(8f, reach), 44f);
			int anchorSide = band.Anchor.X <= foe.Center.X ? -1 : 1;
			int wantSide = (MemberIdx & 1) == 0 ? anchorSide : -anchorSide;
			FoeClass cls = BandBrain.ClassOf(foe);
			float ideal = blade ? 1.5f : cls == FoeClass.Melee ? Math.Min(reach * 0.85f, 36f) : Math.Min(reach * 0.5f, 24f);
			float minD = blade ? 0f : (cls == FoeClass.Gunner || cls == FoeClass.Caster) ? 5f : 8f;
			Vector2 fcen = foe.Center; Vector2? best = null; float bestScore = float.MaxValue;
			for (int x = fx - R; x <= fx + R; x++)
			{
				for (int y = fy - 36; y <= fy + 14; y++)
				{
					if (!TileNav.InBounds(x, y) || !TileNav.IsFloor(x, y + 1) || !TileNav.IsStandable(x, y)) continue;
					float dx = x * 16f + 8f - fcen.X, dy = (y - fy) * 16f;
					float d = (float)Math.Sqrt(dx * dx + dy * dy) / 16f;
					if (blade ? d > 9f : (d > reach || d < minD)) continue;
					var tile = new Point(x, y);
					if (SpotBanned(tile, now)) continue;
					Vector2 bot = TileNav.BottomCentre(x, y);
					if (!Collision.CanHitLine(new Vector2(bot.X - 4f, bot.Y - 36f), 8, 8, fcen - new Vector2(4f, 4f), 8, 8)) continue;
					float score = Math.Abs(d - ideal) * (blade ? 1.2f : 0.6f);
					if (!blade) score -= Math.Max(-6f, Math.Min(10f, fy - y)) * 0.5f; // higher than the foe is better
					score += Math.Abs(x - myx) * 0.06f + Math.Abs(y - myy) * 0.12f;
					int side = x < fx ? -1 : 1; if (side != wantSide) score += blade ? 6f : 3f;
					foreach (var o in band.Members)
					{
						if (o == this || !o.NPC.active) continue;
						if (o._purHasSpot && Vector2.Distance(o._purSpot, bot) < (blade ? 3f : 6f) * 16f) score += 22f;
						else if (Vector2.Distance(o.NPC.Bottom, bot) < 3f * 16f) score += 8f;
					}
					if (_purHasSpot && Vector2.Distance(_purSpot, bot) < 2f * 16f) score -= 5f;
					if (score < bestScore) { bestScore = score; best = bot; }
				}
			}
			return best;
		}

		/// <summary>Go and get a firing position on <paramref name="foe"/>: pick the best standable spot in view of it, ask Levy's long-route planner for the way (walk, drop, hook, climb, wing arcs), give the spot up when there is no way to it and pick the next. With no spot at all, route toward the foe itself and take what the planner reaches. A member that has gone nowhere for twelve seconds and is out of every player's sight takes the same hop a levy does.</summary>
		private void Pursue(BandBrain band, NPC foe, uint now)
		{
			if (foe == null) return;
			if (HostileMovementBusy) return; // hanging on a hook, climbing a face or flying an arc: the route owns the body
			if (Walled(now)) { if (now >= _dispAt) Disperse(band, now); if (_dispHas && now < _dispAt) HostileGoTo(_dispSpot); else HostileStop(); return; }
			if (!_purHasSpot || now >= _purPickAt)
			{
				var s = PickPursuitSpot(band, foe, now); PursuitPicks++;
				_purPickAt = now + 150u + (uint)(NPC.whoAmI * 7 % 70);
				if (s.HasValue) { _purSpot = s.Value; _purHasSpot = true; }
				else _purHasSpot = false;
			}
			Vector2 goal = _purHasSpot ? _purSpot : foe.Bottom;
			// parked at the end of the best route there is: members do not stack on one tile, each takes its own spot on the ground it has
			if (!_purHasSpot && _dispHas && now < _dispAt) goal = _dispSpot;
			bool leg = HostileRouteTo(goal, out bool noWay);
			int st = HostileRouteStatus;
			if (!_purHasSpot && noWay && now >= _dispAt) Disperse(band, now);
			if (_purHasSpot && (noWay || st == 3 || st == 4))
			{
				_purBad.Add((TileNav.FeetTile(_purSpot), now + 60u * 12)); NoteNoWay(now);
				_purHasSpot = false; _purPickAt = now + 20;
			}
		}

		private readonly Queue<uint> _noWayAt = new();
		private uint _walledUntil;
		/// <summary>A spot (or the foe itself) has no route: after four of those inside twenty seconds the member is walled in (a pit with no hook, a ledge with no way down) and stops asking the planner for ten seconds.</summary>
		private void NoteNoWay(uint now)
		{
			PursuitNoWays++; _noWayAt.Enqueue(now);
			while (_noWayAt.Count > 0 && now - _noWayAt.Peek() > 1200u) _noWayAt.Dequeue();
			if (_noWayAt.Count >= 4) { _walledUntil = now + 600u; _noWayAt.Clear(); _purBad.Clear(); _purHasSpot = false; PostureSpot = default; _postureOk = false; }
		}
		private bool Walled(uint now) => now < _walledUntil;

		/// <summary>Every think of a fighting member that has a foe in view: a body that cannot hit it and has gone nowhere for twelve seconds takes the hop a levy takes when it is out of every player's sight; after half a minute in sight, with no hook, wings or way out, it drinks its Recall and goes home rather than stand in a pit for ever.</summary>
		private Vector2 _boxMin, _boxMax; private uint _boxSince; private int _purConf;
		private void StallWatch(BandBrain band, NPC foe, uint now)
		{
			if (Panic || Recalling || foe == null) return;
			if (CanStrike(foe)) { _purConf = 0; _boxSince = 0; return; }
			if (_boxSince == 0) { _boxSince = now; _boxMin = _boxMax = NPC.Center; return; }
			_boxMin = Vector2.Min(_boxMin, NPC.Center); _boxMax = Vector2.Max(_boxMax, NPC.Center);
			if (now - _boxSince < 720u) return; // a twelve-second window
			bool confined = _boxMax.X - _boxMin.X <= 16f * 16f && _boxMax.Y - _boxMin.Y <= 7f * 16f; // hopping and shuffling about in one pit is still going nowhere
			_boxSince = now; _boxMin = _boxMax = NPC.Center;
			if (!confined) { _purConf = 0; return; }
			_purConf++;
			if (Environment.GetEnvironmentVariable("WARBANDS_MOVEDBG") == "1") Mod.Logger.Info($"[Warbands] stall #{MemberIdx} windows={_purConf}");
			if (now - _purHopAt > 60u * 8) TryHop(band, foe, now);
			if (_purConf >= 3 && HasRecall && !HostileMovementBusy && band.Aggro && !Recalling) { Recalling = true; RecallDrinks++; _recallDone = now + 55; band.Chat(this, "RETREAT", force: true); SoundEngine.PlaySound(SoundID.Item3, NPC.Center); NPC.netUpdate = true; }
		}

		/// <summary>Stuck at the end of the route with other members on the same tiles: a nearby standable spot (3 to 14 tiles on) farthest from every other member, kept for four seconds.</summary>
		private void Disperse(BandBrain band, uint now)
		{
			_dispAt = now + 240u;
			bool crowded = false;
			foreach (var o in band.Members) if (o != this && o.NPC.active && Vector2.Distance(o.NPC.Center, NPC.Center) < 4f * 16f) { crowded = true; break; }
			if (!crowded) { _dispHas = false; return; }
			int myx = (int)(NPC.Center.X / 16f), myy = (int)((NPC.Bottom.Y - 1f) / 16f);
			Vector2? best = null; float bestScore = float.MinValue;
			for (int x = myx - 14; x <= myx + 14; x++)
			{
				if (Math.Abs(x - myx) < 3) continue;
				for (int y = myy - 4; y <= myy + 4; y++)
				{
					if (!TileNav.InBounds(x, y) || !TileNav.IsFloor(x, y + 1) || !TileNav.IsStandable(x, y)) continue;
					Vector2 bot = TileNav.BottomCentre(x, y);
					float minD = float.MaxValue; foreach (var o in band.Members) if (o != this && o.NPC.active) minD = Math.Min(minD, Vector2.Distance(o.NPC.Bottom, bot) / 16f);
					float score = Math.Min(minD, 9f) - Math.Abs(x - myx) * 0.1f - Math.Abs(y - myy) * 0.3f;
					if (score > bestScore) { bestScore = score; best = bot; }
				}
			}
			if (best.HasValue) { _dispSpot = best.Value; _dispHas = true; } else _dispHas = false;
		}

		/// <summary>The off-screen catch-up hop a levy takes (its landing rules: out of every player's sight, a standable spot, not a sealed pocket): a body that is walled in and unseen lands on the standable spot nearest its firing position (or the foe) that lies outside the place it has been confined to.</summary>
		private void TryHop(BandBrain band, NPC foe, uint now)
		{
			bool dbg = Environment.GetEnvironmentVariable("WARBANDS_MOVEDBG") == "1";
			if (OffscreenHop.Seen(NPC.Center) || NPC.wet || HostileMovementBusy) { if (dbg) Mod.Logger.Info($"[Warbands] hop refused #{MemberIdx} seen={OffscreenHop.Seen(NPC.Center)} wet={NPC.wet} busy={HostileMovementBusy}"); return; }
			_purBad.Clear();
			var s = PickPursuitSpot(band, foe, now);
			Vector2 goal = s ?? foe.Bottom, me = NPC.Bottom;
			Vector2 lo = _boxMin - new Vector2(3f * 16f, 3f * 16f), hi = _boxMax + new Vector2(3f * 16f, 3f * 16f);
			int mx = (int)(me.X / 16f), my = (int)((me.Y - 1f) / 16f);
			var cand = new List<(float d, Vector2 bot)>();
			for (int x = mx - 45; x <= mx + 45; x++)
				for (int y = my - 50; y <= my + 6; y++)
				{
					if (!TileNav.InBounds(x, y) || !TileNav.IsFloor(x, y + 1) || !TileNav.IsStandable(x, y)) continue;
					Vector2 bot = TileNav.BottomCentre(x, y);
					if (bot.X >= lo.X && bot.X <= hi.X && bot.Y >= lo.Y && bot.Y <= hi.Y) continue; // where it has been going round and round
					if (OffscreenHop.Seen(bot)) continue;
					cand.Add((Vector2.DistanceSquared(bot, goal), bot));
				}
			cand.Sort((a, b) => a.d.CompareTo(b.d));
			Vector2 land = default; bool ok = false;
			for (int i = 0; i < cand.Count && i < 14 && !ok; i++)
				if (OffscreenHop.RoomIsOpen(TileNav.FeetTile(cand[i].bot))) { land = cand[i].bot; ok = true; }
			if (dbg) Mod.Logger.Info($"[Warbands] hop try #{MemberIdx} ok={ok} cands={cand.Count} goal=({goal.X / 16f:0},{goal.Y / 16f:0}) land=({land.X / 16f:0},{land.Y / 16f:0}) from=({me.X / 16f:0},{me.Y / 16f:0})");
			if (!ok) return;
			_purHopAt = now; PursuitHops++; _purConf = 0;
			FollowerTravel.HopTo(this, land);
			HostileRouteReset(); _boxSince = 0; _purHasSpot = false; _purPickAt = 0;
		}

		/// <summary>Hit and run for a skirmishing blade: after five to eight seconds in melee it breaks off for a moment, back-stepping with the full movement kit, then closes in again.</summary>
		private bool SkirmishWithdraw(NPC near, uint now)
		{
			if (now < _skWithdrawUntil)
			{
				HostileDropTarget(); HostileIdleState();
				float away = NPC.Center.X >= near.Center.X ? 1f : -1f;
				HostileGoTo(new Vector2(NPC.Center.X + away * 12f * 16f, NPC.Center.Y), false);
				return true;
			}
			if (_skNextAt == 0) _skNextAt = now + 300u + (uint)Main.rand.Next(180);
			if (now >= _skNextAt && Vector2.Distance(NPC.Center, near.Center) < 10f * 16f) { _skWithdrawUntil = now + 80; _skNextAt = now + 80 + 300u + (uint)Main.rand.Next(180); }
			return false;
		}

		/// <summary>The event is over: walk away from the nearest player, shooting at nobody. EventBands removes the member once it is off screen.</summary>
		private void LeaveStep(int now)
		{
			HostileDropTarget(); HostileIdleState();
			if (_leaveDir == 0)
			{
				float best = float.MaxValue; _leaveDir = Main.rand.NextBool() ? 1 : -1;
				for (int i = 0; i < Main.maxPlayers; i++) { var p = Main.player[i]; if (!p.active || p.dead) continue; float d = Math.Abs(p.Center.X - NPC.Center.X); if (d < best) { best = d; _leaveDir = NPC.Center.X >= p.Center.X ? 1 : -1; } }
			}
			HostileGoTo(new Vector2(NPC.Center.X + _leaveDir * 40f * 16f, NPC.Center.Y));
		}

		/// <summary>The Recall potion, now: the same glow and flash as a routed member, with no morale needed (EventBands' fallback for a member that could not walk off).</summary>
		public void BeginRecall()
		{
			if (Recalling) return;
			Recalling = true; RecallDrinks++; _recallDone = Main.GameUpdateCount + 55; NPC.netUpdate = true;
			SoundEngine.PlaySound(SoundID.Item3, NPC.Center);
		}

		/// <summary>What the band does about the class of its foe: ranged members keep the maximum range from a melee foe, close in on a gunner or caster, and take flanking positions around a summoner.</summary>
		private void Tactics(BandBrain band, FoeClass cls, NPC foe, bool engaged, int now)
		{
			bool shooter = Role != 'E';
			float mult = 1f;
			if (shooter)
			{
				switch (cls)
				{
					case FoeClass.Melee: mult = 1.9f; break;
					case FoeClass.Gunner: case FoeClass.Caster: mult = 0.4f; break;
					case FoeClass.Summoner: mult = 0.5f; break;
				}
			}
			HostileStandoffMult = mult;
			if (cls != FoeClass.Unknown && (uint)now - _classTalkAt > 60 * 25)
			{
				_classTalkAt = (uint)now;
				string key = cls == FoeClass.Melee ? "SPOTTED_MELEE" : cls == FoeClass.Summoner ? "SPOTTED_SUMMONER" : cls == FoeClass.Caster ? "SPOTTED_CASTER" : "SPOTTED_GUNNER";
				if (!band.ClassRemarked.Contains(cls)) { band.ClassRemarked.Add(cls); band.Chat(this, key, foe.ModNPC is PlayerStandIn si ? si.Who : null); }
			}
			if (!shooter || !engaged || foe == null) return;
			float d = Vector2.Distance(NPC.Center, foe.Center) / 16f;
			if (cls == FoeClass.Summoner)
			{
				// close in from several sides: an even member takes the left of the foe, an odd one the right, nearer the further down the line
				int side = (MemberIdx & 1) == 0 ? 1 : -1; int rank = MemberIdx / 2;
				if (d > 6f) HostileGoTo(new Vector2(foe.Center.X + side * (5f + rank * 2.5f) * 16f, foe.Bottom.Y));
			}
			else if (cls == FoeClass.Gunner || cls == FoeClass.Caster)
			{
				if (d > 9f) HostileGoTo(foe.Bottom); // get in close
			}
		}

		// ---------------------------------------------------------------------------------------------------- per tick (server)
		public override void AI()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) { NPC.chaseable = !_passiveNet; base.AI(); return; }
			uint now = Main.GameUpdateCount;
			var band = BandBrain.Get(BandId);
			bool passive = band != null && band.Neutral && !band.Provoked;
			if (passive != _passiveNet) { _passiveNet = passive; NPC.netUpdate = true; }
			NPC.chaseable = !passive; // a neutral band nobody has provoked is not a minion target
			TrackDamage(band, now);
			if (band != null) Morale_(band, now);
			base.AI();
			if (Recalling)
			{
				NPC.velocity.X *= 0.2f;
				if (!Main.dedServ && now % 3 == 0) { int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.MagicMirror, 0f, -2f); Main.dust[d].noGravity = true; }
				if (now >= _recallDone) Vanish(band);
			}
		}

		private void TrackDamage(BandBrain band, uint now)
		{
			if (_lastLife < 0) _lastLife = NPC.life;
			int d = _lastLife - NPC.life;
			if (d > 0)
			{
				_lastBlow = d; _lifeBeforeBlow = _lastLife; _lastHitTick = now;
				if (band != null) { band.OnMemberHit(this, d); if (NPC.life * 4 < NPC.lifeMax && Main.rand.NextBool(3)) band.Chat(this, "LOW_LIFE"); else if (Main.rand.NextBool(7)) band.Chat(this, "HURT"); }
				Morale -= 100f * d / Math.Max(1, NPC.lifeMax) * 0.9f;
			}
			_lastLife = NPC.life;
		}

		private void Morale_(BandBrain band, uint now)
		{
			NPC foe = HostileTarget;
			bool engaged = foe != null && State != SoldierState.SearchDestroy;
			// chip damage: what one of this member's hits does to the player is a sliver of the player's life
			if (engaged && band.Aggro && foe.ModNPC is PlayerStandIn si && si.Who != null && si.IsAlive && AttackCooldown > 0)
			{
				Player p = si.Who;
				float eff = Math.Max(1f, _budgetHit - p.statDefense * Levy.Common.Util.LevyUtil.ModeDefenseEffectiveness());
				float frac = eff / Math.Max(1f, p.statLifeMax2);
				if (frac < 0.012f) { Morale -= 0.1f; if (++_chip % 90 == 0) band.Chat(this, "CHIP_DAMAGE"); }
			}
			if (NPC.life * 4 < NPC.lifeMax) Morale -= 0.05f;
			else if (!engaged && now - _lastHitTick > 120) Morale = Math.Min(100f, Morale + 0.08f);
			if (band.Members.Count <= 1 && band.Killed >= 2) Morale -= 0.15f;
			Morale = MathHelper.Clamp(Morale, 0f, 100f);
			if (!Panic && Morale <= 25f)
			{
				Panic = true; PanicEntries++; HostileSetRouted(true);
				_recallAt = now + 100 + (uint)Main.rand.Next(120);
				band.Chat(this, "PANIC");
				NPC.netUpdate = true;
			}
			else if (Panic && !Recalling)
			{
				NPC n = HostileNearestEnemy(10f, out _);
				if (Morale >= 55f && n == null) { Panic = false; HostileSetRouted(false); NPC.netUpdate = true; }
				else if (HasRecall && now >= _recallAt && now - _lastHitTick > 45 && HostileNearestEnemy(8f, out _) == null)
				{
					Recalling = true; RecallDrinks++; _recallDone = now + 55; band.Chat(this, "RETREAT", force: true);
					SoundEngine.PlaySound(SoundID.Item3, NPC.Center);
				}
			}
		}

		/// <summary>The Recall potion: a glow, a flash, gone. Counts as a fled member (the band's bounty is paid on those who fell).</summary>
		private void Vanish(BandBrain band)
		{
			Vanished++;
			band?.OnMemberFled(this);
			if (!Main.dedServ)
			{
				for (int i = 0; i < 24; i++) { int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.MagicMirror, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f)); Main.dust[d].noGravity = true; }
				SoundEngine.PlaySound(SoundID.Item6, NPC.Center);
			}
			NPC.active = false; NPC.netUpdate = true;
		}

		// ---------------------------------------------------------------------------------------------------- death and loot
		public override void OnKill()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || Member == null) return;
			var band = BandBrain.Get(BandId);
			bool oneTap = _lifeBeforeBlow >= NPC.lifeMax * 0.6f && _lastBlow >= NPC.lifeMax * 0.55f;
			band?.OnMemberDied(this, oneTap);
			if (band != null && band.IsEvent) { EventBands.OnMemberKilled(this, band); return; } // an event band's reward is the event's own loot, not its gear
			var cfg = WarbandsConfig.Get; if (cfg == null || !cfg.GearDrops) return; float share = cfg.LootSharePercent / 100f;
			void Roll(string name, int stack, float chance) { int t = BandData.ItemType(name); if (t > 0 && Main.rand.NextFloat() < chance) Item.NewItem(NPC.GetSource_Loot(), NPC.Hitbox, t, stack); }
			Roll(Member.Weapon, 1, share);
			if (Member.Ammo != null) Roll(Member.Ammo, 15 + Main.rand.Next(26), Math.Min(1f, share * 1.5f));
			foreach (var a in Member.Armor) Roll(a, 1, share * 0.7f);
			if (Record != null && !Record.Gear.Hook.IsAir && Main.rand.NextFloat() < share * 0.5f) Item.NewItem(NPC.GetSource_Loot(), NPC.Hitbox, Record.Gear.Hook.type);
		}

		// ---------------------------------------------------------------------------------------------------- net + draw
		public override void SendExtraAI(BinaryWriter writer)
		{
			base.SendExtraAI(writer);
			writer.Write(BandId); writer.Write(TypeIdx); writer.Write(MemberIdx); writer.Write(StageIdx); writer.Write(Panic); writer.Write(_passiveNet);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			base.ReceiveExtraAI(reader);
			BandId = reader.ReadInt32(); TypeIdx = reader.ReadByte(); MemberIdx = reader.ReadByte(); StageIdx = reader.ReadByte(); Panic = reader.ReadBoolean(); _passiveNet = reader.ReadBoolean();
		}

		private static Asset<Texture2D> _pennant;
		public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			base.PostDraw(spriteBatch, screenPos, drawColor);
			if (Main.dedServ || !NPC.active || Roster == null) return;
			try
			{
				_pennant ??= ModContent.Request<Texture2D>("Warbands/Content/NPCs/BandPennant");
				if (!_pennant.IsLoaded) return;
				var tex = _pennant.Value; int dir = NPC.direction == 0 ? 1 : NPC.direction;
				Vector2 pos = NPC.Center - screenPos + new Vector2(-dir * 11f, -20f + (float)Math.Sin(Main.GameUpdateCount * 0.12f + NPC.whoAmI) * 1.2f);
				var tint = new Color(Roster.TintR, Roster.TintG, Roster.TintB);
				spriteBatch.Draw(tex, pos, null, Color.Lerp(Color.White, tint, 0.0f), 0f, new Vector2(tex.Width * 0.5f, tex.Height), 1f, dir == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
			}
			catch { }
		}
	}
}
