using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Extensions;
using Cocktail;
using Demos.Accounting;
using Cocktail.HTS;
using Cocktail.Interp;

namespace UnitTests.Cocktail
{
	public class TriggerTests
	{
		IAccounting m_accountingInvoker;
		//IHIdFactory m_idFactory;
		List<Spacetime> m_spacetimes = new List<Spacetime>();
		List<MonitoredAccount> m_accountStates = new List<MonitoredAccount>();
		List<ScopedStateRef> m_accounts = new List<ScopedStateRef>();

		protected State AccountFactory(Spacetime st, IHTimestamp stamp, int index)
		{
			return new MonitoredAccount(TStateId.DebugCreate(111ul * ((ulong)index + 1)), stamp);
		}


		public TriggerTests()
		{
			m_accountingInvoker = InvocationBuilder.Build<IAccounting>();
			ServiceManager.Init();
		}

		private void Setup()
		{
			m_accountStates.Clear();
			m_accounts.Clear();
			m_spacetimes.Clear();

			ServiceManager.Reset(EReset.Strong);

			var vmST = ServiceManager.ComputeNode.VMSpacetimeForUnitTest;
			vmST.VMDefine(typeof(IAccounting), typeof(ConstrainedAccounting));


			var initialST = ServiceManager.ComputeNode.CreateSpacetime();
			var secondST = ServiceManager.ComputeNode.CreateSpacetime();
			m_spacetimes.AddRange(new[] { initialST, secondST });



			// create 2 accounts
			for (int ii = 0; ii < 2; ++ii)
			{
				// firstly create into same ST then we migrate one to another ST
				var newAccount = m_spacetimes[ii].CreateState((st, stamp) => AccountFactory(st, stamp, ii));
				//m_namingSvc.RegisterObject(newAccount.StateId.ToString(), newAccount.GetType().ToString(), newAccount);
				m_accountStates.Add((MonitoredAccount)newAccount);
				m_accounts.Add(new ScopedStateRef(newAccount.StateId, newAccount.GetType().ToString()));
			}
		}

		//[Fact]
        public void InlineTrigger()
        {
            Assert.Equal(0,0);
        }

		[Fact]
		public void TwoTriggers()
		{
			Setup();

			var firstId = TStateId.DebugCreate(111ul);
			var secondId = TStateId.DebugCreate(222ul);

			using (new WithIn(m_spacetimes[0]))
				m_accountingInvoker.Deposit(m_accounts[0], 100.0f);

			using (new WithIn(m_spacetimes[1]))
				m_accountingInvoker.Deposit(m_accounts[1], 100.0f);

			m_spacetimes[1].PullAllFrom(m_spacetimes[0].Snapshot(m_spacetimes[1].LatestEvent));
			m_spacetimes[0].PullAllFrom(m_spacetimes[1].Snapshot(m_spacetimes[0].LatestEvent));

			m_accountStates[0].RegisterInlineTrigger((s) =>
				{
					StateRef from = new ScopedStateRef(firstId, typeof(MonitoredAccount).Name),
						to = new ScopedStateRef(secondId, typeof(MonitoredAccount).Name);

					WithIn.GetWithin().DeferExecute("Transfer", Utils.MakeArgList("fromAcc", from, "toAcc", to), 100.0f);
				},
				(s) => (s as MonitoredAccount).Balance > 100);

			m_accountStates[1].RegisterInlineTrigger((s) =>
				{
					StateRef from = new ScopedStateRef(secondId, typeof(MonitoredAccount).Name),
						to = new ScopedStateRef(firstId, typeof(MonitoredAccount).Name);

					WithIn.GetWithin().DeferExecute("Transfer", Utils.MakeArgList("fromAcc", from, "toAcc", to), 100.0f);
				},
				(s) => (s as MonitoredAccount).Balance > 100);

			using (new WithIn(m_spacetimes[0]))
				m_accountingInvoker.Deposit(m_accounts[0], 5.0f);
		}

	}
}
