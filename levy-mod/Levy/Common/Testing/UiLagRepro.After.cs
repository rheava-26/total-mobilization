using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Levy.Common.Logistics;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	public partial class UiLagRepro
	{
		/// <summary>The same two network readers once the network is read through <see cref="CachedStock"/> (what the Magic Storage compat hands out).</summary>
		static partial void AfterNetwork(ArmoryTE te, Player payer, LogisticsOrder draft, int netStacks)
		{
			var cached = new CachedStock(new FakeNet(netStacks)); var raw = (FakeNet)cached.Inner;
			Time($"Craft browser with a {netStacks}-stack network (snapshot)", 3, () => { var pool = CraftBrowser.PoolFor(te, payer); pool.Sources.Add(cached); return CraftBrowser.Scan(te, pool).Count + " recipes, " + raw.Walks + " network walks"; });
			Time($"Orders page frame with the network (snapshot)", 4, () =>
			{
				var pool = CraftBrowser.PoolFor(te, payer); pool.Sources.Add(cached); raw.Walks = 0;
				var cc = new CountCache(pool.Count);
				var plan = LogisticsRunner.PlanRemaining(draft, te, cc);
				return plan.Need.Count + " materials, " + raw.Walks + " network walks";
			});
		}
	}
}
