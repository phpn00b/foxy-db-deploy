using System;
using System.Collections.Generic;
using System.Text;

namespace foxy_db_deploy.Model
{
	public class StoredProcedure
	{
		public string Schema { get; set; }

		public string SchemaStoredProcedureName { get; set; }

		public string StoredProcedureName { get; set; }

		public DateTime CreatedDate { get; set; }

		public DateTime LastAlteredDate { get; set; }
		public string FileName => StoredProcedureName + ".sql";
	}
}
