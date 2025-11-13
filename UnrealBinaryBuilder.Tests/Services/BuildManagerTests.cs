using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Tests.Services
{
	/// <summary>
	/// BuildManager 的单元测试
	/// </summary>
	public class BuildManagerTests : IDisposable
	{
		private readonly Mock<IProcessManager> _mockProcessManager;
		private readonly Mock<ILogger> _mockLogger;
		private readonly BuildManager _buildManager;

		public BuildManagerTests()
		{
			_mockProcessManager = new Mock<IProcessManager>();
			_mockLogger = new Mock<ILogger>();
			_buildManager = new BuildManager(_mockProcessManager.Object, _mockLogger.Object);
		}

		[Fact]
		public void IsBuilding_Initially_ShouldBeFalse()
		{
			// Assert
			Assert.False(_buildManager.IsBuilding);
		}

		[Fact]
		public void LastBuildSuccess_Initially_ShouldBeFalse()
		{
			// Assert
			Assert.False(_buildManager.LastBuildSuccess);
		}

		[Fact]
		public async Task BuildEngineAsync_WithEmptyPath_ShouldReturnFalse()
		{
			// Act
			bool result = await _buildManager.BuildEngineAsync(string.Empty, "args");

			// Assert
			Assert.False(result);
			_mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("AutomationExePath"))), Times.Once);
		}

		[Fact]
		public async Task BuildEngineAsync_WhenAlreadyBuilding_ShouldReturnFalse()
		{
			// Arrange
			_mockProcessManager.Setup(x => x.IsProcessRunning).Returns(true);

			// Act
			bool result = await _buildManager.BuildEngineAsync("path.exe", "args");

			// Assert
			Assert.False(result);
		}

		[Fact]
		public void StopBuild_WhenNotBuilding_ShouldNotThrow()
		{
			// Act & Assert
			_buildManager.StopBuild();
			// 不应该抛出异常
			Assert.True(true);
		}

		[Fact]
		public void ResetBuildState_ShouldResetAllCounters()
		{
			// Act
			_buildManager.ResetBuildState();

			// Assert
			Assert.False(_buildManager.IsBuilding);
			Assert.False(_buildManager.LastBuildSuccess);
		}

		public void Dispose()
		{
			_buildManager?.StopBuild();
		}
	}
}

