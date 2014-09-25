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

namespace Demos.Accounting
{
	[MultiModelContent]
	public class DistributedAccountingDemo : BaseAccountingDemo
	{
		Random m_rand = new Random();

		public override void Init(AABB worldBox)
		{
			m_vmST.VMBind(typeof(IAccounting), typeof(Accounting));
			base.Init(worldBox);
		}

		protected override void UpdateWorld(float interval)
		{
			if (m_elapsed < 0.5)
				return;

			using (new WithIn(m_spacetimes[0]))
			{
				m_accountingInvoker.Transfer(m_accounts[0], m_accounts[1], (float)(m_rand.Next(100) - 50));
			}
		}

		protected override State AccountFactory(Spacetime st, IHTimestamp stamp, int index)
		{
			return new Account(TStateId.DebugCreate(111ul * ((ulong)index + 1)), stamp);
		}

		public override IPresent GetPresent()
		{
			return new Present(m_accounts.Select(a => ServiceManager.SyncService.AggregateDistributedDelta(a.StateId)).ToArray()
							, m_worldBox);
		}
	}
}
