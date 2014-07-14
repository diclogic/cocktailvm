using System;
using System.Collections.Generic;
using System.Linq;
using Cocktail;
using Cocktail.HTS;
using Demos.States;
using DOA;
using MathLib;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using Skeleton;

namespace Demos
{
	[MultiModelContent]
	public class DistributedAccountingDemo : BaseAccountingDemo
	{
		protected override void UpdateWorld(float interval)
		{
			if (m_elapsed < 0.5)
				return;

			using (new WithIn(m_spacetimes[0]))
			{
				m_accountingInvoker.Transfer( new LocalStateRef<Account>((Account)m_accounts[0])
									, GenRemoteRef(m_accounts[1])
									, (float)(m_rand.Next(100)-50));
			}
		}

		protected override State AccountFactory(Spacetime st, IHTimestamp stamp, int index)
		{
			return new Account(TStateId.DebugCreate(111), stamp);
		}

		public override IPresenter GetPresent()
		{
			return new Present(m_accounts.Select(st => (Account)st), m_worldBox);
		}

		public class Present : BaseAccountingDemo.Present
		{
			public Present(IEnumerable<Account> accounts, AABB worldBox)
				:base(accounts.Select(a => PseudoSyncMgr.Instance.AggregateDistributedDelta(a.StateId)).ToArray(), worldBox)
			{
			}
		}
	}
}
