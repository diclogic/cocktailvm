using System.Collections.Generic;
using System.Text;
using Cocktail;
using Common;
using Cocktail.HTS;
using OpenTK;
using Cocktail.Interp;

namespace Demos.States
{
	[State]
	public class Particle : State
	{
		public float mass = 1;
		public Vector3 pt = new Vector3(0, 0, 0);
		public float radius = 1;
		public Vector3 accel = new Vector3(0, 0, 0);
		public Vector3 velocity = new Vector3(0, 0, 0);

        public Particle( IHTimestamp creationStamp)
            : base( creationStamp)
        {

        }

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(base.ToString());
			sb.AppendFormat("{{mass = {0}, pt = {1}, radius = {2}, impulse = {3} }}"
							, mass
							, Vector3Extension.ToString(pt)
							, radius
							, Vector3Extension.ToString(accel));
			return sb.ToString();
		}
	}

	public delegate List<Vector3> CollideDeleg(IEnumerable<StateParamInst> states, EStyle style);
	public delegate void GodPushDeleg(IEnumerable<StateParamInst> states, Vector3 force);

}
