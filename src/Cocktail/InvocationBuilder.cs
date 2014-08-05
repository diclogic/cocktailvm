using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Cocktail
{
	public class InvokerAttribute : Attribute { }

	
	public static class InvocationBuilder
	{
		public static T Build<T>()
			where T: class
		{
			var implType = BuildType(typeof(T));
			return Activator.CreateInstance(implType) as T;
		}

		public static Type BuildType(Type interf)
		{
			// must be interface
			if (!interf.IsInterface)
				throw new JITCompileException(String.Format("Can't create invocation, the type must be an interface: {0}", interf.FullName));

			if (!interf.GetCustomAttributes(typeof(InvokerAttribute), false).Any())
				throw new JITCompileException(string.Format("Can't create invocation, the type must have [Invoker] attribute: {0}", interf.FullName));


			// module creation
			var module = GenerateModule("CocktailInvoker_"+interf.Name);
			var source = module.DefineDocument(new StackFrame(true).GetFileName(), Guid.Empty, Guid.Empty, Guid.Empty);

			// type definition
			TypeBuilder typeBuilder;
			{
				var typeName = string.Format("Proxy_{0}", interf.Name);
				typeBuilder = module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
			}

			// declare interface
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
				GenerateMethod(typeBuilder, ifMethod, source);
			}

			return typeBuilder.CreateType();
		}

		
		/// <summary>
		/// the following il code makes something like this:
		/// void Deposit(StateRef account, float amount)
		/// {
		///     WithIn.GetWithin()
		///         .Execute("Deposit", Utils.MakeArgList("account", account), amount);
		/// }
		/// </summary>
		private static void GenerateMethod(TypeBuilder typeBuilder, MethodInfo ifMethod, ISymbolDocumentWriter source)
		{
			var ifMethodParams = ifMethod.GetParameters();

			var methodBuilder = typeBuilder.DefineMethod(
														ifMethod.Name,
														MethodAttributes.Public | MethodAttributes.Virtual,
														ifMethod.ReturnType,
														ifMethodParams.Select(mp => mp.ParameterType).ToArray());

			var il = methodBuilder.GetILGenerator();

			var stateArgPairsType = typeof(IEnumerable<KeyValuePair<string, StateRef>>);

			// local variables
			var localVarargs = il.DeclareLocal(typeof(object[]));	// varargs
			var localStateArgs = il.DeclareLocal(stateArgPairsType);
			localStateArgs.SetLocalSymInfo("stateArgs");
			var localST = il.DeclareLocal(typeof(Spacetime));

			// exceptions
			var runtimeExcType = typeof(Cocktail.RuntimeException);

			//static Utils.MakeArgList(...)
			{
				var stateParams = ifMethodParams.Where(x => x.ParameterType == typeof(StateRef));
				if (stateParams.FirstOrDefault() == null)
					throw new JITCompileException(string.Format("Each method must have at least one state. Method name:", ifMethod.Name));

				MarkTraceablePoint(il, source, new StackFrame(true));
				CreateArray(il, localVarargs, stateParams.Count() * 2);	//< 2 elements per arg

				int idx = 0;
				foreach (var sp in stateParams)
				{
					AddToArray(il, localVarargs, idx, (il2) => il2.Emit(OpCodes.Ldstr, sp.Name));
					++idx;
					AddToArray(il, localVarargs, idx, (il2) => il2.Emit(OpCodes.Ldarg, sp.Position + 1));
					++idx;
				}

				il.Emit(OpCodes.Ldloc, localVarargs);
				var methodMakeArgList = typeof(InterpUtils).GetMethod("MakeArgList", new[] { typeof(object[]) });
				il.EmitCall(OpCodes.Call, methodMakeArgList, null);
				il.Emit(OpCodes.Stloc, localStateArgs);
			}

			// var ST = WithIn.GetWithin();
			// if (ST==null) throw RuntimeExc...
			// ST.Execute("Deposit", ..., params object[] args)
			{
				var lableOK = il.DefineLabel();

				var methodGetWthin = typeof(WithIn).GetMethod("GetWithin", new Type[] { });
				MarkTraceablePoint(il, source, new StackFrame(true));
				il.EmitCall(OpCodes.Call, methodGetWthin, null);
				il.Emit(OpCodes.Stloc, localST);

				il.Emit(OpCodes.Ldloc, localST);
				il.Emit(OpCodes.Brtrue_S, lableOK);

				MarkTraceablePoint(il, source, new StackFrame(true));
				ThrowRuntimeException(il, @"No Spacetime scope detected, please wrap your call with using(new WithIn(ST)){...}");

				MarkTraceablePoint(il, source, new StackFrame(true));
				il.MarkLabel(lableOK);
				il.Emit(OpCodes.Ldloc, localST);
				il.Emit(OpCodes.Ldstr, ifMethod.Name);
				il.Emit(OpCodes.Ldloc, localStateArgs);

				var constParams = ifMethodParams.Skip(1).Where(x => x.ParameterType != typeof(StateRef));

				CreateArray(il, localVarargs, constParams.Count());

				int idx = 0;
				foreach (var cp in constParams)
				{
					AddToArray(il, localVarargs, idx, (il2) =>
						{
							il2.Emit(OpCodes.Ldarg, cp.Position + 1);
							if (cp.ParameterType.IsValueType)
								il2.Emit(OpCodes.Box, cp.ParameterType);
						});
					++idx;
				}

				il.Emit(OpCodes.Ldloc, localVarargs);
				var methodExecute = typeof(Spacetime).GetMethod("Execute", new[] { typeof(string), stateArgPairsType, typeof(object[]) });
				il.EmitCall(OpCodes.Callvirt, methodExecute, null);
				il.Emit(OpCodes.Pop);	//< we don't use the return value (for now)
			}

			il.Emit(OpCodes.Ret);
		}

		private static void MarkTraceablePoint(ILGenerator il, ISymbolDocumentWriter source, StackFrame frame)
		{
			var ln = frame.GetFileLineNumber();
			il.MarkSequencePoint(source, ln, 1, ln, 100);
		}

		private static void ThrowRuntimeException(ILGenerator il, string msg)
		{
			ThrowRuntimeException(il, typeof(RuntimeException), msg);
		}

		private static void ThrowRuntimeException(ILGenerator il, Type excType, string msg)
		{
			Debug.Assert(typeof(RuntimeException).IsAssignableFrom(excType));
			var ctor = excType.GetConstructor(new[] { typeof(string) });
			il.Emit(OpCodes.Ldstr, msg);
			il.Emit(OpCodes.Newobj, ctor);
			il.Emit(OpCodes.Throw);
		}

		private static void CreateArray(ILGenerator il, LocalBuilder arr, int size)
		{
			il.Emit(OpCodes.Ldc_I4, size);
			il.Emit(OpCodes.Newarr, typeof(object));
			il.Emit(OpCodes.Stloc, arr);
		}

		private static void AddToArray(ILGenerator il, LocalBuilder arr, int arrIdx, Action<ILGenerator> pushObjFunc)
		{
			il.Emit(OpCodes.Ldloc, arr);
			il.Emit(OpCodes.Ldc_I4, arrIdx);
			pushObjFunc(il);
			il.Emit(OpCodes.Stelem_Ref);
		}

		private static ModuleBuilder GenerateModule(string moduleName)
		{
			var assemblyName = new AssemblyName(moduleName);
			var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

			var daType = typeof(DebuggableAttribute);
			var daCtor = daType.GetConstructor(new Type[] { typeof(DebuggableAttribute.DebuggingModes) });
			var daBuilder = new CustomAttributeBuilder(daCtor, new object[] { 
				DebuggableAttribute.DebuggingModes.DisableOptimizations | 
				DebuggableAttribute.DebuggingModes.Default });

			assembly.SetCustomAttribute(daBuilder);
			return assembly.DefineDynamicModule(moduleName, true);
		}
	}
}
