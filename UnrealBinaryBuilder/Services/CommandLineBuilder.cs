using System;
using System.Text;
using UnrealBinaryBuilder.Classes;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// Command line builder - Build UAT command line arguments
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
		/// Build engine build command line
		/// </summary>
		public string BuildEngineCommandLine(BuilderSettingsJson settings, string customBuildXmlFile,
			string gameConfigurations, string customOptions, string analyticsOverride, string enginePath)
		{
			try
			{
				var commandLine = new StringBuilder();

				// Build XML file path
				string buildXmlFile = GetBuildXmlFile(customBuildXmlFile);
				commandLine.AppendFormat("BuildGraph -target=\"Make Installed Build Win64\" -script={0}", buildXmlFile);

				// Basic options
				commandLine.AppendFormat(" -set:WithDDC={0}", GetConditionalString(settings.bWithDDC));
				commandLine.AppendFormat(" -set:SignExecutables={0}", GetConditionalString(settings.bSignExecutables));
				commandLine.AppendFormat(" -set:EmbedSrcSrvInfo={0}", GetConditionalString(settings.bEnableSymStore));
				commandLine.AppendFormat(" -set:GameConfigurations={0}", gameConfigurations ?? "Development;Shipping");
				commandLine.AppendFormat(" -set:WithFullDebugInfo={0}", GetConditionalString(settings.bWithFullDebugInfo));
				commandLine.AppendFormat(" -set:HostPlatformEditorOnly={0}", GetConditionalString(settings.bHostPlatformEditorOnly));
				commandLine.AppendFormat(" -set:AnalyticsTypeOverride={0}", analyticsOverride ?? "");

				// DDC options
				if (settings.bWithDDC && settings.bHostPlatformDDCOnly)
				{
					commandLine.Append(" -set:HostPlatformDDCOnly=true");
				}

				// Platform options
				AppendPlatformOptions(commandLine, settings, enginePath);

				// Datasmith plugins
				double engineValue = _versionDetector.GetEngineVersionValue(enginePath);
				if (engineValue >= 4.25)
				{
					commandLine.AppendFormat(" -set:CompileDatasmithPlugins={0}", GetConditionalString(settings.bCompileDatasmithPlugins));
				}

				// Visual Studio version
				AppendVisualStudioOptions(commandLine, settings, engineValue);

				// Server/Client targets
				if (engineValue > 4.22)
				{
					commandLine.AppendFormat(" -set:WithServer={0}", GetConditionalString(settings.bWithServer));
					commandLine.AppendFormat(" -set:WithClient={0}", GetConditionalString(settings.bWithClient));
					commandLine.AppendFormat(" -set:WithHoloLens={0}", GetConditionalString(settings.bWithHoloLens));
				}

				// Custom options
				if (buildXmlFile != "Engine/Build/InstalledEngineBuild.xml" && !string.IsNullOrEmpty(customOptions))
				{
					commandLine.Append($" {customOptions}");
					_logger.LogInfo("Using custom options");
				}

				// Clean build
				if (settings.bCleanBuild)
				{
					commandLine.Append(" -Clean");
					_logger.LogInfo("Clean build enabled");
				}

				string result = commandLine.ToString();
				_logger.LogDebug($"Built command line: {result}");
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "Error occurred while building command line");
				throw;
			}
		}

		/// <summary>
		/// Build Setup.bat command line
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

			// Windows platform
			if (isUE4)
			{
				commandLine.Append($" -set:WithWin32={GetConditionalString(settings.bWithWin32)}");
			}
			commandLine.Append($" -set:WithWin64={GetConditionalString(settings.bWithWin64)}");

			// Other platforms
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

			// HTML5 (UE4 < 4.24 only)
			if (engineValue < 4.24)
			{
				commandLine.Append($" -set:WithHTML5={GetConditionalString(settings.bWithHTML5)}");
			}

			// Console platforms (UE4 <= 4.24 only)
			if (engineValue <= 4.24)
			{
				commandLine.Append($" -set:WithSwitch={GetConditionalString(settings.bWithSwitch)}");
				commandLine.Append($" -set:WithPS4={GetConditionalString(settings.bWithPS4)}");
				commandLine.Append($" -set:WithXboxOne={GetConditionalString(settings.bWithXboxOne)}");
			}
		}

		private void AppendVisualStudioOptions(StringBuilder commandLine, BuilderSettingsJson settings, double engineValue)
		{
			bool useVS2026 = string.Equals(settings.PreferredCompilerVersion, UnrealBinaryBuilderHelpers.VisualStudioVersion2026, StringComparison.OrdinalIgnoreCase);
			bool useVS2022 = string.Equals(settings.PreferredCompilerVersion, UnrealBinaryBuilderHelpers.VisualStudioVersion2022, StringComparison.OrdinalIgnoreCase) && !useVS2026;
			bool useVS2019 = string.Equals(settings.PreferredCompilerVersion, UnrealBinaryBuilderHelpers.VisualStudioVersion2019, StringComparison.OrdinalIgnoreCase) && !useVS2026 && !useVS2022;

			if (!useVS2026 && !useVS2022 && !useVS2019)
			{
				useVS2026 = settings.bVS2026;
				useVS2022 = settings.bVS2022 && !useVS2026;
				useVS2019 = settings.bVS2019 && !useVS2026 && !useVS2022;
			}

			if (engineValue >= 5.5)
			{
				commandLine.Append($" -set:VS2026={GetConditionalString(useVS2026)}");
			}

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
		/// Build project build command line
		/// </summary>
		public string BuildProjectCommandLine(string projectPath, string enginePath, string targetType,
			string targetPlatform, string configuration, bool bCook, bool bCookAll, bool bPackage, bool bBuild, string additionalArgs)
		{
			try
			{
				var commandLine = new StringBuilder();

				// BuildCookRun is the main UAT command
				commandLine.Append("BuildCookRun");

				// Project path
				if (!string.IsNullOrWhiteSpace(projectPath))
				{
					commandLine.AppendFormat(" -project=\"{0}\"", projectPath);
				}

				// Target type (Editor/Client/Server)
				if (!string.IsNullOrWhiteSpace(targetType))
				{
					commandLine.AppendFormat(" -target={0}", targetType);
				}

				// Target platform
				if (!string.IsNullOrWhiteSpace(targetPlatform))
				{
					commandLine.AppendFormat(" -platform={0}", targetPlatform);
				}

				// Configuration
				if (!string.IsNullOrWhiteSpace(configuration))
				{
					commandLine.AppendFormat(" -configuration={0}", configuration);
				}

				// Build option
				if (bBuild)
				{
					commandLine.Append(" -build");
				}

				// Cook option
				if (bCook)
				{
					commandLine.Append(" -cook");
					if (bCookAll)
					{
						commandLine.Append(" -cookall");
					}
				}

				// Package option
				if (bPackage)
				{
					commandLine.Append(" -package");
				}

				// Additional arguments
				if (!string.IsNullOrWhiteSpace(additionalArgs))
				{
					commandLine.AppendFormat(" {0}", additionalArgs);
				}

				string result = commandLine.ToString();
				_logger.LogDebug($"Project build command line: {result}");
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "Error occurred while building project command line");
				throw;
			}
		}

		private string GetConditionalString(bool? condition)
		{
			return condition == true ? "true" : "false";
		}
	}
}