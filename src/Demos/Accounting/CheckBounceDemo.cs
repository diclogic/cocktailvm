using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Skeleton;
using MathLib;
using Cocktail;
using Cocktail.HTS;
using Cocktail.Interp;
using Core.Aux.System;

namespace Demos.Accounting
{
	/// <summary>
	/// Well it's not a real check-bounce by definition
	/// </summary>
	[MultiModelContent]
	class CheckBounceDemo : BaseAccountingDemo
	{
		Random m_rand = new Random();
		int m_nPresenting = 0;

		public override void Init(AABB worldBox)
		{
			m_vmST.VMDefine(typeof(IAccounting), typeof(ConstrainedAccounting));
			base.Init(worldBox);

			RegisterAction(2, "init");
			RegisterAction(2, "kickoff");
			RegisterAction(4, "nextpresent");
		}

		public override void Update(IRenderer renderer, double dt, IEnumerable<string> controlCmds)
		{
			foreach (var cmd in controlCmds)
			{
				var args = cmd.Split(' ');
				if (args.Length <= 0)
					continue;
				switch (args[0])
				{
					case "init":
						InitAccounts();
						break;
					case "kickoff":
						KickOff();
						break;
					case "nextpresent":
						m_nPresenting = (m_nPresenting + 1) % 2;
						break;
				}
			}

			base.Update(renderer, dt, controlCmds);
		}

		protected override void UpdateWorld(float interval)
		{
			if (m_elapsed > 0.5)
			{
				//using(new WithIn(m_spacetimes[0]))
				//{
				//    Log.Info("DEMO", "daily transfer");
				//    m_accountingInvoker.Transfer(new LocalStateRef<MonitoredAccount>((MonitoredAccount)m_accounts[0])
				//                            , GenRemoteRef(m_accounts[1])
				//                            , (float)(m_rand.Next(100) - 50));
				//    SyncSpacetimes();
				//}
			}
		}

		protected override State AccountFactory(Spacetime st, IHTimestamp stamp, int index)
		{
			var firstId = TStateId.DebugCreate(111ul);
			var secondId = TStateId.DebugCreate(222ul);
			var ret = new MonitoredAccount(TStateId.DebugCreate(111ul * ((ulong)index + 1)), stamp);
			if (index == 0)
			{
			ret.RegisterInlineTrigger((s) =>
				{
					StateRef from = new ScopedStateRef(firstId, typeof(MonitoredAccount).Name),
						to = new ScopedStateRef(secondId, typeof(MonitoredAccount).Name);

					WithIn.GetWithin().DeferExecute("Transfer", Utils.MakeArgList("fromAcc", from, "toAcc", to), 100.0f);
				},
				(s) =>  (s as MonitoredAccount).Balance > 100 );
			}
			else
			{
			ret.RegisterInlineTrigger((s) =>
				{
					StateRef from = new ScopedStateRef(secondId, typeof(MonitoredAccount).Name),
						to = new ScopedStateRef(firstId, typeof(MonitoredAccount).Name);

					WithIn.GetWithin().DeferExecute("Transfer", Utils.MakeArgList("fromAcc", from, "toAcc", to), 100.0f);
				},
				(s) =>  (s as MonitoredAccount).Balance > 100 );
			}
			return ret;
		}

		private void InitAccounts()
		{
			Log.Info("DEMO", "init accounts");
			using(new WithIn(m_spacetimes[0]))
				m_accountingInvoker.Deposit(m_accounts[0], 100.0f);

			using(new WithIn(m_spacetimes[1]))
				m_accountingInvoker.Deposit(m_accounts[1], 100.0f);
		}

		private void KickOff()
		{
			Log.Info("DEMO", "kick off");
			using (new WithIn(m_spacetimes[0]))
				m_accountingInvoker.Deposit(m_accounts[0], 5.0f);
		}

		public override IPresent GetPresent()
		{
			int idx = m_nPresenting;
			var statesnapshots = m_accounts.Select(sref => m_spacetimes[idx].ExportStateSnapshot(sref.StateId)).ToArray();
			return new Present(statesnapshots, m_worldBox);
		}
	}
}
