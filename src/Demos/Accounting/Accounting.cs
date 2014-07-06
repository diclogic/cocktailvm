using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.States;
using Cocktail;

namespace Demos
{
	[Invoker]
	public interface IAccounting
	{
		//void Test(StateRef account);
		void Deposit(StateRef account, float amount);
		void Transfer(StateRef fromAcc, StateRef toAcc, float amount);
		void Withdraw(StateRef account, float amount);
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
}
