using System;
using System.Windows;
using UnrealBinaryBuilder.Services;
using UnrealBinaryBuilder.UserControls;
using UnrealBinaryBuilderUpdater;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 更新管理器 - 处理应用程序更新逻辑
	/// </summary>
	public class UpdateManager : IDisposable
	{
		private readonly ILogger _logger;
		private readonly IProcessManager _processManager;
		private UBBUpdater _updater;
		private bool _updateAvailable = false;
		private bool _disposed = false;

		public event EventHandler<UpdateCheckEventArgs> UpdateCheckCompleted;
		public event EventHandler<UpdateDownloadEventArgs> UpdateDownloadProgress;
		public event EventHandler UpdateDownloadFinished;

		public bool IsUpdateAvailable => _updateAvailable;
		public bool IsCheckingForUpdates { get; private set; }
		public bool IsDownloadingUpdate { get; private set; }

		public UpdateManager(ILogger logger, IProcessManager processManager)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
		}

		/// <summary>
		/// 检查更新（静默模式）
		/// </summary>
		public void CheckForUpdatesSilently()
		{
			if (_processManager.IsProcessRunning)
			{
				_logger.LogWarning($"{_processManager.GetCurrentProcessName()} 正在运行，无法检查更新");
				OnUpdateCheckCompleted(new UpdateCheckEventArgs
				{
					Status = UpdateCheckStatus.ProcessRunning,
					Message = ResourceHelper.GetString("WarningBuildInProgress", _processManager.GetCurrentProcessName())
				});
				return;
			}

			if (IsCheckingForUpdates)
			{
				_logger.LogWarning("更新检查已在进行中");
				return;
			}

			try
			{
				IsCheckingForUpdates = true;
				if (_updater == null)
				{
					_updater = new UBBUpdater();
					_updater.SilentUpdateFinishedEventHandler += OnUpdateCheckFinished;
				}

				_logger.LogInfo("开始检查更新...");
				_updater.CheckForUpdatesSilently();
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "检查更新时发生错误");
				IsCheckingForUpdates = false;
				OnUpdateCheckCompleted(new UpdateCheckEventArgs
				{
					Status = UpdateCheckStatus.Error,
					Message = ResourceHelper.GetString("ErrorUpdateCheckFailed")
				});
			}
		}

		/// <summary>
		/// 下载更新
		/// </summary>
		public void DownloadUpdate()
		{
			if (!_updateAvailable)
			{
				_logger.LogWarning("没有可用的更新");
				return;
			}

			if (_processManager.IsProcessRunning)
			{
				_logger.LogWarning($"{_processManager.GetCurrentProcessName()} 正在运行，无法下载更新");
				return;
			}

			if (IsDownloadingUpdate)
			{
				_logger.LogWarning("更新下载已在进行中");
				return;
			}

			try
			{
				IsDownloadingUpdate = true;
				_updater.UpdateDownloadStartedEventHandler += OnUpdateDownloadStarted;
				_updater.UpdateDownloadFinishedEventHandler += OnUpdateDownloadFinished;
				_updater.UpdateProgressEventHandler += OnUpdateProgress;

				_logger.LogInfo("开始下载更新...");
				_updater.DownloadUpdate();
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "下载更新时发生错误");
				IsDownloadingUpdate = false;
			}
		}

		private void OnUpdateCheckFinished(object sender, UpdateProgressFinishedEventArgs e)
		{
			IsCheckingForUpdates = false;

			var args = new UpdateCheckEventArgs
			{
				Status = ConvertUpdateStatus(e.appUpdateCheckStatus),
				Version = e.castItem?.Version,
				Message = GetUpdateStatusMessage(e.appUpdateCheckStatus, e.castItem?.Version)
			};

			_updateAvailable = e.appUpdateCheckStatus == AppUpdateCheckStatus.UpdateAvailable;

			_logger.LogInfo($"更新检查完成: {args.Message}");
			OnUpdateCheckCompleted(args);
		}

		private void OnUpdateDownloadStarted(object sender, UpdateProgressDownloadStartEventArgs e)
		{
			_logger.LogInfo($"开始下载更新版本: {e.Version}");
			UpdateDownloadProgress?.Invoke(this, new UpdateDownloadEventArgs
			{
				Version = e.Version,
				TotalSize = e.UpdateSize,
				Progress = 0
			});
		}

		private void OnUpdateProgress(object sender, UpdateProgressDownloadEventArgs e)
		{
			UpdateDownloadProgress?.Invoke(this, new UpdateDownloadEventArgs
			{
				Progress = e.AppUpdateProgress
			});
		}

		private void OnUpdateDownloadFinished(object sender, UpdateProgressDownloadFinishEventArgs e)
		{
			IsDownloadingUpdate = false;
			_logger.LogInfo($"更新下载完成: {e.castItem?.Version}");

			// 清理事件订阅
			if (_updater != null)
			{
				_updater.UpdateDownloadStartedEventHandler -= OnUpdateDownloadStarted;
				_updater.UpdateDownloadFinishedEventHandler -= OnUpdateDownloadFinished;
				_updater.UpdateProgressEventHandler -= OnUpdateProgress;
			}

			UpdateDownloadFinished?.Invoke(this, EventArgs.Empty);
		}

		private UpdateCheckStatus ConvertUpdateStatus(AppUpdateCheckStatus status)
		{
			return status switch
			{
				AppUpdateCheckStatus.UpdateAvailable => UpdateCheckStatus.UpdateAvailable,
				AppUpdateCheckStatus.NoUpdate => UpdateCheckStatus.NoUpdate,
				AppUpdateCheckStatus.CouldNotDetermine => UpdateCheckStatus.Error,
				AppUpdateCheckStatus.UserSkip => UpdateCheckStatus.Skipped,
				_ => UpdateCheckStatus.Error
			};
		}

		private string GetUpdateStatusMessage(AppUpdateCheckStatus status, string version)
		{
			return status switch
			{
				AppUpdateCheckStatus.UpdateAvailable => ResourceHelper.GetString("MessageUpdateAvailable", version),
				AppUpdateCheckStatus.NoUpdate => ResourceHelper.GetString("MessageNoUpdate"),
				AppUpdateCheckStatus.CouldNotDetermine => ResourceHelper.GetString("ErrorUpdateCheckFailed"),
				_ => "未知状态"
			};
		}

		protected virtual void OnUpdateCheckCompleted(UpdateCheckEventArgs e)
		{
			UpdateCheckCompleted?.Invoke(this, e);
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				if (_updater != null)
				{
					_updater.SilentUpdateFinishedEventHandler -= OnUpdateCheckFinished;
					_updater.UpdateDownloadStartedEventHandler -= OnUpdateDownloadStarted;
					_updater.UpdateDownloadFinishedEventHandler -= OnUpdateDownloadFinished;
					_updater.UpdateProgressEventHandler -= OnUpdateProgress;
				}
				_disposed = true;
			}
		}
	}

	/// <summary>
	/// 更新检查事件参数
	/// </summary>
	public class UpdateCheckEventArgs : EventArgs
	{
		public UpdateCheckStatus Status { get; set; }
		public string Version { get; set; }
		public string Message { get; set; }
	}

	/// <summary>
	/// 更新检查状态
	/// </summary>
	public enum UpdateCheckStatus
	{
		UpdateAvailable,
		NoUpdate,
		Error,
		Skipped,
		ProcessRunning
	}

	/// <summary>
	/// 更新下载事件参数
	/// </summary>
	public class UpdateDownloadEventArgs : EventArgs
	{
		public string Version { get; set; }
		public long TotalSize { get; set; }
		public double Progress { get; set; }
	}
}