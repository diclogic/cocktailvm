
using System;
using Cocktail;
using System.Threading;


namespace Cocktail
{
	public class WithIn : IDisposable
	{
		static ThreadLocal<Spacetime> m_tls = new ThreadLocal<Spacetime>(() => null);

		public WithIn(Spacetime st)
		{
			m_tls.Value = st;
		}

		public static Spacetime GetWithin()
		{
			return m_tls.Value;
		}

		public void Dispose()
		{
			m_tls.Value = null;
		}
	}
}
