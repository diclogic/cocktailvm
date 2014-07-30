using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using MathLib;

namespace Skeleton
{

    //////////////////////////////////////////////////////////////////////////

    public class LockGuard : IDisposable
    {
        object m_lock;
        public LockGuard(object lockIn)
        {
            m_lock = lockIn;
            try
            {
                Monitor.Enter(m_lock);
            }
            catch (System.Exception)
            {
                m_lock = null;
                throw;
            }
        }
        public void Dispose()
        {
            Monitor.Exit(m_lock);
        }
    }

    //////////////////////////////////////////////////////////////////////////

    public interface IRenderer
    {
        IModel Model { set; get; }
        void Start();
        void Stop();
        LockGuard AcquireAuto();
        void Acquire();
        void Release();
        void ChangeViewport(int width, int height);
    }

    //////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// 
    /// </summary>
    public class Renderer : IRenderer
    {
        object update_lock = new object();
        Thread rendering_thread;
        bool viewport_changed = true;
        int viewport_width, viewport_height;
        GLControl m_glCtl;
        bool exit = false;

        IModel m_model;
        public IModel Model
        {
            get { return m_model; }
            set {
                value.Init(new AABB(new Vector3(2,2,2)) );
                lock (update_lock)
                {
                    m_model = value;
                }
            } 
        }

        public Renderer(GLControl glctl)
        {
            m_glCtl = glctl;
        }

        public void Start()
        {
            rendering_thread = new Thread(RenderThread);
            rendering_thread.IsBackground = true;
            rendering_thread.Start();
        }

        public void Stop()
        {
            exit = true; // Set a flag that the rendering thread should stop running.
            rendering_thread.Join();
        }

        public LockGuard AcquireAuto()
        {
            return new LockGuard(update_lock);
        }

        public void Acquire()
        {
            Monitor.Enter(update_lock);
        }

        public void Release()
        {
            Monitor.Exit(update_lock);
        }

        public void ChangeViewport(int width, int height)
        {
            lock (update_lock)
            {
                viewport_changed = true;
                viewport_height = height;
                viewport_width = width;
            }
        }

        void RenderThread()
        {
            m_glCtl.MakeCurrent(); // The context now belongs to this thread. No other thread may use it!

            m_glCtl.VSync = true;

            // Since we don't use OpenTK's timing mechanism, we need to keep time ourselves;
            Stopwatch render_watch = new Stopwatch();
            Stopwatch update_watch = new Stopwatch();
            update_watch.Start();
            render_watch.Start();

			GL.ClearColor(Color.FromArgb(45, 45, 45));
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.PointSmooth);

            while (!exit)
            {
                m_model.Update(this, update_watch.Elapsed.TotalSeconds);
                update_watch.Reset();
                update_watch.Start();

                Render(render_watch.Elapsed.TotalSeconds);
                render_watch.Reset(); //  Stopwatch may be inaccurate over larger intervals.
                render_watch.Start(); // Plus, timekeeping is easier if we always start counting from 0.

                m_glCtl.SwapBuffers();
            }

			//m_glCtl.Context.MakeCurrent(null);
        }

        /// <summary>
        /// This is our main rendering function, which executes on the rendering thread.
        /// </summary>
        public void Render(double time)
        {
            lock (update_lock)
            {
                if (viewport_changed)
                {
                    GL.Viewport(0, 0, viewport_width, viewport_height);
                    viewport_changed = false;
                }
            }

            Matrix4 perspective = Matrix4.CreateOrthographic(2, 2, -1, 1);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref perspective);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            m_model.GetPresent().Render();
        }


    }
}
