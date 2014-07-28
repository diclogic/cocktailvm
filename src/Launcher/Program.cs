using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Core.Aux.System;
using System.Threading;

namespace Launcher
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
			Log.Info("SYSTEM", "Application started!");
			//Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException, false);
			//Application.ThreadException += (_, e) => LogException(e.Exception);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
			try
			{
				Application.Run(new Form1());
			}
			catch (Exception ex)
			{
				LogException(ex);
			}
        }

		static void LogException(Exception ex)
		{
			System.Console.WriteLine("Unhandled Exception: {0}", ex.ToString());
			Log.Info("SYSTEM", "Unhandled Exception: {0}", ex.ToString());
		}
    }
}
