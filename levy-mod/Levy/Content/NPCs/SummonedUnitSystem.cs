using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Targeting;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Per-tick cache of the live summoned units, so enemy targeting can chase them exactly as it chases soldiers and officers.
	/// Rebuilt lazily on read (the <c>_stamp</c> guard), on every netmode, so it never depends on ModSystem hook order — the same
	/// pattern <see cref="Levy.Common.Officers.OfficerSystem.ActiveBodies"/> uses. <see cref="EnemyTargetingSystem.PreUpdateNPCs"/>
	/// reads <see cref="ActiveBodies"/> and adds each to <see cref="EnemyTargeting.Bodies"/>.
	/// </summary>
	public class SummonedUnitSystem : ModSystem
	{
		public static SummonedUnitSystem Instance => ModContent.GetInstance<SummonedUnitSystem>();

		private uint _stamp = uint.MaxValue;
		private readonly List<ITargetBody> _active = new();

		public override void OnWorldLoad() => Reset();
		public override void OnWorldUnload() => Reset();
		private void Reset() { _stamp = uint.MaxValue; _active.Clear(); }

		private void Refresh()
		{
			uint now = Main.GameUpdateCount;
			if (_stamp == now) return;
			_stamp = now;
			_active.Clear();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is SummonedUnitNPC u && u.IsAlive) _active.Add(u);
			}
		}

		/// <summary>The live summoned units this tick, as targeting bodies. Read-only; valid for the current tick.</summary>
		public IReadOnlyList<ITargetBody> ActiveBodies { get { Refresh(); return _active; } }
	}
}
