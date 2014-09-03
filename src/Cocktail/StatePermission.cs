using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cocktail
{
	[Flags]
	enum EPermission
	{
		AnyOne,
		Author,
		Deploy,
		Admin,
	}

	class StatePermissionAttribute : Attribute
	{
		public EPermission Flags;
	}
}
