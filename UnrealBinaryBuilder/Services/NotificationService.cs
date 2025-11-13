using System;
using System.Windows;
using HandyControl.Controls;
using HandyControl.Data;
using UnrealBinaryBuilder.Services;
using UnrealBinaryBuilder.UserControls;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 通知服务 - 统一管理用户通知（Toast 消息）
	/// </summary>
	public class NotificationService
	{
		private readonly ILogger _logger;

		public NotificationService(ILogger logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// 显示信息通知
		/// </summary>
		public void ShowInfo(string message, bool showCloseButton = true, bool staysOpen = false, string token = "", int waitTime = 3)
		{
			ShowNotification(message, LogViewer.EMessageType.Info, showCloseButton, staysOpen, token, waitTime);
		}

		/// <summary>
		/// 显示警告通知
		/// </summary>
		public void ShowWarning(string message, bool showCloseButton = true, bool staysOpen = false, string token = "", int waitTime = 3)
		{
			ShowNotification(message, LogViewer.EMessageType.Warning, showCloseButton, staysOpen, token, waitTime);
		}

		/// <summary>
		/// 显示错误通知
		/// </summary>
		public void ShowError(string message, bool showCloseButton = true, bool staysOpen = false, string token = "", int waitTime = 3)
		{
			ShowNotification(message, LogViewer.EMessageType.Error, showCloseButton, staysOpen, token, waitTime);
		}

		/// <summary>
		/// 显示通知
		/// </summary>
		private void ShowNotification(string message, LogViewer.EMessageType type, bool showCloseButton, bool staysOpen, string token, int waitTime)
		{
			if (string.IsNullOrWhiteSpace(message))
				return;

			try
			{
				Application.Current?.Dispatcher.InvokeAsync(() =>
				{
					Growl.Clear(token);
					var growlInfo = new GrowlInfo
					{
						Message = message,
						ShowDateTime = false,
						ShowCloseButton = showCloseButton,
						StaysOpen = staysOpen,
						Token = token,
						WaitTime = waitTime
					};

					switch (type)
					{
						case LogViewer.EMessageType.Info:
							Growl.Info(growlInfo);
							break;
						case LogViewer.EMessageType.Warning:
							Growl.Warning(growlInfo);
							break;
						case LogViewer.EMessageType.Error:
							Growl.Error(growlInfo);
							break;
					}

					_logger.LogDebug($"通知已显示: {type} - {message}");
				});
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "显示通知时发生错误");
			}
		}

		/// <summary>
		/// 清除指定 token 的通知
		/// </summary>
		public void Clear(string token)
		{
			try
			{
				Application.Current?.Dispatcher.InvokeAsync(() =>
				{
					Growl.Clear(token);
				});
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "清除通知时发生错误");
			}
		}

		/// <summary>
		/// 清除所有通知
		/// </summary>
		public void ClearAll()
		{
			try
			{
				Application.Current?.Dispatcher.InvokeAsync(() =>
				{
					Growl.Clear();
				});
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "清除所有通知时发生错误");
			}
		}
	}
}

