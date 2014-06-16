using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cocktail;
using Cocktail.HTS;
using System.IO;

namespace Demos.States
{
    [State]
    public class Account : State
    {
		[StateField(PatchKind = FieldPatchCompatibility.CommutativeDelta)]
		public float Balance;

        public Account( IHTimestamp stamp) : base(stamp) { }
		public Account(TStateId sid, IHTimestamp stamp) : base(sid, stamp.ID, stamp.Event, StatePatchMethod.Auto) { }
    }

	/// <summary>
	/// Monitored account that can't be commutative because we need immediate response to special value changes
	/// </summary>
    [State]
    public class MonitoredAccount : State
    {
		[StateField(PatchKind = FieldPatchCompatibility.Swap)]
		public float Balance;

        public MonitoredAccount( IHTimestamp stamp) : base(stamp) { }
		public MonitoredAccount(TStateId sid, IHTimestamp stamp) : base(sid, stamp.ID, stamp.Event, StatePatchMethod.Auto) { }
    }
}
