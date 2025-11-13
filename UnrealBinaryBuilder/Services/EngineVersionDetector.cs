using System;
using System.IO;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 引擎版本检测器 - 处理引擎版本检测逻辑
	/// </summary>
	public class EngineVersionDetector
	{
		private readonly ILogger _logger;
		private string _engineVersionMajor;
		private string _engineVersionMinor;
		private string _engineVersionPatch;

		public bool IsUnrealEngine5 { get; private set; } = false;

		public EngineVersionDetector(ILogger logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// 检测引擎版本
		/// </summary>
		public string DetectEngineVersion(string baseEnginePath, bool forceDetect = false)
		{
			if (string.IsNullOrWhiteSpace(baseEnginePath))
			{
				_logger.LogWarning("引擎路径为空，无法检测版本");
				return null;
			}

			if (_engineVersionMajor == null || forceDetect)
			{
				string engineVersion = GetEngineVersion(baseEnginePath);
				if (engineVersion != null)
				{
					string[] splitString = engineVersion.Split('.');
					if (splitString.Length >= 3)
					{
						_engineVersionMajor = splitString[0];
						_engineVersionMinor = splitString[1];
						_engineVersionPatch = splitString[2];
						IsUnrealEngine5 = _engineVersionMajor.StartsWith("5");
						_logger.LogInfo($"检测到引擎版本: {engineVersion} (UE5: {IsUnrealEngine5})");
					}
					else
					{
						_logger.LogWarning($"无法解析引擎版本: {engineVersion}");
						ResetVersion();
						return null;
					}
				}
				else
				{
					ResetVersion();
					return null;
				}
			}

			return $"{_engineVersionMajor}.{_engineVersionMinor}.{_engineVersionPatch}";
		}

		/// <summary>
		/// 获取引擎版本号
		/// </summary>
		private string GetEngineVersion(string baseEnginePath)
		{
			string versionFile = Path.Combine(baseEnginePath, "Engine", "Source", "Runtime", "Launch", "Resources", "Version.h");
			
			if (!File.Exists(versionFile))
			{
				_logger.LogWarning($"版本文件不存在: {versionFile}");
				return null;
			}

			try
			{
				string engineVersionMajor = null;
				string engineVersionMinor = null;
				string engineVersionPatch = null;

				using (var file = new StreamReader(versionFile))
				{
					string currentLine;
					while ((currentLine = file.ReadLine()) != null)
					{
						if (currentLine.StartsWith("#define ENGINE_MAJOR_VERSION"))
						{
							engineVersionMajor = currentLine.Replace("#define ENGINE_MAJOR_VERSION", "")
								.Replace("\t", "").Trim();
						}
						else if (currentLine.StartsWith("#define ENGINE_MINOR_VERSION"))
						{
							engineVersionMinor = currentLine.Replace("#define ENGINE_MINOR_VERSION", "")
								.Replace("\t", "").Trim();
						}
						else if (currentLine.StartsWith("#define ENGINE_PATCH_VERSION"))
						{
							engineVersionPatch = currentLine.Replace("#define ENGINE_PATCH_VERSION", "")
								.Replace("\t", "").Trim();
							break;
						}
					}
				}

				if (!string.IsNullOrWhiteSpace(engineVersionMajor) &&
				    !string.IsNullOrWhiteSpace(engineVersionMinor) &&
				    !string.IsNullOrWhiteSpace(engineVersionPatch))
				{
					return $"{engineVersionMajor}.{engineVersionMinor}.{engineVersionPatch}";
				}
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, $"读取引擎版本文件时发生错误: {versionFile}");
			}

			return null;
		}

		/// <summary>
		/// 获取引擎版本数值（用于比较）
		/// </summary>
		public double GetEngineVersionValue(string baseEnginePath)
		{
			string engineVersion = DetectEngineVersion(baseEnginePath);
			if (engineVersion != null)
			{
				string[] parts = engineVersion.Split('.');
				if (parts.Length >= 2)
				{
					if (double.TryParse($"{parts[0]}.{parts[1]}", out double result))
					{
						return result;
					}
				}
			}
			return 0;
		}

		/// <summary>
		/// 重置版本信息
		/// </summary>
		private void ResetVersion()
		{
			_engineVersionMajor = _engineVersionMinor = _engineVersionPatch = null;
			IsUnrealEngine5 = false;
		}
	}
}

