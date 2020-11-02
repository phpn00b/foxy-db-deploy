using System;
using System.IO;
using System.IO.Compression;
using FoxHorn.CasinoSystem.DatabaseMaintiance;

namespace foxy_db_deploy.Tasks
{
	public class PrepareUpgradeDatapackageTask : BaseDatabaseTask
	{
		private string _sourcePath = AppDomain.CurrentDomain.BaseDirectory + "data-ingestion" + Path.DirectorySeparatorChar;
		private readonly string _targetPath = AppDomain.CurrentDomain.BaseDirectory + "data-working" + Path.DirectorySeparatorChar;
		private const string DataPackageFileName = "CasinoSystemDatabase.zip";
		private RunDatabaseUpdateTask _task;
		public PrepareUpgradeDatapackageTask(RunDatabaseUpdateTask runDatabaseUpdateTask)
			: base(null)
		{
			_task = runDatabaseUpdateTask;
			if (_task._updatePackageLocation != null)
				_sourcePath = _task._updatePackageLocation;
			else
			if (!Directory.Exists(_sourcePath))
			{
				Directory.CreateDirectory(_sourcePath);
			}
			if (!Directory.Exists(_targetPath))
			{
				Directory.CreateDirectory(_targetPath);
			}
			else
			{
				DirectoryInfo directoryInfo = new DirectoryInfo(_targetPath);
				directoryInfo.Delete(true);
				Directory.CreateDirectory(_targetPath);
			}
		}

		public override bool RunTask()
		{
			try
			{
				string fullPath = _sourcePath + DataPackageFileName;
				if (File.Exists(fullPath))
				{
					var zipFile = ZipFile.Read(fullPath);
					zipFile.ExtractAll(_targetPath);
				}

				return true;
			}
			catch (Exception e)
			{
				//LogManager.Current.Log.Error("failed extracting datapackage", e);
				_task._logProcessor.Log(Source, "failed unziping update package: " + e);
			}

			return false;
		}
	}
}