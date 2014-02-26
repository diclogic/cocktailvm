using System;
using System.IO;
using HTS;
using System.Collections.Generic;
using System.Runtime.Serialization;

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

	public class StatePatch
	{
		public IHierarchicalEvent FromRev;
		public IHierarchicalEvent ToRev;
		public Stream delta;
	}

	public abstract class State //: ICloneable
	{
		static Random m_seed = new Random();

		private Spacetime m_spaceTime;					//< the space-time it belongs to
		private StatePatch m_pendingPatch;

		public TStateId StateId { get; protected set; }	//< to identify a state
		public IHierarchicalTimestamp LatestUpdate { get; private set; }


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

		public virtual bool Merge(State rhs)
		{
			return false;
		}

		public virtual bool Patch(IHierarchicalEvent fromRev, IHierarchicalEvent toRev, Stream delta) { return true; }
		protected virtual void AddPatch(Stream delta)
		{
			if (m_pendingPatch != null)
				throw new ApplicationException("Trying to patch a state twice in one execution");

			m_pendingPatch = new StatePatch() { FromRev = LatestUpdate.Event, delta = delta };
		}
		public virtual StatePatch FinishPatch(IHierarchicalEvent toRev)
		{
			LatestUpdate = HTSFactory.Make(LatestUpdate.ID, toRev);

			if (m_pendingPatch == null)
				return null;

			m_pendingPatch.ToRev = toRev;
			var retval = m_pendingPatch;
			m_pendingPatch = null;
			return retval;
		}

		public virtual void Serialize(Stream ostream) { }
		//public abstract IEnumerable<object> Properties { get; }

	}
}
