

using System.Runtime.Serialization;
using System;
using System.Diagnostics;


namespace itcsharp
{

    [Serializable]
    public sealed class Event : ICloneable, IEquatable<Event>
    {

        public readonly Event Left;
        public readonly Event Right;
        public readonly int N;

        public Event()
        {
            Left = null;
            Right = null;
            N = 0;
        }

        public Event(int value)
        {
            this.N = value;
        }

        public Event(int value, Event left, Event right)
        {
            this.N = value;
            this.Left = left;
            this.Right = right;
            CheckConsistancy();
        }

        // with Normalization
        internal Event(int value, Event left, Event right, NormalizeInitFlag _)
            :this(value,left,right)
        {
            Normalize(ref N, ref Left, ref Right);
        }

        // for Clone
        private Event(Event rhs)
        {
            this.N = rhs.N;
            if (rhs.Left != null)
            {
                Left = new Event(rhs.Left);
                Right = new Event(rhs.Right);
            }
        }

        public object Clone()
        {
            return CloneT();
        }

        public Event CloneT()
        {
            return new Event(this);
        }

        public bool Equals(Event rhs)
        {
            if (rhs == null)
                return false;

            if (IsSimplex() && rhs.IsSimplex())
                return N == rhs.N;
            if (!IsSimplex() && !rhs.IsSimplex())
                return N == rhs.N && Left.Equals(rhs.Left) && Right.Equals(rhs.Right);
            return false;
        }

        public override string ToString()
        {
            if (IsSimplex())
                return N.ToString();
            return N + "+(" + Left + ", " + Right + ")";
        }

        public bool IsSimplex()
        {
            return Left == null;
        }

        //////////////////////////////////////////////////////////////////////////
        // Event Operations
        //////////////////////////////////////////////////////////////////////////

        internal static int Min(Event e)
        {
            if (e == null)
                return 0;

            if (e.IsSimplex())
                return e.N;
            else
                return e.N + Math.Min(Min(e.Left), Min(e.Right));
        }

        internal static int Max(Event e)
        {
            if (e == null)
                return 0;

            if (e.IsSimplex())
                return e.N;
            else
                return e.N + Math.Max(Max(e.Left), Max(e.Right));
        }

        private Event Lift(int m)
        {
            if (IsSimplex())
                return new Event(N + m);
            else
                return new Event(N + m, Left, Right);
        }

        private Event Sink(int m)
        {
            if (IsSimplex())
                return new Event(N - m);
            else
                return new Event(N - m, Left, Right);
        }

        public Event Normalize()
        {
            var value = N;
            var left = Left;
            var right = Right;
            if (Normalize(ref value, ref left, ref right))
                return new Event(value,left, right);
            return this;
        }

        private static bool Normalize(ref int value, ref Event left, ref Event right)
        {
            if (left == null)
                return false;

            // simply merge sub-events if they are equal
            if (left.Left == null && right.Left == null && left.N == right.N)
            {
                value += left.N;
                left = right = null;
            }
            else
            {
                var m = Math.Min(Min(left), Min(right));
                value += m;
                left = left.Sink(m);
                right = right.Sink(m);
            }
            return true;
        }

        public static bool Leq(Event e1, Event e2)
        {
            // leq(n1,(n2,l2,r2)) => n1 <= n2
            if (e1.IsSimplex())
                return e1.N <= e2.N;
            // leq((n1,l1,r1),n2) => n1 <= n2 && leq(l1^n1,n2) && leq(r1^n1,n2)
            else if (e2.IsSimplex())
                return e1.N <= e2.N &&
                        Leq(e1.Left.Lift(e1.N), e2) &&
                        Leq(e1.Right.Lift( e1.N), e2);
            // leq((n1,l1,r1),(n2,l2,r2)) => n1 <= n2 && leq(l1^n1,l2^n2) && leq(r1^n1,r2^n2)
            else
                return e1.N <= e2.N &&
                        Leq(e1.Left.Lift(e1.N), e2.Left.Lift(e2.N)) &&
                        Leq(e1.Right.Lift(e1.N), e2.Right.Lift(e2.N));
        }

        public static Event Join(Event e1, Event e2)
        {
            if (e1.IsSimplex() && e2.IsSimplex())
                return new Event(Math.Max(e1.N, e2.N));
            else if (e1.IsSimplex())
                return Join(new Event(e1.N, new Event(0), new Event(0)), e2);
            else if (e2.IsSimplex())
                return Join(e1, new Event(e2.N, new Event(0), new Event(0)));
            else
            {
                if (e1.N > e2.N)
                    return Join(e2, e1);
                else
                {
                    Event left = Join(e1.Left, e2.Left.Lift( e2.N - e1.N));
                    Event right = Join(e1.Right, e2.Right.Lift( e2.N - e1.N));
                    NormalizeInitFlag f;
                    return new Event(e1.N, left, right, f);
                }
            }
        }

        private int MaxDepth(int depth)
        {
            if (Left == null && Right == null)
                return depth;
            return Math.Max(Left.MaxDepth(depth + 1), Right.MaxDepth(depth + 1));
        }

        public int MaxDepth()
        {
            return MaxDepth(0);
        }

        // Utils
        private void CheckConsistancy()
        {
            if ((Left == null) ^ (Right == null))
                throw new ChildNodeInconsistantException();
        }

    }
}