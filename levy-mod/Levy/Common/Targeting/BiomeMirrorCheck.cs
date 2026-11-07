using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Targeting
{
	/// <summary>Boot self-check: a scratch Player in ZoneCrimson/ZoneJungle/Underworld (+ the first loaded ModBiome, if any) is copied onto a scratch dummy by BiomeMirror and must read back the same zones.</summary>
	public class BiomeMirrorCheck : ModSystem
	{
		public override void PostSetupContent() => global::Levy.Common.Util.LoadSafe.Run(PostSetupContentCore);

		private void PostSetupContentCore()
		{
			var fails = new System.Collections.Generic.List<string>();
			string modBiomeNote = "no ModBiome loaded";
			try
			{
				var src = new Player(); var dst = new Player();
				src.ZoneCrimson = true; src.ZoneJungle = true; src.ZoneBeach = true; src.ZoneLihzhardTemple = true; src.ZoneUnderworldHeight = true;
				ModBiome mb = null;
				foreach (var b in ModContent.GetContent<ModBiome>()) { mb = b; break; }
				var zi = typeof(ModBiome).GetProperty("ZeroIndexType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				var fl = BiomeMirror.FlagsField?.GetValue(src) as System.Collections.BitArray;
				if (mb != null && (zi == null || fl == null)) fails.Add("modBiomeFlags/ZeroIndexType not found by reflection");
				else if (mb != null) { fl[(int)zi.GetValue(mb)] = true; modBiomeNote = "ModBiome " + mb.FullName; }
				BiomeMirror.Copy(src, dst);
				if (!dst.ZoneCrimson) fails.Add("ZoneCrimson");
				if (!dst.ZoneJungle) fails.Add("ZoneJungle");
				if (!dst.ZoneBeach) fails.Add("ZoneBeach");
				if (!dst.ZoneLihzhardTemple) fails.Add("ZoneLihzhardTemple");
				if (!dst.ZoneUnderworldHeight) fails.Add("ZoneUnderworldHeight");
				if (dst.ZoneCorrupt || dst.ZoneHallow || dst.ZoneSnow) fails.Add("spurious zone");
				if (mb != null && !dst.InModBiome(mb)) fails.Add("InModBiome " + mb.FullName);
			}
			catch (System.Exception e) { fails.Add("threw " + e.GetType().Name + ": " + e.Message); }
			if (fails.Count == 0) Mod.Logger.Info("Levy biome-mirror self-check: dummy reads the copied zones OK (" + modBiomeNote + ")");
			else Mod.Logger.Warn("Levy biome-mirror self-check FAILED: " + string.Join(", ", fails));
		}
	}
}
