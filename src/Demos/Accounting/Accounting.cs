using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.States;
using Cocktail;
using Cocktail.HTS;

namespace Demos.Accounting
{
	/// <summary>
	/// Monitored account that can't be commutative because we need immediate response to special value changes
	/// </summary>
    [State]
    public class MonitoredAccount : State
    {
		[StaticTrigger(Triggers.Bankrupt, "val <= 0")]
		[StateField(PatchKind = FieldPatchCompatibility.Swap)]
		public float Balance;

		public MonitoredAccount(IHTimestamp stamp) : base(stamp) { }
		public MonitoredAccount(TStateId sid, IHTimestamp stamp) : base(sid, stamp.ID, stamp.Event, StatePatchMethod.Auto) { }
    }

    public static class Accounting
    {
		public static void Deposit([State] Account account, float amount)
		{
			account.Balance += amount;
		}

        public static void Transfer([State] Account fromAcc, [State] Account toAcc, float amount)
        {
            fromAcc.Balance -= amount;
            toAcc.Balance += amount;
        }

		public static void Withdraw([State] Account account, float amount)
		{
			account.Balance -= amount;
		}
    }

    public static class ConstrainedAccounting
    {
		public static void Deposit([State] MonitoredAccount account, float amount)
		{
			account.Balance += amount;
		}

        public static void Transfer([State] MonitoredAccount fromAcc, [State] MonitoredAccount toAcc, float amount)
        {
            fromAcc.Balance -= amount;
            toAcc.Balance += amount;
        }

		public static void Withdraw([State] MonitoredAccount account, float amount)
		{
			account.Balance -= amount;
		}
    }

	public static class Triggers
	{
		public static void Bankrupt(State acc)
		{

		}

	}

	[CSharpInvoker]
	public interface IAccounting
	{
		//void Test(StateRef account);
		void Deposit(StateRef account, float amount);
		void Transfer(StateRef fromAcc, StateRef toAcc, float amount);
		void Withdraw(StateRef account, float amount);
	}

}
