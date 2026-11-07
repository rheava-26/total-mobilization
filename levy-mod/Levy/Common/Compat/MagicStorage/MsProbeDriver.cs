using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using MagicStorage.Components;
using Levy.Common.Testing;
using Levy.Content.TileEntities;

namespace Levy.Common.Compat.MagicStorageLink
{
	/// <summary>TEST-ONLY (used by <see cref="SummonSourceProbe"/>; never runs in play): a real in-memory Magic Storage network (storage heart + a tile-less unit) and a placed armory beside it, so the armory's Magic Storage reads and withdrawals run through the real Magic Storage code.</summary>
	[JITWhenModsEnabled("MagicStorage")]
	internal static class MsProbeDriver
	{
		private static TEStorageHeart _heart; private static TECheckUnit _unit;
		private static readonly List<TileEntity> Placed = new();
		private static bool _savedMenu; private static bool _menuSaved;

		private static TileEntity Place<T>(int x, int y) where T : ModTileEntity
		{
			int id = ModContent.GetInstance<T>().Place(x, y);
			var te = TileEntity.ByID[id]; Placed.Add(te); return te;
		}

		public static SummonSourceProbe.MsProbeScope Open()
		{
			if (!_menuSaved) { _savedMenu = Main.gameMenu; _menuSaved = true; }
			Main.gameMenu = false; // Magic Storage refuses every access while the game menu is up (it is, during load)
			if (_heart == null)
			{
				_heart = (TEStorageHeart)Place<TEStorageHeart>(80, 40);
				_unit = (TECheckUnit)Place<TECheckUnit>(82, 40);
				_heart.ComponentManager.Link(_unit);
			}
			_unit.Items.Clear();
			var armory = (ArmoryTE)Place<ArmoryTE>(40, 40);
			return new Scope { Armory = armory };
		}

		public static void Shutdown()
		{
			foreach (var te in Placed) { TileEntity.ByID.Remove(te.ID); TileEntity.ByPosition.Remove(te.Position); }
			Placed.Clear(); _heart = null; _unit = null;
			if (_menuSaved) { Main.gameMenu = _savedMenu; _menuSaved = false; }
		}

		private sealed class Scope : SummonSourceProbe.MsProbeScope
		{
			public override void Put(IEnumerable<Item> items) { foreach (var it in items) _unit.Items.Add(it.Clone()); }
			public override IEnumerable<Item> Contents() => _unit.Items.Where(i => i != null && !i.IsAir).ToList();
			public override void Close()
			{
				if (Armory != null) { TileEntity.ByID.Remove(Armory.ID); TileEntity.ByPosition.Remove(Armory.Position); Placed.Remove(Armory); }
				_unit?.Items.Clear();
			}
		}
	}
}
