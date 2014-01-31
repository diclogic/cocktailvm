using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using itc = itcsharp;

namespace CollisionTest.States
{
    [State]
    public class Account : State
    {
        public Account(IHierarchicalTimestamp stamp) : base(stamp) { }
        public float Balance;
    }
}
