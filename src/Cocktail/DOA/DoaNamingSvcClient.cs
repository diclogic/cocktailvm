using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail;
using System.Net;
using System.Net.Sockets;
using Cocktail.HTS;

//////////////////////////////////////////////////////////////////////////
/// Fake DOA implementation
//////////////////////////////////////////////////////////////////////////

namespace DOA
{
	public class NamingSvcClient
	{
		protected struct Entry
		{
			public string type;
			public State ptr;	// TODO: fake impl
		}

		public struct Location
		{
			public IPEndPoint addrPort;
			public ulong index;
		}

		private Dictionary<string, Entry> m_objects = new Dictionary<string, Entry>();

		public bool RegisterObject(string objName, string objType, State ptr)
		{
			m_objects.Add(objName, new Entry() { type = objType, ptr = ptr });
			return true;
		}

		public object GetObject(string objName, string objType)
		{
			Entry retval;
			m_objects.TryGetValue(objName, out retval);

			if (retval.type != objType)
				return null;
			return retval.ptr;
		}
		public IHId GetObjectSpaceTimeID(string objName)
		{
			Entry retval;
			if (m_objects.TryGetValue(objName, out retval))
			{
				return retval.ptr.SpacetimeID;
			}

			return null;
		}

		public void Reset()
		{
			m_objects.Clear();
		}
	}
}
