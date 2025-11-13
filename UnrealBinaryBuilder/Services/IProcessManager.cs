using System;
using System.Diagnostics;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 进程管理接口
	/// </summary>
	public interface IProcessManager : IDisposable
	{
		/// <summary>
		/// 当前运行的进程
		/// </summary>
		Process CurrentProcess { get; }

		/// <summary>
		/// 是否有进程正在运行
		/// </summary>
		bool IsProcessRunning { get; }

		/// <summary>
		/// 进程退出事件
		/// </summary>
		event EventHandler<ProcessExitedEventArgs> ProcessExited;

		/// <summary>
		/// 进程输出数据接收事件
		/// </summary>
		event EventHandler<string> OutputDataReceived;

		/// <summary>
		/// 进程错误数据接收事件
		/// </summary>
		event EventHandler<string> ErrorDataReceived;

		/// <summary>
		/// 启动进程
		/// </summary>
		bool StartProcess(ProcessStartInfo startInfo);

		/// <summary>
		/// 关闭进程
		/// </summary>
		void CloseProcess(bool killProcess = false);

		/// <summary>
		/// 获取当前进程名称
		/// </summary>
		string GetCurrentProcessName();
	}

	/// <summary>
	/// 进程退出事件参数
	/// </summary>
	public class ProcessExitedEventArgs : EventArgs
	{
		public int ExitCode { get; set; }
		public bool Success => ExitCode == 0;
	}
}

