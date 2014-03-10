﻿using System;
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

		private Spacetime m_spaceTime;					//< the space-time it belongs to
		private StatePatch m_pendingPatch;
		private readonly StatePatchMethod m_patchMethod;

		public TStateId StateId { get; protected set; }	//< to identify a state
		public IHTimestamp LatestUpdate { get; private set; }

		public State(Spacetime spaceTime, IHTimestamp stamp)
			: this(spaceTime, stamp, StatePatchMethod.Auto)
		{
		}

		public State(Spacetime spaceTime, IHTimestamp stamp, StatePatchMethod patchMethod)
			: this(new TStateId(m_seed), spaceTime, stamp, patchMethod)
		{
		}
		public State(TStateId stateId, Spacetime spaceTime, IHTimestamp stamp, StatePatchMethod patchMethod)
		{
			StateId = stateId;
			m_spaceTime = spaceTime;
			LatestUpdate = stamp;
			m_patchMethod = patchMethod;
		}

		//public object Clone()
		//{
		//    return new State(LatestUpdate);
		//}

		public bool IsCompatible(IHTimestamp stamp)
		{
			return LatestUpdate.Event.LtEq(stamp.Event);
		}

		public virtual bool Merge(/*StateSnapshot snapshot,*/ StatePatch patch)
		{
			return Patch(patch);
		}

		public bool Patch(StatePatch patch)
		{
			return Patch(patch.FromRev, patch.ToRev, patch.data);
		}

		public bool Patch(IHEvent fromRev, IHEvent toRev, Stream delta)
		{
			if (toRev.LtEq(LatestUpdate.Event))
				throw new ApplicationException("Trying to update to an older revision");

			if (m_patchMethod == StatePatchMethod.Auto)
			{
				try
				{
					StatePatcher.PatchState(delta, this);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Failed to auto-patch state ({0}) from {1} to {2} with exception:\n{3}", StateId, fromRev.ToString(), toRev.ToString(), ex.ToString());
					return false;
				}
			}
			else
			{
				return DoPatch(delta);
			}
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

			var patch = new StatePatch()
			{
				FromRev = oldSnapshot.Timestamp.Event,
				ToRev = expectingEvent,
				Flag = this.GetPatchFlag(),
				data = ostream
			};
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

		public virtual void DoSerialize(Stream ostream, StateSnapshot oldSnapshot) { }

		public StateSnapshot GetSnapshot() { return GetSnapshot(LatestUpdate); }
		public StateSnapshot GetSnapshot( IHEvent overridingEvent)
		{
			return GetSnapshot(HTSFactory.Make(LatestUpdate.ID, overridingEvent));
		}
		public StateSnapshot GetSnapshot( IHTimestamp overridingTS)
		{

			var retval = new StateSnapshot(StateId, GetType().FullName, overridingTS);

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
	}
}

