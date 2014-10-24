using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Threading;

namespace Cocktail
{
	public enum TriggerConditionType
	{
		Changed,
		Customized,
	}

	public class StaticTriggerAttribute : Attribute
	{
		public Action<State> Trigger { get; set; }
		public Func<State,bool> Condition { get; set; }
		public StaticTriggerAttribute(Action<State> trigger, Func<State,bool> condition)
		{
			Trigger = trigger;
			Condition = condition;
		}
	}

	public class StateTrigger
	{
		private Action<State> m_response;
		private Func<State, bool> m_condition;
		public StateTrigger(Type stateType, Action<State> response, Func<State, bool> condition)
		{
			m_response = response;
			m_condition = condition;
		}

		public void TryFire(State state)
		{
			if (m_condition(state))
				m_response(state);
		}
	}

	public abstract partial class State
	{
		protected Dictionary<int, StateTrigger> m_triggers = new Dictionary<int,StateTrigger>();
		int m_idx = 0;

		// Well-known conditions
		private readonly Func<State, bool> s_Changed = (s) => true;	

		public int RegisterInlineTrigger(TriggerConditionType type, Action<State> response)
		{
			if (type == TriggerConditionType.Changed)
				return RegisterInlineTrigger(response, s_Changed);

			throw new ArgumentOutOfRangeException("Unsupported trigger condition type");
		}

		public int RegisterInlineTrigger(Action<State> response, Func<State, bool> condition)
		{
			var tr = new StateTrigger(GetType(), response, condition);
			var newIdx = Interlocked.Increment(ref m_idx);
			m_triggers.Add(newIdx, tr);
			return newIdx;
		}

		public void UnregisterInlineTrigger(int handle)
		{
			m_triggers.Remove(handle);
		}

		public void FireInlineTriggers()
		{
			foreach (var tr in m_triggers.Values)
			{
				tr.TryFire(this);
			}
		}
	};
}
