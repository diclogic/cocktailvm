using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Concurrent;
using MathLib;

using itc = itcsharp;
using System.Diagnostics;


///
/// Wrapper for itc library
/// 

namespace CollisionTest
{
    public interface IHierarchicalId : IComparable
    {

    }

    public interface IHierarchicalIdFactory
    {
        IHierarchicalId CreateFromRoot();
        IHierarchicalId CreateSiblingsOf(IHierarchicalId elderBrother);
    }

    public static class HierarchicalIdService
    {
        private static IHierarchicalIdFactory m_factory;
        static HierarchicalIdService()
        {
            m_factory = new ITCIdentityFactory();
        }
        public static IHierarchicalIdFactory GetFactory()
        {
            return m_factory;
        }
    }

    internal class ITCIdentity : IHierarchicalId
    {
        readonly itc.Identity m_impl;
        readonly ITCIdentity m_causalParent;	//< since we used sibling tree, the tree structure no longer follows the causality exactly,
												//  so we need to save a reference to the causal parent

		private ITCIdentity(itc.Identity impl)
		{
			m_impl = impl;
			m_causalParent = this;
		}

        internal ITCIdentity(itc.Identity impl, ITCIdentity causalParent)
        {
            m_impl = impl;
            m_causalParent = causalParent;
        }

		public static ITCIdentity CreateRootID()
		{
			return new ITCIdentity(itc.Identity.ONE);
		}

        internal IEnumerable<ITCIdentity> CreateChild(uint hintNum, ITCIdentity causalParent)
        {
            var num = BitOps.HighestBitPos(BitOps.RoundUp(hintNum - 1));

            var remains = new List<itc.Identity>();
            remains.Add(m_impl);
            for (var ii = 0; ii < num; ++ii)
            {
                var midresult = new List<itc.Identity>();
                foreach (var id in remains)
                {
                    var pair = m_impl.Fork();
                    midresult.Add(pair.First);
                    midresult.Add(pair.Second);
                }
                remains = midresult;
            }

            return remains.Select((input) => new ITCIdentity(input, causalParent));
        }

        internal itc.Identity GetImpl() { return m_impl; }
        internal ITCIdentity GetCausalParent() { return m_causalParent; }

        public int CompareTo(object obj)
        {
            var rhs = obj as ITCIdentity;
            if (rhs == null)
                return 1;   //< any identity is greater than null

            // Equals() is way faster than bit compare
            if (m_impl.Equals(rhs.m_impl))
                return 0;

            var lhsDepth = m_impl.GetMaxDepth();
            var rhsDepth = rhs.m_impl.GetMaxDepth();
            if (lhsDepth != rhsDepth)
                return lhsDepth > rhsDepth ? 1 : -1;

            var lhsBits = m_impl.ToBitArray(lhsDepth);
            var rhsBits = rhs.m_impl.ToBitArray(lhsDepth);
            for (var ii = 0; ii < lhsDepth; ++ii )
            {
                if (lhsBits[ii] != rhsBits[ii])
                    return (lhsBits[ii] ? 1 : 0) > (rhsBits[ii] ? 1 : 0) ? 1 : -1;
            }
            return 0;
        }

        public override bool Equals(object rhs)
        {
            return m_impl.Equals((rhs as ITCIdentity).m_impl);
        }
    }

    /// <summary>
    /// The factory is here because i like to keep itc a library that is simple and generic.
    /// </summary>
    class ITCIdentityFactory : IHierarchicalIdFactory
    {
        const uint INITIAL_SIZE = 32;
        const uint BATCH_SIZE = 16;
        const uint SEED_RATIO = 4;  // TODO: use it to replace the one seed approach

        struct FreelistEntry
        {
            public object entryLock;
            public List<ITCIdentity> seeds;
            public List<ITCIdentity> remains;
        }
        ITCIdentity m_root;
        ConcurrentDictionary<itc.Identity, FreelistEntry > m_freelist;

        public ITCIdentityFactory()
        {
			m_root = ITCIdentity.CreateRootID();
            m_freelist = new ConcurrentDictionary<itc.Identity, FreelistEntry>();

            var entry = CausallyExpand(m_root, INITIAL_SIZE);
            entry.entryLock = new object();
            if (!m_freelist.TryAdd(m_root.GetImpl(), entry))
                throw new ApplicationException("ITCIdentityFactory being dereferenced while constructing");
        }

        public IHierarchicalId CreateFromRoot()
        {
            return CreateSiblingsOf(m_root);
        }

        public IHierarchicalId CreateSiblingsOf(IHierarchicalId elderBrother)
        {
            return CreateSiblingsOf(elderBrother, BATCH_SIZE);
        }

        public IHierarchicalId CreateSiblingsOf(IHierarchicalId elderBrother, uint batchSize)
        {
            ITCIdentity itcEB = elderBrother as ITCIdentity;
            if (itcEB == null)
                return null;

			var itcParent = itcEB.GetCausalParent();

            var entry = m_freelist.GetOrAdd(itcParent.GetImpl(), (_) =>
            {
                var newEntry = CausallyExpand(itcParent, batchSize);
                newEntry.entryLock = new object();
                return newEntry;
            });

            lock (entry.entryLock)
            {
                if (entry.remains.Count <= 0)
                // plant a seed
                {
                    var seeds = entry.seeds;
                    var seed = seeds[seeds.Count - 1];
                    seeds.RemoveAt(seeds.Count - 1);
                    var expandRet = ExpandSeed(seed, batchSize);

                    entry.remains.AddRange(expandRet.remains);
                    entry.seeds.AddRange(expandRet.seeds);
                }

                // consume a free node
                Debug.Assert(entry.remains.Count > 0);
                var retval = entry.remains[entry.remains.Count - 1];
                entry.remains.RemoveAt(entry.remains.Count - 1);
                return retval;
            }
        }

        // expand in the sibling tree style
        private FreelistEntry Expand(ITCIdentity parent, ITCIdentity causalParent, uint batchSize)
        {
                var all = parent.CreateChild(batchSize, causalParent).ToList();
                // preserve some nodes as seeds
                var seeds = all.GetRange(0,1);
                all.RemoveRange(0,1);
                return new FreelistEntry() {seeds = seeds, remains = all};
        }
        private FreelistEntry ExpandSeed(ITCIdentity seed, uint batchSize)
        {
            return Expand(seed, seed.GetCausalParent(), batchSize);
        }
        private FreelistEntry CausallyExpand(ITCIdentity parent, uint batchSize)
        {
            return Expand(parent, parent, batchSize);
        }
    }
}
