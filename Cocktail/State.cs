using System;
using System.IO;
using HTS;

namespace Cocktail
{


	public class StateAttribute : Attribute { }

	public struct TStateId : IComparable, IComparable<TStateId>, IEquatable<TStateId>
	{
		private ulong m_val;

		public TStateId(Random seed)
		{
			ulong highBits = ((ulong)seed.Next()) << 32;
			m_val = (ulong)(uint)seed.Next() | highBits;
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
	}

	public class State //: ICloneable
	{
		static Random m_seed = new Random();

		private Spacetime m_spaceTime;					//< the space-time it belongs to
		public TStateId StateId { get; protected set; }	//< to identify a state
		public IHierarchicalTimestamp LatestUpdate;


		public State(Spacetime spaceTime, IHierarchicalTimestamp stamp)
		{
			StateId = new TStateId(m_seed);
			m_spaceTime = spaceTime;
			LatestUpdate = stamp;
		}

		//public object Clone()
		//{
		//    return new State(LatestUpdate);
		//}

		public bool IsCompatible(IHierarchicalTimestamp stamp)
		{
			return LatestUpdate.Event.LtEq(stamp.Event);
		}

		public virtual bool Merge(Stream newState)
		{
			return false;   //< can't merge if subclass didn't provide a Merge function
		}
	}
}
