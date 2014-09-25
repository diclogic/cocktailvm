using System;
using System.IO;
using System.Linq;
using Cocktail.HTS;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Reflection;

namespace Cocktail
{

	public struct TStateId : IComparable, IComparable<TStateId>, IEquatable<TStateId>
	{
		private ulong m_val;

		public TStateId(Random seed)
		{
			ulong highBits = ((ulong)seed.Next()) << 32;
			m_val = (ulong)(uint)seed.Next() | highBits;
		}

		internal TStateId(ulong val)
		{
			m_val = val;
		}

		public static TStateId DebugCreate(ulong val)
		{
			TStateId retval;
			retval.m_val = val;
			return retval;
		}

		public int CompareTo(object rhs)
		{
			if (rhs.GetType().Equals(this))
				return CompareTo((TStateId)rhs);
			return 1;	// null is small than any value
		}

		public int CompareTo(TStateId rhs)
		{
			return m_val.CompareTo(rhs.m_val);
		}

		public bool Equals(TStateId rhs)
		{
			return m_val.Equals(rhs.m_val);
		}

		public bool IsNull()
		{
			return m_val == 0;
		}

		public override string ToString()
		{
			return m_val.ToString();
		}

		public ulong ToUlong()
		{
			return m_val;
		}
	}
}
