using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using itcsharp;

namespace CollisionTest
{
    /// <summary>
    /// the SpaceTime represents the development of objects
    /// it is a thread apartment that can include one to many objects
    /// 1) An activated object must be in an spaceTime.
    /// 2) 2 SpaceTimes can merge into 1
    /// 3) SpaceTime cannot cross machine boundary (we need something else to do distributed transaction)
    /// </summary>
    class SpaceTime
    {
        private Identity m_id;
        private SpaceTime(Identity id)
        {
            m_id = id;
        }

        SpaceTime Fork()
        {

        }
    }
}
