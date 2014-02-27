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

        public Account(Spacetime spaceTime, IHierarchicalTimestamp stamp) : base(spaceTime,stamp) { }
		public override bool Merge(State rhs)
		{
			var rhsAcc = rhs as Account;
			if (Balance != rhsAcc.Balance)
				return false;
			return true;
		}

		public virtual void SerializePatch(Stream ostream, State oldState)
		{
			StatePatcher.GeneratePatch(ostream, this, oldState);
		}
    }
}
