using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using System.Reflection;



[assembly: log4net.Config.XmlConfigurator(Watch = true)]


namespace Core.Aux.System
{
	public static class Log
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger("Core.Aux.System.Log");

		public static readonly string CHRONON = "CHRONON";

		public static void Error(string format, params object[] args)
		{
			log.ErrorFormat(format, args);
		}

		public static void Warning(string format, params object[] args)
		{
			log.WarnFormat(format, args);
		}

		public static void Info(string group, string format, params object[] args)
		{
			var log = LogManager.GetLogger(group);
			log.InfoFormat(format, args);
		}
	}
}
