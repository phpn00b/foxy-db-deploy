using System.Data;

namespace foxy_db_deploy.Model
{
	public class UserType
	{
		public UserType(IDataReader reader)
		{
			Schema = DataReaderUtility.GetValue<string>(reader, "Schema");
			Name = DataReaderUtility.GetValue<string>(reader, "Name");
		}

		public string Schema { get; set; }
		public string Name { get; set; }
		public string FileName => $"{Schema}.{Name}.sql";
	}
}