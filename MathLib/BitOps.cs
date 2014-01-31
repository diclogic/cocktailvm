using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MathLib
{
    public static class BitOps
    {
        public static uint RoundUp(uint x)
        {
            var c = x;
            c &= c >> 1;
            c &= c >> 2;
            c &= c >> 4;
            c &= c >> 8;
            c &= c >> 16;
            c += 1;
            return c;
        }

        public static int HighestBitPos(uint x)
        {
            int count = -1;
            var t = x;

            while (x != 0)
            {
                x >>= 1;
                count += 1;
            }
            return count;
        }
    }
}
