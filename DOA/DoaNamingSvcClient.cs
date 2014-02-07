using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail;
using System.Net;
using System.Net.Sockets;

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
			public object ptr;
		}

		public struct Location
		{
			public IPEndPoint addrPort;
			public ulong index;
		}

		public static NamingSvcClient Instance = new NamingSvcClient();

		private Dictionary<string, Entry> m_objects = new Dictionary<string, Entry>();

		public bool RegisterObject(string objName, string objType, object ptr)
		{
			m_objects.Add(objName, new Entry() { type = objType, ptr = ptr });
			return true;
		}

		public object QueryObjectLocation(string objName, string objType)
		{
			Entry retval;
			m_objects.TryGetValue(objName, out retval);

			if (retval.type != objType)
				return null;
			return retval.ptr;
		}
	}
}
