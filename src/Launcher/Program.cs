using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Core.Aux.System;
using System.Threading;
using Skeleton;

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
				var mainWindow = new LauncherWindow();
				var glWindow = mainWindow.GetGLWindow();
				var args = Environment.GetCommandLineArgs();

				var loader = new ModelLoader();
				var model = loader.LoadModel(args.ElementAtOrDefault(1));
				var renderer = new Renderer(glWindow);

				var controller = new Controller();
				controller.Initialize(renderer, model, mainWindow, mainWindow, glWindow);

				Application.Run(mainWindow);
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
