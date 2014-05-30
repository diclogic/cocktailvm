﻿using System;
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

		internal PrePullResult PrePullRequest(IHId idPuller, IHId idRequester, IHEvent evtOriginal, IEnumerable<TStateId> affectedStates)
		{
			return m_spaceTimes[idPuller].PrePullRequest(idRequester, evtOriginal, affectedStates);
		}

		public bool SyncPullRequest(IHId idPuller, IHId idRequester, IHEvent foreignExpectedEvent, ILookup<TStateId, StatePatch> affectedStates)
		{
			return m_spaceTimes[idPuller].SyncPullRequest(idRequester, foreignExpectedEvent, affectedStates);
		}

		public StateSnapshot AggregateDistributedDelta(IEnumerable<IHId> STIDs, TStateId state, IHEvent evtUpTo)
		{
			return DoAggregateDistributedDelta(m_spaceTimes.Where(kv => STIDs.Contains(kv.Key)).Select(kv => kv.Value), state, evtUpTo);
		}

		public StateSnapshot AggregateDistributedDelta(TStateId state, IHEvent evtUpTo)
		{
			return DoAggregateDistributedDelta(m_spaceTimes.Values, state, evtUpTo);
		}
		private StateSnapshot DoAggregateDistributedDelta(IEnumerable<Spacetime> spacetimes, TStateId stateId, IHEvent evtUpTo)
		{
			var firstST = spacetimes.First();
			StateSnapshot seed = firstST.ExportStateSnapshot(stateId);

			// cleanup all non-CommutativeDelta in seed
			foreach (var f in seed.Fields)
			{
				if (0 == (f.Attrib.PatchKind & FieldPatchCompatibility.CommutativeDelta))
					f.Value = null;
			}

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
