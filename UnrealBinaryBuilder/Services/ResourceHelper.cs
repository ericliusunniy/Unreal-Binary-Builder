using System;
using System.Resources;
using System.Reflection;
using ResMgr = System.Resources.ResourceManager;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 资源辅助类 - 用于访问国际化字符串
	/// </summary>
	public static class ResourceHelper
	{
		private static readonly ResMgr _resourceManager;

		static ResourceHelper()
		{
			_resourceManager = new ResMgr("UnrealBinaryBuilder.Resources.Resources", Assembly.GetExecutingAssembly());
		}

		/// <summary>
		/// 获取资源字符串
		/// </summary>
		public static string GetString(string key, params object[] args)
		{
			try
			{
				string value = _resourceManager.GetString(key, null); // null 使用当前 CultureInfo
				if (string.IsNullOrEmpty(value))
				{
					return key; // 如果找不到资源，返回键名
				}

				if (args != null && args.Length > 0)
				{
					return string.Format(value, args);
				}

				return value;
			}
			catch
			{
				return key; // 发生错误时返回键名
			}
		}
	}
}

