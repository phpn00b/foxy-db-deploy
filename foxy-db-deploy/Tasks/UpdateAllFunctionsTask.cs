using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace foxy_db_deploy.Tasks
{
	public class UpdateAllFunctionsTask : BaseDatabaseTask
	{
		private const string ListAllStoredProceduresQuery = @"SELECT 
	o.name as SchemaStoredProcedureName, 
	SCHEMA_NAME( o.schema_id) + '.' + o.name  as StoredProcedureName,
	SCHEMA_NAME( o.schema_id) as [Schema],
	o.create_date as CreatedDate,
	o.modify_date as LastAlteredDate
FROM 
	sys.sql_modules m 
	INNER JOIN sys.objects o 
		ON m.object_id=o.object_id
WHERE 
	type_desc like '%function%'";

		public class StoredProcedure
		{
			public string Schema { get; set; }

			public string SchemaStoredProcedureName { get; set; }

			public string StoredProcedureName { get; set; }

			public DateTime CreatedDate { get; set; }

			public DateTime LastAlteredDate { get; set; }
			public string FileName => StoredProcedureName + ".sql";
		}

		private readonly RunDatabaseUpdateTask _task;
		private readonly string _sprocPath;//= $"{AppDomain.CurrentDomain.BaseDirectory}{Path.DirectorySeparatorChar}data-working{Path.DirectorySeparatorChar}CasinoSystem{Path.DirectorySeparatorChar}Functions";
		public UpdateAllFunctionsTask(string connectionString, RunDatabaseUpdateTask runDatabaseUpdateTask)
			: base(connectionString)
		{
			_task = runDatabaseUpdateTask;
			if (runDatabaseUpdateTask._useUnzipedFolderPath != null)
				_sprocPath = $"{runDatabaseUpdateTask._updatePackageLocation}{runDatabaseUpdateTask._databaseFolderName}{Path.DirectorySeparatorChar}Functions";
			else
				_sprocPath = $"{AppDomain.CurrentDomain.BaseDirectory}data-working{Path.DirectorySeparatorChar}{runDatabaseUpdateTask._databaseFolderName}{Path.DirectorySeparatorChar}Functions";
		}

		private List<StoredProcedure> _existingStoredProcedures = new List<StoredProcedure>();
		private List<string> _storedProcedureFiles = new List<string>();
		private List<string> _storedProcedureNotUpdated = new List<string>();

		public override bool RunTask()
		{
			try
			{
				LoadExistingStoredProcedures();
				LoadLocalFiles();
				_task._logProcessor.Log(Source, $"Database has {_existingStoredProcedures.Count} functions and deploy package has {_storedProcedureFiles.Count} functions. Starting sync process now!");
				foreach (var storedProcedureFile in _storedProcedureFiles)
				{
					ProcessFile(storedProcedureFile);
					Thread.Sleep(1);
				}
				return true;
			}
			catch (Exception e)
			{
				_task._logProcessor.Log(Source, "failed syncing functions", attachment: e.ToString());
			}

			return false;
		}

		private void ProcessFile(string path)
		{
			FileInfo fileInfo = new FileInfo(path);
			string name = fileInfo.Name;
			string commandText = null;
			bool isNew = true;
			try
			{
				StoredProcedure existingProcedure = _existingStoredProcedures.FirstOrDefault(o => o.FileName == name);
				commandText = File.ReadAllText(path);

				if (existingProcedure != null)
				{
					isNew = false;
					commandText = Regex.Replace(commandText, "create(.*)function", "alter function", RegexOptions.IgnoreCase);
				}

				int code = 0;
				using (DbCommand command = Database.GetSqlStringCommand(commandText))
				{
					code = Database.ExecuteNonQuery(command);
				}

				_task._logProcessor.Log(Source, $"finished with {(isNew ? "create" : "alter")} {name} have return code: {code}");
				//	fileInfo.Delete();
			}
			catch (Exception e)
			{
				_task._logProcessor.Log(Source, "failed processing function file: " + path, attachment: e.ToString());
				string errorFile = path.Replace(".sql", "_error.txt");
				File.WriteAllText(errorFile, e.ToString());
				_storedProcedureNotUpdated.Add(fileInfo.Name.Replace(".sql", ""));
				_task._logProcessor.Log(Source, $"failed to with {(isNew ? "create" : "alter")} {name} with message {e.Message}", attachment: commandText);
			}
		}

		private void LoadLocalFiles()
		{
			string[] allSprocs = Directory.GetFiles(_sprocPath, "*.sql", SearchOption.TopDirectoryOnly);
			_storedProcedureFiles.AddRange(allSprocs);
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
						entity.Schema = DataReaderUtility.GetValue<string>(reader, "Schema");
						entity.SchemaStoredProcedureName = DataReaderUtility.GetValue<string>(reader, "SchemaStoredProcedureName");
						entity.StoredProcedureName = DataReaderUtility.GetValue<string>(reader, "StoredProcedureName");
						entity.CreatedDate = DataReaderUtility.GetValue<DateTime>(reader, "CreatedDate");
						entity.LastAlteredDate = DataReaderUtility.GetValue<DateTime>(reader, "LastAlteredDate");
						_existingStoredProcedures.Add(entity);
					}
				}
			}
		}
	}
}