using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HTS;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;



namespace Cocktail
{
	using FieldEntryPair = itcsharp.Pair<StateSnapshot.FieldEntry>;

	/// <summary>
	/// Only fields with CommutativeDelta compatibility are aggregated
	/// </summary>
	public static class StateAggregation
	{

		public static void Aggregate(this StateSnapshot agg, StateSnapshot rhs)
		{
			agg.Rev = Math.Max(agg.Rev, rhs.Rev);
			agg.Timestamp = agg.Timestamp.Join(rhs.Timestamp);

			if (agg.TypeName != rhs.TypeName)
				throw new PatchException(string.Format("Find inconsistant type during aggregation: expecting {0}, got {1}", agg.TypeName, rhs.TypeName));

			if (agg.Fields.Count != rhs.Fields.Count)
				throw new PatchException(string.Format("Inconsistant number of fields: expecting {0}, got {1}", agg.Fields.Count, rhs.Fields.Count));

			var fpairSeq =	from a in agg.Fields
							join b in rhs.Fields
							on a.Name equals b.Name select new FieldEntryPair(a,b);

			foreach (var fpair in fpairSeq)
			{
				if (0 == (fpair.First.Attrib.PatchKind & FieldPatchCompatibility.CommutativeDelta))
					continue;

				if (fpair.First.Type != fpair.Second.Type)
					throw new PatchException(string.Format("Inconsistant field type found: expecting {0}, got {1}", fpair.First.Type.Name, fpair.Second.Type.Name));

				var type = fpair.First.Type;
				type.GetMethod();
				fpair.First.Value = fpair.Second.Value;
			}
		}
	}
}