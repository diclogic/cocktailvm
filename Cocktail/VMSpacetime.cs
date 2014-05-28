using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HTS;
using System.Reflection;


namespace Cocktail
{

	public class VMSpacetime : Spacetime
	{
		public readonly TStateId VMStateId;

		public VMSpacetime(IHIdFactory idFactory)
			: base(idFactory.CreateFromRoot(), HTSFactory.CreateZeroEvent(), idFactory)
		{
			VMStateId = m_vm.StateId;
			m_storageComponent.AddNativeState(m_vm);
			DOA.NamingSvcClient.Instance.RegisterObject(VMStateId.ToString(), m_vm.GetType().FullName, m_vm);
		}

		public void VMBind(Type interf, Type impl)
		{
			if (!interf.IsInterface || !impl.IsClass)
				throw new RuntimeException(string.Format("Failed to bind interface `{0}' to implementation `{1}', one of them are disqalified"
										, interf.FullName, impl.FullName));

			foreach (var m in interf.GetMethods())
			{
				VMDefine(m.Name, impl.GetMethod(m.Name));
			}
		}

		public void VMDefine(string funcName, MethodInfo method)
		{
			VMExecute("Cocktail.DeclareAndLink", funcName, method);
		}

		public void VMExecute(string funcName, params object[] constArgs)
		{
			VMExecuteArgs(funcName, constArgs);
		}

		public void VMExecuteArgs(string funcName, IEnumerable<object> constArgs)
		{
			ExecuteArgs(funcName
				, InterpUtils.MakeArgList("VM", new LocalStateRef<VMState>(m_vm))
				, constArgs);
		}
	}
}
