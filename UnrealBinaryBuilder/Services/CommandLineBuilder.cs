using System;
using System.Text;
using UnrealBinaryBuilder.Classes;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 命令行构建器 - 构建 UAT 命令行参数
	/// </summary>
	public class CommandLineBuilder
	{
		private readonly ILogger _logger;
		private readonly EngineVersionDetector _versionDetector;

		public CommandLineBuilder(ILogger logger, EngineVersionDetector versionDetector)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_versionDetector = versionDetector ?? throw new ArgumentNullException(nameof(versionDetector));
		}

		/// <summary>
		/// 构建引擎构建命令行
		/// </summary>
		public string BuildEngineCommandLine(BuilderSettingsJson settings, string customBuildXmlFile,
			string gameConfigurations, string customOptions, string analyticsOverride, string enginePath)
		{
			try
			{
				var commandLine = new StringBuilder();

				// 构建 XML 文件路径
				string buildXmlFile = GetBuildXmlFile(customBuildXmlFile);
				commandLine.AppendFormat("BuildGraph -target=\"Make Installed Build Win64\" -script={0}", buildXmlFile);

				// 基本选项
				commandLine.AppendFormat(" -set:WithDDC={0}", GetConditionalString(settings.bWithDDC));
				commandLine.AppendFormat(" -set:SignExecutables={0}", GetConditionalString(settings.bSignExecutables));
				commandLine.AppendFormat(" -set:EmbedSrcSrvInfo={0}", GetConditionalString(settings.bEnableSymStore));
				commandLine.AppendFormat(" -set:GameConfigurations={0}", gameConfigurations ?? "Development;Shipping");
				commandLine.AppendFormat(" -set:WithFullDebugInfo={0}", GetConditionalString(settings.bWithFullDebugInfo));
				commandLine.AppendFormat(" -set:HostPlatformEditorOnly={0}", GetConditionalString(settings.bHostPlatformEditorOnly));
				commandLine.AppendFormat(" -set:AnalyticsTypeOverride={0}", analyticsOverride ?? "");

				// DDC 选项
				if (settings.bWithDDC && settings.bHostPlatformDDCOnly)
				{
					commandLine.Append(" -set:HostPlatformDDCOnly=true");
				}

				// 平台选项
				AppendPlatformOptions(commandLine, settings, enginePath);

				// Datasmith 插件
				double engineValue = _versionDetector.GetEngineVersionValue(enginePath);
				if (engineValue >= 4.25)
				{
					commandLine.AppendFormat(" -set:CompileDatasmithPlugins={0}", GetConditionalString(settings.bCompileDatasmithPlugins));
				}

				// Visual Studio 版本
				AppendVisualStudioOptions(commandLine, settings, engineValue);

				// 服务器/客户端目标
				if (engineValue > 4.22)
				{
					commandLine.AppendFormat(" -set:WithServer={0}", GetConditionalString(settings.bWithServer));
					commandLine.AppendFormat(" -set:WithClient={0}", GetConditionalString(settings.bWithClient));
					commandLine.AppendFormat(" -set:WithHoloLens={0}", GetConditionalString(settings.bWithHoloLens));
				}

				// 自定义选项
				if (buildXmlFile != "Engine/Build/InstalledEngineBuild.xml" && !string.IsNullOrEmpty(customOptions))
				{
					commandLine.Append($" {customOptions}");
					_logger.LogInfo("使用自定义选项");
				}

				// 清理构建
				if (settings.bCleanBuild)
				{
					commandLine.Append(" -Clean");
					_logger.LogInfo("启用清理构建");
				}

				string result = commandLine.ToString();
				_logger.LogDebug($"构建的命令行: {result}");
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "构建命令行时发生错误");
				throw;
			}
		}

		/// <summary>
		/// 构建 Setup.bat 命令行
		/// </summary>
		public string BuildSetupCommandLine(BuilderSettingsJson settings)
		{
			var commandLine = new StringBuilder("--force");

			if (settings.GitDependencyAll)
			{
				commandLine.Append(" --all");
			}

			foreach (var platform in settings.GitDependencyPlatforms)
			{
				if (!platform.bIsIncluded)
				{
					commandLine.Append($" --exclude={platform.Name}");
				}
			}

			commandLine.Append($" --threads={settings.GitDependencyThreads}");
			commandLine.Append($" --max-retries={settings.GitDependencyMaxRetries}");

			if (!settings.GitDependencyEnableCache)
			{
				commandLine.Append(" --no-cache");
			}
			else if (!string.IsNullOrEmpty(settings.GitDependencyCache))
			{
				commandLine.Append($" --cache={settings.GitDependencyCache.Replace("\\", "/")}");
				commandLine.Append($" --cache-size-multiplier={settings.GitDependencyCacheMultiplier}");
				commandLine.Append($" --cache-days={settings.GitDependencyCacheDays}");
			}

			if (!string.IsNullOrEmpty(settings.GitDependencyProxy))
			{
				commandLine.Append($" --proxy={settings.GitDependencyProxy}");
			}

			return commandLine.ToString();
		}

		private string GetBuildXmlFile(string customBuildXmlFile)
		{
			const string defaultBuildXml = "Engine/Build/InstalledEngineBuild.xml";

			if (string.IsNullOrEmpty(customBuildXmlFile) || customBuildXmlFile == defaultBuildXml)
			{
				return defaultBuildXml;
			}

			return $"\"{customBuildXmlFile}\"";
		}

		private void AppendPlatformOptions(StringBuilder commandLine, BuilderSettingsJson settings, string enginePath)
		{
			double engineValue = _versionDetector.GetEngineVersionValue(enginePath);
			bool isUE4 = engineValue < 5.0;

			if (settings.bHostPlatformOnly)
			{
				commandLine.Append(" -set:HostPlatformOnly=true");
				return;
			}

			// Windows 平台
			if (isUE4)
			{
				commandLine.Append($" -set:WithWin32={GetConditionalString(settings.bWithWin32)}");
			}
			commandLine.Append($" -set:WithWin64={GetConditionalString(settings.bWithWin64)}");

			// 其他平台
			commandLine.Append($" -set:WithMac={GetConditionalString(settings.bWithMac)}");
			commandLine.Append($" -set:WithAndroid={GetConditionalString(settings.bWithAndroid)}");
			commandLine.Append($" -set:WithIOS={GetConditionalString(settings.bWithIOS)}");
			commandLine.Append($" -set:WithTVOS={GetConditionalString(settings.bWithTVOS)}");
			commandLine.Append($" -set:WithLinux={GetConditionalString(settings.bWithLinux)}");
			commandLine.Append($" -set:WithLumin={GetConditionalString(settings.bWithLumin)}");

			// Linux ARM64
			if (isUE4 && engineValue >= 4.24)
			{
				commandLine.Append($" -set:WithLinuxAArch64={GetConditionalString(settings.bWithLinuxAArch64)}");
			}
			else if (!isUE4)
			{
				commandLine.Append($" -set:WithLinuxArm64={GetConditionalString(settings.bWithLinuxAArch64)}");
			}

			// HTML5 (仅 UE4 < 4.24)
			if (engineValue < 4.24)
			{
				commandLine.Append($" -set:WithHTML5={GetConditionalString(settings.bWithHTML5)}");
			}

			// 游戏机平台 (仅 UE4 <= 4.24)
			if (engineValue <= 4.24)
			{
				commandLine.Append($" -set:WithSwitch={GetConditionalString(settings.bWithSwitch)}");
				commandLine.Append($" -set:WithPS4={GetConditionalString(settings.bWithPS4)}");
				commandLine.Append($" -set:WithXboxOne={GetConditionalString(settings.bWithXboxOne)}");
			}
		}

		private void AppendVisualStudioOptions(StringBuilder commandLine, BuilderSettingsJson settings, double engineValue)
		{
			// 这里需要检查 Visual Studio 可用性，简化处理
			bool useVS2022 = settings.bVS2022;
			bool useVS2019 = settings.bVS2019 && !useVS2022;

			if (engineValue >= 4.27)
			{
				commandLine.Append($" -set:VS2022={GetConditionalString(useVS2022)}");
			}

			if (engineValue >= 4.25)
			{
				commandLine.Append($" -set:VS2019={GetConditionalString(useVS2019)}");
			}
		}

		/// <summary>
		/// 构建项目构建命令行
		/// </summary>
		public string BuildProjectCommandLine(string projectPath, string enginePath, string targetType,
			string targetPlatform, string configuration, bool bCook, bool bCookAll, bool bPackage, bool bBuild, string additionalArgs)
		{
			try
			{
				var commandLine = new StringBuilder();

				// BuildCookRun 是 UAT 的主要命令
				commandLine.Append("BuildCookRun");

				// 项目路径
				if (!string.IsNullOrWhiteSpace(projectPath))
				{
					commandLine.AppendFormat(" -project=\"{0}\"", projectPath);
				}

				// 目标类型 (Editor/Client/Server)
				if (!string.IsNullOrWhiteSpace(targetType))
				{
					commandLine.AppendFormat(" -target={0}", targetType);
				}

				// 目标平台
				if (!string.IsNullOrWhiteSpace(targetPlatform))
				{
					commandLine.AppendFormat(" -platform={0}", targetPlatform);
				}

				// 配置
				if (!string.IsNullOrWhiteSpace(configuration))
				{
					commandLine.AppendFormat(" -configuration={0}", configuration);
				}

				// 构建选项
				if (bBuild)
				{
					commandLine.Append(" -build");
				}

				// Cook 选项
				if (bCook)
				{
					commandLine.Append(" -cook");
					if (bCookAll)
					{
						commandLine.Append(" -cookall");
					}
				}

				// Package 选项
				if (bPackage)
				{
					commandLine.Append(" -package");
				}

				// 额外参数
				if (!string.IsNullOrWhiteSpace(additionalArgs))
				{
					commandLine.AppendFormat(" {0}", additionalArgs);
				}

				string result = commandLine.ToString();
				_logger.LogDebug($"项目构建命令行: {result}");
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "构建项目命令行时发生错误");
				throw;
			}
		}

		private string GetConditionalString(bool? condition)
		{
			return condition == true ? "true" : "false";
		}
	}
}