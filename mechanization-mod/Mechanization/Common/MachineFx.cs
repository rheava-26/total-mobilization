using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mechanization.Common
{
	/// <summary>
	/// What a hurt machine looks and sounds like: a red tint that deepens with the damage, a throttled alarm beep when it is nearly gone, and a cartoon fireball when it dies.
	/// Everything here is client-side dust, gore and sound; nothing spawns a projectile or touches a player, so the blast hurts nobody.
	/// </summary>
	public static class MachineFx
	{
		/// <summary>Life fraction below which a machine beeps.</summary>
		public const float AlarmBelow = 0.4f;
		/// <summary>Beep spacing in ticks: the slowest (at the threshold) and the fastest (nearly dead).</summary>
		public const int SlowBeepTicks = 48, FastBeepTicks = 12;
		/// <summary>At most this many beeps a second are played across every machine, and only for machines this close (px) to the local player.</summary>
		public const int MaxBeepsPerSecond = 4;
		public const float HearingRangePx = 1400f;

		private static uint _windowStart; private static int _inWindow;
		/// <summary>Lab counter: beeps actually played and beeps the throttle held back.</summary>
		public static int Beeped, Throttled;

		/// <summary>The body's light colour pushed toward red by the share of its life lost (full health leaves it untouched). <paramref name="pulse"/> 0..1 brightens it on a beep.</summary>
		public static Color Hurt(Color light, int life, int lifeMax, float pulse = 0f)
		{
			if (lifeMax <= 0 || life >= lifeMax) return light;
			float lost = MathHelper.Clamp(1f - life / (float)lifeMax, 0f, 1f);
			float k = 1f - 0.62f * lost;
			int r = Math.Min(255, (int)(light.R * (1f + 0.25f * lost) + 60f * pulse));
			return new Color(r, (int)(light.G * k * (1f - 0.3f * pulse)), (int)(light.B * k * (1f - 0.3f * pulse)), light.A);
		}

		/// <summary>Ticks to wait before this machine's next beep (shorter the less life it has), or 0 when it is healthy enough to stay quiet.</summary>
		public static int BeepGap(int life, int lifeMax)
		{
			if (lifeMax <= 0 || life <= 0) return 0;
			float f = life / (float)lifeMax;
			if (f >= AlarmBelow) return 0;
			return (int)MathHelper.Lerp(FastBeepTicks, SlowBeepTicks, f / AlarmBelow);
		}

		/// <summary>Plays one alarm beep at <paramref name="at"/> unless the whole army has already beeped enough this second or the machine is out of earshot.</summary>
		public static bool TryBeep(Vector2 at)
		{
			if (Main.dedServ) return false;
			Player me = Main.LocalPlayer;
			if (me == null || Vector2.DistanceSquared(me.Center, at) > HearingRangePx * HearingRangePx) return false;
			uint now = Main.GameUpdateCount;
			if (now - _windowStart >= 60u) { _windowStart = now; _inWindow = 0; }
			if (_inWindow >= MaxBeepsPerSecond) { Throttled++; return false; }
			_inWindow++; Beeped++;
			SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.75f, Volume = 1f, MaxInstances = MaxBeepsPerSecond, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew }, at);
			return true;
		}

		/// <summary>The cartoon death: a fireball of bright dust over a white flash, black smoke puffs, sparks and a few scraps. <paramref name="size"/> is roughly the machine's longer side in px.</summary>
		public static void Explode(Terraria.DataStructures.IEntitySource src, Vector2 center, int size)
		{
			if (Main.dedServ) return;
			float s = MathHelper.Clamp(size / 30f, 0.8f, 2f);
			for (int i = 0; i < (int)(26 * s); i++)
			{
				Dust d = Dust.NewDustPerfect(center, i % 3 == 0 ? DustID.YellowStarDust : DustID.Torch, Main.rand.NextVector2Circular(6.5f, 6.5f) * s, 0, default, Main.rand.NextFloat(1.7f, 2.9f));
				d.noGravity = true;
			}
			for (int i = 0; i < (int)(10 * s); i++)
			{
				Dust d = Dust.NewDustPerfect(center, DustID.Smoke, Main.rand.NextVector2Circular(3.5f, 3.5f) * s, 120, new Color(40, 40, 40), Main.rand.NextFloat(1.6f, 2.6f));
				d.noGravity = true;
			}
			for (int i = 0; i < 8; i++)
			{
				Dust d = Dust.NewDustPerfect(center, DustID.Electric, Main.rand.NextVector2CircularEdge(7f, 7f) * Main.rand.NextFloat(0.6f, 1.2f), 0, default, 1.1f);
				d.noGravity = true;
			}
			Dust flash = Dust.NewDustPerfect(center, DustID.Smoke, Vector2.Zero, 0, Color.White, 3.2f * s); flash.noGravity = true; flash.fadeIn = 0f;
			for (int i = 0; i < 4; i++)
				Gore.NewGore(src, center + Main.rand.NextVector2Circular(8f, 8f), Main.rand.NextVector2Circular(2.5f, 2.5f), Main.rand.Next(61, 64)); // the big smoke puffs bombs leave
			Lighting.AddLight(center, 1.6f, 1.0f, 0.35f);
		}
	}
}
