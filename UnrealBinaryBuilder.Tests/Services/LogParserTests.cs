using Xunit;
using UnrealBinaryBuilder.Services;
using UnrealBinaryBuilder.UserControls;

namespace UnrealBinaryBuilder.Tests.Services
{
	/// <summary>
	/// LogParser 的单元测试
	/// </summary>
	public class LogParserTests
	{
		private readonly LogParser _parser;

		public LogParserTests()
		{
			_parser = new LogParser();
		}

		[Fact]
		public void ParseLogMessage_WithStepInfo_ShouldDetectStep()
		{
			// Arrange
			string message = "****** [5/10] Building...";

			// Act
			var result = _parser.ParseLogMessage(message);

			// Assert
			Assert.True(result.IsStepInfo);
			Assert.Equal(5, result.StepCurrent);
			Assert.Equal(10, result.StepTotal);
		}

		[Fact]
		public void ParseLogMessage_WithWarning_ShouldDetectWarning()
		{
			// Arrange
			string message = "warning: This is a warning message";

			// Act
			var result = _parser.ParseLogMessage(message);

			// Assert
			Assert.True(result.IsWarning);
			Assert.Equal(LogViewer.EMessageType.Warning, result.MessageType);
		}

		[Fact]
		public void ParseLogMessage_WithError_ShouldDetectError()
		{
			// Arrange
			string message = "ERROR: This is an error message";

			// Act
			var result = _parser.ParseLogMessage(message);

			// Assert
			Assert.True(result.IsError);
			Assert.Equal(LogViewer.EMessageType.Error, result.MessageType);
		}

		[Fact]
		public void ParseLogMessage_WithProcessedFile_ShouldDetectFile()
		{
			// Arrange
			string message = "Compiling MyFile.cpp";

			// Act
			var result = _parser.ParseLogMessage(message);

			// Assert
			Assert.True(result.IsProcessedFile);
		}

		[Fact]
		public void ParseLogMessage_WithNullMessage_ShouldNotThrow()
		{
			// Act & Assert
			var result = _parser.ParseLogMessage(null);
			Assert.NotNull(result);
		}

		[Fact]
		public void ParseLogMessage_WithExplicitError_ShouldMarkAsError()
		{
			// Arrange
			string message = "Some message";

			// Act
			var result = _parser.ParseLogMessage(message, isError: true);

			// Assert
			Assert.True(result.IsError);
			Assert.Equal(LogViewer.EMessageType.Error, result.MessageType);
		}
	}
}

