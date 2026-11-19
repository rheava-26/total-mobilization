namespace Levy.Common
{
	/// <summary>Spectral Lens (Content/Items/SpectralLens.cs, Common/Vanity/SpectralLens.cs).</summary>
	public static partial class LevyConstants
	{
		/// <summary>How opaque levies, their held weapons, their projectiles and their summons are drawn at the lens's first setting: 15%, so 85% transparent.</summary>
		public const float LensOpacity = 0.15f;
		/// <summary>The lens's settings, chosen by right-clicking it in the inventory: 85%, 95% and 99% transparent (index = the item's saved setting).</summary>
		public static readonly float[] LensOpacities = { LensOpacity, 0.05f, 0.01f };
		/// <summary>The recipe group of every grave marker item (Tombstone, Grave Marker, Cross Grave Marker, Headstone, Gravestone, Obelisk, the five Rich Gravestones).</summary>
		public const string RecipeGroupAnyGrave = "Levy:AnyGrave";
		/// <summary>Spectral Lens recipe at a Work Bench: one grave marker, this many Invisibility Potions and one pair of Goggles.</summary>
		public const int LensRecipePotions = 2;
	}
}
