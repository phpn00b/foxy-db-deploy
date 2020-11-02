using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;

namespace foxy_db_deploy.Tasks
{
	public class UpdateAllTypesTask : BaseDatabaseTask
	{
		private const string ListAllStoredProceduresQuery = @"
	SELECT
		SCHEMA_NAME(r.schema_id) as [Schema],
		r.name as Name
	FROM
		sys.table_types r
		";
		
		public class UserType
		{
			public string Schema { get; set; }
			public string Name { get; set; }
			public string FileName => $"{Schema}.{Name}.sql";
		}

		private readonly string _sprocPath;
		private RunDatabaseUpdateTask _task;
		public UpdateAllTypesTask(string connectionString, RunDatabaseUpdateTask runDatabaseUpdateTask)
			: base(connectionString)
		{
			_task = runDatabaseUpdateTask;
			if (runDatabaseUpdateTask._useUnzipedFolderPath != null)
				_sprocPath = $"{runDatabaseUpdateTask._updatePackageLocation}{runDatabaseUpdateTask._databaseFolderName}{Path.DirectorySeparatorChar}Types";
			else
				_sprocPath = $"{AppDomain.CurrentDomain.BaseDirectory}{Path.DirectorySeparatorChar}data-working{Path.DirectorySeparatorChar}{runDatabaseUpdateTask._databaseFolderName}{Path.DirectorySeparatorChar}Types";
		}

		private List<UserType> _existingStoredProcedures = new List<UserType>();
		private List<string> _storedProcedureFiles = new List<string>();
		private List<string> _storedProcedureNotUpdated = new List<string>();

		public override bool RunTask()
		{
			try
			{
				LoadExistingStoredProcedures();
				LoadLocalFiles();

				_task._logProcessor.Log(Source, $"Database has {_existingStoredProcedures.Count} user types and deploy package has {_storedProcedureFiles.Count} user types. Starting sync process now!");
				foreach (var storedProcedureFile in _storedProcedureFiles)
				{
					if (!ProcessFile(storedProcedureFile))
						return false;
					Thread.Sleep(1);
				}
				return true;
			}
			catch (Exception e)
			{
				_task._logProcessor.Log(Source, "failed syncing user types", attachment: e.ToString());
				//LogManager.Current.Log.Error("failed syncing user types", e);
			}

			return false;
		}

		private bool ProcessFile(string path, bool retry = true)
		{
			FileInfo fileInfo = new FileInfo(path);
			string name = fileInfo.Name;
			bool isNew = true;
			string commandText = null;
			UserType existingProcedure = _existingStoredProcedures.FirstOrDefault(o => o.FileName == name);
			try
			{
				if (retry)
					_task._logProcessor.Log(Source, $"Starting with {name}");
				//UserType existingProcedure = _existingStoredProcedures.FirstOrDefault(o => o.FileName == name);
				commandText = File.ReadAllText(path);

				if (existingProcedure != null)
				{
					Database.ExecuteNonQuery(CommandType.Text, $"DROP TYPE [{existingProcedure.Schema}].[{existingProcedure.Name}];");

					//commandText = Regex.Replace(commandText, "create(.*)procedure", "alter procedure", RegexOptions.IgnoreCase);
				}

				int code = 0;
				using (DbCommand command = Database.GetSqlStringCommand(commandText))
				{
					code = Database.ExecuteNonQuery(command);
				}

				_task._logProcessor.Log(Source, $" finished with {(isNew ? "create" : "alter")} {name} have return code: {code}!!!!");
				_task._logProcessor.Log(Source, "");
				//		fileInfo.Delete();
				return true;
			}
			catch (Exception e)
			{
				if (existingProcedure != null && e.Message.Contains($"Cannot drop type '{existingProcedure.Schema}.{existingProcedure.Name}'") && retry)
				{
					_task._logProcessor.Log(Source, " " + e.Message);
					//Console.WriteLine("  " + e.Message);
					DropReferencedStoredProcedures(existingProcedure.Schema, existingProcedure.Name);
					return ProcessFile(path, false);

				}

				_task._logProcessor.Log(Source, "failed user type file: " + path, attachment: e.ToString());
				//LogManager.Current.Log.Error("failed user type sproc file: " + path, e);
				string errorFile = path.Replace(".sql", "_error.txt");
				File.WriteAllText(errorFile, e.ToString());
				_storedProcedureNotUpdated.Add(fileInfo.Name.Replace(".sql", ""));
				//	Console.WriteLine();
				_task._logProcessor.Log(Source, $" failed to with {(isNew ? "create" : "alter")} {name} with message {e.Message}!!!", attachment: commandText);

			}
			return false;
		}

		private void DropReferencedStoredProcedures(string schema, string name)
		{
			string query = $@"
				SELECT 
					OBJECT_NAME(referencing_id) AS Name,
					OBJECT_SCHEMA_NAME(referencing_id) as SchemaName
				FROM
					sys.sql_expression_dependencies
				WHERE
					referenced_entity_name = '{name}'
					AND referenced_schema_name = '{schema}'
					AND referenced_class_desc = 'TYPE'";
			List<string> dropStatements = new List<string>();
			using (DbCommand command = Database.GetSqlStringCommand(query))
			{
				using (IDataReader reader = Database.ExecuteReader(command))
				{
					while (reader.Read())
					{
						string name1, schema1;
						name1 = DataReaderUtility.GetValue<string>(reader, "Name");
						schema1 = DataReaderUtility.GetValue<string>(reader, "SchemaName");
						dropStatements.Add($"DROP {(name1.StartsWith("f") ? "FUNCTION" : "PROCEDURE")} [{schema1}].[{name1}];");

					}
				}
			}

			dropStatements.ForEach(o =>
			{
				_task._logProcessor.Log(Source, " " + o);
				//Console.WriteLine("  " + o);
				Database.ExecuteNonQuery(CommandType.Text, o);
			});
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
						UserType entity = new UserType();
						entity.Schema = DataReaderUtility.GetValue<string>(reader, "Schema");
						entity.Name = DataReaderUtility.GetValue<string>(reader, "Name");

						_existingStoredProcedures.Add(entity);
					}
				}
			}
		}
	}
}