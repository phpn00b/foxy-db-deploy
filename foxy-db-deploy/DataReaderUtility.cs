using System;
using System.Data;
using System.Data.Common;
using System.IO;

namespace foxy_db_deploy
{
	/// <summary>
	/// Represents a set of <see cref="IDataReader"/> utility methods.
	/// </summary>
	public static class DataReaderUtility
	{
		/// <summary>
		/// Gets the bytes.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="columnName">Name of the column.</param>
		/// <returns></returns>
		public static byte[] GetBytes(IDataReader reader, string columnName)
		{
			int index = reader.GetOrdinal(columnName);
			if (reader.IsDBNull(index))
			{
				return null;
			}

			const int bufferSize = 1024;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(memoryStream))
				{
					long position = 0;
					long bytesRead;
					byte[] buffer = new byte[bufferSize];
					do
					{
						bytesRead = reader.GetBytes(0, position, buffer, 0, bufferSize);
						if (bytesRead > 0)
						{
							position += bytesRead;
							writer.Write(buffer);
						}

					} while (bytesRead > 0);

					writer.Flush();
					memoryStream.Seek(0, SeekOrigin.Begin);
					return memoryStream.GetBuffer();
				}
			}
		}

		/// <summary>
		/// Gets the value.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="reader">The reader.</param>
		/// <param name="columnName">Name of the column.</param>
		/// <returns></returns>
		public static T GetValue<T>(IDataReader reader, string columnName)
		{
			int index = reader.GetOrdinal(columnName);
			if (reader.IsDBNull(index))
			{
				return default(T);
			}
			return (T)reader.GetValue(index);
		}

		/// <summary>
		/// Gets the value.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="reader">The reader.</param>
		/// <param name="columnName">Name of the column.</param>
		/// <param name="destination">The destination.</param>
		/// <returns></returns>
		public static bool TryGetValue<T>(IDataReader reader, string columnName, ref T destination)
		{
			for (int i = 0; i < reader.FieldCount; ++i)
			{
				if (reader.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase))
				{
					destination = GetValue<T>(reader, columnName);
					return true;
				}

			}
			return false;
		}

		/// <summary>
		/// Gets the value.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="reader">The reader.</param>
		/// <param name="columnName">Name of the column.</param>
		/// <returns></returns>
		public static T GetValue<T>(DbDataReader reader, string columnName)
		{
			int index = reader.GetOrdinal(columnName);
			if (reader.IsDBNull(index))
			{
				return default(T);
			}
			return (T)reader.GetValue(index);
		}
	}
}