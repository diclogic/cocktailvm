using System;
using System.IO;
using System.Linq;
using HTS;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Reflection;

namespace Cocktail
{


	public class StateAttribute : Attribute { }

	public struct TStateId : IComparable, IComparable<TStateId>, IEquatable<TStateId>
	{
		private ulong m_val;

		public TStateId(Random seed)
		{
			ulong highBits = ((ulong)seed.Next()) << 32;
			m_val = (ulong)(uint)seed.Next() | highBits;
		}

		internal TStateId(ulong val)
		{
			m_val = val;
		}

		public static TStateId DebugCreate(ulong val)
		{
			TStateId retval;
			retval.m_val = val;
			return retval;
		}

		public int CompareTo(object rhs)
		{
			if (rhs.GetType().Equals(this))
				return CompareTo((TStateId)rhs);
			return 1;	// null is small than any value
		}

		public int CompareTo(TStateId rhs)
		{
			return m_val.CompareTo(rhs.m_val);
		}

		public bool Equals(TStateId rhs)
		{
			return m_val.Equals(rhs.m_val);
		}

		public bool IsNull()
		{
			return m_val == 0;
		}

		public override string ToString()
		{
			return m_val.ToString();
		}
	}

	public abstract class State //: ICloneable
	{
		static Random m_seed = new Random();

		private StatePatch m_pendingPatch;
		private readonly StatePatchMethod m_patchMethod;



		public TStateId StateId { get; protected set; }	//< to identify a state
		public IHId SpacetimeID { get; private set; }
		public IHEvent LatestUpdate { get; private set; }
		public long Rev { get; private set; }

		public State(IHTimestamp stamp)
			: this(stamp, StatePatchMethod.Auto)
		{
		}

		public State(IHTimestamp stamp, StatePatchMethod patchMethod)
			: this(new TStateId(m_seed), stamp.ID, stamp.Event, patchMethod)
		{
		}

		public State(TStateId stateId, IHId spacetimeId, IHEvent expectingEvent, StatePatchMethod patchMethod)
		{
			StateId = stateId;
			SpacetimeID = spacetimeId;
			LatestUpdate = expectingEvent;
			m_patchMethod = patchMethod;
			Rev = 0;
		}

		//public object Clone()
		//{
		//    return new State(LatestUpdate);
		//}

		public bool IsCompatible(IHTimestamp stamp)
		{
			return LatestUpdate.KnownBy(stamp.Event);
		}

		public bool Patch(StatePatchingCtx patchCtx)
		{
			return Patch(patchCtx.Metadata.FromEvent, patchCtx.Metadata.ToEvent, patchCtx.Metadata.ToRev, patchCtx.DataStream);
		}

		public bool Patch(IHEvent fromEvent, IHEvent toEvent, long toRev, Stream delta)
		{
			if (toEvent.KnownBy(LatestUpdate))
				throw new ApplicationException(string.Format("Trying to update to an older revision. Current {0}, Applying {1}", Rev, toRev));

			if (m_patchMethod == StatePatchMethod.Auto)
			{
				try
				{
					StatePatcher.PatchState(delta, this);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Failed to auto-patch state ({0}) from {1} to {2} with exception:\n{3}", StateId, fromEvent.ToString(), toEvent.ToString(), ex.ToString());
					return false;
				}
			}
			else
			{
				if (!DoPatch(delta))
					return false;
			}

			Rev = toRev;
			return true;
		}
		protected virtual bool DoPatch(Stream delta) { return false; }

		public IEnumerable<FieldInfo> GetFields() { return GetFields(FieldPatchKind.All); }
		public IEnumerable<FieldInfo> GetFields(FieldPatchKind kinds)
		{
			var fields = GetType().GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic);
			return fields.Where(fi =>
				{
					var attr = (StateFieldAttribute)fi.GetCustomAttributes(typeof(StateFieldAttribute), false).FirstOrDefault();
					return attr != null && ((attr.PatchKind & kinds) != 0);
				});
		}

		public StatePatch Serialize(StateSnapshot oldSnapshot, IHEvent expectingEvent)
		{
			var ostream = new MemoryStream();
			Serialize(ostream, oldSnapshot);
			ostream.Close();

			var patch = new StatePatch( this.GetPatchFlag(),
				 oldSnapshot.Timestamp.Event,
				 expectingEvent,
				 ostream.ToArray() );
			return patch;
		}

		public void Serialize(Stream ostream, StateSnapshot oldSnapshot)
		{
			if (m_patchMethod == StatePatchMethod.Auto)
			{
				StatePatcher.GeneratePatch(ostream, this.GetSnapshot(), oldSnapshot, null);
			}
			else
			{
				DoSerialize(ostream, oldSnapshot);
			}

		}

		public virtual void DoSerialize(Stream ostream, StateSnapshot oldSnapshot) { throw new NotImplementedException(); }

		public StateSnapshot GetSnapshot() { return GetSnapshot(LatestUpdate); }
		public StateSnapshot GetSnapshot( IHEvent overridingEvent)
		{
			return GetSnapshot(HTSFactory.Make(SpacetimeID, overridingEvent));
		}

		public StateSnapshot GetSnapshot( IHTimestamp overridingTS)
		{
			var retval = new StateSnapshot(StateId, GetType().FullName, overridingTS, Rev);

			if (m_patchMethod == StatePatchMethod.Customized)
			{
				return DoSnapshot(retval);
			}

			foreach (var fi in GetFields())
			{
				var fval = fi.GetValue(this);
				retval.Fields.Add(new StateSnapshot.FieldEntry() {
					Name = fi.Name,
					Value = fval,
					Type = fi.FieldType,
					Attrib = fi.GetCustomAttributes(typeof(StateFieldAttribute), false).FirstOrDefault() as StateFieldAttribute
				});
			}
			return retval;
		}

		public virtual StateSnapshot DoSnapshot(StateSnapshot initial) { throw new NotImplementedException(); }

		internal void OnCommitting(IHEvent evtFinal)
		{
			LatestUpdate = evtFinal;
			Rev += 1;
		}
	}
}

