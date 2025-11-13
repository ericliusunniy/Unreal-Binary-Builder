using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 缓存管理器 - 管理应用程序缓存
	/// </summary>
	public class CacheManager : IDisposable
	{
		private readonly ILogger _logger;
		private readonly string _cacheDirectory;
		private readonly Dictionary<string, CacheEntry> _memoryCache = new Dictionary<string, CacheEntry>();
		private readonly object _lockObject = new object();
		private bool _disposed = false;

		public CacheManager(ILogger logger, string cacheDirectory = null)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_cacheDirectory = cacheDirectory ?? Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"UnrealBinaryBuilder",
				"Cache");

			EnsureCacheDirectoryExists();
		}

		/// <summary>
		/// 设置缓存项
		/// </summary>
		public void Set<T>(string key, T value, TimeSpan? expiration = null)
		{
			if (string.IsNullOrWhiteSpace(key))
				throw new ArgumentException("缓存键不能为空", nameof(key));

			lock (_lockObject)
			{
				var entry = new CacheEntry
				{
					Key = key,
					Value = value,
					ExpirationTime = expiration.HasValue ? DateTime.Now.Add(expiration.Value) : DateTime.MaxValue,
					CreatedTime = DateTime.Now
				};

				_memoryCache[key] = entry;
				_logger.LogDebug($"缓存已设置: {key}");
			}
		}

		/// <summary>
		/// 获取缓存项
		/// </summary>
		public T Get<T>(string key, T defaultValue = default(T))
		{
			if (string.IsNullOrWhiteSpace(key))
				return defaultValue;

			lock (_lockObject)
			{
				if (!_memoryCache.TryGetValue(key, out var entry))
				{
					return defaultValue;
				}

				// 检查是否过期
				if (DateTime.Now > entry.ExpirationTime)
				{
					_memoryCache.Remove(key);
					_logger.LogDebug($"缓存已过期: {key}");
					return defaultValue;
				}

				if (entry.Value is T typedValue)
				{
					return typedValue;
				}

				return defaultValue;
			}
		}

		/// <summary>
		/// 检查缓存是否存在
		/// </summary>
		public bool Contains(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
				return false;

			lock (_lockObject)
			{
				if (!_memoryCache.TryGetValue(key, out var entry))
				{
					return false;
				}

				// 检查是否过期
				if (DateTime.Now > entry.ExpirationTime)
				{
					_memoryCache.Remove(key);
					return false;
				}

				return true;
			}
		}

		/// <summary>
		/// 移除缓存项
		/// </summary>
		public void Remove(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
				return;

			lock (_lockObject)
			{
				if (_memoryCache.Remove(key))
				{
					_logger.LogDebug($"缓存已移除: {key}");
				}
			}
		}

		/// <summary>
		/// 清除所有缓存
		/// </summary>
		public void Clear()
		{
			lock (_lockObject)
			{
				_memoryCache.Clear();
				_logger.LogInfo("所有缓存已清除");
			}
		}

		/// <summary>
		/// 清除过期缓存
		/// </summary>
		public void ClearExpired()
		{
			lock (_lockObject)
			{
				var expiredKeys = _memoryCache
					.Where(kvp => DateTime.Now > kvp.Value.ExpirationTime)
					.Select(kvp => kvp.Key)
					.ToList();

				foreach (var key in expiredKeys)
				{
					_memoryCache.Remove(key);
				}

				if (expiredKeys.Count > 0)
				{
					_logger.LogInfo($"已清除 {expiredKeys.Count} 个过期缓存项");
				}
			}
		}

		/// <summary>
		/// 保存缓存到文件
		/// </summary>
		public void SaveToFile(string fileName = "cache.json")
		{
			try
			{
				lock (_lockObject)
				{
					ClearExpired();
					string filePath = Path.Combine(_cacheDirectory, fileName);
					string json = JsonConvert.SerializeObject(_memoryCache, Formatting.Indented);
					File.WriteAllText(filePath, json);
					_logger.LogInfo($"缓存已保存到: {filePath}");
				}
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "保存缓存到文件时发生错误");
			}
		}

		/// <summary>
		/// 从文件加载缓存
		/// </summary>
		public void LoadFromFile(string fileName = "cache.json")
		{
			try
			{
				string filePath = Path.Combine(_cacheDirectory, fileName);
				if (!File.Exists(filePath))
				{
					_logger.LogDebug($"缓存文件不存在: {filePath}");
					return;
				}

				lock (_lockObject)
				{
					string json = File.ReadAllText(filePath);
					var loadedCache = JsonConvert.DeserializeObject<Dictionary<string, CacheEntry>>(json);
					if (loadedCache != null)
					{
						_memoryCache.Clear();
						foreach (var kvp in loadedCache)
						{
							// 只加载未过期的项
							if (DateTime.Now <= kvp.Value.ExpirationTime)
							{
								_memoryCache[kvp.Key] = kvp.Value;
							}
						}
						_logger.LogInfo($"从文件加载了 {_memoryCache.Count} 个缓存项");
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "从文件加载缓存时发生错误");
			}
		}

		private void EnsureCacheDirectoryExists()
		{
			try
			{
				if (!Directory.Exists(_cacheDirectory))
				{
					Directory.CreateDirectory(_cacheDirectory);
					_logger.LogDebug($"缓存目录已创建: {_cacheDirectory}");
				}
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "创建缓存目录时发生错误");
			}
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				SaveToFile();
				Clear();
				_disposed = true;
			}
		}

		private class CacheEntry
		{
			public string Key { get; set; }
			public object Value { get; set; }
			public DateTime ExpirationTime { get; set; }
			public DateTime CreatedTime { get; set; }
		}
	}
}

