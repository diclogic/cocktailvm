using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace Cocktail
{



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
			foreach (var stateInst in states)
				stateInst.arg.Sync();
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
			var constTypeEnumeerator = m_paramTypes.GetEnumerator();
			bStateRemain = stateEnumerator.MoveNext();
			bConstRemain = constTypeEnumeerator.MoveNext() && constEnumerator.MoveNext();

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
					var curVal = constEnumerator.Current;
					var curType = constTypeEnumeerator.Current;
					if (curType.IsAssignableFrom(curVal.GetType()))
						yield return curVal;
					else
						yield return Convert.ChangeType(curVal, curType);
					bConstRemain = constTypeEnumeerator.MoveNext() && constEnumerator.MoveNext();
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
				var ret = (spi.arg as RemoteStateRef).GetObject();
				return new KeyValuePair<object, int>(ret, spi.index);
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

}