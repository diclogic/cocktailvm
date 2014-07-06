using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MathLib;

namespace Skeleton
{
    public interface IPresenter
    {
        void PreRender();
        void Render();
        void PostRender();
    }

    public class BasePresenter : IPresenter
    {
        public virtual void PreRender() { }
        public virtual void Render() { }
        public virtual void PostRender() { }
    }

	public delegate void ActionAssignmentChangedDleg(IEnumerable<KeyValuePair<int,string>> mapping);

    public interface IModel
    {
        void Init(AABB worldBox);
		void Update(IRenderer renderer, double time);
		void Input(string controlCmd);
        IPresenter GetPresent();

		event ActionAssignmentChangedDleg ActionMapAssigned;
    }

	public abstract class BaseModel : IModel
	{
		protected List<string> m_controlCmdQueue = new List<string>();
		protected Dictionary<int, string> m_actionMap = new Dictionary<int, string>();

		public event ActionAssignmentChangedDleg ActionMapAssigned;


		public virtual void Init(AABB worldBox) { }
		public abstract IPresenter GetPresent();

		public void Update(IRenderer renderer, double time)
		{
			List<string> controlCmds;
			lock (m_controlCmdQueue)
			{
				controlCmds = m_controlCmdQueue;
				m_controlCmdQueue = new List<string>();
			}

			Update(renderer, time, controlCmds);
		}

		public virtual void Update(IRenderer renderer, double time, IEnumerable<string> controlCmds)
		{
		}

		public void Input(string controlCmd)
		{
			lock (m_controlCmdQueue)
				m_controlCmdQueue.Add(controlCmd);
		}

		public void RegisterAction(int actionReg, string command)
		{
			m_actionMap[actionReg] = command;
			FireActionMapAssigned(m_actionMap);
		}
		public Dictionary<int, string> GetActionMap()
		{
			return m_actionMap;
		}

		protected void FireActionMapAssigned(IEnumerable<KeyValuePair<int,string>> mapping)
		{
			if (ActionMapAssigned != null)
			{
				ActionMapAssigned(mapping);
			}
		}
	}
}
