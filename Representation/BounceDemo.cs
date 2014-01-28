using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using CollisionTest;
using MathLib;

namespace Representation
{
    public class BounceModel : IModel
    {
        internal struct Particle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color4 Color;
        }
        public class PresentMetadata: BasePresenter
        {
            List<Particle> m_particles;
            internal PresentMetadata(List<Particle> particles)
            {
                m_particles = new List<Particle>(particles);
            }
            public override void Render()
            {
                GL.PointSize(16);
                GL.Begin(BeginMode.Points);
                foreach (Particle p in m_particles)
                {
                    GL.Color4(p.Color);
                    GL.Vertex2(p.Position);
                }
                GL.End();
            }
        }
        Random rand = new Random();
        const float GravityAccel = -9.81f;
        List<Particle> Particles = new List<Particle>();
        bool position_changed = true;
        int position_x, position_y;
        float position_dx, position_dy;

        public void Init(AABB worldBox)
        {
            for (int i = 0; i < 64; i++)
            {
                Particle p = new Particle();
                p.Position = new Vector2((float)rand.NextDouble() * 2 - 1, (float)rand.NextDouble() * 2 - 1);
                p.Color.R = (float)rand.NextDouble();
                p.Color.G = (float)rand.NextDouble();
                p.Color.B = (float)rand.NextDouble();
                Particles.Add(p);
            }
        }

        public void InitMovement(int x, int y)
        {
            position_x = x;
            position_y = y;
        }
        public void TriggerMovement(int x, int y, int mx, int my)
        {
            position_changed = true;
            position_dx = (position_x - x) / (float)mx;
            position_dy = (position_y - y) / (float)my;
            position_x = x;
            position_y = y;
        }

        public void Update(IRenderer renderer, double time)
        {
            using (var g = renderer.AcquireAuto())
            {
                // When the user moves the window we make the particles react to
                // this movement. The reaction is semi-random and not physically
                // correct. It looks quite good, however.
                if (position_changed)
                {
                    for (int i = 0; i < Particles.Count; i++)
                    {
                        Particle p = Particles[i];
                        p.Velocity += new Vector2(
                            16 * (position_dx + 0.05f * (float)(rand.NextDouble() - 0.5)),
                            32 * (position_dy + 0.05f * (float)(rand.NextDouble() - 0.5)));
                        Particles[i] = p;
                    }

                    position_changed = false;
                }
            }

            // For simplicity, we use simple Euler integration to simulate particle movement.
            // This is not accurate, especially under varying timesteps (as is the case here).
            // A better solution would have been time-corrected Verlet integration, as
            // described here:
            // http://www.gamedev.net/reference/programming/features/verlet/
            for (int i = 0; i < Particles.Count; i++)
            {
                Particle p = Particles[i];

                p.Velocity.X = Math.Abs(p.Position.X) >= 1 ? -p.Velocity.X * 0.92f : p.Velocity.X * 0.97f;
                p.Velocity.Y = Math.Abs(p.Position.Y) >= 1 ? -p.Velocity.Y * 0.92f : p.Velocity.Y * 0.97f;
                if (p.Position.Y > -0.99)
                {
                    p.Velocity.Y += (float)(GravityAccel * time);
                }
                else
                {
                    if (Math.Abs(p.Velocity.Y) < 0.02)
                    {
                        p.Velocity.Y = 0;
                        p.Position.Y = -1;
                    }
                    else
                    {
                        p.Velocity.Y *= 0.9f;
                    }
                }

                p.Position += p.Velocity * (float)time;
                if (p.Position.Y <= -1)
                    p.Position.Y = -1;

                Particles[i] = p;
            }
        }


        public IPresenter GetPresent()
        {
            return new PresentMetadata(Particles);
        }

    }
}
