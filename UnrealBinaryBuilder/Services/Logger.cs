using System;
using System.IO;
using UnrealBinaryBuilder.Classes;
using UnrealBinaryBuilder.UserControls;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 统一日志服务实现
	/// </summary>
	public class Logger : ILogger, IDisposable
	{
		private readonly string _logFilePath;
		private readonly string _errorLogFilePath;
		private readonly object _lockObject = new object();
		private bool _disposed = false;

		public Logger()
		{
			try
			{
				string logBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UnrealBinaryBuilder", "Logs");
				_logFilePath = Path.Combine(logBasePath, "UnrealBinaryBuilder.log");
				_errorLogFilePath = Path.Combine(logBasePath, "BuildErrors.log");

				// 确保日志目录存在
				if (!Directory.Exists(logBasePath))
				{
					Directory.CreateDirectory(logBasePath);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"初始化日志路径失败: {ex.Message}");
				_logFilePath = Path.Combine(Path.GetTempPath(), "UnrealBinaryBuilder.log");
				_errorLogFilePath = Path.Combine(Path.GetTempPath(), "BuildErrors.log");
			}
		}

		public void LogInfo(string message)
		{
			if (string.IsNullOrWhiteSpace(message))
				return;

			WriteToFile(_logFilePath, $"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
			System.Diagnostics.Debug.WriteLine($"[INFO] {message}");
		}

		public void LogWarning(string message)
		{
			if (string.IsNullOrWhiteSpace(message))
				return;

			WriteToFile(_logFilePath, $"[WARNING] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
			System.Diagnostics.Debug.WriteLine($"[WARNING] {message}");
		}

		public void LogError(string message, Exception exception = null)
		{
			if (string.IsNullOrWhiteSpace(message) && exception == null)
				return;

			string errorMessage = $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
			if (exception != null)
			{
				errorMessage += $"\n异常详情: {exception.Message}\n堆栈跟踪: {exception.StackTrace}";
			}

			WriteToFile(_logFilePath, errorMessage);
			WriteToFile(_errorLogFilePath, errorMessage);
			System.Diagnostics.Debug.WriteLine($"[ERROR] {message}", exception);
		}

		public void LogDebug(string message)
		{
			if (string.IsNullOrWhiteSpace(message))
				return;

#if DEBUG
			WriteToFile(_logFilePath, $"[DEBUG] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
			System.Diagnostics.Debug.WriteLine($"[DEBUG] {message}");
#endif
		}

		public void LogException(Exception exception, string context = null)
		{
			if (exception == null)
				return;

			string errorMessage = $"[EXCEPTION] {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
			if (!string.IsNullOrWhiteSpace(context))
			{
				errorMessage += $" - 上下文: {context}";
			}
			errorMessage += $"\n异常类型: {exception.GetType().Name}";
			errorMessage += $"\n异常消息: {exception.Message}";
			errorMessage += $"\n堆栈跟踪:\n{exception.StackTrace}";

			if (exception.InnerException != null)
			{
				errorMessage += $"\n内部异常: {exception.InnerException.Message}";
			}

			WriteToFile(_logFilePath, errorMessage);
			WriteToFile(_errorLogFilePath, errorMessage);
			System.Diagnostics.Debug.WriteLine($"[EXCEPTION] {context ?? "未知上下文"}: {exception.Message}", exception);
		}

		private void WriteToFile(string filePath, string message)
		{
			try
			{
				lock (_lockObject)
				{
					File.AppendAllText(filePath, message + Environment.NewLine);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"写入日志文件失败: {ex.Message}");
			}
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				_disposed = true;
			}
		}
	}
}

