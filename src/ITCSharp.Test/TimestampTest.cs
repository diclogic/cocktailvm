using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Extensions;

using itcsharp;

namespace UnitTests.ITCTests
{
    public class TimestampTest
    {
        [Fact]
        public void TestStampAll()
        {
            TimeStamp stamp = new TimeStamp();
            var fork1 = stamp.Fork();
            //System.out.println("fork1[0] = " + fork1[0]);
            //System.out.println("fork1[1] = " + fork1[1]);
            TimeStamp event1 = fork1.First.FireEvent();
            //System.out.println("event1 = " + event1);
            TimeStamp event2 = fork1.Second.FireEvent().FireEvent();
            //System.out.println("event2 = " + event2);
            var fork2 = event1.Fork();
            //System.out.println("fork2[0] = " + fork2[0]);
            //System.out.println("fork2[1] = " + fork2[1]);
            TimeStamp event11 = fork2.First.FireEvent();
            //System.out.println("event11 = " + event11);
            TimeStamp join1 = TimeStamp.Join(fork2.Second, event2);
            //System.out.println("join1 = " + join1);
            var fork22 = join1.Fork();
            //System.out.println("fork22[0] = " + fork22[0]);
            //System.out.println("fork22[1] = " + fork22[1]);
            TimeStamp join2 = TimeStamp.Join(fork22.First, event11);
            //System.out.println("join2 = " + join2);
            TimeStamp event3 = join2.FireEvent();
            //System.out.println("event3 = " + event3);
            Assert.Equal(new TimeStamp(Identity.Create(Identity.ONE, Identity.ZERO), new Event(2)), event3);
        }

        [Fact]
        public void TestStampLeq()
        {
            TimeStamp s1 = new TimeStamp();
            TimeStamp s2 = new TimeStamp();
            Assert.True(TimeStamp.Leq(s1, s2.FireEvent()));
            Assert.False(TimeStamp.Leq(s2.FireEvent(), s1));
        }
    }

    public class IDTest
    {
        [Fact]
        public void TestIDNorm0()
        {
            Assert.Equal(Identity.ZERO,
                Identity.Create(Identity.ZERO, Identity.Create(Identity.ZERO, Identity.ZERO)));//norm(0, (0, 0))
        }

        [Fact]
        public void TestIDNorm1()
        {
            Assert.Equal(Identity.ONE,
                Identity.Create(Identity.ONE, Identity.Create(Identity.ONE, Identity.ONE)));//norm(1, (1, 1))
        }
    }

    public class EventTest
    {
        [Fact]
        public void TestNorm()
        {
            Assert.Equal(new Event(3),
                    new Event(2, new Event(1), new Event(1), NormalizeInitFlag.Flag));//norm(2, 1, 1)

            Assert.Equal(new Event(4, new Event(0, new Event(1), new Event(0)), new Event(1)), //(4, (0, 1, 0), 1)==
                    new Event(2, new Event(2, new Event(1), new Event(0)), new Event(3),NormalizeInitFlag.Flag));//norm(2, (2, 1, 0), 3)
        }
    }
}
