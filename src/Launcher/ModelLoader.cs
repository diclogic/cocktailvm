using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Skeleton;
using System.Reflection;

namespace Launcher
{
	internal class ModelLoader
	{
		public IModel LoadModel(string hint)
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
	}
}
