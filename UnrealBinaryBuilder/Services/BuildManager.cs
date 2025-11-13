using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 构建管理器实现
	/// </summary>
	public class BuildManager : IBuildManager
	{
		private readonly IProcessManager _processManager;
		private readonly ILogger _logger;
		private readonly Stopwatch _stopwatch = new Stopwatch();

		private bool _isBuilding = false;
		private bool _lastBuildSuccess = false;
		private int _errorCount = 0;
		private int _warningCount = 0;

		public bool IsBuilding => _isBuilding;
		public bool LastBuildSuccess => _lastBuildSuccess;

		public event EventHandler<BuildFinishedEventArgs> BuildFinished;

		public BuildManager(IProcessManager processManager, ILogger logger)
		{
			_processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));

			_processManager.ProcessExited += OnProcessExited;
			_processManager.ErrorDataReceived += OnErrorDataReceived;
		}

		public Task<bool> BuildEngineAsync(string automationExePath, string commandLineArgs)
		{
			if (_isBuilding)
			{
				_logger.LogWarning("构建已在进行中，无法启动新的构建");
				return Task.FromResult(false);
			}

			if (string.IsNullOrWhiteSpace(automationExePath))
			{
				_logger.LogError("AutomationExePath 不能为空");
				return Task.FromResult(false);
			}

			try
			{
				_isBuilding = true;
				_errorCount = 0;
				_warningCount = 0;
				_stopwatch.Restart();

				var startInfo = new ProcessStartInfo
				{
					FileName = automationExePath,
					Arguments = commandLineArgs,
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardError = true,
					RedirectStandardOutput = true
				};

				bool started = _processManager.StartProcess(startInfo);
				if (!started)
				{
					_isBuilding = false;
					return Task.FromResult(false);
				}

				_logger.LogInfo($"构建已启动: {automationExePath}");
				_logger.LogInfo($"命令行参数: {commandLineArgs}");

				return Task.FromResult(true);
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "启动构建时发生错误");
				_isBuilding = false;
				return Task.FromResult(false);
			}
		}

		public void StopBuild()
		{
			if (_isBuilding)
			{
				_logger.LogInfo("正在停止构建...");
				_processManager.CloseProcess(true);
			}
		}

		public void ResetBuildState()
		{
			_isBuilding = false;
			_lastBuildSuccess = false;
			_errorCount = 0;
			_warningCount = 0;
			_stopwatch.Reset();
		}

		private void OnProcessExited(object sender, ProcessExitedEventArgs e)
		{
			_stopwatch.Stop();
			_isBuilding = false;
			_lastBuildSuccess = e.Success;

			_logger.LogInfo($"构建完成，退出代码: {e.ExitCode}，错误数: {_errorCount}，警告数: {_warningCount}，耗时: {_stopwatch.Elapsed:hh\\:mm\\:ss}");

			BuildFinished?.Invoke(this, new BuildFinishedEventArgs
			{
				Success = e.Success,
				ExitCode = e.ExitCode,
				ErrorCount = _errorCount,
				WarningCount = _warningCount,
				ElapsedTime = _stopwatch.Elapsed
			});
		}

		private void OnErrorDataReceived(object sender, string data)
		{
			if (string.IsNullOrWhiteSpace(data))
				return;

			// 简单的错误检测逻辑，可以根据需要扩展
			if (data.Contains("error", StringComparison.OrdinalIgnoreCase) ||
			    data.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
			{
				_errorCount++;
			}
			else if (data.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
			         data.Contains("WARNING", StringComparison.OrdinalIgnoreCase))
			{
				_warningCount++;
			}
		}
	}
}

