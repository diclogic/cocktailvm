using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail;
using HTS;

namespace DOA
{
	public class SyncManager
	{
		private Dictionary<IHierarchicalId, Spacetime> m_spaceTimes;
		private object m_lock = new object();

		public static SyncManager Instance = new SyncManager();
		public static KeyValuePair<IHierarchicalTimestamp, IEnumerable<State>> NullSpacetime
			= new KeyValuePair<IHierarchicalTimestamp, IEnumerable<State>>(null, Enumerable.Empty<State>());

		private SyncManager() { }

		public void RegisterSpaceTime(Spacetime st)
		{
			lock (m_lock)
			{
				m_spaceTimes.Add(st.ID, st);
			}
		}

		public KeyValuePair<IHierarchicalTimestamp, IEnumerable<State>> GetSpacetime(IHierarchicalId id)
		{
			lock (m_lock)
			{
				Spacetime st;
				if (m_spaceTimes.TryGetValue(id, out st))
					return st.Snapshot();

				return NullSpacetime;
			}
		}

	}
}
