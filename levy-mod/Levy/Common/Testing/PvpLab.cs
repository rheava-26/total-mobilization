using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Content.NPCs;
using Levy.Content.Projectiles;

namespace Levy.Common.Testing
{
	/// <summary>
	/// LAB ONLY: two levy armies fighting each other (the calrev harness's `fight pvp ...` line, CombatHarness.Pvp.cs). Off unless that harness scenario runs a pvp fight, so nothing here touches a game.
	/// Both armies are ordinary levies of the stand-in commander (same brain, gear rules, potions, healing, intake). Each body carries a team (1 or 2). The game's own "is it an enemy" tests (vanilla
	/// projectile hits, homing, minion targeting, Levy's targeting) all read NPC.friendly, so the lab flips that flag by perspective: while a team-1 body or a team-1 shot updates, team-2 bodies are
	/// non-friendly and team-1 bodies friendly, and the other way round. Between updates (players, recorder, systems) every body is friendly again, as in a game.
	/// </summary>
	internal static class PvpLab
	{
		/// <summary>A pvp fight is running. Set and cleared only by the harness.</summary>
		internal static bool On;
		/// <summary>Team of each NPC slot (0 none, 1, 2).</summary>
		internal static readonly sbyte[] Team = new sbyte[Main.maxNPCs + 1];
		/// <summary>The team whose body or shot is updating now (0: none, every body friendly).</summary>
		internal static int Persp;
		/// <summary>The NPC slots of every team body (live or not).</summary>
		internal static readonly List<int> Bodies = new();

		internal static void Add(int idx, int team) { Team[idx] = (sbyte)team; if (!Bodies.Contains(idx)) Bodies.Add(idx); }
		internal static void Remove(int idx) { if ((uint)idx < Main.maxNPCs) { Team[idx] = 0; if (Main.npc[idx].ModNPC is SoldierNPC) Main.npc[idx].friendly = true; } Bodies.Remove(idx); }

		/// <summary>Make <paramref name="team"/>'s bodies friendly and the other team's bodies non-friendly (0: all friendly).</summary>
		internal static void SetPersp(int team)
		{
			if (team == Persp) return;
			Persp = team;
			for (int k = 0; k < Bodies.Count; k++)
			{
				int i = Bodies[k]; NPC n = Main.npc[i];
				if (n.active && n.ModNPC is SoldierNPC) n.friendly = team == 0 || Team[i] == team || Team[i] == 0;
			}
		}

		internal static int TeamOf(int npcIdx) => (uint)npcIdx < Main.maxNPCs ? Team[npcIdx] : 0;

		/// <summary>The soldier NPC slot a projectile belongs to (a soldier shot, its children, a soldier's minion), or -1.</summary>
		internal static int ProjSoldier(Projectile p)
		{
			if (p == null || !p.active) return -1;
			if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0) return tag.SoldierIndex;
			if (p.type == ModContent.ProjectileType<SoldierMinion>()) return (int)p.ai[0];
			return -1;
		}
		internal static int ProjTeam(Projectile p) => TeamOf(ProjSoldier(p));

		/// <summary>A live body of the other team, seen from the perspective now set.</summary>
		internal static bool IsEnemy(NPC n)
		{
			if (Persp == 0 || n == null || !n.active || n.life <= 0) return false;
			int i = n.whoAmI;
			if ((uint)i >= Main.maxNPCs || !ReferenceEquals(Main.npc[i], n)) return false;
			int t = Team[i];
			return t != 0 && t != Persp && n.ModNPC is SoldierNPC s && s.IsAlive;
		}

		/// <summary>May this projectile hit this body? A team shot hits only the other team's bodies.</summary>
		internal static bool ShotMayHit(Projectile p, NPC target)
		{
			int pt = ProjTeam(p), tt = TeamOf(target.whoAmI);
			return pt != 0 && tt != 0 && pt != tt;
		}

		internal static void Reset()
		{
			SetPersp(0);
			On = false; Persp = 0;
			Array.Clear(Team, 0, Team.Length);
			Bodies.Clear();
		}
	}
}
