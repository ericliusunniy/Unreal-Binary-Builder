using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 异步辅助类 - 提供异步操作的辅助方法
	/// </summary>
	public static class AsyncHelper
	{
		/// <summary>
		/// 在后台线程上执行操作，然后在 UI 线程上更新
		/// </summary>
		public static async Task<T> ExecuteAsync<T>(Func<T> backgroundWork, Action<T> uiUpdate, Dispatcher dispatcher = null)
		{
			dispatcher = dispatcher ?? Application.Current?.Dispatcher;
			
			// 在后台线程执行
			T result = await Task.Run(backgroundWork);

			// 在 UI 线程更新
			if (dispatcher != null && !dispatcher.CheckAccess())
			{
				await dispatcher.InvokeAsync(() => uiUpdate?.Invoke(result));
			}
			else
			{
				uiUpdate?.Invoke(result);
			}

			return result;
		}

		/// <summary>
		/// 在后台线程上执行操作
		/// </summary>
		public static async Task ExecuteAsync(Action backgroundWork)
		{
			await Task.Run(backgroundWork);
		}

		/// <summary>
		/// 安全地执行异步操作，捕获异常
		/// </summary>
		public static async Task SafeExecuteAsync(Func<Task> asyncAction, Action<Exception> onError = null)
		{
			try
			{
				await asyncAction();
			}
			catch (Exception ex)
			{
				onError?.Invoke(ex);
			}
		}

		/// <summary>
		/// 安全地执行异步操作并返回结果
		/// </summary>
		public static async Task<T> SafeExecuteAsync<T>(Func<Task<T>> asyncAction, T defaultValue = default(T), Action<Exception> onError = null)
		{
			try
			{
				return await asyncAction();
			}
			catch (Exception ex)
			{
				onError?.Invoke(ex);
				return defaultValue;
			}
		}

		/// <summary>
		/// 带超时的异步操作
		/// </summary>
		public static async Task<T> ExecuteWithTimeoutAsync<T>(Func<Task<T>> asyncAction, TimeSpan timeout, T defaultValue = default(T))
		{
			var timeoutTask = Task.Delay(timeout);
			var actionTask = asyncAction();

			var completedTask = await Task.WhenAny(actionTask, timeoutTask);
			if (completedTask == timeoutTask)
			{
				throw new TimeoutException($"操作超时（{timeout.TotalSeconds} 秒）");
			}

			return await actionTask;
		}
	}
}

