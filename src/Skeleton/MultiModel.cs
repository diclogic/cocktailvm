using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MathLib;

namespace Skeleton
{
	public class MultiModel : BaseModel
	{
		private List<BaseModel> m_modelCache = new List<BaseModel>();
		private BaseModel m_currentModel = null;
		private AABB m_worldBox = new AABB();
		private readonly IPresenter m_nullPresenter = new NullPresenter();

		public override void Init(AABB worldBox)
		{
			m_worldBox = worldBox;

			FireActionAssignmentChanged(1, "load Demos.NumericalDemo");
		}

		public override void Update(IRenderer renderer, double time, IEnumerable<string> controlCmds)
		{
			foreach (var cmd in controlCmds)
			{
				var args = cmd.Split(' ');
				if (args[0] == "load")
					Load(args[1]);
				else if (args[0] == "unload")
					Unload(args[1]);
			}

			if (m_currentModel != null)
				m_currentModel.Update(renderer, time, controlCmds);
		}

		public void Load(string hint)
		{
			Type type = null;
			foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
			{
				var tt = asm.GetTypes().FirstOrDefault(t => t.Name == hint || t.FullName == hint);
				if (tt != null)
				{
					type = tt;
					break;
				}
			}

			if (type == null)
				throw new ApplicationException(string.Format("Can't find the class: {0}", hint));

			var existingModel = m_modelCache.FirstOrDefault(m => m.GetType() == type);
			if (existingModel != null)
			{
				existingModel.Init(m_worldBox);
				m_currentModel = existingModel;
				return;
			}

			var newModel = Activator.CreateInstance(type) as BaseModel;
			if (newModel == null)
				throw new ApplicationException(string.Format("The given class is not inherited from IModel: {0}", type.FullName));

			newModel.Init(m_worldBox);
			m_modelCache.Add(newModel);
			m_currentModel = newModel;
		}

		public void Unload(string hint)
		{
			if (m_modelCache.Count <= 1)
				return;

			var idx = m_modelCache.FindIndex(m => m.GetType().FullName == hint);
			if (idx != -1)
				m_modelCache.RemoveAt(idx);
		}

		public override IPresenter GetPresent()
		{
			if (m_currentModel == null)
				return m_nullPresenter;

			return m_currentModel.GetPresent();
		}

		private class NullPresenter : IPresenter
		{
			public void PreRender() { }
			public void Render() { }
			public void PostRender() { }
		}
	}
}
