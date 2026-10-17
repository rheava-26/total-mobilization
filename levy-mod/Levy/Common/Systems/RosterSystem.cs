using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Config;
using Levy.Common.Dialogue;
using Levy.Common.Fear;
using Levy.Common.Movement;
using Levy.Common.Net;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Systems
{
	/// <summary>
	/// The roster: regulars (persistent), the memorial, command points, tiers, dawn ticks, payday, desertion, respawn queue,
	/// body deployment under the render cap, and orders. Server-authoritative: every mutating member is a no-op on a
	/// multiplayer client; clients hold a display-only copy of <see cref="Regulars"/> fed by roster snapshots.
	/// </summary>
	public class RosterSystem : ModSystem
	{
		public static RosterSystem Instance => ModContent.GetInstance<RosterSystem>();

		public readonly List<SoldierRecord> Regulars = new();
		public readonly List<SoldierRecord> Memorial = new();
		public int NextId = 1;
		/// <summary>Day counter (dawns seen) for once-per-day flags.</summary>
		public int DayNumber;
		public int RallyCooldown;

		// ---- runtime ----
		private readonly List<SoldierNPC> _bodies = new();
		private readonly List<Deserter> _deserters = new();
		private readonly List<SoldierRecord> _deployScratch = new();
		private readonly Dictionary<string, int> _onlineByName = new();
		private int _deployTimer, _snapshotTimer, _feedTimer, _spoilsTimer, _spoilsSoundCd;
		private bool _dawnInit, _lastDayTime;

		private sealed class Deserter { public SoldierNPC Body; public int Ticks; }

		private static readonly Comparison<SoldierRecord> DeployOrder = (a, b) => DeployPriority(a).CompareTo(DeployPriority(b));

		public override void Load() => RosterNet.Register();

		// =====================================================================================================
		// Progression (computed from boss flags every time it is read; never stored)
		// =====================================================================================================
		private static bool AnyMechDown => NPC.downedMechBossAny || NPC.downedMechBoss1 || NPC.downedMechBoss2 || NPC.downedMechBoss3;

		public virtual int CommandPoints
		{
			get
			{
				int cp = LevyConstants.CommandPointsBase;
				if (NPC.downedSlimeKing) cp += LevyConstants.CpKingSlime;
				if (NPC.downedBoss1) cp += LevyConstants.CpEyeOfCthulhu;
				if (NPC.downedBoss2) cp += LevyConstants.CpEaterOrBrain;
				if (NPC.downedQueenBee) cp += LevyConstants.CpQueenBee;
				if (NPC.downedBoss3) cp += LevyConstants.CpSkeletron;
				if (NPC.downedDeerclops) cp += LevyConstants.CpDeerclops;
				if (Main.hardMode) cp += LevyConstants.CpWallOfFlesh;
				if (NPC.downedQueenSlime) cp += LevyConstants.CpQueenSlime;
				if (NPC.downedMechBoss1) cp += LevyConstants.CpEachMech;
				if (NPC.downedMechBoss2) cp += LevyConstants.CpEachMech;
				if (NPC.downedMechBoss3) cp += LevyConstants.CpEachMech;
				if (NPC.downedPlantBoss) cp += LevyConstants.CpPlantera;
				if (NPC.downedGolemBoss) cp += LevyConstants.CpGolem;
				if (NPC.downedEmpressOfLight) cp += LevyConstants.CpEmpress;
				if (NPC.downedFishron) cp += LevyConstants.CpFishron;
				if (NPC.downedAncientCultist) cp += LevyConstants.CpCultist;
				if (NPC.downedMoonlord) cp += LevyConstants.CpMoonLord;
				return cp;
			}
		}

		public virtual int CommandPointsUsed { get { int n = 0; foreach (var r in Regulars) n += r.CommandPointCost; return n; } }

		public virtual int Tier
		{
			get
			{
				if (NPC.downedMoonlord) return 6;
				if (NPC.downedGolemBoss) return 5;
				if (NPC.downedPlantBoss) return 4;
				if (AnyMechDown) return 3;
				if (Main.hardMode) return 2;
				return 1;
			}
		}

		public virtual long LevyWage => LevyConstants.TierLevyWage[Math.Clamp(Tier - 1, 0, LevyConstants.TierLevyWage.Length - 1)];

		public virtual bool GradeUnlocked(Grade g) => g switch
		{
			Grade.Levy => true,
			Grade.ManAtArms => AnyMechDown,
			Grade.Champion => NPC.downedGolemBoss,
			_ => false,
		};

		// =====================================================================================================
		// Lookups (contract)
		// =====================================================================================================
		public virtual SoldierRecord FindById(int id) { foreach (var r in Regulars) if (r.Id == id) return r; return null; }
		public virtual IEnumerable<SoldierRecord> RegularsOf(Player commander) { if (commander == null) yield break; foreach (var r in Regulars) if (r.CommanderName == commander.name) yield return r; }

		// =====================================================================================================
		// Helpers
		// =====================================================================================================
		/// <summary>Chat line to one player: NewText in single player, a targeted chat message on the server.</summary>
		internal static void SendToPlayer(Player player, string text, Color? color = null)
		{
			if (player == null || string.IsNullOrEmpty(text)) return;
			Color c = color ?? new Color(255, 240, 20);
			if (Main.netMode == NetmodeID.Server) ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(text), c, player.whoAmI);
			else if (Main.netMode == NetmodeID.SinglePlayer) Main.NewText(text, c);
		}

		/// <summary>Pushes a barracks tile entity to every client after its treasury/manpower changed (server).</summary>
		internal static void SyncBarracks(BarracksTE te)
		{
			if (te == null || Main.netMode != NetmodeID.Server) return;
			NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, te.ID, te.Position.X, te.Position.Y);
		}

		private static SoldierNPC BodyOf(SoldierRecord rec) => rec != null && rec.HasBody ? Main.npc[rec.BodyIndex].ModNPC as SoldierNPC : null;

		/// <summary>The commander if online (refreshes <see cref="SoldierRecord.CommanderIndex"/>), else null.</summary>
		private static Player OnlineCommander(SoldierRecord rec)
		{
			if (rec == null || string.IsNullOrEmpty(rec.CommanderName)) return null;
			int i = rec.CommanderIndex;
			if (i < 0 || i >= Main.maxPlayers || !Main.player[i].active || Main.player[i].name != rec.CommanderName) i = LevyUtil.FindPlayerByName(rec.CommanderName);
			rec.CommanderIndex = i;
			return i >= 0 ? Main.player[i] : null;
		}

		private static BarracksTE HomeOf(SoldierRecord rec) => SettlementSystem.Instance.GetBarracks(rec?.HomeBarracksId ?? -1);
		private static ArmoryTE ArmoryOf(BarracksTE home) => home == null ? null : SettlementSystem.Instance.LinkedArmory(home);

		/// <summary>The barracks that pays this regular: its home, or for the homeless the nearest barracks to the commander (or body).</summary>
		private static BarracksTE PayingBarracks(SoldierRecord rec)
		{
			var home = HomeOf(rec);
			if (home != null) return home;
			var commander = OnlineCommander(rec);
			var body = BodyOf(rec);
			if (commander != null) return SettlementSystem.Instance.NearestBarracks(commander.Center);
			if (body != null) return SettlementSystem.Instance.NearestBarracks(body.NPC.Center);
			return null;
		}

		private static string GradeName(Grade g) => LevyUtil.T(g switch { Grade.ManAtArms => "UI.GradeManAtArms", Grade.Champion => "UI.GradeChampion", _ => "UI.GradeLevy" });

		/// <summary>"Pvt. Andrew" → "Andrew" (strips a dotted rank prefix only).</summary>
		private static string BaseName(string name)
		{
			if (string.IsNullOrEmpty(name)) return "";
			int sp = name.IndexOf(' ');
			return sp > 0 && name[sp - 1] == '.' && sp + 1 < name.Length ? name.Substring(sp + 1) : name;
		}

		private static T Pick<T>(T[] options) => options[Main.rand.Next(options.Length)];

		private static int DeployPriority(SoldierRecord r)
		{
			if (r.Order == SoldierOrder.Follow && r.CommanderIndex >= 0) return 0;
			if (r.Order == SoldierOrder.Patrol) return 1;
			return 2;
		}

		/// <summary>
		/// Re-binds a living body to its (changed) record so the body re-reads gear, name and stats. Bind resets the brain
		/// to Idle for one think cycle, which is acceptable for promotions and crystal feeding.
		/// CONTRACT-REQUEST: a public SoldierNPC.NotifyGearChanged() that only calls Combat_OnGearChanged would avoid the brain reset.
		/// </summary>
		private static void RebindBody(SoldierNPC body, SoldierRecord rec)
		{
			if (body == null || rec == null || !body.NPC.active) return;
			rec.CurrentHp = Math.Max(1, body.NPC.life);
			body.NPC.GivenName = rec.Name;
			body.NotifyGearChanged(); // re-reads the weapon profile and stats without resetting the brain
		}

		private static void DespawnBody(SoldierNPC body)
		{
			if (body == null) return;
			var npc = body.NPC;
			if (!npc.active) return;
			npc.active = false;
			npc.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
		}

		private static void DropAllGear(SoldierRecord rec, IEntitySource source, Rectangle box)
		{
			foreach (var it in rec.Gear.AllItems())
			{
				if (it == null || it.IsAir || it.stack <= 0) continue;
				Item.NewItem(source, box, it);
			}
			rec.Gear.Clear();
		}

		// =====================================================================================================
		// Roster ops (server)
		// =====================================================================================================
		public virtual SoldierRecord Enlist(BarracksTE home, Player commander, Grade grade)
		{
			if (LevyUtil.IsClient || home == null || commander == null) return null;
			int gi = (int)grade;
			if (gi < 0 || gi >= LevyConstants.GradeCpCost.Length) return null;
			if (!GradeUnlocked(grade)) { SendToPlayer(commander, LevyUtil.T("UI.GradeLocked")); return null; }
			if (CommandPointsUsed + LevyConstants.GradeCpCost[gi] > CommandPoints) { SendToPlayer(commander, LevyUtil.T("UI.NotEnoughCp")); return null; }
			long fee = EconomySystem.MusterFee(grade);
			if (!EconomySystem.TryPay(home, fee)) { SendToPlayer(commander, LevyUtil.T("UI.NotEnoughMoney")); return null; }

			var rec = NewRecord(grade);
			rec.HomeBarracksId = home.ID; rec.HomePos = home.Position;
			rec.CommanderName = commander.name; rec.CommanderIndex = commander.whoAmI;
			rec.Order = SoldierOrder.Follow; rec.StoodDown = false;
			ArmoryOf(home)?.IssueGear(rec);
			Regulars.Add(rec);
			SpawnBody(rec, home.MusterPoint); // -1 under the render cap: the deployment loop picks it up later
			LevyUtil.Chat(LevyUtil.T("Chat.Enlisted", rec.Name, SettlementSystem.Instance.TownName(home)));
			SyncBarracks(home);
			SendRosterSnapshot();
			return rec;
		}

		private SoldierRecord NewRecord(Grade grade)
		{
			var rec = new SoldierRecord
			{
				Id = NextId++, Kind = SoldierKind.Regular, Grade = grade,
				Temperament = (Temperament)Main.rand.Next(4),
				Male = Main.rand.NextBool(),
			};
			var variants = rec.Male ? LevyConstants.MaleSkinVariants : LevyConstants.FemaleSkinVariants;
			rec.SkinVariant = Pick(variants);
			rec.Hair = Main.rand.Next(0, LevyConstants.HairStyleCount);
			rec.HairColor = Pick(LevyConstants.HairPalette); rec.SkinColor = Pick(LevyConstants.SkinPalette); rec.EyeColor = Pick(LevyConstants.EyePalette);
			rec.ShirtColor = Pick(LevyConstants.ShirtPalette); rec.UnderShirtColor = Pick(LevyConstants.UnderShirtPalette);
			rec.PantsColor = Pick(LevyConstants.PantsPalette); rec.ShoeColor = Pick(LevyConstants.ShoePalette);
			string given = DialogueSystem.RandomSoldierName(rec.Male);
			if (string.IsNullOrWhiteSpace(given)) given = rec.Male ? "Andrew" : "Molly";
			rec.Name = rec.RankPrefix + " " + given;
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			return rec;
		}

		public virtual bool Promote(SoldierRecord rec, Grade to)
		{
			if (LevyUtil.IsClient || rec == null || rec.Kind != SoldierKind.Regular || !Regulars.Contains(rec)) return false;
			int ti = (int)to;
			if (ti >= LevyConstants.GradeCpCost.Length || to <= rec.Grade) return false;
			var commander = OnlineCommander(rec);
			if (!GradeUnlocked(to)) { SendToPlayer(commander, LevyUtil.T("UI.GradeLocked")); return false; }
			int delta = LevyConstants.GradeCpCost[ti] - LevyConstants.GradeCpCost[(int)rec.Grade];
			if (CommandPointsUsed + delta > CommandPoints) { SendToPlayer(commander, LevyUtil.T("UI.NotEnoughCp")); return false; }
			var payer = PayingBarracks(rec);
			if (payer == null) { SendToPlayer(commander, LevyUtil.T("Roster.NoHome")); return false; }
			if (!EconomySystem.TryPay(payer, EconomySystem.PromotionCost(rec.Grade, to))) { SendToPlayer(commander, LevyUtil.T("UI.NotEnoughMoney")); return false; }

			rec.Grade = to;
			rec.CurrentHp = Math.Clamp(rec.CurrentHp, 1, rec.MaxHp);
			rec.Mana = Math.Clamp(rec.Mana, 0, rec.MaxMana);
			ArmoryOf(HomeOf(rec))?.IssueGear(rec); // fills the new accessory slots
			var body = BodyOf(rec);
			RebindBody(body, rec);
			LevyUtil.Chat(LevyUtil.T("Chat.Promoted", rec.Name, GradeName(to)));
			if (body != null) DialogueSystem.Bark(body, DialogueTrigger.PROMOTED);
			SyncBarracks(payer);
			SendRosterSnapshot();
			return true;
		}

		public virtual void Discharge(SoldierRecord rec)
		{
			if (LevyUtil.IsClient || rec == null) return;
			bool wasListed = Regulars.Remove(rec);
			var body = BodyOf(rec);
			var home = HomeOf(rec);
			var armory = ArmoryOf(home);
			if (armory != null) armory.ReturnGear(rec.Gear);
			else
			{
				Rectangle box;
				if (body != null) box = body.NPC.Hitbox;
				else if (home != null) box = new Rectangle((int)home.MusterPoint.X - 8, (int)home.MusterPoint.Y - 40, 16, 40);
				else { var c = OnlineCommander(rec); box = c != null ? c.Hitbox : Rectangle.Empty; }
				if (box != Rectangle.Empty) DropAllGear(rec, new EntitySource_Misc("Levy:Discharge"), box);
			}
			rec.Gear.Clear();
			DespawnBody(body);
			rec.BodyIndex = -1; rec.Dead = false; rec.RespawnTimer = 0;
			if (wasListed) LevyUtil.Chat(LevyUtil.T("Roster.Discharged", rec.Name));
			SendRosterSnapshot();
		}

		public virtual bool FeedCrystal(SoldierRecord rec, int itemType)
		{
			if (LevyUtil.IsClient || rec == null) return false;
			if (!ApplyCrystal(rec, itemType)) return false;
			SendRosterSnapshot();
			return true;
		}

		/// <summary>Applies the growth for one crystal (never touches any inventory or storage, never sends a snapshot).</summary>
		private bool ApplyCrystal(SoldierRecord rec, int itemType)
		{
			if (LevyUtil.IsClient || rec == null) return false;
			int gi = Math.Clamp((int)rec.Grade, 0, LevyConstants.MaxLifeCrystalsByGrade.Length - 1);
			int hpGain = 0;
			switch (itemType)
			{
				case ItemID.LifeCrystal:
					if (rec.LifeCrystals >= LevyConstants.MaxLifeCrystalsByGrade[gi] || rec.MaxHp >= LevyConstants.GradeHpCap[gi]) return false;
					rec.LifeCrystals++; hpGain = LevyConstants.HpPerLifeCrystal;
					break;
				case ItemID.LifeFruit:
					if (rec.LifeFruit >= LevyConstants.MaxLifeFruit || rec.MaxHp >= LevyConstants.GradeHpCap[gi]) return false;
					rec.LifeFruit++; hpGain = LevyConstants.HpPerLifeFruit;
					break;
				case ItemID.ManaCrystal:
					if (rec.ManaCrystals >= (LevyConstants.MaxMana - LevyConstants.BaseMana) / LevyConstants.ManaPerCrystal) return false;
					rec.ManaCrystals++; rec.Mana = rec.MaxMana;
					break;
				default:
					return false;
			}
			int maxHp = Math.Max(1, rec.MaxHp);
			var body = BodyOf(rec);
			if (body != null)
			{
				var npc = body.NPC;
				npc.lifeMax = maxHp;
				npc.life = Math.Clamp(npc.life + hpGain, 1, maxHp);
				rec.CurrentHp = npc.life;
				body.StatsDirty = true;
				npc.netUpdate = true;
			}
			else if (!rec.Dead) rec.CurrentHp = Math.Clamp(rec.CurrentHp + hpGain, 1, maxHp);
			return true;
		}

		public virtual void Rebase(SoldierRecord rec, BarracksTE newHome)
		{
			if (LevyUtil.IsClient || rec == null) return;
			TryRebase(rec, newHome);
			var commander = OnlineCommander(rec);
			var home = HomeOf(rec);
			if (home != null) SendToPlayer(commander, LevyUtil.T("Roster.Rebased", rec.Name, SettlementSystem.Instance.TownName(home)));
			else SendToPlayer(commander, LevyUtil.T("Roster.Homeless", rec.Name));
			SendRosterSnapshot();
		}

		/// <summary>Sets the home (explicit, or the nearest barracks within <see cref="LevyConstants.HomelessRebaseRadius"/>), else homeless. No chat.</summary>
		private static void TryRebase(SoldierRecord rec, BarracksTE newHome)
		{
			if (newHome == null)
			{
				var body = BodyOf(rec);
				Vector2? at = null;
				if (body != null) at = body.NPC.Center;
				else if (rec.HomePos != Point16.NegativeOne) at = new Vector2((rec.HomePos.X + 1.5f) * 16f, (rec.HomePos.Y + 1.5f) * 16f);
				else { var c = OnlineCommander(rec); if (c != null) at = c.Center; }
				if (at.HasValue) newHome = SettlementSystem.Instance.NearestBarracks(at.Value, LevyConstants.HomelessRebaseRadius);
			}
			if (newHome != null) { rec.HomeBarracksId = newHome.ID; rec.HomePos = newHome.Position; }
			else { rec.HomeBarracksId = -1; rec.HomePos = Point16.NegativeOne; }
		}

		// =====================================================================================================
		// Treasury (server / single player)
		// =====================================================================================================
		public virtual bool Deposit(Player player, BarracksTE barracks, long copper)
		{
			if (LevyUtil.IsClient || player == null || barracks == null || copper <= 0) return false;
			if (!player.BuyItem(copper)) return false;
			barracks.Treasury += copper;
			SyncBarracks(barracks);
			return true;
		}

		public virtual bool Withdraw(Player player, BarracksTE barracks, long copper)
		{
			if (LevyUtil.IsClient || player == null || barracks == null || copper <= 0) return false;
			if (barracks.Treasury < copper) return false;
			barracks.Treasury -= copper;
			EconomySystem.GiveCoins(player, copper, player.GetSource_Misc("Levy:Withdraw"));
			SyncBarracks(barracks);
			return true;
		}

		// =====================================================================================================
		// Bodies
		// =====================================================================================================
		public virtual int SpawnBody(SoldierRecord rec, Vector2 position)
		{
			if (LevyUtil.IsClient || rec == null) return -1;
			if (rec.HasBody) return rec.BodyIndex; // never two bodies for one record
			if (ActiveBodyCount >= LevyConfig.Get.RenderCap) return -1;
			int type = ModContent.NPCType<SoldierNPC>();
			if (type <= 0) return -1;
			position = SafeSpawnPosition(position); // never materialise inside a fight (falls back to the asked position)
			int idx = NPC.NewNPC(new EntitySource_WorldEvent("Levy:Soldier"), (int)position.X, (int)position.Y, type);
			if (idx < 0 || idx >= Main.maxNPCs) return -1;
			var npc = Main.npc[idx];
			if (npc.ModNPC is not SoldierNPC body) { npc.active = false; return -1; }
			body.Bind(rec);
			npc.Bottom = position;
			npc.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
			_bodies.Add(body);
			return idx;
		}

		// ---- safe spawn placement ----
		private static readonly Vector2[] _spawnHostileCentre = new Vector2[LevyConstants.SpawnHostileCap];
		private static readonly Rectangle[] _spawnHostileBox = new Rectangle[LevyConstants.SpawnHostileCap];

		/// <summary>
		/// Where a body asked for at <paramref name="wanted"/> (bottom-centre) should really appear. If a hostile NPC is within
		/// SpawnDangerRadiusTiles (hitbox distance) of it, the nearest standable spot within SpawnSafeSearchTiles columns that is clear of
		/// every hostile by that radius is returned instead. Candidates are scored by distance from the wanted point (so a muster point
		/// stays the home side), with a malus for lying on the nearest hostile's side of it. No hostile near, or no safe spot found:
		/// the wanted point comes back unchanged. Server / single player only; bounded to SpawnSafeSearchTiles x (2 x SpawnSafeVerticalTiles + 1) tile tests.
		/// </summary>
		public static Vector2 SafeSpawnPosition(Vector2 wanted)
		{
			if (LevyUtil.IsClient) return wanted;
			try
			{
				float tilePx = LevyConstants.TilePx;
				float dangerPx = LevyConstants.SpawnDangerRadiusTiles * tilePx;
				float dangerSq = dangerPx * dangerPx;
				Vector2 wantedBody = wanted - new Vector2(0f, SoldierNPC.BodyHeight * 0.5f);
				float scanPx = dangerPx + (LevyConstants.SpawnSafeSearchTiles + 2) * tilePx;
				// remember the hostiles that can matter to any candidate (hitbox boxes; contact damage > 0 or a boss)
				int n = 0; bool wantedUnsafe = false; float nearestSq = float.MaxValue; Vector2 nearestCentre = default;
				for (int i = 0; i < Main.maxNPCs && n < _spawnHostileBox.Length; i++)
				{
					NPC h = Main.npc[i];
					if (!h.active || h.ModNPC is SoldierNPC || !LevyUtil.IsHostileTarget(h) || (h.damage <= 0 && !h.boss)) continue;
					if (Math.Abs(h.Center.X - wanted.X) > scanPx + h.width || Math.Abs(h.Center.Y - wanted.Y) > scanPx + h.height) continue;
					_spawnHostileCentre[n] = h.Center; _spawnHostileBox[n] = h.Hitbox; n++;
					float dsq = Vector2.DistanceSquared(wantedBody, h.Hitbox.ClosestPointInRect(wantedBody));
					if (dsq < dangerSq) wantedUnsafe = true;
					if (dsq < nearestSq) { nearestSq = dsq; nearestCentre = h.Center; }
				}
				if (!wantedUnsafe) return wanted;

				int wx = (int)(wanted.X / 16f), wy = (int)((wanted.Y - 1f) / 16f);
				float side = Math.Sign(nearestCentre.X - wanted.X);
				bool found = false; Vector2 best = wanted; float bestScore = float.MaxValue;
				for (int dx = -LevyConstants.SpawnSafeSearchTiles; dx <= LevyConstants.SpawnSafeSearchTiles; dx++)
				{
					int x = wx + dx;
					float colScoreFloor = (Math.Abs(dx) - 1) * tilePx;
					if (colScoreFloor >= bestScore) continue; // columns only get farther out: cannot beat the best already found
					for (int k = 0; k <= LevyConstants.SpawnSafeVerticalTiles * 2; k++)
					{
						int dy = (k & 1) == 0 ? -(k >> 1) : ((k + 1) >> 1); // 0, +1, -1... nearest rows first (alternating)
						int y = wy + dy;
						if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y) || TileNav.HasLiquid(x, y) || TileNav.HasLiquid(x, y - 1)) continue;
						Vector2 bottom = TileNav.BottomCentre(x, y);
						Vector2 body = bottom - new Vector2(0f, SoldierNPC.BodyHeight * 0.5f);
						bool safe = true;
						for (int j = 0; j < n; j++)
							if (Vector2.DistanceSquared(body, _spawnHostileBox[j].ClosestPointInRect(body)) < dangerSq) { safe = false; break; }
						if (!safe) continue;
						float score = Vector2.Distance(bottom, wanted);
						if (side != 0f && Math.Sign(bottom.X - wanted.X) == side) score += LevyConstants.SpawnHostileSideMalusTiles * tilePx;
						if (score < bestScore) { bestScore = score; best = bottom; found = true; }
						break; // the nearest standable row of this column is enough
					}
				}
				return found ? best : wanted;
			}
			catch { return wanted; } // placement is a nicety: a tile read failing must never cost the soldier its body
		}

		public override void PreUpdateNPCs()
		{
			_bodies.Clear();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (n.active && n.ModNPC is SoldierNPC s && s.Record != null) _bodies.Add(s);
			}
		}

		public virtual IEnumerable<SoldierNPC> ActiveBodies()
		{
			for (int i = 0; i < _bodies.Count; i++)
			{
				var s = _bodies[i];
				if (s.NPC.active && s.Record != null) yield return s;
			}
		}

		public virtual int ActiveBodyCount
		{
			get
			{
				int n = 0;
				for (int i = 0; i < _bodies.Count; i++) if (_bodies[i].NPC.active && _bodies[i].Record != null) n++;
				return n;
			}
		}

		public override void PostUpdateNPCs()
		{
			if (LevyUtil.IsClient) return;
			if (RallyCooldown > 0) RallyCooldown--;
			TickDeserters();
			if (++_deployTimer >= LevyConstants.DeployIntervalTicks)
			{
				_deployTimer = 0;
				ResolveCommanders();
				TickRespawns(LevyConstants.DeployIntervalTicks);
				Deploy();
			}
			if (++_feedTimer >= LevyConstants.AutoFeedIntervalTicks) { _feedTimer = 0; TickAutoFeed(); }
			if (_spoilsSoundCd > 0) _spoilsSoundCd--;
			if (++_spoilsTimer >= LevyConstants.SpoilsIntervalTicks) { _spoilsTimer = 0; TickSpoils(); }
			if (LevyUtil.IsServer && ++_snapshotTimer >= LevyConstants.SnapshotIntervalTicks) { _snapshotTimer = 0; SendRosterSnapshot(); }
		}

		private readonly List<BarracksTE> _spoilsTouched = new();

		/// <summary>
		/// Every <see cref="LevyConstants.SpoilsIntervalTicks"/>: dropped coins lying within <see cref="LevyConstants.SpoilsRadiusPx"/> of a living
		/// soldier body are removed and their copper value goes to that soldier's home barracks treasury. A coin inside any active
		/// player's pickup range is left alone. A soldier with no home barracks leaves the coin. Server / single player only.
		/// </summary>
		private void TickSpoils()
		{
			if (LevyUtil.IsClient) return;
			var cfg = LevyConfig.Get;
			if (cfg == null || !cfg.Spoils || _bodies.Count == 0) return;
			float r2 = LevyConstants.SpoilsRadiusPx * LevyConstants.SpoilsRadiusPx;
			bool looted = false;
			Vector2 soundAt = default;
			_spoilsTouched.Clear();
			for (int i = 0; i < Main.maxItems; i++)
			{
				var item = Main.item[i];
				if (!item.active || item.stack <= 0 || !item.IsACoin || item.noGrabDelay > 0) continue;
				long value = EconomySystem.CoinValue(item);
				if (value <= 0 || NearAnyPlayer(item)) continue;
				Vector2 c = item.Center;
				for (int b = 0; b < _bodies.Count; b++)
				{
					var s = _bodies[b];
					if (!s.IsAlive || Vector2.DistanceSquared(s.NPC.Center, c) > r2) continue;
					var home = s.Home;
					if (home == null) continue;
					EconomySystem.Deposit(home, value);
					if (!_spoilsTouched.Contains(home)) _spoilsTouched.Add(home);
					soundAt = c;
					looted = true;
					item.TurnToAir();
					if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncItem, -1, -1, null, i);
					break;
				}
			}
			for (int k = 0; k < _spoilsTouched.Count; k++) SyncBarracks(_spoilsTouched[k]);
			_spoilsTouched.Clear();
			if (looted && _spoilsSoundCd <= 0)
			{
				_spoilsSoundCd = LevyConstants.SpoilsSoundCooldownTicks;
				if (Main.netMode == NetmodeID.Server)
				{
					if (SoundID.IndexByName != null && SoundID.IndexByName.TryGetValue("CoinPickup", out ushort idx))
						NetMessage.PlayNetSound(new NetMessage.NetSoundInfo(soundAt, idx));
				}
				else SoundEngine.PlaySound(SoundID.CoinPickup, soundAt);
			}
		}

		/// <summary>True when the item is inside a player's normal pickup reach (their body padded by the vanilla grab range, magnets included).</summary>
		private static bool NearAnyPlayer(Item item)
		{
			Rectangle box = item.Hitbox;
			for (int p = 0; p < Main.maxPlayers; p++)
			{
				var pl = Main.player[p];
				if (!pl.active || pl.dead) continue;
				int g = pl.GetItemGrabRange(item);
				if (new Rectangle((int)pl.position.X - g, (int)pl.position.Y - g, pl.width + g * 2, pl.height + g * 2).Intersects(box)) return true;
			}
			return false;
		}

		/// <summary>Growth crystals a soldier will draw from the home Armory's storage, in the order they are tried.</summary>
		private static readonly int[] AutoFeedItems = { ItemID.LifeCrystal, ItemID.LifeFruit, ItemID.ManaCrystal };

		/// <summary>
		/// Every <see cref="LevyConstants.AutoFeedIntervalTicks"/>: each living soldier whose home Armory storage holds a growth crystal
		/// it can still absorb takes ONE (at most one per soldier per check). Server / single player only.
		/// </summary>
		private void TickAutoFeed()
		{
			if (LevyUtil.IsClient) return;
			var cfg = LevyConfig.Get;
			if (cfg == null || !cfg.AutoFeedCrystals) return;
			bool changed = false;
			for (int i = 0; i < Regulars.Count; i++)
			{
				var rec = Regulars[i];
				if (rec == null || rec.Dead) continue;
				var armory = ArmoryOf(HomeOf(rec));
				if (armory == null) continue;
				for (int k = 0; k < AutoFeedItems.Length; k++)
				{
					int type = AutoFeedItems[k];
					if (!CanAbsorbCrystal(rec, type) || !armory.TryTakeOne(type)) continue;
					if (!ApplyCrystal(rec, type)) { armory.Deposit(new Item(type)); break; } // refused after all: the crystal goes back
					changed = true;
					var body = BodyOf(rec);
					PlayCrystalSound(type, body != null ? body.NPC.Center : armory.Center);
					break;
				}
			}
			if (changed) SendRosterSnapshot();
		}

		/// <summary>True when the record can still absorb one of this crystal (life crystal / life fruit by grade and HP cap, mana crystal by the mana cap).</summary>
		public static bool CanAbsorbCrystal(SoldierRecord rec, int itemType)
		{
			if (rec == null) return false;
			switch (itemType)
			{
				case ItemID.LifeCrystal:
				{
					int cap = LevyConstants.MaxLifeCrystalsByGrade[Math.Clamp((int)rec.Grade, 0, LevyConstants.MaxLifeCrystalsByGrade.Length - 1)];
					return rec.LifeCrystals < cap && rec.MaxHp < LevyConstants.GradeHpCap[(int)rec.Grade];
				}
				case ItemID.LifeFruit:
					return rec.LifeFruit < LevyConstants.MaxLifeFruit && rec.MaxHp < LevyConstants.GradeHpCap[(int)rec.Grade];
				case ItemID.ManaCrystal:
					return rec.MaxMana < LevyConstants.MaxMana;
				default:
					return false;
			}
		}

		/// <summary>The vanilla use sound for a growth crystal (Item4 for Life Crystal / Life Fruit, Item29 for Mana Crystal) and its SoundID field name.</summary>
		public static SoundStyle CrystalSound(int itemType, out string soundName)
		{
			if (itemType == ItemID.ManaCrystal) { soundName = "Item29"; return SoundID.Item29; }
			soundName = "Item4";
			return SoundID.Item4;
		}

		/// <summary>Plays the crystal use sound: locally in single player, broadcast from the server in multiplayer. A client never calls this.</summary>
		public static void PlayCrystalSound(int itemType, Vector2 position)
		{
			var style = CrystalSound(itemType, out string name);
			if (Main.netMode == NetmodeID.Server)
			{
				if (SoundID.IndexByName != null && SoundID.IndexByName.TryGetValue(name, out ushort idx))
					NetMessage.PlayNetSound(new NetMessage.NetSoundInfo(position, idx));
			}
			else SoundEngine.PlaySound(style, position);
		}

		private void ResolveCommanders()
		{
			_onlineByName.Clear();
			for (int i = 0; i < Main.maxPlayers; i++) if (Main.player[i].active && !string.IsNullOrEmpty(Main.player[i].name)) _onlineByName[Main.player[i].name] = i;
			foreach (var rec in Regulars) rec.CommanderIndex = !string.IsNullOrEmpty(rec.CommanderName) && _onlineByName.TryGetValue(rec.CommanderName, out int idx) ? idx : -1;
		}

		private void TickRespawns(int ticks)
		{
			bool changed = false;
			for (int i = 0; i < Regulars.Count; i++)
			{
				var rec = Regulars[i];
				if (!rec.Dead) continue;
				if (rec.RespawnTimer > 0) { rec.RespawnTimer -= ticks; if (rec.RespawnTimer > 0) continue; rec.RespawnTimer = 0; }
				var home = HomeOf(rec);
				if (home == null) { TryRebase(rec, null); home = HomeOf(rec); if (home == null) continue; }
				if (home.Manpower <= 0) continue; // keep waiting; re-checked every deploy tick
				rec.Dead = false; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Fear = 0f;
				ArmoryOf(home)?.IssueGear(rec);
				SpawnBody(rec, home.MusterPoint);
				SendToPlayer(OnlineCommander(rec), LevyUtil.T("Roster.ReturnsToDuty", rec.Name));
				changed = true;
			}
			if (changed) SendRosterSnapshot();
		}

		private void Deploy()
		{
			_deployScratch.Clear();
			foreach (var rec in Regulars) if (!rec.Dead && !rec.HasBody) _deployScratch.Add(rec);
			if (_deployScratch.Count == 0) return;
			_deployScratch.Sort(DeployOrder);
			for (int i = 0; i < _deployScratch.Count; i++)
			{
				var rec = _deployScratch[i];
				if (!SpawnPositionFor(rec, out var pos)) continue;
				if (SpawnBody(rec, pos) < 0) break; // render cap or no NPC slot: the rest wait
			}
			_deployScratch.Clear();
		}

		private static bool SpawnPositionFor(SoldierRecord rec, out Vector2 pos)
		{
			var commander = OnlineCommander(rec);
			var home = HomeOf(rec);
			if (commander != null && (rec.Order == SoldierOrder.Follow || home == null))
			{
				pos = Movement.FollowerTravel.FindStandingSpot(commander.Center, LevyConstants.SpawnNearCommanderRadius, out var spot) ? spot : commander.Bottom;
				return true;
			}
			if (home != null) { pos = home.MusterPoint; return true; }
			pos = default;
			return false;
		}

		private void TickDeserters()
		{
			for (int i = _deserters.Count - 1; i >= 0; i--)
			{
				var d = _deserters[i];
				var body = d.Body;
				bool live = body != null && body.NPC.active && body.NPC.whoAmI >= 0 && body.NPC.whoAmI < Main.maxNPCs && ReferenceEquals(Main.npc[body.NPC.whoAmI].ModNPC, body);
				if (!live) { _deserters.RemoveAt(i); continue; }
				if (--d.Ticks > 0) continue;
				DespawnBody(body);
				_deserters.RemoveAt(i);
			}
		}

		public virtual void OnBodyDied(SoldierNPC body, int killerNpcType, string killerName)
		{
			if (LevyUtil.IsClient || body == null) return;
			var rec = body.Record;
			if (rec == null) return;
			var npc = body.NPC;
			var home = HomeOf(rec);
			var commander = OnlineCommander(rec);

			byte difficulty = commander != null ? commander.difficulty : PlayerDifficultyID.SoftCore;
			if (difficulty == PlayerDifficultyID.Creative) difficulty = PlayerDifficultyID.SoftCore;
			bool hardcore = difficulty == PlayerDifficultyID.Hardcore;
			bool mediumcore = difficulty == PlayerDifficultyID.MediumCore;

			// ---- drops where they fell ----
			var source = npc.GetSource_Death();
			var box = npc.Hitbox;
			if (hardcore || mediumcore) DropAllGear(rec, source, box);
			else
			{
				var pouch = rec.Gear.Ammo;
				if (pouch != null && !pouch.IsAir && pouch.stack >= 2)
				{
					int half = pouch.stack / 2;
					var drop = pouch.Clone(); drop.stack = half;
					Item.NewItem(source, box, drop);
					pouch.stack -= half;
				}
			}

			// ---- manpower ----
			if (home != null) { home.Manpower = Math.Max(0, home.Manpower - 1); SyncBarracks(home); }

			var ctx = new DialogueContext
			{
				Killer = KillerText(killerNpcType, killerName),
				Wage = LevyUtil.Coins(EconomySystem.WagePerDay(rec.Grade, false)),
				Player = commander?.name ?? rec.CommanderName,
				Town = SettlementSystem.Instance.TownName(home),
			};

			rec.Dead = true; rec.BodyIndex = -1; rec.CurrentHp = 0; rec.Fear = 0f;
			rec.LastDeathTick = (int)Main.GameUpdateCount;
			if (rec.Kind == SoldierKind.Regular)
			{
				rec.RespawnTimer = LevyConstants.RegularRespawnTicks;
				if (hardcore && Regulars.Remove(rec)) Memorial.Add(rec);
				if (LevyConfig.Get.RegularDeathMessages) DialogueSystem.ChatLog(DialogueTrigger.DEATH_MESSAGE, rec, ctx);
				if (hardcore) LevyUtil.Chat(LevyUtil.T("Roster.Memorial", rec.Name));
			}
			else
			{
				switch (LevyConfig.Get.MilitiaDeathMessages)
				{
					case MilitiaDeathMessageMode.Individual: DialogueSystem.ChatLog(DialogueTrigger.DEATH_MESSAGE, rec, ctx); break;
					// Count mode: SettlementSystem.OnMilitiaBodyDied owns MilitiaLostThisEvent and reports it at stand down.
				}
				SettlementSystem.Instance.OnMilitiaBodyDied(home, rec, body);
			}

			bool tombstone = LevyConfig.Get.Tombstones switch { TombstoneMode.Always => true, TombstoneMode.Vanilla => hardcore, _ => false };
			if (tombstone) DropTombstone(body, rec, ctx);

			FearSystem.OnComradeDied(body);
			npc.life = 0;
			if (rec.Kind == SoldierKind.Regular) SendRosterSnapshot(); // militia are not in the regulars list
		}

		private static string KillerText(int killerNpcType, string killerName)
		{
			if (!string.IsNullOrWhiteSpace(killerName)) return killerName;
			if (killerNpcType > 0 && killerNpcType < NPCLoader.NPCCount)
			{
				string n = Lang.GetNPCNameValue(killerNpcType);
				if (!string.IsNullOrWhiteSpace(n)) return n;
			}
			return killerNpcType switch
			{
				LevyConstants.EnvKillerLava => LevyUtil.T("Roster.KillerLava"),
				LevyConstants.EnvKillerDrowning => LevyUtil.T("Roster.KillerDrowning"),
				LevyConstants.EnvKillerFall => LevyUtil.T("Roster.KillerFall"),
				_ => LevyUtil.T("Roster.KillerUnknown"),
			};
		}

		/// <summary>Replicates Player.DropTombstone for a soldier: a cheap-tier tombstone projectile carrying the death line.</summary>
		private static void DropTombstone(SoldierNPC body, SoldierRecord rec, DialogueContext ctx)
		{
			var npc = body.NPC;
			string line = DialogueSystem.PickLine(DialogueTrigger.DEATH_MESSAGE, rec, ctx);
			if (string.IsNullOrWhiteSpace(line)) line = LevyUtil.T("Roster.Fell", rec.Name);
			float drift;
			for (drift = Main.rand.Next(-35, 36) * 0.1f; drift < 2f && drift > -2f; drift += Main.rand.Next(-30, 31) * 0.1f) { }
			int kind = Main.rand.Next(6);
			int type = kind != 0 ? 200 + kind : ProjectileID.Tombstone; // GraveMarker..Obelisk (201-205) or the plain Tombstone (43)
			int dir = npc.direction == 0 ? 1 : -npc.direction;
			int owner = Main.myPlayer;
			float ai0 = rec.CommanderIndex >= 0 ? rec.CommanderIndex : owner;
			int idx = Projectile.NewProjectile(npc.GetSource_Death(), npc.Center.X, npc.Center.Y,
				Main.rand.Next(10, 30) * 0.1f * dir + drift, Main.rand.Next(-40, -20) * 0.1f, type, 0, 0f, owner, ai0);
			if (idx < 0 || idx >= Main.projectile.Length) return;
			DateTime now = DateTime.Now;
			string date = GameCulture.FromCultureName(GameCulture.CultureName.English).IsActive ? now.ToString("MMMM d, yyy") : now.ToString("D");
			Main.projectile[idx].miscText = line + "\n" + date;
		}

		public virtual void OnBodyDeserted(SoldierNPC body)
		{
			if (LevyUtil.IsClient || body == null) return;
			DesertBookkeeping(body);
			SendRosterSnapshot();
		}

		private void DesertBookkeeping(SoldierNPC body)
		{
			var rec = body.Record;
			if (rec != null) Regulars.Remove(rec);
			foreach (var d in _deserters) if (ReferenceEquals(d.Body, body)) return;
			_deserters.Add(new Deserter { Body = body, Ticks = LevyConstants.DesertDespawnTicks });
		}

		// =====================================================================================================
		// Dawn: payday, desertion, daily flags, settlements, taxes
		// =====================================================================================================
		public override void PostUpdateTime()
		{
			if (LevyUtil.IsClient) return;
			bool day = Main.dayTime;
			if (!_dawnInit) { _dawnInit = true; _lastDayTime = day; return; }
			if (day && !_lastDayTime) OnDawn();
			_lastDayTime = day;
		}

		private void OnDawn()
		{
			DayNumber++;
			ResolveCommanders();

			var payers = new Dictionary<int, BarracksTE>();
			var wages = new Dictionary<int, long>();
			var members = new Dictionary<int, List<SoldierRecord>>();
			var orphans = new List<SoldierRecord>();
			foreach (var rec in Regulars)
			{
				var te = PayingBarracks(rec);
				if (te == null) { orphans.Add(rec); continue; }
				if (!payers.ContainsKey(te.ID)) { payers[te.ID] = te; wages[te.ID] = 0; members[te.ID] = new List<SoldierRecord>(); }
				wages[te.ID] += EconomySystem.WagePerDay(rec.Grade, rec.StoodDown);
				members[te.ID].Add(rec);
			}
			foreach (var kv in payers)
			{
				var te = kv.Value; long due = wages[kv.Key]; var list = members[kv.Key];
				if (te.Treasury >= due)
				{
					te.Treasury -= due;
					foreach (var r in list) { r.DaysUnpaid = 0; var b = BodyOf(r); if (b != null) DialogueSystem.Bark(b, DialogueTrigger.PAYDAY); }
					if (due > 0) LevyUtil.Chat(LevyUtil.T("Chat.Payday", LevyUtil.Coins(due)));
				}
				else
				{
					foreach (var r in list) { r.DaysUnpaid++; var b = BodyOf(r); if (b != null) DialogueSystem.Bark(b, DialogueTrigger.UNPAID); }
					LevyUtil.Chat(LevyUtil.T("Chat.Unpaid", SettlementSystem.Instance.TownName(te)));
				}
				SyncBarracks(te);
			}
			foreach (var r in orphans) r.DaysUnpaid++; // nobody can pay a homeless regular whose commander is away

			// desertion rolls
			for (int i = Regulars.Count - 1; i >= 0; i--)
			{
				var r = Regulars[i];
				if (r.DaysUnpaid < 2) continue;
				float chance = LevyConstants.DesertionChanceByDaysUnpaid[Math.Min(r.DaysUnpaid, LevyConstants.DesertionChanceByDaysUnpaid.Length - 1)];
				if (chance <= 0f || Main.rand.NextFloat() >= chance) continue;
				Desert(r);
			}

			foreach (var r in Regulars) r.HazardPaid = false;
			SettlementSystem.Instance.OnDawn(DayNumber);
			RouteTaxes();
			SendRosterSnapshot();
		}

		private void Desert(SoldierRecord rec)
		{
			string weapon = rec.Gear.HasWeapon ? rec.Gear.Weapon.Name : LevyUtil.T("Roster.Nothing");
			LevyUtil.Chat(LevyUtil.T("Chat.Deserted", rec.Name, weapon));
			var body = BodyOf(rec);
			if (body != null)
			{
				DialogueSystem.Bark(body, DialogueTrigger.DESERTED);
				body.State = SoldierState.Desert; body.StateTimer = 0; body.TargetIndex = -1;
				body.NPC.netUpdate = true;
				DesertBookkeeping(body); // the Brain walks the body off the map; it is force-despawned after DesertDespawnTicks
			}
			else Regulars.Remove(rec);
		}

		/// <summary>Single player only: a housed Tax Collector's take goes to a barracks with TaxToTreasury. In multiplayer taxMoney is per client and stays with the player.</summary>
		private static void RouteTaxes()
		{
			if (Main.netMode != NetmodeID.SinglePlayer) return;
			var player = Main.LocalPlayer;
			if (player == null || !player.active || player.taxMoney <= 0) return;
			foreach (var te in SettlementSystem.Instance.AllBarracks())
			{
				if (!te.TaxToTreasury) continue;
				bool housed = false;
				foreach (var n in SettlementSystem.Instance.TownNpcsOf(te)) if (n != null && n.type == NPCID.TaxCollector) { housed = true; break; }
				if (!housed) continue;
				long amount = player.taxMoney;
				te.Treasury += amount;
				player.taxMoney = 0;
				LevyUtil.Chat(LevyUtil.T("Roster.TaxCollected", LevyUtil.Coins(amount), SettlementSystem.Instance.TownName(te)));
				break;
			}
		}

		// =====================================================================================================
		// Orders
		// =====================================================================================================
		private bool Selected(SoldierRecord rec, Player commander, bool atBarracks, out SoldierNPC body)
		{
			body = null;
			if (rec.CommanderName != commander.name) return false;
			body = BodyOf(rec);
			if (atBarracks) return true;
			return body != null && LevyUtil.WithinTiles(body.NPC.Center, commander.Center, LevyConstants.OrderRadius);
		}

		public virtual void IssueOrder(Player commander, SoldierOrder order, bool atBarracks = false)
		{
			if (LevyUtil.IsClient || commander == null) return;
			bool any = false;
			foreach (var rec in Regulars)
			{
				if (!Selected(rec, commander, atBarracks, out var body)) continue;
				rec.Order = order;
				rec.StoodDown = order == SoldierOrder.ReturnToBarracks;
				if (order == SoldierOrder.Hold && body == null) rec.HoldPos = commander.Center;
				body?.ReceiveOrder(order);
				any = true;
			}
			if (any) SendRosterSnapshot();
		}

		public virtual void ToggleHoldFire(Player commander, bool atBarracks = false)
		{
			if (LevyUtil.IsClient || commander == null) return;
			int total = 0, holding = 0;
			foreach (var rec in Regulars) if (Selected(rec, commander, atBarracks, out _)) { total++; if (rec.HoldFire) holding++; }
			if (total == 0) return;
			bool set = holding * 2 <= total; // majority holding fire → release; otherwise hold
			foreach (var rec in Regulars)
			{
				if (!Selected(rec, commander, atBarracks, out var body)) continue;
				rec.HoldFire = set;
				if (body != null) { body.NPC.netUpdate = true; if (set) DialogueSystem.Bark(body, DialogueTrigger.HOLD_FIRE); }
			}
			SendToPlayer(commander, LevyUtil.T(set ? "UI.HoldFire" : "UI.FireAtWill"));
			SendRosterSnapshot();
		}

		/// <summary>Toggle the wide-formation stance for the commander's soldiers (same majority rule as HoldFire).</summary>
		public virtual void ToggleSpreadOut(Player commander, bool atBarracks = false)
		{
			if (LevyUtil.IsClient || commander == null) return;
			int total = 0, spread = 0;
			foreach (var rec in Regulars) if (Selected(rec, commander, atBarracks, out _)) { total++; if (rec.SpreadOut) spread++; }
			if (total == 0) return;
			bool set = spread * 2 <= total; // majority already spread → close up; otherwise spread
			foreach (var rec in Regulars)
			{
				if (!Selected(rec, commander, atBarracks, out var body)) continue;
				rec.SpreadOut = set;
				if (body != null) body.NPC.netUpdate = true;
			}
			SendToPlayer(commander, LevyUtil.T(set ? "UI.SpreadOut" : "UI.CloseUp"));
			SendRosterSnapshot();
		}

		// ---- focus target (commander-scoped, runtime only, never saved) ----
		private static readonly Dictionary<string, (int id, int type)> _focusTargets = new();

		/// <summary>Server / single player: the commander's soldiers focus this hostile NPC. -1, an invalid NPC or the same NPC again clears it.</summary>
		public virtual void SetFocusTarget(Player commander, int npcId)
		{
			if (LevyUtil.IsClient || commander == null) return;
			string who = commander.name;
			if (npcId < 0 || npcId >= Main.maxNPCs || !Main.npc[npcId].active || Targeting.SoldierTargeting.IsNeverTarget(Main.npc[npcId]))
			{
				if (_focusTargets.Remove(who)) SendToPlayer(commander, LevyUtil.T("UI2.FocusCleared"));
				return;
			}
			var npc = Main.npc[npcId];
			if (_focusTargets.TryGetValue(who, out var cur) && cur.id == npcId && cur.type == npc.type)
			{
				_focusTargets.Remove(who);
				SendToPlayer(commander, LevyUtil.T("UI2.FocusCleared"));
				return;
			}
			_focusTargets[who] = (npcId, npc.type);
			SendToPlayer(commander, LevyUtil.T("UI2.FocusSet", npc.GivenOrTypeName));
		}

		/// <summary>The NPC index this commander's soldiers should focus, or -1. Clears itself when the NPC died, was replaced in its slot, or stopped being a legal target.</summary>
		public static int FocusTargetOf(string commanderName)
		{
			if (string.IsNullOrEmpty(commanderName) || !_focusTargets.TryGetValue(commanderName, out var f)) return -1;
			if (f.id >= 0 && f.id < Main.maxNPCs)
			{
				var n = Main.npc[f.id];
				if (n.active && n.type == f.type && n.life > 0 && !Targeting.SoldierTargeting.IsNeverTarget(n)) return f.id;
			}
			_focusTargets.Remove(commanderName);
			return -1;
		}

		public static void ClearFocusTargets() => _focusTargets.Clear();

		public virtual void Rally(Player commander)
		{
			if (LevyUtil.IsClient || commander == null) return;
			if (RallyCooldown > 0) { SendToPlayer(commander, LevyUtil.T("Roster.RallyCooldown", (RallyCooldown + 59) / 60)); return; }
			FearSystem.Rally(commander.Center, LevyConstants.RallyRadius);
			RallyCooldown = LevyConstants.RallyCooldownTicks;
			LevyUtil.Chat(LevyUtil.T("Chat.Rallied"));
		}

		/// <summary>
		/// Re-arm order (server / single player): every living Regular of this commander and every fielded militiaman returns
		/// their kit to the home Armory, then all of them draw again best-first from the current issue slots. Returning
		/// everyone before anyone draws means the whole muster re-deals from one pool. Soldiers with no reachable Armory
		/// keep what they carry. Deployed bodies are told to re-read their weapon profile.
		/// </summary>
		public virtual void Rearm(Player commander)
		{
			if (LevyUtil.IsClient || commander == null) return;
			var batch = new List<(ArmoryTE armory, SoldierRecord rec)>();
			foreach (var rec in Regulars)
			{
				if (rec.CommanderName != commander.name || rec.Dead) continue;
				var armory = ArmoryOf(HomeOf(rec));
				if (armory != null) batch.Add((armory, rec));
			}
			foreach (var (armory, rec) in batch) armory.ReturnGear(rec.Gear);
			foreach (var (armory, rec) in batch)
			{
				armory.IssueGear(rec);
				RebindBody(BodyOf(rec), rec);
			}
			int total = batch.Count + SettlementSystem.Instance.RearmMilitia();
			SendToPlayer(commander, LevyUtil.T(total > 0 ? "Chat.Rearmed" : "Chat.RearmNone"));
			if (batch.Count > 0) SendRosterSnapshot();
		}

		public virtual void AssignPatrol(SoldierRecord rec, Point16 a, Point16 b)
		{
			if (LevyUtil.IsClient || rec == null) return;
			rec.PatrolA = a; rec.PatrolB = b;
			rec.Order = SoldierOrder.Patrol; rec.StoodDown = false;
			BodyOf(rec)?.ReceiveOrder(SoldierOrder.Patrol);
			SendRosterSnapshot();
		}

		// =====================================================================================================
		// Hooks for other modules
		// =====================================================================================================
		public virtual void OnMainBossKilled(NPC boss)
		{
			if (LevyUtil.IsClient || boss == null) return;
			// Defer crediting until the whole fight is over: the Twins are two main-boss roots and the Eater of Worlds' head
			// whoAmI shifts as it splits, so crediting per boss NPC would double-count the Twins and miss the Eater.
			if (Common.Fear.FearSystem.AnotherMainBossAlive(boss)) return;
			int veterans = 0;
			foreach (var r in Regulars) if (r.Veteran) veterans++;
			bool changed = false;
			foreach (var rec in Regulars)
			{
				if (rec.Dead || rec.EngagedBosses.Count == 0) continue; // engaged at least one main boss this fight
				rec.BossFightsSurvived++;
				changed = true;
				var body = BodyOf(rec);
				if (body != null) DialogueSystem.Bark(body, DialogueTrigger.VICTORY);
				if (!rec.Veteran && rec.BossFightsSurvived >= LevyConstants.VeteranBossFights && veterans < LevyConstants.VeteranMaxFraction * Regulars.Count)
				{
					rec.Veteran = true; veterans++;
					rec.Name = rec.RankPrefix + " " + BaseName(rec.Name);
					if (body != null) { body.NPC.GivenName = rec.Name; body.NPC.netUpdate = true; DialogueSystem.Bark(body, DialogueTrigger.VETERAN); }
					LevyUtil.Chat(LevyUtil.T("Chat.Veteran", rec.Name));
				}
			}
			foreach (var rec in Regulars) rec.EngagedBosses.Clear();
			if (changed) SendRosterSnapshot();
		}

		public virtual void OnCommanderDied(Player commander)
		{
			if (LevyUtil.IsClient || commander == null) return;
			IssueOrder(commander, SoldierOrder.ReturnToBarracks, atBarracks: true);
		}

		// =====================================================================================================
		// Multiplayer
		// =====================================================================================================
		/// <summary>Server: send the whole regulars list to every client (display-only copies).</summary>
		public void SendRosterSnapshot()
		{
			if (!LevyUtil.IsServer) return;
			var p = LevyPackets.New(LevyPacket.RosterSnapshot);
			WriteRoster(p);
			p.Send();
		}

		private void WriteRoster(BinaryWriter writer)
		{
			writer.Write(Regulars.Count);
			foreach (var r in Regulars) r.WriteSnapshot(writer);
		}

		/// <summary>Client: replace the display-only regulars list with a snapshot.</summary>
		internal void ReadRoster(BinaryReader reader)
		{
			int n = reader.ReadInt32();
			if (n < 0 || n > LevyConstants.MaxSnapshotRecords) return;
			Regulars.Clear();
			for (int i = 0; i < n; i++) Regulars.Add(SoldierRecord.ReadSnapshot(reader));
		}

		public override void NetSend(BinaryWriter writer) => WriteRoster(writer);
		public override void NetReceive(BinaryReader reader) => ReadRoster(reader);

		// =====================================================================================================
		// Persistence
		// =====================================================================================================
		public override void SaveWorldData(TagCompound tag)
		{
			var list = new List<TagCompound>(); foreach (var r in Regulars) list.Add(r.Save()); tag["regulars"] = list;
			var mem = new List<TagCompound>(); foreach (var r in Memorial) mem.Add(r.Save()); tag["memorial"] = mem;
			tag["nextId"] = NextId; tag["day"] = DayNumber;
			var deadIds = new List<int>(); var deadTimers = new List<int>();
			foreach (var r in Regulars) if (r.Dead) { deadIds.Add(r.Id); deadTimers.Add(r.RespawnTimer); }
			tag["deadIds"] = deadIds; tag["deadTimers"] = deadTimers;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Regulars.Clear(); Memorial.Clear();
			foreach (var t in tag.GetList<TagCompound>("regulars")) Regulars.Add(SoldierRecord.Load(t));
			foreach (var t in tag.GetList<TagCompound>("memorial")) Memorial.Add(SoldierRecord.Load(t));
			NextId = tag.GetInt("nextId"); if (NextId < 1) NextId = 1; DayNumber = tag.GetInt("day");
			foreach (var r in Regulars) if (r.Id >= NextId) NextId = r.Id + 1;
			foreach (var r in Memorial) if (r.Id >= NextId) NextId = r.Id + 1;
			var deadIds = tag.GetList<int>("deadIds"); var deadTimers = tag.GetList<int>("deadTimers");
			for (int i = 0; i < deadIds.Count; i++)
			{
				var r = FindById(deadIds[i]);
				if (r == null) continue;
				r.Dead = true; r.RespawnTimer = i < deadTimers.Count ? Math.Max(0, deadTimers[i]) : 0; r.CurrentHp = 0;
			}
			ResetRuntime();
		}

		private void ResetRuntime()
		{
			foreach (var r in Regulars) { r.BodyIndex = -1; r.CommanderIndex = -1; r.EngagedBosses.Clear(); r.Fear = 0f; }
			foreach (var r in Memorial) { r.BodyIndex = -1; r.CommanderIndex = -1; r.Dead = true; }
			_bodies.Clear(); _deserters.Clear(); _deployScratch.Clear(); _onlineByName.Clear();
			_deployTimer = 0; _snapshotTimer = 0; _feedTimer = 0; _spoilsTimer = 0; _spoilsSoundCd = 0; RallyCooldown = 0; _focusTargets.Clear();
			_dawnInit = false; _lastDayTime = Main.dayTime;
		}

		public override void OnWorldLoad()
		{
			// tModLoader renumbers tile-entity IDs on every world load (WorldFile.cs: tileEntity.ID = num2++), so the saved
			// HomeBarracksId is stale. Re-resolve each home from its saved tile position, which is stable.
			var set = SettlementSystem.Instance;
			void Rehome(SoldierRecord r) { if (r == null) return; if (r.HomePos != Terraria.DataStructures.Point16.NegativeOne) { var b = set.BarracksAt(r.HomePos); r.HomeBarracksId = b?.ID ?? -1; } else r.HomeBarracksId = -1; }
			foreach (var r in Regulars) Rehome(r);
			foreach (var r in Memorial) Rehome(r);
			ResetRuntime();
		}

		public override void ClearWorld()
		{
			Regulars.Clear(); Memorial.Clear(); NextId = 1; DayNumber = 0;
			ResetRuntime();
		}
	}
}
