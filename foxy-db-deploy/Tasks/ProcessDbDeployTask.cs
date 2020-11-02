using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace foxy_db_deploy.Tasks
{
	public class ProcessDbDeployTask : BaseDatabaseTask
	{
		private const string ListAllStoredProceduresQuery = @"SELECT
	CONVERT(INT,cl.change_number) as ChangeNumber,
	cl.delta_set as DeltaSet
FROM
	dbo.ChangeLog cl
ORDER BY
	cl.delta_set,
	cl.change_number";

		public class StoredProcedure
		{
			public int ChangeNumber { get; set; }

			public string DeltaSet { get; set; }

		}

		public class ChangeScriptWrapper
		{
			public FileInfo Info { get; }
			public int ChangeNumber { get; }
			public string DeltaSet { get; }
			public DateTime StartDate { get; set; }
			public DateTime EndDate { get; set; }
			public string Description { get; }
			public ChangeScriptWrapper(string path)
			{
				Info = new FileInfo(path);
				string[] parts = Info.Name.Split('.');
				ChangeNumber = Convert.ToInt32(parts[0]);
				parts = path.Split(Path.DirectorySeparatorChar);
				DeltaSet = parts.Reverse().Skip(1).First();
				Description = Info.Name;
			}

			public string InsertStatement => $"INSERT INTO dbo.ChangeLog (change_number,delta_set,start_dt,complete_dt,applied_by,description) VALUES ({ChangeNumber},'{DeltaSet}','{StartDate.ToString("g")}','{EndDate.ToString("g")}','Foxy DB Maintenance','{Description.Replace("'", "''")}')";
		}

		private RunDatabaseUpdateTask _task;
		private readonly string _sprocPath;// = $"{AppDomain.CurrentDomain.BaseDirectory}{Path.DirectorySeparatorChar}data-working{Path.DirectorySeparatorChar}CasinoSystem{Path.DirectorySeparatorChar}dbDeploy";
		public ProcessDbDeployTask(string connectionString, RunDatabaseUpdateTask runDatabaseUpdateTask)
			: base(connectionString)
		{
			_task = runDatabaseUpdateTask;
			if (runDatabaseUpdateTask._useUnzipedFolderPath != null)
				_sprocPath = $"{runDatabaseUpdateTask._updatePackageLocation}{runDatabaseUpdateTask._databaseFolderName}{Path.DirectorySeparatorChar}dbDeploy";
			else
				_sprocPath = $"{AppDomain.CurrentDomain.BaseDirectory}data-working{Path.DirectorySeparatorChar}{runDatabaseUpdateTask._databaseFolderName}{Path.DirectorySeparatorChar}dbDeploy";
		}

		private List<StoredProcedure> _existingStoredProcedures = new List<StoredProcedure>();
		private List<ChangeScriptWrapper> _storedProcedureFiles = new List<ChangeScriptWrapper>();
		private List<string> _storedProcedureNotUpdated = new List<string>();

		public override bool RunTask()
		{
			try
			{
				LoadExistingStoredProcedures();
				LoadLocalFiles();

				_task._logProcessor.Log(Source, $"Database has {_existingStoredProcedures.Count} deployed changes and deploy package has {_storedProcedureFiles.Count} changes to deploy. Starting sync process now!");
				using (var connection = new Microsoft.Data.SqlClient.SqlConnection(ConnectionString))
				{
					Server server = new Server(new ServerConnection(connection));
					foreach (var storedProcedureFile in _storedProcedureFiles)
					{
						if (!ProcessFile(storedProcedureFile, server))
							return false;
					}
				}

				return true;
			}
			catch (Exception e)
			{
				_task._logProcessor.Log(Source, "failed syncing stored procedures", attachment: e.ToString());
			}

			return false;
		}

		private bool ProcessFile(ChangeScriptWrapper path, Server server)
		{

			string name = path.Description;

			string commandText = null;
			try
			{

				StoredProcedure existingProcedure = _existingStoredProcedures.FirstOrDefault(o => o.ChangeNumber == path.ChangeNumber && o.DeltaSet == path.DeltaSet);
				if (existingProcedure != null)
				{
					_task._logProcessor.Log(Source, $"Skipping {path.DeltaSet} change: {path.ChangeNumber} file: {path.Description} as it was already applied");
					//path.Info.Delete();
					return true;
				}
				commandText = File.ReadAllText(path.Info.FullName);
				path.StartDate = DateTime.Now;
				server.ConnectionContext.BeginTransaction();
				server.ConnectionContext.ExecuteNonQuery(commandText);

				path.EndDate = DateTime.Now;

				_task._logProcessor.Log(Source, $"Finished processing {path.DeltaSet} change: {path.ChangeNumber} file: {path.Description} in {(path.EndDate - path.StartDate).TotalMilliseconds.ToString("N2")}ms");
				//	path.Info.Delete();
				//	server.ConnectionContext.
				server.ConnectionContext.ExecuteNonQuery(path.InsertStatement);
				server.ConnectionContext.CommitTransaction();
				return true;

			}
			catch (Exception e)
			{
				path.EndDate = DateTime.Now;
				_task._logProcessor.Log(Source, "failed processing sproc file: " + path.Info.FullName, attachment: e.ToString());
				string errorFile = path.Info.FullName.Replace(".sql", "_error.txt");
				File.WriteAllText(errorFile, e.ToString());
				_storedProcedureNotUpdated.Add(path.Description.Replace(".sql", ""));
				_task._logProcessor.Log(Source, $"Failed processing {path.DeltaSet} change: {path.ChangeNumber} file: {path.Description} in {(path.EndDate - path.StartDate).TotalMilliseconds.ToString("N2")}ms with message {e.Message}", attachment: commandText);
				return false;
			}
		}

		private void LoadLocalFiles()
		{
			string[] allSprocs = Directory.GetFiles(_sprocPath, "*.sql", SearchOption.AllDirectories);
			_storedProcedureFiles.AddRange(allSprocs.Where(o =>
			{
				var fi = new FileInfo(o);
				return !fi.Name.StartsWith("output") && fi.Name.StartsWith("0");
			}).Select(o =>
			{
				ChangeScriptWrapper wrapper = null;
				try
				{
					wrapper = new ChangeScriptWrapper(o);
				}
				catch (Exception e)
				{
					_task._logProcessor.Log(Source, $"Error processing file {o}\n{e}");
				}

				return wrapper;
			}));
		}

		private void LoadExistingStoredProcedures()
		{
			using (DbCommand command = Database.GetSqlStringCommand(ListAllStoredProceduresQuery))
			{
				using (IDataReader reader = Database.ExecuteReader(command))
				{
					while (reader.Read())
					{
						StoredProcedure entity = new StoredProcedure();
						entity.ChangeNumber = DataReaderUtility.GetValue<int>(reader, "ChangeNumber");
						entity.DeltaSet = DataReaderUtility.GetValue<string>(reader, "DeltaSet");
						_existingStoredProcedures.Add(entity);
					}
				}
			}
		}
	}
}