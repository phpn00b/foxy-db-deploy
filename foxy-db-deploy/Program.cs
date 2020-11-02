using System;
using System.Collections.Generic;
using System.IO;
using foxy_db_deploy.Logging;
using foxy_db_deploy.Tasks;

namespace foxy_db_deploy
{
	class Program
	{

		public const string DefaultHost = "localhost";
		public const string DefaultDatabase = "CasinoSystem";
		static void Main(string[] args)
		{
			Console.WriteLine(@"
example usage: foxy-db-deploy {host} {database}
If you call it like that it will directly execute.
I hope that was helpful");
			if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "Logs"))
				Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "Logs");

			List<BaseDatabaseTask> tasks = new List<BaseDatabaseTask>();
			string host = DefaultHost;
			string dbName = DefaultDatabase;
			bool skipInput = false;
			string user = null;
			string pass = null;
			string path = null;
			if (args != null && args.Length == 2)
			{
				host = args[0];
				dbName = args[1];
				skipInput = true;
			}
			else if (args.Length == 5)
			{
				host = args[0];
				dbName = args[1];
				user = args[2];
				pass = args[3];
				path = args[4];
				skipInput = true;
			}

			if (!skipInput)
			{
				Console.WriteLine($"Please enter the hostname to use. (default: {DefaultHost})");
				host = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(host))
					host = DefaultHost;
				Console.WriteLine($"Using host: {host}");
				Console.WriteLine($"Please enter the database to use. (default: {DefaultDatabase})");
				dbName = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(dbName))
					dbName = DefaultDatabase;
				Console.WriteLine($"Using database name: {dbName}");
			}
			else
			{
				Console.WriteLine($"Using host: {host}");
				Console.WriteLine($"Using database name: {dbName}");
				Console.WriteLine("Starting Update Process");
				Console.WriteLine("......");
				Console.WriteLine();
			}

			string masterConnectionString = $"Data Source={host};Initial Catalog=master;Integrated Security=True;MultipleActiveResultSets=True;Application Name=Database Maintenance;";
			string primaryDatabaseConnectionString = $"Data Source={host};Initial Catalog={dbName};Integrated Security=True;MultipleActiveResultSets=True;Application Name=Database Maintenance;";

			if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass))
			{
				masterConnectionString = $"Data Source={host};Initial Catalog=master;uid={user};pwd={pass};MultipleActiveResultSets=True;Application Name=Database Maintenance;";
				primaryDatabaseConnectionString = $"Data Source={host};Initial Catalog={dbName};uid={user};pwd={pass};MultipleActiveResultSets=True;Application Name=Database Maintenance;";
				Console.WriteLine("using credential format");
			}

			var processor = new LogProcessor();
			RunDatabaseUpdateTask task = new RunDatabaseUpdateTask(host, dbName, path, processor, path, primaryDatabaseConnectionString);
			task.ProcessUpdatePackage();
		}
	}
}
