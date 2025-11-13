using System;
using System.Threading.Tasks;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 构建管理器接口
	/// </summary>
	public interface IBuildManager
	{
		/// <summary>
		/// 是否正在构建
		/// </summary>
		bool IsBuilding { get; }

		/// <summary>
		/// 最后一次构建是否成功
		/// </summary>
		bool LastBuildSuccess { get; }

		/// <summary>
		/// 构建完成事件
		/// </summary>
		event EventHandler<BuildFinishedEventArgs> BuildFinished;

	/// <summary>
	/// 异步构建引擎
	/// </summary>
	Task<bool> BuildEngineAsync(string automationExePath, string commandLineArgs);

	/// <summary>
	/// 异步构建项目
	/// </summary>
	Task<bool> BuildProjectAsync(string runUatPath, string commandLineArgs);

	/// <summary>
	/// 停止构建
	/// </summary>
	void StopBuild();

		/// <summary>
		/// 重置构建状态
		/// </summary>
		void ResetBuildState();
	}

	/// <summary>
	/// 构建完成事件参数
	/// </summary>
	public class BuildFinishedEventArgs : EventArgs
	{
		public bool Success { get; set; }
		public int ExitCode { get; set; }
		public int ErrorCount { get; set; }
		public int WarningCount { get; set; }
		public TimeSpan ElapsedTime { get; set; }
	}
}

