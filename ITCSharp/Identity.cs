

using System.Runtime.Serialization;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace itcsharp
{
    [Serializable]
    public sealed class Identity : ICloneable, IEquatable<Identity>
    {
        private static ObjectIDGenerator g_idPool = new ObjectIDGenerator();
        private static ConcurrentDictionary<Pair<long>, WeakReference> g_pool = new ConcurrentDictionary<Pair<long>, WeakReference>();
        public static Identity ZERO = new Identity(0);
        public static Identity ONE = new Identity(1);

        public readonly Identity Left;
        public readonly Identity Right;
        private readonly int bit = -1;

        private Identity(Identity left, Identity right)
        {
            this.Left = left;
            this.Right = right;
            CheckConsistancy();
        }

        private Identity(int value)
        {
            this.bit = value;
        }

        public static Identity Create(Identity left, Identity right)
        {
            var newBit = DeltaNormalize(left, right);
            // if successfully normalized, that means left == right == newOne
            // that means we can just return one of them
            if (newBit >= 0)
                return left;

            bool _;
            Pair<long> idPair;
            // Cannot trust the out values of the two GetId() because they are not atomic
            idPair.First = g_idPool.GetId(left, out _);
            idPair.Second = g_idPool.GetId(right, out _);
            var candicate = new Identity(left, right);
            var ret = g_pool.GetOrAdd(idPair, new WeakReference(candicate));
            return ret.Target as Identity;
        }

        public bool Equals(Identity rhs)
        {
            if (rhs == null)
                return false;

            return this == rhs; //< because we use flyweight design pattern
        }

        public override string ToString()
        {
            if (IsSimplex())
                return bit.ToString();
            return "(" + Left + ", " + Right + ")";
        }

        public object Clone()
        {
            return CloneT();
        }

        public Identity CloneT()
        {
            // Identity is designed to be used as flyweight, there is only one instance per an identical value
            // Saves memory
            return this;
        }

        public bool IsZero()
        {
            return IsSimplex() && bit == 0;
        }

        public bool IsOne()
        {
            return IsSimplex() && bit == 1;
        }

        public bool IsSimplex()
        {
            return Left == null;
        }

        public bool CheckNormalization()
        {
            if (this.IsSimplex())
                return true;
            if (Left.IsSimplex() && Right.IsSimplex())
                return Left.bit != Right.bit;

            return Left.CheckNormalization() && Right.CheckNormalization();
        }

        /// <summary>
        /// Only do one step normalize, presume given left, right are already normalized
        /// </summary>
        private static int DeltaNormalize(Identity left, Identity right)
        {
            if (left.IsSimplex() && right.IsSimplex() && left.bit == right.bit)
                return left.bit;
            return -1;
        }

        public Pair<Identity> Fork()
        {
            if (IsZero())
                return new Pair<Identity>(ZERO, ZERO);
            if (IsOne())
                return new Pair<Identity>(Identity.Create(ONE, ZERO), Identity.Create(ZERO, ONE));
            if (Left != null && Left.IsZero())
            {
                var rightSplit = Right.Fork();
                return new Pair<Identity>(Identity.Create(ZERO, rightSplit.First), Identity.Create(ZERO, rightSplit.Second));
            }
            if (Right != null && Right.IsZero())
            {
                var leftSplit = Left.Fork();
                return new Pair<Identity>(Identity.Create(leftSplit.First, ZERO), Identity.Create(leftSplit.Second, ZERO));
            }
            return new Pair<Identity>(Identity.Create(Left, ZERO), Identity.Create(ZERO, Right));
        }

        public static Identity Join(Identity id1, Identity id2)
        {
            if (id1 == null || id2 == null)
                throw new ArgumentNullException();

            if (id1.IsZero())
                return id2;
            if (id2.IsZero())
                return id1;
            return Identity.Create(Join(id1.Left, id2.Left), Join(id1.Right, id2.Right));
        }
        
        // Utils
        private void CheckConsistancy()
        {
            if ((Left == null) ^ (Right == null))
                throw new ChildNodeInconsistantException();
        }
    }
}