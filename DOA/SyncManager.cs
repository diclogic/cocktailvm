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
		private Dictionary<IHId, Spacetime> m_spaceTimes = new Dictionary<IHId, Spacetime>();
		private object m_lock = new object();

		public static PseudoSyncMgr Instance = new PseudoSyncMgr();
		private Spacetime m_vmST;
		private TStateId m_vmStateId;

		private PseudoSyncMgr() { }

		public void RegisterSpaceTime(Spacetime st)
		{
			lock (m_lock)
			{
				m_spaceTimes.Add(st.ID, st);
			}
		}

		public SpacetimeSnapshot? GetSpacetime(IHId id)
		{
			lock (m_lock)
			{
				Spacetime st;
				if (m_spaceTimes.TryGetValue(id, out st))
					return st.Snapshot();

				return null;
			}
		}

		internal bool PrePullRequest(IHId idPuller, IHId idRequester, IHEvent evtOriginal, IEnumerable<TStateId> affectedStates)
		{
			return m_spaceTimes[idPuller].PrePullRequest(idRequester, evtOriginal, affectedStates);
		}

		public bool SyncPullRequest(IHId idPuller, IHId idRequester, IHEvent foreignExpectedEvent, ILookup<TStateId, StatePatch> affectedStates)
		{
			return m_spaceTimes[idPuller].SyncPullRequest(idRequester, foreignExpectedEvent, affectedStates);
		}

		public void Initialize(VMSpacetime vmST)
		{
			m_vmST = vmST;
			m_vmStateId = vmST.VMStateId;
		}

		public void PullFromVmSt(IHId spacetimeId)
		{
			m_spaceTimes[spacetimeId].PullFromVmSt(m_vmST.Snapshot(), m_vmStateId);
		}
	}
}
