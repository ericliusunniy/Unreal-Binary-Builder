using System;
using System.Diagnostics;
using Xunit;
using Moq;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Tests.Services
{
	/// <summary>
	/// ProcessManager 的单元测试
	/// </summary>
	public class ProcessManagerTests : IDisposable
	{
		private readonly Mock<ILogger> _mockLogger;
		private readonly ProcessManager _processManager;

		public ProcessManagerTests()
		{
			_mockLogger = new Mock<ILogger>();
			_processManager = new ProcessManager(_mockLogger.Object);
		}

		[Fact]
		public void StartProcess_WithNullStartInfo_ShouldReturnFalse()
		{
			// Act
			bool result = _processManager.StartProcess(null);

			// Assert
			Assert.False(result);
			_mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("ProcessStartInfo"))), Times.Once);
		}

		[Fact]
		public void StartProcess_WithNonExistentFile_ShouldReturnFalse()
		{
			// Arrange
			var startInfo = new ProcessStartInfo
			{
				FileName = "NonExistentFile.exe",
				UseShellExecute = false
			};

			// Act
			bool result = _processManager.StartProcess(startInfo);

			// Assert
			Assert.False(result);
		}

		[Fact]
		public void IsProcessRunning_WhenNoProcess_ShouldReturnFalse()
		{
			// Act
			bool result = _processManager.IsProcessRunning;

			// Assert
			Assert.False(result);
		}

		[Fact]
		public void GetCurrentProcessName_WhenNoProcess_ShouldReturnDefault()
		{
			// Act
			string name = _processManager.GetCurrentProcessName();

			// Assert
			Assert.Equal("无", name);
		}

		[Fact]
		public void CloseProcess_WhenNoProcess_ShouldNotThrow()
		{
			// Act & Assert
			_processManager.CloseProcess();
			_processManager.CloseProcess(true);
			// 不应该抛出异常
			Assert.True(true);
		}

		public void Dispose()
		{
			_processManager?.Dispose();
		}
	}
}

