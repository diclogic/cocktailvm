using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail;
using HTS;

namespace CollisionTest.States
{
    [State]
    public class Account : State
    {
        public Account(SpaceTime spaceTime, IHierarchicalTimestamp stamp) : base(spaceTime,stamp) { }
        public float Balance;
    }
}
