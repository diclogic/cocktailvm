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
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;

namespace Launcher
{


    public partial class Form1 : Form
    {
        IModel m_model;
        Renderer m_renderer;
		Dictionary<int, string> m_actions = new Dictionary<int,string>();

        public Form1()
        {
            InitializeComponent();

            this.glControl1.Paint += new System.Windows.Forms.PaintEventHandler(this.glControl1_Paint);
            this.glControl1.Load += new System.EventHandler(this.glControl1_Load);
			this.button1.Click += new EventHandler(OnButtonClick);
			this.button2.Click += new EventHandler(OnButtonClick);

			var args = Environment.GetCommandLineArgs();

            m_renderer = new Renderer(this.glControl1);
			m_model = LoadModel(args.ElementAtOrDefault(1));
			m_model.ActionMapAssigned += OnModelActionMapAssigned;

            m_renderer.Model = m_model;

			this.Move += new EventHandler(Form1_Move);
            this.Resize += (_,__) => 
                m_renderer.ChangeViewport(this.glControl1.Width, this.glControl1.Height);

            //HACK: window movement as input for BOUNCE
			m_model.Input(string.Format("move init {0} {1}", this.Left, this.Top));

            m_renderer.ChangeViewport(this.glControl1.Width, this.glControl1.Height);
        }

		private IModel LoadModel(string hint)
		{
			var demosDll = Assembly.Load(AssemblyName.GetAssemblyName("Demos.dll"));

			string typeName = string.Empty;
			if (string.IsNullOrEmpty(hint))
				typeName = typeof(MultiModel).AssemblyQualifiedName;
			else
			{
				foreach (var tt in demosDll.GetTypes())
				{
					if (0 == string.Compare(tt.FullName, hint, true))
					{
						typeName = tt.AssemblyQualifiedName;
						break;
					}
				}
			}

			var modelType = Type.GetType(typeName);
			if (modelType == null)
				throw new ApplicationException(string.Format("Can't find model with name: {0}", typeName));
			return (IModel)Activator.CreateInstance(modelType);
		}

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
        }

        protected override void  OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            m_renderer.Stop();
            base.OnClosing(e);
        }

		protected void OnModelActionMapAssigned(IEnumerable<KeyValuePair<int,string>> mapping)
		{
			m_actions = new Dictionary<int, string>();
			foreach (var kv in mapping)
				m_actions[kv.Key] = kv.Value;
		}

		void OnButtonClick(object sender, EventArgs e)
		{
			var match = Regex.Match(((Button)sender).Name, "button([0-9])");
			if (match.Groups.Count != 2)
				return;

			var buttonNumber = int.Parse(match.Groups[1].Captures[0].Value);

			string cmd;
			if (m_actions.TryGetValue(buttonNumber, out cmd))
				m_model.Input(cmd);
		}

		void Form1_Move(object sender, EventArgs e)
		{
			//(m_model as BounceModel).TriggerMovement(this.Left, this.Top, this.glControl1.Width, this.glControl1.Height);
			m_model.Input(string.Format("move {0} {1} {2} {3}", Left, Top, glControl1.Width, glControl1.Height));
		}

        private void glControl1_Load(object sender, EventArgs e)
        {
            glControl1.Context.MakeCurrent(null); // Release the OpenGL context so it can be used on the new thread.
            m_renderer.Start();
        }

        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            Thread.Sleep(1);
        }

    }


}
