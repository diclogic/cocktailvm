using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using itc = itcsharp;

namespace HTS
{
    public interface IHEvent
    {
        bool LtEq(IHEvent rhs);
    }

    public class ITCEvent : IHEvent
    {
		private itc.Event m_impl;

		public static ITCEvent CreateZero() { return new ITCEvent(new itc.Event()); }

		public ITCEvent(itc.Event impl) { m_impl = impl; }

		public itc.Event GetImpl() { return m_impl; }

		public bool LtEq(IHEvent rhs)
		{
			return itc.Event.Leq(m_impl, (rhs as ITCEvent).m_impl);
		}
    }
}
