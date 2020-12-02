using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace foxy_db_deploy.Tasks
{
	public class UpdateAllProceduresTask : BaseDatabaseTask
	{
		private const string ListAllStoredProceduresQuery = @"
select
		SCHEMA_NAME(pro.schema_id) as [Schema],
		pro.name  as SchemaStoredProcedureName,
		SCHEMA_NAME(pro.schema_id)+'.'+pro.name as StoredProcedureName,
		OBJECT_DEFINITION (pro.object_id) as FullText,
		pro.create_date as CreatedDate,
		pro.modify_date as LastAlteredDate
	FROM
		sys.procedures pro
	ORDER BY
		SCHEMA_NAME(pro.schema_id)+'.'+pro.name ASC;";

		public class StoredProcedure
		{
			public StoredProcedure(IDataReader reader)
			{
				Schema = DataReaderUtility.GetValue<string>(reader, "Schema");
				SchemaStoredProcedureName = DataReaderUtility.GetValue<string>(reader, "SchemaStoredProcedureName");
				StoredProcedureName = DataReaderUtility.GetValue<string>(reader, "StoredProcedureName");
				FullText = DataReaderUtility.GetValue<string>(reader, "FullText");
				CreatedDate = DataReaderUtility.GetValue<DateTime>(reader, "CreatedDate");
				LastAlteredDate = DataReaderUtility.GetValue<DateTime>(reader, "LastAlteredDate");
			}
			public string Schema { get; set; }

			public string SchemaStoredProcedureName { get; set; }

			public string StoredProcedureName { get; set; }

			public DateTime CreatedDate { get; set; }

			public DateTime LastAlteredDate { get; set; }

			public string FileName => StoredProcedureName + ".sql";
			public string FullText { get; set; }

			public string DatabaseHash
			{
				get
				{
					if (!string.IsNullOrWhiteSpace(FullText))
						return Hash(FullText);
					return string.Empty;
				}
			}

			public string Hash(string input)
			{
				if (string.IsNullOrWhiteSpace(input))
					return string.Empty;
				var hash = new SHA1Managed().ComputeHash(Encoding.UTF8.GetBytes(input.ToLower()));
				return string.Concat(hash.Select(b => b.ToString("x2")));
			}
		}

		private readonly RunDatabaseUpdateTask _task;
		private readonly string _sprocPath;
		public UpdateAllProceduresTask(string connectionString, RunDatabaseUpdateTask runDatabaseUpdateTask)
			: base(connectionString)
		{
			_task = runDatabaseUpdateTask;
			if (runDatabaseUpdateTask._useUnzipedFolderPath != null)
				_sprocPath = $"{runDatabaseUpdateTask._updatePackageLocation}{runDatabaseUpdateTask._databaseFolderName}{Path.DirectorySeparatorChar}Procedures";
			else
				_sprocPath = $"{AppDomain.CurrentDomain.BaseDirectory}data-working{Path.DirectorySeparatorChar}{runDatabaseUpdateTask._databaseFolderName}{Path.DirectorySeparatorChar}Procedures";
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
				_task._logProcessor.Log(Source, $"Database has {_existingStoredProcedures.Count} sprocs and deploy package has {_storedProcedureFiles.Count} sprocs. Starting sync process now!");
				foreach (var storedProcedureFile in _storedProcedureFiles)
				{
					ProcessFile(storedProcedureFile);
					Thread.Sleep(1);
				}
				return true;
			}
			catch (Exception e)
			{
				_task._logProcessor.Log(Source, "failed syncing stored procedures", attachment: e.ToString());
			}

			return false;
		}


		private void ProcessFile(string path)
		{
			FileInfo fileInfo = new FileInfo(path);

			string name = fileInfo.Name;
			bool isNew = true;
			string commandText = null;
			try
			{
				//if (_doPortalCopy)

				StoredProcedure existingProcedure = _existingStoredProcedures.FirstOrDefault(o => o.FileName == name);
				commandText = File.ReadAllText(path);
				string newHash = string.Empty;
				if (existingProcedure != null)
				{
					isNew = false;
					newHash = existingProcedure.Hash(commandText);

					commandText = Regex.Replace(commandText, "create(.*)procedure", "alter procedure", RegexOptions.IgnoreCase);
					//	if(commandText.Contains(""))
				}
				if (newHash == existingProcedure?.DatabaseHash)
				{
					_task._logProcessor.Log(Source, $"skipping sproc {name} as the version in the DB has the same hash");
				}
				else
				{
					int code = ExecuteNonQuery(commandText);

					_task._logProcessor.Log(Source, $"finished with {(isNew ? "create" : "alter")} {name} have return code: {code}");
				}
				//fileInfo.Delete();
			}
			catch (Exception e)
			{
				_task._logProcessor.Log(Source, "failed processing sproc file: " + path, attachment: e.ToString());
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
			using var connection = CreateConnection();
			using var command = CreateRawCommand(connection, ListAllStoredProceduresQuery);
			using IDataReader reader = command.ExecuteReader();

			while (reader.Read())
				_existingStoredProcedures.Add(new StoredProcedure(reader));
		}
	}
}