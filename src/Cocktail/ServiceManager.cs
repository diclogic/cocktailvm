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

		private static DOA.PseudoSyncMgr m_pseudoSync;
		private static bool m_initialized = false;

		static ServiceManager()
		{
		}

		public static void Init(VMSpacetime vmST = null)
		{
			if (m_initialized)
				return;

			m_initialized = true;

			m_pseudoSync = new DOA.PseudoSyncMgr();
			LocatingService = m_pseudoSync;
			SyncService = m_pseudoSync;
			ReinitComputeNode();

			if (vmST == null)
				vmST = ComputeNode.VMSpacetimeForUnitTest;

			m_pseudoSync.Initialize(vmST);
		}

		public static void Reset(EReset reset = EReset.Weak, VMSpacetime vmST = null)
		{
			if (reset == EReset.Weak)
				m_pseudoSync.Reset();
			else if (reset == EReset.Strong)
			{
				m_pseudoSync = null;
				LocatingService = null;
				SyncService = null;
				m_initialized = false;
				Init(vmST);
			}
		}

		private static void ReinitComputeNode()
		{
			ComputeNode = new ComputeNode(0, 2, DeployMode.Debug);	//< TODO: hardcoded for 2 nodes for now
		}
    }
}
