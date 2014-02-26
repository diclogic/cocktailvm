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
        public Account(Spacetime spaceTime, IHierarchicalTimestamp stamp) : base(spaceTime,stamp) { }
        public float Balance;

		public override bool Merge(State rhs)
		{
			var rhsAcc = rhs as Account;
			if (Balance != rhsAcc.Balance)
				return false;
			return true;
		}

		public override bool Patch(IHierarchicalEvent fromRev, IHierarchicalEvent toRev, Stream delta)
		{
			throw new Exception("The method or operation is not implemented.");
		}

		protected override void AddPatch(Stream delta)
		{
			throw new Exception("The method or operation is not implemented.");
		}
    }
}
