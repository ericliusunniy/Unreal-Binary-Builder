using System;
using System.Text;

namespace UnrealBinaryBuilder.Services.Extensions
{
	/// <summary>
	/// 异常扩展方法
	/// </summary>
	public static class ExceptionExtensions
	{
		/// <summary>
		/// 获取异常的完整消息（包括内部异常）
		/// </summary>
		public static string GetFullMessage(this Exception exception)
		{
			if (exception == null)
				return string.Empty;

			var sb = new StringBuilder();
			Exception current = exception;

			while (current != null)
			{
				if (sb.Length > 0)
					sb.Append(" -> ");

				sb.Append(current.Message);
				current = current.InnerException;
			}

			return sb.ToString();
		}

		/// <summary>
		/// 获取异常的完整堆栈跟踪（包括内部异常）
		/// </summary>
		public static string GetFullStackTrace(this Exception exception)
		{
			if (exception == null)
				return string.Empty;

			var sb = new StringBuilder();
			Exception current = exception;

			while (current != null)
			{
				if (sb.Length > 0)
					sb.AppendLine("--- Inner Exception Stack Trace ---");

				sb.AppendLine(current.StackTrace);
				current = current.InnerException;
			}

			return sb.ToString();
		}

		/// <summary>
		/// 获取异常的详细信息（消息 + 堆栈跟踪）
		/// </summary>
		public static string GetDetailedMessage(this Exception exception)
		{
			if (exception == null)
				return string.Empty;

			var sb = new StringBuilder();
			sb.AppendLine($"异常类型: {exception.GetType().Name}");
			sb.AppendLine($"异常消息: {exception.GetFullMessage()}");
			sb.AppendLine();
			sb.AppendLine("堆栈跟踪:");
			sb.AppendLine(exception.GetFullStackTrace());

			return sb.ToString();
		}
	}
}

