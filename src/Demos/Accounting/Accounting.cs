using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CollisionTest.States;
using Cocktail;

namespace CollisionTest
{
	public interface IAccounting
	{
		//void Test(Spacetime ST, StateRef account);
		void Deposit(Spacetime ST, StateRef account, float amount);
		void Transfer(Spacetime ST, StateRef fromAcc, StateRef toAcc, float amount);
		void Withdraw(Spacetime ST, StateRef account, float amount);
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
}
