using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.Systems;

namespace Levy.Common.UI
{
	/// <summary>Minimap / fullscreen-map dots for live soldier bodies: gold for regulars, blue for militia. Hover shows the name.</summary>
	public class SoldierMapLayer : ModMapLayer
	{
		private static Texture2D _gold, _blue;
		private static readonly Color GoldFill = new(255, 200, 60), BlueFill = new(80, 140, 255), Edge = new(25, 25, 35);

		public override void Draw(ref MapOverlayDrawContext context, ref string text)
		{
			if (Main.dedServ) return;
			EnsureTextures();
			if (_gold == null || _blue == null) return;
			foreach (var s in RosterSystem.Instance.ActiveBodies())
			{
				if (s?.NPC == null || !s.NPC.active) continue;
				var tex = s.IsMilitia ? _blue : _gold;
				if (context.Draw(tex, s.NPC.Center / LevyConstants.TilePx, Alignment.Center).IsMouseOver) text = s.SoldierName;
			}
		}

		private static void EnsureTextures()
		{
			if (_gold != null && !_gold.IsDisposed && _blue != null && !_blue.IsDisposed) return;
			var device = Main.graphics?.GraphicsDevice;
			if (device == null) return;
			_gold = MakeDot(device, GoldFill);
			_blue = MakeDot(device, BlueFill);
		}

		private static Texture2D MakeDot(GraphicsDevice device, Color fill)
		{
			int n = LevyConstants.UI.MapDotSize;
			var tex = new Texture2D(device, n, n);
			var data = new Color[n * n];
			for (int y = 0; y < n; y++)
				for (int x = 0; x < n; x++)
					data[y * n + x] = (x == 0 || y == 0 || x == n - 1 || y == n - 1) ? Edge : fill;
			tex.SetData(data);
			return tex;
		}

		public override void Unload()
		{
			if (!Main.dedServ)
			{
				var g = _gold; var b = _blue;
				Main.QueueMainThreadAction(() => { g?.Dispose(); b?.Dispose(); });
			}
			_gold = null; _blue = null;
		}
	}
}
