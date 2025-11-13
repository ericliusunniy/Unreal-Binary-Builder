using System;
using System.Diagnostics;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 进程管理器实现
	/// </summary>
	public class ProcessManager : IProcessManager, IDisposable
	{
		private Process _currentProcess;
		private bool _disposed = false;
		private readonly ILogger _logger;

		public Process CurrentProcess => _currentProcess;
		public bool IsProcessRunning => _currentProcess != null && !_currentProcess.HasExited;

		public event EventHandler<ProcessExitedEventArgs> ProcessExited;
		public event EventHandler<string> OutputDataReceived;
		public event EventHandler<string> ErrorDataReceived;

		public ProcessManager(ILogger logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		public bool StartProcess(ProcessStartInfo startInfo)
		{
			if (startInfo == null)
			{
				_logger.LogError("ProcessStartInfo 不能为 null");
				return false;
			}

			if (!System.IO.File.Exists(startInfo.FileName))
			{
				_logger.LogError($"文件不存在: {startInfo.FileName}");
				return false;
			}

			try
			{
				_currentProcess = new Process
				{
					StartInfo = startInfo,
					EnableRaisingEvents = true
				};

				_currentProcess.OutputDataReceived += (sender, e) =>
				{
					if (!string.IsNullOrWhiteSpace(e.Data))
					{
						OutputDataReceived?.Invoke(this, e.Data);
					}
				};

				_currentProcess.ErrorDataReceived += (sender, e) =>
				{
					if (!string.IsNullOrWhiteSpace(e.Data))
					{
						ErrorDataReceived?.Invoke(this, e.Data);
					}
				};

				_currentProcess.Exited += (sender, e) =>
				{
					var exitCode = _currentProcess?.ExitCode ?? -1;
					ProcessExited?.Invoke(this, new ProcessExitedEventArgs { ExitCode = exitCode });
				};

				_currentProcess.Start();
				_currentProcess.BeginOutputReadLine();
				_currentProcess.BeginErrorReadLine();

				_logger.LogInfo($"进程已启动: {startInfo.FileName}");
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, $"启动进程失败: {startInfo.FileName}");
				CloseProcess();
				return false;
			}
		}

		public void CloseProcess(bool killProcess = false)
		{
			if (_currentProcess == null)
				return;

			try
			{
				if (killProcess && !_currentProcess.HasExited)
				{
					_currentProcess.Kill(true);
					_logger.LogInfo($"进程已被强制终止: {GetCurrentProcessName()}");
				}
				else if (!_currentProcess.HasExited)
				{
					_currentProcess.Close();
				}
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "关闭进程时发生错误");
			}
			finally
			{
				_currentProcess?.Dispose();
				_currentProcess = null;
			}
		}

		public string GetCurrentProcessName()
		{
			return _currentProcess?.ProcessName ?? "无";
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				CloseProcess(true);
				_disposed = true;
			}
		}
	}
}

