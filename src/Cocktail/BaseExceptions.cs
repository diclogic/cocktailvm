using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cocktail
{
    public class CompileTimeException : ApplicationException
    {
        public CompileTimeException(string reason) : base(reason) { }
    }

	public class JITCompileException : ApplicationException
	{
		public JITCompileException(string reason) : base(reason) { }
	}

	public class RuntimeException : ApplicationException
	{
        public RuntimeException(string reason) : base(reason) { }
	}
}
