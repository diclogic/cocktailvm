using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;


namespace Cocktail
{

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

	public static class InterpUtils
	{
		public static IEnumerable<KeyValuePair<string, StateRef>> MakeArgList(params object[] args)
		{
			for (int i = 0; i < args.Length; i += 2)
			{
				yield return new KeyValuePair<string, StateRef>((string)args[i], (StateRef)args[i + 1]);
			}
		}

		public static KeyValuePair<string, StateRef>[] MakeArgArray(params object[] args)
		{
			return MakeArgList(args).ToArray();
		}
	}

    //public class EventProcessor
    //{
    //    Dictionary<SubscriptionSignature, Function> m_subscriptions = new Dictionary<SubscriptionSignature, Function>();
    //    List<EventInstance> m_pendingEvents = new List<EventInstance>();
    //    Interpreter m_kernel;

    //    EventProcessor(Interpreter kernel)
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

	public class JITCompileException : ApplicationException
	{
		public JITCompileException(string reason) : base(reason) { }
	}

	public class RuntimeException : ApplicationException
	{
        public RuntimeException(string reason) : base(reason) { }
	}


    /// <summary>
    /// The prototype of cocktail interpreter backend
    /// </summary>
	public class Interpreter
	{
		public static Interpreter Instance = new Interpreter();

		// TODO: support function overloading
		Dictionary<string, List<FunctionSignature>> m_declGroups = new Dictionary<string, List<FunctionSignature>>();
		Dictionary<FunctionSignature, Function> m_functionBodies;

		public Interpreter()
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

		public bool IsDeclared(string name)
		{
			return m_declGroups.ContainsKey(name);
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
			VM.DeclareAndLink(name, methodInfo);
		}

		// TODO: implement
		/// <summary>
		/// Serialize and deploy a function from remote
		/// </summary>
		public void DeclareAndLinkBinary(string name, FunctionForm form, Stream binary)
		{

		}

		public void Callva(string eventName, IEnumerable<KeyValuePair<string, StateRef>> states, params object[] constArgs)
		{
			Call(eventName, states, constArgs);
		}
		public void Call(string eventName, IEnumerable<KeyValuePair<string, StateRef>> states, IEnumerable<object> constArgs)
		{
			if (states.Any(kv => kv.Value == null))
				throw new RuntimeException("state argument can't be null");

			var stateParams = GenStateParams(states);
			var constTypes = constArgs.Select((o) => o == null ? typeof(object) : o.GetType());
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
