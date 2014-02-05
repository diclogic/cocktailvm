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

namespace Representation
{
	class NumericalDemo : IModel
	{
		delegate void TransferDeleg(Account fromAcc, Account toAcc);
		List<Account> m_accounts = new List<Account>();
		Interpretor kernel = Interpretor.Instance;
		AABB m_worldBox;
		double m_accumulate = 0;
		Random m_rand = new Random();
		SpaceTime m_initialST;
		IHierarchicalIdFactory m_idFactory = HierarchicalIdService.GetFactory();

		// trival
		double m_elapsed;
		bool m_pushFlag = true;

		public void Init(AABB worldBox)
		{
			m_worldBox = worldBox;

			m_initialST = new SpaceTime(m_idFactory.CreateFromRoot(), ITCEvent.CreateZero(), m_idFactory);
			var newAccount = m_initialST.CreateState((st, stamp) => new Account(st, stamp));
			m_accounts.Add(newAccount as Account);
			newAccount = m_initialST.CreateState((st, stamp) => new Account(st, stamp));
			m_accounts.Add(newAccount as Account);

			//kernel.Declare("CreateAccount"
			//                , FunctionForm.From(typeof(Accounting).GetMethod("CreateAccount")));

			kernel.Declare("Initiate", FunctionForm.From(typeof(Accounting).GetMethod("Initiate")));
			// declare a function form for an event, which also means binding an event to one or a few state types
			kernel.Declare("Transfer", FunctionForm.From(typeof(Accounting).GetMethod("Transfer")));

			//foreach (var p in m_particles)
			//{
			//    kernel.AwareOf("GodPush"
			//                   , typeof(NewtonPhysics).GetMethod("GodPush")
			//                   , new Action<Particle, Vector3>(NewtonPhysics.GodPush)
			//                   , p);
			//    //kernel.AwareOf("Collide", typeof(NewtonPhysics).GetMethod("Collide"), new Action<Particle,Particle>(NewtonPhysics.Collide) );
			//}

			foreach (var acc in m_accounts)
			{
				kernel.Call("Initiate", m_initialST, Utils.MakeArgList("account", acc), 900);
			}
		}

		void UpdateWorld(float interval)
		{
			if (m_elapsed > 2 && m_pushFlag)
			{
				kernel.Call("Transfer", m_initialST, Utils.MakeArgList("fromAcc", m_accounts[0], "toAcc", m_accounts[1]), m_rand.Next(50));
			}
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
			Account[] m_accounts;
			readonly AABB m_worldbox;
			public Present(List<Account> pts, AABB worldBox)
			{
				m_accounts = pts.ToArray();
				m_worldbox = worldBox;
			}
			public override void Render()
			{
				const float MAX_AMOUNT = 1000;
				var count = m_accounts.Length;
				var worldWidth = m_worldbox.Max.X - m_worldbox.Min.X;
				var worldHeight = m_worldbox.Max.Y - m_worldbox.Min.Y;
				var stepWidth = worldWidth / count;
				int index = 0;

				GL.Begin(BeginMode.Triangles);
				foreach (var p in m_accounts)
				{
					GL.Color4(Color4.DarkRed);
					GL.Vertex2(stepWidth * index, m_worldbox.Min.Y);
					GL.Vertex2(stepWidth * index, worldHeight);
					GL.Vertex2(stepWidth * index + (stepWidth / 2), worldHeight * p.Balance / MAX_AMOUNT);
					++index;
				}
				GL.End();
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
