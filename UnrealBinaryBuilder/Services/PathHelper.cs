using System;
using System.IO;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 路径辅助类 - 提供路径相关的辅助方法
	/// </summary>
	public static class PathHelper
	{
		/// <summary>
		/// 规范化路径（统一使用反斜杠）
		/// </summary>
		public static string NormalizePath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return path;

			return Path.GetFullPath(path);
		}

		/// <summary>
		/// 检查路径是否为有效的目录路径
		/// </summary>
		public static bool IsValidDirectoryPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return false;

			try
			{
				Path.GetFullPath(path);
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// 检查路径是否为有效的文件路径
		/// </summary>
		public static bool IsValidFilePath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return false;

			try
			{
				string directory = Path.GetDirectoryName(path);
				if (string.IsNullOrEmpty(directory))
					return false;

				Path.GetFullPath(path);
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// 获取相对路径
		/// </summary>
		public static string GetRelativePath(string fromPath, string toPath)
		{
			if (string.IsNullOrWhiteSpace(fromPath) || string.IsNullOrWhiteSpace(toPath))
				return toPath;

			try
			{
				Uri fromUri = new Uri(Path.GetFullPath(fromPath) + Path.DirectorySeparatorChar);
				Uri toUri = new Uri(Path.GetFullPath(toPath));

				Uri relativeUri = fromUri.MakeRelativeUri(toUri);
				return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar));
			}
			catch
			{
				return toPath;
			}
		}

		/// <summary>
		/// 确保路径以目录分隔符结尾
		/// </summary>
		public static string EnsureTrailingSeparator(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return path;

			if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()) && 
			    !path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
			{
				return path + Path.DirectorySeparatorChar;
			}

			return path;
		}

		/// <summary>
		/// 移除路径末尾的目录分隔符
		/// </summary>
		public static string RemoveTrailingSeparator(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return path;

			while (path.EndsWith(Path.DirectorySeparatorChar.ToString()) || 
			       path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
			{
				path = path.Substring(0, path.Length - 1);
			}

			return path;
		}

		/// <summary>
		/// 组合路径（自动处理分隔符）
		/// </summary>
		public static string Combine(params string[] paths)
		{
			if (paths == null || paths.Length == 0)
				return string.Empty;

			string result = paths[0];
			for (int i = 1; i < paths.Length; i++)
			{
				if (!string.IsNullOrWhiteSpace(paths[i]))
				{
					result = Path.Combine(result, paths[i]);
				}
			}

			return result;
		}

		/// <summary>
		/// 检查路径是否在指定目录下
		/// </summary>
		public static bool IsPathUnderDirectory(string path, string directory)
		{
			if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory))
				return false;

			try
			{
				string fullPath = Path.GetFullPath(path);
				string fullDirectory = Path.GetFullPath(directory);
				return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
			}
			catch
			{
				return false;
			}
		}
	}
}

