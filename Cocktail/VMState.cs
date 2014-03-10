using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HTS;
using System.Reflection;

namespace Cocktail
{
	/// <summary>
	/// VM is a special state. It has version. It can only be changed/updated only by deployment system
	/// </summary>
	public class VMState : State
	{
		struct FunctionMetadata
		{
			public string Name;
			public string HostClass;
			public string MethodName;
		}

		private Interpreter m_interpreter;
		[StateField(PatchKind = FieldPatchKind.CommutativeDelta)]
		private Dictionary<string, FunctionMetadata> m_functionMetadatas = new Dictionary<string, FunctionMetadata>();

		public VMState(Spacetime st, IHTimestamp stamp)
			: base(new TStateId(19830602), st, stamp, StatePatchMethod.Customized)
		{
			m_interpreter = new Interpreter();
			m_interpreter.DeclareAndLink("Cocktail.DeclareAndLink", typeof(Interpreter).GetMethod("DeclareAndLink_cocktail"));
		}

		public void DeclareAndLink(string name, MethodInfo methodInfo)
		{
			m_interpreter.DeclareAndLink(name, methodInfo);
			RecordMetadata(name, methodInfo);
		}

		public void Call(string eventName, IEnumerable<KeyValuePair<string, StateRef>> states, IEnumerable<object> constArgs)
		{
			m_interpreter.Call(eventName, states, constArgs);
		}

		public override StateSnapshot DoSnapshot(StateSnapshot initial)
		{
			var retval = initial;
			var entry = new StateSnapshot.FieldEntry();
			entry.Name = "m_functionMetadatas";
			entry.Type = m_functionMetadatas.GetType();
			entry.Attrib = null;
			entry.Value = m_functionMetadatas.ToDictionary(kv => kv.Key, kv => kv.Value);
			retval.Fields.Add(entry);
			return retval;
		}

		public override void DoSerialize(Stream ostream, StateSnapshot oldSnapshot)
		{
			var oldDict = (Dictionary<string,FunctionMetadata>)oldSnapshot.Fields.First(field => field.Name == "m_functionMetadatas").Value;
			foreach (var k in m_functionMetadatas.Keys.Except(oldDict.Keys))
			{
				var newEntry = m_functionMetadatas[k];

				var writer = new BinaryWriter(ostream);
				writer.Write((char)0xD9);
				writer.Write(newEntry.Name);
				writer.Write(newEntry.HostClass);
				writer.Write(newEntry.MethodName);
			}
		}

		protected override bool DoPatch(Stream delta)
		{
			var newEntries = new List<FunctionMetadata>();
			var reader = new BinaryReader(delta);
			while (reader.PeekChar() != -1)
			{
				if (reader.PeekChar() != (char)0xD9)
					return false;

				reader.ReadChar();
				var name = reader.ReadString();
				var className = reader.ReadString();
				var methodName = reader.ReadString();
				newEntries.Add(new FunctionMetadata()
					{
						Name = name,
						HostClass = className,
						MethodName = methodName
					});
			}

			RestoreFromMetadata(newEntries);

			foreach (var entry in newEntries)
				m_functionMetadatas.Add(entry.Name, entry);

			return true;
		}

		private void RecordMetadata(string name, MethodInfo methodInfo)
		{
			m_functionMetadatas.Add(name, new FunctionMetadata()
				{
					Name = name,
					HostClass = methodInfo.DeclaringType.FullName,
					MethodName = methodInfo.Name
				});
		}

		private void RestoreFromMetadata(IEnumerable<FunctionMetadata> newEntries)
		{
			foreach (var entry in newEntries)
			{
				var methodInfo = Type.GetType(entry.HostClass).GetMethod(entry.MethodName);
				m_interpreter.DeclareAndLink(entry.Name, methodInfo);
			}
		}
	}

	public class VMSpacetime : Spacetime
	{
		public readonly TStateId VMStateId;

		public VMSpacetime(IHIdFactory idFactory)
			:base(idFactory.CreateFromRoot(), HTSFactory.CreateZeroEvent(), idFactory)
		{
			VMStateId = m_vm.StateId;
			m_nativeStates.Add(m_vm.StateId, m_vm);
			DOA.NamingSvcClient.Instance.RegisterObject(VMStateId.ToString(),m_vm.GetType().FullName, m_vm);
		}

		public void VMExecute(string funcName, params object[] constArgs)
		{
			VMExecuteArgs(funcName, constArgs);
		}
		public void VMExecuteArgs(string funcName, IEnumerable<object> constArgs)
		{
			ExecuteArgs(funcName
				, Enumerable.Repeat(new KeyValuePair<string, StateRef>("VM", new LocalStateRef<VMState>(m_vm)), 1)
				, constArgs);
		}
	}

}
