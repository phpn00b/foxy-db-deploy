using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace foxy_db_deploy.Model
{
	public class ChangeScript
	{
		public ChangeScript(IDataReader reader)
		{
			ChangeNumber = DataReaderUtility.GetValue<int>(reader, "ChangeNumber");
			DeltaSet = DataReaderUtility.GetValue<string>(reader, "DeltaSet");
		}
		public int ChangeNumber { get; set; }

		public string DeltaSet { get; set; }
	}
}
