using System;
using System.Text.RegularExpressions;
using UnrealBinaryBuilder.UserControls;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 日志解析器 - 从构建输出中提取信息
	/// </summary>
	public class LogParser
	{
		private static readonly Regex StepPattern = new Regex(@"\*{6} \[(\d+)\/(\d+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex WarningPattern = new Regex(@"warning|\*\*\* Unable to determine ", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex DebugPattern = new Regex(@".+\*\s\D\d\D\d\D\s\w+|.+\*\sFor\sUE4", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex ErrorPattern = new Regex(@"Error_Unknown|ERROR|exited with code 1", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex ProcessedFilesPattern = new Regex(@"\w.+\.(cpp|cc|c|h|ispc)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <summary>
		/// 解析日志消息并返回消息类型
		/// </summary>
		public LogParseResult ParseLogMessage(string message, bool isError = false)
		{
			var result = new LogParseResult
			{
				Message = message,
				MessageType = isError ? LogViewer.EMessageType.Error : LogViewer.EMessageType.Info,
				IsStepInfo = false,
				IsProcessedFile = false,
				IsWarning = false,
				IsError = isError,
				IsDebug = false
			};

			if (string.IsNullOrWhiteSpace(message))
			{
				return result;
			}

			if (isError)
			{
				result.IsError = true;
				result.MessageType = LogViewer.EMessageType.Error;
				return result;
			}

			// 检查步骤信息
			var stepMatch = StepPattern.Match(message);
			if (stepMatch.Success)
			{
				result.IsStepInfo = true;
				result.StepCurrent = int.Parse(stepMatch.Groups[1].Value);
				result.StepTotal = int.Parse(stepMatch.Groups[2].Value);
			}

			// 检查处理的文件
			if (ProcessedFilesPattern.IsMatch(message))
			{
				result.IsProcessedFile = true;
			}

			// 检查警告
			if (WarningPattern.IsMatch(message))
			{
				result.IsWarning = true;
				result.MessageType = LogViewer.EMessageType.Warning;
			}
			// 检查错误（排除某些误报）
			else if (ErrorPattern.IsMatch(message) && 
			         !message.Contains("ShadowError") && 
			         !message.Contains("error_details.") && 
			         !message.Contains("error_code."))
			{
				result.IsError = true;
				result.MessageType = LogViewer.EMessageType.Error;
			}
			// 检查调试信息
			else if (DebugPattern.IsMatch(message))
			{
				result.IsDebug = true;
				result.MessageType = LogViewer.EMessageType.Debug;
			}

			return result;
		}
	}

	/// <summary>
	/// 日志解析结果
	/// </summary>
	public class LogParseResult
	{
		public string Message { get; set; }
		public LogViewer.EMessageType MessageType { get; set; }
		public bool IsStepInfo { get; set; }
		public int StepCurrent { get; set; }
		public int StepTotal { get; set; }
		public bool IsProcessedFile { get; set; }
		public bool IsWarning { get; set; }
		public bool IsError { get; set; }
		public bool IsDebug { get; set; }
	}
}

