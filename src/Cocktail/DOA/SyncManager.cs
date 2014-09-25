using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail;
using Cocktail.HTS;

namespace DOA
{


	public class PseudoSyncMgr : ISyncService, ILocatingService
	{
		private Dictionary<IHId, Spacetime> m_spaceTimes = new Dictionary<IHId, Spacetime>();
		private object m_lock = new object();

		private Spacetime m_vmST;
		private TStateId m_vmStateId;

		internal PseudoSyncMgr() { }

		public void Reset()
		{
			lock (m_lock)
			{
				m_vmST = null;
				m_vmStateId = new TStateId();
				m_spaceTimes.Clear();
			}
		}

		public void RegisterSpaceTime(Spacetime st)
		{
			lock (m_lock)
			{
				m_spaceTimes.Add(st.ID, st);
			}
		}

		public SpacetimeSnapshot? GetSpacetime(IHId id, IHEvent evtAck)
		{
			lock (m_lock)
			{
				Spacetime st;
				if (m_spaceTimes.TryGetValue(id, out st))
					return st.Snapshot(evtAck);

				return null;
			}
		}

		public TStateId? GetSpacetimeStorageSID(IHId stHid)
		{
			Spacetime st;
			if (!m_spaceTimes.TryGetValue(stHid, out st))
				return null;

			return st.StorageSID;
		}

		public PrePullRequestResult PrePullRequest(IHId idPuller, IHId idRequester, IHEvent evtOriginal, IEnumerable<TStateId> affectedStates)
		{
			return m_spaceTimes[idPuller].PrePullRequest(idRequester, evtOriginal, affectedStates);
		}

		public bool PullRequest(IHId idPuller, IHId idRequester, IHEvent foreignExpectedEvent, ILookup<TStateId, StatePatch> affectedStates)
		{
			return m_spaceTimes[idPuller].PullRequest(idRequester, foreignExpectedEvent, affectedStates);
		}

		public StateSnapshot AggregateDistributedDelta(IEnumerable<IHId> STIDs, TStateId state)
		{
			return DoAggregateDistributedDelta(m_spaceTimes.Where(kv => STIDs.Contains(kv.Key)).Select(kv => kv.Value), state);
		}

		public StateSnapshot AggregateDistributedDelta(TStateId state)
		{
			return DoAggregateDistributedDelta(m_spaceTimes.Values, state);
		}

		private StateSnapshot DoAggregateDistributedDelta(IEnumerable<Spacetime> spacetimes, TStateId stateId)
		{
			// find first ST that contains the state
			StateSnapshot seed = StateSnapshot.CreateNull(stateId);
			int skipCount = 0;
			foreach (var st in spacetimes)
			{
				seed = st.ExportStateSnapshot(stateId);
				++skipCount;
				if (!string.IsNullOrEmpty(seed.TypeName))
					break;
			}

			// cleanup all non-CommutativeDelta in seed
			foreach (var f in seed.Fields)
			{
				if (0 == (f.Attrib.PatchKind & FieldPatchCompatibility.CommutativeDelta))
					f.Value = null;
			}

			return DoAggregateDistributedDelta(spacetimes.Skip(skipCount), stateId, seed);
		}

		private StateSnapshot DoAggregateDistributedDelta(IEnumerable<Spacetime> spacetimes, TStateId stateId, StateSnapshot seed)
		{
			foreach (var spacetime in spacetimes.Skip(1))
			{
				var state = spacetime.ExportStateSnapshot(stateId);
				seed.Aggregate(state);
			}

			return seed;
		}

		public void Initialize(VMSpacetime vmST)
		{
			m_vmST = vmST;
			m_vmStateId = vmST.VMStateId;
		}

		public void PullFromVmSt(IHId spacetimeId)
		{
			m_spaceTimes[spacetimeId].PullFromVmSt(m_vmST.Snapshot(HTSFactory.CreateZeroEvent()), m_vmStateId);
		}
	}
}
