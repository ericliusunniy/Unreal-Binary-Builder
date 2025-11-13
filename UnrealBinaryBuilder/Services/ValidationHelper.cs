using System;
using System.IO;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 验证辅助类 - 提供常用的验证方法
	/// </summary>
	public static class ValidationHelper
	{
		/// <summary>
		/// 验证文件是否存在
		/// </summary>
		public static ValidationResult ValidateFileExists(string filePath, string parameterName = "文件路径")
		{
			if (string.IsNullOrWhiteSpace(filePath))
			{
				return ValidationResult.Failure($"{parameterName} 不能为空");
			}

			if (!File.Exists(filePath))
			{
				return ValidationResult.Failure($"{parameterName} 不存在: {filePath}");
			}

			return ValidationResult.Success();
		}

		/// <summary>
		/// 验证目录是否存在
		/// </summary>
		public static ValidationResult ValidateDirectoryExists(string directoryPath, string parameterName = "目录路径")
		{
			if (string.IsNullOrWhiteSpace(directoryPath))
			{
				return ValidationResult.Failure($"{parameterName} 不能为空");
			}

			if (!Directory.Exists(directoryPath))
			{
				return ValidationResult.Failure($"{parameterName} 不存在: {directoryPath}");
			}

			return ValidationResult.Success();
		}

		/// <summary>
		/// 验证字符串不为空
		/// </summary>
		public static ValidationResult ValidateNotNullOrEmpty(string value, string parameterName = "参数")
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return ValidationResult.Failure($"{parameterName} 不能为空");
			}

			return ValidationResult.Success();
		}

		/// <summary>
		/// 验证对象不为 null
		/// </summary>
		public static ValidationResult ValidateNotNull(object value, string parameterName = "参数")
		{
			if (value == null)
			{
				return ValidationResult.Failure($"{parameterName} 不能为 null");
			}

			return ValidationResult.Success();
		}

		/// <summary>
		/// 验证路径格式
		/// </summary>
		public static ValidationResult ValidatePath(string path, string parameterName = "路径")
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return ValidationResult.Failure($"{parameterName} 不能为空");
			}

			try
			{
				Path.GetFullPath(path);
				return ValidationResult.Success();
			}
			catch (Exception ex)
			{
				return ValidationResult.Failure($"{parameterName} 格式无效: {ex.Message}");
			}
		}
	}

	/// <summary>
	/// 验证结果
	/// </summary>
	public class ValidationResult
	{
		public bool IsValid { get; private set; }
		public string ErrorMessage { get; private set; }

		private ValidationResult(bool isValid, string errorMessage = null)
		{
			IsValid = isValid;
			ErrorMessage = errorMessage;
		}

		public static ValidationResult Success()
		{
			return new ValidationResult(true);
		}

		public static ValidationResult Failure(string errorMessage)
		{
			return new ValidationResult(false, errorMessage);
		}

		public void ThrowIfInvalid()
		{
			if (!IsValid)
			{
				throw new ArgumentException(ErrorMessage);
			}
		}
	}
}

