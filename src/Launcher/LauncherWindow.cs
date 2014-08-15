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


    public partial class LauncherWindow : Form, IControlPanel
    {
		Dictionary<Button, int> m_buttons = new Dictionary<Button, int>();

		public event ActionTriggeredDeleg ActionTriggered;

        internal LauncherWindow()
		{
			InitializeComponent();

			// fit the GL window for mac osx
			if (Type.GetType("Mono.Runtime") != null)
				this.glControl1.Size = new System.Drawing.Size(494, 341);

			this.glControl1.Paint += new System.Windows.Forms.PaintEventHandler(this.glControl1_Paint);
			this.glControl1.Load += new System.EventHandler(this.glControl1_Load);

			for (int ii = 0; ii < Controls.Count; ++ii)
			{
				if (Controls[ii] is Button)
					m_buttons.Add(Controls[ii] as Button, 0);
			}

			var btCtls = m_buttons.Keys.ToArray();

			foreach (var b in btCtls)
			{
				var match = Regex.Match(b.Name, "button([0-9]+)");
				if (match.Groups.Count != 2)
					return;

				var buttonNumber = int.Parse(match.Groups[1].Captures[0].Value);
				m_buttons[b] = buttonNumber;

				b.Click += (sender,_) => OnButtonClick((Button)sender);
			}
		}

		public GLControl GetGLWindow() { return this.glControl1; }

		public void SetButtonText(int idx, string text)
		{
			m_buttons.First(kv => kv.Value == idx).Key.Text = text;
		}

		void OnButtonClick(Button sender)
		{
			if (ActionTriggered != null)
				ActionTriggered(m_buttons[sender]);
		}

        private void glControl1_Load(object sender, EventArgs e)
        {
            glControl1.Context.MakeCurrent(null); // Release the OpenGL context so it can be used on the new thread.
        }

        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            Thread.Sleep(1);
        }

	}


}
