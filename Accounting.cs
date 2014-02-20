using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CollisionTest.States;
using Cocktail;

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

	public static class AccountingWrapper
	{
		public static void Initiate([State] StateRef account, float amount)
		{
			account.SetField("Balance", amount);
		}

        public static void Transfer([State] StateRef fromAcc,[State] StateRef toAcc, float amount)
        {
			var fFrom = fromAcc.GetField<float>("Balance");
			fromAcc.SetField("Balance", fFrom - amount);
			var fTo = toAcc.GetField<float>("Balance");
			toAcc.SetField("Balance", fTo + amount);
        }
	}
}
