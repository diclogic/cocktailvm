using System;
using System.Collections.Generic;
using System.Linq;
using Cocktail;
using Cocktail.HTS;
using Demos.States;
using MathLib;
using Skeleton;
using Core.Aux.System;

namespace Demos.Accounting
{
	[MultiModelContent]
	public class ConstrainedAccountingDemo : BaseAccountingDemo
	{
		Random m_rand = new Random();
		int m_nPresenting = 0;

		public override void Init(AABB worldBox)
		{
			m_vmST.VMBind(typeof(IAccounting), typeof(ConstrainedAccounting));
			base.Init(worldBox);

			RegisterAction(2, "transfer");
			RegisterAction(3, "collision");
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
					case "transfer":
						MakeNormalTransfer();
						break;
					case "collision":
						MakeCollision();
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
			return new MonitoredAccount(TStateId.DebugCreate(111ul * ((ulong)index + 1)), stamp);
		}

		private void MakeNormalTransfer()
		{
			Log.Info("DEMO", "normal transfer");
			//using(new WithIn(m_spacetimes[0]))
				m_accountingInvoker.Transfer(m_accounts[0], m_accounts[1], (float)(m_rand.Next(100) - 50));
		}

		private void MakeCollision()
		{
			Log.Info("DEMO", "collision step 1");
			using (new WithIn(m_spacetimes[0]))
				m_accountingInvoker.Withdraw(m_accounts[0], 5.0f);

			Log.Info("DEMO", "collision step 2");
			using (new WithIn(m_spacetimes[1]))
				m_accountingInvoker.Transfer(m_accounts[0], m_accounts[1], 7.0f);
		}

		public override IPresent GetPresent()
		{
			int idx = m_nPresenting;
			var statesnapshots = m_accounts.Select(sref => m_spacetimes[idx].ExportStateSnapshot(sref.StateId)).ToArray();
			return new Present(statesnapshots, m_worldBox);
		}
	}
}
