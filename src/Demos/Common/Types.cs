using System;
using System.Text;
using OpenTK;
using OpenTK.Graphics;

namespace Common
{
    public static class Vector3Extension
    {
        public static string ToString(this Vector3 v)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("Vector3{{ {0},{1},{2} }}", v.X, v.Y, v.Z);
            return sb.ToString();
        }
    }


    public enum EStyle
    {
        None,
    }
}