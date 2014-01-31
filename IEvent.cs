using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using itc = itcsharp;

namespace CollisionTest
{
    public interface IHierarchicalEvent
    {
        bool LtEq(IHierarchicalEvent rhs);
    }

    public class ITCEvent : IHierarchicalEvent
    {
        private itc.Event m_impl;
            public ITCEvent(itc.Event impl) { m_impl = impl; }

            public itc.Event GetImpl() { return m_impl; }

            public bool LtEq(IHierarchicalEvent rhs)
            {
                return itc.Event.Leq(m_impl, (rhs as ITCEvent).m_impl);
            }
    }
}
