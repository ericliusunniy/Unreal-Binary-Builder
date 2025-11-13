using System;
using System.Threading;
using System.Threading.Tasks;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 重试辅助类 - 提供操作重试机制
	/// </summary>
	public static class RetryHelper
	{
		/// <summary>
		/// 执行操作，失败时重试
		/// </summary>
		public static bool ExecuteWithRetry(Action action, int maxRetries = 3, TimeSpan? delay = null, ILogger logger = null)
		{
			if (action == null)
				throw new ArgumentNullException(nameof(action));

			delay = delay ?? TimeSpan.FromSeconds(1);
			int attempt = 0;

			while (attempt < maxRetries)
			{
				try
				{
					action();
					return true;
				}
				catch (Exception ex)
				{
					attempt++;
					logger?.LogWarning($"操作失败 (尝试 {attempt}/{maxRetries}): {ex.Message}");

					if (attempt >= maxRetries)
					{
						logger?.LogError($"操作在 {maxRetries} 次尝试后仍然失败");
						return false;
					}

					Thread.Sleep(delay.Value);
				}
			}

			return false;
		}

		/// <summary>
		/// 执行操作并返回结果，失败时重试
		/// </summary>
		public static T ExecuteWithRetry<T>(Func<T> func, int maxRetries = 3, TimeSpan? delay = null, T defaultValue = default(T), ILogger logger = null)
		{
			if (func == null)
				throw new ArgumentNullException(nameof(func));

			delay = delay ?? TimeSpan.FromSeconds(1);
			int attempt = 0;

			while (attempt < maxRetries)
			{
				try
				{
					return func();
				}
				catch (Exception ex)
				{
					attempt++;
					logger?.LogWarning($"操作失败 (尝试 {attempt}/{maxRetries}): {ex.Message}");

					if (attempt >= maxRetries)
					{
						logger?.LogError($"操作在 {maxRetries} 次尝试后仍然失败");
						return defaultValue;
					}

					Thread.Sleep(delay.Value);
				}
			}

			return defaultValue;
		}

		/// <summary>
		/// 异步执行操作，失败时重试
		/// </summary>
		public static async Task<bool> ExecuteWithRetryAsync(Func<Task> asyncAction, int maxRetries = 3, TimeSpan? delay = null, ILogger logger = null)
		{
			if (asyncAction == null)
				throw new ArgumentNullException(nameof(asyncAction));

			delay = delay ?? TimeSpan.FromSeconds(1);
			int attempt = 0;

			while (attempt < maxRetries)
			{
				try
				{
					await asyncAction();
					return true;
				}
				catch (Exception ex)
				{
					attempt++;
					logger?.LogWarning($"操作失败 (尝试 {attempt}/{maxRetries}): {ex.Message}");

					if (attempt >= maxRetries)
					{
						logger?.LogError($"操作在 {maxRetries} 次尝试后仍然失败");
						return false;
					}

					await Task.Delay(delay.Value);
				}
			}

			return false;
		}

		/// <summary>
		/// 异步执行操作并返回结果，失败时重试
		/// </summary>
		public static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> asyncFunc, int maxRetries = 3, TimeSpan? delay = null, T defaultValue = default(T), ILogger logger = null)
		{
			if (asyncFunc == null)
				throw new ArgumentNullException(nameof(asyncFunc));

			delay = delay ?? TimeSpan.FromSeconds(1);
			int attempt = 0;

			while (attempt < maxRetries)
			{
				try
				{
					return await asyncFunc();
				}
				catch (Exception ex)
				{
					attempt++;
					logger?.LogWarning($"操作失败 (尝试 {attempt}/{maxRetries}): {ex.Message}");

					if (attempt >= maxRetries)
					{
						logger?.LogError($"操作在 {maxRetries} 次尝试后仍然失败");
						return defaultValue;
					}

					await Task.Delay(delay.Value);
				}
			}

			return defaultValue;
		}
	}
}

