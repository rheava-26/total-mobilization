using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Officers;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// AIM AUDIT of conjured creatures, a part of <see cref="OfficerWorldCheck"/>. With a real player (slot 0, the commander) standing 8 tiles BEHIND the officer and a pinned enemy 10 tiles in front, one creature of a type is
	/// conjured and the world steps <see cref="FightTicks"/> ticks; then the enemy is removed and it steps <see cref="IdleTicks"/> more with the player at the officer's side. Every projectile that appears next to the creature is
	/// classified by where its first-tick velocity points (at the enemy, at the player, elsewhere), and whether the creature's own targeting ever pointed at a real player is counted per tick.
	/// A creature passes only when its attacks hurt the enemy, none of its shots flew toward the player, none was fired at nothing, and its AI never targeted a real player.
	/// </summary>
	public partial class OfficerWorldCheck
	{
		private const int FightTicks = 600, IdleTicks = 300;

		private sealed class AuditRow
		{
			public int Type; public string Name; public int Ai;
			public int Utility, TypeChanges, OfficerHurt, ChildBlows, Children, ChildrenHostile, ChildrenToPlayer, ChildrenToEnemy, ChildrenOther, ChildrenReachedPlayer, ChildrenReachedEnemy, PlayerHurtTicks, Unflagged, UnflaggedHostile, Shots, ToEnemy, ToPlayer, Other, Static, IdleShots, IdleToPlayer, PlayerTargetTicks, EnemyDmg, ProjHits, ProjDmg;
			public bool Alive = true, Conjured = true; public string Why = "";
			public bool Pass => Conjured && Alive && EnemyDmg > 0 && (Shots + Children == 0 || ProjHits + ChildBlows > 0) && ToPlayer == 0 && IdleShots == 0 && UnflaggedHostile == 0 && ChildrenHostile == 0 && ChildrenToPlayer == 0 && ChildrenReachedPlayer == 0 && PlayerHurtTicks == 0 && OfficerHurt == 0 && PlayerTargetTicks == 0;
			public override string ToString() =>
				$"{Name} (type {Type}, ai {Ai}): shots={Shots} toEnemy={ToEnemy} toPlayer={ToPlayer} other={Other} static={Static}; idleShots={IdleShots} idleToPlayer={IdleToPlayer}; ticksTargetingRealPlayer={PlayerTargetTicks}; unflaggedShots={Unflagged} (hostile {UnflaggedHostile}); spawnedNpcs={Children} (hostile {ChildrenHostile}, toEnemy {ChildrenToEnemy}, toPlayer {ChildrenToPlayer}, other {ChildrenOther}, reachedPlayer {ChildrenReachedPlayer}, reachedEnemy {ChildrenReachedEnemy}); playerHurtTicks={PlayerHurtTicks}; orbBlows={ChildBlows}; utilityNpcs={Utility}; typeChanges={TypeChanges}; officerHurtTicks={OfficerHurt}; enemyDamage={EnemyDmg} (by projectile {ProjDmg} in {ProjHits} hits); alive={Alive}{(Why.Length > 0 ? " " + Why : "")}";
		}

		private static float AngleBetween(Vector2 a, Vector2 b)
		{
			if (a.LengthSquared() < 1e-6f || b.LengthSquared() < 1e-6f) return 180f;
			return MathHelper.ToDegrees((float)Math.Acos(MathHelper.Clamp(Vector2.Dot(Vector2.Normalize(a), Vector2.Normalize(b)), -1f, 1f)));
		}

		/// <summary>Runs the aim audit for one creature type; the owner avatar must be marching. Returns the result row.</summary>
		private static AuditRow AuditType(SoldierNPC avatar, int type, string name, int ai, int fightTicks, int idleTicks)
		{
			var row = new AuditRow { Type = type, Name = name, Ai = ai };
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			for (int i = 0; i < Main.maxNPCs; i++) if (ConjuredGlobalNPC.IsConjured(Main.npc[i])) Main.npc[i].active = false; // the last type's leftovers (its thrown orbs)
			_auditKids.Clear();
			SpawnEnemy(10);
			avatar.NPC.Bottom = new Vector2(Cx * 16f - 40f, FloorY * 16f); avatar.NPC.velocity = Vector2.Zero;
			Vector2 eveBehind = new Vector2(Cx * 16f - 40f - 8 * 16f, (FloorY - 3) * 16f), eveBeside = new Vector2(Cx * 16f - 40f - 16f, (FloorY - 3) * 16f);
			_cmdAt = eveBehind;
			int idx = ConjuredGlobalNPC.Conjure(avatar.NPC, 0, type);
			if (idx < 0) { row.Conjured = false; row.Why = "could not be conjured: " + ConjuredGlobalNPC.LastFail; KillEnemy(); return row; }
			NPC n = Main.npc[idx];
			var seen = new bool[Main.maxProjectiles];
			bool trace = Environment.GetEnvironmentVariable("LEVY_AUDIT_TRACE") == "1"; int traced = 0;
			var lastDesc = new Dictionary<int, string>();
			var live = new Dictionary<int, (int born, float minD, string desc)>();
			var kids = new Dictionary<int, (int born, int stage)>(); // whoAmI -> (tick born, 0 = not yet classified)
			int hurt0 = _commanderHurt, blows0 = ConjuredGlobalNPC.ChildBlows;
			int hits0 = ConjuredProjectile.HitsOnNpcs, dmg0 = ConjuredProjectile.DamageOnNpcs;
			int officerLife = avatar.NPC.life, extensions = 0, wasType = type;
			for (int tick = 0; tick < fightTicks + idleTicks; tick++)
			{
				if (tick == fightTicks && extensions < 6 && row.Shots + row.Children > 0 && (ConjuredProjectile.HitsOnNpcs - hits0) + (ConjuredGlobalNPC.ChildBlows - blows0) == 0) { extensions++; fightTicks += 600; } // it has thrown or shot and nothing has landed yet: give it more time to land one
				bool idle = tick >= fightTicks;
				if (tick == fightTicks) { KillEnemy(); for (int q = 0; q < Main.maxNPCs; q++) if (Main.npc[q].active && Main.npc[q].TryGetGlobalNPC(out HarnessTarget hz) && hz.IsTarget) { if (trace) Note("trace: a pinned enemy (slot " + q + ") survived KillEnemy; removed"); Main.npc[q].active = false; } _cmdAt = eveBeside; for (int i = 0; i < Main.maxProjectiles; i++) { Main.projectile[i].active = false; seen[i] = false; } }
				Step(1);
				if (trace && tick == fightTicks + 30) { var l = new List<string>(); for (int q = 0; q < Main.maxNPCs; q++) { NPC o = Main.npc[q]; if (o.active) l.Add($"{o.TypeName}#{q}(f={o.friendly},town={o.townNPC},d={o.damage},life={o.life}/{o.lifeMax},pinned={(o.TryGetGlobalNPC(out HarnessTarget hh) && hh.IsTarget)})"); } Note("trace idle NPCs: " + string.Join(" ", l)); }
				if (avatar.NPC.life < officerLife && avatar.NPC.life > 0) { row.OfficerHurt++; if (trace) Note($"trace {name}: tick {tick} the officer lost {officerLife - avatar.NPC.life} life (now {avatar.NPC.life}); creature at {(int)((n.Center.X - avatar.NPC.Center.X) / 16f)},{(int)((n.Center.Y - avatar.NPC.Center.Y) / 16f)} tiles from him; npcs near: " + string.Join(" ", Main.npc.Where(o => o.active && o.whoAmI != avatar.NPC.whoAmI && Vector2.Distance(o.Center, avatar.NPC.Center) < 400f).Select(o => $"{o.TypeName}#{o.whoAmI}(f={o.friendly},d={o.damage})"))); }
				officerLife = avatar.NPC.life;
				if (!avatar.NPC.active || avatar.NPC.life <= 0) { row.Why = "owner avatar lost at tick " + tick; break; }
				if (n.type != wasType) { row.TypeChanges++; if (trace) Note($"trace {name}: became {n.TypeName} at tick {tick}; conjured now = {ConjuredGlobalNPC.IsConjured(n)} friendly={n.friendly}"); wasType = n.type; }
				if (row.TypeChanges > 0 && !ConjuredGlobalNPC.IsConjured(n)) { row.Conjured = false; row.Why += " lost its conjuring when it changed form"; }
				if (!n.active) { row.Alive = false; row.Why += $" creature gone at tick {tick}"; break; }
				if (_target != null && _target.active && _target.life < _target.lifeMax) row.EnemyDmg += _target.lifeMax - _target.life;
				if (n.target != SoldierNPC.DummySlot && n.target != EnemyTargeting.FlyerSlot && n.target >= 0 && n.target < Main.maxPlayers && Main.player[n.target].active) { row.PlayerTargetTicks++; if (trace) Note($"trace {name}: tick {tick} npc.target={n.target} (a real player) pos={(int)((n.Center.X - _cmdAt.X) / 16f)},{(int)((n.Center.Y - _cmdAt.Y) / 16f)} tiles from the player"); }
				Player eve = Main.player[0];
				if (trace && Environment.GetEnvironmentVariable("LEVY_AUDIT_AI") == "1" && tick % 40 == 0) Note($"trace {name}: tick {tick} ai=[{n.ai[0]:0},{n.ai[1]:0},{n.ai[2]:0},{n.ai[3]:0}] pos={(int)((n.Center.X - _targetAt.X) / 16f)},{(int)((n.Center.Y - _targetAt.Y) / 16f)} from enemy vel=({n.velocity.X:0.#},{n.velocity.Y:0.#}) target={n.target} noGravity={n.noGravity} npcs={Main.npc.Count(z => z.active)}");
				AuditChildren(row, n, avatar.NPC, kids, tick, idle, trace, name);
				if (trace) foreach (int key in live.Keys.ToList())
				{
					Projectile lp = Main.projectile[key & 1023];
					bool gone = !lp.active || lp.identity != (key >> 10);
					if (gone) { var v = live[key]; Note($"trace {name}:   shot lived {tick - v.born} ticks, closest to enemy {(int)(v.minD / 16f * 10) / 10f} tiles; last seen {lastDesc[key]}"); live.Remove(key); }
					else if (_target != null && _target.active)
					{
						live[key] = (live[key].born, Math.Min(live[key].minD, Vector2.Distance(lp.Center, _target.Center)), live[key].desc);
						lastDesc[key] = $"pos=({(int)((lp.Center.X - _target.Center.X) / 16f * 10) / 10f},{(int)((lp.Center.Y - _target.Center.Y) / 16f * 10) / 10f}) from enemy, timeLeft={lp.timeLeft} penetrate={lp.penetrate} tileCollide={lp.tileCollide} inTile={Collision.SolidCollision(lp.position, lp.width, lp.height)} overlapsEnemy={lp.Hitbox.Intersects(_target.Hitbox)} enemyImmune0={_target.immune[0]} hostile={lp.hostile} friendly={lp.friendly}";
					}
				}
				for (int q = 0; q < Main.maxProjectiles; q++)
				{
					Projectile pr = Main.projectile[q];
					if (!pr.active) { seen[q] = false; continue; }
					if (seen[q]) continue;
					seen[q] = true;
					bool flagged = pr.TryGetGlobalProjectile(out ConjuredProjectile cp) && cp.Conjured;
					if (!flagged && (Vector2.Distance(pr.Center, n.Center) > 160f || (_avatarTypes != null && _avatarTypes.Contains(pr.type) && !pr.hostile))) continue; // somebody else's (the officer's own shot)
					if (!flagged) { row.Unflagged++; if (pr.hostile) row.UnflaggedHostile++; if (trace) Note($"trace {name}: UNFLAGGED proj {ProjectileID.Search.GetName(pr.type)} hostile={pr.hostile} friendly={pr.friendly} owner={pr.owner}"); }
					row.Shots++;
					if (trace && traced++ < 12)
					{
						float tE = _target != null && _target.active ? AngleBetween(pr.velocity, _target.Center - pr.Center) : -1f, tP = AngleBetween(pr.velocity, eve.Center - pr.Center);
						string d = $"proj {pr.type}={Terraria.ID.ProjectileID.Search.GetName(pr.type)} w={pr.width} timeLeft={pr.timeLeft} dmg={pr.damage} hostile={pr.hostile} friendly={pr.friendly} npcProj={pr.npcProj} owner={pr.owner} tick={tick}{(idle ? " IDLE" : "")} vel=({pr.velocity.X:0.#},{pr.velocity.Y:0.#}) angleToEnemy={tE:0} angleToPlayer={tP:0} grav={pr.aiStyle} | creature at ({(int)((n.Center.X - _targetAt.X) / 16f)},{(int)((n.Center.Y - _targetAt.Y) / 16f)}) tiles from the enemy's feet, player at ({(int)((eve.Center.X - _targetAt.X) / 16f)},{(int)((eve.Center.Y - _targetAt.Y) / 16f)}), npc.target={n.target} targetRect centre ({(int)((n.targetRect.Center.X - _targetAt.X) / 16f)},{(int)((n.targetRect.Center.Y - _targetAt.Y) / 16f)}) creatureTarget={n.GetGlobalNPC<ConjuredGlobalNPC>().Target} creatureVel=({n.velocity.X:0.#},{n.velocity.Y:0.#}) ai=[{n.ai[0]:0.#},{n.ai[1]:0.#},{n.ai[2]:0.#},{n.ai[3]:0.#}]";
						Note($"trace {name}: {d}");
						live[q | (pr.identity << 10)] = (tick, float.MaxValue, d); lastDesc[q | (pr.identity << 10)] = "";
					}
					if (idle)
					{
						row.IdleShots++;
						if (pr.velocity.Length() >= 1f && AngleBetween(pr.velocity, eve.Center - pr.Center) < 40f) row.IdleToPlayer++;
						continue;
					}
					if (pr.velocity.Length() < 1f) { row.Static++; continue; }
					if (_target == null || !_target.active) { row.Other++; continue; }
					if (Vector2.Distance(pr.Center, _target.Center) < 40f) { row.Static++; continue; } // fired from inside the enemy: no direction to judge
					float aE = AngleBetween(pr.velocity, _target.Center - pr.Center), aP = AngleBetween(pr.velocity, eve.Center - pr.Center);
					if (Math.Min(aE, aP) > 40f) row.Other++;
					else if (aP <= 12f && aP + 20f < aE) row.ToPlayer++;
					else row.ToEnemy++;
				}
			}
			row.PlayerHurtTicks = _commanderHurt - hurt0; row.ChildBlows = ConjuredGlobalNPC.ChildBlows - blows0;
			row.ProjHits = ConjuredProjectile.HitsOnNpcs - hits0; row.ProjDmg = ConjuredProjectile.DamageOnNpcs - dmg0;
			if (n.active) n.active = false;
			KillEnemy(); Step(2);
			return row;
		}

		private static HashSet<int> _avatarTypes;
		/// <summary>NPCs a creature under audit spawned (a caster's orbs): the stranger sweep must leave them be while they are watched.</summary>
		private static readonly HashSet<int> _auditKids = new(), _nearKids = new(); // _nearKids: thrown from within 10 tiles of the player (it was already beside her)

		/// <summary>Finds NPCs that appeared next to the creature this tick (not the officer, the enemy or a squad member), classifies each one's flight after its first AI tick, and tracks whether it reaches the player or the enemy.</summary>
		private static void AuditChildren(AuditRow row, NPC creature, NPC avatarNpc, Dictionary<int, (int born, int stage)> kids, int tick, bool idle, bool trace, string name)
		{
			Player eve = Main.player[0];
			for (int q = 0; q < Main.maxNPCs; q++)
			{
				NPC k = Main.npc[q];
				if (!k.active) { if (kids.Remove(q)) _auditKids.Remove(q); continue; }
				if (q == creature.whoAmI || q == avatarNpc.whoAmI || k == _target || kids.ContainsKey(q)) continue;
				if (k.ModNPC is SoldierNPC || k.townNPC) continue;
				if (k.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) continue;
				if (Vector2.Distance(k.Center, creature.Center) > 200f) continue;
				if (OfficerSystem.Data(k) != null && OfficerSystem.Data(k).Enrolled) continue;
				if (k.TryGetGlobalNPC(out ConjuredGlobalNPC cg0) && cg0.Active && !cg0.IsChild) continue; // a squad member, not something the creature threw
				kids[q] = (tick, 0); _auditKids.Add(q); if (Vector2.Distance(k.Center, eve.Center) < 160f) _nearKids.Add(q); else _nearKids.Remove(q);
				if (k.damage <= 0) { row.Utility++; if (!k.friendly) row.ChildrenHostile++; kids[q] = (tick, 1); continue; } // a harmless helper (a shield): not an attack, but never hostile
				row.Children++;
				bool hostile = !k.friendly; if (hostile) row.ChildrenHostile++;
				if (trace) Note($"trace {name}: spawned NPC {k.TypeName} (type {k.type}) hostile={hostile} damage={k.damage} ai={k.aiStyle} tick={tick}{(idle ? " IDLE" : "")} parentTarget={(creature.TryGetGlobalNPC(out ConjuredGlobalNPC pg) && pg.Target >= 0 ? Main.npc[pg.Target].TypeName + "#" + pg.Target + " hostileTarget=" + global::Levy.Common.Util.LevyUtil.IsHostileTarget(Main.npc[pg.Target]) + " friendly=" + Main.npc[pg.Target].friendly : "none")}");
			}
			foreach (int q in kids.Keys.ToList())
			{
				NPC k = Main.npc[q]; var (born, stage) = kids[q];
				if (!k.active) continue;
				if (stage == 0 && tick > born)
				{
					kids[q] = (born, 1);
					if (idle) { row.IdleShots++; continue; }
					if (_target == null || !_target.active || k.velocity.Length() < 0.5f) { row.ChildrenOther++; continue; }
					float aE = AngleBetween(k.velocity, _target.Center - k.Center), aP = AngleBetween(k.velocity, eve.Center - k.Center);
					if (Math.Min(aE, aP) > 40f) row.ChildrenOther++; else if (aP <= 12f && aP + 20f < aE) row.ChildrenToPlayer++; else row.ChildrenToEnemy++;
					if (trace) Note($"trace {name}: spawned NPC {k.TypeName} velocity=({k.velocity.X:0.#},{k.velocity.Y:0.#}) angleToEnemy={aE:0} angleToPlayer={aP:0} friendly={k.friendly} target={k.target}");
				}
				if (!_nearKids.Contains(q) && Vector2.Distance(k.Center, eve.Center) < 40f) { row.ChildrenReachedPlayer++; kids[q] = (born, 2); }
				if (_target != null && _target.active && k.Hitbox.Intersects(_target.Hitbox)) row.ChildrenReachedEnemy++;
			}
		}

		/// <summary>The projectile types the owner officer fires on its own against the arena enemy (so a creature's shots are told apart from its officer's).</summary>
		private static HashSet<int> AvatarBaseline(SoldierNPC avatar)
		{
			var set = new HashSet<int>();
			SpawnEnemy(10);
			avatar.NPC.Bottom = new Vector2(Cx * 16f - 40f, FloorY * 16f); avatar.NPC.velocity = Vector2.Zero; _cmdAt = new Vector2(Cx * 16f - 40f - 16f, (FloorY - 3) * 16f);
			for (int t = 0; t < 400; t++) { Step(1); for (int q = 0; q < Main.maxProjectiles; q++) if (Main.projectile[q].active && Main.projectile[q].timeLeft > 0) set.Add(Main.projectile[q].type); }
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			KillEnemy(); Step(2);
			Note("audit: the owner officer's own projectile types (ignored when unflagged): " + string.Join(", ", set.Select(x => ProjectileID.Search.GetName(x))));
			return set;
		}

		private static void LogAudit(AuditRow r) => _mod.Logger.Info($"[OfficerWorld] AUDIT {(r.Pass ? "ok  " : "BAD ")} {r}");

		/// <summary>The audit across every conjurable type (all stages: the roster's whole candidate set up to the top stage caps, plus every skeleton on the Clothier's ladder). Lab entry: LEVY_OFFICERWORLD_ONLY=audit.</summary>
		private static void AuditAll()
		{
			NPC town = Commission(NPCID.Merchant, 0, out SoldierNPC avatar);
			if (avatar == null) { Check("audit: an owner officer is marching", false); return; }
			var types = new List<(int t, string n, int ai)>();
			var seen = new HashSet<int>();
			for (int st = 0; st <= 10; st++) foreach (var f in ConjureRoster.BeastCandidates(st, false, out _)) if (seen.Add(f.Type)) types.Add((f.Type, f.Name, f.AiStyle));
			for (int st = 0; st <= 10; st++) foreach (int t in ConjureRoster.SkeletonPool(st)) if (seen.Add(t)) { var pr = new NPC(); pr.SetDefaults(t); types.Add((t, Lang.GetNPCNameValue(t), pr.aiStyle)); }
			string filter = Environment.GetEnvironmentVariable("LEVY_AUDIT_TYPES");
			if (!string.IsNullOrEmpty(filter)) { var want = new HashSet<string>(filter.Split(',').Select(x => x.Trim().Replace('_', ' '))); types = types.Where(x => want.Contains(x.n) || want.Contains(x.t.ToString())).ToList(); }
			Note($"audit: {types.Count} creature types, {FightTicks} fight ticks + {IdleTicks} idle ticks each");
			_avatarTypes = AvatarBaseline(avatar);
			int bad = 0; var rows = new List<AuditRow>();
			foreach (var (t, n, ai) in types)
			{
				if (avatar == null || !avatar.NPC.active || avatar.NPC.life <= 0)
				{
					Note("audit: the owner officer was lost; commissioning another");
					town.active = false; Step(2);
					town = Commission(NPCID.Merchant, 0, out avatar);
					if (avatar == null) { Check("audit: an owner officer is marching again", false); return; }
				}
				var r = AuditType(avatar, t, n, ai, ai == NPCAIStyleID.Caster ? FightTicks * 3 : FightTicks, ai == NPCAIStyleID.Caster ? IdleTicks * 2 : IdleTicks); // a caster fires on a slow cycle: more ticks for it to cast several times
				rows.Add(r); LogAudit(r); if (!r.Pass) bad++;
			}
			Note($"audit: {rows.Count - bad} ok, {bad} bad of {rows.Count}");
			Check($"audit: {bad} of {rows.Count} conjurable creatures fail the aim audit", bad == 0);
			Dismiss(town);
			KillEnemy();
		}
	}
}
