using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HTS;
using System.Reflection;

namespace Cocktail
{
	public enum StatePatchMethod
	{
		Auto,
		Customized,
	}

	public enum FieldPatchKind
	{
		None,
		CommutativeDelta,
		Delta,
		Replace,
	}

	public class StateFieldAttribute : Attribute
	{
		public FieldPatchKind PatchKind;
	}

	public class StatePatch
	{
		public IHierarchicalEvent FromRev;
		public IHierarchicalEvent ToRev;
		public Stream delta;
	}

	public static class StatePatcher
	{
		public static void GeneratePatch(Stream ostream, State newState, State oldState)
		{
			var type = newState.GetType();
			if (oldState.GetType() != type)
				throw new ApplicationException("Mismatch between the type of new and old States");

			using (var writer = new BinaryWriter(ostream))
			{
				var fields = type.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (var f in fields)
				{
					var attr = f.GetCustomAttributes(typeof(StateFieldAttribute), false).FirstOrDefault() as StateFieldAttribute;
					if (attr == null)
						continue;
					writer.Write(f.Name);
					writer.Write((Int16)attr.PatchKind);
					switch (attr.PatchKind)
					{
						case FieldPatchKind.CommutativeDelta:
						case FieldPatchKind.Delta:
							SerializeField(writer, f, newState, oldState);
							break;
						case FieldPatchKind.Replace:
							SerializeField(writer, f, newState);
							break;
					}
				}
			}
		}

		public static void PatchState(Stream istream, State state)
		{
			using (var reader = new BinaryReader(istream))
			{
				while (reader.PeekChar() != -1)
				{
					var fieldName = reader.ReadString();
					FieldPatchKind patchKind = (FieldPatchKind)reader.ReadInt16();

					var fieldInfo = state.GetType().GetField(fieldName);
					switch (patchKind)
					{
						case FieldPatchKind.Delta:
							DeserializeFieldDiff(reader, fieldInfo, state);
							break;
						case FieldPatchKind.Replace:
							DeserializeField(reader, fieldInfo, state);
							break;
					}
				}
			}
		}

		private static void SerializeField(BinaryWriter writer, FieldInfo fi, object newObj, object oldObj)
		{
			Action<BinaryWriter, FieldInfo, object,object> func;
			if (!m_fieldDiffSerializers.TryGetValue(fi.FieldType, out func))
				throw new ApplicationException(string.Format("Unsupported State Field type: {0}", fi.FieldType.FullName));
			func(writer, fi, newObj, oldObj);
		}

		private static void SerializeField(BinaryWriter writer, FieldInfo fi, object obj)
		{
			Action<BinaryWriter, FieldInfo, object> func;
			if (!m_fieldSerializers.TryGetValue(fi.FieldType, out func))
				throw new ApplicationException(string.Format("Unsupported State Field type: {0}", fi.FieldType.FullName));
			func(writer, fi, obj);
		}

		private static void DeserializeField(BinaryReader reader, FieldInfo fi, object obj)
		{
			Action<BinaryReader, FieldInfo, object> func;
			if (!m_fieldDeserializers.TryGetValue(fi.FieldType, out func))
				throw new ApplicationException(string.Format("Unsupported State Field type: {0}", fi.FieldType.FullName));
			func(reader, fi, obj);
		}

		private static void DeserializeFieldDiff(BinaryReader reader, FieldInfo fi, object obj)
		{
			Action<BinaryReader, FieldInfo, object> func;
			if (!m_fieldDiffDeserializers.TryGetValue(fi.FieldType, out func))
				throw new ApplicationException(string.Format("Unsupported State Field type: {0}", fi.FieldType.FullName));
			func(reader, fi, obj);
		}

		private static Dictionary<Type, Action<BinaryWriter, FieldInfo, object>> m_fieldSerializers
			= new Dictionary<Type, Action<BinaryWriter, FieldInfo, object>>()
			{
				{ typeof(float), (w,fi,obj) => w.Write((float)fi.GetValue(obj)) }
				,{typeof(int), (w,fi,obj)=> w.Write((int)fi.GetValue(obj))}
			};

		private static Dictionary<Type, Action<BinaryWriter, FieldInfo, object, object>> m_fieldDiffSerializers
			= new Dictionary<Type, Action<BinaryWriter, FieldInfo, object, object>>()
			{
				{ typeof(float), (w,fi,objNew, objOld) => w.Write((float)fi.GetValue(objNew) - (float)fi.GetValue(objOld))}
				,{ typeof(int), (w,fi,objNew, objOld) => w.Write((int)fi.GetValue(objNew) - (int)fi.GetValue(objOld))}
			};

		private static Dictionary<Type, Action<BinaryReader, FieldInfo, object>> m_fieldDeserializers
			= new Dictionary<Type, Action<BinaryReader, FieldInfo, object>>()
			{
				{ typeof(float), (r,fi,obj) => fi.SetValue(obj, r.ReadSingle()) }
				,{ typeof(int), (r,fi,obj) => fi.SetValue(obj, r.ReadInt32()) }
			};

		private static Dictionary<Type, Action<BinaryReader, FieldInfo, object>> m_fieldDiffDeserializers
			= new Dictionary<Type, Action<BinaryReader, FieldInfo, object>>()
			{
				{ typeof(float), (r,fi,obj) => fi.SetValue(obj, (float)fi.GetValue(obj) + r.ReadSingle()) }
				,{ typeof(int), (r,fi,obj) => fi.SetValue(obj, (int)fi.GetValue(obj) + r.ReadInt32()) }
			};
	}
}
