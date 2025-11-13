using System;
using System.IO;
using System.Linq;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 文件系统辅助类
	/// </summary>
	public static class FileSystemHelper
	{
		/// <summary>
		/// 安全地创建目录
		/// </summary>
		public static bool SafeCreateDirectory(string path)
		{
			try
			{
				if (!Directory.Exists(path))
				{
					Directory.CreateDirectory(path);
				}
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// 安全地删除文件
		/// </summary>
		public static bool SafeDeleteFile(string filePath)
		{
			try
			{
				if (File.Exists(filePath))
				{
					File.Delete(filePath);
				}
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// 安全地删除目录
		/// </summary>
		public static bool SafeDeleteDirectory(string directoryPath, bool recursive = true)
		{
			try
			{
				if (Directory.Exists(directoryPath))
				{
					Directory.Delete(directoryPath, recursive);
				}
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// 获取目录大小（字节）
		/// </summary>
		public static long GetDirectorySize(string directoryPath, bool includeSubdirectories = true)
		{
			if (!Directory.Exists(directoryPath))
				return 0;

			try
			{
				var directory = new DirectoryInfo(directoryPath);
				long size = directory.GetFiles().Sum(file => file.Length);

				if (includeSubdirectories)
				{
					size += directory.GetDirectories().Sum(subDir => GetDirectorySize(subDir.FullName, true));
				}

				return size;
			}
			catch
			{
				return 0;
			}
		}

		/// <summary>
		/// 格式化文件大小
		/// </summary>
		public static string FormatFileSize(long bytes)
		{
			string[] sizes = { "B", "KB", "MB", "GB", "TB" };
			if (bytes == 0)
				return "0" + sizes[0];

			int place = (int)Math.Floor(Math.Log(bytes, 1024));
			double num = Math.Round(bytes / Math.Pow(1024, place), 1);
			return $"{num} {sizes[place]}";
		}

		/// <summary>
		/// 检查目录是否可写
		/// </summary>
		public static bool IsDirectoryWritable(string directoryPath)
		{
			if (!Directory.Exists(directoryPath))
				return false;

			try
			{
				string testFile = Path.Combine(directoryPath, Guid.NewGuid().ToString());
				File.WriteAllText(testFile, "test");
				File.Delete(testFile);
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// 确保路径存在（创建所有必要的目录）
		/// </summary>
		public static bool EnsurePathExists(string filePath)
		{
			try
			{
				string directory = Path.GetDirectoryName(filePath);
				if (!string.IsNullOrEmpty(directory))
				{
					SafeCreateDirectory(directory);
				}
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}

