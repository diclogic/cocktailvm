using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail.HTS;
using Core.Aux.System;

namespace Cocktail
{

	public struct ExternalSTEntry
	{
		public IHId SpacetimeId;
		public IHEvent LatestUpateTime;
		public bool IsListeningTo;
		public IDictionary<TStateId,State> LocalStates;
	}

	public class SpacetimeStorage : IScope
	{
		private Dictionary<TStateId, State> m_nativeStates;
		private Dictionary<TStateId, State> m_stateCache;
		// we use cached state to "pro-act" on an event involves external states optimistically, and let the external ST denies it.
		private Dictionary<IHId, ExternalSTEntry> m_externalSTs = new Dictionary<IHId, ExternalSTEntry>();

		public SpacetimeStorage(IEnumerable<State> initialStates, IEnumerable<ExternalSTEntry> initialExternalSTs)
		{
			m_stateCache = initialStates.ToDictionary((s) => s.StateId);
			m_nativeStates = initialStates.ToDictionary((s) => s.StateId);

			foreach (var st in initialExternalSTs)
				AddSpacetime(st);
		}

		internal void AddNativeState(State newState)
		{
			m_nativeStates.Add(newState.StateId, newState);
			m_stateCache[newState.StateId] = newState;
		}

		//internal void AddState(State newState)
		//{
		//    m_allStates.Add(newState.StateId, newState);
		//}

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

		public object Dereference(TStateId sid)
		{
			return m_stateCache[sid];
		}

		#endregion
	}
}
