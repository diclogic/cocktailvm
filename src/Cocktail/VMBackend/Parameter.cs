using System;
using System.Collections.Generic;

namespace Cocktail.Interp
{
	public class StateParam : IEquatable<StateParam>
	{
		public string name;
		public string type;
		public int index;

		public bool Equals(StateParam rhs)
		{
			return name == rhs.name && type == rhs.type;
		}
	}

	public class StateParamInst : StateParam
	{
		public StateRef arg;

		public KeyValuePair<object, int> ConvertCocktailToCSharp(IScope scope)
		{
			var spi = this;
			var stateRefType = spi.arg.GetType();
			if (typeof(DirectStateRef).IsAssignableFrom(stateRefType))
			{
				var ret = (spi.arg as DirectStateRef).GetObject(null);
				return new KeyValuePair<object, int>(ret, spi.index);
			}
			else if (typeof(ScopedStateRef).IsAssignableFrom(stateRefType))
			{
				var ret = (spi.arg as ScopedStateRef).GetObject(scope);
				return new KeyValuePair<object, int>(ret, spi.index);
			}

			throw new ArgumentOutOfRangeException("Unknown StateRef");
		}
	}

	public class StateParamComparer : IEqualityComparer<StateParam>
	{
		public bool Equals(StateParam x, StateParam y)
		{
			return x.name == y.name && x.type == y.type;
		}
		public int GetHashCode(StateParam obj)
		{
			return obj.name.GetHashCode() ^ obj.type.GetHashCode();
		}
	}

	public class ConstParamComparer : IEqualityComparer<Type>
	{
		public bool Equals(Type x, Type y)
		{
			if (x.IsAssignableFrom(y))
				return true;
			if (x.IsPrimitive && y.IsPrimitive)
				return true;
			return false;
		}

		public int GetHashCode(Type obj)
		{
			return obj.GetHashCode();
		}

	}

}