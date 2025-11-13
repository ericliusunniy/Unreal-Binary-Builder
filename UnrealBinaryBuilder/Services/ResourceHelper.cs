using System;
using System.Resources;
using System.Reflection;
using ResMgr = System.Resources.ResourceManager;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// Resource helper class - Used to access internationalized strings
	/// </summary>
	public static class ResourceHelper
	{
		private static readonly ResMgr _resourceManager;

		static ResourceHelper()
		{
			_resourceManager = new ResMgr("UnrealBinaryBuilder.Resources.Resources", Assembly.GetExecutingAssembly());
		}

		/// <summary>
		/// Get resource string
		/// </summary>
		public static string GetString(string key, params object[] args)
		{
			try
			{
				string value = _resourceManager.GetString(key, null); // null uses current CultureInfo
				if (string.IsNullOrEmpty(value))
				{
					return key; // If resource not found, return key name
				}

				if (args != null && args.Length > 0)
				{
					return string.Format(value, args);
				}

				return value;
			}
			catch
			{
				return key; // Return key name on error
			}
		}
	}
}

