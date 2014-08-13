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