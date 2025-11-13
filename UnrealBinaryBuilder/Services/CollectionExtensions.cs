using System;
using System.Collections.Generic;
using System.Linq;

namespace UnrealBinaryBuilder.Services.Extensions
{
	/// <summary>
	/// 集合扩展方法
	/// </summary>
	public static class CollectionExtensions
	{
		/// <summary>
		/// 安全地获取集合中的元素，如果索引超出范围返回默认值
		/// </summary>
		public static T SafeGet<T>(this IList<T> list, int index, T defaultValue = default(T))
		{
			if (list == null || index < 0 || index >= list.Count)
				return defaultValue;

			return list[index];
		}

		/// <summary>
		/// 检查集合是否为 null 或空
		/// </summary>
		public static bool IsNullOrEmpty<T>(this IEnumerable<T> collection)
		{
			return collection == null || !collection.Any();
		}

		/// <summary>
		/// 如果集合为 null，返回空集合
		/// </summary>
		public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> collection)
		{
			return collection ?? Enumerable.Empty<T>();
		}

		/// <summary>
		/// 批量处理集合元素
		/// </summary>
		public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
		{
			if (collection == null || action == null)
				return;

			foreach (var item in collection)
			{
				action(item);
			}
		}

		/// <summary>
		/// 批量处理集合元素（带索引）
		/// </summary>
		public static void ForEach<T>(this IEnumerable<T> collection, Action<T, int> action)
		{
			if (collection == null || action == null)
				return;

			int index = 0;
			foreach (var item in collection)
			{
				action(item, index++);
			}
		}

		/// <summary>
		/// 将集合转换为只读列表
		/// </summary>
		public static IReadOnlyList<T> ToReadOnlyList<T>(this IEnumerable<T> collection)
		{
			if (collection == null)
				return Array.Empty<T>();

			return collection.ToList().AsReadOnly();
		}

		/// <summary>
		/// 安全地获取字典值
		/// </summary>
		public static TValue SafeGet<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
		{
			if (dictionary == null || key == null)
				return defaultValue;

			return dictionary.TryGetValue(key, out TValue value) ? value : defaultValue;
		}
	}
}

