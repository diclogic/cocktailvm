using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using HTS;
using System.IO;
using DOA;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;
using System.Collections.Concurrent;

using SnapshotPair = itcsharp.Pair<Cocktail.State,Cocktail.StateSnapshot>;

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

	public struct ExternalSTEntry
	{
		public IHTimestamp LatestUpateTime;
		public Dictionary<TStateId,State> States;
	}

	public struct SpacetimeSnapshot
	{
		public IHTimestamp Timestamp;
		public IEnumerable<State> States;
		public ILookup<TStateId, StatePatch> Redos;
	}

	public enum PrePullResult
	{
		Failed,
		Succeeded,
		NoLocking,
	}

    /// <summary>
    /// the SpaceTime represents the development of objects
    /// it is a thread apartment that can include one to many objects
    /// 1) An activated object must be in an spaceTime.
    /// 2) 2 SpaceTimes can merge into 1
    /// 3) SpaceTime cannot cross machine boundary (we need something else to do distributed transaction)
    /// </summary>
	public class Spacetime //: ISerializable
	{
		private class RedoEntry
		{
			public IHEvent Rev;
			public IEnumerable<IHId> AffectedFSTs;
			public IDictionary<TStateId, StatePatch> LocalChanges;

			RedoEntry Filter(IEnumerable<IHId> STs, IDictionary<TStateId,IHId> dict)
			{
				var retval = new RedoEntry();
				retval.Rev = this.Rev;
				retval.AffectedFSTs = this.AffectedFSTs.Where(val => STs.Contains(val));
				retval.LocalChanges = this.LocalChanges.Where(kv => STs.Contains(dict[kv.Key])).ToDictionary(kv => kv.Key, kv => kv.Value);
				return retval;
			}
		}

        private IHIdFactory m_idFactory;
        private IHTimestamp m_currentTime;
		protected Dictionary<TStateId, State> m_states;
		protected Dictionary<TStateId, State> m_nativeStates;
		private List<RedoEntry> m_RedoList = new List<RedoEntry>();
		private object m_executionLock = new object();
		private SortedList<IHEvent, ExecutionFraction> m_incomingExecutions = new SortedList<IHEvent, ExecutionFraction>();
		private IHEvent m_executingEvent;
		private bool m_isWaitingForPullRequest = false;
		protected VMState m_vm;
		// we use cached state to "pro-act" on an event involves external states optimistically, and let the external ST denies it.
		private Dictionary<IHId, ExternalSTEntry> m_cachedExternalST = new Dictionary<IHId, ExternalSTEntry>();

		public IHId ID { get { return m_currentTime.ID; } }
		public IHEvent LatestEvent { get { return m_currentTime.Event; } }

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
			m_states = initialStates.ToDictionary((s) => s.StateId);
			m_nativeStates = initialStates.ToDictionary((s) => s.StateId);

			// every ST must have the minimal VM since the very beginning, VM's life time has no beginning nor an end
			m_vm = new VMState(this, stamp);
			m_states.Add(m_vm.StateId, m_vm);
		}

		public State CreateState(Func<Spacetime, IHTimestamp, State> constructor)
        {
            var event_ = BeginEventAdvance();
            var newState = constructor(this, m_currentTime);
			m_states.Add(newState.StateId, newState);
			m_nativeStates.Add(newState.StateId, newState);

			var redo = new RedoEntry();
			redo.LocalChanges = new Dictionary<TStateId, StatePatch>();

			var patch = newState.GetSnapshot(event_).GenerateCreatePatch(m_currentTime.Event);
			redo.LocalChanges.Add(newState.StateId, patch);

			CommitAdvance(event_, Enumerable.Repeat(newState, 1), redo);
            return newState;
        }

		public void Serialize(Stream ostream)
		{
			var writer = new BinaryWriter(ostream, Encoding.UTF8);
			var formatter = new BinaryFormatter();

			lock (m_executionLock)
			{
				var curTime = m_currentTime;
				writer.Write(curTime.ID.ToString());
				writer.Write(curTime.Event.ToString());
				writer.Write(m_states.Count);
				foreach (var state in m_states.Values)
				{
					formatter.Serialize(ostream, state);
				}
			}
		}


		public SpacetimeSnapshot Snapshot(IHEvent evtAck)
		{
			lock (m_executionLock)
			{
				return new SpacetimeSnapshot() { 
					Timestamp = m_currentTime,
					States = m_states.Values,
					Redos = m_RedoList.Aggregate(new List<KeyValuePair<TStateId, StatePatch>>(),
												(accu,entry) =>
													{
														if (!entry.Rev.LtEq(evtAck))
															foreach (var sp in entry.LocalChanges)
																accu.Add(sp);
														return accu;
													}
												).ToLookup(kv => kv.Key, kv => kv.Value)
				};
			}
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
			int count = m_nativeStates.Count;
			if (count <= 1)
				return new Spacetime[] { this };
			var ids = m_idFactory.CreateChildren(ID, count);
			var event_ = BeginEventAdvance();

			var redo = new RedoEntry();

			var iter = m_nativeStates.Values.GetEnumerator();
			var retval = ids.Select((id) =>
				{
					iter.MoveNext();
					redo.LocalChanges.Add(iter.Current.StateId, StatePatcher.GenerateDestroyPatch(event_, iter.Current.LatestUpdate.Event));
					return new Spacetime(id, event_, m_idFactory, new State[] { iter.Current });
				});
			CommitAdvance(event_, m_nativeStates.Values, redo);
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
				if (fraction.timeStamp.Event.LtEq(m_executingEvent)
					&& fraction.timeStamp.Event.LtEq(m_currentTime.Event))
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
			lock (m_executionLock)
			{
				return ExecuteArgs(funcName, stateParams, constArgs);
			}
		}

		protected bool ExecuteArgs(string funcName, IEnumerable<KeyValuePair<string,StateRef>> stateParams, IEnumerable<object> constArgs)
		{
			IHEvent evtOriginal, evtFinal;
			while ((evtOriginal = BeginAdvance()) == null) ;
			evtFinal = evtOriginal;

			// cross ST event
			IEnumerable<TStateId> nativeIds, foreignIds;
			SplitStateParams(stateParams.Select((sp) => sp.Value.StateId), out nativeIds, out foreignIds);

			var foreignStates = new List<KeyValuePair<IHId,TStateId>>();
			var foreignSTIds = Enumerable.Empty<IHId>();
			var pullSTs = Enumerable.Empty<IGrouping<IHId, TStateId>>();
			if (foreignIds.Count() > 0)
			{
				// Fetch it from somewhere
				foreach (var sid in foreignIds)
				{
					var hid = NamingSvcClient.Instance.GetObjectSpaceTimeID(sid.ToString());
					foreignStates.Add(new KeyValuePair<IHId,TStateId>(hid,sid));
				}
				foreignSTIds = foreignStates.Select(kv=>kv.Key).Distinct();

				var foreignSTs = foreignSTIds.Select(id => PseudoSyncMgr.Instance.GetSpacetime(id, evtOriginal))
											.Where(val=>val.HasValue)
											.ToDictionary(val=>val.Value.Timestamp.ID, val=>val.Value);

				pullSTs = foreignStates.Where(sp =>
					{
						var state = foreignSTs[sp.Key].States.FirstOrDefault(s => s.StateId.Equals(sp.Value));
						if (state == null)
							throw new ApplicationException("No such state in foreign Spacetime and creating state into external ST is not allowed");
						return (0 == (state.GetPatchFlag() & StatePatchFlag.CommutativeBit));
					}).GroupBy(kv => kv.Key, kv => kv.Value);

				// because we know who is involved, we can give headsup to them beforehand
				foreach (var st in pullSTs)
					if (PrePullResult.Failed == PseudoSyncMgr.Instance.PrePullRequest(st.Key, this.ID, evtOriginal, st))
						throw new ApplicationException("Failed to lock foreign ST");

				IHTimestamp failingST = null;
				foreach (var st in foreignSTs)
				{
					if (!MergeSpacetime(st.Value.Timestamp, st.Value.Redos, ref evtFinal))
					{
						failingST = st.Value.Timestamp;
						break;
					}
				}
				
				// failed to merge foreign ST
				if (failingST != null)
				{
					AbortAdvance();
					return false;
				}
			}

			//---------- collect old snapshot for redo ----------

			var states = stateParams.Select(sp => m_states[sp.Value.StateId]);
			var oldSnapshots = new List<StateSnapshot>();
			foreach (var ns in states)
				oldSnapshots.Add(ns.GetSnapshot());

			//-------- execute ---------

			evtFinal = evtFinal.Advance(ID);

			m_vm.Call(funcName, stateParams, constArgs.ToArray());

			//----- make redo -------

			var redo = new RedoEntry();
			redo.AffectedFSTs = foreignSTIds.ToList();
			redo.LocalChanges = new Dictionary<TStateId, StatePatch>();

			foreach (var spair in
						from s1 in states join s2 in oldSnapshots on s1.StateId equals s2.ID 
						select new SnapshotPair(s1,s2))
			{
				var patch = spair.First.Serialize(spair.Second, evtFinal);
				redo.LocalChanges.Add(spair.First.StateId, patch);
			}

			//--------- get approve from foreign STs ---------
			// Send "pull request" to spacetimes whose non-commutative sates we changed
			bool bApproved = true;
			var pulledSTIds = new List<IHId>();
			foreach (var fst in pullSTs)
			{
				bApproved &= PseudoSyncMgr.Instance.SyncPullRequest(fst.Key, this.ID, evtFinal, redo.LocalChanges.Where(kv => fst.Contains(kv.Key)).ToLookup(kv => kv.Key, kv => kv.Value));
				pulledSTIds.Add(fst.Key);
			}

			if (!bApproved)
			{
				//foreach (var stId in pulledSTIds)
				//    PseudoSyncMgr.Instance.RollbackPullRequest(stId);
				return false;
			}

			// ---------- commit to local ST ------------
			CommitAdvance(evtOriginal, evtFinal, states, redo);

			// Push native changes to spacetimes that are highly depend on us

			return true;
		}

		// TODO: the operation should not change spacetime data because it's not committed yet
		private bool MergeSpacetime(IHTimestamp foreignStamp, ILookup<TStateId, StatePatch> redos, ref IHEvent expectingEvent)
		{
			var expectingEventCopy = expectingEvent;
			// all states are put into m_states
			var newStates = new Dictionary<TStateId, State>();
			foreach (var fst in redos)
			{
				var fstateId = fst.Key;
				var patches = fst.OrderBy(patch => patch.FromRev, HTSFactory.GetEventComparer(foreignStamp.ID))
								.SkipWhile(patch => patch.ToRev.LtEq(expectingEventCopy)); // we use the expecting event because one event can be sync'ed from 2 sources

				foreach (var p in patches)
					p.data.Seek(0, SeekOrigin.Begin);

				State lst;
				if (!m_states.TryGetValue(fstateId, out lst))
				{

					if (!StatePatcher.TryCreateFromPatch(foreignStamp.ID, fstateId, patches.First(), out lst))
						throw new ApplicationException(string.Format("State {0} not found in current ST {1} and the first patch is not constructive patch {2}",fstateId, ID, patches.First().Flag ));
					m_states.Add(fstateId, lst);
				}

				foreach (var patch in patches)
					if (!lst.Patch(patch))
						return false;

				newStates.Add(fstateId, lst);
			}

			ExternalSTEntry entry;
			entry.LatestUpateTime = foreignStamp;
			entry.States = newStates;
			m_cachedExternalST[foreignStamp.ID] = entry;

			var localTime = HTSFactory.Make(ID, expectingEvent);
			expectingEvent = localTime.Join(foreignStamp).Event;
			return true;
		}

		private void SplitStateParams(IEnumerable<TStateId> stateIds, out IEnumerable<TStateId> natives, out IEnumerable<TStateId> externals)
		{
			natives = stateIds.Intersect(m_nativeStates.Keys);
			externals = stateIds.Except(natives);
		}

		// SHOULDN'T BE using it this lazy way
		//public IHierarchicalEvent Advance()
		//{
		//    var event_ = BeginAdvance();
		//    CommitAdvance(event_);
		//    return event_;
		//}

        private IHEvent BeginEventAdvance()
        {
            var newTime = m_currentTime.FireEvent();
			var oldVal = Interlocked.CompareExchange(ref m_executingEvent, newTime.Event, null);

			// if failed
			if (oldVal != null)
				return null;

            return newTime.Event;
        }

		private IHEvent BeginAdvance()
		{
			var retval = m_currentTime;
			var oldVal = Interlocked.CompareExchange(ref m_executingEvent, retval.Event, null);
			if (oldVal != null)
				return null;

			// pull don't necessarily have event increment on local component as long as the final result event is identifiable (by the component of external ST)
			return retval.Event;
		}

		private void CommitAdvance(IHEvent evt, IEnumerable<State> states, RedoEntry redo) { CommitAdvance(evt, evt, states, redo); }

		private void CommitAdvance(IHEvent evtOriginal, IHEvent evtFinal, IEnumerable<State> states, RedoEntry redo)
        {
			// If current time is not compatible to committing event
            if (!m_currentTime.Event.LtEq(evtFinal))
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
        }

		private void AbortAdvance()
		{
			Interlocked.Exchange(ref m_executingEvent, null);
		}

		#region ISerializable Members

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			throw new NotImplementedException();
		}

		#endregion

		internal PrePullResult PrePullRequest(IHId idRequester, IHEvent evtOriginal, IEnumerable<TStateId> affectedStates)
		{
			if (affectedStates.FirstOrDefault().Equals(default(TStateId)))
				return PrePullResult.NoLocking;

			// Currently we lock whole ST
			if (m_isWaitingForPullRequest)
				throw new ApplicationException("PullRequest lock already locked");

			lock (m_executionLock)
			{
				m_isWaitingForPullRequest = true;
			}

			if (m_currentTime.Event.LtEq(evtOriginal))
			{
				return PrePullResult.Succeeded;
			}

			return PrePullResult.Failed;
		}

		internal bool SyncPullRequest(IHId idRequester, IHEvent foreignExpectedEvent, ILookup<TStateId,StatePatch> redos)
		{
			var evtOriginal = BeginAdvance();
			var evtFinal = evtOriginal;

			var redo = new RedoEntry();
			redo.LocalChanges = new Dictionary<TStateId, StatePatch>();

			bool bOK = MergeSpacetime(HTSFactory.Make(idRequester, foreignExpectedEvent), redos, ref evtFinal);
			if (!bOK)
				return false;

			m_isWaitingForPullRequest = false;
			CommitAdvance(evtOriginal, evtFinal, Enumerable.Empty<State>(), redo);
			return true;
		}

		internal void PullFromVmSt(SpacetimeSnapshot vmST, TStateId vmStateId)
		{
			var cmp = HTSFactory.GetEventComparer(vmST.Timestamp.ID);

			var evtOri = BeginAdvance();
			var evtFinal = evtOri;

			var newRedos = vmST.Redos[vmStateId].OrderBy(patch=> patch.ToRev, HTSFactory.GetEventComparer(vmST.Timestamp.ID))
								.SkipWhile(patch => cmp.Compare(m_currentTime.Event, patch.FromRev) > 0)
								.ToLookup(_ => vmStateId);
			var localRedo = new RedoEntry();
			localRedo.LocalChanges = new Dictionary<TStateId, StatePatch>();

			if (!MergeSpacetime(vmST.Timestamp, newRedos, ref evtFinal))
				throw new ApplicationException("Failed to pull from VM Spacetime");

			State vmState;
			if (!m_states.TryGetValue(vmStateId, out vmState))
				throw new ApplicationException("VM Spacetime don't contain the VMState");

			m_vm = (VMState)vmState;
			CommitAdvance(evtOri, evtFinal, Enumerable.Empty<State>(), localRedo);
		}

		public void PullAllFrom(SpacetimeSnapshot foreignST)
		{
			var cmp = HTSFactory.GetEventComparer(foreignST.Timestamp.ID);

			var evtOri = BeginAdvance();
			var evtFinal = evtOri;

			var flatPairs = foreignST.Redos.SelectMany(kgroup => kgroup.Select(elem => new KeyValuePair<TStateId, StatePatch>(kgroup.Key, elem)));
			var newRedos = flatPairs.Where(kv => cmp.Compare(m_currentTime.Event, kv.Value.FromRev) <= 0)
								.ToLookup(p => p.Key, p => p.Value);
			var localRedo = new RedoEntry();
			localRedo.LocalChanges = new Dictionary<TStateId, StatePatch>();
			// TODO: external changes to our local states should be seen as local changes in the pull event?

			if (!MergeSpacetime(foreignST.Timestamp, newRedos, ref evtFinal))
				throw new ApplicationException(string.Format("Failed to pull from Spacetime {0}, to local {1}", foreignST.Timestamp.ToString(), m_currentTime));

			CommitAdvance(evtOri, evtFinal, Enumerable.Empty<State>(), localRedo);
		}
	}
}
