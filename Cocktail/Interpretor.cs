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
using System.IO;
using System.Reflection.Emit;


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

	public class StateParam : IEquatable<StateParam>
	{
		public string name;
		public string type;
		public int index;

		public bool Equals(StateParam rhs)
		{
			return name == rhs.name && type == rhs.type;
		}

	}

	public class StateParamComparer : IEqualityComparer<StateParam>
	{
		public bool Equals(StateParam x, StateParam y)
		{
			return x.name == y.name && x.type == y.type;
		}
		public int GetHashCode(StateParam obj)
		{
			return obj.name.GetHashCode() ^ obj.type.GetHashCode();
		}
	}

	public class ConstParamComparer : IEqualityComparer<Type>
	{
		public bool Equals(Type x, Type y)
		{
			return x.IsAssignableFrom(y);
		}

		public int GetHashCode(Type obj)
		{
			return obj.GetHashCode();
		}

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
		Func<object[],object> m_fn;
		public FunctionForm Form { get; private set; }
		public Function(FunctionForm form, Delegate fn)
		{
			m_fn = (args) => fn.DynamicInvoke(args);
			Form = form;
		}
		public Function(MethodInfo methodInfo)
		{
			Form = FunctionForm.From(methodInfo);
			// TODO: need Cocktail to C# invocation adapter
			m_fn = (args) => methodInfo.Invoke(null, args);
		}
		
		public void Exec(IEnumerable<StateParamInst> states, IEnumerable<object> constArgs)
		{
			if (!Form.Check(states, constArgs))
				throw new ApplicationException("The invocation doesn't match the form of the event declaration");
			var argList = Form.GenArgList(states, constArgs).ToArray();
			Exec(argList);
		}

		private void Exec(object[] argList)
		{
			// return value ignored for now
			m_fn(argList);
		}

		//public static Function Make<P1>(Action<IEnumerable<StateParam>,P1> fn)
		//{
		//    var retval = new Function(fn);
		//    return retval;
		//}
	}

	public sealed class FunctionForm : IComparable<FunctionForm>, IEquatable<FunctionForm>
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
			IEnumerable<KeyValuePair<object,int>> stateArgs = states.Select<StateParamInst, StateParamInst>((spi) =>
				{
					StateParam sp;
					if (!m_stateTypes.TryGetValue(spi.name, out sp))
						throw new ApplicationException(string.Format("can't find state param '{0}'", spi.name));
					var newspi = new StateParamInst() { name = sp.name, type = sp.type, index = sp.index, arg = spi.arg };
					return newspi;
				}).Select(ConvertCocktailToCSharp);
			bool bStateRemain, bConstRemain;
			var stateEnumerator = stateArgs.GetEnumerator();
			var constEnumerator = constArgs.GetEnumerator();
			bStateRemain = stateEnumerator.MoveNext();
			bConstRemain = constEnumerator.MoveNext();

			int idx = 0;
			do
			{
				if (bStateRemain && idx == stateEnumerator.Current.Value)
				{
					yield return stateEnumerator.Current.Key;
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

		private static KeyValuePair<object, int> ConvertCocktailToCSharp(StateParamInst spi)
		{
			// no need to convert if already warpped
			if (spi.type == "Cocktail.StateRef")
				return new KeyValuePair<object, int>(spi.arg, spi.index);

			var stateType = spi.arg.GetType();
			if (stateType.IsGenericType && stateType.GetGenericTypeDefinition() == typeof(LocalStateRef<>))
			{
				var ret = stateType.InvokeMember("GetInterface", BindingFlags.InvokeMethod, null, spi.arg, new object[0]);
				return new KeyValuePair<object, int>(ret, spi.index);
			}
			else if (stateType == typeof(RemoteStateRef))
			{
				throw new NotImplementedException();
			}

			throw new ArgumentOutOfRangeException("Unknown StateRef");
		}

        public bool Check(IEnumerable<StateParam> states, IEnumerable<Type> constParams)
        {
			//var eqsts = from st in m_stateTypes.Values
			//    from sp in states
			//        where st == sp
			//        select st;
            return m_stateTypes.Values.SequenceEqual(states, new StateParamComparer())
				&& m_paramTypes.SequenceEqual(constParams, new ConstParamComparer());
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

		public bool Equals(FunctionForm rhs)
		{
			if (this.m_stateTypes.Count != rhs.m_stateTypes.Count
				|| this.m_paramTypes.Count != rhs.m_paramTypes.Count)
				return false;
			foreach (var stateKV in this.m_stateTypes)
			{
				StateParam sp;
				if (!rhs.m_stateTypes.TryGetValue(stateKV.Key, out sp))
					return false;
				if (!stateKV.Value.Equals(sp))
					return false;
			}

			if (!this.m_paramTypes.SequenceEqual(rhs.m_paramTypes, new ConstParamComparer()))
				return false;

			return true;
		}

		public override int GetHashCode()
		{
			int retval = 0;
			foreach (var pt in m_paramTypes)
				retval ^= pt.FullName.GetHashCode();
			foreach (var st in m_stateTypes)
				retval ^= st.Value.type.GetHashCode();
			return retval;
		}
    }

	public class FunctionSignature : IComparable<FunctionSignature>, IEquatable<FunctionSignature>
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

		public bool Equals(FunctionSignature rhs)
		{
			return (this.Name == rhs.Name) && this.Form.Equals(rhs.Form);
		}

		public static FunctionSignature Generate(string name, IEnumerable<KeyValuePair<string, State>> states, IEnumerable<Type> paramTypes)
		{
			return new FunctionSignature(name, FunctionForm.From(states, paramTypes));
		}
	}

	public class FunctionSignatureComparer : IEqualityComparer<FunctionSignature>
	{
		public bool Equals(FunctionSignature x, FunctionSignature y)
		{
			return x.Equals(y);
		}

		public int GetHashCode(FunctionSignature obj)
		{
			return obj.Name.GetHashCode() ^ obj.Form.GetHashCode();
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

	public class RuntimeException : ApplicationException
	{
        public RuntimeException(string reason) : base(reason) { }
	}


    /// <summary>
    /// The prototype of cocktail interpretor backend
    /// </summary>
	public class Interpretor
	{
		public static Interpretor Instance = new Interpretor();

		Dictionary<string, List<FunctionSignature>> m_declGroups = new Dictionary<string, List<FunctionSignature>>();
		Dictionary<FunctionSignature, Function> m_functionBodies;

		public Interpretor()
		{
			m_functionBodies = new Dictionary<FunctionSignature, Function>(new FunctionSignatureComparer());
		}

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

        public void Link(string name, Function body)
        {
            try
            {
                m_functionBodies.Add(new FunctionSignature(name, body.Form), body);
            }
            catch (System.ArgumentException)
            {
                throw new CompileTimeException("Function body with the same signature already exists");
            }
        }

		public void DeclareAndLink(string name, MethodInfo methodInfo)
		{
			var form = FunctionForm.From(methodInfo);
			Declare(name, form);
			Link(name, new Function(methodInfo));
		}

		public static void DeclareAndLink_cocktail([State] VMState VM, string name, MethodInfo methodInfo)
		{
			VM.Interpretor.DeclareAndLink(name, methodInfo);
		}

		// TODO: implement
		/// <summary>
		/// Serialize and deploy a function from remote
		/// </summary>
		public void DeclareAndLinkBinary(string name, FunctionForm form, Stream binary)
		{

		}

		public void Call(string eventName, SpaceTime mainST, IEnumerable<KeyValuePair<string, StateRef>> states, params object[] constArgs)
		{
			var stateParams = GenStateParams(states);
			var constTypes = constArgs.Select<object,Type>((o)=>o.GetType());
            var sign = Find(eventName , stateParams , constTypes );

			if (sign == null)
				throw new CompileTimeException(string.Format("Function [{0}({1})] was not declared"
												, eventName
												, MakeSignatureString(stateParams, constTypes) ));

            Function func;
            if (!m_functionBodies.TryGetValue(sign, out func))
				throw new RuntimeException(string.Format("Function [{0}({1})] was not linked to any body"
											, eventName
											, MakeSignatureString(stateParams, constTypes) ));

			func.Exec(GenStateParamInsts(states), constArgs);
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

		string MakeSignatureString(IEnumerable<StateParam> stateParams, IEnumerable<Type> constParams)
		{
			var sb = new StringBuilder();
			foreach (var sp in stateParams.ToList())
				sb.AppendFormat("{0} {1},", sp.type, sp.name);
			foreach (var cp in constParams)
				sb.AppendFormat("const {0},", cp.Name);
			return sb.ToString(0, Math.Max(0, sb.Length - 1));
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
