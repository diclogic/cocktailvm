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

		private struct StateCacheEntry
		{
			public State ref_;
			public DateTime lastTouch;
			public int refCount;
		}

		// just want to make it non-commutative
		[StateField(PatchKind = FieldPatchCompatibility.Invalid)]
		private Dictionary<TStateId, State> m_nativeStates = new Dictionary<TStateId, State>();

		// we use cached state to "pro-act" on an event involves external states optimistically, and let the external ST denies it.
		private Dictionary<IHId, ExternalSTEntry> m_externalSTs = new Dictionary<IHId, ExternalSTEntry>();

		// look up table for all states we have
		private Dictionary<TStateId, StateCacheEntry> m_stateCache = new Dictionary<TStateId, StateCacheEntry>();

		public SpacetimeStorage(IHTimestamp stamp, IEnumerable<State> initialStates, IEnumerable<ExternalSTEntry> initialExternalSTs)
			:base(stamp, StatePatchMethod.Customized)
		{
			foreach (var s in initialStates)
				AddNativeState(s);

			// add itself
			m_nativeStates.Add(this.StateId, this);

			foreach (var st in initialExternalSTs)
				AddSpacetime(st);
		}

		internal void AddNativeState(State newState)
		{
			m_nativeStates.Add(newState.StateId, newState);
			CacheAdd(newState.StateId, newState);
		}

		internal IEnumerable<State> GetAllStates()
		{
			return m_stateCache.Values.Select(e => e.ref_);
		}

		internal IEnumerable<State> GetNativeStates()
		{
			return m_nativeStates.Values;
		}

		internal IEnumerable<State> GetAllStates(IEnumerable<TStateId> ids)
		{
			return m_stateCache.Where(kv => ids.Contains(kv.Key)).Select(kv => kv.Value.ref_);
		}

		internal bool HasState(TStateId id)
		{
			return null != GetState(id);
		}

		internal State GetState(TStateId id)
		{
			return CacheTouch(id);
		}

		internal State GetOrCreate(TStateId id, Func<State> constructor)
		{
			var retval = GetState(id);
			if (retval != null)
				return retval;
			retval = constructor();
			if (retval == null)
				throw new ArgumentException("The provided constructor doesn't always provide a State object");

			CacheAdd(retval.StateId, retval);
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

		private void AddSpacetime(ExternalSTEntry entryST)
		{
			m_externalSTs[entryST.SpacetimeId] = entryST;

			foreach (var s in entryST.LocalStates)
			{
				if (m_stateCache.ContainsKey(s.Key))
					Log.Warning("Relpacing cached state `{0}`", s.Key);

				CacheAdd(s.Key, s.Value);
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
			return GetState(sid);
		}

		public State Dereference(TStateId sid, string refType)
		{
			var state = GetState(sid);
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
				CacheAdd(stateid, state);
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
			StateCacheEntry s;
			if (!m_stateCache.TryGetValue(id, out s))
				throw new ApplicationException(string.Format("Can't promote/immigrate state `{0}` to a native state, can't find it in `{1}`"
					, id, SpacetimeID));

			s.ref_.OnMigratingTo(this.SpacetimeID);
			m_nativeStates.Add(id, s.ref_);
			s.refCount += 1;
		}

		internal void RemoveNativeState(TStateId id)
		{
			m_nativeStates.Remove(id);
			CacheRemove(id);
		}

		#endregion

		private void CacheAdd(TStateId id, State ref_)
		{
			var newState = ref_;
			StateCacheEntry entry;
			if (!m_stateCache.TryGetValue(newState.StateId, out entry))
			{
				entry = new StateCacheEntry();
				m_stateCache.Add(newState.StateId, entry);
			}
			entry.ref_ = newState;
			entry.lastTouch = DateTime.Now;
			++entry.refCount;
		}

		private void CacheRemove(TStateId id)
		{
			var entry = m_stateCache[id];
			if (--entry.refCount <= 0)
				m_stateCache.Remove(id);
		}

		private State CacheTouch(TStateId id)
		{
			StateCacheEntry entry;
			if (!m_stateCache.TryGetValue(id, out entry))
				return null;

			entry.lastTouch = DateTime.Now;
			return entry.ref_;
		}
	}
}
