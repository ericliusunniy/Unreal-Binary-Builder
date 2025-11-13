using System;
using System.Collections.Generic;
using System.Linq;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 事件聚合器 - 实现发布/订阅模式
	/// </summary>
	public class EventAggregator : IDisposable
	{
		private readonly Dictionary<Type, List<object>> _subscribers = new Dictionary<Type, List<object>>();
		private readonly object _lockObject = new object();
		private bool _disposed = false;

		/// <summary>
		/// 订阅事件
		/// </summary>
		public void Subscribe<T>(Action<T> handler) where T : class
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			lock (_lockObject)
			{
				Type eventType = typeof(T);
				if (!_subscribers.ContainsKey(eventType))
				{
					_subscribers[eventType] = new List<object>();
				}

				_subscribers[eventType].Add(handler);
			}
		}

		/// <summary>
		/// 取消订阅事件
		/// </summary>
		public void Unsubscribe<T>(Action<T> handler) where T : class
		{
			if (handler == null)
				return;

			lock (_lockObject)
			{
				Type eventType = typeof(T);
				if (_subscribers.ContainsKey(eventType))
				{
					_subscribers[eventType].Remove(handler);
					if (_subscribers[eventType].Count == 0)
					{
						_subscribers.Remove(eventType);
					}
				}
			}
		}

		/// <summary>
		/// 发布事件
		/// </summary>
		public void Publish<T>(T eventData) where T : class
		{
			if (eventData == null)
				return;

			List<Action<T>> handlers;

			lock (_lockObject)
			{
				Type eventType = typeof(T);
				if (!_subscribers.ContainsKey(eventType))
				{
					return;
				}

				// 创建副本以避免锁定期间执行
				handlers = _subscribers[eventType]
					.Cast<Action<T>>()
					.ToList();
			}

			// 在锁外执行处理器
			foreach (var handler in handlers)
			{
				try
				{
					handler(eventData);
				}
				catch (Exception ex)
				{
					// 记录异常但不中断其他处理器
					System.Diagnostics.Debug.WriteLine($"事件处理器异常: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// 清除所有订阅
		/// </summary>
		public void Clear()
		{
			lock (_lockObject)
			{
				_subscribers.Clear();
			}
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				Clear();
				_disposed = true;
			}
		}
	}
}

