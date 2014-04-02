using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Text;
using Cocktail;

namespace Cocktail
{
	public static class InvocationBuilder
	{
		public static Type Build( Type interf)
		{
			// checking
			CheckInterface(interf);

			// module creation
			var module = CreateModule(interf);

			// type definition
			var typeName = string.Format("Proxy_{0}", interf.Name);
			var typeBuilder = module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
			typeBuilder.AddInterfaceImplementation(interf);

			// constructor
			{
				var ctorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new Type[0]);
				var il = ctorBuilder.GetILGenerator();
				il.Emit(OpCodes.Ret);
			}

			// methods
			foreach (var ifMethod in interf.GetMethods())
			{
				//void Deposit(Spacetime ST, StateRef account, float amount)
				//{
				//    ST.Execute("Deposit", Utils.MakeArgList("account", account), amount);
				//}

				var ifMethodParams = ifMethod.GetParameters();
				var loadArgSpacetime = OpCodes.Ldarg_1;

				if (!typeof(Spacetime).IsAssignableFrom(ifMethodParams.FirstOrDefault().ParameterType))
					throw new ApplicationException("First param must be `Spacetime' or it's subclasses");

				var methodBuilder = typeBuilder.DefineMethod(
															ifMethod.Name,
															MethodAttributes.Public | MethodAttributes.Virtual,
															//CallingConventions.HasThis,
															ifMethod.ReturnType,
															ifMethodParams.Select(mp => mp.ParameterType).ToArray());

				var il = methodBuilder.GetILGenerator();

				var stateArgPairsType = typeof(IEnumerable<KeyValuePair<string, StateRef>>);

				// local vars
				il.DeclareLocal(typeof(object[]));	// varargs
				var localStateArgs = il.DeclareLocal(stateArgPairsType);
				var storeVarArgs = OpCodes.Stloc_0;
				var loadVarArgs = OpCodes.Ldloc_0;


				//static Utils.MakeArgList()
				{
					var stateParams = ifMethodParams.Where(x => x.ParameterType == typeof(StateRef));
					CreateArray(il, stateParams.Count() * 2);	//< 2 elements per arg
					il.Emit(storeVarArgs);

					int idx = 0;
					foreach (var sp in stateParams)
					{
						il.Emit(loadVarArgs);
						AddToArray(il, idx, (il2) => il2.Emit(OpCodes.Ldstr, sp.Name));
						++idx;
						il.Emit(loadVarArgs);
						AddToArray(il, idx, (il2) => il2.Emit(OpCodes.Ldarg, sp.Position + 1));
						++idx;
					}

					il.Emit(loadVarArgs);
					var methodMakeArgList = typeof(Utils).GetMethod("MakeArgList", new[] { typeof(object[]) });
					il.EmitCall(OpCodes.Call, methodMakeArgList, null);
					il.Emit(OpCodes.Stloc, localStateArgs);
				}

				// ST.Execute(Spacetime, "Deposit", ...
				// ST.Execute(..., params object[] args)
				{
					il.Emit(loadArgSpacetime);
					il.Emit(OpCodes.Ldstr, ifMethod.Name);
					il.Emit(OpCodes.Ldloc, localStateArgs);

					var constParams = ifMethodParams.Skip(1).Where(x => x.ParameterType != typeof(StateRef));

					CreateArray(il, constParams.Count());
					il.Emit(storeVarArgs);

					int idx = 0;
					foreach (var cp in constParams)
					{
						il.Emit(loadVarArgs);
						AddToArray(il, idx, (il2) =>
							{
								il2.Emit(OpCodes.Ldarg, cp.Position + 1);
								if (cp.ParameterType.IsValueType)
									il2.Emit(OpCodes.Box, cp.ParameterType);
							});
						++idx;
					}

					il.Emit(loadVarArgs);
					var methodExecute = typeof(Spacetime).GetMethod("Execute", new[] {typeof(string), stateArgPairsType, typeof(object[])});
					il.EmitCall(OpCodes.Callvirt, methodExecute, null);
					il.Emit(OpCodes.Pop);	//< we don't use the return value (for now)
				}

				il.Emit(OpCodes.Ret);
			}

			return typeBuilder.CreateType();
		}

		private static void CreateArray(ILGenerator il, int size)
		{
			il.Emit(OpCodes.Ldc_I4, size);
			il.Emit(OpCodes.Newarr, typeof(object));
		}

		private static void AddToArray(ILGenerator il, int arrIdx, Action<ILGenerator> pushObjFunc)
		{
			il.Emit(OpCodes.Ldc_I4, arrIdx);
			pushObjFunc(il);
			il.Emit(OpCodes.Stelem_Ref);
		}

		private static ModuleBuilder CreateModule(Type interf)
		{
			var assemblyName = new AssemblyName("InmemoryInvocation_" + interf.Name);
			var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
			return assembly.DefineDynamicModule("InmemoryInvocation_"+interf.Name, true);
		}

		private static void CheckInterface(Type interf)
		{
			if (!interf.IsInterface)
			{
				throw new ArgumentException(String.Format("Can't create log service for type {0}", interf), "_interface");
			}
		}
	}
}
