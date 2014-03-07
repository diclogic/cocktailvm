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
		[StateField(PatchKind=FieldPatchKind.Delta)]
		public float Balance;

        public Account(Spacetime spaceTime, IHTimestamp stamp) : base(spaceTime,stamp) { }
		public override bool Merge(StateSnapshot snapshot, StatePatch patch)
		{
			if (Balance != (float)snapshot.Fields.First(f=>f.Name == "Balance").Value)
				return false;

			return base.Merge(snapshot, patch);
		}
    }
}
