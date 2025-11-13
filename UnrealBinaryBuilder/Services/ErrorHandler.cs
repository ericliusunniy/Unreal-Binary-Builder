using System;
using System.IO;
using System.Windows;
using UnrealBinaryBuilder.Services;
using UnrealBinaryBuilder.UserControls;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 错误处理器 - 统一处理应用程序错误
	/// </summary>
	public class ErrorHandler
	{
		private readonly ILogger _logger;
		private readonly Action<string, LogViewer.EMessageType> _showToastMessage;

		public ErrorHandler(ILogger logger, Action<string, LogViewer.EMessageType> showToastMessage = null)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_showToastMessage = showToastMessage;
		}

		/// <summary>
		/// 处理异常
		/// </summary>
		public void HandleException(Exception exception, string context = null, bool showToUser = false)
		{
			if (exception == null)
				return;

			// 记录异常
			_logger.LogException(exception, context);

			// 显示给用户（如果需要）
			if (showToUser)
			{
				string userMessage = GetUserFriendlyMessage(exception, context);
				ShowErrorToUser(userMessage);
			}
		}

		/// <summary>
		/// 处理错误消息
		/// </summary>
		public void HandleError(string errorMessage, bool showToUser = false)
		{
			if (string.IsNullOrWhiteSpace(errorMessage))
				return;

			_logger.LogError(errorMessage);

			if (showToUser)
			{
				ShowErrorToUser(errorMessage);
			}
		}

		/// <summary>
		/// 处理异常（重载方法，兼容旧代码）
		/// </summary>
		public void HandleError(Exception exception, string context = null)
		{
			HandleException(exception, context, false);
		}

		/// <summary>
		/// 安全执行操作（捕获异常）
		/// </summary>
		public bool SafeExecute(Action action, string context = null, bool showToUser = false)
		{
			try
			{
				action();
				return true;
			}
			catch (Exception ex)
			{
				HandleException(ex, context, showToUser);
				return false;
			}
		}

		/// <summary>
		/// 安全执行操作并返回结果
		/// </summary>
		public T SafeExecute<T>(Func<T> func, T defaultValue = default(T), string context = null, bool showToUser = false)
		{
			try
			{
				return func();
			}
			catch (Exception ex)
			{
				HandleException(ex, context, showToUser);
				return defaultValue;
			}
		}

		/// <summary>
		/// 安全执行异步操作
		/// </summary>
		public async System.Threading.Tasks.Task<bool> SafeExecuteAsync(Func<System.Threading.Tasks.Task> asyncAction, string context = null, bool showToUser = false)
		{
			try
			{
				await asyncAction();
				return true;
			}
			catch (Exception ex)
			{
				HandleException(ex, context, showToUser);
				return false;
			}
		}

		/// <summary>
		/// 获取用户友好的错误消息
		/// </summary>
		private string GetUserFriendlyMessage(Exception exception, string context)
		{
			string message = exception.GetType().Name switch
			{
				nameof(UnauthorizedAccessException) => "访问被拒绝。请检查文件权限。",
				nameof(FileNotFoundException) => "文件未找到。请检查文件路径。",
				nameof(DirectoryNotFoundException) => "目录未找到。请检查目录路径。",
				nameof(OutOfMemoryException) => "内存不足。请关闭其他应用程序后重试。",
				nameof(TimeoutException) => "操作超时。请稍后重试。",
				nameof(InvalidOperationException) => "操作无效。请检查操作状态。",
				_ => "发生错误。请查看日志获取详细信息。"
			};

			if (!string.IsNullOrWhiteSpace(context))
			{
				message = $"{context}: {message}";
			}

			return message;
		}

		/// <summary>
		/// 向用户显示错误
		/// </summary>
		private void ShowErrorToUser(string message)
		{
			if (_showToastMessage != null)
			{
				Application.Current?.Dispatcher.InvokeAsync(() =>
				{
					_showToastMessage(message, LogViewer.EMessageType.Error);
				});
			}
			else
			{
				// 如果没有提供显示方法，使用 MessageBox
				Application.Current?.Dispatcher.InvokeAsync(() =>
				{
					MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
				});
			}
		}
	}
}

