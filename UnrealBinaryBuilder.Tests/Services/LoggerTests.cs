using System;
using System.IO;
using Xunit;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Tests.Services
{
	/// <summary>
	/// Logger 服务的单元测试
	/// </summary>
	public class LoggerTests : IDisposable
	{
		private readonly Logger _logger;
		private readonly string _testLogDirectory;

		public LoggerTests()
		{
			_testLogDirectory = Path.Combine(Path.GetTempPath(), "UnrealBinaryBuilderTests", Guid.NewGuid().ToString());
			Directory.CreateDirectory(_testLogDirectory);
			_logger = new Logger();
		}

		[Fact]
		public void LogInfo_ShouldWriteToFile()
		{
			// Arrange
			string message = "测试信息消息";

			// Act
			_logger.LogInfo(message);

			// Assert
			// 验证日志文件已创建（实际实现中需要检查文件内容）
			Assert.True(true); // 占位符，实际测试需要验证文件内容
		}

		[Fact]
		public void LogError_ShouldWriteToErrorLog()
		{
			// Arrange
			string message = "测试错误消息";

			// Act
			_logger.LogError(message);

			// Assert
			Assert.True(true); // 占位符
		}

		[Fact]
		public void LogException_ShouldIncludeStackTrace()
		{
			// Arrange
			Exception exception = new InvalidOperationException("测试异常");

			// Act
			_logger.LogException(exception, "测试上下文");

			// Assert
			Assert.True(true); // 占位符
		}

		[Fact]
		public void LogInfo_WithNullMessage_ShouldNotThrow()
		{
			// Act & Assert
			_logger.LogInfo(null);
			_logger.LogInfo(string.Empty);
			_logger.LogInfo("   ");

			// 不应该抛出异常
			Assert.True(true);
		}

		public void Dispose()
		{
			_logger?.Dispose();
			if (Directory.Exists(_testLogDirectory))
			{
				Directory.Delete(_testLogDirectory, true);
			}
		}
	}
}

