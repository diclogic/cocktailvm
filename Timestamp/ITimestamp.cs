using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using itc = itcsharp;

namespace HTS
{
    public interface IHierarchicalTimestamp
    {
        IHierarchicalId ID { get; }
        IHierarchicalEvent Event { get; }
        IHierarchicalTimestamp FireEvent();
		IHierarchicalTimestamp Join(IHierarchicalTimestamp rhs);
    }

	public static class HierarchicalTimestampFactory
	{
		public static IHierarchicalTimestamp NullValue = new ITCTimestamp(ITCIdentity.Null, ITCEvent.CreateZero());
		public static IHierarchicalTimestamp Make(IHierarchicalId id, IHierarchicalEvent event_)
		{
			return new ITCTimestamp(id as ITCIdentity, event_ as ITCEvent);
		}
	}

    internal class ITCTimestamp : IHierarchicalTimestamp
    {
        private itc.TimeStamp m_impl;
        private ITCIdentity m_IdCache;
        private ITCEvent m_EventCache;

        public IHierarchicalId ID { get { return m_IdCache; } }
        public IHierarchicalEvent Event { get { return m_EventCache; } }

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

		public IHierarchicalTimestamp Join(IHierarchicalTimestamp rhs)
		{
			var itcRight = rhs as ITCTimestamp;
			if (m_IdCache.GetCausalParent() == itcRight.m_IdCache.GetCausalParent())
			{
				var newStamp = itc.TimeStamp.Join(m_impl, itcRight.m_impl);
				return new ITCTimestamp(new ITCIdentity(newStamp.ID, m_IdCache.GetCausalParent()), new ITCEvent(newStamp.Event));
			}
			throw new ApplicationException("Not supported");
		}
    }
}
