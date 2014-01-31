using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using itc = itcsharp;

namespace CollisionTest
{
    /// <summary>
    /// the SpaceTime represents the development of objects
    /// it is a thread apartment that can include one to many objects
    /// 1) An activated object must be in an spaceTime.
    /// 2) 2 SpaceTimes can merge into 1
    /// 3) SpaceTime cannot cross machine boundary (we need something else to do distributed transaction)
    /// </summary>
    public class SpaceTime
    {
        private IHierarchicalId m_id;
        private IHierarchicalIdFactory m_idFactory;
        private IHierarchicalTimestamp m_currentTime;
        public SpaceTime(IHierarchicalId id, IHierarchicalIdFactory idFactory)
        {
            m_id = id;
            m_idFactory = idFactory;
        }

        public State CreateState(Func<IHierarchicalTimestamp,State> constructor)
        {
            var event_ = BeginAdvance();
            var retval = constructor(m_currentTime);
            CommitAdvance(event_);
            return retval;
        }

        public SpaceTime Fork()
        {
			var newIdForNewST = m_idFactory.CreateSiblingsOf(m_id);
            var retval = new SpaceTime(newIdForNewST, m_idFactory);
            //TODO: init thread?
            return retval;
        }

        public void Join(SpaceTime spaceTime)
        {

        }

        public IHierarchicalEvent Advance()
        {
            var event_ = BeginAdvance();
            CommitAdvance(event_);
            return event_;
        }

        private IHierarchicalEvent BeginAdvance()
        {
            var newTime = m_currentTime.FireEvent();
            return newTime.Event;
        }

        private bool CommitAdvance(IHierarchicalEvent event_)
        {
            if (m_currentTime.Event.LtEq(event_))
            {
                m_currentTime = new ITCTimestamp(m_currentTime.ID as ITCIdentity, event_ as ITCEvent);
                return true;
            }

            return false;
        }
    }
}
