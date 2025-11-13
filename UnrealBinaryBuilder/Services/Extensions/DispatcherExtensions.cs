using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace UnrealBinaryBuilder.Services.Extensions
{
	/// <summary>
	/// Dispatcher 扩展方法 - 简化 UI 线程调用
	/// </summary>
	public static class DispatcherExtensions
	{
		/// <summary>
		/// 异步在 UI 线程上执行操作
		/// </summary>
		public static Task InvokeAsync(this Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Normal)
		{
			if (dispatcher == null)
				throw new ArgumentNullException(nameof(dispatcher));
			if (action == null)
				throw new ArgumentNullException(nameof(action));

			var tcs = new TaskCompletionSource<object>();
			dispatcher.BeginInvoke(priority, new Action(() =>
			{
				try
				{
					action();
					tcs.SetResult(null);
				}
				catch (Exception ex)
				{
					tcs.SetException(ex);
				}
			}));
			return tcs.Task;
		}

		/// <summary>
		/// 异步在 UI 线程上执行操作并返回结果
		/// </summary>
		public static Task<T> InvokeAsync<T>(this Dispatcher dispatcher, Func<T> func, DispatcherPriority priority = DispatcherPriority.Normal)
		{
			if (dispatcher == null)
				throw new ArgumentNullException(nameof(dispatcher));
			if (func == null)
				throw new ArgumentNullException(nameof(func));

			var tcs = new TaskCompletionSource<T>();
			dispatcher.BeginInvoke(priority, new Action(() =>
			{
				try
				{
					T result = func();
					tcs.SetResult(result);
				}
				catch (Exception ex)
				{
					tcs.SetException(ex);
				}
			}));
			return tcs.Task;
		}

		/// <summary>
		/// 检查是否在 UI 线程上
		/// </summary>
		public static bool IsOnUIThread(this Dispatcher dispatcher)
		{
			return dispatcher?.CheckAccess() ?? false;
		}
	}
}

