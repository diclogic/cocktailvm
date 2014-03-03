using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail;
using HTS;

namespace DOA
{

	public class PseudoSyncMgr
	{
		private Dictionary<IHierarchicalId, Spacetime> m_spaceTimes = new Dictionary<IHierarchicalId, Spacetime>();
		private object m_lock = new object();

		public static PseudoSyncMgr Instance = new PseudoSyncMgr();

		private PseudoSyncMgr() { }

		public void RegisterSpaceTime(Spacetime st)
		{
			lock (m_lock)
			{
				m_spaceTimes.Add(st.ID, st);
			}
		}

		public SpacetimeSnapshot? GetSpacetime(IHierarchicalId id)
		{
			lock (m_lock)
			{
				Spacetime st;
				if (m_spaceTimes.TryGetValue(id, out st))
					return st.Snapshot();

				return null;
			}
		}

	}
}
