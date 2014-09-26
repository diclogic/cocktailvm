using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail.HTS;

namespace Cocktail
{
	public class ComputeNode
	{
		private IHId m_nodeRoot;
		public IHIdFactory HIdFactory { get; private set; }
		public VMSpacetime VMST { get; private set; }

		public ComputeNode(int n, int upperbound)
		{
			HIdFactory = new ITCIdentityFactory();
			// TODO: merge-able ITC
			m_nodeRoot = HIdFactory.CreateFromRoot(upperbound).ElementAt(n);

			if (n == 0)
			{
				VMST = new VMSpacetime(CreateHid(), HIdFactory);
			}
		}

		public IHId CreateHid()
		{
			return HIdFactory.CreateChildren(m_nodeRoot, 1).First();
		}

		public Spacetime CreateSpacetime()
		{
			return CreateSpacetime(ITCEvent.CreateZero());
		}
		public Spacetime CreateSpacetime(IHEvent event_)
		{
			var st = new Spacetime(CreateHid(), event_, HIdFactory);
			ServiceManager.LocatingService.RegisterSpaceTime(st);
			ServiceManager.SyncService.PullFromVmSt(st.ID);
			return st;
		}
	}
}
