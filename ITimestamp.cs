using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using itc = itcsharp;

namespace CollisionTest
{
    public interface IHierarchicalTimestamp
    {
        IHierarchicalId ID { get; }
        IHierarchicalEvent Event { get; }
        IHierarchicalTimestamp FireEvent();
    }

    internal class ITCTimestamp : IHierarchicalTimestamp
    {
        private itc.TimeStamp m_impl;
        private ITCIdentity m_IdCache;
        private ITCEvent m_EventCache;
        public ITCTimestamp(ITCIdentity id, ITCEvent event_)
        {
            m_IdCache = id;
            m_EventCache = event_;
            m_impl = new itc.TimeStamp(id.GetImpl(), event_.GetImpl());
        }

        public IHierarchicalTimestamp FireEvent()
        {
            var newStamp = m_impl.FireEvent();
            return new ITCTimestamp(m_IdCache, new ITCEvent(newStamp.Event));
        }

        public IHierarchicalId ID { get { return m_IdCache; } }
        public IHierarchicalEvent Event { get { return m_EventCache; } }
    }
}
