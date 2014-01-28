using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using OpenTK;
using CollisionTest;
using MathLib;

namespace Representation
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
        IPresenter GetPresent();
    }
}
