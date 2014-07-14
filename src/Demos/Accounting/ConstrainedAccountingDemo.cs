using System;
using System.Collections.Generic;
using System.Linq;
using Cocktail;
using Cocktail.HTS;
using Demos.States;
using MathLib;
using Skeleton;

namespace Demos
{
	[MultiModelContent]
	public class ConstrainedAccountingDemo : BaseAccountingDemo
	{
		protected override void UpdateWorld(float interval)
		{
			if (m_elapsed > 0.5)
			{
				using(new WithIn(m_spacetimes[0]))
				{
					m_accountingInvoker.Transfer(new LocalStateRef<MonitoredAccount>((MonitoredAccount)m_accounts[0])
											, GenRemoteRef(m_accounts[1])
											, (float)(m_rand.Next(100) - 50));
				}
			}
		}

		override protected State AccountFactory(Spacetime st, IHTimestamp stamp, int index)
		{
			return new MonitoredAccount(TStateId.DebugCreate(111ul * (ulong)index), stamp);
		}


		private void MakeCollision()
		{
			using (new WithIn(m_spacetimes[0]))
				m_accountingInvoker.Withdraw(new LocalStateRef<MonitoredAccount>((MonitoredAccount)m_accounts[0]), 5.0f);
			using (new WithIn(m_spacetimes[1]))
				m_accountingInvoker.Transfer(GenRemoteRef(m_accounts[0]), GenRemoteRef(m_accounts[1]), 7.0f);

			SyncSpacetimes();
		}

		public override IPresenter GetPresent()
		{
			return new Present(m_accounts.Select( acc => (MonitoredAccount)acc), m_worldBox);
		}

		protected class Present : BaseAccountingDemo.Present
		{
			public Present(IEnumerable<MonitoredAccount> accounts, AABB worldBox)
				:base(accounts.Select(a => a.Snapshot()).ToArray(), worldBox)
			{
			}
		}
	}
}
