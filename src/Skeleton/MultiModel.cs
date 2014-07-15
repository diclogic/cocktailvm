using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MathLib;

namespace Skeleton
{
	public class MultiModelContentAttribute : Attribute { }

	public class MultiModel : BaseModel
	{
		private List<BaseModel> m_modelCache = new List<BaseModel>();
		private BaseModel m_currentModel = null;
		private AABB m_worldBox = new AABB();
		private readonly IPresent m_nullPresenter = new NullPresent();
		private List<string> m_modelNames = new List<string>();
		private int m_loopIndex = -1;

		public override void Init(AABB worldBox)
		{
			m_worldBox = worldBox;
			CollectAllModel();
			RegisterAction(1, "next");
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
				else if (args[0] == "next")
					Next();
			}

			if (m_currentModel != null)
				m_currentModel.Update(renderer, time, controlCmds);
		}

		private void CollectAllModel()
		{
			m_modelNames.Clear();

			foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
				foreach (var type in asm.GetTypes())
				{
					if (0 < type.GetCustomAttributes(typeof(MultiModelContentAttribute), false).Length)
						m_modelNames.Add(type.FullName);
				}
		}

		public void Next()
		{
			if (m_modelNames.Count <= 0)
				return;

			if (m_loopIndex >= 0)
			{
				var prevName = m_modelNames[m_loopIndex % m_modelNames.Count];
				Unload(prevName);
			}

			Load(m_modelNames[(++m_loopIndex) % m_modelNames.Count]);
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
				if (existingModel == m_currentModel)
					return;

				m_currentModel.ActionMapAssigned -= OnSubmodelActionMapAssigned;
				existingModel.ActionMapAssigned += OnSubmodelActionMapAssigned;
				existingModel.Init(m_worldBox);
				m_currentModel = existingModel;
				return;
			}

			var newModel = Activator.CreateInstance(type) as BaseModel;
			if (newModel == null)
				throw new ApplicationException(string.Format("The given class is not inherited from IModel: {0}", type.FullName));

			newModel.ActionMapAssigned += OnSubmodelActionMapAssigned;
			newModel.Init(m_worldBox);
			m_modelCache.Add(newModel);
			m_currentModel = newModel;
		}

		public void Unload(string hint)
		{
			if (m_modelCache.Count <= 1)
				return;

			var idx = m_modelCache.FindIndex(m => m.GetType().FullName == hint);
			if (idx == -1)
				return;

			var model = m_modelCache[idx];
			model.ActionMapAssigned -= OnSubmodelActionMapAssigned;
			m_modelCache.RemoveAt(idx);
		}

		public override IPresent GetPresent()
		{
			if (m_currentModel == null)
				return m_nullPresenter;

			return m_currentModel.GetPresent();
		}

		private void OnSubmodelActionMapAssigned(IEnumerable<KeyValuePair<int, string>> mapping)
		{
			FireActionMapAssigned(mapping.Union(this.GetActionMap()));
		}
	}
}
