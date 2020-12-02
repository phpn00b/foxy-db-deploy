using System;
using System.Collections.Generic;
using System.Text;

namespace foxy_db_deploy.Model
{
	public class DatabaseObjectHash
	{
		public string SchemaName { get; set; }

		public string ObjectName { get; set; }

		public string ObjectType { get; set; }

		public string ObjectHash { get; set; }

		public DateTime CreatedDate { get; set; }

		public DateTime LastUpdated { get; set; }

		public string FileName { get; set; }
	}
}
