using System;
using System.Text.RegularExpressions;

namespace UnrealBinaryBuilder.Services.Extensions
{
	/// <summary>
	/// 字符串扩展方法
	/// </summary>
	public static class StringExtensions
	{
		/// <summary>
		/// 检查字符串是否为 null 或空白
		/// </summary>
		public static bool IsNullOrWhiteSpace(this string value)
		{
			return string.IsNullOrWhiteSpace(value);
		}

		/// <summary>
		/// 安全地格式化字符串
		/// </summary>
		public static string SafeFormat(this string format, params object[] args)
		{
			if (string.IsNullOrEmpty(format))
				return string.Empty;

			try
			{
				return args != null && args.Length > 0 ? string.Format(format, args) : format;
			}
			catch
			{
				return format;
			}
		}

		/// <summary>
		/// 检查字符串是否匹配正则表达式
		/// </summary>
		public static bool Matches(this string value, string pattern, RegexOptions options = RegexOptions.None)
		{
			if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(pattern))
				return false;

			try
			{
				return Regex.IsMatch(value, pattern, options);
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// 安全地截取字符串
		/// </summary>
		public static string SafeSubstring(this string value, int startIndex, int length = int.MaxValue)
		{
			if (string.IsNullOrEmpty(value))
				return string.Empty;

			if (startIndex < 0)
				startIndex = 0;

			if (startIndex >= value.Length)
				return string.Empty;

			int actualLength = Math.Min(length, value.Length - startIndex);
			return value.Substring(startIndex, actualLength);
		}
	}
}

