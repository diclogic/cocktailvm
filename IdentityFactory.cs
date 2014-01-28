using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Concurrent;

namespace CollisionTest
{
    interface IHierarchicalId : IComparable
    {

    }

    interface IHierarchicalIdFactory
    {
        IHierarchicalId CreateFrom(IHierarchicalId parent);
    }

    class ITCIdentity : IHierarchicalId
    {
        itcsharp.Identity m_impl;
        internal ITCIdentity(itcsharp.Identity impl)
        {
            m_impl = impl;
        }

        public List<ITCIdentity> CreateChild(uint hintNum)
        {
            m_impl;
        }

        internal itcsharp.Identity GetImpl() { return m_impl; }
    }
    /// <summary>
    /// The factory is here because i like to keep ITCSharp a library that is simple and generic.
    /// </summary>
    class ITCIdentityFactory : IHierarchicalIdFactory
    {
        const uint BATCH_SIZE = 16;
        ITCIdentity m_root;
        ConcurrentDictionary<itcsharp.Identity, List<ITCIdentity> > m_freelist;

        public ITCIdentityFactory()
        {
            m_root = new ITCIdentity(itcsharp.Identity.ONE);
        }

        public IHierarchicalId CreateFrom(IHierarchicalId parent)
        {
            ITCIdentity itcParent = parent as ITCIdentity;
            if (itcParent == null)
                return null;

            var children = m_freelist.GetOrAdd(itcParent.GetImpl(), (_) => itcParent.CreateChild(BATCH_SIZE));
            lock (children)
            {
                children.
                return children.FirstOrDefault();
            }
        }
    }
}
