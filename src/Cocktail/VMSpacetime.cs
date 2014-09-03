using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Cocktail.HTS;
using System.Reflection;
using Cocktail.Interp;


namespace Cocktail
{
	/// <summary>
	/// The Spacetime that the VM lives in
	/// </summary>
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

		public bool VMExist(Type interf)
		{
			if (!interf.IsInterface)
				throw new RuntimeException(string.Format("Exist(): the provided type {0} is not an interface"
										, interf.FullName));

			foreach (var m in interf.GetMethods())
			{
				if (!VMExist(m.Name))
					return false;
			}

			return true;
		}

		public bool VMExist(string funcName)
		{
			return m_vm.IsDeclared(funcName);
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
			ExecuteArgs("Cocktail.DeclareAndLink"
				, Utils.MakeArgList("VM", new _LocalStateRef<VMState>(m_vm))
				, new object[] { funcName, method });
		}
	}
}
