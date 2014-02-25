﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using HTS;
using System.IO;
using DOA;
using System.Runtime.Serialization;

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

	public struct ExternalSTEntry
	{
		IHierarchicalTimestamp LatestUpateTime;
		HashSet<State> States;
	}

	/// <summary>
	/// VM is a special state. It has version. It can only be changed/updated only by deployment system
	/// </summary>
	public class VMState : State
	{
		public Interpreter Interpreter { get; private set; }

		public VMState(Spacetime st, IHierarchicalTimestamp stamp)
			:base(st,stamp)
		{
			Interpreter = new Interpreter();
			Interpreter.DeclareAndLink("Cocktail.DeclareAndLink", typeof(Interpreter).GetMethod("DeclareAndLink_cocktail"));
		}
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
        private IHierarchicalIdFactory m_idFactory;
        private IHierarchicalTimestamp m_currentTime;
		private IHierarchicalId m_id { get { return m_currentTime.ID; } }
		private Dictionary<TStateId, State> m_states;
		private object m_executionLock = new object();
		private SortedList<IHierarchicalEvent, ExecutionFraction> m_incomingExecutions;
		private IHierarchicalEvent m_executingEvent;
		private VMState m_vm;

		// we use cached state to "pro-act" on an event involves external states optimistically, and let the external ST denies it.
		private Dictionary<IHierarchicalId, ExternalSTEntry> m_cachedExternalST;

        public Spacetime(IHierarchicalTimestamp stamp, IHierarchicalIdFactory idFactory)
			:this(stamp, idFactory, Enumerable.Empty<State>())
        {
        }

		public Spacetime(IHierarchicalId id, IHierarchicalEvent event_, IHierarchicalIdFactory idFactory)
			:this(HierarchicalTimestampFactory.Make(id, event_), idFactory, Enumerable.Empty<State>())
		{
		}

		public Spacetime(IHierarchicalId id, IHierarchicalEvent event_, IHierarchicalIdFactory idFactory, IEnumerable<State> initialStates)
			:this(HierarchicalTimestampFactory.Make(id, event_), idFactory, initialStates)
		{
		}

		public Spacetime(IHierarchicalTimestamp stamp, IHierarchicalIdFactory idFactory, IEnumerable<State> initialStates)
		{
			m_currentTime = stamp;
			m_idFactory = idFactory;
			m_states = initialStates.ToDictionary((s) => s.StateId);
			m_vm = (VMState)CreateState((st,_stamp) => new VMState(st,_stamp));
		}

		public State CreateState(Func<Spacetime, IHierarchicalTimestamp, State> constructor)
        {
            var event_ = BeginAdvance();
            var newState = constructor(this, m_currentTime);
			m_states.Add(newState.StateId, newState);
            CommitAdvance(event_);
            return newState;
        }

		public Stream Serialize()
		{
			Stream retval = new MemoryStream();
			var writer = new BinaryWriter(retval, Encoding.UTF8);
			writer.Write(this.)
			
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
			int count = m_states.Count;
			if (count <= 1)
				return new Spacetime[] { this };
			var ids = m_idFactory.CreateChildren(m_id, count);
			var event_ = Advance();


			var iter = m_states.Values.GetEnumerator();
			return ids.Select((id) =>
				{
					iter.MoveNext();
					return new Spacetime(id, event_, m_idFactory, new State[] { iter.Current });
				});
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
						State state;
						if (m_states.TryGetValue(stateIn.Key, out state))
							state.Merge(stateIn.Value);
					}
				}
			}
		}

		public void Execute(string funcName, IEnumerable<KeyValuePair<string, StateRef>> stateParams, params object[] constArgs)
		{
			ExecuteArgs(funcName, stateParams, constArgs);
		}

		public void ExecuteArgs(string funcName, IEnumerable<KeyValuePair<string,StateRef>> stateParams, IEnumerable<object> constArgs)
		{
			IEnumerable<TStateId> excluded;
			// internal event
			if (ContainAll(stateParams.Select((sp) => sp.Value.StateId), out excluded))
			{
				IHierarchicalEvent evt;
				while ((evt = BeginAdvance()) == null) ;

				m_vm.Interpreter.Call(funcName, stateParams, constArgs.ToArray());
				CommitAdvance(evt);
			}

			foreach (var sp in stateParams.Where(kv=> excluded.Contains(kv.Value.StateId)))
			{
				var hid = NamingSvcClient.Instance.GetObjectSpaceTimeID(sp.Value.StateId.ToString());
				
			}
			{
				//TODO:

				// Use local cache first

				// Fetch it from somewhere

			}
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
				var oldVal = Interlocked.CompareExchange(ref m_executingEvent, null, event_);
				if (oldVal != event_)
					throw new ApplicationException("Race condition on m_executingEvent");

                m_currentTime = new ITCTimestamp(m_currentTime.ID as ITCIdentity, event_ as ITCEvent);
                return true;
            }

            return false;
        }

		#region ISerializable Members

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
