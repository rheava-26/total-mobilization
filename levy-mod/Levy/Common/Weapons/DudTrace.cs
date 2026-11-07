using System;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Measurement only (env LEVY_DUD_TRACE=1): per weapon item, counts how often a soldier tried to use it, what the dispatch returned, which projectile types its hooks spawned, and every 10 s which soldier-tagged
	/// projectile types are alive. Written to the mod log as "DUDTRACE ..." lines so a weapon that measures zero damage can be traced to the step that loses it. Off by default; no gameplay effect.
	/// </summary>
	public class DudTrace : ModSystem
	{
		internal static readonly bool On = Environment.GetEnvironmentVariable("LEVY_DUD_TRACE") == "1";
		private sealed class Stat { public string Family = "?"; public int Calls, Fired, NotFired, Held, Spawned; public Dictionary<string, int> Proj = new(), Note = new(); }
		private static readonly Dictionary<string, Stat> _s = new();

		private static Stat Of(Item item)
		{
			string k = item.ModItem != null ? item.ModItem.Mod.Name + "/" + item.ModItem.Name : item.Name;
			if (!_s.TryGetValue(k, out Stat st)) _s[k] = st = new Stat();
			return st;
		}

		internal static void Call(Item item, WeaponProfile p) { if (!On) return; var s = Of(item); s.Calls++; s.Family = p.Family.ToString(); }
		internal static void Result(Item item, bool fired) { if (!On) return; var s = Of(item); if (fired) s.Fired++; else s.NotFired++; }
		internal static void Spawned(Item item, Projectile p)
		{
			if (!On) return;
			var s = Of(item); s.Spawned++;
			string n = (p.ModProjectile != null ? p.ModProjectile.Name : p.type.ToString()) + "@" + p.owner;
			s.Proj[n] = s.Proj.TryGetValue(n, out int c) ? c + 1 : 1;
		}
		/// <summary>A free-form counter on an item ("held-spawn-null", "no-mana"...).</summary>
		internal static void NoteType(int projType, string what) { if (!On) return; string k = "proj:" + (ContentSamples.ProjectilesByType.TryGetValue(projType, out Projectile pr) && pr.ModProjectile != null ? pr.ModProjectile.Name : projType.ToString()); if (!_s.TryGetValue(k, out Stat st)) _s[k] = st = new Stat(); st.Note[what] = st.Note.TryGetValue(what, out int c) ? c + 1 : 1; }
		internal static void Note(Item item, string what) { if (!On || item == null) return; var s = Of(item); s.Note[what] = s.Note.TryGetValue(what, out int c) ? c + 1 : 1; }

		public override void PostUpdateEverything()
		{
			if (!On || Main.GameUpdateCount % 600 != 0) return;
			var live = new Dictionary<string, int>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || !p.TryGetGlobalProjectile(out SoldierProjectileTag t) || t.SoldierIndex < 0) continue;
				string n = (p.ModProjectile != null ? p.ModProjectile.Name : p.type.ToString()) + "@" + p.owner + (p.friendly ? "/f" : "/-") + (p.damage > 0 ? "/d" : "/0") + "/ai=" + (int)p.ai[0] + "," + (int)p.ai[1] + "/v" + (int)p.velocity.Length() + "/tl" + p.timeLeft / 100;
				live[n] = live.TryGetValue(n, out int c) ? c + 1 : 1;
			}
			var sb = new StringBuilder();
			foreach (var kv in live) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
			foreach (var kv in _s)
			{
				Stat s = kv.Value; var pj = new StringBuilder(); foreach (var q in s.Proj) pj.Append(q.Key).Append('=').Append(q.Value).Append(',');
				var nt = new StringBuilder(); foreach (var q in s.Note) nt.Append(q.Key).Append('=').Append(q.Value).Append(',');
				Mod.Logger.Info($"DUDTRACE {kv.Key} fam={s.Family} calls={s.Calls} fired={s.Fired} notFired={s.NotFired} spawned={s.Spawned} proj[{pj}] notes[{nt}]");
			}
			if (sb.Length > 0) Mod.Logger.Info("DUDTRACE live: " + sb);
		}
	}

	/// <summary>Measurement only (LEVY_DUD_TRACE=1): per projectile type, how often a soldier-tagged projectile hit something, how much damage, and how it ended (its first AI value at the end, bucketed by 10).</summary>
	public class DudTraceProjectiles : GlobalProjectile
	{
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!DudTrace.On || !projectile.TryGetGlobalProjectile(out SoldierProjectileTag t) || t.SoldierIndex < 0) return;
			DudTrace.NoteType(projectile.type, "hit");
			DudTrace.NoteType(projectile.type, "dmg+" + Math.Min(damageDone, 99999) / 100 * 100);
		}

		public override void OnKill(Projectile projectile, int timeLeft)
		{
			if (!DudTrace.On || !projectile.TryGetGlobalProjectile(out SoldierProjectileTag t) || t.SoldierIndex < 0) return;
			DudTrace.NoteType(projectile.type, "end@ai0=" + (int)(projectile.ai[0] / 10f) * 10);
		}
	}
}
