using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail;
using Cocktail.HTS;

namespace Cocktail
{
	public struct SpacetimeSnapshot
	{
		public IHTimestamp Timestamp;
		public IEnumerable<State> States;
		public ILookup<TStateId, StatePatch> Redos;

		public IEnumerable<KeyValuePair<TStateId, IHEvent>> LatestEvents
		{
			get
			{
				foreach (var state in Redos)
					yield return new KeyValuePair<TStateId, IHEvent>( state.Key,
						state.Aggregate(HTSFactory.CreateZeroEvent(),
										(acc, patch) => acc.KnownBy(patch.ToEvent) ? patch.ToEvent : acc)
						);
			}
		}
	}

	public interface IState
	{
		TStateId StateId { get; }
	}
	public interface ILocatingService
	{
		void RegisterSpaceTime(Spacetime st);
		TStateId? GetSpacetimeStorageSID(IHId stHid);
		SpacetimeSnapshot? GetSpacetime(IHId id, IHEvent evtAck);
	}
}
