using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HTS;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Cocktail
{
	public enum StatePatchMethod
	{
		Auto,
		Customized,
	}

	[Flags]
	public enum FieldPatchKind : ushort
	{
		None = 0,
		CommutativeDelta = 0x1,
		Delta = 0x2,
		CommutativeReplace = 0x4,	// for mutable data
		Replace = 0x8,
		All = 0xFFff,
	}

	[Flags]
	public enum StatePatchFlag : ushort
	{
		None = 0,
		CreateBit = 0x1,
		DestroyBit = 0x2,
		CommutativeBit = 0x4,
		ReplaceBit = 0x8,

		Create = CreateBit | ReplaceBit,
		OrderedDelta = 0,
		CommutativeDelta = CommutativeBit,
		OrderedReplace = ReplaceBit,
		CommutativeReplace = ReplaceBit | CommutativeBit,
		Destroy = DestroyBit | ReplaceBit,
	}

	[Serializable]
	public class StateFieldAttribute : Attribute
	{
		public FieldPatchKind PatchKind;
	}

	public class StatePatch
	{
		public byte[] m_data;
		public IHEvent FromRev;
		public IHEvent ToRev;
		public StatePatchFlag Flag;
		public Stream DataStream
		{
			get { return new MemoryStream(m_data); }
		}

		public StatePatch(StatePatchFlag flag, IHEvent fromRev, IHEvent toRev, byte[] data)
		{
			m_data = data;
			FromRev = fromRev;
			ToRev = toRev;
			Flag = flag;
		}
	}

	[Serializable]
	public struct StateCreationHeader
	{
		public string AssemblyQualifiedClassName;

		public StateCreationHeader(Stream istream)
		{
			var reader = new BinaryReader(istream);
			AssemblyQualifiedClassName = reader.ReadString();
		}

		public void Write(Stream ostream)
		{
			var writer = new BinaryWriter(ostream);
			writer.Write(AssemblyQualifiedClassName);
		}
	}

	public class StateSnapshot
	{
		public class FieldEntry
		{
			public string Name;
			public object Value;
			public Type Type;
			public StateFieldAttribute Attrib;
		}

		public TStateId ID;
		public IHTimestamp Timestamp;
		public string TypeName;			// basically the field collection is already a mean of type, we keep the type name just for distinguish
		public List<FieldEntry> Fields = new List<FieldEntry>();

		public StateSnapshot(TStateId id, string typeName, IHTimestamp timestamp)
		{
			ID = id;
			TypeName = typeName;
			Timestamp = timestamp;
		}

	}

	public static class StatePatchingExtension
	{

		public static StatePatchFlag GetPatchFlag(this State lhs)
		{
			if (lhs == null)
				return StatePatchFlag.None;

			var fieldPatchKinds = lhs.GetFields().Select(fi => ((StateFieldAttribute)fi.GetCustomAttributes(typeof(StateFieldAttribute), false).FirstOrDefault()).PatchKind);
			return StatePatcher.GetPatchFlag(fieldPatchKinds);
		}
	}

	public static class StatePatcher
	{
		private struct FieldPair
		{
			public string Name;
			public Type Type;
			public StateFieldAttribute Attrib;
			public object newVal;
			public object oldVal;
		}

		public static StatePatch GenerateDestroyPatch(IHEvent expectingEvent, IHEvent oriEvent)
		{
			var retval = new StatePatch(StatePatchFlag.Destroy, oriEvent, expectingEvent, new byte[0]);
			return retval;
		}

		public static StatePatch GenerateCreatePatch(this StateSnapshot newState, IHEvent originalEvent)
		{
			var pseudoOld = new StateSnapshot(newState.ID, newState.TypeName, HTSFactory.Make(newState.Timestamp.ID, originalEvent));
			var ostream = new MemoryStream();
			StateCreationHeader header;
			header.AssemblyQualifiedClassName = Assembly.CreateQualifiedName(Assembly.GetAssembly(Type.GetType(newState.TypeName)).FullName, newState.TypeName);
			header.Write(ostream);
			GeneratePatch(ostream, newState, pseudoOld, FieldPatchKind.Replace);

			var retval = new StatePatch(StatePatchFlag.Create, pseudoOld.Timestamp.Event,
										 newState.Timestamp.Event,
										 ostream.ToArray());
			return retval;
		}

		public static StatePatchFlag GetPatchFlag(this StateSnapshot snapshot)
		{
			return GetPatchFlag(snapshot.Fields.Select(field => field.Attrib.PatchKind));
		}

		public static StatePatchFlag GetPatchFlag(IEnumerable<FieldPatchKind> fieldPatchKinds)
		{
			return fieldPatchKinds.Aggregate(StatePatchFlag.None,(accu, elem) =>
				{
				if (0 != (elem & (FieldPatchKind.CommutativeDelta | FieldPatchKind.CommutativeReplace)))
					accu |= StatePatchFlag.CommutativeBit;

				if (0 != (elem & (FieldPatchKind.Replace | FieldPatchKind.CommutativeReplace)))
					accu |= StatePatchFlag.ReplaceBit;
				return accu;
				});
		}

		public static StatePatch GeneratePatch(this StateSnapshot newState, StateSnapshot oldState, FieldPatchKind? forceKind)
		{
			var ostream = new MemoryStream();
			GeneratePatch(ostream, newState, oldState, forceKind);
			var retval = new StatePatch( GetPatchFlag(newState),
							 oldState.Timestamp.Event,
							 newState.Timestamp.Event,
							 ostream.ToArray());
			return retval;
		}

		public static void GeneratePatch(Stream ostream, StateSnapshot newState, IHEvent originalEvent)
		{
			var pseudoOld = new StateSnapshot(newState.ID, newState.TypeName, HTSFactory.Make(newState.Timestamp.ID, originalEvent));
			GeneratePatch(ostream, newState, pseudoOld, null);
		}

		public static void GeneratePatch(Stream ostream, StateSnapshot newState, StateSnapshot oldState, FieldPatchKind? forceKind)
		{
			if (oldState.TypeName != newState.TypeName)
				throw new ApplicationException("Mismatch between the type of new and old States");

			var writer = new BinaryWriter(ostream);
			var fpairs = from f1 in newState.Fields
						 join f2 in oldState.Fields on f1.Name equals f2.Name
						 select new FieldPair() { Name = f1.Name, Type = f1.Type, Attrib = f1.Attrib, newVal = f1.Value, oldVal = f2.Value };
			foreach (var fp in fpairs)
			{
				writer.Write(fp.Name);
				var patchKind = forceKind.HasValue ? forceKind.Value : fp.Attrib.PatchKind;
				writer.Write((ushort)patchKind);
				SerializeField(writer, fp, forceKind);
			}
		}

		public static void PatchState(Stream istream, State state)
		{
			var reader = new BinaryReader(istream);
				while (reader.PeekChar() != -1)
				{
					DeserializeField(reader, state);
				}
		}

		public static bool TryCreateFromPatch(IHId hostST, TStateId stateId, StatePatch patch, out State created)
		{
			if (patch.Flag != StatePatchFlag.Create)
			{
				created = null;
				return false;
			}

			var header = new StateCreationHeader(patch.DataStream);
			var type = Type.GetType(header.AssemblyQualifiedClassName);
			// newly created state must start from "FromRev"
			created = (State)Activator.CreateInstance(type, stateId, (Spacetime)null, HTSFactory.Make(hostST, patch.FromRev));
			return true;
		}

		private static void SerializeField(BinaryWriter writer, FieldPair fpair, FieldPatchKind? forceKind = null)
		{
			switch (forceKind.HasValue ? forceKind.Value : fpair.Attrib.PatchKind)
			{
				case FieldPatchKind.CommutativeDelta:
				case FieldPatchKind.Delta:
					{
						Action<BinaryWriter, object, object> func;
						if (!m_fieldDiffSerializers.TryGetValue(fpair.Type, out func))
							throw new ApplicationException(string.Format("Unsupported State Field type: {0}", fpair.Type.FullName));
						func(writer, fpair.newVal, fpair.oldVal);
					}
					break;
				case FieldPatchKind.CommutativeReplace:
				case FieldPatchKind.Replace:
					{
						Action<BinaryWriter, object> func;
						if (!m_fieldSerializers.TryGetValue(fpair.Type, out func))
							throw new ApplicationException(string.Format("Unsupported State Field type: {0}", fpair.Type.FullName));
						func(writer, fpair.newVal);
					}
					break;
			}
		}

		private static void DeserializeField(BinaryReader reader, State state)
		{
			var fieldName = reader.ReadString();
			FieldPatchKind patchKind = (FieldPatchKind)reader.ReadInt16();

			var fi = state.GetType().GetField(fieldName);
			var host = state;
			switch (patchKind)
			{
				case FieldPatchKind.CommutativeDelta:
				case FieldPatchKind.Delta:
					{
						Action<BinaryReader, FieldInfo, object> func;
						if (!m_fieldDeserializers.TryGetValue(fi.FieldType, out func))
							throw new ApplicationException(string.Format("Unsupported State Field type: {0}", fi.FieldType.FullName));
						func(reader, fi, host);
					}
					break;
				case FieldPatchKind.CommutativeReplace:
				case FieldPatchKind.Replace:
					{
						Action<BinaryReader, FieldInfo, object> func;
						if (!m_fieldDiffDeserializers.TryGetValue(fi.FieldType, out func))
							throw new ApplicationException(string.Format("Unsupported State Field type: {0}", fi.FieldType.FullName));
						func(reader, fi, host);
					}
					break;
			}

		}

		private static Dictionary<Type, Action<BinaryWriter, object>> m_fieldSerializers
			= new Dictionary<Type, Action<BinaryWriter, object>>()
			{
				{ typeof(float), (w,obj) => w.Write((float)obj) }
				,{ typeof(int), (w,obj)=> w.Write((int)obj) }
			};

		private static Dictionary<Type, Action<BinaryWriter, object, object>> m_fieldDiffSerializers
			= new Dictionary<Type, Action<BinaryWriter, object, object>>()
			{
				{ typeof(float), (w,objNew, objOld) => w.Write((float)objNew - (float)objOld)}
				,{ typeof(int), (w,objNew, objOld) => w.Write((int)objNew - (int)objOld)}
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
