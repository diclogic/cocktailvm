using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using OpenTK;
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

    public interface IModel
    {
        void Init(AABB worldBox);
		void Update(IRenderer renderer, double time);
		void Update(IRenderer renderer, double time, IEnumerable<string> controlCmds);
        IPresenter GetPresent();
    }

	public abstract class BaseModel : IModel
	{
		public virtual void Init(AABB worldBox) { }
		public virtual void Update(IRenderer renderer, double time) { Update(renderer, time, Enumerable.Empty<string>()); }
		public virtual void Update(IRenderer renderer, double time, IEnumerable<string> controlCmds) { }
		public abstract IPresenter GetPresent();
	}
}
