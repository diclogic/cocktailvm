using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Cocktail.HTS;
using Cocktail.Interp;
using System.Reflection;

namespace Cocktail
{
	/// <summary>
	/// VM is a special state. It has version. It can only be changed/updated only by deployment system
	/// </summary>
	[StatePermission(Flags = EPermission.Deploy | EPermission.Admin)]
	public class VMState : State
	{
		struct FunctionMetadata
		{
			public string Name;
			public string HostClass;
			public string MethodName;
		}

		private LangVM m_interpreter;

		[StateField(PatchKind = FieldPatchCompatibility.CommutativeDelta)]
		private Dictionary<string, FunctionMetadata> m_functionMetadatas = new Dictionary<string, FunctionMetadata>();

		public VMState( IHTimestamp stamp)
			: base(new TStateId(19830602), stamp.ID, stamp.Event, StatePatchMethod.Customized)
		{
			m_interpreter = new LangVM();	//< we don't really need one interpreter per VMstate
												//< interpreter instances can be shared by copy-on-write
			m_interpreter.DeclareAndLink("Cocktail.DeclareAndLink", typeof(LangVM).GetMethod("DeclareAndLink_cocktail"));

			LoadStdLib();
		}

		public void DeclareAndLink(string name, MethodInfo methodInfo)
		{
			m_interpreter.DeclareAndLink(name, methodInfo);
			RecordMetadata(name, methodInfo);
		}

		public bool IsDeclared(string name)
		{
			return m_interpreter.IsDeclared(name);
		}

		public void Call(IScope scope, string eventName, IEnumerable<KeyValuePair<string, StateRef>> states, IEnumerable<object> constArgs)
		{
			m_interpreter.Call(scope, eventName, states, constArgs);
		}

		protected override StateSnapshot DoSnapshot(StateSnapshot initial)
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

		protected override void DoSerialize(Stream ostream, StateSnapshot oldSnapshot)
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
					HostClass = methodInfo.DeclaringType.AssemblyQualifiedName,
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

		private void LoadStdLib()
		{
			foreach (var m in typeof(StdLib).GetMethods())
			{
				if (!m.IsStatic || !m.IsPublic)
					continue;

				m_interpreter.DeclareAndLink("Cocktail." + m.Name, m);
			}
		}
	}

}
