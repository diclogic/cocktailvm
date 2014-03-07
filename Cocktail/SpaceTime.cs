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
			public List<IHId> AffectedFSTs = new List<IHId>();
			public Dictionary<TStateId, StatePatch> LocalChanges = new Dictionary<TStateId, StatePatch>();
		}

        private IHIdFactory m_idFactory;
        private IHTimestamp m_currentTime;
		private Dictionary<TStateId, State> m_states;
		private Dictionary<TStateId, State> m_nativeStates;
		private List<RedoEntry> m_RedoList = new List<RedoEntry>();
		private object m_executionLock = new object();
		private SortedList<IHEvent, ExecutionFraction> m_incomingExecutions = new SortedList<IHEvent, ExecutionFraction>();
		private IHEvent m_executingEvent;
		private VMState m_vm;
		// we use cached state to "pro-act" on an event involves external states optimistically, and let the external ST denies it.
		private Dictionary<IHId, ExternalSTEntry> m_cachedExternalST = new Dictionary<IHId, ExternalSTEntry>();

		public IHId ID { get { return m_currentTime.ID; } }

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
			m_vm = (VMState)CreateState((st,_stamp) => new VMState(st,_stamp));

		}

		public State CreateState(Func<Spacetime, IHTimestamp, State> constructor)
        {
            var event_ = BeginAdvance();
            var newState = constructor(this, m_currentTime);
			m_states.Add(newState.StateId, newState);
			m_nativeStates.Add(newState.StateId, newState);

			var redo = new RedoEntry();

			var patch = newState.GetSnapshot(event_).GenerateCreatePatch(m_currentTime.Event);
			redo.LocalChanges.Add(newState.StateId, patch);

            CommitAdvance(event_, redo);
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

		public SpacetimeSnapshot Snapshot()
		{
			lock (m_executionLock)
			{
				return new SpacetimeSnapshot() { 
					Timestamp = m_currentTime,
					States = m_states.Values,
					Redos = m_RedoList.Aggregate(new List<KeyValuePair<TStateId, StatePatch>>(),
												(accu,item) =>
													{
														foreach (var sp in item.LocalChanges)
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
			var event_ = BeginAdvance();

			var redo = new RedoEntry();

			var iter = m_nativeStates.Values.GetEnumerator();
			var retval = ids.Select((id) =>
				{
					iter.MoveNext();
					redo.LocalChanges.Add(iter.Current.StateId, StatePatcher.GenerateDestroyPatch(event_, iter.Current.LatestUpdate.Event));
					return new Spacetime(id, event_, m_idFactory, new State[] { iter.Current });
				});
			CommitAdvance(event_, redo);
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

		public void Execute(string funcName, IEnumerable<KeyValuePair<string, StateRef>> stateParams, params object[] constArgs)
		{
			ExecuteArgs(funcName, stateParams, constArgs);
		}

		public bool ExecuteArgs(string funcName, IEnumerable<KeyValuePair<string,StateRef>> stateParams, IEnumerable<object> constArgs)
		{
			IHEvent evtOriginal,evtFinal;
			while ((evtOriginal = BeginAdvance()) == null) ;
			evtFinal = evtOriginal;

			// cross ST event
			IEnumerable<TStateId> nativeIds, foreignIds;
			SplitStateParams(stateParams.Select((sp) => sp.Value.StateId), out nativeIds, out foreignIds);

			var foreignSTs = new List<KeyValuePair<IHId,TStateId>>();
			if (foreignIds.Count() > 0)
			{
				// Fetch it from somewhere
				foreach (var sid in foreignIds)
				{
					var hid = NamingSvcClient.Instance.GetObjectSpaceTimeID(sid.ToString());
					foreignSTs.Add(new KeyValuePair<IHId,TStateId>(hid,sid));
				}

				IHTimestamp failingST = null;
				foreach (var hid in foreignSTs.ToLookup(kv=>kv.Key))
				{
					var st = PseudoSyncMgr.Instance.GetSpacetime(hid);
					if (!MergeSpacetime(st.Value.Timestamp, st.Value.States, st.Value.Redos, ref evtFinal))
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

			m_vm.Call(funcName, stateParams, constArgs.ToArray());

			//----- make redo -------

			var redo = new RedoEntry();
			redo.AffectedFSTs = foreignSTs.ToList();
			redo.LocalChanges = new Dictionary<TStateId, StatePatch>();

			foreach (var spair in
						from s1 in states join s2 in oldSnapshots on s1.StateId equals s2.ID 
						select new SnapshotPair(s1,s2))
			{
				var ostream = new MemoryStream();
				spair.First.Serialize(ostream, spair.Second);
				var patch = new StatePatch()
				{
					FromRev = spair.Second.Timestamp.Event,
					ToRev = evtFinal,
					data = ostream
				};
				redo.LocalChanges.Add(spair.First.StateId, patch);
			}

			//--------- get approve from foreign STs ---------

			// TODO: because we know who is involved, we can give headsup to them beforehand

			// Send "pull request" to spacetimes whose non-commutative sates we changed

			var syncFST = new HashSet<IHId>();
			foreach (var state in redo.LocalChanges)
			{
				if (0 != (state.Value.Flag & StatePatchFlag.CommutativeBit))
					continue;
				syncFST.Add(foreignIdToSTMap[state.Key]);
			}
			foreach (var fst in syncFST)
			{
				var bOK = PseudoSyncMgr.Instance.PullRequest(fst, this.ID, redo.LocalChanges[]);
			}

			CommitAdvance(evtOriginal, evtFinal, redo);

			// Push native changes to other spacetimes that are aware of us
			//PseudoSyncMgr.Instance.

			return true;
		}

		private bool MergeSpacetime(IHTimestamp foreignStamp, IEnumerable<State> foreignStates, ILookup<TStateId, StatePatch> redos, ref IHEvent expectingEvent)
		{
			var redoDict = redos;
			// all states are put into m_states
			var newStates = new Dictionary<TStateId, State>();
			foreach (var fst in foreignStates)
			{
				State lst;
				if (m_states.TryGetValue(fst.StateId, out lst))
				{
					foreach (var patch in redoDict[fst.StateId])
						if (!lst.Merge(fst.GetSnapshot(), patch))
							return false;
				}
				newStates.Add(fst.StateId, lst ?? fst);
			}

			ExternalSTEntry entry;
			entry.LatestUpateTime = foreignStamp;
			entry.States = newStates;
			m_cachedExternalST.Add(foreignStamp.ID, entry);

			var localTime = HTSFactory.Make(ID, expectingEvent);
			expectingEvent = localTime.Join(foreignStamp).Event;
			return true;
		}

		public void VMExecute(string funcName, params object[] constArgs)
		{
			VMExecuteArgs(funcName, constArgs);
		}
		public void VMExecuteArgs(string funcName, IEnumerable<object> constArgs)
		{
			ExecuteArgs(funcName
				, Enumerable.Repeat(new KeyValuePair<string, StateRef>("VM", new LocalStateRef<VMState>(m_vm)), 1)
				, constArgs);
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

        private IHEvent BeginAdvance()
        {
            var newTime = m_currentTime.FireEvent();
			var oldVal = Interlocked.CompareExchange(ref m_executingEvent, newTime.Event, null);

			// if failed
			if (oldVal != null)
				return null;

            return newTime.Event;
        }

		private void CommitAdvance(IHEvent evt, RedoEntry redo) { CommitAdvance(evt, evt, redo); }

        private void CommitAdvance(IHEvent evtOriginal, IHEvent evtFinal, RedoEntry redo)
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
	}
}
