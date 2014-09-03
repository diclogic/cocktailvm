using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cocktail.HTS;
using System.IO;
using DOA;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;
using System.Collections.Concurrent;
using Core.Aux.System;

using SnapshotPair = itcsharp.Pair<Cocktail.State,Cocktail.StateSnapshot>;
using System.Diagnostics;

namespace Cocktail
{
	public struct ExecutionFraction 
	{
		public IHTimestamp timeStamp;
		public KeyValuePair<TStateId,Stream>[] affectedStates;
	}

	internal class SpaceTimeMeta
	{

	}

	public struct SpacetimeSnapshot
	{
		public IHTimestamp Timestamp;
		public IEnumerable<State> States;
		public ILookup<TStateId, StatePatch> Redos;

		public IEnumerable<KeyValuePair<TStateId, IHEvent>> LatestEvents
		{
			get
			{
				foreach (var state in Redos)
					yield return new KeyValuePair<TStateId, IHEvent>( state.Key,
						state.Aggregate(HTSFactory.CreateZeroEvent(),
										(acc, patch) => acc.KnownBy(patch.ToEvent) ? patch.ToEvent : acc)
						);
			}
		}
	}

	public static class SpacetimeUtils
	{
		public static ILookup<TStateId,IHEvent> ReduceLatestStateEvents(IEnumerable<KeyValuePair<TStateId, IHEvent>> input)
		{
			return input.ToLookup(kv => kv.Key, kv=>kv.Value);
		}
	}

	public enum PrePullRequestResult
	{
		Locked,						//< already locked by someone else
		Succeeded,
		SucceededButRequireSync,	//< the PullRequester don't have the latest revision of the requestee
		NoLocking,					//< no need to lock
	}

    /// <summary>
    /// the SpaceTime represents the development of objects
    /// it is a thread apartment that can include one to many objects
    /// 1) An activated object must be in an spaceTime.
    /// 2) 2 SpaceTimes can merge into 1
    /// 3) SpaceTime cannot cross machine boundary (we need something else to do distributed transaction)
    /// </summary>
	public class Spacetime
	{
		private class RedoEntry
		{
			public IHEvent Rev;
			public IEnumerable<IHId> AffectedFSTs;
			public IEnumerable<KeyValuePair<TStateId, IHEvent>> IntegratedStates;
			public IDictionary<TStateId, StatePatch> LocalChanges;	// Local changes means things we changed locally, not just for local states

			// TODO: define what it does
			RedoEntry Filter(IEnumerable<IHId> STs, IDictionary<TStateId,IHId> dict)
			{
				var retval = new RedoEntry();
				retval.Rev = this.Rev;
				retval.AffectedFSTs = this.AffectedFSTs.Where(val => STs.Contains(val));
				retval.IntegratedStates = this.IntegratedStates.ToArray();
				retval.LocalChanges = this.LocalChanges.Where(kv => STs.Contains(dict[kv.Key])).ToDictionary(kv => kv.Key, kv => kv.Value);
				return retval;
			}
		}

		private static IStdLib m_stdlib = InvocationBuilder.Build<IStdLib>("Cocktail");

		// ========== components ===============
		protected SpacetimeStorage m_storageComponent;

        private IHIdFactory m_idFactory;
        private IHTimestamp m_currentTime;
		private List<RedoEntry> m_RedoList = new List<RedoEntry>();
		private object m_executionLock = new object();
		private SortedList<IHEvent, ExecutionFraction> m_incomingExecutions = new SortedList<IHEvent, ExecutionFraction>();
		private IHEvent m_executingEvent;
		private IHId m_pullRequestedBy = null;
		protected VMState m_vm;

		public IHId ID { get { return m_currentTime.ID; } }
		public IHEvent LatestEvent { get { return m_currentTime.Event; } }
		public TStateId StorageSID { get { return m_storageComponent.StateId; } }


        public Spacetime(IHTimestamp stamp, IHIdFactory idFactory)
			:this(stamp, idFactory, Enumerable.Empty<State>())
        {
        }

		public Spacetime(IHId id, IHEvent event_, IHIdFactory idFactory)
			:this(HTSFactory.Make(id, event_), idFactory, Enumerable.Empty<State>())
		{
		}

		public Spacetime(IHId id, IHEvent event_, IHIdFactory idFactory, IEnumerable<State> initialStates)
			:this(HTSFactory.Make(id, event_), idFactory, initialStates)
		{
		}

		public Spacetime(IHTimestamp stamp, IHIdFactory idFactory, IEnumerable<State> initialStates)
		{
			m_currentTime = stamp;
			m_idFactory = idFactory;
			m_vm = new VMState(stamp);

			// every ST must have the minimal VM since the very beginning, VM's life time has no beginning nor end
			ExternalSTEntry vmST;
			vmST.IsListeningTo = true;
			vmST.SpacetimeId = m_vm.SpacetimeID;
			vmST.LatestUpateTime = HTSFactory.CreateZeroEvent();
			vmST.LocalStates = Enumerable.Repeat<State>(m_vm, 1).ToDictionary(s => s.StateId);

			m_storageComponent = new SpacetimeStorage(stamp, initialStates, Enumerable.Repeat(vmST, 1));
		}

		public State CreateState(Func<Spacetime, IHTimestamp, State> constructor)
        {
            var evtOriginal = BeginChronon();
			var evtFinal = evtOriginal.Advance(ID);
            var newState = constructor(this, m_currentTime);
			m_storageComponent.AddNativeState(newState);

			var redo = new RedoEntry();
			redo.LocalChanges = new Dictionary<TStateId, StatePatch>();

			// do create and destroy only for non-commutatives
			if (0 == (newState.GetPatchFlag() & PatchFlag.CommutativeBit))
			{
				var patch = newState.Snapshot(evtFinal).GenerateCreatePatch(m_currentTime.Event);
				redo.LocalChanges.Add(newState.StateId, patch);
			}

			CommitChronon(evtOriginal, evtFinal, Enumerable.Repeat(newState, 1), redo);
            return newState;
        }

		// I don't think we still need it since we have Snapshot()
		//public void Serialize(Stream ostream)
		//{
		//    var writer = new BinaryWriter(ostream, Encoding.UTF8);
		//    var formatter = new BinaryFormatter();

		//    lock (m_executionLock)
		//    {
		//        var curTime = m_currentTime;
		//        writer.Write(curTime.ID.ToString());
		//        writer.Write(curTime.Event.ToString());
		//        writer.Write(m_states.Count);
		//        foreach (var state in m_states.Values)
		//        {
		//            formatter.Serialize(ostream, state);
		//        }
		//    }
		//}

		public SpacetimeSnapshot Snapshot(IHEvent evtAck)
		{
			lock (m_executionLock)
			{
				return new SpacetimeSnapshot() { 
					Timestamp = m_currentTime,
					States = m_storageComponent.GetAllStates(),
					Redos = m_RedoList.Aggregate(new List<KeyValuePair<TStateId, StatePatch>>(),
												(accu,entry) =>
													{
														if (!entry.Rev.KnownBy(evtAck))
															foreach (var sp in entry.LocalChanges)
																accu.Add(sp);
														return accu;
													}
												).ToLookup(kv => kv.Key, kv => kv.Value)
				};
			}
		}

		/// <summary>
		/// Designed to be used by foreign spacetime
		/// </summary>
		public StateSnapshot ExportStateSnapshot(TStateId stateId, IHEvent evtUpTo)
		{
			throw new NotImplementedException();
		}

		public StateSnapshot ExportStateSnapshot(TStateId stateId)
		{
			var state = m_storageComponent.GetState(stateId);
			if (state == null)
				return StateSnapshot.CreateNull(stateId);

			return state.Snapshot();	
		}

		///// <summary>
		///// Create another ST in parallel to this one.
		///// won't expect to merge it with this ST in the future.
		///// </summary>
		///// <returns></returns>
		//public SpaceTime Fork()
		//{
		//    var newIdForNewST = m_idFactory.CreateSiblingsOf(m_id);
		//    var retval = new SpaceTime(newIdForNewST, m_currentTime.Event, m_idFactory);
		//    //TODO: init thread?
		//    return retval;
		//}

		//public void Join(SpaceTime spaceTime)
		//{
		//    m_currentTime.
		//}

		/// <summary>
		/// Split the spacetime into many pieces so that each state has it's own spacetime
		/// </summary>
		public IEnumerable<Spacetime> SplitForEach()
		{
			IEnumerable<State> m_nativeStates = m_storageComponent.GetNativeStates();
			int count = m_nativeStates.Count();
			if (count <= 1)
				return new Spacetime[] { this };
			var ids = m_idFactory.CreateChildren(ID, count);
			var evtOriginal = BeginChronon();
			var evtFinal = evtOriginal.Advance(ID);

			var redo = new RedoEntry();

			var iter = m_nativeStates.GetEnumerator();
			var retval = ids.Select((id) =>
				{
					iter.MoveNext();
					redo.LocalChanges.Add(iter.Current.StateId, StatePatchUtils.GenerateDestroyPatch(evtFinal, iter.Current.LatestUpdate));
					return new Spacetime(id, evtFinal, m_idFactory, new State[] { iter.Current });
				});
			CommitChronon(evtOriginal, evtFinal, m_nativeStates, redo);
			return retval;
		}

		public static Spacetime Merge(IEnumerable<Spacetime> spaces)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns>true if this event is compatible to all events that happened or happening </returns>>
		public bool TryEnqueue(ExecutionFraction fraction)
		{
			// lock a bit earlier to avoid ongoing 
			lock (m_executionLock)
			{
				// if compatible, sort it into the list
				if (fraction.timeStamp.Event.KnownBy(m_executingEvent)
					&& fraction.timeStamp.Event.KnownBy(m_currentTime.Event))
				{
					m_incomingExecutions.Add( fraction.timeStamp.Event, fraction );
					return true;
				}
			}
			return false;
		}

		private void ExecuteIncomingQueue()
		{
			// TODO: optimize it with dual buffer approach
			lock (m_executionLock)
			{
				foreach (var exec in m_incomingExecutions.Values)
				{
					foreach (var stateIn in exec.affectedStates)
					{
						//State state;
						//if (m_states.TryGetValue(stateIn.Key, out state))
						//    state.Merge(stateIn.Value);
					}
				}
			}
		}

		public bool Execute(string funcName, IEnumerable<KeyValuePair<string, StateRef>> stateParams, params object[] constArgs)
		{
			if (this == null)
				throw new RuntimeException("Can't find SpaceTime to execute in. Did you forgot use `WithIn` syntax");

			lock (m_executionLock)
			{
				return ExecuteArgs(funcName, stateParams, constArgs);
			}
		}

		protected bool ExecuteArgs(string funcName, IEnumerable<KeyValuePair<string,StateRef>> stateParams, IEnumerable<object> constArgs)
		{
			IHEvent evtOriginal, evtFinal;
			while ((evtOriginal = BeginChronon()) == null) ;
			evtFinal = evtOriginal;
			var redo = new RedoEntry();

			IEnumerable<TStateId> nativeIds, foreignIds;
			SplitStateParams(out nativeIds, out foreignIds, stateParams.Select((sp) => sp.Value.StateId));

			var foreignSTIds = Enumerable.Empty<IHId>();
			var pulledSTs = Enumerable.Empty<IGrouping<IHId, TStateId>>();
			if (foreignIds.Count() > 0)
			{
				var foreignStateIdPairs = FetchForeignStateIdPair(foreignIds);
				foreignSTIds = foreignStateIdPairs.Select(kv => kv.Key).Distinct();
				var foreignSTs = FetchForeignSpacetime(foreignSTIds, evtOriginal);

				if (!PullForeignSTForExecution(out pulledSTs, foreignStateIdPairs, foreignSTs, evtOriginal, evtFinal))
					AbortChronon();
			}

			//---------- collect old snapshot for redo ----------

			var states = m_storageComponent.GetAllStates(stateParams.Select(sp => sp.Value.StateId));
			var oldSnapshots = states.Select(ns => ns.Snapshot()).ToList();

			//-------- execute ---------

			evtFinal = evtFinal.Advance(ID);

			m_vm.Call(m_storageComponent, funcName, stateParams, constArgs.ToArray());

			//----- make redo -------

			redo.AffectedFSTs = foreignSTIds.ToList();
			redo.LocalChanges = new Dictionary<TStateId, StatePatch>();

			foreach (var spair in
						from s1 in states
						join s2 in oldSnapshots on s1.StateId equals s2.ID
						select new SnapshotPair(s1, s2))
			{
				var patch = spair.First.Serialize(spair.Second, evtFinal);
				redo.LocalChanges.Add(spair.First.StateId, patch);
			}

			// ---------- commit to local ST ------------
			CommitChronon(evtOriginal, evtFinal, states, redo);

			//--------- get approve from foreign STs ---------
			// Send "pull request" (ask them to pull us) to spacetimes whose non-commutative sates we changed
			{
				bool bApproved = true;
				var requestedSTIDs = new List<IHId>();
				foreach (var fst in pulledSTs)
				{
					bApproved &= PseudoSyncMgr.Instance.PullRequest(fst.Key, this.ID, evtFinal, redo.LocalChanges.Where(kv => fst.Contains(kv.Key)).ToLookup(kv => kv.Key, kv => kv.Value));
					requestedSTIDs.Add(fst.Key);
				}

				if (!bApproved)
				{
					//foreach (var stId in pulledSTIds)
					//    PseudoSyncMgr.Instance.RollbackPullRequest(stId);

					throw new ApplicationException("Because we prepull-request so this can't fail");
				}
			}

			// TODO: Push native changes to spacetimes that are highly depend on us

			return true;
		}

		private static IEnumerable<KeyValuePair<IHId, TStateId>> FetchForeignStateIdPair(IEnumerable<TStateId> foreignIds)
		{
			foreach (var sid in foreignIds)
			{
				var hid = NamingSvcClient.Instance.GetObjectSpaceTimeID(sid.ToString());
				yield return new KeyValuePair<IHId, TStateId>(hid, sid);
			}
		}

		/// <summary>
		/// Fetch foreign Spacetimes up to a certain event
		/// </summary>
		private static IDictionary<IHId, SpacetimeSnapshot> FetchForeignSpacetime(IEnumerable<IHId> foreignSTIDs, IHEvent evtUpto)
		{
			return foreignSTIDs.Select(id => PseudoSyncMgr.Instance.GetSpacetime(id, evtUpto))
										.Where(val => val.HasValue)
										.ToDictionary(val => val.Value.Timestamp.ID, val => val.Value);
		}

		private static IDictionary<TStateId, IHEvent> ExtractNewStamps(IDictionary<IHId, SpacetimeSnapshot> spacetimes)
		{
			var stateStamps = new Dictionary<TStateId, IHEvent>();
			foreach (var st in spacetimes)
			{
				foreach (var state in st.Value.LatestEvents)
				{
					if (stateStamps.ContainsKey(state.Key) && !stateStamps[state.Key].KnownBy(state.Value))
						continue;

					stateStamps[state.Key] = state.Value;
				}
			}
			return stateStamps;
		}

		private bool PullForeignSTForExecution(out IEnumerable<IGrouping<IHId, TStateId>> pulledSTs
												, IEnumerable<KeyValuePair<IHId, TStateId>> foreignStateIds
												, IDictionary<IHId, SpacetimeSnapshot> foreignSTs
												, IHEvent evtOriginal, IHEvent evtFinal)
		{
			// excludes commutative states
			pulledSTs = foreignStateIds.Where(sp =>
				{
					var state = foreignSTs[sp.Key].States.FirstOrDefault(s => s.StateId.Equals(sp.Value));
					if (state == null)
						throw new ApplicationException("No such state in foreign Spacetime and creating state into external ST is not allowed");
					return (0 == (state.GetPatchFlag() & PatchFlag.CommutativeBit));
				}).GroupBy(kv => kv.Key, kv => kv.Value);

			// we assume both read and write operations on STs that involved
			// TODO: support RO STs

			// And because we know who is going to be written/PullRequested, we can give heads-up to them beforehand
			foreach (var st in pulledSTs)
			{
				var ret = PseudoSyncMgr.Instance.PrePullRequest(st.Key, this.ID, evtOriginal, st);
				if (ret >= PrePullRequestResult.Succeeded)
					continue;

				throw new ApplicationException("Failed to lock foreign ST");
			}

			// Fetch construction info for new states
			// TODO: currently this is psudo-implementation
			foreach (var sp in foreignStateIds)
				if (!m_storageComponent.HasState(sp.Value))
				{
					var st = foreignSTs[sp.Key];
					var stateType = st.States.First(s => s.StateId.Equals(sp.Value)).GetType();

					m_storageComponent.GetOrCreate(sp.Value, () => (State)Activator.CreateInstance(stateType, sp.Value, HTSFactory.Make(sp.Key, ITCEvent.CreateZero()) ));
				}


			// do the actual work
			IHTimestamp failingST = null;
			foreach (var st in foreignSTs)
			{
				if (!DoPullFrom(st.Value.Timestamp, st.Value.Redos, ref evtFinal))
				{
					failingST = st.Value.Timestamp;
					break;
				}
			}

			return (failingST == null);
		}

		// TODO: the operation should not change the current spacetime's data because it's not committed yet
		private bool DoPullFrom(IHTimestamp foreignStamp, ILookup<TStateId, StatePatch> redos, ref IHEvent expectingEvent)
		{
			var expectingEventCopy = expectingEvent;
			// all states are put into m_states
			var newStates = new Dictionary<TStateId, State>();
			foreach (var fst in redos)
			{
				var fstateId = fst.Key;
				var patches = fst.OrderBy(patch => patch.FromEvent, HTSFactory.GetEventComparer(foreignStamp.ID))
								.SkipWhile(patch => patch.ToEvent.KnownBy(expectingEventCopy)); // we use the expecting event because one event can be sync'ed from 2 sources

				var firstPatch = patches.FirstOrDefault();
				if (firstPatch == null)
					continue;

				// Treat first patch differently because we use it to locate the state obj (or create one)
				var firstPatchCtx = new StatePatchingCtx(firstPatch);

				var lst = m_storageComponent.GetOrCreate(fstateId, () =>
				{
					throw new ApplicationException("The caller of this method (DoPullFrom) should have guaranteed the creation of the state");
					//State ret;
					//if (!StatePatchUtils.TryCreateFromPatch(foreignStamp.ID, fstateId, firstPatchCtx, out ret))
					//    throw new ApplicationException(string.Format("State {0} not found in current ST {1} and the first patch is not constructive patch: {2}", fstateId, ID, patches.First().Flag));
					//return ret;
				});

				if (!lst.Patch(firstPatchCtx))
					return false;

				foreach (var patch in patches.Skip(1))
					if (!lst.Patch(new StatePatchingCtx(patch)))
						return false;

				newStates.Add(fstateId, lst);
			}

			//m_cachedExternalST[foreignStamp.ID] = entry;
			m_storageComponent.AddSpacetime(foreignStamp, newStates.Values);

			var localTime = HTSFactory.Make(ID, expectingEvent);
			expectingEvent = localTime.Join(foreignStamp).Event;
			return true;
		}

		private void SplitStateParams(out IEnumerable<TStateId> natives, out IEnumerable<TStateId> externals, IEnumerable<TStateId> stateIds)
		{
			natives = stateIds.Intersect(m_storageComponent.GetNativeStates().Select(s => s.StateId));
			externals = stateIds.Except(natives);
		}

		// SHOULDN'T BE using it this lazy way
		//public IHierarchicalEvent Advance()
		//{
		//    var event_ = BeginAdvance();
		//    CommitAdvance(event_);
		//    return event_;
		//}

		private IHEvent BeginChronon()
		{
			var retval = m_currentTime;
			var oldVal = Interlocked.CompareExchange(ref m_executingEvent, retval.Event, null);
			if (oldVal != null)
				return null;

			Log.Info(Log.CHRONON, "[{0}] Begin {1}", retval.ID.ToString(), retval.Event.ToString());

			// pull don't necessarily have event increment on local component as long as the final result event is identifiable (by the component of external ST)
			return retval.Event;
		}

		private void CommitChronon(IHEvent evtOriginal, IHEvent evtFinal, IEnumerable<State> states, RedoEntry redo)
        {
			// If current time is not compatible to committing event
            if (!m_currentTime.Event.KnownBy(evtFinal))
				throw new ApplicationException("The event is out dated, recalculation needed");

			// This check is not mature, event value could grow further if sync/merge involved
			//var oldVal = Interlocked.CompareExchange(ref m_executingEvent, null, event_);
			//if (oldVal != event_)
			//    throw new ApplicationException("Race condition on m_executingEvent");

			//atomic{
			Interlocked.Exchange(ref m_executingEvent, null);
			m_currentTime = new ITCTimestamp(m_currentTime.ID as ITCIdentity, evtFinal as ITCEvent);
			foreach (var s in states)
				s.OnCommitting(evtFinal);
			redo.Rev = evtFinal;
			m_RedoList.Add(redo);
			//}

			Log.Info(Log.CHRONON, "[{0}] Commit {1} => {2}", m_currentTime.ID.ToString(), evtOriginal.ToString(), evtFinal.ToString());
        }

		private void AbortChronon()
		{
			Log.Info(Log.CHRONON, "[{0}] Abort {1}", m_currentTime.ID, m_executingEvent.ToString());
			Interlocked.Exchange(ref m_executingEvent, null);
		}

		#region ISerializable Members

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			throw new NotImplementedException();
		}

		#endregion

		internal PrePullRequestResult PrePullRequest(IHId idRequester, IHEvent evtOriginal, IEnumerable<TStateId> affectedStates)
		{
			if (affectedStates.FirstOrDefault().Equals(default(TStateId)))
				return PrePullRequestResult.NoLocking;

			// Currently we lock whole ST
			// TODO: check states' sync/patch method
			if (m_pullRequestedBy != null)
				return PrePullRequestResult.Locked;

			lock (m_executionLock)
			{
				m_pullRequestedBy = idRequester;
			}

			if (!m_currentTime.Event.KnownBy(evtOriginal))
				return PrePullRequestResult.SucceededButRequireSync;

			return PrePullRequestResult.Succeeded;
		}

		internal bool PullRequest(IHId idRequester, IHEvent foreignExpectedEvent, ILookup<TStateId,StatePatch> redos)
		{
			if (m_pullRequestedBy != null && m_pullRequestedBy != idRequester)
				return false;

			var evtOriginal = BeginChronon();
			var evtFinal = evtOriginal;

			var redo = new RedoEntry();
			redo.LocalChanges = new Dictionary<TStateId, StatePatch>();

			bool bOK = DoPullFrom(HTSFactory.Make(idRequester, foreignExpectedEvent), redos, ref evtFinal);
			if (!bOK)
				return false;

			m_pullRequestedBy = null;
			CommitChronon(evtOriginal, evtFinal, Enumerable.Empty<State>(), redo);
			return true;
		}

		internal void PullFromVmSt(SpacetimeSnapshot vmST, TStateId vmStateId)
		{
			var cmp = HTSFactory.GetEventComparer(vmST.Timestamp.ID);

			var evtOri = BeginChronon();
			var evtFinal = evtOri;

			var newRedos = vmST.Redos[vmStateId].OrderBy(patch=> patch.ToEvent, HTSFactory.GetEventComparer(vmST.Timestamp.ID))
								.SkipWhile(patch => cmp.Compare(m_currentTime.Event, patch.FromEvent) > 0)
								.ToLookup(_ => vmStateId);
			var localRedo = new RedoEntry();
			localRedo.LocalChanges = new Dictionary<TStateId, StatePatch>();

			if (!DoPullFrom(vmST.Timestamp, newRedos, ref evtFinal))
				throw new ApplicationException("Failed to pull from VM Spacetime");

			var vmState = m_storageComponent.GetState(vmStateId);
			if (vmState == null)
				throw new ApplicationException("VM Spacetime don't contain the VMState");

			m_vm = (VMState)vmState;
			CommitChronon(evtOri, evtFinal, Enumerable.Empty<State>(), localRedo);
		}

		public void PullAllFrom(SpacetimeSnapshot foreignST)
		{
			var cmp = HTSFactory.GetEventComparer(foreignST.Timestamp.ID);

			var evtOri = BeginChronon();
			var evtFinal = evtOri;

			var flatPairs = foreignST.Redos.SelectMany(kgroup => kgroup.Select(elem => new KeyValuePair<TStateId, StatePatch>(kgroup.Key, elem)));
			var newRedos = flatPairs.Where(kv => cmp.Compare(m_currentTime.Event, kv.Value.FromEvent) <= 0)
								.ToLookup(p => p.Key, p => p.Value);
			var localRedo = new RedoEntry();
			localRedo.LocalChanges = new Dictionary<TStateId, StatePatch>();
			// TODO: external changes to our local states should be seen as local changes in the pull event?

			if (!DoPullFrom(foreignST.Timestamp, newRedos, ref evtFinal))
				throw new ApplicationException(string.Format("Failed to pull from Spacetime {0}, to local {1}", foreignST.Timestamp.ToString(), m_currentTime));

			CommitChronon(evtOri, evtFinal, Enumerable.Empty<State>(), localRedo);
		}

		#region Migration
		public void Immigrate(TStateId immigrantId, IHId departuringST)
		{
			if (!m_storageComponent.HasState(immigrantId))
				throw new RuntimeException("Failed to immigrate `{0}` to `{1}`: not in board yet", immigrantId, departuringST);

			var sidOrNot = PseudoSyncMgr.Instance.GetSpacetimeStorageSID(departuringST);
			if (!sidOrNot.HasValue)
				throw new ApplicationException(string.Format("Unable to find storage for ST `{0}`", departuringST));

			TStateId storageSID = sidOrNot.Value;
			using (new WithIn(this))
			{
				m_stdlib.Migrate(new _LocalStateRef<SpacetimeStorage>(m_storageComponent)
					, new ScopedStateRef(storageSID, typeof(SpacetimeStorage).ToString())
					, immigrantId);
			}
		}
		#endregion
	}
}
