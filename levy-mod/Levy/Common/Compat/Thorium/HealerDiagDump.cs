using System;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ThoriumMod;
using ThoriumMod.Items;
using Levy.Common.Weapons;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// TEMPORARY boot-time diagnostic (off by default): dumps how Levy classifies every Thorium healer-class item and the vanilla Rotted Fork, so a human can see which
	/// weapons mis-sort and why a healer never wins the auto-issue ranking. Log only, changes nothing. Flip <see cref="Dump"/> to true, boot with ThoriumMod, read server.log,
	/// then flip it back / delete this file. JIT-gated + Active-gated so it never resolves a Thorium type when Thorium is absent.
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public class HealerDiagDump : ModSystem
	{
		/// <summary>True to emit the dump on the next boot. Keep false in shipped builds.</summary>
		private const bool Dump = false;

		public override void PostAddRecipes()
		{
			if (!Dump || !ThoriumCompatSystem.Active) return;
			try { Emit(); } catch (Exception e) { Mod.Logger.Warn("Levy healer-diag: threw " + e.GetType().Name + ": " + e.Message); }
		}

		private void Emit()
		{
			var sb = new StringBuilder();
			sb.AppendLine("=== LEVY HEALER/ROTTEDFORK CLASSIFICATION DUMP ===");
			sb.AppendLine("name | type | dmg | useStyle | mana | shoot | isHealer | healType | healAmt | DamageType | => Family | RangeTiles | Grav | Ext | ROLE | Score");

			int count = ItemLoader.ItemCount;
			for (int t = 1; t < count; t++)
			{
				Item it = new Item();
				try { it.SetDefaults(t); } catch { continue; }
				if (it.IsAir) continue;

				bool isRottedFork = it.Name != null && it.Name.IndexOf("rotted fork", StringComparison.OrdinalIgnoreCase) >= 0;
				HealerRole role = HealerCompat.RoleOf(it);
				bool healerClass = role != HealerRole.None || it.DamageType is HealerDamage || it.DamageType is HealerTool;
				if (!healerClass && !isRottedFork) continue;
				string origin = t < ItemID.Count ? "VANILLA" : "modded";

				string healType = "-", healAmt = "-", isHealer = "-";
				if (it.ModItem is ThoriumItem ti) { healType = ti.healType.ToString(); healAmt = ti.healAmount.ToString(); isHealer = ti.isHealer.ToString(); }

				WeaponProfile p = WeaponClassifier.GetProfile(it);
				float score = WeaponClassifier.Score(it);
				string dtype = it.DamageType?.DisplayName?.Value ?? it.DamageType?.ToString() ?? "-";

				sb.Append(it.Name).Append(" [").Append(origin).Append("] | ").Append(t).Append(" | ").Append(it.damage).Append(" | ").Append(it.useStyle)
				  .Append(" | ").Append(it.mana).Append(" | ").Append(it.shoot).Append(" | ").Append(isHealer).Append(" | ").Append(healType)
				  .Append(" | ").Append(healAmt).Append(" | ").Append(dtype).Append(" | => ").Append(p.Family).Append(" | ").Append(p.RangeTiles)
				  .Append(" | ").Append(p.Gravity ? "Y" : "n").Append(" | ").Append(p.Extended ? "Y" : "n").Append(" | ").Append(role).Append(" | ").Append(score.ToString("0"));
				sb.AppendLine();
			}
			sb.Append("RangeMagic baseline = ").Append(LevyConstants.RangeMagic).Append(" tiles (a Magic weapon whose RangeTiles is far below this was clamped to its projectile's computed reach)");
			Mod.Logger.Info(sb.ToString());
		}
	}
}
