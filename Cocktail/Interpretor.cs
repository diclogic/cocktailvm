using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTK;
using OpenTK.Graphics;
using System.Reflection;
using itcsharp;
using HTS;
using Common;
using DOA;


namespace Cocktail
{
	[State]
	public class Particle : State
	{
		public float mass = 1;
		public Vector3 pt = new Vector3(0, 0, 0);
		public float radius = 1;
		public Vector3 accel = new Vector3(0, 0, 0);
		public Vector3 velocity = new Vector3(0, 0, 0);

        public Particle(SpaceTime spaceTime, IHierarchicalTimestamp creationStamp)
            : base(spaceTime, creationStamp)
        {

        }

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(base.ToString());
			sb.AppendFormat("{{mass = {0}, pt = {1}, radius = {2}, impulse = {3} }}"
							, mass
							, Vector3Extension.ToString(pt)
							, radius
							, Vector3Extension.ToString(accel));
			return sb.ToString();
		}
	}

	public class StateParam : IEquatable<StateParam>, IEqualityComparer<StateParam>
	{
		public string name;
		public string type;
		public int index;

		public bool Equals(StateParam rhs)
		{
			return name == rhs.name && type.Equals(rhs.type);
		}

		#region IEqualityComparer<StateParam> Members

		public bool Equals(StateParam x, StateParam y)
		{
			return x.name == y.name && x.type.Equals(y.type);
		}

		public int GetHashCode(StateParam obj)
		{
			throw new NotImplementedException();
		}

		#endregion
	}

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

		public override T GetField<T>(string name)
		{
			var state = NamingSvcClient.Instance.QueryObjectLocation(StateId.ToString(), m_refType);
			var type = state.GetType();
			return (T)type.GetField(name).GetValue(state);
		}

		public override void SetField<T>(string name, T val)
		{
			var state = NamingSvcClient.Instance.QueryObjectLocation(StateId.ToString(), m_refType);
			var type = state.GetType();
			type.GetField(name).SetValue(state, val);
		}
	}

	public class StateParamInst : StateParam
	{
		public StateRef arg;
	}

	public delegate List<Vector3> CollideDeleg(IEnumerable<StateParamInst> states, EStyle style);
	public delegate void GodPushDeleg(IEnumerable<StateParamInst> states, Vector3 force);


    /// <summary>
    /// the class for function instances that matches a form
    /// </summary>
	public class Function
	{
		Delegate m_fn;
		FunctionForm m_form;
		public Function(FunctionForm form, Delegate fn)
		{
			m_fn = fn;
			m_form = form;
		}
		public void Exec(IEnumerable<StateParamInst> states, IEnumerable<object> constArgs)
		{
			if (!m_form.Check(states, constArgs))
				throw new ApplicationException("The invocation doesn't match the form of the event declaration");
			var argList = m_form.GenArgList(states, constArgs).ToArray();
			m_fn.DynamicInvoke(argList);
		}

		//public static Function Make<P1>(Action<IEnumerable<StateParam>,P1> fn)
		//{
		//    var retval = new Function(fn);
		//    return retval;
		//}
	}

	public sealed class FunctionForm : IComparable<FunctionForm>
	{
		readonly Dictionary<string, StateParam> m_stateTypes;
		readonly List<Type> m_paramTypes;
        readonly string m_signature;


		private FunctionForm(IEnumerable<StateParam> stateInfo, IEnumerable<Type> paramInfo)
		{
			m_stateTypes = stateInfo.ToDictionary((sp) => sp.name);
			m_paramTypes = paramInfo.ToList();

            // Generate signature
            m_signature = GenerateSign(stateInfo, paramInfo);
		}

        private static string GenerateSign(IEnumerable<StateParam> stateInfo, IEnumerable<Type> paramInfo)
        {
            return "";
        }

		public IEnumerable<object> GenArgList(IEnumerable<StateParamInst> states, IEnumerable<object> constArgs)
		{
			states = states.Select<StateParamInst, StateParamInst>((spi) =>
				{
					StateParam sp;
					if (!m_stateTypes.TryGetValue(spi.name, out sp))
						throw new ApplicationException(string.Format("can't find state param '{0}'", spi.name));
					var newspi = new StateParamInst() { name = sp.name, type = sp.type, index = sp.index, arg = spi.arg };
					return newspi;
				});
			bool bStateRemain, bConstRemain;
			var stateEnumerator = states.GetEnumerator();
			var constEnumerator = constArgs.GetEnumerator();
			bStateRemain = stateEnumerator.MoveNext();
			bConstRemain = constEnumerator.MoveNext();

			int idx = 0;
			do
			{
				if (bStateRemain && idx == stateEnumerator.Current.index)
				{
					yield return stateEnumerator.Current.arg;
					bStateRemain = stateEnumerator.MoveNext();
				}
				else if (bConstRemain)
				{
					yield return constEnumerator.Current;
					bConstRemain = constEnumerator.MoveNext();
				}
				++idx;
			}
			while (bStateRemain || bConstRemain);
		}

        public bool Check(IEnumerable<StateParam> states, IEnumerable<Type> constParams)
        {
            return m_stateTypes.Values.SequenceEqual(states)
                && m_paramTypes.SequenceEqual(constParams);
        }

		public bool Check(IEnumerable<StateParam> states, IEnumerable<object> constArgs)
		{
            return Check(states, constArgs.Select<object, Type>((o) => o.GetType()));
				
			//int i = 0;
			//foreach (var c in constArgs)
			//{
			//    // inputs is longer than m_paramTypes
			//    if (i >= m_paramTypes.Count)
			//        return false;
			//    if (c.GetType() != m_paramTypes[i++])
			//        return false;
			//}
			//// inputs is fewer than m_paramTypes
			//if (i != m_paramTypes.Count)
			//    return false;
			//return true;
		}

		//public bool Check(IEnumerable<ParameterInfo> paramInfo)
		//{
		//    int i = 0;
		//    foreach (var p in paramInfo)
		//    {
		//        // inputs is longer than m_paramTypes
		//        if (i >= m_paramTypes.Count)
		//            return false;
		//        if (p.ParameterType != m_paramTypes[i++])
		//            return false;
		//    }
		//    // inputs is fewer than m_paramTypes
		//    if (i != m_paramTypes.Count)
		//        return false;
		//    return true;
		//}

		public bool Check(MethodInfo methodInfo)
		{
			var rhs = From(methodInfo);
			return Check(rhs);
		}

		public bool Check(FunctionForm rhs)
		{
			return m_stateTypes.Values.SequenceEqual(rhs.m_stateTypes.Values) && m_paramTypes.SequenceEqual(m_paramTypes);
		}

		public static FunctionForm From(MethodInfo methodInfo)
		{
			var states = new List<StateParam>();
			var parameters = new List<Type>();

			foreach (var p in methodInfo.GetParameters())
			{
				if (p.GetCustomAttributes(typeof(StateAttribute), false).FirstOrDefault() != null)
				{
					states.Add(new StateParam() { name = p.Name, type = p.ParameterType.ToString(), index = p.Position });
				}
				else
				{
					parameters.Add(p.ParameterType);
				}

			}
			return new FunctionForm(states, parameters);
		}

        public static FunctionForm From(IEnumerable<KeyValuePair<string, State>> states, IEnumerable<Type> paramTypes)
        {
            return new FunctionForm(
                states.Select<KeyValuePair<string, State>, StateParam>((kv) => new StateParam() { name = kv.Key, type = kv.Value.GetType().ToString() })
                , paramTypes);
        }

        public int CompareTo(FunctionForm rhs)
        {
            return m_signature.CompareTo(rhs.m_signature);
        }
    }


    internal struct SubscriptionSignature : IComparable<SubscriptionSignature>
    {
        public string _data;

        public int CompareTo(SubscriptionSignature rhs)
        {
            return _data.CompareTo(rhs._data);
        }
        // Example:
        // 000001-00000A:EventName
        //
        public static SubscriptionSignature Generate(IEnumerable<State> states, string eventName)
        {
            var stateKeys = new List<string>();
            foreach (var s in states)
                stateKeys.Add(s.GetHashCode().ToString("X"));

            stateKeys.Sort();

            SubscriptionSignature retval;
            retval._data = string.Join("-", stateKeys);
            retval._data += ":" + eventName;
            return retval;
        }
    }


	public class EventInstance
	{
		public string Name;
		public IEnumerable<KeyValuePair<string, State>> States;      //< involved states
		public IEnumerable<object> ConstArgs;
		//public TimeStamp timestamp;
	}

	public static class Utils
	{
		public static IEnumerable<KeyValuePair<string, StateRef>> MakeArgList(params object[] args)
		{
			for (int i = 0; i < args.Length; i += 2)
			{
				yield return new KeyValuePair<string, StateRef>((string)args[i], (StateRef)args[i + 1]);
			}
		}
	}

    //public class EventProcessor
    //{
    //    Dictionary<SubscriptionSignature, Function> m_subscriptions = new Dictionary<SubscriptionSignature, Function>();
    //    List<EventInstance> m_pendingEvents = new List<EventInstance>();
    //    Interpretor m_kernel;

    //    EventProcessor(Interpretor kernel)
    //    {
    //        m_kernel = kernel;
    //    }

    //    public void Happen(string eventName, IEnumerable<KeyValuePair<string, State>> states, params object[] constArgs)
    //    {
    //        m_pendingEvents.Add(new EventInstance() { Name = eventName, States = states, ConstArgs = constArgs });
    //    }

    //    public void DoAwareOf(string eventName, FunctionForm newForm, Delegate responser, State[] states)
    //    {
    //        FunctionForm form;
    //        if (!m_kernel.TryGetValue(eventName, out form))
    //        {
    //            Declare(eventName, newForm);
    //            form = newForm;
    //        }
    //        else
    //        {
    //            if (!form.Check(newForm))
    //                throw new ApplicationException("Trying to listen to a event which don't have the same form");
    //        }

    //        var key = SubscriptionSignature.Generate(states, eventName);
    //        m_subscriptions.Add(key, new Function(form, responser));
    //    }

    //    public void AwareOf(string eventName, MethodInfo methodInfo, Delegate responser, params State[] states)
    //    {
    //        DoAwareOf(eventName, FunctionForm.From(methodInfo), responser, states);
    //    }

    //    //public void AwareOf(string eventName, Action<IEnumerable<StateParamInst>> responser, params State[] states)
    //    //{
    //    //    DoAwareOf(eventName, FunctionForm.From(responser.Method), responser, states);
    //    //}
    //    //public void AwareOf<P1>(string eventName, Action<IEnumerable<StateParamInst>, P1> responser, params State[] states)
    //    //{
    //    //    DoAwareOf(eventName, FunctionForm.From(responser.Method), responser, states);
    //    //}
    //    //public void AwareOf<P1, P2>(string eventName, Action<IEnumerable<StateParamInst>, P1, P2> responser, params State[] states)
    //    //{
    //    //    DoAwareOf(eventName, FunctionForm.From(responser.Method), responser, states);
    //    //}
    //    //public void AwareOf<P1, P2, P3>(string eventName, Action<IEnumerable<StateParamInst>, P1, P2, P3> responser, params State[] states)
    //    //{
    //    //    DoAwareOf(eventName, FunctionForm.From(responser.Method), responser, states);
    //    //}

    //}

    public class CompileTimeException : ApplicationException
    {
        public CompileTimeException(string reason) : base(reason) { }
    }

    /// <summary>
    /// The prototype of cocktail interpretor backend
    /// </summary>
	public class Interpretor
	{
		public static Interpretor Instance = new Interpretor();

        class FunctionSignature : IComparable<FunctionSignature>
        {
            public string Name;
            public FunctionForm Form;

            public FunctionSignature(string name, FunctionForm form)
            {
                Name = name;
                Form = form;
            }

            public int CompareTo(FunctionSignature rhs)
            {
                var retval = this.Name.CompareTo(rhs);
                if (retval == 0)
                    retval = this.Form.CompareTo(rhs.Form);
                return retval;
            }
            public static FunctionSignature Generate(string name, IEnumerable<KeyValuePair<string, State>> states, IEnumerable<Type> paramTypes)
            {
                return new FunctionSignature(name, FunctionForm.From(states, paramTypes));
            }
        }

		Dictionary<string, List<FunctionSignature>> m_declGroups = new Dictionary<string, List<FunctionSignature>>();
        Dictionary<FunctionSignature, Function> m_functionBodies = new Dictionary<FunctionSignature, Function>();


        public void Declare(string name, MethodInfo methodInfo)
        {
            Declare(name, FunctionForm.From(methodInfo));
        }
		public void Declare(string name, FunctionForm form)
		{
            List<FunctionSignature> functionGroup;
            if (!m_declGroups.TryGetValue(name, out functionGroup))
            {
                functionGroup = new List<FunctionSignature>();
                m_declGroups.Add(name, functionGroup);
            }
            functionGroup.Add(new FunctionSignature(name, form));
		}

        public void Link(string name, FunctionForm form, Function body)
        {
            try
            {
                m_functionBodies.Add(new FunctionSignature(name, form), body);
            }
            catch (System.ArgumentException)
            {
                throw new CompileTimeException("Function body with the same signature already exists");
            }
        }

		public void DeclareAndLink(string name, MethodInfo methodInfo, Function body)
		{
			var form = FunctionForm.From(methodInfo);
			Declare(name, form);
			Link(name, form, body);
		}

		public void Call(string eventName, SpaceTime mainST, IEnumerable<KeyValuePair<string, StateRef>> states, params object[] constArgs)
		{
            var sign = Find(eventName
                , GenStateParams(states)
                , constArgs.Select<object,Type>((o)=>o.GetType()) );

            Function func;
            if (!m_functionBodies.TryGetValue(sign, out func))
				return;

			mainST.Execute(func, GenStateParamInsts(states), constArgs);
		}


		// Internal

        FunctionSignature Find(string name, IEnumerable<StateParam> stateParams, IEnumerable<Type> constParams )
        {
            List<FunctionSignature> funcGroup;
            if (!m_declGroups.TryGetValue(name, out funcGroup))
                return null;
            // TODO: should use complex algorithm (like decision tree) here, but i'm lazy right now
            foreach (var f in funcGroup)
            {
                if (f.Form.Check(stateParams, constParams))
                    return f;
            }
            return null;
        }


		IEnumerable<StateParamInst> GenStateParamInsts(IEnumerable<KeyValuePair<string, StateRef>> states)
		{
			foreach (var sp in states)
			{
				var param = new StateParamInst() { name = sp.Key, type = sp.Value.GetRefType(), arg = sp.Value };
				yield return param;
			}
		}

		IEnumerable<StateParam> GenStateParams(IEnumerable<KeyValuePair<string, StateRef>> states)
		{
			foreach (var sp in states)
			{
				var param = new StateParam() { name = sp.Key, type = sp.Value.GetRefType()};
				yield return param;
			}
		}
	}
}
