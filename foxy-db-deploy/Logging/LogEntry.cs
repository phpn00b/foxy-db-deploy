using System;

namespace foxy_db_deploy.Logging
{
	public class LogEntry
	{
		public DateTime Date { get; set; }
		public string Details { get; set; }
		public string Source { get; set; }
		public LogLevel Level { get; set; }
		public string Attachment { get; set; }
	}
}