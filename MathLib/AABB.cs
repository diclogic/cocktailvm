using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTK;

namespace MathLib
{
    public struct AABB
    {
        public Vector3 Min, Max;
        public AABB(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }
        public AABB(Vector3 size)
        {
            Min = - (size / 2.0f);
            Max = (size / 2.0f);
        }
    }

    public struct Segment
    {
        public Vector3 From, To;
        public Segment(Vector3 from, Vector3 to)
        {
            From = from;
            To = to;
        }
    }

    public static partial class MathImpl
    {
        public static bool Intersect(this AABB bbox, Segment seg)
        {
            if ((Intersect(bbox, seg.From) && !Intersect(bbox, seg.To))
                || (!Intersect(bbox, seg.From) && Intersect(bbox, seg.To)))
                return true;
            return false;
        }
        //public bool Intersect(AABB bbox, Segment seg, out Vector3 contact)
        //{
        //    contact = default(Vector3);
        //    if (Intersect(bbox, seg))
        //    {
                
        //    }
        //}
        public static bool Intersect(this AABB bbox, Vector3 pt)
        {
            if (pt.X >= bbox.Min.X && pt.X < bbox.Max.X
                && pt.Y >= bbox.Min.Y && pt.Y < bbox.Max.Y
                && pt.Z >= bbox.Min.Z && pt.Z < bbox.Max.Z)
                return true;
            return false;
        }

        public static Vector3 RandomPoint(this AABB bbox, Random rand)
        {
            Vector3 vRand = RandomVector(rand);
            Vector3 ret;
            ret = bbox.Min + Vector3.Multiply(vRand, (bbox.Max - bbox.Min));
            return ret;
        }
    }
}
