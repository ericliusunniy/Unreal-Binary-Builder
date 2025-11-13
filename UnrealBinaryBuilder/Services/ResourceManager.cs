using System;
using System.Collections.Generic;
using System.Linq;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 资源管理器 - 确保所有 IDisposable 资源正确释放
	/// </summary>
	public class ResourceManager : IDisposable
	{
		private readonly List<IDisposable> _resources = new List<IDisposable>();
		private readonly object _lockObject = new object();
		private bool _disposed = false;
		private readonly ILogger _logger;

		public ResourceManager(ILogger logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// 注册资源
		/// </summary>
		public void RegisterResource(IDisposable resource)
		{
			if (resource == null)
				return;

			lock (_lockObject)
			{
				if (!_disposed)
				{
					_resources.Add(resource);
				}
				else
				{
					resource.Dispose();
				}
			}
		}

		/// <summary>
		/// 注销资源
		/// </summary>
		public void UnregisterResource(IDisposable resource)
		{
			if (resource == null)
				return;

			lock (_lockObject)
			{
				_resources.Remove(resource);
			}
		}

		/// <summary>
		/// 释放所有资源
		/// </summary>
		public void Dispose()
		{
			if (_disposed)
				return;

			lock (_lockObject)
			{
				if (_disposed)
					return;

				_disposed = true;

				// 反向释放，确保依赖关系正确处理
				var reversedResources = _resources.ToList().AsEnumerable().Reverse();
				foreach (var resource in reversedResources)
				{
					try
					{
						resource?.Dispose();
					}
					catch (Exception ex)
					{
						_logger?.LogException(ex, $"释放资源时发生错误: {resource.GetType().Name}");
					}
				}

				_resources.Clear();
			}
		}
	}
}

