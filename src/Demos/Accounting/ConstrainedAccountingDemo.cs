using System;
using System.Collections.Generic;
using System.Linq;
using Cocktail;
using Cocktail.HTS;
using Demos.States;
using MathLib;
using Skeleton;
using Core.Aux.System;

namespace Demos
{
	[MultiModelContent]
	public class ConstrainedAccountingDemo : BaseAccountingDemo
	{
		Random m_rand = new Random();

		public override void Init(AABB worldBox)
		{
			m_vmST.VMBind(typeof(IAccounting), typeof(ConstrainedAccounting));
			base.Init(worldBox);

			RegisterAction(2, "collision");
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
					case "collision":
						MakeCollision();
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

		override protected State AccountFactory(Spacetime st, IHTimestamp stamp, int index)
		{
			return new MonitoredAccount(TStateId.DebugCreate(111ul * ((ulong)index + 1)), stamp);
		}

		private void MakeCollision()
		{
			Log.Info("DEMO", "collision step 1");
			using (new WithIn(m_spacetimes[0]))
				m_accountingInvoker.Withdraw(new LocalStateRef<MonitoredAccount>((MonitoredAccount)m_accounts[0]), 5.0f);

			Log.Info("DEMO", "collision step 2");
			using (new WithIn(m_spacetimes[1]))
				m_accountingInvoker.Transfer(GenRemoteRef(m_accounts[0]), GenRemoteRef(m_accounts[1]), 7.0f);

			SyncSpacetimes();
		}

		public override IPresent GetPresent()
		{
			return new Present(m_accounts.Select(acc => ((MonitoredAccount)acc).Snapshot()).ToArray()
				, m_worldBox);
		}
	}
}
