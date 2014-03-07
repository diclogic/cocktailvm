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

	public class StateFieldAttribute : Attribute
	{
		public FieldPatchKind PatchKind;
	}

	public class StatePatch
	{
		public IHEvent FromRev;
		public IHEvent ToRev;
		public StatePatchFlag Flag = StatePatchFlag.CommutativeDelta;
		public Stream data;
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

	public static class StateExtension_Snapshot
	{
		public static StateSnapshot GetSnapshot(this State lhs) { return GetSnapshot(lhs, lhs.LatestUpdate); }
		public static StateSnapshot GetSnapshot(this State lhs, IHEvent overridingEvent)
		{
			return GetSnapshot(lhs, HTSFactory.Make(lhs.LatestUpdate.ID, overridingEvent));
		}
		public static StateSnapshot GetSnapshot(this State lhs, IHTimestamp overridingTS)
		{
			var retval = new StateSnapshot(lhs.StateId, lhs.GetType().FullName, overridingTS);

			foreach (var fi in lhs.GetFields())
			{
				var fval = fi.GetValue(lhs);
				retval.Fields.Add(new StateSnapshot.FieldEntry() {
					Name = fi.Name,
					Value = fval,
					Type = fi.FieldType,
					Attrib = fi.GetCustomAttributes(typeof(StateFieldAttribute), false).FirstOrDefault() as StateFieldAttribute
				});
			}
			return retval;
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
			var retval = new StatePatch();
			retval.FromRev = oriEvent;
			retval.ToRev = expectingEvent;
			retval.Flag = StatePatchFlag.Destroy;
			return retval;
		}

		public static StatePatch GenerateCreatePatch(this StateSnapshot newState, IHEvent originalEvent)
		{
			var pseudoOld = new StateSnapshot(newState.ID, newState.TypeName, HTSFactory.Make(newState.Timestamp.ID, originalEvent));
			var retval = GeneratePatch(newState, pseudoOld, FieldPatchKind.Replace);
			retval.Flag = StatePatchFlag.Create;
			return retval;
		}

		public static StatePatch GeneratePatch(this StateSnapshot newState, StateSnapshot oldState, FieldPatchKind? forceKind)
		{
			var ostream = new MemoryStream();
			GeneratePatch(ostream, newState, oldState, forceKind);
			var retval = new StatePatch();
			StatePatchFlag flag = StatePatchFlag.None;
			foreach (var f in newState.Fields)
			{
				if (0 != (f.Attrib.PatchKind & (FieldPatchKind.CommutativeDelta | FieldPatchKind.CommutativeReplace)))
					flag |= StatePatchFlag.CommutativeBit;

				if (0 != (f.Attrib.PatchKind & (FieldPatchKind.Replace | FieldPatchKind.CommutativeReplace)))
					flag |= StatePatchFlag.ReplaceBit;
			}
			retval.Flag = flag;
			retval.FromRev = oldState.Timestamp.Event;
			retval.ToRev = newState.Timestamp.Event;
			retval.data = ostream;
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

			using (var writer = new BinaryWriter(ostream))
			{
				var fpairs = from f1 in newState.Fields join f2 in oldState.Fields on f1.Name equals f2.Name
							 select new FieldPair(){ Name = f1.Name, Attrib = f1.Attrib, newVal = f1.Value, oldVal = f2.Value };
				foreach (var fp in fpairs)
				{
					writer.Write(fp.Name);
					var patchKind = forceKind.HasValue ? forceKind.Value : fp.Attrib.PatchKind;
					writer.Write((ushort)patchKind);
					SerializeField(writer, fp, forceKind);
				}
			}
		}

		public static void PatchState(Stream istream, State state)
		{
			using (var reader = new BinaryReader(istream))
			{
				while (reader.PeekChar() != -1)
				{
					DeserializeField(reader, state);
				}
			}
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
