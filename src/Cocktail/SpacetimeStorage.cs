using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail.HTS;
using Core.Aux.System;
using System.IO;

namespace Cocktail
{

	public struct ExternalSTEntry
	{
		public IHId SpacetimeId;
		public IHEvent LatestUpateTime;
		public bool IsListeningTo;
		public IDictionary<TStateId,State> LocalStates;
	}

	public class SpacetimeStorage : State, IScope
	{
		public struct _StateRef { public TStateId sid; public string refType; }

		// just want to make it non-commutative
		[StateField(PatchKind = FieldPatchCompatibility.Invalid)]
		private Dictionary<TStateId, State> m_nativeStates;

		private Dictionary<TStateId, State> m_stateCache;

		// we use cached state to "pro-act" on an event involves external states optimistically, and let the external ST denies it.
		private Dictionary<IHId, ExternalSTEntry> m_externalSTs = new Dictionary<IHId, ExternalSTEntry>();

		public SpacetimeStorage(IHTimestamp stamp, IEnumerable<State> initialStates, IEnumerable<ExternalSTEntry> initialExternalSTs)
			:base(stamp, StatePatchMethod.Customized)
		{
			m_stateCache = initialStates.ToDictionary((s) => s.StateId);
			m_nativeStates = initialStates.ToDictionary((s) => s.StateId);

			// add itself
			m_nativeStates.Add(this.StateId, this);

			foreach (var st in initialExternalSTs)
				AddSpacetime(st);
		}

		internal void AddNativeState(State newState)
		{
			m_nativeStates.Add(newState.StateId, newState);
			m_stateCache[newState.StateId] = newState;
		}

		internal IEnumerable<State> GetAllStates()
		{
			return m_stateCache.Values;
		}

		internal IEnumerable<State> GetNativeStates()
		{
			return m_nativeStates.Values;
		}

		internal IEnumerable<State> GetAllStates(IEnumerable<TStateId> ids)
		{
			return m_stateCache.Where(kv => ids.Contains(kv.Key)).Select(kv => kv.Value);
		}

		internal bool HasState(TStateId id)
		{
			return null != GetState(id);
		}

		internal State GetState(TStateId id)
		{
			State retval;
			if (!m_stateCache.TryGetValue(id, out retval))
				return null;
			return retval;
		}

		internal State GetOrCreate(TStateId id, Func<State> constructor)
		{
			var retval = GetState(id);
			if (retval != null)
				return retval;
			retval = constructor();
			if (retval == null)
				throw new ArgumentException("The provided constructor doesn't always provide a State object");

			m_stateCache.Add(retval.StateId, retval);
			return retval;
		}

		public void AddSpacetime(SpacetimeSnapshot snapshot)
		{
			AddSpacetime(snapshot.Timestamp, snapshot.States);
		}

		internal void AddSpacetime(IHTimestamp foreignStamp, IEnumerable<State> newStates)
		{
			ExternalSTEntry entry;
			entry.IsListeningTo = false;
			entry.SpacetimeId = foreignStamp.ID;
			entry.LatestUpateTime = foreignStamp.Event;
			entry.LocalStates = newStates.Where(v => v.SpacetimeID == foreignStamp.ID).ToDictionary(s => s.StateId);

			AddSpacetime(entry);
		}

		private void AddSpacetime(ExternalSTEntry entry)
		{
			m_externalSTs[entry.SpacetimeId] = entry;

			foreach (var s in entry.LocalStates)
			{
				if (m_stateCache.ContainsKey(s.Key))
					Log.Warning("Relpacing cached state `{0}`", s.Key);
				m_stateCache[s.Key] = s.Value;
			}
		}

		void RemoveSpacetime(IHId spacetimeId)
		{
			ExternalSTEntry entry;
			if (!m_externalSTs.TryGetValue(spacetimeId, out entry))
				return;

			foreach (var sid in entry.LocalStates.Keys)
				if (!m_nativeStates.ContainsKey(sid))
					m_stateCache.Remove(sid);

			m_externalSTs.Remove(spacetimeId);
		}

		#region IScope Members

		public State Dereference(TStateId sid)
		{
			return m_stateCache[sid];
		}

		public State Dereference(TStateId sid, string refType)
		{
			var state = m_stateCache[sid];
			if (state.GetType().ToString() != refType)
				throw new RuntimeException(string.Format("Type check failed when dereferencing state `{0}`: expecting `{1}` got `{2}`"
					, sid, refType, state.GetType().ToString()));

			return state;
		}
		#endregion

		#region Customized state patching

		const byte MAGICBYTE = 0xD7;

		protected override StateSnapshot DoSnapshot(StateSnapshot initial)
		{
			var retval = initial;
			var entry = new StateSnapshot.FieldEntry();
			entry.Name = "m_nativeStates";
			entry.Type = typeof(_StateRef[]);
			entry.Attrib = null;
			entry.Value = m_nativeStates.Select(kv =>
				new _StateRef() { sid = kv.Value.StateId, refType = kv.Value.GetType().ToString() }).ToArray();
			retval.Fields.Add(entry);
			return retval;
		}

		protected override void DoSerialize(Stream ostream, StateSnapshot oldSnapshot)
		{
			var oldDict = ((_StateRef[])oldSnapshot.Fields.First(field => field.Name == "m_nativeStates").Value)
				.ToDictionary(kv => kv.sid, kv => kv.refType);

			foreach (var newEntry in m_nativeStates.Keys.Except(oldDict.Keys).Select( k => m_nativeStates[k] ))
			{
				var writer = new BinaryWriter(ostream);
				writer.Write(MAGICBYTE);
				writer.Write(newEntry.StateId.ToUlong());
				writer.Write(newEntry.GetType().ToString());
			}
		}

		protected override bool DoPatch(Stream delta)
		{
			var reader = new BinaryReader(delta);
			while (reader.PeekChar() != -1)
			{
				if (reader.PeekChar() != MAGICBYTE)
					return false;

				reader.ReadChar();
				var stateid = new TStateId(reader.ReadUInt64());
				var refType = reader.ReadString();

				var state = Dereference(stateid, refType);
				m_nativeStates.Add(stateid, state);
				m_stateCache[stateid] = state;
			}

			return true;
		}

		#endregion

		#region Migration support interface

		internal bool HasNativeState(TStateId id)
		{
			return m_nativeStates.ContainsKey(id);
		}

		internal void AddNativeState(TStateId id)
		{
			State s;
			if (!m_stateCache.TryGetValue(id, out s))
				throw new ApplicationException(string.Format("Can't promote/immigrate state `{0}` to a native state, can't find it in `{1}`"
					, id, SpacetimeID));

			s.OnMigratingTo(this.SpacetimeID);
			m_nativeStates.Add(id, s);
		}

		internal void RemoveNativeState(TStateId id)
		{
			m_nativeStates.Remove(id);
			// shall we clear cache?
		}

		#endregion
	}
}
