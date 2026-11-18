using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Util
{
	/// <summary>
	/// STATE IMAGE: an exact copy of an object graph's fields (a levy's puppet Player, its ModPlayers, their collections), taken and written back by compiled accessors.
	/// <para/>Every instance field is a leaf: numbers, bools, enums and floats as their exact bits (structs are walked field by field), references by identity. Arrays are copied element by element; a reference to an "owned"
	/// object (a collection, an array, a mod's or the game's own state class: anything that is not an entity, a mod type, a texture or a delegate) is followed, so a Dictionary of cooldowns is copied down to each cooldown's timer.
	/// <para/>Used by the gear cache (SoldierNPC.GearCache.cs) to keep the image of what a full gear frame builds and to check, every tick, that nothing outside the frame changed what the frame reads; and by its parity check
	/// to put a dummy back exactly where it was (every captured field, every array element, every followed object) and to name every field two images disagree on.
	/// </summary>
	internal sealed class TypePlan
	{
		public readonly Type T; public readonly bool Elem;
		public readonly string[] ValNames, RefNames;
		public readonly bool[] RefRO;
		/// <summary>(holder, index, buf, offset): holder is the object (or the struct array and the element index).</summary>
		public readonly Action<object, int, long[], int> CapV, ResV;
		public readonly Action<object, int, object[], int> CapR, ResR;
		public int NV => ValNames.Length; public int NR => RefNames.Length;

		// RAW LAYOUT (classes): the fields that hold no reference (numbers, bools, enums, plain structs) are copied, compared and written back as the bytes they occupy in the object, range by range. No reference lies inside
		// a range (checked against every reference-holding field's offset), so a raw write never touches what the garbage collector tracks. Read-only fields among them are written back with the bytes they already hold.
		/// <summary>Byte ranges in the object (offset from its first field byte, length) and where each starts in the node's raw buffer.</summary>
		public readonly int[] RangeOff = Array.Empty<int>(), RangeLen = Array.Empty<int>(), RangeBuf = Array.Empty<int>();
		public readonly int RawBytes;
		/// <summary>Each raw field: name, offset in the object, size, and its position in the raw buffer.</summary>
		public readonly string[] RawNames = Array.Empty<string>(); public readonly int[] RawOff = Array.Empty<int>(), RawSize = Array.Empty<int>(), RawBuf = Array.Empty<int>();
		public readonly Type[] RawTypes = Array.Empty<Type>();
		public int NRaw => RawNames.Length;

		private static readonly Dictionary<(Type, bool), TypePlan> Cache = new();
		/// <summary>The plan of <paramref name="t"/>; <paramref name="sample"/> (an instance of t) lets a class plan measure its raw layout the first time.</summary>
		public static TypePlan For(Type t, bool elem = false, object sample = null)
		{
			lock (Cache)
			{
				if (!Cache.TryGetValue((t, elem), out var p)) Cache[(t, elem)] = p = new TypePlan(t, elem, sample);
				return p;
			}
		}

		private struct Leaf { public Expression Acc; public Type Ty; public string Name; public bool RO; }

		private TypePlan(Type t, bool elem, object sample)
		{
			T = t; Elem = elem;
			var holder = Expression.Parameter(typeof(object), "h"); var idx = Expression.Parameter(typeof(int), "i");
			var vbuf = Expression.Parameter(typeof(long[]), "v"); var rbuf = Expression.Parameter(typeof(object[]), "r"); var off = Expression.Parameter(typeof(int), "o");
			// the holder is cast once into a local
			var local = Expression.Variable(elem ? t.MakeArrayType() : t, "x");
			Expression root = elem ? Expression.ArrayAccess(local, idx) : local;
			var vals = new List<Leaf>(); var refs = new List<Leaf>();
			var raw = new List<FieldInfo>(); var other = new List<FieldInfo>();
			bool rawOk = !elem && !t.IsValueType && sample != null && t.IsInstanceOfType(sample);
			if (rawOk)
			{
				foreach (var f in Fields(t)) (Unmanaged(f.FieldType) ? raw : other).Add(f);
				if (raw.Count == 0) rawOk = false;
			}
			if (rawOk)
			{
				try
				{
					var all = raw.Concat(other).ToList();
					long[] offs = Offsets(t, all, sample);
					var rawF = new List<(FieldInfo f, int off, int size)>();
					for (int k = 0; k < raw.Count; k++) rawF.Add((raw[k], (int)offs[k], SizeOf(raw[k].FieldType)));
					var refOffs = new List<int>(); for (int k = raw.Count; k < all.Count; k++) refOffs.Add((int)offs[k]);
					rawF.Sort((a, b) => a.off.CompareTo(b.off));
					var ro = new List<int>(); var rl = new List<int>(); var rb = new List<int>();
					int pos = 0, cs = -1, ce = -1;
					foreach (var (f, o, sz) in rawF)
					{
						bool join = cs >= 0 && o >= ce && o - ce < 8 && !refOffs.Any(x => x >= ce && x < o);
						if (!join) { if (cs >= 0) { ro.Add(cs); rl.Add(ce - cs); rb.Add(pos); pos += ce - cs; } cs = o; }
						ce = Math.Max(ce, o + sz);
					}
					if (cs >= 0) { ro.Add(cs); rl.Add(ce - cs); rb.Add(pos); pos += ce - cs; }
					RangeOff = ro.ToArray(); RangeLen = rl.ToArray(); RangeBuf = rb.ToArray(); RawBytes = pos;
					RawNames = new string[rawF.Count]; RawOff = new int[rawF.Count]; RawSize = new int[rawF.Count]; RawBuf = new int[rawF.Count]; RawTypes = new Type[rawF.Count];
					for (int k = 0; k < rawF.Count; k++)
					{
						RawNames[k] = rawF[k].f.Name; RawOff[k] = rawF[k].off; RawSize[k] = rawF[k].size; RawTypes[k] = rawF[k].f.FieldType;
						int r = Array.FindLastIndex(RangeOff, x => x <= rawF[k].off); RawBuf[k] = RangeBuf[r] + rawF[k].off - RangeOff[r];
					}
					// no reference may sit inside a range
					foreach (int x in refOffs) for (int r = 0; r < RangeOff.Length; r++) if (x >= RangeOff[r] && x < RangeOff[r] + RangeLen[r]) throw new InvalidOperationException("reference inside a raw range of " + t.FullName);
					foreach (var f in other) WalkField(f, root, "", false, 0, vals, refs);
				}
				catch (Exception e)
				{
					global::Levy.Levy.Instance?.Logger.Warn("Levy: state image could not lay out " + t.FullName + " raw (" + e.Message + "); field by field instead");
					rawOk = false; vals.Clear(); refs.Clear();
					RangeOff = RangeLen = RangeBuf = Array.Empty<int>(); RawBytes = 0; RawNames = Array.Empty<string>(); RawOff = RawSize = RawBuf = Array.Empty<int>(); RawTypes = Array.Empty<Type>();
				}
			}
			if (!rawOk) Walk(t, root, "", false, 0, vals, refs);
			ValNames = new string[vals.Count]; RefNames = new string[refs.Count]; RefRO = new bool[refs.Count];
			var capV = new List<Expression>(); var resV = new List<Expression>(); var capR = new List<Expression>(); var resR = new List<Expression>();
			for (int k = 0; k < vals.Count; k++)
			{
				var l = vals[k]; ValNames[k] = l.Name;
				var slot = Expression.ArrayAccess(vbuf, Expression.Add(off, Expression.Constant(k)));
				capV.Add(Expression.Assign(slot, ToLong(l.Acc, l.Ty)));
				if (!l.RO) resV.Add(Expression.Assign(l.Acc, FromLong(slot, l.Ty)));
			}
			for (int k = 0; k < refs.Count; k++)
			{
				var l = refs[k]; RefNames[k] = l.Name; RefRO[k] = l.RO;
				var slot = Expression.ArrayAccess(rbuf, Expression.Add(off, Expression.Constant(k)));
				capR.Add(Expression.Assign(slot, Expression.Convert(l.Acc, typeof(object))));
				if (!l.RO) resR.Add(Expression.Assign(l.Acc, Expression.Convert(slot, l.Ty)));
			}
			Expression cast = Expression.Assign(local, Expression.Convert(holder, local.Type));
			CapV = Lambda<Action<object, int, long[], int>>(local, cast, capV, holder, idx, vbuf, off);
			ResV = Lambda<Action<object, int, long[], int>>(local, cast, resV, holder, idx, vbuf, off);
			CapR = Lambda<Action<object, int, object[], int>>(local, cast, capR, holder, idx, rbuf, off);
			ResR = Lambda<Action<object, int, object[], int>>(local, cast, resR, holder, idx, rbuf, off);
		}

		private static D Lambda<D>(ParameterExpression local, Expression cast, List<Expression> body, params ParameterExpression[] ps)
		{
			if (body.Count == 0) return Expression.Lambda<D>(Expression.Empty(), ps).Compile();
			var all = new List<Expression>(body.Count + 1) { cast }; all.AddRange(body);
			return Expression.Lambda<D>(Expression.Block(new[] { local }, all), ps).Compile();
		}

		private static IEnumerable<FieldInfo> Fields(Type t)
		{
			for (Type b = t; b != null && b != typeof(object) && b != typeof(ValueType); b = b.BaseType)
			{
				foreach (FieldInfo f in b.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
				{
					Type ft = f.FieldType;
					if (ft.IsPointer || ft.IsByRef || ft.IsByRefLike) continue;
					yield return f;
				}
				if (t.IsValueType) break;
			}
		}

		private static readonly Dictionary<Type, bool> UmCache = new();
		/// <summary>A value type holding no reference (and not a pointer).</summary>
		internal static bool Unmanaged(Type ft)
		{
			if (!ft.IsValueType || ft.IsPointer) return false;
			if (ft.IsPrimitive || ft.IsEnum) return true;
			lock (UmCache) { if (!UmCache.TryGetValue(ft, out bool u)) UmCache[ft] = u = ArrOps.Unmanaged(ft); return u; }
		}

		private static readonly Dictionary<Type, int> SizeCache = new();
		private static int SizeOf(Type t)
		{
			lock (SizeCache)
			{
				if (!SizeCache.TryGetValue(t, out int s)) SizeCache[t] = s = (int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf)).MakeGenericMethod(t).Invoke(null, null);
				return s;
			}
		}

		private sealed class RawObj { public byte Data; }
		/// <summary>The first byte of an object's fields (the runtime's own GetRawData trick).</summary>
		public static ref byte RawRef(object o) => ref Unsafe.As<RawObj>(o).Data;

		/// <summary>Byte offset of each field in an instance of <paramref name="t"/>, measured on <paramref name="sample"/> by a tiny IL method (ldflda against the first field byte).</summary>
		private static long[] Offsets(Type t, List<FieldInfo> fields, object sample)
		{
			var dm = new System.Reflection.Emit.DynamicMethod("levy_offsets_" + t.Name, typeof(void), new[] { typeof(object), typeof(long[]) }, typeof(TypePlan).Module, true);
			var il = dm.GetILGenerator();
			var rawRef = typeof(TypePlan).GetMethod(nameof(RawRef), BindingFlags.Public | BindingFlags.Static);
			var byteOffset = typeof(Unsafe).GetMethods().First(m => m.Name == nameof(Unsafe.ByteOffset) && m.IsGenericMethodDefinition && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType.IsByRef).MakeGenericMethod(typeof(byte));
			for (int k = 0; k < fields.Count; k++)
			{
				il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
				il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4, k);
				il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
				il.Emit(System.Reflection.Emit.OpCodes.Call, rawRef);
				il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
				il.Emit(System.Reflection.Emit.OpCodes.Castclass, fields[k].DeclaringType);
				il.Emit(System.Reflection.Emit.OpCodes.Ldflda, fields[k]);
				il.Emit(System.Reflection.Emit.OpCodes.Call, byteOffset);
				il.Emit(System.Reflection.Emit.OpCodes.Conv_I8);
				il.Emit(System.Reflection.Emit.OpCodes.Stelem_I8);
			}
			il.Emit(System.Reflection.Emit.OpCodes.Ret);
			var res = new long[fields.Count];
			((Action<object, long[]>)dm.CreateDelegate(typeof(Action<object, long[]>)))(sample, res);
			return res;
		}

		private static void Walk(Type t, Expression acc, string prefix, bool ro, int depth, List<Leaf> vals, List<Leaf> refs)
		{
			foreach (var f in Fields(t)) WalkField(f, acc, prefix, ro, depth, vals, refs);
		}

		private static void WalkField(FieldInfo f, Expression acc, string prefix, bool ro, int depth, List<Leaf> vals, List<Leaf> refs)
		{
			Type ft = f.FieldType;
			var fa = Expression.Field(acc, f);
			bool fro = ro || f.IsInitOnly || f.IsLiteral;
			string nm = prefix + f.Name;
			if (ft.IsPrimitive || ft.IsEnum) vals.Add(new Leaf { Acc = fa, Ty = ft, Name = nm, RO = fro });
			else if (ft.IsValueType) { if (depth < 8) Walk(ft, fa, nm + ".", fro, depth + 1, vals, refs); }
			else refs.Add(new Leaf { Acc = fa, Ty = ft, Name = nm, RO = fro });
		}

		private static readonly MethodInfo F2I = typeof(BitConverter).GetMethod(nameof(BitConverter.SingleToInt32Bits)), I2F = typeof(BitConverter).GetMethod(nameof(BitConverter.Int32BitsToSingle));
		private static readonly MethodInfo D2L = typeof(BitConverter).GetMethod(nameof(BitConverter.DoubleToInt64Bits)), L2D = typeof(BitConverter).GetMethod(nameof(BitConverter.Int64BitsToDouble));

		private static Expression ToLong(Expression e, Type t)
		{
			if (t == typeof(bool)) return Expression.Condition(e, Expression.Constant(1L), Expression.Constant(0L));
			if (t == typeof(float)) return Expression.Convert(Expression.Call(F2I, e), typeof(long));
			if (t == typeof(double)) return Expression.Call(D2L, e);
			if (t.IsEnum) return Expression.Convert(Expression.Convert(e, Enum.GetUnderlyingType(t)), typeof(long));
			if (t == typeof(IntPtr)) return Expression.Call(e, typeof(IntPtr).GetMethod(nameof(IntPtr.ToInt64)));
			if (t == typeof(UIntPtr)) return Expression.Convert(Expression.Call(e, typeof(UIntPtr).GetMethod(nameof(UIntPtr.ToUInt64))), typeof(long));
			return Expression.Convert(e, typeof(long));
		}

		private static Expression FromLong(Expression x, Type t)
		{
			if (t == typeof(bool)) return Expression.NotEqual(x, Expression.Constant(0L));
			if (t == typeof(float)) return Expression.Call(I2F, Expression.Convert(x, typeof(int)));
			if (t == typeof(double)) return Expression.Call(L2D, x);
			if (t.IsEnum) return Expression.Convert(Expression.Convert(x, Enum.GetUnderlyingType(t)), t);
			if (t == typeof(IntPtr)) return Expression.New(typeof(IntPtr).GetConstructor(new[] { typeof(long) }), x);
			if (t == typeof(UIntPtr)) return Expression.New(typeof(UIntPtr).GetConstructor(new[] { typeof(ulong) }), Expression.Convert(x, typeof(ulong)));
			return Expression.Convert(x, t);
		}
	}

	/// <summary>Copy / compare / restore of a one-dimensional array whose elements hold no references (numbers, bools, plain structs), as raw bytes.</summary>
	internal abstract class ArrOps
	{
		public abstract Array Copy(Array src, Array buf);
		public abstract bool Same(Array a, Array b);
		public abstract void Restore(Array dst, Array buf);
		public abstract void Hash(Array a, ref HashCode h);
		/// <summary>Indices (at most <paramref name="max"/>) where the two arrays of equal length differ.</summary>
		public abstract void Diffs(Array a, Array b, List<int> into, int max);
		private static readonly Dictionary<Type, ArrOps> Cache = new();
		public static ArrOps For(Type elem)
		{
			lock (Cache)
			{
				if (!Cache.TryGetValue(elem, out var o)) Cache[elem] = o = (ArrOps)Activator.CreateInstance(typeof(ArrOps<>).MakeGenericType(elem));
				return o;
			}
		}
		public static bool Unmanaged(Type elem) => elem.IsValueType && !(bool)typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.IsReferenceOrContainsReferences)).MakeGenericMethod(elem).Invoke(null, null);
	}

	internal sealed class ArrOps<T> : ArrOps where T : struct
	{
		private static ReadOnlySpan<byte> B(Array a) => MemoryMarshal.AsBytes(new ReadOnlySpan<T>((T[])a));
		public override Array Copy(Array src, Array buf)
		{
			var s = (T[])src; var b = buf as T[];
			if (b == null || b.Length != s.Length) b = new T[s.Length];
			s.AsSpan().CopyTo(b); return b;
		}
		public override bool Same(Array a, Array b) => a.Length == b.Length && B(a).SequenceEqual(B(b));
		public override void Restore(Array dst, Array buf) { var d = (T[])dst; var b = (T[])buf; if (d.Length == b.Length) b.AsSpan().CopyTo(d); }
		public override void Hash(Array a, ref HashCode h) { h.Add(a.Length); h.AddBytes(B(a)); }
		public override void Diffs(Array a, Array b, List<int> into, int max)
		{
			var x = (T[])a; var y = (T[])b; int n = Math.Min(x.Length, y.Length);
			int sz = Unsafe.SizeOf<T>();
			var bx = B(a); var by = B(b);
			for (int i = 0; i < n && into.Count < max; i++) if (!bx.Slice(i * sz, sz).SequenceEqual(by.Slice(i * sz, sz))) into.Add(i);
		}
	}

	/// <summary>One captured object (or array) of an image.</summary>
	internal sealed class ImgNode
	{
		public object Obj; public string Path; public int Depth;
		public TypePlan P; public long[] V; public object[] R; public ImgNode[] Kid;
		/// <summary>The raw-layout bytes of a class node (<see cref="TypePlan.RangeOff"/>).</summary>
		public byte[] Raw;
		/// <summary>Raw fields: compared segments (buffer start, length) and written segments (object offset, buffer start, length), both after the marks; kept-check field indices.</summary>
		public int[] CmpSeg, ResSeg, CRaw;
		// arrays: Um = raw-byte copy of an unmanaged array; otherwise P is the element plan (struct elements, Elem = true) or null (reference elements: R holds them)
		public ArrOps Um; public Array Copy; public int Len; public bool IsArr;
		public int Stamp, Index; public bool Shared;
		/// <summary>The node whose reference led here (-1 for a root).</summary>
		public int Parent = -1;
		/// <summary>The last non-null reference of each slot that was not followed (so a recapture only re-asks the rules for a new object).</summary>
		public object[] NF;
		/// <summary>Reference slots never followed (an image's <see cref="StateImage.Skip"/> path, or the depth limit).</summary>
		public bool[] NoFollow;
		/// <summary>Leaves not compared (<see cref="StateImage.Mask"/>): value / reference slot indices, or the whole node.</summary>
		public int[] XV, XR; public bool XAll;
		/// <summary>Leaves the gear build was seen to write (<see cref="StateImage.LearnWrites"/>): the whole node (arrays), value slots, reference slots, raw fields.</summary>
		public bool WAll; public bool[] WV, WR, WRaw;
		/// <summary>Raw fields the build counts down by one each time it runs (a cooldown), and a scratch of the fields nothing outside the build touched since the last image.</summary>
		public bool[] WDown, NoDown, UT, DownActive, DownNeg; public sbyte[] LastStep;
		/// <summary>Leaves a merged restore leaves at their current value (a subset of the uncompared ones).</summary>
		public int[] KV, KR; public bool KAll;
		/// <summary>Kept leaves a full build must not write (class 2; class 3 leaves are stepped by the caller).</summary>
		public int[] CV, CR;
		/// <summary>Full leaf names, built once per path (lab diffs).</summary>
		public string[] LeafV, LeafR, LeafRaw; public string LeafPath, ArrLeaf;
		public void Names()
		{
			int nraw = IsArr ? 0 : P?.NRaw ?? 0;
			if (LeafPath == Path && LeafV != null && LeafV.Length == (V?.Length ?? 0) && LeafR.Length == (R?.Length ?? 0) && LeafRaw.Length == nraw) return;
			LeafPath = Path; LeafV = new string[V?.Length ?? 0]; LeafR = new string[R?.Length ?? 0]; LeafRaw = new string[nraw];
			for (int k = 0; k < LeafV.Length; k++) LeafV[k] = Path + "." + StateImage.ValName(this, k);
			for (int k = 0; k < LeafR.Length; k++) LeafR[k] = Path + "." + StateImage.RefName(this, k);
			for (int k = 0; k < nraw; k++) LeafRaw[k] = Path + "." + P.RawNames[k];
		}
	}

	/// <summary>Which references an image follows.</summary>
	internal static class ImgRules
	{
		private static readonly Dictionary<Type, bool> Cache = new();
		private static readonly Assembly Sys = typeof(object).Assembly, Gen = typeof(List<>).Assembly;

		/// <summary>True when an object of runtime type <paramref name="t"/> reached through a field of <paramref name="parent"/> is state to copy, not a shared world object.</summary>
		public static bool Follow(object parent, Type t)
		{
			if (t.IsArray) return t.GetArrayRank() == 1;
			if (t.IsSubclassOf(typeof(ModItem)) || t.IsSubclassOf(typeof(GlobalItem))) return true; // only items followed as roots reach these
			lock (Cache)
			{
				if (Cache.TryGetValue(t, out bool f)) return f;
				Cache[t] = f = Decide(t);
				return f;
			}
		}

		private static bool Decide(Type t)
		{
			if (t == typeof(string) || t.IsPrimitive || typeof(Delegate).IsAssignableFrom(t) || typeof(MemberInfo).IsAssignableFrom(t)) return false;
			if (typeof(Entity).IsAssignableFrom(t) || typeof(ModType).IsAssignableFrom(t) || typeof(Mod).IsAssignableFrom(t) || typeof(ILoadable).IsAssignableFrom(t)) return false;
			string ns = t.Namespace ?? "";
			if (t.IsGenericType && ns == "System.Collections.Generic") return true;
			if (t.IsValueType) return true; // a boxed struct
			if (t.Assembly == Sys || t.Assembly == Gen || ns.StartsWith("System") || ns.StartsWith("Microsoft") || ns.StartsWith("ReLogic") || ns.StartsWith("MonoMod") || ns.StartsWith("Mono.") || ns.StartsWith("log4net") || ns.StartsWith("Newtonsoft") || ns.StartsWith("Steamworks")) return false;
			if (ns.Contains(".UI") || ns.StartsWith("Terraria.Graphics") || ns.Contains("Graphics") || ns.Contains("Shaders") || ns.Contains("Audio") || ns.Contains("Sound") || ns.Contains("Particles") || ns.Contains("Skies") || ns.Contains("Effects")) return false;
			if (t.FullName == "Terraria.Chest" || t.FullName == "Terraria.HitTile" || t.FullName == "Terraria.Player+SavedPlayerDataWithAnnoyingRules") return false;
			if (t.FullName?.StartsWith("Terraria.Localization") == true || t.FullName?.StartsWith("Terraria.DataStructures.IEntitySource") == true) return false;
			return t.IsClass;
		}
	}

	internal sealed class StateImage
	{
		public readonly List<ImgNode> Roots = new();
		private readonly Dictionary<object, ImgNode> _seen = new(ReferenceEqualityComparer.Instance);
		private readonly List<ImgNode> _order = new();
		public int MaxDepth = 4, MaxNodes = 6000, Nodes;
		/// <summary>Paths of references this image never follows (identity only): state no build reads or writes (UI, saves, world interaction).</summary>
		public Func<string, bool> Skip;
		private int _stamp;
		public bool Truncated;

		/// <summary>Captures every root (by identity: a root list of the same objects reuses the buffers).</summary>
		public void Capture(IReadOnlyList<object> roots, IReadOnlyList<string> names)
		{
			_seen.Clear(); _order.Clear(); _stamp++; Nodes = 0; Truncated = false;
			while (Roots.Count > roots.Count) Roots.RemoveAt(Roots.Count - 1);
			for (int i = 0; i < roots.Count; i++)
			{
				object o = roots[i];
				if (o == null) { if (i < Roots.Count) Roots[i] = null; else Roots.Add(null); continue; }
				ImgNode n = i < Roots.Count ? Roots[i] : null;
				if (n == null || !ReferenceEquals(n.Obj, o)) n = new ImgNode();
				if (i < Roots.Count) Roots[i] = n; else Roots.Add(n);
				if (_seen.TryGetValue(o, out var dup)) { Roots[i] = dup; continue; }
				n.Parent = -1; Fill(n, o, names[i], 0);
			}
		}

		private void Fill(ImgNode n, object o, string path, int depth)
		{
			_seen[o] = n; n.Index = _order.Count; _order.Add(n); Nodes++;
			n.Obj = o; n.Path = path; n.Depth = depth; n.Stamp = _stamp; n.Shared = false; n.XV = n.XR = n.KV = n.KR = n.CV = n.CR = null; n.XAll = n.KAll = false; n.CmpSeg = n.ResSeg = n.CRaw = null; n.WAll = false; n.WV = n.WR = n.WRaw = n.WDown = n.NoDown = n.UT = n.DownActive = n.DownNeg = null; n.LastStep = null;
			Type t = o.GetType();
			if (t.IsArray)
			{
				n.IsArr = true; n.Raw = null; var a = (Array)o; n.Len = a.Length; Type et = t.GetElementType();
				if (ArrOps.Unmanaged(et)) { n.Um = ArrOps.For(et); n.Copy = n.Um.Copy(a, n.Copy); n.P = null; n.R = null; n.Kid = null; return; }
				n.Um = null;
				if (et.IsValueType)
				{
					n.P = TypePlan.For(et, true);
					EnsureBufs(n, n.P.NV * n.Len, n.P.NR * n.Len);
					for (int i = 0; i < n.Len; i++) { n.P.CapV(a, i, n.V, i * n.P.NV); n.P.CapR(a, i, n.R, i * n.P.NR); }
				}
				else
				{
					n.P = null; EnsureBufs(n, 0, n.Len);
					Array.Copy(a, n.R, n.Len);
				}
			}
			else
			{
				n.IsArr = false; n.Um = null;
				if (n.P == null || n.P.T != t) { n.P = TypePlan.For(t, false, o); n.V = null; n.R = null; n.Kid = null; n.Raw = null; }
				EnsureBufs(n, n.P.NV, n.P.NR);
				if (n.Raw == null || n.Raw.Length != n.P.RawBytes) n.Raw = new byte[n.P.RawBytes];
				n.P.CapV(o, 0, n.V, 0); n.P.CapR(o, 0, n.R, 0); RawCap(n, o);
			}
			// follow owned references
			int nr = n.R.Length;
			if (n.Kid == null || n.Kid.Length != nr) n.Kid = new ImgNode[nr];
			if (n.NF == null || n.NF.Length != nr) n.NF = new object[nr]; else Array.Clear(n.NF);
			if (n.NoFollow == null || n.NoFollow.Length != nr) n.NoFollow = new bool[nr]; else Array.Clear(n.NoFollow);
			if (depth >= MaxDepth) Array.Fill(n.NoFollow, true);
			else if (Skip != null) for (int k = 0; k < nr; k++) if (n.R[k] != null && Skip(path + "." + RefName(n, k))) n.NoFollow[k] = true;
			for (int k = 0; k < nr; k++)
			{
				object c = n.R[k];
				if (c == null) { n.Kid[k] = null; continue; }
				if (n.NoFollow[k] || !ImgRules.Follow(o, c.GetType())) { n.Kid[k] = null; n.NF[k] = c; continue; }
				if (_seen.TryGetValue(c, out var dup)) { n.Kid[k] = dup; dup.Shared = true; continue; }
				if (Nodes >= MaxNodes) { Truncated = true; n.Kid[k] = null; n.NF[k] = c; continue; }
				ImgNode kn = n.Kid[k];
				if (kn == null || !ReferenceEquals(kn.Obj, c) || kn.Stamp == _stamp) kn = new ImgNode();
				n.Kid[k] = kn;
				kn.Parent = n.Index; Fill(kn, c, path + "." + RefName(n, k), depth + 1);
			}
		}

		private static void EnsureBufs(ImgNode n, int nv, int nr)
		{
			if (n.V == null || n.V.Length != nv) n.V = new long[nv];
			if (n.R == null || n.R.Length != nr) n.R = new object[nr];
		}

		public static string RefName(ImgNode n, int k)
		{
			if (!n.IsArr) return n.P.RefNames[k];
			if (n.P == null) return "[" + k + "]";
			return "[" + k / n.P.NR + "]." + n.P.RefNames[k % n.P.NR];
		}

		public static string ValName(ImgNode n, int k)
		{
			if (!n.IsArr) return n.P.ValNames[k];
			return "[" + k / n.P.NV + "]." + n.P.ValNames[k % n.P.NV];
		}

		/// <summary>Writes every captured field, element and followed object back (read-only fields excepted: nothing can have changed them).</summary>
		public void Restore()
		{
			for (int i = 0; i < _order.Count; i++)
			{
				var n = _order[i]; object o = n.Obj;
				if (n.IsArr)
				{
					var a = (Array)o;
					if (n.Um != null) n.Um.Restore(a, n.Copy);
					else if (n.P != null) for (int e = 0; e < n.Len && e < a.Length; e++) { n.P.ResV(a, e, n.V, e * n.P.NV); n.P.ResR(a, e, n.R, e * n.P.NR); }
					else Array.Copy(n.R, a, Math.Min(n.Len, a.Length));
				}
				else { n.P.ResV(o, 0, n.V, 0); n.P.ResR(o, 0, n.R, 0); RawRes(n, o); }
			}
		}

		// ------------------------------------------------------------------------------------------------------------
		// Raw ranges
		// ------------------------------------------------------------------------------------------------------------
		private static void RawCap(ImgNode n, object o)
		{
			var p = n.P; if (p.RawBytes == 0) return;
			ref byte src = ref TypePlan.RawRef(o);
			for (int r = 0; r < p.RangeOff.Length; r++) Unsafe.CopyBlockUnaligned(ref n.Raw[p.RangeBuf[r]], ref Unsafe.Add(ref src, p.RangeOff[r]), (uint)p.RangeLen[r]);
		}

		private static void RawRes(ImgNode n, object o)
		{
			var p = n.P; if (p.RawBytes == 0) return;
			ref byte dst = ref TypePlan.RawRef(o);
			for (int r = 0; r < p.RangeOff.Length; r++) Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dst, p.RangeOff[r]), ref n.Raw[p.RangeBuf[r]], (uint)p.RangeLen[r]);
		}

		/// <summary>Writes the image's raw bytes back except the fields <paramref name="segs"/> leaves out (object offset, buffer start, length triples).</summary>
		private static void RawResSeg(ImgNode n, object o, int[] segs)
		{
			ref byte dst = ref TypePlan.RawRef(o);
			for (int k = 0; k < segs.Length; k += 3) Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dst, segs[k]), ref n.Raw[segs[k + 1]], (uint)segs[k + 2]);
		}

		/// <summary>Index of the raw field that holds byte <paramref name="pos"/> of the raw buffer.</summary>
		private static int RawFieldAt(TypePlan p, int pos)
		{
			for (int k = 0; k < p.NRaw; k++) if (pos >= p.RawBuf[k] && pos < p.RawBuf[k] + p.RawSize[k]) return k;
			return -1;
		}

		/// <summary>Every leaf as (path, exact value hash), in capture order. Arrays without references are one leaf (their bytes); followed objects add their own leaves.</summary>
		public void Flatten(Action<string, long> emit)
		{
			foreach (var n in _order)
			{
				if (n.IsArr && n.Um != null) { var h = new HashCode(); n.Um.Hash(n.Copy, ref h); if (n.ArrLeaf == null || n.ArrLeaf.Length != n.Path.Length + 2 || !n.ArrLeaf.StartsWith(n.Path, StringComparison.Ordinal)) n.ArrLeaf = n.Path + "[]"; emit(n.ArrLeaf, h.ToHashCode()); continue; }
				n.Names();
				for (int k = 0; k < n.LeafRaw.Length; k++) emit(n.LeafRaw[k], RawValue(n, k));
				for (int k = 0; k < n.V.Length; k++) emit(n.LeafV[k], n.V[k]);
				for (int k = 0; k < n.R.Length; k++) emit(n.LeafR[k], n.Kid != null && n.Kid[k] != null ? TypeToken(n.R[k]) : RefHash(n.R[k]));
			}
		}

		private static long RawValue(ImgNode n, int k)
		{
			var sp = n.Raw.AsSpan(n.P.RawBuf[k], n.P.RawSize[k]);
			if (sp.Length <= 8) { long v = 0; for (int b = sp.Length - 1; b >= 0; b--) v = (v << 8) | sp[b]; return v; }
			var h = new HashCode(); h.AddBytes(sp); return h.ToHashCode() ^ ((long)sp.Length << 40);
		}

		/// <summary>A reference by identity; a string by value; a delegate by its method and target type (a frame that rebuilds a list of the same lambdas makes new delegate objects with the same behaviour).</summary>
		public static long RefHash(object o)
		{
			if (o == null) return 0;
			if (o is string s) return s.GetHashCode() ^ 0x5bd1e995L;
			if (o is Delegate dl) return HashCode.Combine(dl.Method.MethodHandle.Value, dl.Target?.GetType(), dl.GetInvocationList().Length) ^ 0x2545F491L;
			return (long)RuntimeHelpers.GetHashCode(o) | ((long)o.GetType().GetHashCode() << 32);
		}

		/// <summary>A followed object compares by its contents (its own leaves), not by identity: a frame that re-creates an owned object with the same contents changes nothing.</summary>
		public static long TypeToken(object o) => o == null ? 0 : ((long)o.GetType().GetHashCode() << 1) | 1;

		/// <summary>Leaves that differ between two images of the same objects (b against this); at most <paramref name="max"/> names.</summary>
		public int DiffInto(StateImage b, List<string> into, int max)
		{
			var map = new Dictionary<string, long>(); int diffs = 0;
			b.Flatten((p, v) => map[p] = v);
			var seen = new HashSet<string>();
			Flatten((p, v) =>
			{
				seen.Add(p);
				if (!map.TryGetValue(p, out long w) || w != v) { diffs++; if (into.Count < max) into.Add(p); }
			});
			foreach (var p in map.Keys) if (!seen.Contains(p)) { diffs++; if (into.Count < max) into.Add(p + " (only in one)"); }
			return diffs;
		}

		// ------------------------------------------------------------------------------------------------------------
		// The per-tick path (gear cache): the same shape re-captured without the follow bookkeeping, masked comparison, merged restore
		// ------------------------------------------------------------------------------------------------------------
		public IReadOnlyList<ImgNode> Order => _order;

		/// <summary>
		/// Re-captures the shape the last <see cref="Capture"/> built: every node from its object. A followed reference that now points at another object of the same type (an owned list a hook re-created) is
		/// re-bound to it (its contents are captured right after, later in the order). False when the shape changed (a followed reference appeared, vanished, changed type or length, or a shared node moved): call Capture.
		/// </summary>
		/// <summary>The node whose change made the last <see cref="Recapture"/> fail (lab).</summary>
		public ImgNode FailAt;
		/// <summary>Lab: when set, every node's recapture time is added here by path.</summary>
		public Dictionary<string, long> ProfileInto;
		/// <summary>Re-captures only the listed nodes, from the objects they were last captured from (no shape check: the caller takes a full image often enough to follow a re-created object).</summary>
		public void RecaptureOnly(int[] only)
		{
			foreach (int i in only)
			{
				var n = _order[i]; object o = n.Obj;
				if (n.IsArr)
				{
					var a = (Array)o; if (a.Length != n.Len) continue;
					if (n.Um != null) { n.Copy = n.Um.Copy(a, n.Copy); continue; }
					if (n.P != null) for (int e = 0; e < n.Len; e++) { n.P.CapV(a, e, n.V, e * n.P.NV); n.P.CapR(a, e, n.R, e * n.P.NR); }
					else Array.Copy(a, n.R, n.Len);
				}
				else { n.P.CapV(o, 0, n.V, 0); n.P.CapR(o, 0, n.R, 0); RawCap(n, o); }
			}
		}

		/// <summary>The listed nodes and every node on the path from a root to them, in capture order (for <see cref="RecaptureSet"/>).</summary>
		public int[] WithAncestors(int[] nodes)
		{
			var mark = new bool[_order.Count];
			foreach (int i in nodes) for (int j = i; j >= 0 && !mark[j]; j = _order[j].Parent) mark[j] = true;
			var l = new List<int>(); for (int i = 0; i < mark.Length; i++) if (mark[i]) l.Add(i);
			return l.ToArray();
		}

		/// <summary>
		/// <see cref="Recapture"/> on the listed nodes only (a set closed under parents, in capture order): their values are captured again and a followed reference that now points at another object of the same type is
		/// re-bound to it, so a list or array the build makes anew is the one captured and written back. False when the shape changed (take a full image).
		/// </summary>
		public bool RecaptureSet(int[] set)
		{
			FailAt = null;
			foreach (int i in set)
			{
				var n = _order[i]; object o = n.Obj;
				if (n.IsArr)
				{
					var a = (Array)o; if (a.Length != n.Len) { FailAt = n; return false; }
					if (n.Um != null) { n.Copy = n.Um.Copy(a, n.Copy); continue; }
					if (n.P != null) for (int e = 0; e < n.Len; e++) { n.P.CapV(a, e, n.V, e * n.P.NV); n.P.CapR(a, e, n.R, e * n.P.NR); }
					else Array.Copy(a, n.R, n.Len);
				}
				else { n.P.CapV(o, 0, n.V, 0); n.P.CapR(o, 0, n.R, 0); RawCap(n, o); }
				var kid = n.Kid; var r = n.R;
				for (int k = 0; k < kid.Length; k++)
				{
					object c = r[k]; var kn = kid[k];
					if (kn != null)
					{
						if (ReferenceEquals(c, kn.Obj)) continue;
						if (c == null || kn.Shared || c.GetType() != kn.Obj.GetType() || (c is Array ca && ca.Length != kn.Len)) { FailAt = kn; return false; }
						kn.Obj = c;
					}
					else if (c != null && !ReferenceEquals(c, n.NF[k]))
					{
						if (!n.NoFollow[k] && ImgRules.Follow(o, c.GetType())) { FailAt = n; return false; }
						n.NF[k] = c;
					}
				}
			}
			return true;
		}

		public bool Recapture()
		{
			FailAt = null;
			long pt = 0; var prof = ProfileInto;
			for (int i = 0; i < _order.Count; i++)
			{
				if (prof != null) { long now = System.Diagnostics.Stopwatch.GetTimestamp(); if (i > 0) { string pk = _order[i - 1].Path; prof.TryGetValue(pk, out long q); prof[pk] = q + now - pt; } pt = now; }
				var n = _order[i]; object o = n.Obj;
				if (n.IsArr)
				{
					var a = (Array)o; if (a.Length != n.Len) { FailAt = n; return false; }
					if (n.Um != null) { n.Copy = n.Um.Copy(a, n.Copy); continue; }
					if (n.P != null) for (int e = 0; e < n.Len; e++) { n.P.CapV(a, e, n.V, e * n.P.NV); n.P.CapR(a, e, n.R, e * n.P.NR); }
					else Array.Copy(a, n.R, n.Len);
				}
				else { n.P.CapV(o, 0, n.V, 0); n.P.CapR(o, 0, n.R, 0); RawCap(n, o); }
				var kid = n.Kid; var r = n.R;
				for (int k = 0; k < kid.Length; k++)
				{
					object c = r[k]; var kn = kid[k];
					if (kn != null)
					{
						if (ReferenceEquals(c, kn.Obj)) continue;
						if (c == null || kn.Shared || c.GetType() != kn.Obj.GetType() || (c is Array ca && ca.Length != kn.Len)) { FailAt = kn; return false; }
						kn.Obj = c;
					}
					else if (c != null && !ReferenceEquals(c, n.NF[k]))
					{
						if (!n.NoFollow[k] && ImgRules.Follow(o, c.GetType())) { FailAt = n; return false; }
						n.NF[k] = c;
					}
				}
			}
			return true;
		}

		/// <summary>Captures (re-using the shape when it still fits): true when the shape was kept.</summary>
		public bool Take(IReadOnlyList<object> roots, IReadOnlyList<string> names)
		{
			if (_order.Count > 0 && Roots.Count == roots.Count)
			{
				bool same = true;
				for (int i = 0; i < roots.Count && same; i++) same = Roots[i] == null ? roots[i] == null : ReferenceEquals(Roots[i].Obj, roots[i]);
				if (same && Recapture()) return true;
			}
			Capture(roots, names);
			return false;
		}

		/// <summary>
		/// Classifies every leaf: 0 compared, 1 not compared (a merged restore writes the image's value), 2 not compared and kept (a merged restore leaves the current value; a full build must not write it), 3 kept and
		/// stepped by the caller (a full build writes it). <paramref name="leaf"/> by full leaf name, <paramref name="node"/> by node path (the whole node: 1 or 0).
		/// </summary>
		public void Mask(Func<string, int> leaf, Func<string, int> node)
		{
			var xv = new List<int>(); var xr = new List<int>(); var kv = new List<int>(); var kr = new List<int>(); var cv = new List<int>(); var cr = new List<int>();
			foreach (var n in _order)
			{
				int nc = node(n.Path); n.XAll = nc >= 1; n.KAll = nc >= 2; n.XV = n.XR = n.KV = n.KR = n.CV = n.CR = null;
				if (n.XAll || (n.IsArr && n.Um != null)) continue;
				n.Names(); xv.Clear(); xr.Clear(); kv.Clear(); kr.Clear(); cv.Clear(); cr.Clear();
				for (int k = 0; k < n.LeafV.Length; k++) { int c = leaf(n.LeafV[k]); if (c >= 1) xv.Add(k); if (c >= 2) kv.Add(k); if (c == 2) cv.Add(k); }
				for (int k = 0; k < n.LeafR.Length; k++) { int c = leaf(n.LeafR[k]); if (c >= 1) xr.Add(k); if (c >= 2) kr.Add(k); if (c == 2) cr.Add(k); }
				if (xv.Count > 0) n.XV = xv.ToArray(); if (xr.Count > 0) n.XR = xr.ToArray();
				if (kv.Count > 0) n.KV = kv.ToArray(); if (kr.Count > 0) n.KR = kr.ToArray();
				if (cv.Count > 0) n.CV = cv.ToArray(); if (cr.Count > 0) n.CR = cr.ToArray();
				if (!n.IsArr && n.P.NRaw > 0)
				{
					var p = n.P; var xRaw = new bool[p.NRaw]; var kRaw = new bool[p.NRaw]; var cRaw = new List<int>(); bool anyX = false, anyK = false;
					for (int k = 0; k < p.NRaw; k++) { int c = leaf(n.LeafRaw[k]); xRaw[k] = c >= 1; kRaw[k] = c >= 2; if (c == 2) cRaw.Add(k); anyX |= xRaw[k]; anyK |= kRaw[k]; }
					if (anyX) n.CmpSeg = Segments(p, xRaw, true);
					if (anyK) n.ResSeg = Segments(p, kRaw, true);
					if (cRaw.Count > 0) n.CRaw = cRaw.ToArray();
				}
			}
		}

		/// <summary>The raw ranges with the marked fields cut out: (buffer start, length) pairs, or with <paramref name="withObj"/> (object offset, buffer start, length) triples.</summary>
		private static int[] Segments(TypePlan p, bool[] cut, bool withObj)
		{
			var o = new List<int>();
			for (int r = 0; r < p.RangeOff.Length; r++)
			{
				int a = p.RangeOff[r], end = a + p.RangeLen[r];
				// raw fields are sorted by offset
				for (int k = 0; k < p.NRaw; k++)
				{
					int fo = p.RawOff[k]; if (fo < p.RangeOff[r] || fo >= end || !cut[k]) continue;
					if (fo > a) Emit(a, fo);
					a = Math.Max(a, fo + p.RawSize[k]);
				}
				if (end > a) Emit(a, end);
				void Emit(int from, int to) { int bp = p.RangeBuf[r] + from - p.RangeOff[r]; if (withObj) o.Add(from); o.Add(bp); o.Add(to - from); }
			}
			return o.ToArray();
		}

		/// <summary>Exchanges the whole contents (captured shape, buffers, marks) with <paramref name="b"/>.</summary>
		public void Swap(StateImage b)
		{
			var r = new List<ImgNode>(Roots); Roots.Clear(); Roots.AddRange(b.Roots); b.Roots.Clear(); b.Roots.AddRange(r);
			var o = new List<ImgNode>(_order); _order.Clear(); _order.AddRange(b._order); b._order.Clear(); b._order.AddRange(o);
			(Nodes, b.Nodes) = (b.Nodes, Nodes); (Truncated, b.Truncated) = (b.Truncated, Truncated); (_stamp, b._stamp) = (b._stamp, _stamp);
			_seen.Clear(); b._seen.Clear();
		}

		/// <summary>Both images hold nodes of the same kinds in the same order (captured from the same roots with the same shape).</summary>
		public bool Aligned(StateImage b)
		{
			if (b._order.Count != _order.Count) return false;
			for (int i = 0; i < _order.Count; i++)
			{
				var x = _order[i]; var y = b._order[i];
				if (x.IsArr != y.IsArr || x.Len != y.Len || x.P != y.P || x.Um != y.Um || (x.V?.Length ?? 0) != (y.V?.Length ?? 0) || (x.R?.Length ?? 0) != (y.R?.Length ?? 0)) return false;
			}
			return true;
		}

		public static bool RefSame(object a, object b, bool followed)
		{
			if (ReferenceEquals(a, b)) return true;
			if (a == null || b == null) return false;
			if (followed) return a.GetType() == b.GetType();
			if (a is string sa) return b is string sb && sa == sb;
			if (a is Delegate da && b is Delegate db) return da.Method == db.Method && da.Target?.GetType() == db.Target?.GetType() && da.GetInvocationList().Length == db.GetInvocationList().Length;
			return false;
		}

		/// <summary>
		/// Index of the first node of <paramref name="b"/> (aligned with this) whose values differ from this image's outside this image's excluded leaves, or -1 when they all match. Followed references compare by type (their
		/// contents are their own nodes); others by identity (strings by value, delegates by method and target type).
		/// </summary>
		public int FirstDiff(StateImage b, out int slot)
		{
			slot = -1;
			for (int i = 0; i < _order.Count; i++)
			{
				var x = _order[i]; var y = b._order[i];
				if (x.XAll) continue;
				if (x.IsArr && x.Um != null) { if (!x.Um.Same(x.Copy, y.Copy)) return i; continue; }
				if (x.Raw != null && x.Raw.Length > 0)
				{
					if (x.CmpSeg == null) { if (!x.Raw.AsSpan().SequenceEqual(y.Raw)) { slot = RawSlot(x, y, 0, x.Raw.Length); return i; } }
					else for (int g = 0; g < x.CmpSeg.Length; g += 3) if (!x.Raw.AsSpan(x.CmpSeg[g + 1], x.CmpSeg[g + 2]).SequenceEqual(y.Raw.AsSpan(x.CmpSeg[g + 1], x.CmpSeg[g + 2]))) { slot = RawSlot(x, y, x.CmpSeg[g + 1], x.CmpSeg[g + 2]); return i; }
				}
				var xv = x.V; var yv = y.V;
				if (x.XV == null) { if (!xv.AsSpan().SequenceEqual(yv)) { slot = Mismatch(xv, yv, null); return i; } }
				else { int m = Mismatch(xv, yv, x.XV); if (m >= 0) { slot = m; return i; } }
				var xr = x.R; var yr = y.R; var kid = x.Kid; var ex = x.XR; int e = 0;
				for (int k = 0; k < xr.Length; k++)
				{
					if (ex != null && e < ex.Length && ex[e] == k) { e++; continue; }
					if (!RefSame(xr[k], yr[k], kid[k] != null)) { slot = xv.Length + k; return i; }
				}
			}
			return -1;
		}

		/// <summary>The slot code of the first raw field that differs in [from, from+len): -2 - field index.</summary>
		private static int RawSlot(ImgNode x, ImgNode y, int from, int len)
		{
			for (int b = from; b < from + len; b++) if (x.Raw[b] != y.Raw[b]) return -2 - RawFieldAt(x.P, b);
			return -1;
		}

		private static int Mismatch(long[] a, long[] b, int[] ex, int len = -1)
		{
			int e = 0; if (len < 0) len = a.Length;
			for (int k = 0; k < len; k++)
			{
				if (ex != null && e < ex.Length && ex[e] == k) { e++; continue; }
				if (a[k] != b[k]) return k;
			}
			return -1;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Against the live objects (no capture): the cache's per-tick check and its write-back
		// ------------------------------------------------------------------------------------------------------------
		private object[] _live = new object[64]; private long[] _sv = new long[64]; private object[] _sr = new object[64];
		/// <summary>The live object of each node, as the last <see cref="FirstDiffLive"/> walked them.</summary>
		public object[] Live => _live;

		/// <summary>
		/// Walks the live objects along this image's shape (roots given; every followed reference read from its live parent) and returns the index of the first node whose live values differ from the image outside the
		/// excluded leaves, -1 when none does, or -2 when the live shape no longer fits (a root, a followed object's type or an array's length changed). Fills <see cref="Live"/>.
		/// </summary>
		public int FirstDiffLive(IReadOnlyList<object> roots, out int slot)
		{
			slot = -1;
			int cnt = _order.Count;
			if (cnt == 0 || Roots.Count != roots.Count) return -2;
			if (_live.Length < cnt) _live = new object[cnt * 2];
			Array.Clear(_live, 0, cnt);
			for (int i = 0; i < roots.Count; i++) { var rn = Roots[i]; if (rn == null ? roots[i] != null : !ReferenceEquals(rn.Obj, roots[i])) return -2; if (rn != null) _live[rn.Index] = roots[i]; }
			int diff = -1;
			for (int i = 0; i < cnt; i++)
			{
				var n = _order[i]; object o = _live[i];
				if (o == null) return -2;
				if (n.IsArr)
				{
					var a = (Array)o;
					if (a.GetType() != n.Obj.GetType() || a.Length != n.Len) return -2;
					if (n.Um != null) { if (diff < 0 && !n.XAll && !n.Um.Same(a, n.Copy)) diff = i; continue; }
					if (n.P != null)
					{
						int nv = n.P.NV, nr = n.P.NR;
						if (_sv.Length < nv) _sv = new long[nv]; if (_sr.Length < nr) _sr = new object[nr];
						for (int e = 0; e < n.Len; e++)
						{
							n.P.CapV(a, e, _sv, 0); n.P.CapR(a, e, _sr, 0);
							if (diff < 0 && !n.XAll) { for (int k = 0; k < nv; k++) if (_sv[k] != n.V[e * nv + k]) { diff = i; break; } }
							for (int k = 0; k < nr; k++)
							{
								var kn = n.Kid[e * nr + k];
								if (kn != null) { if (_sr[k] == null) return -2; _live[kn.Index] = _sr[k]; }
								if (diff < 0 && !n.XAll && !RefSame(_sr[k], n.R[e * nr + k], kn != null)) diff = i;
							}
						}
					}
					else
					{
						var oa = (object[])a;
						for (int k = 0; k < n.Len; k++)
						{
							object c = oa[k]; var kn = n.Kid[k];
							if (kn != null) { if (c == null) return -2; _live[kn.Index] = c; }
							if (diff < 0 && !n.XAll && !RefSame(c, n.R[k], kn != null)) diff = i;
						}
					}
					continue;
				}
				if (o.GetType() != n.P.T) return -2;
				if (diff < 0 && !n.XAll && n.Raw != null && n.Raw.Length > 0)
				{
					ref byte src = ref TypePlan.RawRef(o); var p = n.P;
					if (n.CmpSeg == null) { for (int r = 0; r < p.RangeOff.Length; r++) if (!MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref src, p.RangeOff[r]), p.RangeLen[r]).SequenceEqual(n.Raw.AsSpan(p.RangeBuf[r], p.RangeLen[r]))) { diff = i; slot = LiveRawSlot(n, o, p.RangeOff[r], p.RangeBuf[r], p.RangeLen[r]); break; } }
					else for (int g = 0; g < n.CmpSeg.Length; g += 3) if (!MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref src, n.CmpSeg[g]), n.CmpSeg[g + 2]).SequenceEqual(n.Raw.AsSpan(n.CmpSeg[g + 1], n.CmpSeg[g + 2]))) { diff = i; slot = LiveRawSlot(n, o, n.CmpSeg[g], n.CmpSeg[g + 1], n.CmpSeg[g + 2]); break; }
				}
				int nv2 = n.V.Length, nr2 = n.R.Length;
				if (diff < 0 && !n.XAll && nv2 > 0)
				{
					if (_sv.Length < nv2) _sv = new long[nv2];
					n.P.CapV(o, 0, _sv, 0);
					int m = Mismatch(_sv, n.V, n.XV, nv2); if (m >= 0) { diff = i; slot = m; }
				}
				if (nr2 > 0)
				{
					if (_sr.Length < nr2) _sr = new object[nr2];
					n.P.CapR(o, 0, _sr, 0);
					var ex = n.XR; int e2 = 0;
					for (int k = 0; k < nr2; k++)
					{
						var kn = n.Kid[k];
						if (kn != null) { if (_sr[k] == null) return -2; _live[kn.Index] = _sr[k]; }
						if (ex != null && e2 < ex.Length && ex[e2] == k) { e2++; continue; }
						if (diff < 0 && !n.XAll && !RefSame(_sr[k], n.R[k], kn != null)) { diff = i; slot = nv2 + k; }
					}
				}
			}
			return diff;
		}

		private static int LiveRawSlot(ImgNode n, object o, int objOff, int bufPos, int len)
		{
			ref byte src = ref TypePlan.RawRef(o);
			for (int b = 0; b < len; b++) if (Unsafe.Add(ref src, objOff + b) != n.Raw[bufPos + b]) return -2 - RawFieldAt(n.P, bufPos + b);
			return -1;
		}

		/// <summary>
		/// Writes the listed nodes of this image into the live objects <paramref name="live"/> (aligned with this image's order): values and raw bytes from the image, except the kept leaves (this image's marks), which keep
		/// their live values; followed references keep their live object (whose contents are written as its own node), others take the image's reference.
		/// </summary>
		public void RestoreInto(object[] live, int[] only)
		{
			foreach (int i in only)
			{
				var n = _order[i]; object o = live[i];
				if (n.KAll || o == null) continue;
				if (n.IsArr)
				{
					var a = (Array)o;
					if (n.Um != null) { n.Um.Restore(a, n.Copy); continue; }
					if (n.P != null)
					{
						int nr = n.P.NR; if (_sr.Length < nr) _sr = new object[nr]; var tr = new object[nr];
						for (int e = 0; e < n.Len && e < a.Length; e++)
						{
							n.P.CapR(a, e, _sr, 0);
							for (int k = 0; k < nr; k++) tr[k] = n.Kid[e * nr + k] != null ? _sr[k] : n.R[e * nr + k];
							n.P.ResV(a, e, n.V, e * n.P.NV); n.P.ResR(a, e, tr, 0);
						}
					}
					else { var oa = (object[])a; for (int k = 0; k < n.Len && k < oa.Length; k++) if (n.Kid[k] == null) oa[k] = n.R[k]; }
					continue;
				}
				long[] v = n.V; object[] r = n.R;
				if (n.KV != null)
				{
					if (_sv.Length < v.Length) _sv = new long[v.Length];
					n.P.CapV(o, 0, _sv, 0);
					if (_tv.Length < v.Length) _tv = new long[v.Length];
					Array.Copy(v, _tv, v.Length); foreach (int k in n.KV) _tv[k] = _sv[k]; v = _tv;
				}
				bool anyKid = false; foreach (var kk in n.Kid) if (kk != null) { anyKid = true; break; }
				if (n.KR != null || anyKid)
				{
					if (_sr.Length < r.Length) _sr = new object[r.Length];
					n.P.CapR(o, 0, _sr, 0);
					if (_tr.Length < r.Length) _tr = new object[r.Length];
					Array.Copy(r, _tr, r.Length);
					if (n.KR != null) foreach (int k in n.KR) _tr[k] = _sr[k];
					for (int k = 0; k < r.Length; k++) if (n.Kid[k] != null) _tr[k] = _sr[k];
					r = _tr;
				}
				n.P.ResV(o, 0, v, 0); n.P.ResR(o, 0, r, 0);
				if (r == _tr) Array.Clear(_tr, 0, n.R.Length);
				if (n.P.RawBytes > 0) { if (n.ResSeg == null) RawRes(n, o); else RawResSeg(n, o, n.ResSeg); }
			}
		}

		/// <summary>Name of a leaf <see cref="FirstDiff"/> reported (value slots first, then reference slots).</summary>
		public string LeafName(int node, int slot)
		{
			var n = _order[node]; if (n.IsArr && n.Um != null) return n.Path + "[]";
			n.Names(); return slot <= -2 && -2 - slot < n.LeafRaw.Length ? n.LeafRaw[-2 - slot] : slot < 0 ? n.Path : slot < n.LeafV.Length ? n.LeafV[slot] : n.LeafR[slot - n.LeafV.Length];
		}

		/// <summary>Indices of the nodes on which this image and <paramref name="b"/> (aligned) differ anywhere, plus those holding leaves marked "not compared, written back" (class 1), which a merged restore must always write.</summary>
		public int[] NodesToWrite(StateImage b)
		{
			var o = new List<int>();
			for (int i = 0; i < _order.Count; i++)
			{
				var x = _order[i]; var y = b._order[i];
				bool w = (x.XAll && !x.KAll) || (x.XV != null && (x.KV == null || x.XV.Length != x.KV.Length)) || (x.XR != null && (x.KR == null || x.XR.Length != x.KR.Length)) || (x.CmpSeg != null && !SameSegs(x));
				if (!w)
				{
					if (x.IsArr && x.Um != null) w = !x.Um.Same(x.Copy, y.Copy);
					else
					{
						w = (x.Raw != null && !x.Raw.AsSpan().SequenceEqual(y.Raw)) || !x.V.AsSpan().SequenceEqual(y.V);
						for (int k = 0; !w && k < x.R.Length; k++) w = !RefSame(x.R[k], y.R[k], x.Kid[k] != null);
					}
				}
				if (w) o.Add(i);
			}
			return o.ToArray();
		}

		/// <summary>The raw fields this node does not compare are all kept ones (none is "written back").</summary>
		private static bool SameSegs(ImgNode x)
		{
			if (x.ResSeg == null) return false;
			int cmp = 0, res = 0; for (int g = 2; g < x.CmpSeg.Length; g += 3) cmp += x.CmpSeg[g]; for (int g = 2; g < x.ResSeg.Length; g += 3) res += x.ResSeg[g];
			return cmp == res;
		}

		/// <summary>True when this image and <paramref name="b"/> (aligned) agree on every leaf this image keeps: whatever wrote b did not touch them.</summary>
		public bool SameOnKept(StateImage b, out int node)
		{
			for (int i = 0; i < _order.Count; i++)
			{
				node = i;
				var x = _order[i]; var y = b._order[i];
				if (x.KAll)
				{
					if (x.IsArr && x.Um != null) { if (!x.Um.Same(x.Copy, y.Copy)) return false; continue; }
					if (!x.V.AsSpan().SequenceEqual(y.V)) return false;
					for (int k = 0; k < x.R.Length; k++) if (!RefSame(x.R[k], y.R[k], x.Kid[k] != null)) return false;
					continue;
				}
				if (x.CV != null) foreach (int k in x.CV) if (x.V[k] != y.V[k]) return false;
				if (x.CRaw != null) foreach (int k in x.CRaw) if (!x.Raw.AsSpan(x.P.RawBuf[k], x.P.RawSize[k]).SequenceEqual(y.Raw.AsSpan(x.P.RawBuf[k], x.P.RawSize[k]))) return false;
				if (x.CR != null) foreach (int k in x.CR) if (!RefSame(x.R[k], y.R[k], x.Kid[k] != null)) return false;
			}
			node = -1;
			return true;
		}

		private long[] _tv = new long[64]; private object[] _tr = new object[64];

		/// <summary>
		/// Writes this image back, except the leaves <paramref name="cur"/> (aligned, captured now) keeps, which keep their current values. Followed objects are written into the objects this image captured.
		/// </summary>
		public void RestoreExcept(StateImage cur, int[] only = null)
		{
			int cnt = only?.Length ?? _order.Count;
			for (int q = 0; q < cnt; q++)
			{
				int i = only != null ? only[q] : q;
				var n = _order[i]; var c = cur._order[i]; object o = n.Obj;
				if (c.KAll) continue;
				if (n.IsArr)
				{
					var a = (Array)o;
					if (n.Um != null) n.Um.Restore(a, n.Copy);
					else if (n.P != null) for (int e = 0; e < n.Len && e < a.Length; e++) { n.P.ResV(a, e, n.V, e * n.P.NV); n.P.ResR(a, e, n.R, e * n.P.NR); }
					else Array.Copy(n.R, a, Math.Min(n.Len, a.Length));
					continue;
				}
				long[] v = n.V; object[] r = n.R;
				if (c.KV != null) { if (_tv.Length < v.Length) _tv = new long[v.Length]; Array.Copy(v, _tv, v.Length); foreach (int k in c.KV) _tv[k] = c.V[k]; v = _tv; }
				if (c.KR != null) { if (_tr.Length < r.Length) _tr = new object[r.Length]; Array.Copy(r, _tr, r.Length); foreach (int k in c.KR) _tr[k] = c.R[k]; r = _tr; }
				n.P.ResV(o, 0, v, 0); n.P.ResR(o, 0, r, 0);
				if (r == _tr) Array.Clear(_tr, 0, n.R.Length);
				if (n.P.RawBytes > 0) { if (c.ResSeg == null) RawRes(n, o); else RawResSeg(n, o, c.ResSeg); }
			}
		}


		// ------------------------------------------------------------------------------------------------------------
		// The approximate gear frame (SoldierNPC.GearApprox.cs): what the build writes, learned from images taken either side of it, and written back on the ticks it does not run
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>This image (after a build) against <paramref name="pre"/> (aligned, before it): every leaf that differs is marked as written by the build. Marks accumulate until the shape is captured afresh.</summary>
		public bool LearnWrites(StateImage pre)
		{
			bool grew = false;
			for (int i = 0; i < _order.Count; i++)
			{
				var y = _order[i]; var x = pre._order[i];
				if (y.WAll) continue;
				if (y.IsArr)
				{
					bool d;
					if (y.Um != null) d = !y.Um.Same(x.Copy, y.Copy);
					else
					{
						d = !x.V.AsSpan().SequenceEqual(y.V);
						for (int k = 0; !d && k < y.R.Length; k++) d = y.Kid[k] == null && !RefSame(x.R[k], y.R[k], false);
					}
					if (d) { y.WAll = true; grew = true; }
					continue;
				}
				if (!x.V.AsSpan().SequenceEqual(y.V)) for (int k = 0; k < y.V.Length; k++) if (x.V[k] != y.V[k] && !(y.WV != null && y.WV[k])) { (y.WV ??= new bool[y.V.Length])[k] = true; grew = true; }
				for (int k = 0; k < y.R.Length; k++) if (y.Kid[k] == null && !RefSame(x.R[k], y.R[k], false) && !(y.WR != null && y.WR[k])) { (y.WR ??= new bool[y.R.Length])[k] = true; grew = true; }
				var p = y.P;
				if (p.NRaw > 0 && x.Raw != null && y.Raw != null && !x.Raw.AsSpan().SequenceEqual(y.Raw))
					for (int k = 0; k < p.NRaw; k++)
						if (!(y.WRaw != null && y.WRaw[k]) && !x.Raw.AsSpan(p.RawBuf[k], p.RawSize[k]).SequenceEqual(y.Raw.AsSpan(p.RawBuf[k], p.RawSize[k]))) { (y.WRaw ??= new bool[p.NRaw])[k] = true; grew = true; }
			}
			return grew;
		}

		/// <summary>This image (the last build's result) against <paramref name="pre"/> (aligned, taken where the next build starts): marks the raw fields nothing outside the build changed in between.</summary>
		public void MarkUntouched(StateImage pre)
		{
			for (int i = 0; i < _order.Count; i++)
			{
				var y = _order[i]; var x = pre._order[i];
				if (y.IsArr || y.P.NRaw == 0 || x.Raw == null || y.Raw == null) { y.UT = null; continue; }
				var p = y.P; var ut = y.UT != null && y.UT.Length == p.NRaw ? y.UT : (y.UT = new bool[p.NRaw]);
				for (int k = 0; k < p.NRaw; k++) ut[k] = x.Raw.AsSpan(p.RawBuf[k], p.RawSize[k]).SequenceEqual(y.Raw.AsSpan(p.RawBuf[k], p.RawSize[k]));
			}
		}

		/// <summary>
		/// After a build (this, aligned with <paramref name="pre"/> and marked by <see cref="MarkUntouched"/> against the last build): an int or float field nothing else touched that the build moved by exactly one. Down by one is a
		/// cooldown (marked: a served tick counts it down itself); up by one is a counter whose threshold the build tests (its name goes into <paramref name="up"/>). Returns true when a new cooldown was found.
		/// </summary>
		public bool DetectCounters(StateImage pre, List<string> up)
		{
			bool grew = false;
			for (int i = 0; i < _order.Count; i++)
			{
				var y = _order[i]; var x = pre._order[i];
				if (y.UT == null || y.IsArr || x.Raw == null || y.Raw == null) continue;
				var p = y.P;
				for (int k = 0; k < p.NRaw && k < y.UT.Length; k++)
				{
					Type t = p.RawTypes[k];
					if (t != typeof(int) && t != typeof(float)) continue;
					var last = y.LastStep ??= new sbyte[p.NRaw];
					double a, b;
					if (t == typeof(int)) { a = BitConverter.ToInt32(x.Raw, p.RawBuf[k]); b = BitConverter.ToInt32(y.Raw, p.RawBuf[k]); }
					else { a = BitConverter.ToSingle(x.Raw, p.RawBuf[k]); b = BitConverter.ToSingle(y.Raw, p.RawBuf[k]); }
					double step = b - a;
					if (y.WDown != null && y.WDown[k])
					{
						// a known cooldown: is the build counting it down now (a floored one sits at zero), and does it go below zero
						(y.DownActive ??= new bool[p.NRaw])[k] = step == -1;
						if (step == -1 && b < 0) (y.DownNeg ??= new bool[p.NRaw])[k] = true;
					}
					if (!y.UT[k]) { last[k] = 0; continue; } // something outside the build set it (a hook raising a cooldown): a new run starts
					if (step == -1)
					{
						// down by one on two builds in a row, and never raised by the build: a cooldown (a stat that went down by one once, or flips between two values, is not)
						if (last[k] == -1 && !(y.NoDown != null && y.NoDown[k]) && !(y.WDown != null && y.WDown[k])) { (y.WDown ??= new bool[p.NRaw])[k] = true; (y.DownActive ??= new bool[p.NRaw])[k] = true; if (b < 0) (y.DownNeg ??= new bool[p.NRaw])[k] = true; grew = true; }
						last[k] = -1;
					}
					else if (step > 0)
					{
						(y.NoDown ??= new bool[p.NRaw])[k] = true;
						if (y.WDown != null && y.WDown[k]) { y.WDown[k] = false; grew = true; }
						if (step == 1 && last[k] == 1 && up != null) { y.Names(); up.Add(y.LeafRaw[k]); } // up by one on two builds in a row: a counter the build climbs
						last[k] = (sbyte)(step == 1 ? 1 : 0);
					}
					else last[k] = 0;
				}
			}
			return grew;
		}

		/// <summary>A cooldown a served tick counts down: the field, and the node that says whether the build is counting it now and whether it goes below zero.</summary>
		public readonly struct Cool { public readonly object O; public readonly int Off, K; public readonly bool F; public readonly ImgNode N; public Cool(object o, int off, bool f, ImgNode n, int k) { O = o; Off = off; F = f; N = n; K = k; } }

		/// <summary>The cooldowns found (<see cref="DetectCounters"/>), for a served tick to count down.</summary>
		public List<Cool> Cooldowns()
		{
			var l = new List<Cool>();
			foreach (var n in _order) if (n.WDown != null) for (int k = 0; k < n.WDown.Length; k++) if (n.WDown[k]) l.Add(new Cool(n.Obj, n.P.RawOff[k], n.P.RawTypes[k] == typeof(float), n, k));
			return l;
		}

		/// <summary>Counts each cooldown the last build was counting down by one, as the build does: to zero, or past it when the build was seen to go below zero.</summary>
		public static void StepCooldowns(List<Cool> l)
		{
			foreach (var c in l)
			{
				var n = c.N; if (n.DownActive == null || !n.DownActive[c.K]) continue;
				bool neg = n.DownNeg != null && n.DownNeg[c.K];
				ref byte b = ref Unsafe.Add(ref TypePlan.RawRef(c.O), c.Off);
				if (c.F) { ref float v = ref Unsafe.As<byte, float>(ref b); if (neg || v > 0f) v -= 1f; }
				else { ref int v = ref Unsafe.As<byte, int>(ref b); if (neg || v > 0) v--; }
			}
		}

		/// <summary>Number of leaves marked written (lab).</summary>
		public int WrittenLeaves
		{
			get
			{
				int c = 0;
				foreach (var n in _order)
				{
					if (n.WAll) { c++; continue; }
					if (n.WV != null) foreach (bool b in n.WV) if (b) c++;
					if (n.WR != null) foreach (bool b in n.WR) if (b) c++;
					if (n.WRaw != null) foreach (bool b in n.WRaw) if (b) c++;
				}
				return c;
			}
		}

		/// <summary>The written leaves by name (lab).</summary>
		public IEnumerable<string> WrittenNames()
		{
			foreach (var n in _order)
			{
				if (n.WAll) { yield return n.Path + "[]"; continue; }
				if (n.WV == null && n.WR == null && n.WRaw == null) continue;
				n.Names();
				if (n.WV != null) for (int k = 0; k < n.WV.Length; k++) if (n.WV[k]) yield return n.LeafV[k];
				if (n.WR != null) for (int k = 0; k < n.WR.Length; k++) if (n.WR[k]) yield return n.LeafR[k];
				if (n.WRaw != null) for (int k = 0; k < n.WRaw.Length; k++) if (n.WRaw[k]) yield return n.LeafRaw[k];
			}
		}

		/// <summary>
		/// Marks every node so that <see cref="RestoreInto"/> writes back only the learned written leaves, minus those <paramref name="keep"/> names (state other code moves every tick: the body's motion, life, buffs, the tail's meters).
		/// Returns the nodes to write and fills <paramref name="live"/> with each node's object.
		/// </summary>
		public int[] WriteMask(Func<string, bool> keep, out object[] live, Func<string, bool> always = null)
		{
			var list = new List<int>(); live = new object[_order.Count];
			var kv = new List<int>(); var kr = new List<int>();
			for (int i = 0; i < _order.Count; i++)
			{
				var n = _order[i]; live[i] = n.Obj;
				n.XV = n.XR = n.KV = n.KR = n.CV = n.CR = null; n.XAll = false; n.KAll = true; n.CmpSeg = n.CRaw = null; n.ResSeg = null;
				if (n.IsArr)
				{
					if ((n.WAll || (always != null && always(n.Path))) && !keep(n.Path)) { n.KAll = false; list.Add(i); }
					continue;
				}
				if (n.WV == null && n.WR == null && n.WRaw == null && (always == null || n.Depth > 0)) continue;
				n.Names(); kv.Clear(); kr.Clear(); bool any = false;
				for (int k = 0; k < n.V.Length; k++) { bool w = ((n.WV != null && n.WV[k]) || (always != null && always(n.LeafV[k]))) && !keep(n.LeafV[k]); if (w) any = true; else kv.Add(k); }
				for (int k = 0; k < n.R.Length; k++) { bool w = n.Kid[k] == null && n.WR != null && n.WR[k] && !keep(n.LeafR[k]); if (w) any = true; else kr.Add(k); }
				var p = n.P;
				if (p.NRaw > 0)
				{
					var cut = new bool[p.NRaw];
					for (int k = 0; k < p.NRaw; k++) { bool w = ((n.WRaw != null && n.WRaw[k]) || (always != null && always(n.LeafRaw[k]))) && !(n.WDown != null && n.WDown[k]) && !keep(n.LeafRaw[k]); if (w) any = true; else cut[k] = true; }
					n.ResSeg = Segments(p, cut, true);
				}
				if (!any) continue;
				n.KAll = false;
				if (kv.Count > 0) n.KV = kv.ToArray();
				if (kr.Count > 0) n.KR = kr.ToArray();
				list.Add(i);
			}
			return list.ToArray();
		}

		/// <summary>The object each node was captured from (after a <see cref="Recapture"/>, the live ones).</summary>
		public void LiveObjects(object[] into) { for (int i = 0; i < _order.Count && i < into.Length; i++) into[i] = _order[i].Obj; }

		public int NodeCount => _order.Count;
		public IEnumerable<(string path, long leaves)> NodeList() { foreach (var n in _order) yield return (n.Path, n.IsArr && n.Um != null ? n.Len : n.V.Length + n.R.Length + (n.IsArr ? 0 : n.P.NRaw)); }
		public long LeafCount { get { long c = 0; foreach (var n in _order) c += n.IsArr && n.Um != null ? 1 : n.V.Length + n.R.Length + (n.IsArr ? 0 : n.P.NRaw); return c; } }
	}
}
