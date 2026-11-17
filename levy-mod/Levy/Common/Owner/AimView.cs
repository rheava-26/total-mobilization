using Microsoft.Xna.Framework;
using Terraria;

namespace Levy.Common.Owner
{
	/// <summary>
	/// The cursor a levy aims with. A player's cursor sits on a 1920 x 1080 screen centred on the player, and a lot of weapons place their shot relative to that screen
	/// (Star Shower rains from <c>Main.screenPosition.Y - 80</c>, the top edge of the screen, onto the cursor). The old cursor put the screen at the world's top-left corner, so every such rain
	/// spawned at world y = -80, thousands of pixels above the fight, and never reached it. The levy's view is the screen it would have as a player: centred on the levy, with the cursor on the point it aims at.
	/// <c>Main.MouseWorld</c> and everything built on <c>screenPosition + mouse</c> still read the aim point exactly.
	/// </summary>
	public static class AimView
	{
		public const float Width = 1920f, Height = 1080f;

		/// <summary>The view's top-left corner for a shooter at <paramref name="shooter"/>: centred on it, kept inside the world like a player's screen.</summary>
		/// <summary>Harness only: LEVY_AIMVIEW=0 restores the old cursor (screen at the world's top-left corner) so a sweep can measure the unfixed behaviour.</summary>
		internal static readonly bool Legacy = System.Environment.GetEnvironmentVariable("LEVY_AIMVIEW") == "0";

		public static Vector2 ScreenFor(Vector2 shooter)
		{
			if (Legacy) return Vector2.Zero;
			var sp = shooter - new Vector2(Width * 0.5f, Height * 0.5f);
			if (sp.X < 0f) sp.X = 0f;
			if (sp.Y < 0f) sp.Y = 0f;
			return sp;
		}

		/// <summary>Points <c>Main.screenPosition</c> / <c>Main.mouseX</c> / <c>Main.mouseY</c> at the levy's view with the cursor on <paramref name="aim"/>. The caller saves and restores the three.</summary>
		public static void Set(Vector2 aim, Vector2 shooter)
		{
			Vector2 sp = ScreenFor(shooter);
			Main.screenPosition = sp;
			Main.mouseX = (int)(aim.X - sp.X); Main.mouseY = (int)(aim.Y - sp.Y);
		}
	}
}
