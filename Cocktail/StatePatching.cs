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
		CommutitaiveReplace = 0x4,	// for mutable data
		Replace = 0x8,
		All = 0xFFff,
	}

	public class StateFieldAttribute : Attribute
	{
		public FieldPatchKind PatchKind;
	}

	public class StatePatch
	{
		public enum EFlag {Delta, Create, Destroy};
		public IHierarchicalEvent FromRev;
		public IHierarchicalEvent ToRev;
		public EFlag Flag = EFlag.Delta;
		public Stream delta;
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
		public IHierarchicalTimestamp Timestamp;
		public string TypeName;			// basically the field collection is already a mean of type, we keep the type name just for distinguish
		public List<FieldEntry> Fields = new List<FieldEntry>();

		public StateSnapshot(TStateId id, string typeName, IHierarchicalTimestamp timestamp)
		{
			ID = id;
			TypeName = typeName;
			Timestamp = timestamp;
		}

	}

	public static class StateExtension_Snapshot
	{
		public static StateSnapshot GetSnapshot(this State lhs) { return GetSnapshot(lhs, lhs.LatestUpdate); }
		public static StateSnapshot GetSnapshot(this State lhs, IHierarchicalEvent overridingEvent)
		{
			return GetSnapshot(lhs, HTSFactory.Make(lhs.LatestUpdate.ID, overridingEvent));
		}
		public static StateSnapshot GetSnapshot(this State lhs, IHierarchicalTimestamp overridingTS)
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

		public static StatePatch GeneratePatch(IHierarchicalEvent expectingEvent, StateSnapshot oldState)
		{
			var retval = new StatePatch();
			retval.FromRev = oldState.Timestamp.Event;
			retval.ToRev = expectingEvent;
			retval.Flag = StatePatch.EFlag.Destroy;
			return retval;
		}

		public static StatePatch GeneratePatch(StateSnapshot newState, IHierarchicalEvent originalEvent)
		{
			var pseudoOld = new StateSnapshot(newState.ID, newState.TypeName, HTSFactory.Make(newState.Timestamp.ID, originalEvent));
			var retval = GeneratePatch(newState, pseudoOld);
			retval.Flag = StatePatch.EFlag.Create;
			return retval;
		}

		public static StatePatch GeneratePatch(StateSnapshot newState, StateSnapshot oldState)
		{
			var ostream = new MemoryStream();
			GeneratePatch(ostream, newState, oldState);
			var retval = new StatePatch();
			retval.FromRev = oldState.Timestamp.Event;
			retval.ToRev = newState.Timestamp.Event;
			retval.delta = ostream;
			return retval;
		}

		public static void GeneratePatch(Stream ostream, StateSnapshot newState, IHierarchicalEvent originalEvent)
		{
			var pseudoOld = new StateSnapshot(newState.ID, newState.TypeName, HTSFactory.Make(newState.Timestamp.ID, originalEvent));
			GeneratePatch(ostream, newState, pseudoOld);
		}
		public static void GeneratePatch(Stream ostream, StateSnapshot newState, StateSnapshot oldState)
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
					writer.Write((ushort)fp.Attrib.PatchKind);
					SerializeField(writer, fp);
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

		private static void SerializeField(BinaryWriter writer, FieldPair fpair)
		{
			switch (fpair.Attrib.PatchKind)
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
