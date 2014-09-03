using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cocktail
{
	public interface IStdLib
	{
		void Migrate(StateRef arriving, StateRef departuring, TStateId immigrant);
	}

	internal static class StdLib
	{
		public static void Migrate([State] SpacetimeStorage arriving, [State] SpacetimeStorage departuring, TStateId immigrant)
		{
			if (!departuring.HasNativeState(immigrant))
				throw new RuntimeException("Failed to migrate `{0}` from `{1}` to `{2}`: no such state in storage", immigrant, departuring, arriving);

			departuring.RemoveNativeState(immigrant);
			arriving.AddNativeState(immigrant);
		}
	}
}
