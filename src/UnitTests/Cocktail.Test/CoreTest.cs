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

		}

		// TEARDOWN
		public void Dispose()
		{

		}
	}

	public class TwoSpacetimeTests : IUseFixture<CoreTestFixture>
	{
		IAccounting m_accountingInvoker;
		//IHIdFactory m_idFactory;
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
		}

		private void Setup()
		{
			ServiceManager.Init();
			var vmST = ServiceManager.ComputeNode.VMSpacetimeForUnitTest;
			vmST.VMBind(typeof(IAccounting), typeof(ConstrainedAccounting));


			var initialST = ServiceManager.ComputeNode.CreateSpacetime();
			var secondST = ServiceManager.ComputeNode.CreateSpacetime();
			m_spacetimes.AddRange(new[] { initialST, secondST });



			// create 2 accounts
			for (int ii = 0; ii < 2; ++ii)
			{
				// firstly create into same ST then we migrate one to another ST
				var newAccount = m_spacetimes[0].CreateState((st, stamp) => AccountFactory(st, stamp, ii));
				//m_namingSvc.RegisterObject(newAccount.StateId.ToString(), newAccount.GetType().ToString(), newAccount);
				m_accountStates.Add((MonitoredAccount)newAccount);
				m_accounts.Add(new ScopedStateRef(newAccount.StateId, newAccount.GetType().ToString()));
			}
		}

		private void Teardown()
		{
			m_accountStates.Clear();
			m_accounts.Clear();
			m_spacetimes.Clear();
			ServiceManager.Reset();
		}

		[Fact]
		public void TestLocalAccountDeposit()
		{
			Setup();

			using (new WithIn(m_spacetimes[0]))
			{
				m_accountingInvoker.Deposit(m_accounts[0], 900.0f);
			}

			Assert.Equal(900.0f, m_accountStates[0].Balance);

			Teardown();
		}

		//[Fact]
		public void TestMigration()
		{
			m_spacetimes[1].Immigrate(m_accounts[1].StateId, m_spacetimes[0].ID);
		}

		//[Fact]
		public void TestRemoteAccountDeposit()
		{
			Setup();

			using (new WithIn(m_spacetimes[0]))
			{
				m_accountingInvoker.Deposit(m_accounts[1], 900.0f);
			}

			Assert.Equal(900.0f, m_accountStates[1].Balance);

			Teardown();
		}
	}
}
