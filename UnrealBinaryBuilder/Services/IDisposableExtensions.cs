using System;

namespace UnrealBinaryBuilder.Services.Extensions
{
	/// <summary>
	/// IDisposable 扩展方法
	/// </summary>
	public static class IDisposableExtensions
	{
		/// <summary>
		/// 安全地释放资源
		/// </summary>
		public static void SafeDispose(this IDisposable disposable)
		{
			if (disposable != null)
			{
				try
				{
					disposable.Dispose();
				}
				catch
				{
					// 忽略释放时的异常
				}
			}
		}

		/// <summary>
		/// 使用后自动释放
		/// </summary>
		public static T Using<T>(this T disposable, Action<T> action) where T : IDisposable
		{
			try
			{
				action(disposable);
			}
			finally
			{
				disposable?.Dispose();
			}
			return disposable;
		}
	}
}

