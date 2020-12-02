using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Windows.Input;
using foxy_db_deploy.Model;
using Microsoft.SqlServer.Management.Smo;

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
		/// <returns>true if finished and can go to next task</returns>
		public abstract bool RunTask();

		public SqlConnection CreateConnection()
		{
			return new SqlConnection(ConnectionString);
		}

		private DatabaseObjectHash[] ReadObjectHashes(IDataReader reader)
		{
			List<DatabaseObjectHash> entities = new List<DatabaseObjectHash>();
			while (reader.Read())
			{
				entities.Add(new DatabaseObjectHash
				{
					SchemaName = DataReaderUtility.GetValue<string>(reader, "SchemaName"),
					ObjectName = DataReaderUtility.GetValue<string>(reader, "ObjectName"),
					ObjectType = DataReaderUtility.GetValue<string>(reader, "ObjectType"),
					ObjectHash = DataReaderUtility.GetValue<string>(reader, "ObjectHash"),
					CreatedDate = DataReaderUtility.GetValue<DateTime>(reader, "CreatedDate"),
					LastUpdated = DataReaderUtility.GetValue<DateTime>(reader, "LastUpdated"),
					FileName = DataReaderUtility.GetValue<string>(reader, "FileName")
				});
			}

			return entities.ToArray();
		}

		protected DbCommand CreateCommand(DbConnection connection, string commandName)
		{
			if (connection.State != ConnectionState.Open)
				connection.Open();

			var command = connection.CreateCommand();
			command.CommandText = commandName;
			command.CommandType = CommandType.StoredProcedure;
			return command;
		}


		protected DbCommand CreateRawCommand(DbConnection connection, string commandText)
		{
			if (connection.State != ConnectionState.Open)
				connection.Open();

			var command = connection.CreateCommand();
			command.CommandText = commandText;
			command.CommandType = CommandType.Text;
			return command;
		}

		protected DatabaseObjectHash[] LoadObjectHashes(string query)
		{
			using var connection = CreateConnection();
			using DbCommand command = CreateRawCommand(connection, query);
			using IDataReader reader = command.ExecuteReader();
			return ReadObjectHashes(reader);
		}

		protected int ExecuteNonQuery(string query)
		{
			using var connection = CreateConnection();
			using DbCommand command = CreateRawCommand(connection, query);
			return command.ExecuteNonQuery();
		}
	}
}