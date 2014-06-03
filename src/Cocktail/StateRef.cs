using System;
using DOA;

namespace Cocktail
{


	public abstract class StateRef
	{
		protected string m_refType;
		public TStateId StateId { get; private set; }

		protected StateRef(TStateId stateId, Type refType)
			:this(stateId, refType.ToString())
		{
		}
		protected StateRef(TStateId stateId, string refType)
		{
			StateId = stateId;
			m_refType = refType;
		}
		public string GetRefType() { return m_refType; }
		//public virtual object GetInterface() { return null; }

		public virtual void Sync() { throw new NotImplementedException(); }

		public virtual T GetField<T>(string name) { throw new NotImplementedException(); }
		public virtual void SetField<T>(string name, T val) { throw new NotImplementedException(); }
	}

	public abstract class StateRefT<T>: StateRef
		where T : class
	{
		protected StateRefT(TStateId stateId)
			: base(stateId, typeof(T))
		{
		}

		//public virtual T GetInterface() { return null; }
	}

	public class LocalStateRef<T> : StateRefT<T>
		where T : State
	{
		State m_impl;

		public LocalStateRef(T impl)
			:base(impl.StateId)
		{
			m_impl = impl;
		}

		public T GetInterface()
		{
			return (T)m_impl;
		}

		public override void Sync() { }

		public override TField GetField<TField>(string name)
		{
			return (TField)m_impl.GetType().GetField(name).GetValue(m_impl);
		}

		public override void SetField<TField>(string name, TField val)
		{
			m_impl.GetType().GetField(name).SetValue(m_impl, val);
		}
	}

	public class RemoteStateRef : StateRef
	{
		public RemoteStateRef(TStateId stateId, string refType)
			: base(stateId, refType)
		{
		}

		public object GetObject()
		{
			var state = NamingSvcClient.Instance.GetObject(StateId.ToString(), m_refType);
			return state;
		}

		public override void Sync()
		{
			//TODO: send it back, with version checking and merging
		}

		public override T GetField<T>(string name)
		{
			var state = NamingSvcClient.Instance.GetObject(StateId.ToString(), m_refType);
			var type = state.GetType();
			return (T)type.GetField(name).GetValue(state);
		}

		public override void SetField<T>(string name, T val)
		{
			var state = NamingSvcClient.Instance.GetObject(StateId.ToString(), m_refType);
			var type = state.GetType();
			type.GetField(name).SetValue(state, val);
		}
	}

}