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
using Core.Aux.System;



namespace Demos
{
	public abstract class BaseAccountingDemo : BaseModel
	{
		protected List<State> m_accounts = new List<State>();
		protected AABB m_worldBox;
		protected double m_accumulate = 0;
		protected List<Spacetime> m_spacetimes = new List<Spacetime>();
		protected IHIdFactory m_idFactory;
		protected VMSpacetime m_vmST;
		protected NamingSvcClient m_namingSvc;
		protected IAccounting m_accountingInvoker;

		// trivial
		protected double m_elapsed;

		public BaseAccountingDemo()
		{
			// reset all global states, to allow model switching
			NamingSvcClient.Instance.Reset();
			HIdService.Reset();
			PseudoSyncMgr.Instance.Reset();

			m_namingSvc = NamingSvcClient.Instance;
			m_accountingInvoker = InvocationBuilder.Build<IAccounting>();
			m_idFactory = HIdService.GetFactory();
			m_vmST = new VMSpacetime(m_idFactory);
		}

		public override void Init(AABB worldBox)
		{
			m_worldBox = worldBox;
			if (!m_vmST.VMExist(typeof(IAccounting)))
				throw new ApplicationException("Accounting functions are not declared in VM yet");

			// --- init demo objects ---

			{
				var initialST = new Spacetime(m_idFactory.CreateFromRoot(), ITCEvent.CreateZero(), m_idFactory);
				var secondST = new Spacetime(m_idFactory.CreateFromRoot(), ITCEvent.CreateZero(), m_idFactory);
				m_spacetimes.AddRange(new[] { initialST, secondST });
			}

			// fake the globally existing SyncManager
			PseudoSyncMgr.Instance.Initialize(m_vmST);
			foreach (var ST in m_spacetimes)
				PseudoSyncMgr.Instance.RegisterSpaceTime(ST);

			// must pull new VM to use IAccounting
			foreach (var ST in m_spacetimes)
				PseudoSyncMgr.Instance.PullFromVmSt(ST.ID);


			// create 2 accounts
			for (int ii = 0; ii < 2; ++ii)
			{
				var newAccount = m_spacetimes[ii].CreateState((st, stamp) => AccountFactory(st, stamp, ii));
				m_namingSvc.RegisterObject(newAccount.StateId.ToString(), newAccount.GetType().ToString(), newAccount);
				m_accounts.Add(newAccount);
			}

			// deposit some initial money
			using (new WithIn(m_spacetimes[0]))
			{
				foreach (var acc in m_accounts)
					m_accountingInvoker.Deposit(GenRemoteRef(acc), 900.0f);
			}

			// initial chronon
			SyncSpacetimes();
		}

		protected abstract State AccountFactory(Spacetime st, IHTimestamp stamp, int index);

		protected static RemoteStateRef GenRemoteRef(State state) { return new RemoteStateRef(state.StateId, state.GetType().ToString()); }

		protected void SyncSpacetimes()
		{
			Log.Info("DEMO", "Sync 1 << 0");
			m_spacetimes[1].PullAllFrom(m_spacetimes[0].Snapshot(m_spacetimes[1].LatestEvent));
			Log.Info("DEMO", "Sync 0 << 1");
			m_spacetimes[0].PullAllFrom(m_spacetimes[1].Snapshot(m_spacetimes[0].LatestEvent));
		}

		protected abstract void UpdateWorld(float interval);

		public override void Update(IRenderer renderer, double dt, IEnumerable<string> controlCmds)
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

		protected class Present : BasePresent
		{
			StateSnapshot[] m_accounts;
			readonly AABB m_worldbox;

			public Present(StateSnapshot[] accounts, AABB worldBox)
			{
				m_accounts = accounts;
				m_worldbox = worldBox;
			}

			public override void Render()
			{
				const float MAX_AMOUNT = 10000;

				GL.Begin(BeginMode.Triangles);
				int index = 0;
				foreach (var p in m_accounts.Select(acc => (float)acc.Fields.Find(f => f.Name == "Balance").Value))
				{
					GL.Color4(Color4.DarkRed);
					DrawBar(m_accounts.Length * 2, index * 2, Math.Min(p, MAX_AMOUNT) / MAX_AMOUNT);
					++index;
				}
				GL.End();
			}

			protected void DrawQuadByTriangles(float minX, float minY, float width, float height)
			{
				GL.Vertex2(minX, minY);
				GL.Vertex2(minX + width, minY);
				GL.Vertex2(minX, minY + height);

				GL.Vertex2(minX, minY + height);
				GL.Vertex2(minX + width, minY);
				GL.Vertex2(minX + width, minY + height);
			}

			/// <param name="height">from 0.0 to 1.0</param>
			protected void DrawBar(int total, int index, float height)
			{
				var worldWidth = m_worldbox.Max.X - m_worldbox.Min.X;
				var worldHeight = m_worldbox.Max.Y - m_worldbox.Min.Y;
				var barWidth = worldWidth / total;
				DrawQuadByTriangles(barWidth * index + m_worldbox.Min.X, m_worldbox.Min.Y
										, barWidth
										, worldHeight * height);
			}

		}
	}
}