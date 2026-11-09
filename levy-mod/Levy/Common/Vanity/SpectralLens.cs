using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Officers;
using Levy.Content.NPCs;

namespace Levy.Common.Vanity
{
	/// <summary>The local player's Spectral Lens state: worn in an accessory slot or a vanity slot this tick.</summary>
	public class SpectralLensPlayer : ModPlayer
	{
		public bool Worn;
		public override void ResetEffects() => Worn = false;
	}

	/// <summary>
	/// Spectral Lens: while the local player wears it, this client draws every levy, its held weapon, its projectiles, its summoned units and its officers at <see cref="LevyConstants.LensOpacity"/>.
	/// A draw-only effect on this machine: nothing in the simulation changes and other players see the army as usual.
	/// </summary>
	public static class LensDraw
	{
		/// <summary>True when the local player wears the lens (never on a dedicated server).</summary>
		public static bool Active
		{
			get
			{
				if (Main.dedServ || Main.gameMenu) return false;
				Player p = Main.LocalPlayer;
				return p != null && p.active && p.TryGetModPlayer(out SpectralLensPlayer s) && s.Worn;
			}
		}

		/// <summary>The rule on its own: a colour's four channels scaled to the lens opacity.</summary>
		public static Color Fade(Color c) => c * LevyConstants.LensOpacity;
	}

	/// <summary>
	/// Runs after the last vanilla layer of a levy's player-renderer pass and scales every sprite the pass queued (body, armor, held weapon, glows) to the lens opacity. The dummy player that draws a levy sits in
	/// player slot <see cref="SoldierNPC.DummySlot"/>, so no real player is touched.
	/// </summary>
	public class SpectralLensDrawLayer : PlayerDrawLayer
	{
		public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.EyebrellaCloud);

		public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => drawInfo.drawPlayer.whoAmI == SoldierNPC.DummySlot && LensDraw.Active;

		protected override void Draw(ref PlayerDrawSet drawInfo)
		{
			var cache = drawInfo.DrawDataCache;
			for (int i = 0; i < cache.Count; i++)
			{
				DrawData d = cache[i];
				d.color = LensDraw.Fade(d.color);
				cache[i] = d;
			}
		}
	}

	/// <summary>Draws a levy's projectiles (and the mod's own projectiles: whips, minions, thrown weapons) faint while the lens is worn.</summary>
	public class SpectralLensProjectiles : GlobalProjectile
	{
		private static bool _faded;
		private static int _savedAlpha;

		private static bool IsLevys(Projectile p) => p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.IsSoldierShot || p.ModProjectile != null && p.ModProjectile.Mod == Levy.Instance;

		public override bool PreDraw(Projectile projectile, ref Color lightColor)
		{
			_faded = false;
			if (!LensDraw.Active || !IsLevys(projectile)) return true;
			_faded = true; _savedAlpha = projectile.alpha;
			lightColor = LensDraw.Fade(lightColor);
			projectile.alpha = 255 - (int)((255 - projectile.alpha) * LevyConstants.LensOpacity); // the stock draw reads GetAlpha; a custom draw that reads the alpha does too
			return true;
		}

		public override void PostDraw(Projectile projectile, Color lightColor)
		{
			if (!_faded) return;
			_faded = false;
			projectile.alpha = _savedAlpha;
		}
	}

	/// <summary>Officers are vanilla town NPCs parked under an avatar (the avatar itself draws nothing): fade the parked town NPC.</summary>
	public class SpectralLensOfficers : GlobalNPC
	{
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => lateInstantiation && entity.townNPC;

		public override void DrawEffects(NPC npc, ref Color drawColor)
		{
			if (LensDraw.Active && npc.TryGetGlobalNPC(out OfficerGlobalNPC g) && g.Parked) drawColor = LensDraw.Fade(drawColor);
		}
	}
}
