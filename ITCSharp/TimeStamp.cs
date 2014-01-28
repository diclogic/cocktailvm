



using System.Runtime.Serialization;
using System;
using System.Collections.Generic;

namespace itcsharp
{


    [Serializable]
    public sealed class TimeStamp : IEquatable<TimeStamp>, ICloneable
    {
        private readonly Identity m_id;
        private readonly Event m_event;

        public TimeStamp()
        {
            m_id = Identity.ONE;
            m_event = new Event(0);
        }

        internal TimeStamp(Identity id, Event eventIn)
        {
            this.m_id = id;
            this.m_event = eventIn;
        }

        public Pair<TimeStamp> Fork()
        {
            var ids = m_id.Fork();

            return new Pair<TimeStamp>(new TimeStamp(ids.First, m_event)
                                    , new TimeStamp(ids.Second, m_event));
        }

        public Pair<TimeStamp> Peek()
        {
            return new Pair<TimeStamp>(new TimeStamp(m_id, m_event), new TimeStamp(Identity.ZERO, m_event));
        }

        public static TimeStamp Join(TimeStamp s1, TimeStamp s2)
        {
            return new TimeStamp(Identity.Join(s1.m_id, s2.m_id), Event.Join(s1.m_event, s2.m_event));
        }

        private static Event Fill(Identity id, Event eventIn)
        {
            NormalizeInitFlag f;

            if (id.IsZero())
                return eventIn;
            if (id.IsOne())
                return new Event(Event.Max(eventIn));
            if (eventIn.IsSimplex())
                return new Event(eventIn.N);
            if (id.Left != null && id.Left.IsOne())
            {
                Event er = Fill(id.Right, eventIn.Right);
                int max = Math.Max(Event.Max(eventIn.Left), Event.Min(er));
                return new Event(eventIn.N, new Event(max), er, f);
            }
            if (id.Right != null && id.Right.IsOne())
            {
                Event el = Fill(id.Left, eventIn.Left);
                int max = Math.Max(Event.Max(eventIn.Right), Event.Min(el));
                return new Event(eventIn.N, el, new Event(max), f);
            }
            return new Event(eventIn.N
                        , Fill(id.Left, eventIn.Left)
                        , Fill(id.Right, eventIn.Right)
                        , f);
        }

        private static Pair<Event, int> Grow(Identity id, Event eventIn)
        {
            if (id.IsOne() && eventIn.IsSimplex())
                return new Pair<Event, int>(new Event(eventIn.N + 1), 0);
            if (eventIn.IsSimplex())
            {
                var ret = Grow(id, new Event(eventIn.N, new Event(0), new Event(0)));
                ret.Second += eventIn.MaxDepth() + 1;
                return ret;
            }
            if (id.Left != null && id.Left.IsZero())
            {
                var ret = Grow(id.Right, eventIn.Right);
                var e = new Event(eventIn.N, eventIn.Left, ret.First);
                return new Pair<Event,int>(e, ret.Second + 1);
            }
            if (id.Right != null && id.Right.IsZero())
            {
                var ret = Grow(id.Left, eventIn.Left);
                var e = new Event(eventIn.N, ret.First, eventIn.Right);
                return new Pair<Event, int>(e, ret.Second + 1);
            }
            var left = Grow(id.Left, eventIn.Left);
            var right = Grow(id.Right, eventIn.Right);
            if (left.Second < right.Second)
            {
                Event e = new Event(eventIn.N, left.First, eventIn.Right);
                return new Pair<Event, int>(e, left.Second + 1);
            }
            else
            {
                Event e = new Event(eventIn.N, eventIn.Left, right.First);
                return new Pair<Event, int>(e, right.Second + 1);
            }
        }

        public TimeStamp FireEvent()
        {
            Event e = Fill(m_id, m_event);
            if (!m_event.Equals(e))
                return new TimeStamp(m_id, e);
            else
            {
                var ret = Grow(m_id, m_event);
                return new TimeStamp(m_id, ret.First);
            }
        }

        public override string ToString()
        {
            return "{" + m_id + " | " + m_event + "}";
        }

        public object Clone()
        {
            return CloneT();
        }

        public TimeStamp CloneT()
        {
            return new TimeStamp(m_id.CloneT(), m_event.CloneT());
        }

        public override bool Equals(object rhs)
        {
            if (rhs == null || rhs.GetType() != typeof(TimeStamp))
                return false;
            return Equals(rhs);
        }

        public bool Equals(TimeStamp rhs)
        {
            if (rhs == null)
                return false;

            return m_id.Equals(rhs.m_id) && m_event.Equals(rhs.m_event);
        }

        public override int GetHashCode()
        {
            return m_id.GetHashCode() ^ m_event.GetHashCode();
        }

        public static Pair<TimeStamp> Send(TimeStamp s)
        {
            return s.FireEvent().Peek();
        }

        public static TimeStamp Receive(TimeStamp s1, TimeStamp s2)
        {
            return Join(s1, s2).FireEvent();
        }

        public static Pair<TimeStamp> Sync(TimeStamp s1, TimeStamp s2)
        {
            return Join(s1, s2).Fork();
        }

        /// <summary>
        /// Less than or equal to.
        /// </summary>
        public static bool Leq(TimeStamp s1, TimeStamp s2)
        {
            return itcsharp.Event.Leq(s1.m_event, s2.m_event);
        }
    }
}