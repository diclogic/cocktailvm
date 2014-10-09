using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail.HTS;

namespace Cocktail
{
	public enum DeployMode
	{
		Debug,
		UnitTest,
	}

	public class ComputeNode
	{
		private IHId m_nodeRoot;
		private int m_nodeSerial;
		public IHIdFactory HIdFactory { get; private set; }

#region For Unit Tests
		private VMSpacetime m_vmstStandalone;
		public VMSpacetime VMSpacetimeForUnitTest
		{
			get {
				if (m_nodeSerial == 0)
					return m_vmstStandalone;
				throw new ApplicationException("Trying to access standalone VM spacetime without being a unit test");
			}
		}
#endregion

		public ComputeNode(int n, int upperbound, DeployMode deployMode)
		{
			m_nodeSerial = n;
			if (deployMode == DeployMode.Debug || deployMode == DeployMode.UnitTest)
				HIdFactory = new ITCIdentityFactory(4, 4);
			else
				HIdFactory = new ITCIdentityFactory();

			// TODO: merge-able ITC
			m_nodeRoot = HIdFactory.CreateFromRoot(upperbound).ElementAt(n);

			if (m_nodeSerial == 0)
			{
				m_vmstStandalone = new VMSpacetime(CreateHid(), HIdFactory);
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
