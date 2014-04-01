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
			//{
			//    var ctorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new Type[0]);
			//    var il = ctorBuilder.GetILGenerator();
			//    il.Emit(OpCodes.Ret);
			//}
			typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);


			// methods implementation
			foreach (var ifMethod in interf.GetMethods())
			{
				//void Deposit(Spacetime ST, StateRef account, float amount)
				//{
				//    ST.Execute("Deposit", Utils.MakeArgList("account", account), amount);
				//}

				var ifMethodParams = ifMethod.GetParameters();

				var methodBuilder = typeBuilder.DefineMethod(
															ifMethod.Name,
															MethodAttributes.Public | MethodAttributes.Virtual,
															//CallingConventions.HasThis,
															ifMethod.ReturnType,
															ifMethodParams.Select(mp => mp.ParameterType).ToArray());

				var il = methodBuilder.GetILGenerator();
				il.UsingNamespace("Cocktail");

				// local vars
				//var localCocktailArgs = il.DeclareLocal(typeof(KeyValuePair<string, StateRef>[]));

				//static Utils.MakeArgList()
				{
					int count = 0;
				    //var argTypes = new List<Type>();
					foreach (var mp in ifMethodParams)
					{
						if (mp.ParameterType == typeof(StateRef))
						{
							il.Emit(OpCodes.Ldstr, mp.Name);
							il.Emit(OpCodes.Ldarg, mp.Position + 1);
							//argTypes.Add(typeof(string));
							//argTypes.Add(typeof(StateRef));
							++count;
						}
					}
					il.Emit(OpCodes.Stelem_Ref);
					il.Emit(OpCodes.Ldc_I4, count);
					il.Emit(OpCodes.Newarr, typeof(object));

					var methodMakeArgList = typeof(Utils).GetMethod("MakeArgArray", new[] { typeof(object[]) });
					il.EmitCall(OpCodes.Call, methodMakeArgList, null);
					il.Emit(OpCodes.Pop);
					//il.Emit(OpCodes.Stloc, localCocktailArgs);
				}

				// ST.Execute()
				//{
				//    il.Emit(OpCodes.Ldarg_1);
				//    il.Emit(OpCodes.Ldstr, ifMethod.Name);
				//    il.Emit(OpCodes.Ldloc, localCocktailArgList);
				//    //var argTypes = new List<Type>();
				//    foreach (var cmp in ifMethodParams.Skip(1))	//< TODO: the first param should always be Spacetime
				//    {
				//        if (cmp.ParameterType != typeof(StateRef))
				//        {
				//            il.Emit(OpCodes.Ldarg, cmp.Position + 1);
				//            //argTypes.Add(cmp.ParameterType);
				//        }
				//    }
				//    var methodExecute = typeof(Spacetime).GetMethod("Execute");
				//    il.EmitCall(OpCodes.Call, methodExecute, null);
				//    il.Emit(OpCodes.Pop);	//< we don't use the return value (for now)
				//}

				il.Emit(OpCodes.Ret);
			}

			return typeBuilder.CreateType();
		}

		private static void AddToArray()
		{

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
