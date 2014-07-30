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


    public partial class LauncherWindow : Form
    {

        internal LauncherWindow(IActionListener actionListener)
		{
			InitializeComponent();

			// fit the GL window for mac osx
			if (Type.GetType("Mono.Runtime") != null)
				this.glControl1.Size = new System.Drawing.Size(494, 341);

			this.glControl1.Paint += new System.Windows.Forms.PaintEventHandler(this.glControl1_Paint);
			this.glControl1.Load += new System.EventHandler(this.glControl1_Load);
			this.button1.Click += (sender,_) => OnButtonClick((Button)sender, actionListener);
			this.button2.Click += (sender,_) => OnButtonClick((Button)sender, actionListener);
			this.button3.Click += (sender,_) => OnButtonClick((Button)sender, actionListener);
		}

		public GLControl GetGLWindow() { return this.glControl1; }

		void OnButtonClick(Button sender, IActionListener listener)
		{
			var match = Regex.Match(((Button)sender).Name, "button([0-9])");
			if (match.Groups.Count != 2)
				return;

			var buttonNumber = int.Parse(match.Groups[1].Captures[0].Value);
			listener.OnAction(buttonNumber);
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
