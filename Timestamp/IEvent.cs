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
		IHEvent Advance(IHId id);
    }

	internal class ITCEventComparer : IComparer<IHEvent>
	{
		private ITCIdentity m_mask;
		internal ITCEventComparer(ITCIdentity mask)
		{
			m_mask = mask;
		}

		public int Compare(IHEvent x, IHEvent y)
		{
			//TODO: use mask
			if (!x.LtEq(y))
				return 1;
			if (!y.LtEq(x))
				return -1;
			return 0;
		}
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

		public IHEvent Advance(IHId id)
		{
			return HTSFactory.Make(id, this).FireEvent().Event;
		}

		public override string ToString()
		{
			return GetDebugString();
		}
		public string GetDebugString()
		{
			var sb = new StringBuilder();
			RecursiveDebugString(sb, m_impl, 0, 0, m_impl.MaxDepth());
			return sb.ToString();
		}

		public static void RecursiveDebugString(StringBuilder sb, itc.Event _event, int baseVal, int depth, int maxDepth)
		{
			if (_event.IsSimplex())
			{
				for (int ii = 0; ii < (1 << (maxDepth - depth)); ++ii )
					sb.AppendFormat("{0}|", baseVal + _event.N);
			}
			else
			{
				RecursiveDebugString(sb, _event.Left, baseVal + _event.N, depth+1, maxDepth);
				RecursiveDebugString(sb, _event.Right, baseVal + _event.N, depth+1, maxDepth);
			}
		}

    }
}
