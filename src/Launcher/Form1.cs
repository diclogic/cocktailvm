using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using Skeleton;
using Demos;

namespace Launcher
{


    public partial class Form1 : Form
    {
        IModel m_model;
        Renderer m_renderer;

        public Form1()
        {
            InitializeComponent();

            this.glControl1.Paint += new System.Windows.Forms.PaintEventHandler(this.glControl1_Paint);
            this.glControl1.Load += new System.EventHandler(this.glControl1_Load);

            m_renderer = new Renderer(this.glControl1);
			m_model = LoadModel("");
            m_renderer.Model = m_model;

            this.Resize += (_,__) => 
            {
                m_renderer.ChangeViewport(this.glControl1.Width, this.glControl1.Height);
            };

            //HACK: window movement as input for BOUNCE
			if (m_model.GetType() == typeof(BounceModel))
			{
				this.Move += (_, __) =>
				{
					using (var gd = m_renderer.AcquireAuto())
					{
						(m_model as BounceModel).TriggerMovement(this.Left, this.Top, this.glControl1.Width, this.glControl1.Height);
					}
				};

				(m_model as BounceModel).InitMovement(this.Left, this.Top);
			}

            m_renderer.ChangeViewport(this.glControl1.Width, this.glControl1.Height);
        }

		private IModel LoadModel(string hint)
		{
			string fullname = null;
			if (string.IsNullOrEmpty(hint))
			{
				hint = "Representation.NumericalDemo";
				fullname = typeof(NumericalDemo).AssemblyQualifiedName;
			}

            //m_model = new BounceModel();
            //m_model = new CollisionDemo();
			return (IModel)Activator.CreateInstance(Type.GetType(fullname));
		}

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
        }

        private void glControl1_Load(object sender, EventArgs e)
        {
            glControl1.Context.MakeCurrent(null); // Release the OpenGL context so it can be used on the new thread.
            m_renderer.Start();
        }

        protected override void  OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            m_renderer.Stop();
            base.OnClosing(e);
        }

        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            Thread.Sleep(1);
        }

    }


}
