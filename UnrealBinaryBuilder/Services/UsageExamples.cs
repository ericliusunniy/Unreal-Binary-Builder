using System;
using System.Threading.Tasks;
using System.Windows;
using UnrealBinaryBuilder.Services;
using UnrealBinaryBuilder.Services.Extensions;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 使用示例 - 展示如何使用各种服务
	/// </summary>
	public static class UsageExamples
	{
		/// <summary>
		/// 示例 1: 使用服务容器
		/// </summary>
		public static void Example1_ServiceContainer()
		{
			using (var services = new ServiceContainer())
			{
				// 使用日志服务
				services.Logger.LogInfo("这是一条信息日志");
				services.Logger.LogError("这是一条错误日志");

				// 使用性能监控
				using (services.PerformanceMonitor.StartOperation("示例操作"))
				{
					// 执行一些操作
					System.Threading.Thread.Sleep(100);
				}

				// 使用缓存
				services.CacheManager.Set("key1", "value1", TimeSpan.FromMinutes(5));
				string value = services.CacheManager.Get<string>("key1");
			}
		}

		/// <summary>
		/// 示例 2: 使用错误处理器
		/// </summary>
		public static void Example2_ErrorHandler()
		{
			var logger = new Logger();
			var errorHandler = new ErrorHandler(logger);

			// 安全执行操作
			bool success = errorHandler.SafeExecute(() =>
			{
				// 可能抛出异常的操作
				throw new InvalidOperationException("测试异常");
			}, "执行操作", false);

			// 安全执行并返回结果
			int result = errorHandler.SafeExecute(() =>
			{
				return 42;
			}, defaultValue: 0, context: "计算值");
		}

		/// <summary>
		/// 示例 3: 使用性能监控
		/// </summary>
		public static void Example3_PerformanceMonitor()
		{
			var logger = new Logger();
			var monitor = new PerformanceMonitor(logger);

			// 监控操作
			using (monitor.StartOperation("数据库查询"))
			{
				// 执行数据库查询
				System.Threading.Thread.Sleep(200);
			}

			// 获取统计信息
			var stats = monitor.GetAllStats();
			foreach (var stat in stats)
			{
				Console.WriteLine($"{stat.Key}: 平均 {stat.Value.AverageTime.TotalMilliseconds}ms");
			}
		}

		/// <summary>
		/// 示例 4: 使用事件聚合器
		/// </summary>
		public static void Example4_EventAggregator()
		{
			var eventAggregator = new EventAggregator();

			// 订阅事件
			eventAggregator.Subscribe<BuildStartedEvent>(evt =>
			{
				Console.WriteLine($"构建已启动: {evt.BuildName}");
			});

			// 发布事件
			eventAggregator.Publish(new BuildStartedEvent { BuildName = "引擎构建" });
		}

		/// <summary>
		/// 示例 5: 使用 Dispatcher 扩展
		/// </summary>
		public static async Task Example5_DispatcherExtensions()
		{
			var dispatcher = Application.Current?.Dispatcher;
			if (dispatcher == null)
				return;

			// 异步在 UI 线程执行
			await dispatcher.InvokeAsync(() =>
			{
				// 更新 UI
				Console.WriteLine("UI 已更新");
			});

			// 异步执行并返回结果
			string result = await dispatcher.InvokeAsync(() =>
			{
				return "结果";
			});
		}

		/// <summary>
		/// 示例 6: 使用验证辅助
		/// </summary>
		public static void Example6_ValidationHelper()
		{
			// 验证文件
			var result = ValidationHelper.ValidateFileExists("path/to/file.txt");
			if (!result.IsValid)
			{
				Console.WriteLine($"验证失败: {result.ErrorMessage}");
			}

			// 验证并抛出异常
			ValidationHelper.ValidateNotNullOrEmpty("value", "参数名").ThrowIfInvalid();
		}

		/// <summary>
		/// 示例 7: 使用异步辅助
		/// </summary>
		public static async Task Example7_AsyncHelper()
		{
			// 后台执行并更新 UI
			await AsyncHelper.ExecuteAsync(
				backgroundWork: () => "计算结果",
				uiUpdate: result => Console.WriteLine($"结果: {result}")
			);

			// 安全执行异步操作
			await AsyncHelper.SafeExecuteAsync(async () =>
			{
				await Task.Delay(100);
			}, onError: ex => Console.WriteLine($"错误: {ex.Message}"));
		}

		/// <summary>
		/// 示例 8: 使用缓存管理器
		/// </summary>
		public static void Example8_CacheManager()
		{
			var logger = new Logger();
			var cache = new CacheManager(logger);

			// 设置缓存（5分钟过期）
			cache.Set("user_name", "John", TimeSpan.FromMinutes(5));

			// 获取缓存
			string userName = cache.Get<string>("user_name", "Unknown");

			// 检查缓存
			if (cache.Contains("user_name"))
			{
				Console.WriteLine("缓存存在");
			}

			// 清除过期缓存
			cache.ClearExpired();

			// 保存到文件
			cache.SaveToFile();
		}
	}

	/// <summary>
	/// 构建启动事件示例
	/// </summary>
	public class BuildStartedEvent
	{
		public string BuildName { get; set; }
		public DateTime StartTime { get; set; } = DateTime.Now;
	}
}

