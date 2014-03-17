using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using itc = itcsharp;

namespace HTS
{
    public interface IHTimestamp
    {
        IHId ID { get; }
        IHEvent Event { get; }
        IHTimestamp FireEvent();
		IHTimestamp Join(IHTimestamp rhs);
    }

	public static class HTSFactory
	{
		public static IHTimestamp NullValue = new ITCTimestamp(ITCIdentity.Null, ITCEvent.CreateZero());
		public static IHTimestamp Make(IHId id, IHEvent event_)
		{
			return new ITCTimestamp(id as ITCIdentity, event_ as ITCEvent);
		}

		public static IHEvent CreateZeroEvent()
		{
			return ITCEvent.CreateZero();
		}

		public static IComparer<IHEvent> GetEventComparer(IHId mask)
		{
			if (!typeof(ITCIdentity).IsAssignableFrom(mask.GetType()))
				throw new ArgumentException("provided hid is not implemented in the same implementation family");

			return new ITCEventComparer((ITCIdentity)mask);
		}
	}

    internal class ITCTimestamp : IHTimestamp
    {
        private itc.TimeStamp m_impl;
        private ITCIdentity m_IdCache;
        private ITCEvent m_EventCache;

        public IHId ID { get { return m_IdCache; } }
        public IHEvent Event { get { return m_EventCache; } }

        public ITCTimestamp(ITCIdentity id, ITCEvent event_)
        {
            m_IdCache = id;
            m_EventCache = event_;
            m_impl = new itc.TimeStamp(id.GetImpl(), event_.GetImpl());
        }

        public IHTimestamp FireEvent()
        {
            var newStamp = m_impl.FireEvent();
            return new ITCTimestamp(m_IdCache, new ITCEvent(newStamp.Event));
        }

		public IHTimestamp Join(IHTimestamp rhs)
		{
			var itcRight = rhs as ITCTimestamp;
			if (m_IdCache.GetCausalParent() == itcRight.m_IdCache.GetCausalParent())
			{
				var newStamp = itc.TimeStamp.Join(m_impl, itcRight.m_impl);
				return new ITCTimestamp(new ITCIdentity(newStamp.ID, m_IdCache.GetCausalParent()), new ITCEvent(newStamp.Event));
			}
			throw new ApplicationException("Not supported");
		}

		public override string ToString()
		{
			var maxDepth = Math.Max(m_impl.ID.GetMaxDepth(), m_impl.Event.MaxDepth());

			var sb = new StringBuilder();
			ITCIdentity.RecursiveDebugString(sb, m_impl.ID, 0, maxDepth);
			var idStr = sb.ToString();

			sb.Clear();
			ITCEvent.RecursiveDebugString(sb, m_impl.Event, 0, 0, maxDepth);
			var eventStr = sb.ToString();

			return string.Format("[({0})|({1})]", idStr, eventStr);
		}
    }
}
