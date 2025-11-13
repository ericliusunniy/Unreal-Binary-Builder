using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 后台任务管理器 - 管理后台任务
	/// </summary>
	public class BackgroundTaskManager : IDisposable
	{
		private readonly ILogger _logger;
		private readonly Dictionary<string, CancellationTokenSource> _tasks = new Dictionary<string, CancellationTokenSource>();
		private readonly object _lockObject = new object();
		private bool _disposed = false;

		public BackgroundTaskManager(ILogger logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// 启动后台任务
		/// </summary>
		public Task StartTaskAsync(string taskName, Func<CancellationToken, Task> taskFunc)
		{
			if (string.IsNullOrWhiteSpace(taskName))
				throw new ArgumentException("任务名称不能为空", nameof(taskName));
			if (taskFunc == null)
				throw new ArgumentNullException(nameof(taskFunc));

			lock (_lockObject)
			{
				// 如果任务已存在，先取消
				if (_tasks.ContainsKey(taskName))
				{
					_tasks[taskName].Cancel();
					_tasks.Remove(taskName);
				}

				var cts = new CancellationTokenSource();
				_tasks[taskName] = cts;

				_logger.LogInfo($"后台任务已启动: {taskName}");

				return Task.Run(async () =>
				{
					try
					{
						await taskFunc(cts.Token);
						_logger.LogInfo($"后台任务已完成: {taskName}");
					}
					catch (OperationCanceledException)
					{
						_logger.LogInfo($"后台任务已取消: {taskName}");
					}
					catch (Exception ex)
					{
						_logger.LogException(ex, $"后台任务失败: {taskName}");
					}
					finally
					{
						lock (_lockObject)
						{
							_tasks.Remove(taskName);
							cts.Dispose();
						}
					}
				}, cts.Token);
			}
		}

		/// <summary>
		/// 取消任务
		/// </summary>
		public void CancelTask(string taskName)
		{
			if (string.IsNullOrWhiteSpace(taskName))
				return;

			lock (_lockObject)
			{
				if (_tasks.TryGetValue(taskName, out var cts))
				{
					cts.Cancel();
					_logger.LogInfo($"后台任务已请求取消: {taskName}");
				}
			}
		}

		/// <summary>
		/// 取消所有任务
		/// </summary>
		public void CancelAllTasks()
		{
			lock (_lockObject)
			{
				foreach (var kvp in _tasks)
				{
					kvp.Value.Cancel();
					_logger.LogInfo($"后台任务已请求取消: {kvp.Key}");
				}
			}
		}

		/// <summary>
		/// 检查任务是否正在运行
		/// </summary>
		public bool IsTaskRunning(string taskName)
		{
			lock (_lockObject)
			{
				return _tasks.ContainsKey(taskName);
			}
		}

		/// <summary>
		/// 获取所有运行中的任务
		/// </summary>
		public IReadOnlyList<string> GetRunningTasks()
		{
			lock (_lockObject)
			{
				return new List<string>(_tasks.Keys).AsReadOnly();
			}
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				CancelAllTasks();

				lock (_lockObject)
				{
					foreach (var cts in _tasks.Values)
					{
						cts.Dispose();
					}
					_tasks.Clear();
				}

				_disposed = true;
			}
		}
	}
}

