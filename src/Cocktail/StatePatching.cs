using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Cocktail.HTS;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Cocktail
{
	public enum StatePatchMethod
	{
		Auto,
		Customized,		// the state defines how it should be patched
	}

	[Flags]
	public enum FieldPatchCompatibility : ushort
	{
		Invalid = 0,
		CommutativeDelta = 0x1,
		CommutativeSwap = 0x2,	// for mutable data
		Swap = 0x4,
		All = 0xFFff,
	}

	[Flags]
	public enum PatchFlag : ushort
	{
		None = 0,
		CreateBit = 0x1,
		DestroyBit = 0x2,
		CommutativeBit = 0x4,
		SwapBit = 0x8,		//< update by replacing
		DistributedBit = 0x10,

		Invalid = 0,	//< it also means Ordered Delta but that's a useless case
		Create = PatchFlag.CreateBit | PatchFlag.SwapBit,
		CommutativeDelta = PatchFlag.CommutativeBit,
		DistributedCommutativeDelta = PatchFlag.DistributedBit | PatchFlag.CommutativeBit,
		OrderedSwap = PatchFlag.SwapBit,
		CommutativeSwap = PatchFlag.SwapBit | PatchFlag.CommutativeBit,
		Destroy = PatchFlag.DestroyBit | PatchFlag.SwapBit,
	}

	[Serializable]
	public class StateFieldAttribute : Attribute
	{
		public FieldPatchCompatibility PatchKind;
	}

	public class MetaStatePatch
	{
		public IHEvent FromEvent;
		public IHEvent ToEvent;
		public PatchFlag Flag;
		public long ToRev;
	}

	public class StatePatch : MetaStatePatch
	{
		private byte[] m_data;
		public Stream CreateReadStream()
		{
			return new MemoryStream(m_data, false);
		}

		public StatePatch(PatchFlag flag, IHEvent fromRev, IHEvent toRev, byte[] data)
		{
			m_data = data;
			FromEvent = fromRev;
			ToEvent = toRev;
			Flag = flag;
		}
	}

	public class StatePatchingCtx
	{
		public MetaStatePatch Metadata;
		public Stream DataStream;

		public StatePatchingCtx(StatePatch patch)
		{
			Metadata = patch;
			DataStream = patch.CreateReadStream();
		}
	}

	[Serializable]
	public struct StateCreationHeader
	{
		public string AssemblyQualifiedClassName;

		public StateCreationHeader(Type stateType)
		{
			AssemblyQualifiedClassName = 
				Assembly.CreateQualifiedName(Assembly.GetAssembly(stateType).FullName, stateType.FullName);
		}

		public StateCreationHeader(Stream istream)
		{
			var reader = new BinaryReader(istream);
			AssemblyQualifiedClassName = reader.ReadString();
		}

		public void WriteTo(Stream ostream)
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

		public class FieldEntryEqualityComparer : IEqualityComparer<FieldEntry>
		{
			public bool Equals(FieldEntry x, FieldEntry y) { return x.Name == y.Name; }
			public int GetHashCode(FieldEntry obj) { return obj.Name.GetHashCode(); }
		}

		public TStateId ID;
		public IHTimestamp Timestamp;
		public long Rev;
		public string TypeName;			// basically the field collection is already a mean of type, we keep the type name just for distinguish
		public List<FieldEntry> Fields = new List<FieldEntry>();

		public StateSnapshot(TStateId id, string typeName, IHTimestamp timestamp, long rev)
		{
			ID = id;
			TypeName = typeName;
			Timestamp = timestamp;
			Rev = rev;
		}

		public static StateSnapshot CreateNull(TStateId id)
		{
			return new StateSnapshot(id, string.Empty, HTSFactory.Null, 0);
		}
	}

	public static class StatePatchingExtension
	{

		public static PatchFlag GetPatchFlag(this State lhs)
		{
			if (lhs == null)
				return PatchFlag.Invalid;

			var fieldPatchKinds = lhs.GetFields().Select(fi => ((StateFieldAttribute)fi.GetCustomAttributes(typeof(StateFieldAttribute), false).FirstOrDefault()).PatchKind);
			return StatePatchUtils.FindPatchMethod(fieldPatchKinds);
		}
	}

	public class PatchException : RuntimeException
	{
		public PatchException(string reason) :base(reason) { }
	}

	public static class StatePatchUtils
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
			var retval = new StatePatch(PatchFlag.Destroy, oriEvent, expectingEvent, new byte[0]);
			return retval;
		}

		public static StatePatch GenerateCreatePatch(this StateSnapshot newState, IHEvent originalEvent)
		{
			var flag = newState.FindPatchMethod() | PatchFlag.Create;

			var pseudoOld = new StateSnapshot(newState.ID, newState.TypeName, HTSFactory.Make(newState.Timestamp.ID, originalEvent), -1);
			var ostream = new MemoryStream();
			GeneratePatch(ostream, newState, pseudoOld, flag);

			var retval = new StatePatch(flag, pseudoOld.Timestamp.Event,
										 newState.Timestamp.Event,
										 ostream.ToArray());
			return retval;
		}

		public static bool TryCreateFromPatch(IHId hostST, TStateId stateId, StatePatchingCtx patchCtx, out State created)
		{
			// nothing to create
			if (0 == (patchCtx.Metadata.Flag & (PatchFlag.CreateBit | PatchFlag.CommutativeBit)))
			{
				created = null;
				return false;
			}

			var header = new StateCreationHeader(patchCtx.DataStream);
			var type = Type.GetType(header.AssemblyQualifiedClassName);
			// newly created state must start from "FromRev"
			created = (State)Activator.CreateInstance(type, stateId, HTSFactory.Make(hostST, patchCtx.Metadata.FromEvent));
			return true;
		}

		public static PatchFlag FindPatchMethod(this StateSnapshot snapshot)
		{
			return FindPatchMethod(snapshot.Fields.Select(field => field.Attrib.PatchKind));
		}

		public static PatchFlag FindPatchMethod(IEnumerable<FieldPatchCompatibility> fieldPatchKinds)
		{
			var iniVal = PatchFlag.None | PatchFlag.CommutativeBit;
			var actionBits = fieldPatchKinds.Aggregate(iniVal, (accu, elem) =>
				{
					// if anything is non-commutative, the state is non-commutative
					if (0 == (elem & (FieldPatchCompatibility.CommutativeDelta | FieldPatchCompatibility.CommutativeSwap)))
					{
						accu &= ~PatchFlag.CommutativeBit;
					}

					// if anything is swap, then it's a swap
					if (0 != (elem & (FieldPatchCompatibility.Swap | FieldPatchCompatibility.CommutativeSwap)))
					{
						accu |= PatchFlag.SwapBit;
					}
					return accu;
				});

			return (PatchFlag)actionBits;
		}

		// not needed
		public static FieldPatchCompatibility CalcFieldPatchMethod(PatchFlag stateFlag, FieldPatchCompatibility patchKind)
		{
			if (0 == (stateFlag & PatchFlag.CommutativeBit))
			{
				patchKind &= ~FieldPatchCompatibility.CommutativeDelta;
				patchKind &= ~FieldPatchCompatibility.CommutativeSwap;
			}

			if (0 != (stateFlag & PatchFlag.SwapBit))
			{
				patchKind &= ~FieldPatchCompatibility.CommutativeDelta;
			}

			return patchKind;
		}

		public static StatePatch GeneratePatch(this StateSnapshot newState, StateSnapshot oldState, PatchFlag? overridingFlag)
		{
			var flag = overridingFlag.HasValue ? overridingFlag.Value : newState.FindPatchMethod();

			var ostream = new MemoryStream();
			GeneratePatch(ostream, newState, oldState, flag);
			var retval = new StatePatch(flag,
							 oldState.Timestamp.Event,
							 newState.Timestamp.Event,
							 ostream.ToArray());
			return retval;
		}

		// Deprecated
		//public static void GeneratePatch(Stream ostream, StateSnapshot newState, IHEvent originalEvent)
		//{
		//    var pseudoOld = new StateSnapshot(newState.ID, newState.TypeName, HTSFactory.Make(newState.Timestamp.ID, originalEvent));
		//    GeneratePatch(ostream, newState, pseudoOld, null);
		//}

		public static void GeneratePatch(Stream ostream, StateSnapshot newState, StateSnapshot oldState, PatchFlag? overridingFlag)
		{
			if (oldState.TypeName != newState.TypeName)
				throw new ApplicationException("Mismatch between the type of new and old States");

			var flag = overridingFlag.HasValue ? overridingFlag.Value : newState.FindPatchMethod();

			// TODO: since now every patch can potentially create the new state, we should do it smartly (registered type or self-descriptive dynamic type)
			//// Every commutative delta has the potential to create a new object, so we need the type
			//if (0 != (flag & (PatchFlag.CreateBit | PatchFlag.CommutativeBit)))
			//{
			//    var header = new StateCreationHeader(Type.GetType(newState.TypeName));
			//    header.WriteTo(ostream);
			//}

			var fpairs = from f1 in newState.Fields
						 join f2 in oldState.Fields on f1.Name equals f2.Name
						 select new FieldPair() { Name = f1.Name, Type = f1.Type, Attrib = f1.Attrib, newVal = f1.Value, oldVal = f2.Value };

			var writer = new BinaryWriter(ostream);
			foreach (var fp in fpairs)
			{
				SerializeField(writer, fp, flag);
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

		private static void SerializeField(BinaryWriter writer, FieldPair fpair, PatchFlag flag)
		{
			writer.Write(fpair.Name);
			writer.Write((ushort)(flag & ~(PatchFlag.CreateBit | PatchFlag.DestroyBit)));

			if (0 != (flag & PatchFlag.SwapBit))
			{
				Action<BinaryWriter, object> func;
				if (!m_fieldSerializers.TryGetValue(fpair.Type, out func))
					throw new ApplicationException(string.Format("Unsupported State Field type: {0}", fpair.Type.FullName));
				func(writer, fpair.newVal);
			}
			else
			{
				Action<BinaryWriter, object, object> func;
				if (!m_fieldDiffSerializers.TryGetValue(fpair.Type, out func))
					throw new ApplicationException(string.Format("Unsupported State Field type: {0}", fpair.Type.FullName));
				func(writer, fpair.newVal, fpair.oldVal);
			}
		}

		private static void DeserializeField(BinaryReader reader, State state)
		{
			var fieldName = reader.ReadString();
			var patchKind = (PatchFlag)reader.ReadInt16();

			var fi = state.GetType().GetField(fieldName);
			var host = state;

			if (0 != (patchKind & PatchFlag.SwapBit))
			{
				Action<BinaryReader, FieldInfo, object> func;
				if (!m_fieldDeserializers.TryGetValue(fi.FieldType, out func))
					throw new ApplicationException(string.Format("Unsupported State Field type: {0}", fi.FieldType.FullName));
				func(reader, fi, host);
			}
			else
			{
				Action<BinaryReader, FieldInfo, object> func;
				if (!m_fieldDiffDeserializers.TryGetValue(fi.FieldType, out func))
					throw new ApplicationException(string.Format("Unsupported State Field type: {0}", fi.FieldType.FullName));
				func(reader, fi, host);
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
