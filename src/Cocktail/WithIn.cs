
using System;
using Cocktail;
using System.Threading;
using System.Collections.Generic;


namespace Cocktail
{
	public class WithIn : IDisposable
	{
		static ThreadLocal<List<Spacetime>> ms_tls = new ThreadLocal<List<Spacetime>>(() => new List<Spacetime>());

		List<Spacetime> m_localStack;

		public WithIn(Spacetime st)
		{
			m_localStack = ms_tls.Value;

			m_localStack.Add(st);
		}

		public static Spacetime GetWithin()
		{
			var m_localStack = ms_tls.Value;
			return m_localStack[m_localStack.Count - 1];
		}

		public void Dispose()
		{
			m_localStack.RemoveAt(m_localStack.Count - 1);
		}
	}
}
