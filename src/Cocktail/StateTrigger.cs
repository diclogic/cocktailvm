using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

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

		public void Fire(State state)
		{
			if (m_condition(state))
				m_response(state);
		}

	}

	public abstract partial class State
	{
		protected List<StateTrigger> m_triggers;
		protected Dictionary<string, StateTrigger> m_triggerLookup;

		public void RegisterInlineTrigger(Action<State> response, string condition)
		{

		}

		public void FireInlineTriggers(IEnumerable<string> affectedFields)
		{
			// TODO: only fire those triggers that cares one of those modified fields
			foreach (var tr in m_triggers)
			{
				tr.
			}
		}
	};
}
