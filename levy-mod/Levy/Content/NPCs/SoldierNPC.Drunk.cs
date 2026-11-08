using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Content.Buffs;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// A levy under a drink (the buff, which the server syncs): a hop and a raised arm when the round arrives, then a gentle sway with an occasional lurch for as long as the buff lasts.
	/// Draw only: nothing here touches the body's movement, hitbox or aim (the aim penalty is DrinkBuffs.AimErrorFactor), so it costs nothing on the server and cannot desync.
	/// The buff's own timer is read only to notice a fresh serving; the levy times the celebration from the tick it first saw the buff, so it never depends on the client counting buffTime down.
	/// </summary>
	public partial class SoldierNPC
	{
		private uint _drunkScanTick = uint.MaxValue, _drunkStart;
		private bool _drunkOn;
		private int _drunkLastBuffTime;

		private const float DrunkSwayRad = 0.045f, DrunkSwayWobbleRad = 0.025f, DrunkLurchRad = 0.16f, DrunkSwayPx = 1.2f;
		private const int DrunkSwayRampStart = 40, DrunkSwayRampLen = 80, HopTicks = 70, ArmUpTicks = 110, LurchTicks = 28;
		private const float HopPx = 7f;

		/// <summary>Adjusts the final body draw for a drink: position (hop, sway), rotation about the feet, and raises the off-hand arm while cheering. False when the levy is sober.</summary>
		private bool DrunkPose(ref Vector2 pos, ref float rot, ref Vector2 origin)
		{
			uint tick = Main.GameUpdateCount;
			if (tick != _drunkScanTick)
			{
				_drunkScanTick = tick;
				int bt = 0;
				for (int i = 0; i < NPC.buffType.Length; i++)
					if (NPC.buffType[i] > 0 && DrinkBuffs.IsDrinkBuff(NPC.buffType[i])) { bt = Math.Max(1, NPC.buffTime[i]); break; }
				_drunkOn = bt > 0;
				if (!_drunkOn) _drunkLastBuffTime = 0;
				else { if (bt > _drunkLastBuffTime) _drunkStart = tick; _drunkLastBuffTime = bt; }
			}
			if (!_drunkOn) return false;

			int id = NPC.whoAmI;
			float t = tick, ph = id * 1.37f;
			int elapsed = (int)(tick - _drunkStart);

			// the cheer: a hop and an arm in the air, on the same delay as the emote bubble
			int e = elapsed - DrinkCheers.CheerDelay(id);
			if (e >= 0 && e < HopTicks) pos.Y -= MathF.Abs(MathF.Sin(e * 0.2f)) * HopPx * (1f - e / (float)HopTicks);
			if (e >= 0 && e < ArmUpTicks && ItemAnimation <= 0)
			{
				int dir = Dummy.direction == 0 ? 1 : Dummy.direction;
				Dummy.SetCompositeArmBack(enabled: true, Player.CompositeArmStretchAmount.Full, -dir * (MathF.PI * 0.82f + 0.12f * MathF.Sin(e * 0.5f)));
			}

			// then the sway, growing in after the cheer: two slow sines, plus a lurch every ten seconds or so
			float amp = MathHelper.Clamp((elapsed - DrunkSwayRampStart) / (float)DrunkSwayRampLen, 0f, 1f);
			float sway = MathF.Sin(t * 0.05f + ph);
			float r = DrunkSwayRad * sway + DrunkSwayWobbleRad * MathF.Sin(t * 0.13f + ph * 2.1f);
			int period = 540 + id % 7 * 40;
			int ts = (int)((tick + (uint)(id * 53)) % (uint)period);
			if (ts < LurchTicks) r += DrunkLurchRad * MathF.Sin(ts / (float)LurchTicks * MathF.PI) * ((id & 1) == 0 ? 1f : -1f);
			rot += r * amp;
			pos.X += DrunkSwayPx * sway * amp;
			origin = new Vector2(Dummy.width * 0.5f, Dummy.height); // sway about the feet
			return true;
		}
	}
}
