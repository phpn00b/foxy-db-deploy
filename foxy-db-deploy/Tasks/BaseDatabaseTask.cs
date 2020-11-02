namespace foxy_db_deploy.Tasks
{
	public abstract class BaseDatabaseTask
	{
		
		protected const string Source = "SQL";
		protected BaseDatabaseTask(string connectionString)
		{
			ConnectionString = connectionString;
			
		}

		protected string ConnectionString { get; }

		/// <summary>
		/// 
		/// </summary>
		/// <returns>true if finished and can go to next taske</returns>
		public abstract bool RunTask();
	}
}