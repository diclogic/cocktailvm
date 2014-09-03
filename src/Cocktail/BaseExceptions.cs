using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cocktail
{
	public class BaseException : Exception
    {
        public BaseException(string reason) : base(reason) { }
    }

    public class CompileTimeException : BaseException
    {
        public CompileTimeException(string reason) : base(reason) { }
    }

	public class JITCompileException : BaseException
	{
		public JITCompileException(string reason) : base(reason) { }
	}

	public class RuntimeException : BaseException
	{
        public RuntimeException(string reason) : base(reason) { }
        public RuntimeException(string reason, params object[] args) : this(string.Format(reason,args)) { }
	}
}
