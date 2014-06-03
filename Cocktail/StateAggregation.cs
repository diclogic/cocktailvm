using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HTS;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Globalization;



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

				if (type == typeof(int))
				{
					Accumulate(ref fpair.First.Value, (int)fpair.Second.Value);
				}
				else if (type == typeof(float))
				{
					Accumulate(ref fpair.First.Value, (float)fpair.Second.Value);
				}
				else if (type == typeof(string))
				{
					throw new NotImplementedException();
				}
				else
					throw new PatchException(string.Format("Non-Primitive type found as a field of a state: state {0}, field {1}", agg.TypeName, fpair.First.Name));
			}
		}

		private static void Accumulate(ref object acc, int rhs)
		{
			acc = (int)acc + rhs;
		}
		private static void Accumulate(ref object acc, float rhs)
		{
			acc = (float)acc + rhs;
		}

	}
}