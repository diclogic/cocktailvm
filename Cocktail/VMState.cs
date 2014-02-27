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
		private Interpreter m_interpreter;
		private Stream m_patchStream;

		public VMState(Spacetime st, IHierarchicalTimestamp stamp)
			:base(st,stamp)
		{
			m_interpreter = new Interpreter();
			m_interpreter.DeclareAndLink("Cocktail.DeclareAndLink", typeof(Interpreter).GetMethod("DeclareAndLink_cocktail"));
			m_patchStream = new MemoryStream();
		}

		public void DeclareAndLink(string name, MethodInfo methodInfo)
		{
			m_interpreter.DeclareAndLink(name, methodInfo);

			SerializeDnL(m_patchStream, name, methodInfo);
		}

		public void Call(string eventName, IEnumerable<KeyValuePair<string, StateRef>> states, IEnumerable<object> constArgs)
		{
			m_interpreter.Call(eventName, states, constArgs);
		}

		protected override bool DoPatch(Stream delta)
		{
			TryDeserializeDnL(delta);
			return true;
		}

		private Stream SerializeDnL(Stream ostream, string name, MethodInfo methodInfo)
		{
			var retval = ostream;
			using(var writer = new BinaryWriter(retval))
			{
				writer.Write((char)0xD9);
				writer.Write(name);
				writer.Write(methodInfo.DeclaringType.FullName);
				writer.Write(methodInfo.Name);
			}
			return retval;
		}

		private bool TryDeserializeDnL(Stream delta)
		{
			using (var reader = new BinaryReader(delta))
			{
				if (reader.PeekChar() != (char)0xD9)
					return false;

				reader.ReadChar();
				var name = reader.ReadString();
				var className = reader.ReadString();
				var methodName = reader.ReadString();

				var methodInfo = Type.GetType(className).GetMethod(methodName);

				m_interpreter.DeclareAndLink(name, methodInfo);
			}
			return true;
		}
	}

}
