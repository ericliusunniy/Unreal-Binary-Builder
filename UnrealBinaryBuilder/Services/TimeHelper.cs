using System;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 时间辅助类 - 提供时间相关的辅助方法
	/// </summary>
	public static class TimeHelper
	{
		/// <summary>
		/// 格式化时间跨度
		/// </summary>
		public static string FormatTimeSpan(TimeSpan timeSpan)
		{
			if (timeSpan.TotalDays >= 1)
			{
				return $"{(int)timeSpan.TotalDays}天 {timeSpan.Hours}小时 {timeSpan.Minutes}分钟";
			}
			else if (timeSpan.TotalHours >= 1)
			{
				return $"{timeSpan.Hours}小时 {timeSpan.Minutes}分钟 {timeSpan.Seconds}秒";
			}
			else if (timeSpan.TotalMinutes >= 1)
			{
				return $"{timeSpan.Minutes}分钟 {timeSpan.Seconds}秒";
			}
			else
			{
				return $"{timeSpan.Seconds}秒";
			}
		}

		/// <summary>
		/// 格式化时间跨度（简短格式）
		/// </summary>
		public static string FormatTimeSpanShort(TimeSpan timeSpan)
		{
			return timeSpan.ToString(@"hh\:mm\:ss");
		}

		/// <summary>
		/// 格式化时间跨度（详细格式）
		/// </summary>
		public static string FormatTimeSpanDetailed(TimeSpan timeSpan)
		{
			return $"{timeSpan.Days}天 {timeSpan.Hours}小时 {timeSpan.Minutes}分钟 {timeSpan.Seconds}秒 {timeSpan.Milliseconds}毫秒";
		}

		/// <summary>
		/// 获取相对时间描述（如"5分钟前"）
		/// </summary>
		public static string GetRelativeTimeDescription(DateTime dateTime)
		{
			TimeSpan timeSpan = DateTime.Now - dateTime;

			if (timeSpan.TotalSeconds < 60)
			{
				return "刚刚";
			}
			else if (timeSpan.TotalMinutes < 60)
			{
				return $"{(int)timeSpan.TotalMinutes}分钟前";
			}
			else if (timeSpan.TotalHours < 24)
			{
				return $"{(int)timeSpan.TotalHours}小时前";
			}
			else if (timeSpan.TotalDays < 30)
			{
				return $"{(int)timeSpan.TotalDays}天前";
			}
			else if (timeSpan.TotalDays < 365)
			{
				return $"{(int)(timeSpan.TotalDays / 30)}个月前";
			}
			else
			{
				return $"{(int)(timeSpan.TotalDays / 365)}年前";
			}
		}

		/// <summary>
		/// 格式化文件时间
		/// </summary>
		public static string FormatFileTime(DateTime dateTime)
		{
			return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
		}

		/// <summary>
		/// 格式化日期
		/// </summary>
		public static string FormatDate(DateTime dateTime)
		{
			return dateTime.ToString("yyyy-MM-dd");
		}

		/// <summary>
		/// 格式化时间
		/// </summary>
		public static string FormatTime(DateTime dateTime)
		{
			return dateTime.ToString("HH:mm:ss");
		}
	}
}

