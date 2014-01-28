using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using itcsharp;

namespace CollisionTest
{
    public class StateAttribute : Attribute { }

    public class State : ICloneable
    {
        static Random m_seed = new Random();
        public UInt64 StateId { get; protected set; }
        public TimeStamp LatestUpdate;

        public State(TimeStamp stamp)
        {
            StateId = (ulong)m_seed.Next();
            LatestUpdate = stamp;
        }

        public object Clone()
        {
            return new State(LatestUpdate);
        }

        public virtual bool Merge(State rhs)
        {
            return false;   //< can't merge if subclass didn't provide a Merge function
        }
    }
}
