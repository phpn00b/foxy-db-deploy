using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using foxy_db_deploy.Model;
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




		private readonly RunDatabaseUpdateTask _task;
		private readonly string _sprocPath;
		public ProcessDbDeployTask(string connectionString, RunDatabaseUpdateTask runDatabaseUpdateTask)
			: base(connectionString)
		{
			_task = runDatabaseUpdateTask;
			if (runDatabaseUpdateTask._useUnzipedFolderPath != null)
				_sprocPath = $"{runDatabaseUpdateTask._updatePackageLocation}{runDatabaseUpdateTask._databaseFolderName}{Path.DirectorySeparatorChar}dbDeploy";
			else
				_sprocPath = $"{AppDomain.CurrentDomain.BaseDirectory}data-working{Path.DirectorySeparatorChar}{runDatabaseUpdateTask._databaseFolderName}{Path.DirectorySeparatorChar}dbDeploy";
		}

		private readonly List<ChangeScript> _existingChangeScripts = new List<ChangeScript>();
		private readonly List<ChangeScriptWrapper> _changeScriptFiles = new List<ChangeScriptWrapper>();

		public override bool RunTask()
		{
			try
			{
				LoadExistingStoredProcedures();
				LoadLocalFiles();

				_task._logProcessor.Log(Source, $"Database has {_existingChangeScripts.Count} deployed changes and deploy package has {_changeScriptFiles.Count} changes to deploy. Starting sync process now!");
				using (var connection = new Microsoft.Data.SqlClient.SqlConnection(ConnectionString))
				{
					Server server = new Server(new ServerConnection(connection));
					foreach (var storedProcedureFile in _changeScriptFiles)
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

				var existingProcedure = _existingChangeScripts.FirstOrDefault(o => o.ChangeNumber == path.ChangeNumber && o.DeltaSet == path.DeltaSet);
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
				_task._logProcessor.Log(Source, $"Failed processing {path.DeltaSet} change: {path.ChangeNumber} file: {path.Description} in {(path.EndDate - path.StartDate).TotalMilliseconds.ToString("N2")}ms with message {e.Message}", attachment: commandText);
				return false;
			}
		}

		private void LoadLocalFiles()
		{
			string[] allSprocs = Directory.GetFiles(_sprocPath, "*.sql", SearchOption.AllDirectories);
			_changeScriptFiles.AddRange(allSprocs.Where(o =>
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
			using var connection = CreateConnection();
			using var command = CreateRawCommand(connection, ListAllStoredProceduresQuery);
			using IDataReader reader = command.ExecuteReader();

			while (reader.Read())
				_existingChangeScripts.Add(new ChangeScript(reader));
		}
	}
}