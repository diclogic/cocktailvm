using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail.HTS;

namespace Cocktail
{
	public enum EReset
	{
		Weak,
		Strong,
	}
	public static class ServiceManager
	{
		//public static ServiceManager Instance = new ServiceManager();

		public static ILocatingService LocatingService { get; private set; }
		public static ISyncService SyncService { get; private set; }
		public static ComputeNode ComputeNode { get; private set; }
		public static IHIdFactory HIdFactory { get { return ComputeNode.HIdFactory; } }

		private static DOA.PseudoSyncMgr m_pseudoSync; 

		static ServiceManager()
		{
			m_pseudoSync = new DOA.PseudoSyncMgr();
			LocatingService = m_pseudoSync;
			SyncService = m_pseudoSync;
			ComputeNode = new ComputeNode(0, 2);
		}

		public static void Init(VMSpacetime vmST)
		{
			m_pseudoSync.Initialize(vmST);
		}

		public static void Reset(EReset reset = EReset.Weak)
		{
			m_pseudoSync.Reset();

			if (reset == EReset.Strong)
				ComputeNode = new ComputeNode(0, 2);
		}
    }
}
