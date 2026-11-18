using System;
using System.Collections.Generic;
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

		private static readonly Dictionary<(Type, bool), TypePlan> Cache = new();
		public static TypePlan For(Type t, bool elem = false)
		{
			lock (Cache)
			{
				if (!Cache.TryGetValue((t, elem), out var p)) Cache[(t, elem)] = p = new TypePlan(t, elem);
				return p;
			}
		}

		private struct Leaf { public Expression Acc; public Type Ty; public string Name; public bool RO; }

		private TypePlan(Type t, bool elem)
		{
			T = t; Elem = elem;
			var holder = Expression.Parameter(typeof(object), "h"); var idx = Expression.Parameter(typeof(int), "i");
			var vbuf = Expression.Parameter(typeof(long[]), "v"); var rbuf = Expression.Parameter(typeof(object[]), "r"); var off = Expression.Parameter(typeof(int), "o");
			Expression root = elem ? Expression.ArrayAccess(Expression.Convert(holder, t.MakeArrayType()), idx) : Expression.Convert(holder, t);
			var vals = new List<Leaf>(); var refs = new List<Leaf>();
			Walk(t, root, "", false, 0, vals, refs);
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
			CapV = Lambda<Action<object, int, long[], int>>(capV, holder, idx, vbuf, off);
			ResV = Lambda<Action<object, int, long[], int>>(resV, holder, idx, vbuf, off);
			CapR = Lambda<Action<object, int, object[], int>>(capR, holder, idx, rbuf, off);
			ResR = Lambda<Action<object, int, object[], int>>(resR, holder, idx, rbuf, off);
		}

		private static D Lambda<D>(List<Expression> body, params ParameterExpression[] ps)
		{
			Expression b = body.Count == 0 ? Expression.Empty() : Expression.Block(body);
			return Expression.Lambda<D>(b, ps).Compile();
		}

		private static void Walk(Type t, Expression acc, string prefix, bool ro, int depth, List<Leaf> vals, List<Leaf> refs)
		{
			for (Type b = t; b != null && b != typeof(object) && b != typeof(ValueType); b = b.BaseType)
			{
				foreach (FieldInfo f in b.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
				{
					Type ft = f.FieldType;
					if (ft.IsPointer || ft.IsByRef || ft.IsByRefLike) continue;
					var fa = Expression.Field(acc, f);
					bool fro = ro || f.IsInitOnly || f.IsLiteral;
					string nm = prefix + f.Name;
					if (ft.IsPrimitive || ft.IsEnum) vals.Add(new Leaf { Acc = fa, Ty = ft, Name = nm, RO = fro });
					else if (ft.IsValueType) { if (depth < 8) Walk(ft, fa, nm + ".", fro, depth + 1, vals, refs); }
					else refs.Add(new Leaf { Acc = fa, Ty = ft, Name = nm, RO = fro });
				}
				if (t.IsValueType) break;
			}
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
		// arrays: Um = raw-byte copy of an unmanaged array; otherwise P is the element plan (struct elements, Elem = true) or null (reference elements: R holds them)
		public ArrOps Um; public Array Copy; public int Len; public bool IsArr;
		public int Stamp;
		/// <summary>Full leaf names, built once per path (lab diffs).</summary>
		public string[] LeafV, LeafR; public string LeafPath;
		public void Names()
		{
			if (LeafPath == Path && LeafV != null && LeafV.Length == (V?.Length ?? 0) && LeafR.Length == (R?.Length ?? 0)) return;
			LeafPath = Path; LeafV = new string[V?.Length ?? 0]; LeafR = new string[R?.Length ?? 0];
			for (int k = 0; k < LeafV.Length; k++) LeafV[k] = Path + "." + StateImage.ValName(this, k);
			for (int k = 0; k < LeafR.Length; k++) LeafR[k] = Path + "." + StateImage.RefName(this, k);
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
				Fill(n, o, names[i], 0);
			}
		}

		private void Fill(ImgNode n, object o, string path, int depth)
		{
			_seen[o] = n; _order.Add(n); Nodes++;
			n.Obj = o; n.Path = path; n.Depth = depth; n.Stamp = _stamp;
			Type t = o.GetType();
			if (t.IsArray)
			{
				n.IsArr = true; var a = (Array)o; n.Len = a.Length; Type et = t.GetElementType();
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
				if (n.P == null || n.P.T != t) { n.P = TypePlan.For(t); n.V = null; n.R = null; n.Kid = null; }
				EnsureBufs(n, n.P.NV, n.P.NR);
				n.P.CapV(o, 0, n.V, 0); n.P.CapR(o, 0, n.R, 0);
			}
			// follow owned references
			int nr = n.R.Length;
			if (n.Kid == null || n.Kid.Length != nr) n.Kid = new ImgNode[nr];
			for (int k = 0; k < nr; k++)
			{
				object c = n.R[k];
				if (c == null || depth >= MaxDepth || !ImgRules.Follow(o, c.GetType())) { n.Kid[k] = null; continue; }
				if (_seen.TryGetValue(c, out var dup)) { n.Kid[k] = dup; continue; }
				if (Nodes >= MaxNodes) { Truncated = true; n.Kid[k] = null; continue; }
				ImgNode kn = n.Kid[k];
				if (kn == null || !ReferenceEquals(kn.Obj, c) || kn.Stamp == _stamp) kn = new ImgNode();
				n.Kid[k] = kn;
				Fill(kn, c, path + "." + RefName(n, k), depth + 1);
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
				else { n.P.ResV(o, 0, n.V, 0); n.P.ResR(o, 0, n.R, 0); }
			}
		}

		/// <summary>Every leaf as (path, exact value hash), in capture order. Arrays without references are one leaf (their bytes); followed objects add their own leaves.</summary>
		public void Flatten(Action<string, long> emit)
		{
			foreach (var n in _order)
			{
				if (n.IsArr && n.Um != null) { var h = new HashCode(); n.Um.Hash(n.Copy, ref h); n.LeafPath ??= n.Path + "[]"; if (!n.LeafPath.StartsWith(n.Path) || n.LeafPath.Length != n.Path.Length + 2) n.LeafPath = n.Path + "[]"; emit(n.LeafPath, h.ToHashCode()); continue; }
				n.Names();
				for (int k = 0; k < n.V.Length; k++) emit(n.LeafV[k], n.V[k]);
				for (int k = 0; k < n.R.Length; k++) emit(n.LeafR[k], n.Kid != null && n.Kid[k] != null ? TypeToken(n.R[k]) : RefHash(n.R[k]));
			}
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

		public int NodeCount => _order.Count;
		public IEnumerable<(string path, long leaves)> NodeList() { foreach (var n in _order) yield return (n.Path, n.IsArr && n.Um != null ? n.Len : n.V.Length + n.R.Length); }
		public long LeafCount { get { long c = 0; foreach (var n in _order) c += n.IsArr && n.Um != null ? 1 : n.V.Length + n.R.Length; return c; } }
	}
}
