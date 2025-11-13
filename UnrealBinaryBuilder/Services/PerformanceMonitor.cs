using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 性能监控器 - 监控操作性能
	/// </summary>
	public class PerformanceMonitor : IDisposable
	{
		private readonly ILogger _logger;
		private readonly Dictionary<string, Stopwatch> _activeOperations = new Dictionary<string, Stopwatch>();
		private readonly Dictionary<string, List<TimeSpan>> _operationHistory = new Dictionary<string, List<TimeSpan>>();
		private readonly object _lockObject = new object();
		private bool _disposed = false;

		public PerformanceMonitor(ILogger logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// 开始监控操作
		/// </summary>
		public IDisposable StartOperation(string operationName)
		{
			if (string.IsNullOrWhiteSpace(operationName))
				throw new ArgumentException("操作名称不能为空", nameof(operationName));

			lock (_lockObject)
			{
				if (_activeOperations.ContainsKey(operationName))
				{
					_logger.LogWarning($"操作 '{operationName}' 已经在运行中");
					return new OperationScope(this, operationName, false);
				}

				var stopwatch = Stopwatch.StartNew();
				_activeOperations[operationName] = stopwatch;
				_logger.LogDebug($"开始监控操作: {operationName}");
				return new OperationScope(this, operationName, true);
			}
		}

		/// <summary>
		/// 停止监控操作
		/// </summary>
		internal void StopOperation(string operationName)
		{
			lock (_lockObject)
			{
				if (!_activeOperations.TryGetValue(operationName, out var stopwatch))
				{
					_logger.LogWarning($"操作 '{operationName}' 未找到");
					return;
				}

				stopwatch.Stop();
				TimeSpan elapsed = stopwatch.Elapsed;

				_activeOperations.Remove(operationName);

				if (!_operationHistory.ContainsKey(operationName))
				{
					_operationHistory[operationName] = new List<TimeSpan>();
				}

				_operationHistory[operationName].Add(elapsed);

				_logger.LogInfo($"操作 '{operationName}' 完成，耗时: {elapsed.TotalSeconds:F2} 秒");
			}
		}

		/// <summary>
		/// 获取操作的平均耗时
		/// </summary>
		public TimeSpan GetAverageTime(string operationName)
		{
			lock (_lockObject)
			{
				if (!_operationHistory.ContainsKey(operationName) || _operationHistory[operationName].Count == 0)
				{
					return TimeSpan.Zero;
				}

				double averageTicks = _operationHistory[operationName].Average(t => t.Ticks);
				return TimeSpan.FromTicks((long)averageTicks);
			}
		}

		/// <summary>
		/// 获取操作的总次数
		/// </summary>
		public int GetOperationCount(string operationName)
		{
			lock (_lockObject)
			{
				return _operationHistory.ContainsKey(operationName) ? _operationHistory[operationName].Count : 0;
			}
		}

		/// <summary>
		/// 获取所有操作统计
		/// </summary>
		public Dictionary<string, OperationStats> GetAllStats()
		{
			lock (_lockObject)
			{
				var stats = new Dictionary<string, OperationStats>();
				foreach (var kvp in _operationHistory)
				{
					var times = kvp.Value;
					stats[kvp.Key] = new OperationStats
					{
						Count = times.Count,
						AverageTime = TimeSpan.FromTicks((long)times.Average(t => t.Ticks)),
						MinTime = times.Min(),
						MaxTime = times.Max(),
						TotalTime = TimeSpan.FromTicks(times.Sum(t => t.Ticks))
					};
				}
				return stats;
			}
		}

		/// <summary>
		/// 清除统计信息
		/// </summary>
		public void ClearStats()
		{
			lock (_lockObject)
			{
				_operationHistory.Clear();
				_logger.LogInfo("性能统计信息已清除");
			}
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				lock (_lockObject)
				{
					foreach (var kvp in _activeOperations)
					{
						kvp.Value.Stop();
						_logger.LogWarning($"操作 '{kvp.Key}' 在监控器释放时仍在运行");
					}
					_activeOperations.Clear();
				}
				_disposed = true;
			}
		}

		/// <summary>
		/// 操作作用域 - 自动停止监控
		/// </summary>
		private class OperationScope : IDisposable
		{
			private readonly PerformanceMonitor _monitor;
			private readonly string _operationName;
			private readonly bool _shouldStop;

			public OperationScope(PerformanceMonitor monitor, string operationName, bool shouldStop)
			{
				_monitor = monitor;
				_operationName = operationName;
				_shouldStop = shouldStop;
			}

			public void Dispose()
			{
				if (_shouldStop)
				{
					_monitor.StopOperation(_operationName);
				}
			}
		}
	}

	/// <summary>
	/// 操作统计信息
	/// </summary>
	public class OperationStats
	{
		public int Count { get; set; }
		public TimeSpan AverageTime { get; set; }
		public TimeSpan MinTime { get; set; }
		public TimeSpan MaxTime { get; set; }
		public TimeSpan TotalTime { get; set; }
	}
}

