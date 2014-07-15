using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using MathLib;
using System.Reflection;
using Cocktail;
using Demos.States;
using Skeleton;

namespace Demos
{
    class CollisionDemo : BaseModel
    {
        delegate void GodPushDeleg(Particle p,Vector3 v);
        List<Particle> m_particles = new List<Particle>();
        Interpreter kernel = Interpreter.Instance;
        AABB m_worldBox;
        double m_accumulate = 0;
        Random m_rand = new Random();

        // trival
        double m_elapsed;
        bool m_pushFlag = true;

        public void Init(AABB worldBox)
        {
            m_worldBox = worldBox;
            //for (int ii = 0; ii < 10; ++ii )
            //    m_particles.Add(new Particle(new ITCTimestamp()) { pt = worldBox.RandomPoint(m_rand) });

            // declare a function form for an event, which also means binding an event to one or a few state types
            kernel.Declare("GodPush"
                            , FunctionForm.From(typeof(NewtonPhysics).GetMethod("GodPush")));

            //foreach (var p in m_particles)
            //{
            //    kernel.AwareOf("GodPush"
            //                   , typeof(NewtonPhysics).GetMethod("GodPush")
            //                   , new Action<Particle, Vector3>(NewtonPhysics.GodPush)
            //                   , p);
            //    //kernel.AwareOf("Collide", typeof(NewtonPhysics).GetMethod("Collide"), new Action<Particle,Particle>(NewtonPhysics.Collide) );
            //}
        }

        void UpdateWorld(float interval)
        {
            if (m_elapsed > 2 && m_pushFlag)
            {
                // (p).GodPush([1,1,0]);
                m_pushFlag = false;
                foreach (var p in m_particles)
                    kernel.Callva("GodPush", null, InterpUtils.MakeArgList("body", new LocalStateRef<Particle>(p)), new Vector3(1.0f,1.0f,0).Random(m_rand));
            }

            foreach (var p in m_particles)
                NewtonPhysics.Move(p, interval, m_worldBox);
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

        public class Present : BasePresent
        {
            Particle[] m_particles;
            public Present(List<Particle> pts)
            {
                m_particles = pts.ToArray();
            }
            public override void Render()
            {
                GL.PointSize(16);
                GL.Begin(BeginMode.Points);
                foreach (var p in m_particles)
                {
                    
                    GL.Color4(Color4.DarkRed);
                    GL.Vertex2(p.pt.Xy);
                }
                GL.End();
            }
        }
        public override IPresent GetPresent()
        {
            return new Present(m_particles);
        }

        // Internal

        void Dynamic()
        {

        }
    }
}
