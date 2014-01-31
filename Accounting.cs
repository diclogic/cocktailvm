using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CollisionTest.States;

namespace CollisionTest
{
    public static class Accounting
    {
		public static void Initiate([State] Account account, float amount)
		{
			account.Balance = amount;
		}

        public static void Transfer([State] Account fromAcc, [State] Account toAcc, float amount)
        {
            fromAcc.Balance -= amount;
            toAcc.Balance += amount;
        }
    }
}
