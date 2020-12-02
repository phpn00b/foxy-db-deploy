using System;
using System.Collections.Generic;
using System.Threading;

namespace foxy_db_deploy.Logging
{
	public class LogProcessor
	{
		private readonly object Mutex = new object();
		private readonly List<LogEntry> _logEntries = new List<LogEntry>(10000);

		public List<string> Messages { get; } = new List<string>(10000);

		public Action<LogEntry> OnEventLogged { get; set; }

		public void Log(string source, string details, LogLevel level = LogLevel.Info, string attachment = null)
		{
			var log = new LogEntry
			{
				Date = DateTime.Now,
				Details = details,
				Level = level,
				Source = source,
				Attachment = attachment
			};
			string line = $"{log.Source}: {log.Details}\r\n";
			if (log.Attachment != null)
			{
				line += $"  {log.Attachment}\r\n\r\n";
			};
			Messages.Add(line);

			if (OnEventLogged != null)
			{
				OnEventLogged.Invoke(log);
				Thread.Sleep(2);
				return;
			}
			//*
			Console.WriteLine($"{source}: {details}");
			if (attachment != null)
			{
				Console.WriteLine($"  {attachment}");
				Console.WriteLine();
			}
			//*/

			_logEntries.Add(log);
		}

		public void Clear()
		{
			lock (Mutex)
			{
				_logEntries.Clear();
			}
		}

		public LogEntry[] GetAll()
		{

			return _logEntries.ToArray();

		}
	}
}