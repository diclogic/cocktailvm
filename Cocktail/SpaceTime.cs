using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using HTS;
using System.IO;

namespace Cocktail
{
	public struct ExecutionFraction 
	{
		public IHierarchicalTimestamp timeStamp;
		public KeyValuePair<TStateId,Stream>[] affectedStates;
	}

	internal class SpaceTimeMeta
	{

	}

    /// <summary>
    /// the SpaceTime represents the development of objects
    /// it is a thread apartment that can include one to many objects
    /// 1) An activated object must be in an spaceTime.
    /// 2) 2 SpaceTimes can merge into 1
    /// 3) SpaceTime cannot cross machine boundary (we need something else to do distributed transaction)
    /// </summary>
	public class SpaceTime
	{
        private IHierarchicalIdFactory m_idFactory;
        private IHierarchicalTimestamp m_currentTime;
		private IHierarchicalId m_id { get { return m_currentTime.ID; } }
		private Dictionary<TStateId, State> m_states;
		private object m_executionLock = new object();
		private SortedList<IHierarchicalEvent, ExecutionFraction> m_incomingExecutions;
		private IHierarchicalEvent m_executingEvent;

		// we use cached state to "pro-act" on an event involves external states optimistically, and let the external ST denies it.
		private Dictionary<IHierarchicalId, HashSet<State>> m_cachedExternalStates;

        public SpaceTime(IHierarchicalTimestamp stamp, IHierarchicalIdFactory idFactory)
			:this(stamp, idFactory, Enumerable.Empty<State>())
        {
        }

		public SpaceTime(IHierarchicalId id, IHierarchicalEvent event_, IHierarchicalIdFactory idFactory)
			:this(HierarchicalTimestampFactory.Make(id, event_), idFactory, Enumerable.Empty<State>())
		{
		}

		public SpaceTime(IHierarchicalId id, IHierarchicalEvent event_, IHierarchicalIdFactory idFactory, IEnumerable<State> initialStates)
			:this(HierarchicalTimestampFactory.Make(id, event_), idFactory, initialStates)
		{
		}

		public SpaceTime(IHierarchicalTimestamp stamp, IHierarchicalIdFactory idFactory, IEnumerable<State> initialStates)
		{
			m_currentTime = stamp;
			m_idFactory = idFactory;
			m_states = initialStates.ToDictionary((s) => s.StateId);
		}

		public State CreateState(Func<SpaceTime, IHierarchicalTimestamp, State> constructor)
        {
            var event_ = BeginAdvance();
            var newState = constructor(this, m_currentTime);
			m_states.Add(newState.StateId, newState);
            CommitAdvance(event_);
            return newState;
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
		public IEnumerable<SpaceTime> SplitForEach()
		{
			int count = m_states.Count;
			if (count <= 1)
				return new SpaceTime[] { this };
			var ids = m_idFactory.CreateChildren(m_id, count);
			var event_ = Advance();


			var iter = m_states.Values.GetEnumerator();
			return ids.Select((id) =>
				{
					iter.MoveNext();
					return new SpaceTime(id, event_, m_idFactory, new State[] { iter.Current });
				});
		}

		public static SpaceTime Merge(IEnumerable<SpaceTime> spaces)
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
						State state;
						if (m_states.TryGetValue(stateIn.Key, out state))
							state.Merge(stateIn.Value);
					}
				}
			}
		}

		public void Execute(Function func, IEnumerable<StateParamInst> stateParams, IEnumerable<object> constArgs)
		{
			IEnumerable<TStateId> excluded;
			// internal event
			if (ContainAll(stateParams.Select((stparam) => stparam.arg.StateId), out excluded))
			{
				IHierarchicalEvent evt;
				while ((evt = BeginAdvance()) == null) ;
				func.Exec(stateParams, constArgs);
				CommitAdvance(evt);
			}
			else
			{
				//TODO:

				// Use local cache first

				// Fetch it from somewhere

			}
		}

		private bool ContainAll(IEnumerable<TStateId> stateIds, out IEnumerable<TStateId> excluded)
		{
			excluded = stateIds.Except(m_states.Keys);
			return excluded.FirstOrDefault().IsNull();
		}

        public IHierarchicalEvent Advance()
        {
            var event_ = BeginAdvance();
            CommitAdvance(event_);
            return event_;
        }

        private IHierarchicalEvent BeginAdvance()
        {
            var newTime = m_currentTime.FireEvent();
			var oldVal = Interlocked.CompareExchange(ref m_executingEvent, newTime.Event, null);

			// if failed
			if (oldVal != null)
				return null;

            return newTime.Event;
        }

        private bool CommitAdvance(IHierarchicalEvent event_)
        {
            if (m_currentTime.Event.LtEq(event_))
            {
                m_currentTime = new ITCTimestamp(m_currentTime.ID as ITCIdentity, event_ as ITCEvent);
                return true;
            }

            return false;
        }
    }
}
