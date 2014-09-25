using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail.HTS;

namespace Cocktail
{
	public static class ServiceManager
	{
		//public static ServiceManager Instance = new ServiceManager();

		public static ILocatingService LocatingService { get; private set; }
		public static ISyncService SyncService { get; private set; }
		public static IHIdFactory HIdFactory { get; private set; }

		private static DOA.PseudoSyncMgr m_pseudoSync; 

		static ServiceManager()
		{
			m_pseudoSync = new DOA.PseudoSyncMgr();
			LocatingService = m_pseudoSync;
			SyncService = m_pseudoSync;
			HIdFactory = new ITCIdentityFactory();
		}

		public static void Init(VMSpacetime vmST)
		{
			m_pseudoSync.Initialize(vmST);
		}

		public static void Reset()
		{
			m_pseudoSync.Reset();
			HIdFactory = new ITCIdentityFactory();
		}
    }
}
