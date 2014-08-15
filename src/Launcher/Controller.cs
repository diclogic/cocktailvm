using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Skeleton;
using System.Windows.Forms;

namespace Launcher
{

	public delegate void ActionTriggeredDeleg(int idx);

	public interface IControlPanel
	{
		void SetButtonText(int idx, string text);
		event ActionTriggeredDeleg ActionTriggered;
	}

	internal class Controller
	{
        IModel m_model;
        IRenderer m_renderer;
		Dictionary<int, string> m_actions = new Dictionary<int, string>();
		IControlPanel m_panel;

		public IModel Model { get { return m_model; } }
		public IRenderer Renderer { get { return m_renderer; } }

		public void Initialize(IRenderer renderer, IModel model, IControlPanel panel, Form mainWindow, UserControl viewportWindow)
		{
			m_renderer = renderer;
			m_model = model;
			m_panel = panel;

			m_model.ActionMapAssigned += OnModelActionMapAssigned;
            m_renderer.Model = m_model;

			m_panel.ActionTriggered += OnAction;

			mainWindow.Move += (_,__) =>
				m_model.Input(string.Format("move {0} {1} {2} {3}", mainWindow.Left, mainWindow.Top
																, viewportWindow.Width, viewportWindow.Height));

            mainWindow.Resize += (_,__) => 
                m_renderer.ChangeViewport(viewportWindow.Width, viewportWindow.Height);

			viewportWindow.Load += (_, __) => m_renderer.Start();
			viewportWindow.HandleDestroyed += (_, __) => m_renderer.Stop();

            //HACK: window movement as input for BOUNCE
			m_model.Input(string.Format("move init {0} {1}", mainWindow.Left, mainWindow.Top));

			// init viewport size
            m_renderer.ChangeViewport(viewportWindow.Width, viewportWindow.Height);
		}

		protected void OnModelActionMapAssigned(IEnumerable<KeyValuePair<int,string>> mapping)
		{
			m_actions = new Dictionary<int, string>();
			foreach (var kv in mapping)
			{
				m_actions[kv.Key] = kv.Value;
				m_panel.SetButtonText(kv.Key, kv.Value);
			}
		}

		public void OnAction(int idx)
		{
			string cmd;
			if (m_actions.TryGetValue(idx, out cmd))
				m_model.Input(cmd);
		}
	}
}
