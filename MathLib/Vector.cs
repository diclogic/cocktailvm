using System;
using OpenTK;

namespace MathLib
{
    public static partial class MathImpl
    {
        public static Vector3 RandomVector(Random rand)
        {
            return new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
        }

        public static Vector3 Random(this Vector3 bound, Random rand)
        {
            return Vector3.Multiply(RandomVector(rand), bound);
        }
    }
}