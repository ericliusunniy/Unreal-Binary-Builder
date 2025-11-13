using System;
using System.IO;
using Xunit;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Tests.Services
{
	/// <summary>
	/// ValidationHelper 的单元测试
	/// </summary>
	public class ValidationHelperTests
	{
		[Fact]
		public void ValidateFileExists_WithExistingFile_ShouldReturnSuccess()
		{
			// Arrange
			string tempFile = Path.GetTempFileName();

			try
			{
				// Act
				var result = ValidationHelper.ValidateFileExists(tempFile);

				// Assert
				Assert.True(result.IsValid);
				Assert.Null(result.ErrorMessage);
			}
			finally
			{
				File.Delete(tempFile);
			}
		}

		[Fact]
		public void ValidateFileExists_WithNonExistentFile_ShouldReturnFailure()
		{
			// Arrange
			string nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

			// Act
			var result = ValidationHelper.ValidateFileExists(nonExistentFile);

			// Assert
			Assert.False(result.IsValid);
			Assert.NotNull(result.ErrorMessage);
		}

		[Fact]
		public void ValidateFileExists_WithNullPath_ShouldReturnFailure()
		{
			// Act
			var result = ValidationHelper.ValidateFileExists(null);

			// Assert
			Assert.False(result.IsValid);
		}

		[Fact]
		public void ValidateDirectoryExists_WithExistingDirectory_ShouldReturnSuccess()
		{
			// Arrange
			string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			Directory.CreateDirectory(tempDir);

			try
			{
				// Act
				var result = ValidationHelper.ValidateDirectoryExists(tempDir);

				// Assert
				Assert.True(result.IsValid);
			}
			finally
			{
				Directory.Delete(tempDir);
			}
		}

		[Fact]
		public void ValidateNotNullOrEmpty_WithValidString_ShouldReturnSuccess()
		{
			// Act
			var result = ValidationHelper.ValidateNotNullOrEmpty("test");

			// Assert
			Assert.True(result.IsValid);
		}

		[Fact]
		public void ValidateNotNullOrEmpty_WithNullString_ShouldReturnFailure()
		{
			// Act
			var result = ValidationHelper.ValidateNotNullOrEmpty(null);

			// Assert
			Assert.False(result.IsValid);
		}

		[Fact]
		public void ValidateNotNull_WithNullObject_ShouldReturnFailure()
		{
			// Act
			var result = ValidationHelper.ValidateNotNull(null);

			// Assert
			Assert.False(result.IsValid);
		}

		[Fact]
		public void ValidateNotNull_WithValidObject_ShouldReturnSuccess()
		{
			// Act
			var result = ValidationHelper.ValidateNotNull(new object());

			// Assert
			Assert.True(result.IsValid);
		}

		[Fact]
		public void ThrowIfInvalid_WithInvalidResult_ShouldThrow()
		{
			// Arrange
			var result = ValidationResult.Failure("Test error");

			// Act & Assert
			Assert.Throws<ArgumentException>(() => result.ThrowIfInvalid());
		}

		[Fact]
		public void ThrowIfInvalid_WithValidResult_ShouldNotThrow()
		{
			// Arrange
			var result = ValidationResult.Success();

			// Act & Assert
			result.ThrowIfInvalid(); // 不应该抛出异常
			Assert.True(true);
		}
	}
}

