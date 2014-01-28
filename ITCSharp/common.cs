using System;

namespace itcsharp
{
    public class ChildNodeInconsistantException : ApplicationException
    {
        public ChildNodeInconsistantException()
            : base("Every node must have either two children or no child.")
        { }
    }

    internal struct NormalizeInitFlag { public static NormalizeInitFlag Flag; }


    internal static class Misc
    {
    }
}