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

		public KeyValuePair<IHierarchicalTimestamp, IEnumerable<State>> GetSpaceTime(IHierarchicalId id)
		{
			Spacetime st;
			if (m_spaceTimes.TryGetValue(id, out st))
			{
				st.
			}

		}

	}
}
