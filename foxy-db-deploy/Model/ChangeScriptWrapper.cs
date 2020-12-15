using System;
using System.IO;
using System.Linq;

namespace foxy_db_deploy.Model
{
	public class ChangeScriptWrapper
	{
		public FileInfo Info { get; }
		public int ChangeNumber { get; }
		public string DeltaSet { get; }
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		public string Description { get; }

		public string SortString => DeltaSet.PadLeft(10, '0') + "-" + ChangeNumber;
		public ChangeScriptWrapper(string path)
		{
			Info = new FileInfo(path);
			string[] parts = Info.Name.Split('.');
			ChangeNumber = Convert.ToInt32(parts[0]);
			parts = path.Split(Path.DirectorySeparatorChar);
			DeltaSet = Convert.ToDecimal(parts.Reverse().Skip(1).First()).ToString("N2");
			Description = Info.Name;
		}

		public string InsertStatement => $"INSERT INTO dbo.ChangeLog (change_number,delta_set,start_dt,complete_dt,applied_by,description) VALUES ({ChangeNumber},'{DeltaSet}','{StartDate.ToString("g")}','{EndDate.ToString("g")}','Foxy DB Maintenance','{Description.Replace("'", "''")}')";
	}
}