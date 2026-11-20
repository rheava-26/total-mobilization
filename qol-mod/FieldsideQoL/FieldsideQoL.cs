using Terraria.ModLoader;

namespace FieldsideQoL
{
	/// <summary>
	/// Fieldside QoL: small quality-of-life fixes for things unrelated to the Levy army, one feature each.
	/// Feature 1: <see cref="Common.WotgAnglerFix"/>, the Angler quest fix for Calamity: Wrath of the Gods (soft dependency, no hard reference).
	/// </summary>
	public class FieldsideQoL : Mod
	{
		public static FieldsideQoL Instance => ModContent.GetInstance<FieldsideQoL>();
	}
}
