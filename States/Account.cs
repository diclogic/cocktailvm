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

        public Account( IHTimestamp stamp) : base(stamp) { }
		public Account(TStateId sid, IHTimestamp stamp) : base(sid, stamp.ID, stamp.Event, StatePatchMethod.Auto) { }
    }
}
