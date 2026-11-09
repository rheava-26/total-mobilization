using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Util;

namespace Levy.Common.Fear
{
	/// <summary>
	/// Event-based fear: on the tick a world event begins (an invasion, Blood Moon, Eclipse, Pumpkin Moon, Frost Moon, the Old One's Army, the Lunar Events) or a boss arrives, every levy is jolted
	/// by <see cref="FearSystem.OnWorldEvent"/>. Levies whose commander has the Spectator potion active are skipped. Server / single player; watches the world every
	/// <see cref="LevyConstants.EventFearScanTicks"/> ticks and reacts to the rising edge only.
	/// </summary>
	public class FearEventSystem : ModSystem
	{
		private bool _bloodMoon, _eclipse, _pumpkin, _frost, _dd2, _lunar;
		private int _invasion;
		private readonly bool[] _bossSeen = new bool[Main.maxNPCs];
		private bool _primed;

		public override void OnWorldLoad() { _primed = false; System.Array.Clear(_bossSeen); }
		public override void OnWorldUnload() { _primed = false; System.Array.Clear(_bossSeen); }

		public override void PostUpdateEverything()
		{
			if (!LevyUtil.IsServerOrSingle || Main.GameUpdateCount % (uint)LevyConstants.EventFearScanTicks != 0) return;
			bool blood = Main.bloodMoon, eclipse = Main.eclipse, pumpkin = Main.pumpkinMoon, frost = Main.snowMoon, dd2 = DD2Event.Ongoing, lunar = NPC.LunarApocalypseIsUp;
			int invasion = Main.invasionType > 0 && Main.invasionSize > 0 ? Main.invasionType : 0;
			float jolt = 0f;
			if (_primed) // the first scan of a world only records what is already going on: loading into a Blood Moon is not its arrival
			{
				if (blood && !_bloodMoon) jolt = System.Math.Max(jolt, LevyConstants.EventFearBloodMoon);
				if (eclipse && !_eclipse) jolt = System.Math.Max(jolt, LevyConstants.EventFearEclipse);
				if (pumpkin && !_pumpkin) jolt = System.Math.Max(jolt, LevyConstants.EventFearPumpkinMoon);
				if (frost && !_frost) jolt = System.Math.Max(jolt, LevyConstants.EventFearFrostMoon);
				if (dd2 && !_dd2) jolt = System.Math.Max(jolt, LevyConstants.EventFearOldOnesArmy);
				if (lunar && !_lunar) jolt = System.Math.Max(jolt, LevyConstants.EventFearLunarEvent);
				if (invasion != 0 && invasion != _invasion) jolt = System.Math.Max(jolt, InvasionFear(invasion));
			}
			_bloodMoon = blood; _eclipse = eclipse; _pumpkin = pumpkin; _frost = frost; _dd2 = dd2; _lunar = lunar; _invasion = invasion;

			bool bossArrived = false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				bool boss = n.active && n.realLife < 0 && LevyUtil.IsBossNpc(n);
				if (boss && !_bossSeen[i] && _primed) bossArrived = true;
				_bossSeen[i] = boss;
			}
			if (bossArrived) jolt = System.Math.Max(jolt, LevyConstants.EventFearBossArrival);
			_primed = true;
			if (jolt > 0f) FearSystem.OnWorldEvent(jolt);
		}

		/// <summary>Fear of one vanilla invasion: Martian Madness is the worst, the Goblin Army the mildest. Any other (modded) invasion counts as the Frost Legion.</summary>
		public static float InvasionFear(int invasionType) => invasionType switch
		{
			InvasionID.GoblinArmy => LevyConstants.EventFearGoblins,
			InvasionID.PirateInvasion => LevyConstants.EventFearPirates,
			InvasionID.MartianMadness => LevyConstants.EventFearMartians,
			_ => LevyConstants.EventFearFrostLegion,
		};
	}
}
