using System;
using System.IO;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Common.Testing
{
	/// <summary>Lab only: with env LEVY_ROGUE_DUMP=path, writes every rogue-class item (mod, name, stats) at load so a lab can pick weapons across tiers.</summary>
	public class RogueDump : ModSystem
	{
		public override void PostAddRecipes()
		{
			string path = Environment.GetEnvironmentVariable("LEVY_ROGUE_DUMP");
			if (string.IsNullOrEmpty(path)) return;
			try
			{
				var sb = new StringBuilder("id\tname\tdamage\tuseTime\tuseAnim\trare\tvalue\tshoot\tshootSpeed\tchannel\tconsumable\tnoMelee\tcrit\tknockBack\n");
				for (int t = 1; t < ItemLoader.ItemCount; t++)
				{
					if (!ContentSamples.ItemsByType.TryGetValue(t, out Item it) || it.ModItem == null || it.damage <= 0) continue;
					if (!ExtraDamageClasses.IsRogue(it.DamageType)) continue;
					sb.Append($"{it.ModItem.Mod.Name}/{it.ModItem.Name}\t{it.Name}\t{it.damage}\t{it.useTime}\t{it.useAnimation}\t{it.rare}\t{it.value}\t{it.shoot}\t{it.shootSpeed}\t{it.channel}\t{it.consumable}\t{it.noMelee}\t{it.crit}\t{it.knockBack}\n");
				}
				File.WriteAllText(path, sb.ToString());
			}
			catch (Exception e) { Mod.Logger.Warn("RogueDump: " + e.Message); }
		}
	}
}
