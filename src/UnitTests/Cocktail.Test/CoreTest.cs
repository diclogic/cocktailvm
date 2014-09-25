using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Cocktail;
using Demos.Accounting;
using Cocktail.HTS;

namespace UnitTests.Cocktail
{
	public class CoreTestFixture: IDisposable
	{
		// SETUP
		public CoreTestFixture()
		{
			ServiceManager.Reset();

		}

		// TEARDOWN
		public void Dispose()
		{

		}
	}

	public class TwoSpacetimeTests : IUseFixture<CoreTestFixture>
	{
		IAccounting m_accountingInvoker;
		IHIdFactory m_idFactory;
		List<Spacetime> m_spacetimes = new List<Spacetime>();
		List<MonitoredAccount> m_accountStates = new List<MonitoredAccount>();
		List<ScopedStateRef> m_accounts = new List<ScopedStateRef>();

		public void SetFixture(CoreTestFixture data) { }

		protected State AccountFactory(Spacetime st, IHTimestamp stamp, int index)
		{
			return new MonitoredAccount(TStateId.DebugCreate(111ul * ((ulong)index + 1)), stamp);
		}

		public TwoSpacetimeTests()
		{
			m_accountingInvoker = InvocationBuilder.Build<IAccounting>();
			m_idFactory = ServiceManager.HIdFactory;
			var m_vmST = new VMSpacetime(m_idFactory);
			m_vmST.VMBind(typeof(IAccounting), typeof(MonitoredAccount));


			ServiceManager.Init(m_vmST);

			Assert.True(m_vmST.VMExist(typeof(IAccounting)));

			// --- init demo objects ---

			{
				var initialST = new Spacetime(m_idFactory.CreateFromRoot(), ITCEvent.CreateZero(), m_idFactory);
				var secondST = new Spacetime(m_idFactory.CreateFromRoot(), ITCEvent.CreateZero(), m_idFactory);
				m_spacetimes.AddRange(new[] { initialST, secondST });
			}

			// fake the globally existing SyncManager
			foreach (var ST in m_spacetimes)
				ServiceManager.LocatingService.RegisterSpaceTime(ST);

			// must pull new VM to use IAccounting
			foreach (var ST in m_spacetimes)
				ServiceManager.SyncService.PullFromVmSt(ST.ID);


			// create 2 accounts
			for (int ii = 0; ii < 2; ++ii)
			{
				// firstly create into same ST then we migrate one to another ST
				var newAccount = m_spacetimes[0].CreateState((st, stamp) => AccountFactory(st, stamp, ii));
				//m_namingSvc.RegisterObject(newAccount.StateId.ToString(), newAccount.GetType().ToString(), newAccount);
				m_accountStates.Add((MonitoredAccount)newAccount);
				m_accounts.Add(new ScopedStateRef(newAccount.StateId, newAccount.GetType().ToString()));
			}

			m_spacetimes[1].Immigrate(m_accounts[1].StateId, m_spacetimes[0].ID);

			// deposit some initial money
			using (new WithIn(m_spacetimes[0]))
			{
				foreach (var acc in m_accounts)
					m_accountingInvoker.Deposit(acc, 900.0f);
			}
		}

		[Fact]
		public void TestLocalAccountDeposit()
		{
			using (new WithIn(m_spacetimes[0]))
			{
				m_accountingInvoker.Deposit(m_accounts[0], 900.0f);
			}

			Assert.Equal(900.0f, m_accountStates[0].Balance);
		}

		[Fact]
		public void TestRemoteAccountDeposit()
		{
			using (new WithIn(m_spacetimes[0]))
			{
				m_accountingInvoker.Deposit(m_accounts[1], 900.0f);
			}

			Assert.Equal(900.0f, m_accountStates[1].Balance);
		}
	}
}
