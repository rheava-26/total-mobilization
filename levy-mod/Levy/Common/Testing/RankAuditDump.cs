using System;
using System.IO;
using System.Linq;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Common.Testing
{
	/// <summary>
	/// TEST-ONLY dump for docs/RANK_AUDIT.md (changes no game behaviour; does nothing unless LEVY_RANKAUDIT=&lt;output path&gt; is set). At PostSetupRecipes writes one TSV row per issuable weapon of vanilla and of the mods named
	/// in LEVY_RANKAUDIT_MODS (default CalamityMod): family, damage class, damage, rarity, value, Score, tier, found flag, and the Balanced rank at world progress 0 / 2 / 5 / 7 / 12 / 18
	/// (before any boss, after Eye, after Skeletron, early Hardmode, after Plantera, after Moon Lord). Ranks are taken with the armory rules and deposit recency out of the picture.
	/// </summary>
	public class RankAuditDump : ModSystem
	{
		public static readonly float[] Stages = { 0f, 2f, 5f, 7f, 12f, 18f };

		public override void PostSetupRecipes()
		{
			string outp = Environment.GetEnvironmentVariable("LEVY_RANKAUDIT");
			if (string.IsNullOrEmpty(outp)) return;
			try
			{
				var sb = new StringBuilder();
				sb.AppendLine("mod\tname\tfamily\tclass\tdamage\trare\tvalue\tscore\ttier\tfound\t" + string.Join("\t", Stages.Select(s => "rk" + (int)s)));
				var mods = (Environment.GetEnvironmentVariable("LEVY_RANKAUDIT_MODS") ?? "CalamityMod").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				WeaponRank.Force(WeaponRankMode.Balanced, false);
				try
				{
					foreach (var mod in new[] { "Vanilla" }.Concat(mods))
					{
						System.Collections.Generic.IEnumerable<(int type, string name)> src;
						if (mod == "Vanilla") src = Enumerable.Range(1, ItemID.Count - 1).Select(t => (t, ItemID.Search.TryGetName(t, out var n) ? n : "?"));
						else if (ModLoader.TryGetMod(mod, out var m)) src = m.GetContent<ModItem>().Select(x => (x.Type, x.Name));
						else continue;
						foreach (var (t, nm) in src)
						{
							var it = new Item();
							try { it.SetDefaults(t); } catch { continue; }
							if (it.accessory || it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0 || it.ammo > 0 || it.createTile >= 0 || it.pick > 0 || it.axe > 0 || it.hammer > 0) continue;
							if (!WeaponClassifier.IsIssuableWeapon(it)) continue;
							var p = WeaponClassifier.GetProfile(it);
							var cls = WeaponClassifier.ItemDamageClass(it);
							var ranks = new float[Stages.Length];
							for (int i = 0; i < Stages.Length; i++) { WeaponRank.ForceProgress = Stages[i]; ranks[i] = WeaponRank.Of(it); }
							WeaponRank.ForceProgress = -1f;
							sb.AppendLine(string.Join("\t", new[] { mod, nm, p.Family.ToString(), cls == null ? "none" : cls.Name, it.damage.ToString(), it.rare.ToString(), it.value.ToString(), WeaponClassifier.Score(it).ToString("F1"), ProgressionTier.Of(it.type).ToString("F1"), ProgressionTier.IsFound(it.type) ? "1" : "0" }.Concat(ranks.Select(r => r.ToString("F2")))));
						}
					}
				}
				finally { WeaponRank.ForceProgress = -1f; WeaponRank.Release(); }
				File.WriteAllText(outp, sb.ToString());
				Mod.Logger.Info("RANKAUDIT: wrote " + outp);
			}
			catch (Exception e) { Mod.Logger.Error("RANKAUDIT failed: " + e); }
		}
	}
}
