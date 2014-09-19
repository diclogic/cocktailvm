﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Threading;

namespace Cocktail
{
	public class StaticTriggerAttribute : Attribute
	{
		public Action<State> Trigger { get; set; }
		public Expression Condition { get; set; }
		public StaticTriggerAttribute(Action<State> trigger, Expression condition)
		{
			Trigger = trigger;
			Condition = condition;
		}
	}

	public class StateTrigger
	{
		private Action<State> m_response;
		private Func<State, bool> m_condition;
		public StateTrigger(Type stateType, Action<State> response, LambdaExpression condition)
		{
			m_response = response;
			m_condition = CompileCondition(condition,stateType);
		}

		Func<State,bool> CompileCondition(LambdaExpression condition, Type stateType)
		{
			foreach (var param in condition.Parameters)
			{
				param.Name;

			}
			
			BlockExpression wrapper = Expression.Block(
				new[] {Expression.Parameter(typeof(string), "")},
				Expression.Assign(),
				Expression.Call()
				);
			fn.Invoke();
			condition.Body
		}

		public void TryFire(State state)
		{
			if (m_condition(state))
				m_response(state);
		}

	}

	public abstract partial class State
	{
		protected Dictionary<int, StateTrigger> m_triggers;
		int m_idx = 0;

		public int RegisterInlineTrigger(Action<State> response, LambdaExpression condition)
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
