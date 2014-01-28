using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace itcsharp
{
    public struct Pair<T> : IEquatable<Pair<T>>
    {
        public T First;
        public T Second;

        public Pair(T first, T second)
        {
            First = first;
            Second = second;
        }

        public bool Equals(Pair<T> rhs)
        {
            return First.Equals(rhs.First) && Second.Equals(rhs.Second);
        }

        public override bool Equals(object rhs)
        {
            if (!GetType().IsInstanceOfType(rhs))
                return false;
            return Equals((Pair<T>)rhs);
        }

        public override int GetHashCode()
        {
            return First.GetHashCode() ^ Second.GetHashCode();
        }
    }

    public struct Pair<T1, T2>
    {
        public T1 First;
        public T2 Second;

        public Pair(T1 first, T2 second)
        {
            First = first;
            Second = second;
        }
    }
}
