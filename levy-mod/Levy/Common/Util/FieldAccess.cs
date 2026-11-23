using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Levy.Common.Util
{
	/// <summary>
	/// Compiled reads and writes of a field found by reflection (another mod's private field): the same answer as <see cref="FieldInfo.GetValue"/> / <see cref="FieldInfo.SetValue(object, object)"/>, without the reflection
	/// call and the boxing of every value. For fields read per levy per tick (perf pass, 11-22). A null field gives a null delegate; the instance is ignored for a static field.
	/// </summary>
	internal static class FieldAccess
	{
		internal static Func<object, T> Getter<T>(FieldInfo f)
		{
			if (f == null) return null;
			var o = Expression.Parameter(typeof(object), "o");
			Expression field = f.IsStatic ? Expression.Field(null, f) : Expression.Field(Expression.Convert(o, f.DeclaringType), f);
			if (field.Type != typeof(T)) field = Expression.Convert(field, typeof(T));
			return Expression.Lambda<Func<object, T>>(field, o).Compile();
		}

		/// <summary>Null for a read-only field (SetValue would throw for it too).</summary>
		internal static Action<object, T> Setter<T>(FieldInfo f)
		{
			if (f == null || f.IsInitOnly || f.IsLiteral) return null;
			var o = Expression.Parameter(typeof(object), "o"); var v = Expression.Parameter(typeof(T), "v");
			Expression field = f.IsStatic ? Expression.Field(null, f) : Expression.Field(Expression.Convert(o, f.DeclaringType), f);
			Expression val = f.FieldType == typeof(T) ? v : Expression.Convert(v, f.FieldType);
			return Expression.Lambda<Action<object, T>>(Expression.Assign(field, val), o, v).Compile();
		}

		/// <summary>A compiled getter of an instance property.</summary>
		internal static Func<object, T> PropGetter<T>(PropertyInfo p)
		{
			if (p == null || p.GetMethod == null) return null;
			var o = Expression.Parameter(typeof(object), "o");
			Expression prop = Expression.Property(p.GetMethod.IsStatic ? null : Expression.Convert(o, p.DeclaringType), p);
			if (prop.Type != typeof(T)) prop = Expression.Convert(prop, typeof(T));
			return Expression.Lambda<Func<object, T>>(prop, o).Compile();
		}
	}
}
