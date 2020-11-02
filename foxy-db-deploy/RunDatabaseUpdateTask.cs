using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using foxy_db_deploy.Logging;
using foxy_db_deploy.Tasks;
using Microsoft.SqlServer.Management.HadrModel;

namespace foxy_db_deploy
{
	public class RunDatabaseUpdateTask
	{
		private List<BaseDatabaseTask> tasks = new List<BaseDatabaseTask>();
		internal readonly string _hostName;
		internal readonly string _databaseName;
		internal readonly string _databaseFolderName;
		internal readonly string _updatePackageLocation;
		internal readonly LogProcessor _logProcessor;
		internal readonly string _useUnzipedFolderPath;
		internal readonly string _customConnectionString;

		public RunDatabaseUpdateTask(string hostname, string databaseName, string updatePackageLocation, LogProcessor logProcessor, string useUnzipedFolderPath = null, string connectionString = null)
		{
			if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "Logs"))
				Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "Logs");
			_logProcessor = logProcessor;
			_hostName = hostname;

			_databaseName = databaseName;
			_updatePackageLocation = updatePackageLocation;
			_useUnzipedFolderPath = useUnzipedFolderPath;
			_customConnectionString = connectionString;
			if (databaseName.Contains("CasinoSystem"))
			{
				_databaseFolderName = "CasinoSystem";
			}
			else
			{
				_databaseFolderName = databaseName;
			}
		}

		public bool ProcessUpdatePackage()
		{
			string masterConnectionString = $"Data Source={_hostName};Initial Catalog=master;Integrated Security=True;MultipleActiveResultSets=True;Application Name=FoxHorn Database Maintenance;";
			string casinoSystemConnectionString = string.IsNullOrWhiteSpace(_customConnectionString) ? $"Data Source={_hostName};Initial Catalog={_databaseName};Integrated Security=True;MultipleActiveResultSets=True;Application Name=FoxHorn Database Maintenance;" : _customConnectionString;


			Console.WriteLine($"{_useUnzipedFolderPath} so not going to unpack our database");

			UpdateAllTypesTask updateAllTypesTask = new UpdateAllTypesTask(casinoSystemConnectionString, this);
			tasks.Add(updateAllTypesTask);

			// process db deployment scripts
			ProcessDbDeployTask dbDeployTask = new ProcessDbDeployTask(casinoSystemConnectionString, this);
			tasks.Add(dbDeployTask);

			// update all database functions
			UpdateAllFunctionsTask updateAllFunctionsTask = new UpdateAllFunctionsTask(casinoSystemConnectionString, this);
			tasks.Add(updateAllFunctionsTask);

			// Update all database procedures
			UpdateAllProceduresTask updateAllProceduresTask = new UpdateAllProceduresTask(casinoSystemConnectionString, this);
			tasks.Add(updateAllProceduresTask);

			//dbDeployTask.RunTask();

			foreach (var task in tasks)
			{
				if (!task.RunTask())
					return false;
				Thread.Sleep(50);
			}

			return true;
		}
	}
}
