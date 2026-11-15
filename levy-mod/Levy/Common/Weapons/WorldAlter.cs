using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// THE ARMY NEVER ALTERS THE WORLD (Eve, 2026-11-14). No levy, officer, militia, drone or turret uses anything that breaks tiles or walls, places or moves liquid, converts or paints the world, or mines:
	/// explosives, tile-breaking rockets and rounds, drills, picks, hammers, axes, sand and ice guns, solutions. <see cref="Alters(Item)"/> is the one question every pick path asks (weapon classifier, allocation, sidearms,
	/// ammo, drone and turret arsenals, officer kits, the armory's deposit warning); <see cref="AltersWith"/> asks it of a (weapon, round) pair.
	/// <para/>How it knows. The answer is MEASURED, not listed: <c>TileSweepCheck</c> fires every weapon with every round of its kind (and spawns every projectile type) into a block field and compares the field tile by tile;
	/// what it saw is the table <c>WorldAlterTable.tsv</c> shipped inside the mod (W: an item that alters by itself, A: a round that does, X: one pair that does, P: a projectile that does, S / R: swept and safe). The sweep's
	/// self-check fails the build when this class would let anything it measured through. Items and projectiles the table has never seen (another mod, or a newer version of one) are classified from their own compiled code
	/// the first time they are asked about (calls into the tile, wall and liquid APIs of the item's class chain and of its shot's class chain; <see cref="Scan"/>), cached, and treated as altering when in doubt.
	/// Vanilla's own tool flags are always honoured: pick, axe and hammer power, drills and chainsaws.
	/// </summary>
	public static class WorldAlter
	{
		/// <summary>What a shot changes. <see cref="Soft"/> is only tiles vanilla itself lets any sword or boomerang cut (grass blades, vines, cobweb: Main.tileCut): measured and reported, never a reason to bench a weapon.</summary>
		[Flags] public enum Kind : ushort { None = 0, Tiles = 1, Walls = 2, Liquid = 4, Convert = 8, Paint = 16, Place = 32, Other = 64, Tool = 128, Soft = 256 }

		/// <summary>The kinds that make a shot an exclusion: everything but the cuttable-decor bit.</summary>
		public static Kind Hard(Kind k) => k & ~Kind.Soft;

		public const string TablePath = "Common/Weapons/WorldAlterTable.tsv";

		/// <summary>Lab A/B switch: with env LEVY_WORLDALTER=0 nothing is flagged (the sweep's self-check then reports what would get through).</summary>
		public static readonly bool Disabled = Environment.GetEnvironmentVariable("LEVY_WORLDALTER") == "0";

		private static readonly object Gate = new();
		private static bool _loaded;
		private static readonly Dictionary<string, Kind> _weapon = new(), _round = new(), _projectile = new();
		private static readonly Dictionary<(string, string), Kind> _pair = new();
		private static readonly HashSet<string> _safeItem = new(), _safeProjectile = new();
		private static readonly Dictionary<char, List<string>> _keys = new();
		private static readonly Dictionary<int, Kind> _itemCache = new(), _projCache = new();
		private static byte[] _fastSafe;
		private static readonly Dictionary<(int, int), Kind> _pairCache = new();
		/// <summary>Lines the shipped table held, by letter (for the self-check).</summary>
		public static readonly Dictionary<char, int> TableLines = new();
		/// <summary>Items / projectiles classified from compiled code because the table did not know them.</summary>
		public static int Scanned, ScannedAltering;
		public static readonly List<string> ScannedNames = new();

		// ------------------------------------------------------------------------------------------------------ keys

		public static string KeyOf(int itemType)
		{
			if (itemType <= 0) return "";
			if (itemType < ItemID.Count) return ItemID.Search.GetName(itemType);
			ModItem mi = ItemLoader.GetItem(itemType); return mi == null ? "#" + itemType : mi.Mod.Name + "/" + mi.Name;
		}

		public static string ProjectileKeyOf(int projType)
		{
			if (projType <= 0) return "";
			if (projType < ProjectileID.Count) return ProjectileID.Search.GetName(projType);
			ModProjectile mp = ProjectileLoader.GetProjectile(projType); return mp == null ? "#" + projType : mp.Mod.Name + "/" + mp.Name;
		}

		// ------------------------------------------------------------------------------------------------------ the table

		private static Kind ParseKind(string s)
		{
			Kind k = Kind.None; if (string.IsNullOrEmpty(s)) return k;
			foreach (string part in s.Split('+')) if (Enum.TryParse(part, out Kind one)) k |= one;
			return Hard(k) == Kind.None ? (s == "Soft" ? Kind.None : Kind.Other) : Hard(k); // a table line that is only Soft changes nothing the army cares about
		}

		/// <summary>Reads the shipped table (once).</summary>
		public static void EnsureLoaded()
		{
			if (_loaded) return;
			lock (Gate)
			{
				if (_loaded) return;
				try
				{
					byte[] raw = ModContent.GetInstance<Levy>()?.GetFileBytes(TablePath);
					if (raw != null) Parse(Encoding.UTF8.GetString(raw));
				}
				catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn("Levy: the world-alter table could not be read (" + e.Message + "); unknown items are classified from their code only."); }
				_loaded = true;
			}
		}

		/// <summary>Self-check seam: loads table text (replacing nothing already read: used by the check's own copy).</summary>
		public static void Parse(string text)
		{
			foreach (string line in text.Split('\n'))
			{
				if (line.Length < 3) continue;
				string[] f = line.TrimEnd('\r').Split('\t');
				char c = f[0][0]; TableLines[c] = TableLines.TryGetValue(c, out int n) ? n + 1 : 1;
				if (f.Length > 1) { if (!_keys.TryGetValue(c, out var kl)) _keys[c] = kl = new List<string>(); kl.Add(f[1]); }
				switch (c)
				{
					case 'W': if (f.Length > 2) _weapon[f[1]] = ParseKind(f[2]); break;
					case 'A': if (f.Length > 2) _round[f[1]] = ParseKind(f[2]); break;
					case 'X': if (f.Length > 3) _pair[(f[1], f[2])] = ParseKind(f[3]); break;
					case 'P': if (f.Length > 2) _projectile[f[1]] = ParseKind(f[2]); break;
					case 'S': _safeItem.Add(f[1]); break;
					case 'R': _safeProjectile.Add(f[1]); break;
				}
			}
			_itemCache.Clear(); _projCache.Clear(); _pairCache.Clear(); _fastSafe = null;
		}

		/// <summary>The keys of every table line of one letter (E: items that threw in the sweep, Q: projectiles that did).</summary>
		public static List<string> TableKeys(char letter) { EnsureLoaded(); return _keys.TryGetValue(letter, out var l) ? l : new List<string>(); }

		/// <summary>Self-check seam: the item types of the rounds the shipped table says alter the world, and of the items that do by themselves.</summary>
		public static List<int> TableItems(bool rounds)
		{
			EnsureLoaded();
			var res = new List<int>();
			foreach (string key in (rounds ? _round : _weapon).Keys)
			{
				int t = ItemFromKey(key); if (t > 0) res.Add(t);
			}
			return res;
		}

		/// <summary>The item type a table key names (vanilla ItemID name, or Mod/Name), or 0.</summary>
		public static int ItemFromKey(string key)
		{
			int slash = key.IndexOf('/');
			if (slash < 0) return ItemID.Search.TryGetId(key, out int id) ? id : 0;
			return ModLoader.TryGetMod(key.Substring(0, slash), out Mod m) && m.TryFind(key.Substring(slash + 1), out ModItem mi) ? mi.Type : 0;
		}

		/// <summary>Forgets what was learned (lab: the sweep's own run, and the self-check's unknown-type test).</summary>
		public static void ForgetCaches() { lock (Gate) { _itemCache.Clear(); _projCache.Clear(); _pairCache.Clear(); _fastSafe = null; } }

		// ------------------------------------------------------------------------------------------------------ the questions

		/// <summary>True when using this item, or anything it fires, can change the world. Never true for air, armor, accessories or placeable blocks.</summary>
		public static bool Alters(Item it) => Hard(KindOf(it)) != Kind.None;

		/// <summary>The kinds of world change using this item causes by itself (any round included), or None.</summary>
		public static Kind KindOf(Item it)
		{
			if (Disabled || it == null || it.IsAir || it.type <= 0) return Kind.None;
			byte[] fast = _fastSafe; int ty = it.type;
			if (fast != null && ty < fast.Length && fast[ty] != 0) return Kind.None; // the common answer (armory scans ask thousands of times a second) without the lock
			lock (Gate)
			{
				if (_itemCache.TryGetValue(it.type, out Kind c)) return c;
				EnsureLoaded();
				Kind k = Compute(it);
				_itemCache[it.type] = k;
				if (k == Kind.None)
				{
					if (_fastSafe == null || _fastSafe.Length <= ty) { var grown = new byte[Math.Max(ItemLoader.ItemCount, ty + 1)]; if (_fastSafe != null) Array.Copy(_fastSafe, grown, _fastSafe.Length); _fastSafe = grown; }
					_fastSafe[ty] = 1;
				}
				return k;
			}
		}

		private static bool Candidate(Item it) => it.damage > 0 || it.shoot > ProjectileID.None || it.useAmmo > 0 || it.ammo > 0 || it.pick > 0 || it.axe > 0 || it.hammer > 0;

		/// <summary>Vanilla's own tool flags: a pick, axe or hammer power (they cut or break blocks), a drill or chainsaw item.</summary>
		public static bool IsTool(Item it)
		{
			if (it.pick > 0 || it.axe > 0 || it.hammer > 0) return true;
			if (it.type > 0 && it.type < ItemID.Count && (ItemID.Sets.IsDrill[it.type] || ItemID.Sets.IsChainsaw[it.type])) return true;
			return false;
		}

		private static Kind Compute(Item it)
		{
			if (IsTool(it)) return Kind.Tool;
			if (!Candidate(it)) return Kind.None;
			string key = KeyOf(it.type);
			if (_weapon.TryGetValue(key, out Kind w)) return w;
			if (_round.TryGetValue(key, out Kind a)) return a;
			if (_safeItem.Contains(key)) return Kind.None; // measured, nothing changed (a pair-level rule may still apply through AltersWith)
			// never measured: what its own compiled code says, its shot included (cached; when in doubt it alters)
			Kind k = Kind.None;
			if (it.shoot > ProjectileID.None) k |= ProjectileKind(it.shoot);
			k |= ScanItem(it);
			Scanned++; if (k != Kind.None) ScannedAltering++;
			if (ScannedNames.Count < 400) ScannedNames.Add(key + (k != Kind.None ? " [" + k + "]" : ""));
			return k;
		}

		/// <summary>Whether this weapon firing this round changes the world (no round: the weapon alone).</summary>
		public static bool AltersWith(Item weapon, Item ammo) => KindWith(weapon, ammo) != Kind.None;

		public static Kind KindWith(Item weapon, Item ammo)
		{
			if (Disabled) return Kind.None;
			Kind k = KindOf(weapon); if (k != Kind.None) return k;
			if (ammo == null || ammo.IsAir) return Kind.None;
			k = KindOf(ammo); if (k != Kind.None) return k;
			if (weapon == null || weapon.IsAir) return Kind.None; // a round judged alone (the pouch of a levy whose weapon is not known)
			lock (Gate)
			{
				if (_pairCache.TryGetValue((weapon.type, ammo.type), out Kind c)) return c;
				EnsureLoaded();
				_pairCache.TryGetValue((weapon.type, ammo.type), out c);
				if (_pair.TryGetValue((KeyOf(weapon.type), KeyOf(ammo.type)), out Kind x)) c = x;
				_pairCache[(weapon.type, ammo.type)] = c;
				return c;
			}
		}

		/// <summary>Whether this projectile type (and anything it spawns that the table knows) changes the world.</summary>
		public static bool AltersProjectile(int projType) => ProjectileKind(projType) != Kind.None;

		public static Kind ProjectileKind(int projType)
		{
			if (Disabled || projType <= 0) return Kind.None;
			lock (Gate)
			{
				if (_projCache.TryGetValue(projType, out Kind c)) return c;
				EnsureLoaded();
				string key = ProjectileKeyOf(projType);
				Kind k;
				if (_projectile.TryGetValue(key, out k)) { }
				else if (_safeProjectile.Contains(key) || projType < ProjectileID.Count) k = Kind.None; // vanilla is fully swept: not listed means measured safe
				else { k = ScanProjectile(projType); Scanned++; if (k != Kind.None) ScannedAltering++; if (ScannedNames.Count < 400) ScannedNames.Add("projectile " + key + (k != Kind.None ? " [" + k + "]" : "")); }
				_projCache[projType] = k;
				return k;
			}
		}

		/// <summary>A short player-facing reason text for a flagged item, or null.</summary>
		public static string ReasonText(Item it)
		{
			Kind k = KindOf(it); if (k == Kind.None) return null;
			string key = k.HasFlag(Kind.Tool) ? "Tool" : k.HasFlag(Kind.Liquid) ? "Liquid" : k.HasFlag(Kind.Convert) ? "Convert" : k.HasFlag(Kind.Place) ? "Place" : k.HasFlag(Kind.Paint) ? "Paint" : "Break";
			return global::Levy.Common.Util.LevyUtil.T("UI2.AlterReason" + key);
		}

		// ------------------------------------------------------------------------------------------------------ compiled-code scan

		private static readonly HashSet<string> WorldGenCalls = new()
		{
			"KillTile", "KillWall", "PlaceTile", "PlaceWall", "ReplaceTile", "ReplaceWall", "Convert", "ConvertSkyWalls", "PoundTile", "SlopeTile", "paintTile", "paintWall", "KillTile_DropItems", "PlaceLiquid", "PlaceObject", "Place1x2",
			"Place2x2", "PlaceChest", "ExplodeMine", "TileRunner", "digTunnel", "DestroyObject", "destroyObject", "KillActuator", "KillWire", "PlaceActuator", "PlaceWire", "ConvertTile",
		};
		private static readonly HashSet<string> TileSetters = new() { "set_HasTile", "set_TileType", "set_WallType", "set_LiquidAmount", "set_LiquidType", "ClearTile", "ClearEverything", "ClearMetadata" };

		private static readonly Dictionary<short, System.Reflection.Emit.OpCode> _ops = BuildOps();
		private static Dictionary<short, System.Reflection.Emit.OpCode> BuildOps()
		{
			var d = new Dictionary<short, System.Reflection.Emit.OpCode>();
			foreach (FieldInfo f in typeof(System.Reflection.Emit.OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)) { var o = (System.Reflection.Emit.OpCode)f.GetValue(null); d[o.Value] = o; }
			return d;
		}

		/// <summary>The compiled metadata of a mod's code, read from the mod file (never loaded twice, never throws).</summary>
		private sealed class Meta
		{
			public MetadataReader Reader; public PEReader Pe;
		}
		private static readonly Dictionary<Assembly, Meta> _meta = new();

		private static Meta MetaOf(Assembly asm)
		{
			if (_meta.TryGetValue(asm, out Meta m)) return m;
			m = null;
			try
			{
				Mod mod = ModLoader.Mods.FirstOrDefault(x => x.Code == asm);
				if (mod != null && mod.FileExists(mod.Name + ".dll"))
				{
					byte[] dll = mod.GetFileBytes(mod.Name + ".dll");
					if (dll != null) { var pe = new PEReader(System.Collections.Immutable.ImmutableArray.Create(dll)); m = new Meta { Pe = pe, Reader = pe.GetMetadataReader() }; }
				}
			}
			catch (Exception) { m = null; }
			_meta[asm] = m;
			return m;
		}

		/// <summary>Namespace, type and method name a call token names, without resolving it (resolution of generic signatures throws, and tML logs every throw). Null for anything it cannot name.</summary>
		private static (string ns, string type, string name, MethodDefinitionHandle def)? Name(MetadataReader r, EntityHandle h)
		{
			switch (h.Kind)
			{
				case HandleKind.MethodDefinition:
				{
					var d = r.GetMethodDefinition((MethodDefinitionHandle)h); var t = r.GetTypeDefinition(d.GetDeclaringType());
					return (r.GetString(t.Namespace), r.GetString(t.Name), r.GetString(d.Name), (MethodDefinitionHandle)h);
				}
				case HandleKind.MemberReference:
				{
					var mr = r.GetMemberReference((MemberReferenceHandle)h);
					if (mr.Parent.Kind == HandleKind.TypeReference) { var tr = r.GetTypeReference((TypeReferenceHandle)mr.Parent); return (r.GetString(tr.Namespace), r.GetString(tr.Name), r.GetString(mr.Name), default); }
					if (mr.Parent.Kind == HandleKind.TypeDefinition) { var td = r.GetTypeDefinition((TypeDefinitionHandle)mr.Parent); return (r.GetString(td.Namespace), r.GetString(td.Name), r.GetString(mr.Name), default); }
					return null;
				}
				case HandleKind.MethodSpecification: return Name(r, r.GetMethodSpecification((MethodSpecificationHandle)h).Method);
			}
			return null;
		}

		/// <summary>True when the method's IL calls one of the tile, wall or liquid APIs (or Projectile.ExplodeTiles / Liquid.AddWater); also fills <paramref name="callees"/> with the methods it calls in the same assembly.</summary>
		private static bool MethodAlters(MethodBase m, Meta meta, List<MethodBase> callees)
		{
			byte[] il; try { il = m.GetMethodBody()?.GetILAsByteArray(); } catch (Exception) { return false; }
			if (il == null || meta == null) return false;
			int i = 0;
			while (i < il.Length)
			{
				short code = il[i++]; if (code == 0xFE && i < il.Length) code = (short)(0xFE00 | il[i++]);
				if (!_ops.TryGetValue(code, out var op)) return false;
				int size = op.OperandType switch
				{
					OperandType.InlineNone => 0, OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1, OperandType.InlineVar => 2,
					OperandType.InlineI8 or OperandType.InlineR => 8, OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i), _ => 4,
				};
				if (op.OperandType == OperandType.InlineMethod && i + 4 <= il.Length && (op.Value == OpCodes.Call.Value || op.Value == OpCodes.Callvirt.Value || op.Value == OpCodes.Newobj.Value))
				{
					int token = BitConverter.ToInt32(il, i);
					try
					{
						var n = Name(meta.Reader, MetadataTokens.EntityHandle(token));
						if (n != null)
						{
							var (ns, type, name, def) = n.Value;
							if (ns == "Terraria")
							{
								if (type == "WorldGen" && WorldGenCalls.Contains(name)) return true;
								if (type == "Tile" && TileSetters.Contains(name)) return true;
								if (type == "Liquid" && name == "AddWater") return true;
								if (type == "Projectile" && name == "ExplodeTiles") return true;
							}
							else if (!def.IsNil && callees != null && callees.Count < 64)
							{
								var d = meta.Reader.GetMethodDefinition(def);
								if (d.GetGenericParameters().Count == 0 && meta.Reader.GetTypeDefinition(d.GetDeclaringType()).GetGenericParameters().Count == 0)
								{
									MethodBase t = m.Module.ResolveMethod(token);
									if (t != null) callees.Add(t);
								}
							}
						}
					}
					catch (Exception) { }
				}
				i += size;
			}
			return false;
		}

		/// <summary>Scans a type's own methods and the ones it calls in its own assembly (three levels): true when any reaches the tile, wall or liquid APIs.</summary>
		private static bool ScanType(Type start)
		{
			var seen = new HashSet<MethodBase>(); var queue = new Queue<(MethodBase, int)>();
			Meta meta = MetaOf(start.Assembly);
			if (meta == null) return true; // the code cannot be read: assume the worst
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
			for (Type t = start; t != null && t != typeof(object) && t != typeof(ModProjectile) && t != typeof(ModItem) && t.Assembly != typeof(Main).Assembly && t.Assembly != typeof(ModProjectile).Assembly; t = t.BaseType)
				foreach (MethodBase m in t.GetMethods(flags)) if (seen.Add(m)) queue.Enqueue((m, 0));
			int budget = 600;
			while (queue.Count > 0 && budget-- > 0)
			{
				var (m, depth) = queue.Dequeue();
				var callees = new List<MethodBase>();
				if (MethodAlters(m, meta, callees)) return true;
				if (depth < 3) foreach (MethodBase c in callees) if (seen.Add(c)) queue.Enqueue((c, depth + 1));
			}
			return false;
		}

		/// <summary>Stopwatch ticks spent in compiled-code scans (the cost of an item the table did not know).</summary>
		public static long ScanTicks;

		private static Kind ScanProjectile(int projType)
		{
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			try { return ScanProjectileCore(projType); } finally { ScanTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
		}

		private static Kind ScanProjectileCore(int projType)
		{
			ModProjectile mp = projType >= ProjectileID.Count ? ProjectileLoader.GetProjectile(projType) : null;
			if (mp == null) return Kind.None;
			// a projectile that borrows the AI of one vanilla measured as altering (a modded spray on the Clentaminator's, a modded bomb on Dynamite's) has no tile call of its own to find
			if (mp.AIType > 0 && mp.AIType < ProjectileID.Count && ProjectileKind(mp.AIType) != Kind.None) return Kind.Other;
			try { return ScanType(mp.GetType()) ? Kind.Other : Kind.None; } catch (Exception) { return Kind.Other; } // unreadable code: assume the worst
		}

		private static Kind ScanItem(Item it)
		{
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			try { return ScanItemCore(it); } finally { ScanTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
		}

		private static Kind ScanItemCore(Item it)
		{
			if (it.ModItem == null) return Kind.None;
			try { return ScanType(it.ModItem.GetType()) ? Kind.Other : Kind.None; } catch (Exception) { return Kind.Other; }
		}

		/// <summary>Self-check seam: the compiled-code verdict on a modded projectile type alone.</summary>
		public static Kind ScanProjectileOnly(int projType) => ScanProjectile(projType);

		/// <summary>Self-check seam: classify from compiled code only, ignoring the table (the sweep measures this against what it saw).</summary>
		public static Kind ScanOnly(Item it)
		{
			Kind k = Kind.None;
			if (it.shoot > ProjectileID.None && it.shoot >= ProjectileID.Count) k |= ScanProjectile(it.shoot);
			return k | ScanItem(it);
		}
	}
}
