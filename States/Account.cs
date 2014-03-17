using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail;
using HTS;
using System.IO;

namespace CollisionTest.States
{
    [State]
    public class Account : State
    {
		[StateField(PatchKind=FieldPatchKind.CommutativeDelta)]
		public float Balance;

        public Account(Spacetime spaceTime, IHTimestamp stamp) : base(spaceTime,stamp) { }
		public Account(TStateId sid, Spacetime spaceTime, IHTimestamp stamp) : base(sid, spaceTime, stamp, StatePatchMethod.Auto) { }
    }
}
