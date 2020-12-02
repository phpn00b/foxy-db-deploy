using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using foxy_db_deploy.Model;

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

		private const string ListAllTypesQuery = @"
SELECT
	*
FROM
	dbo.ObjectHash oh
WHERE
	oh.ObjectType = 'User Type';
";


		public string Hash(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
				return string.Empty;
			var hash = new SHA1Managed().ComputeHash(Encoding.UTF8.GetBytes(input.ToLower()));
			return string.Concat(hash.Select(b => b.ToString("x2")));
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

		private readonly List<UserType> _existingUserTypes = new List<UserType>();
		private readonly List<string> _userTypeFiles = new List<string>();

		private DatabaseObjectHash[] _objectHashes;
		public override bool RunTask()
		{
			try
			{
				ExecuteNonQuery(@"IF OBJECT_ID(N'dbo.ObjectHash', N'U') IS NULL
BEGIN
	create table dbo.ObjectHash
	(
		SchemaName varchar(100) not null,
		ObjectName varchar(100) not null,
		ObjectType varchar(30) not null,
		ObjectHash varchar(100) not null,
		CreatedDate datetime not null,
		LastUpdated datetime not null,
		FileName varchar(200) not null
		PRIMARY KEY(SchemaName,ObjectName,ObjectType)
	);
END");
				_objectHashes = LoadObjectHashes(ListAllTypesQuery);
				LoadExistingStoredProcedures();
				LoadLocalFiles();

				_task._logProcessor.Log(Source, $"Database has {_existingUserTypes.Count} user types and deploy package has {_userTypeFiles.Count} user types. Starting sync process now!");
				foreach (var storedProcedureFile in _userTypeFiles)
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
			UserType existingProcedure = _existingUserTypes.FirstOrDefault(o => o.FileName == name);
			DatabaseObjectHash existingObjectHash = _objectHashes.FirstOrDefault(o => o.FileName == name);
			try
			{
				if (retry)
					_task._logProcessor.Log(Source, $"Starting with {name}");
				//UserType existingProcedure = _existingUserTypes.FirstOrDefault(o => o.FileName == name);
				commandText = File.ReadAllText(path);
				string currentHash = Hash(commandText);
				bool isUpdate = false;
				if (existingProcedure != null)
				{
					_task._logProcessor.Log(Source, $"  not new type values: {existingProcedure != null} || {existingObjectHash == null} || {existingObjectHash?.ObjectHash != currentHash}");
					if (existingObjectHash == null || existingObjectHash.ObjectHash != currentHash)
					{
						ExecuteNonQuery($"DROP TYPE [{existingProcedure.Schema}].[{existingProcedure.Name}];");
						isUpdate = existingObjectHash != null;
					}
					else
					{
						return true;
					}

				}
				else
				{
					_task._logProcessor.Log(Source, $"  {existingProcedure != null} || {existingObjectHash == null} || {existingObjectHash?.ObjectHash != currentHash}");
				}

				int code = ExecuteNonQuery(commandText);


				var parts = name.Split('.');
				string schema = parts[0];
				string objectName = parts[1];
				string query = $"INSERT INTO dbo.ObjectHash (SchemaName,ObjectName,ObjectType,ObjectHash,CreatedDate,LastUpdated,FileName) VALUES ('{schema}','{objectName}','User Type','{currentHash}','{DateTime.Now:G}','{DateTime.Now:G}','{name}');";
				if (isUpdate)
					query = $"UPDATE dbo.ObjectHash SET ObjectHash = '{currentHash}', LastUpdated = '{DateTime.Now:G}' WHERE FileName = '{name}'";
				using var connection = CreateConnection();
				ExecuteNonQuery(query);

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
			using var connection = CreateConnection();
			using DbCommand command = CreateRawCommand(connection, query);
			using IDataReader reader = command.ExecuteReader();
			while (reader.Read())
			{
				string name1, schema1;
				name1 = DataReaderUtility.GetValue<string>(reader, "Name");
				schema1 = DataReaderUtility.GetValue<string>(reader, "SchemaName");
				dropStatements.Add($"DROP {(name1.StartsWith("f") ? "FUNCTION" : "PROCEDURE")} [{schema1}].[{name1}];");

			}


			dropStatements.ForEach(o =>
			{
				_task._logProcessor.Log(Source, " " + o);
				//Console.WriteLine("  " + o);
				ExecuteNonQuery(o);
			});
		}

		private void LoadLocalFiles()
		{
			string[] allSprocs = Directory.GetFiles(_sprocPath, "*.sql", SearchOption.TopDirectoryOnly);
			_userTypeFiles.AddRange(allSprocs);
		}

		private void LoadExistingStoredProcedures()
		{
			using var connection = CreateConnection();
			using var command = CreateRawCommand(connection, ListAllStoredProceduresQuery);
			using IDataReader reader = command.ExecuteReader();

			while (reader.Read())
				_existingUserTypes.Add(new UserType(reader));
		}
	}
}