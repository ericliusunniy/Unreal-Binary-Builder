using System;
using System.Reflection;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 反射辅助类 - 提供反射操作的辅助方法
	/// </summary>
	public static class ReflectionHelper
	{
		/// <summary>
		/// 安全地获取属性值
		/// </summary>
		public static T GetPropertyValue<T>(object obj, string propertyName, T defaultValue = default(T))
		{
			if (obj == null || string.IsNullOrWhiteSpace(propertyName))
				return defaultValue;

			try
			{
				PropertyInfo property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
				if (property != null && property.CanRead)
				{
					object value = property.GetValue(obj);
					if (value is T typedValue)
					{
						return typedValue;
					}
				}
			}
			catch
			{
				// 忽略异常，返回默认值
			}

			return defaultValue;
		}

		/// <summary>
		/// 安全地设置属性值
		/// </summary>
		public static bool SetPropertyValue(object obj, string propertyName, object value)
		{
			if (obj == null || string.IsNullOrWhiteSpace(propertyName))
				return false;

			try
			{
				PropertyInfo property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
				if (property != null && property.CanWrite)
				{
					property.SetValue(obj, value);
					return true;
				}
			}
			catch
			{
				// 忽略异常
			}

			return false;
		}

		/// <summary>
		/// 安全地调用方法
		/// </summary>
		public static T InvokeMethod<T>(object obj, string methodName, params object[] parameters)
		{
			if (obj == null || string.IsNullOrWhiteSpace(methodName))
				return default(T);

			try
			{
				MethodInfo method = obj.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
				if (method != null)
				{
					object result = method.Invoke(obj, parameters);
					if (result is T typedResult)
					{
						return typedResult;
					}
				}
			}
			catch
			{
				// 忽略异常
			}

			return default(T);
		}

		/// <summary>
		/// 检查类型是否实现接口
		/// </summary>
		public static bool ImplementsInterface<T>(Type type) where T : class
		{
			if (type == null)
				return false;

			return typeof(T).IsAssignableFrom(type);
		}

		/// <summary>
		/// 获取程序集版本
		/// </summary>
		public static string GetAssemblyVersion(Assembly assembly = null)
		{
			assembly = assembly ?? Assembly.GetCallingAssembly();
			return assembly?.GetName()?.Version?.ToString() ?? "Unknown";
		}
	}
}

