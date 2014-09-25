using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail.HTS;

namespace Cocktail
{
	public interface ISyncService
	{
		PrePullRequestResult PrePullRequest(IHId idPuller, IHId idRequester, IHEvent evtOriginal, IEnumerable<TStateId> affectedStates);
		bool PullRequest(IHId idPuller, IHId idRequester, IHEvent foreignExpectedEvent, ILookup<TStateId, StatePatch> affectedStates);
		StateSnapshot AggregateDistributedDelta(IEnumerable<IHId> STIDs, TStateId state);
		StateSnapshot AggregateDistributedDelta(TStateId state);
		void PullFromVmSt(IHId spacetimeId);
	}
}
