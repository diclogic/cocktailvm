using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CollisionTest.States;
using CollisionTest;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using MathLib;
using Cocktail;
using HTS;
using DOA;

namespace Representation
{
	class NumericalDemo : IModel
	{
		delegate void TransferDeleg(Account fromAcc, Account toAcc);
		List<Account> m_accounts = new List<Account>();
		AABB m_worldBox;
		double m_accumulate = 0;
		Random m_rand = new Random();
		VMSpacetime m_vmST;
		Spacetime m_initialST;
		Spacetime m_secondST;
		List<Spacetime> m_spacetimes = new List<Spacetime>();
		IHIdFactory m_idFactory = HIdService.GetFactory();
		NamingSvcClient m_namingSvc = NamingSvcClient.Instance;
		IAccounting m_accountingInvoker;

		// trivial
		double m_elapsed;

		public NumericalDemo()
		{
			m_accountingInvoker = InvocationBuilder.Build<IAccounting>();
		}

		public void Init(AABB worldBox)
		{
			m_worldBox = worldBox;

			m_vmST = new VMSpacetime(m_idFactory);
			// declare a function form for an event, which also means binding an event to one or a few state types
			m_vmST.VMBind(typeof(IAccounting), typeof(Accounting));

			//kernel.Declare("CreateAccount", FunctionForm.From(typeof(Accounting).GetMethod("CreateAccount")));
			PseudoSyncMgr.Instance.Initialize(m_vmST);


			m_initialST = new Spacetime(m_idFactory.CreateFromRoot(), ITCEvent.CreateZero(), m_idFactory);
			m_secondST = new Spacetime(m_idFactory.CreateFromRoot(), ITCEvent.CreateZero(), m_idFactory);
			m_spacetimes.Add(m_initialST);
			m_spacetimes.Add(m_secondST);
			PseudoSyncMgr.Instance.RegisterSpaceTime(m_initialST);
			PseudoSyncMgr.Instance.RegisterSpaceTime(m_secondST);
			PseudoSyncMgr.Instance.PullFromVmSt(m_initialST.ID);
			PseudoSyncMgr.Instance.PullFromVmSt(m_secondST.ID);


			var newAccount = m_initialST.CreateState((st, stamp) => new Account(TStateId.DebugCreate(111), stamp));
			m_namingSvc.RegisterObject(newAccount.StateId.ToString(), newAccount.GetType().ToString(), newAccount);
			m_accounts.Add(newAccount as Account);

			newAccount = m_secondST.CreateState((st, stamp) => new Account(TStateId.DebugCreate(222), stamp));
			m_namingSvc.RegisterObject(newAccount.StateId.ToString(), newAccount.GetType().ToString(), newAccount);
			m_accounts.Add(newAccount as Account);


			foreach (var acc in m_accounts)
			{
				m_accountingInvoker.Deposit(m_initialST, GenRemoteRef(acc), 900.0f);
			}

			SyncSpacetimes();

			//MakeCollision();
		}

		RemoteStateRef GenRemoteRef(State state) { return new RemoteStateRef(state.StateId, state.GetType().ToString()); }

		void SyncSpacetimes()
		{
			m_spacetimes[1].PullAllFrom(m_spacetimes[0].Snapshot(m_spacetimes[1].LatestEvent));
			m_spacetimes[0].PullAllFrom(m_spacetimes[1].Snapshot(m_spacetimes[0].LatestEvent));
		}

		void UpdateWorld(float interval)
		{
			if (m_elapsed > 0.5)
			{
				m_accountingInvoker.Transfer(m_initialST, new LocalStateRef<Account>(m_accounts[0]), GenRemoteRef(m_accounts[1])
									, (float)m_rand.Next(50));
				SyncSpacetimes();
			}
		}

		private void MakeCollision()
		{
			m_accountingInvoker.Withdraw(m_initialST, new LocalStateRef<Account>(m_accounts[0]), 5.0f);
			m_accountingInvoker.Transfer(m_secondST, GenRemoteRef(m_accounts[0]), GenRemoteRef(m_accounts[1]), 7.0f);

			SyncSpacetimes();
		}

		public void Update(IRenderer renderer, double dt)
		{
			const double UpdateInterval = 1.0 / 60.0;

			m_elapsed += dt;
			m_accumulate += dt;

			while (m_accumulate > UpdateInterval)
			{
				m_accumulate -= UpdateInterval;
				UpdateWorld((float)UpdateInterval);
			}
		}

		public class Present : BasePresenter
		{
			StateSnapshot[] m_accounts;
			readonly AABB m_worldbox;
			public Present(List<Account> pts, AABB worldBox)
			{
				m_accounts = pts.Select(e => e.GetSnapshot()).ToArray();
				m_worldbox = worldBox;
			}
			public override void Render()
			{
				const float MAX_AMOUNT = 10000;
				var count = m_accounts.Length;
				var worldWidth = m_worldbox.Max.X - m_worldbox.Min.X;
				var worldHeight = m_worldbox.Max.Y - m_worldbox.Min.Y;
				var stepWidth = worldWidth / count;
				int index = 0;

				GL.Begin(BeginMode.Triangles);
				foreach (var p in m_accounts)
				{
					GL.Color4(Color4.DarkRed);
					DrawQuadByTriangles(stepWidth * index + m_worldbox.Min.X, m_worldbox.Min.Y
										, stepWidth * index + (stepWidth / 2.0f)
										, worldHeight * (float)p.Fields.Find(f=>f.Name == "Balance").Value / MAX_AMOUNT);
					++index;
				}
				GL.End();
			}

			private void DrawQuadByTriangles(float minX, float minY, float width, float height)
			{
				GL.Vertex2(minX, minY);
				GL.Vertex2(minX + width, minY);
				GL.Vertex2(minX, minY + height);

				GL.Vertex2(minX, minY + height);
				GL.Vertex2(minX + width, minY);
				GL.Vertex2(minX + width, minY + height);
			}
		}
		public IPresenter GetPresent()
		{
			return new Present(m_accounts, m_worldBox);
		}

		// Internal

		void Dynamic()
		{

		}
	}
}
