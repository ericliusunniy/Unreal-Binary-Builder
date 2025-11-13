
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Themes;
using HandyControl.Tools;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using UnrealBinaryBuilder.Classes;
using UnrealBinaryBuilder.Services;
using UnrealBinaryBuilder.Services.Extensions;
using UnrealBinaryBuilder.UserControls;
using UnrealBinaryBuilderUpdater;
using System.Windows.Data;

namespace UnrealBinaryBuilder
{
	public static class UnrealBinaryBuilderHelpers
	{
		public static readonly string SetupBatFileName = "Setup.bat";
		public static readonly string GenerateProjectBatFileName = "GenerateProjectFiles.bat";
		public static readonly string AUTOMATION_TOOL_NAME = "AutomationTool";
		public static readonly string AUTOMATION_TOOL_LAUNCHER_NAME = $"{AUTOMATION_TOOL_NAME}Launcher";
		public static readonly string DEFAULT_BUILD_XML_FILE = "Engine/Build/InstalledEngineBuild.xml";
		public static bool IsUnrealEngine5 { get; private set; } = false;

		private static readonly string[] VisualStudioEditions = new[] { "Enterprise", "Professional", "Community", "BuildTools", "Preview" };
		private static readonly Dictionary<int, string> MsBuildPathsByMajorVersion = new Dictionary<int, string>();
		private static bool bVisualStudioDetectionAttempted = false;
		private static readonly object VisualStudioDetectionLock = new object();

		public static bool VisualStudio2017Available { get; private set; } = false;
		public static bool VisualStudio2019Available { get; private set; } = false;
		public static bool VisualStudio2022Available { get; private set; } = false;
		public static int LatestVisualStudioMajorVersion { get; private set; } = 0;
		public static string LatestMsBuildPath { get; private set; } = null;

		private static string EngineVersionMajor, EngineVersionMinor, EngineVersionPatch = null;
		public static string GetProductVersionString()
		{
			Version ProductVersion = Assembly.GetEntryAssembly().GetName().Version;
			string ReturnValue = $"{ProductVersion.Major}.{ProductVersion.Minor}";

			if (ProductVersion.Build > 0)
			{
				ReturnValue += $".{ProductVersion.Build}";
			}

			if (ProductVersion.Revision > 0)
			{
				ReturnValue += $".{ProductVersion.Revision}";
			}

			return ReturnValue;
		}

		public static string GetMsBuildPath()
		{
			EnsureVisualStudioInformation();

			if (!string.IsNullOrEmpty(LatestMsBuildPath) && File.Exists(LatestMsBuildPath))
			{
				return LatestMsBuildPath;
			}

			string fallback2019 = TryFindMsBuildInKnownLocations("2019");
			if (fallback2019 != null)
			{
				return fallback2019;
			}

			string fallback2017 = TryFindMsBuildInKnownLocations("2017");
			return fallback2017;
		}

		public static bool TryGetMsBuildPathForVersion(int majorVersion, out string msBuildPath)
		{
			EnsureVisualStudioInformation();
			return MsBuildPathsByMajorVersion.TryGetValue(majorVersion, out msBuildPath);
		}

		public static bool HasVisualStudioMajorVersion(int majorVersion)
		{
			EnsureVisualStudioInformation();
			return MsBuildPathsByMajorVersion.ContainsKey(majorVersion);
		}

		private static void EnsureVisualStudioInformation()
		{
			if (bVisualStudioDetectionAttempted)
			{
				return;
			}

			lock (VisualStudioDetectionLock)
			{
				if (bVisualStudioDetectionAttempted)
				{
					return;
				}

				RefreshVisualStudioInformation();
				bVisualStudioDetectionAttempted = true;
			}
		}

		private static void RefreshVisualStudioInformation()
		{
			MsBuildPathsByMajorVersion.Clear();
			VisualStudio2017Available = false;
			VisualStudio2019Available = false;
			VisualStudio2022Available = false;
			LatestVisualStudioMajorVersion = 0;
			LatestMsBuildPath = null;

			foreach (VsWhereInstallation installation in QueryVisualStudioInstallations().OrderByDescending(inst => inst.installationVersion))
			{
				if (string.IsNullOrWhiteSpace(installation.installationPath))
				{
					continue;
				}

				string msbuildPath = BuildMsBuildPathFromInstallation(installation.installationPath);
				if (string.IsNullOrEmpty(msbuildPath))
				{
					continue;
				}

				int majorVersion = GetMajorVersionFromInstallation(installation.installationVersion, installation.installationPath);
				if (majorVersion == 0)
				{
					continue;
				}

				RecordVisualStudioInstallation(majorVersion, msbuildPath);
			}

			if (string.IsNullOrEmpty(LatestMsBuildPath))
			{
				TryFindMsBuildInKnownLocations("2022", "2019", "2017");
			}
		}

		private static IEnumerable<VsWhereInstallation> QueryVisualStudioInstallations()
		{
			List<VsWhereInstallation> installations = new List<VsWhereInstallation>();
			try
			{
				string programFilesx86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
				if (string.IsNullOrEmpty(programFilesx86))
				{
					return installations;
				}

				string vsWhereExecutable = Path.Combine(programFilesx86, "Microsoft Visual Studio", "Installer", "vswhere.exe");
				if (!File.Exists(vsWhereExecutable))
				{
					return installations;
				}

				ProcessStartInfo psi = new ProcessStartInfo
				{
					FileName = vsWhereExecutable,
					Arguments = "-all -products * -requires Microsoft.Component.MSBuild -format json",
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				};

				using (Process process = Process.Start(psi))
				{
					if (process != null)
					{
						try
						{
							string output = process.StandardOutput.ReadToEnd();
							process.WaitForExit();
							if (!string.IsNullOrWhiteSpace(output))
							{
								installations = JsonConvert.DeserializeObject<List<VsWhereInstallation>>(output);
							}
						}
						finally
						{
							if (process != null && !process.HasExited)
							{
								try
								{
									process.Kill();
								}
								catch (Exception ex)
								{
									System.Diagnostics.Debug.WriteLine($"Failed to kill process: {ex.Message}");
								}
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				// Log detection errors but continue. Fallback logic will handle missing data.
				System.Diagnostics.Debug.WriteLine($"Visual Studio detection error: {ex.Message}");
			}

			return installations ?? new List<VsWhereInstallation>();
		}

		private static void RecordVisualStudioInstallation(int majorVersion, string msbuildPath)
		{
			if (string.IsNullOrEmpty(msbuildPath) || !File.Exists(msbuildPath))
			{
				return;
			}

			MsBuildPathsByMajorVersion[majorVersion] = msbuildPath;

			if (majorVersion >= 17)
			{
				VisualStudio2022Available = true;
			}
			else if (majorVersion == 16)
			{
				VisualStudio2019Available = true;
			}
			else if (majorVersion == 15)
			{
				VisualStudio2017Available = true;
			}

			if (LatestMsBuildPath == null || majorVersion > LatestVisualStudioMajorVersion)
			{
				LatestVisualStudioMajorVersion = majorVersion;
				LatestMsBuildPath = msbuildPath;
			}
		}

		private static string BuildMsBuildPathFromInstallation(string installationPath)
		{
			if (string.IsNullOrEmpty(installationPath))
			{
				return null;
			}

			string candidate = Path.Combine(installationPath, "MSBuild", "Current", "Bin", "MSBuild.exe");
			if (File.Exists(candidate))
			{
				return candidate;
			}

			candidate = Path.Combine(installationPath, "MSBuild", "Current", "Bin", "amd64", "MSBuild.exe");
			if (File.Exists(candidate))
			{
				return candidate;
			}

			candidate = Path.Combine(installationPath, "MSBuild", "15.0", "Bin", "MSBuild.exe");
			if (File.Exists(candidate))
			{
				return candidate;
			}

			return null;
		}

		private static int GetMajorVersionFromInstallation(string installationVersion, string installationPath)
		{
			if (!string.IsNullOrWhiteSpace(installationVersion))
			{
				string[] split = installationVersion.Split('.');
				if (split.Length > 0 && int.TryParse(split[0], out int parsedMajor))
				{
					return parsedMajor;
				}
			}

			if (!string.IsNullOrEmpty(installationPath))
			{
				if (installationPath.Contains($"{Path.DirectorySeparatorChar}2022") || installationPath.Contains("/2022"))
				{
					return 17;
				}

				if (installationPath.Contains($"{Path.DirectorySeparatorChar}2019") || installationPath.Contains("/2019"))
				{
					return 16;
				}

				if (installationPath.Contains($"{Path.DirectorySeparatorChar}2017") || installationPath.Contains("/2017"))
				{
					return 15;
				}
			}

			return 0;
		}

		private static string TryFindMsBuildInKnownLocations(params string[] yearFolders)
		{
			string programFilesx86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
			if (string.IsNullOrEmpty(programFilesx86))
			{
				return null;
			}

			foreach (string year in yearFolders)
			{
				if (string.IsNullOrWhiteSpace(year))
				{
					continue;
				}

				string vsBasePath = Path.Combine(programFilesx86, "Microsoft Visual Studio", year);
				if (!Directory.Exists(vsBasePath))
				{
					continue;
				}

				foreach (string edition in VisualStudioEditions)
				{
					string editionPath = Path.Combine(vsBasePath, edition);
					if (!Directory.Exists(editionPath))
					{
						continue;
					}

					string msbuildPath = BuildMsBuildPathFromInstallation(editionPath);
					if (!string.IsNullOrEmpty(msbuildPath))
					{
						int majorVersion = MapYearToMajorVersion(year);
						if (majorVersion > 0)
						{
							RecordVisualStudioInstallation(majorVersion, msbuildPath);
							return msbuildPath;
						}
					}
				}
			}

			return null;
		}

		private static int MapYearToMajorVersion(string year)
		{
			switch (year)
			{
				case "2022":
					return 17;
				case "2019":
					return 16;
				case "2017":
					return 15;
				default:
					return 0;
			}
		}

		private class VsWhereInstallation
		{
			public string installationPath { get; set; }
			public string installationVersion { get; set; }
		}

		public static string GetEngineVersion(string BaseEnginePath)
		{
			string VersionFile = Path.Combine(BaseEnginePath, "Engine", "Source", "Runtime", "Launch", "Resources", "Version.h");
			string Local_EngineVersionMajor = null;
			string Local_EngineVersionMinor = null;
			string Local_EngineVersionPatch = null;
			if (File.Exists(VersionFile))
			{
				try
				{
					using (StreamReader file = new StreamReader(VersionFile))
					{
						string CurrentLine;
						while ((CurrentLine = file.ReadLine()) != null)
						{
							if (CurrentLine.StartsWith("#define ENGINE_MAJOR_VERSION"))
							{
								Local_EngineVersionMajor = CurrentLine.Replace("#define ENGINE_MAJOR_VERSION", "").Replace("\t", "").Trim();
							}
							else if (CurrentLine.StartsWith("#define ENGINE_MINOR_VERSION"))
							{
								Local_EngineVersionMinor = CurrentLine.Replace("#define ENGINE_MINOR_VERSION", "").Replace("\t", "").Trim();
							}
							else if (CurrentLine.StartsWith("#define ENGINE_PATCH_VERSION"))
							{
								Local_EngineVersionPatch = CurrentLine.Replace("#define ENGINE_PATCH_VERSION", "").Replace("\t", "").Trim();
								break;
							}
						}
					}

					if (!string.IsNullOrWhiteSpace(Local_EngineVersionMajor) && 
					    !string.IsNullOrWhiteSpace(Local_EngineVersionMinor) && 
					    !string.IsNullOrWhiteSpace(Local_EngineVersionPatch))
					{
						return $"{Local_EngineVersionMajor}.{Local_EngineVersionMinor}.{Local_EngineVersionPatch}";
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Failed to read engine version file: {ex.Message}");
				}
			}

			return null;
		}

		public static string DetectEngineVersion(string BaseEnginePath, bool bForceDetect = false)
		{
			if (string.IsNullOrWhiteSpace(BaseEnginePath))
			{
				return null;
			}

			if (EngineVersionMajor == null || bForceDetect)
			{
				string MyEngineVersion = GetEngineVersion(BaseEnginePath);
				if (MyEngineVersion != null)
				{
					string[] SplitString = MyEngineVersion.Split(".");
					EngineVersionMajor = SplitString[0];
					EngineVersionMinor = SplitString[1];
					EngineVersionPatch = SplitString[2];
					IsUnrealEngine5 = EngineVersionMajor.StartsWith("5");
				}
				else
				{
					EngineVersionMajor = EngineVersionMinor = EngineVersionPatch = null;
					IsUnrealEngine5 = false;
					return null;
				}
			}

			return $"{EngineVersionMajor}.{EngineVersionMinor}.{EngineVersionPatch}";
		}

	public static bool AutomationToolExists(string BaseEnginePath)
	{
		if (string.IsNullOrWhiteSpace(BaseEnginePath))
		{
			return false;
		}

		string automationExe = GetAutomationToolExecutablePath(BaseEnginePath);
		return !string.IsNullOrEmpty(automationExe) && File.Exists(automationExe);
	}

	public static string GetAutomationToolExecutablePath(string BaseEnginePath)
	{
		if (string.IsNullOrWhiteSpace(BaseEnginePath))
		{
			return null;
		}

		string automationToolPath = Path.Combine(BaseEnginePath, "Engine", "Binaries", "DotNET", AUTOMATION_TOOL_NAME, $"{AUTOMATION_TOOL_NAME}.exe");
		if (File.Exists(automationToolPath))
		{
			return automationToolPath;
		}

		string automationToolLauncherPath = Path.Combine(BaseEnginePath, "Engine", "Binaries", "DotNET", AUTOMATION_TOOL_LAUNCHER_NAME, $"{AUTOMATION_TOOL_LAUNCHER_NAME}.exe");
		if (File.Exists(automationToolLauncherPath))
		{
			return automationToolLauncherPath;
		}

		return null;
	}

		public static string GetAutomationToolProjectFile(string BaseEnginePath)
		{
			if (string.IsNullOrWhiteSpace(BaseEnginePath))
			{
				return null;
			}

			return Path.Combine(BaseEnginePath, "Engine", "Source", "Programs", AUTOMATION_TOOL_NAME, $"{AUTOMATION_TOOL_NAME}.csproj");
		}

		public static string GetAutomationToolLauncherProjectFile(string BaseEnginePath)
		{
			if (string.IsNullOrWhiteSpace(BaseEnginePath))
			{
				return null;
			}

			return Path.Combine(BaseEnginePath, "Engine", "Source", "Programs", AUTOMATION_TOOL_LAUNCHER_NAME, $"{AUTOMATION_TOOL_LAUNCHER_NAME}.csproj");
		}
	}
	public partial class MainWindow
	{
		#region 服务容器和视图模型
		// 服务容器 - 统一管理所有服务
		private readonly ServiceContainer _services;
		private readonly MainWindowViewModel _viewModel;

		// 服务引用（方便访问）
		private ILogger Logger => _services.Logger;
		private IProcessManager ProcessManager => _services.ProcessManager;
		private IBuildManager BuildManager => _services.BuildManager;
		private UpdateManager UpdateManager => _services.UpdateManager;
		private EngineVersionDetector VersionDetector => _services.VersionDetector;
		private CommandLineBuilder CommandLineBuilder => _services.CommandLineBuilder;
		private LogParser LogParser => _services.LogParser;
		private NotificationService NotificationService => _services.NotificationService;
		private ThemeManager ThemeManager => _services.ThemeManager;
		private SettingsManager SettingsManager => _services.SettingsManager;
		private PerformanceMonitor PerformanceMonitor => _services.PerformanceMonitor;
		private ErrorHandler ErrorHandler => _services.ErrorHandler;
		#endregion

		#region 旧字段（逐步迁移中）
		private Process CurrentProcess = null;

		private int NumErrors = 0;
		private int NumWarnings = 0;

		private int CompiledFiles = 0;
		private int CompiledFilesTotal = 0;

		private bool bIsBuilding = false;
		private bool bLastBuildSuccess = false;

		private string LogMessage = null;
		private string LogMessageErrors = null;
		private string FinalBuildPath = null;

		public string CurrentTheme = null;
		public PostBuildSettings postBuildSettings = null;

		private readonly Stopwatch StopwatchTimer = new Stopwatch();
		private readonly DispatcherTimer DispatchTimer = new DispatcherTimer();

		public BuilderSettingsJson SettingsJSON = null;

		private string AutomationExePath = null;

		private PluginCard CurrentPluginBeingBuilt = null;
		private List<string> PluginBuildEnginePath = new List<string>();
		private Dialog aboutDialog = null;
		private Dialog downloadDialog = null;
		private DownloadDialog downloadDialogWindow = null;
		private static UBBUpdater unrealBinaryBuilderUpdater = null;
		private bool bUpdateAvailable = false;
		private bool bMissingVS2022WarningShown = false;

		public bool AutomationExePathPathIsValid => File.Exists(AutomationExePath);
		#endregion

		public enum ZipLogInclusionType
		{
			FileIncluded,
			FileSkipped,
			ExtensionSkipped
		}

		private enum CurrentProcessType
		{
			None,
			SetupBat,
			GenerateProjectFiles,
			BuildAutomationTool,
			BuildAutomationToolLauncher,
			BuildUnrealEngine,
			BuildPlugin,
			BuildProject
		}

		private CurrentProcessType currentProcessType = CurrentProcessType.None;

		public MainWindow()
		{
			InitializeComponent();

			// 初始化服务容器
			_services = new ServiceContainer();
			_viewModel = new MainWindowViewModel();

			// 初始化 GameAnalytics
			GameAnalyticsCSharp.InitializeGameAnalytics(UnrealBinaryBuilderHelpers.GetProductVersionString(), this);

			// 使用新的日志服务
			string version = UnrealBinaryBuilderHelpers.GetProductVersionString();
			string welcomeMessage = Services.ResourceHelper.GetString("WelcomeMessage", version);
			if (string.IsNullOrEmpty(welcomeMessage))
			{
				welcomeMessage = $"Welcome to Unreal Binary Builder v{version}";
			}
			AddLogEntry(welcomeMessage);
			Logger.LogInfo(welcomeMessage);

			PluginQueueBtn.IsEnabled = false;
			postBuildSettings = new PostBuildSettings(this);

			// 使用新的设置管理器
			SettingsJSON = SettingsManager.Settings ?? BuilderSettings.GetSettingsFile(true);
			_viewModel.Settings = SettingsJSON;
			BuilderSettings.LoadInitialValues();
			DataContext = SettingsJSON;

			// 订阅服务事件
			SubscribeToServiceEvents();

			InitializeVisualStudioPreferences();

			if (Plugins.GetInstalledEngines() == null)
			{
				PluginsTab.Visibility = Visibility.Collapsed;
				string errorMessage = "Could not find any installed Engine versions. Plugins tab is disabled.";
				AddLogEntry(errorMessage, true);
				Logger.LogError(errorMessage);
				NotificationService.ShowError(errorMessage);
			}
			else
			{
				foreach (EngineBuild engineBuild in Plugins.GetInstalledEngines())
				{
					string RunUATFile = Path.Combine(engineBuild.EnginePath, "Engine", "Build", "BatchFiles", "RunUAT.bat");
					if (File.Exists(RunUATFile))
					{
						PluginEngineVersionSelection.Items.Add(engineBuild.EngineName);
						PluginBuildEnginePath.Add(engineBuild.EnginePath);
					}
					else
					{
						string warningMessage = $"{engineBuild.EngineName} will not be available for Plugin build. RunUAT.bat does not exist in {Path.GetDirectoryName(RunUATFile)}.";
						AddLogEntry(warningMessage, true);
						Logger.LogWarning(warningMessage);
					}
				}
			}

			if (File.Exists(AutomationExePath) && Path.GetFileNameWithoutExtension(AutomationExePath) == UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME)
			{
				BuildRocketUE.IsEnabled = true;
			}

			ChangeStatusLabel("Idle.");
			_viewModel.StatusText = Services.ResourceHelper.GetString("StatusIdle", "Idle.");

			DispatchTimer.Tick += new EventHandler(DispatchTimer_Tick);
			DispatchTimer.Interval = new TimeSpan(0, 0, 1);

			// 使用新的主题管理器
			CurrentTheme = SettingsJSON.Theme;
			if (!string.IsNullOrEmpty(CurrentTheme))
			{
				try
				{
					ThemeManager.ApplyTheme(CurrentTheme);
					if (CurrentTheme.ToLower() == "violet")
					{
						NotificationService.ShowWarning("Violet theme is not fully supported yet.", token: "Important", waitTime: 4);
					}
					else if (CurrentTheme.ToLower() != "dark")
					{
						NotificationService.ShowWarning("Default theme is not fully supported yet.", token: "Important", waitTime: 4);
					}
				}
				catch (Exception ex)
				{
					Logger.LogException(ex, "应用主题时发生错误");
					// 回退到旧方法
					if (CurrentTheme.ToLower() == "violet")
					{
						ShowToastMessage("Violet theme is not fully supported yet.", LogViewer.EMessageType.Warning, true, false, "Important", 4);
						UpdateSkin(SkinType.Violet);
					}
					else if (CurrentTheme.ToLower() == "dark")
					{
						UpdateSkin(SkinType.Dark);
					}
					else
					{
						ShowToastMessage("Default theme is not fully supported yet.", LogViewer.EMessageType.Warning, true, false, "Important", 4);
						UpdateSkin(SkinType.Default);
					}
				}
			}

			ZipStatusLabel.Visibility = Visibility.Visible;
			ZipStausStackPanel.Visibility = Visibility.Collapsed;

			// 使用新的更新管理器
			if (SettingsJSON.bCheckForUpdatesAtStartup)
			{
				UpdateManager.CheckForUpdatesSilently();
			}

			// Initialize project build UI texts
			InitializeProjectBuildUI();
		}

		/// <summary>
		/// Initialize project build UI texts from resources
		/// </summary>
		private void InitializeProjectBuildUI()
		{
			try
			{
				// Project Settings GroupBox
				if (ProjectSettingsGroupBox != null)
				{
					ProjectSettingsGroupBox.Header = Services.ResourceHelper.GetString("ProjectSettings");
				}

				// Project Path
				if (ProjectPathLabel != null)
				{
					ProjectPathLabel.Text = Services.ResourceHelper.GetString("ProjectPath");
				}
				if (ProjectPath != null)
				{
					HandyControl.Controls.InfoElement.SetPlaceholder(ProjectPath, Services.ResourceHelper.GetString("ProjectPathPlaceholder"));
				}
				if (ProjectPathBrowse != null)
				{
					ProjectPathBrowse.Content = Services.ResourceHelper.GetString("Browse");
				}

				// Engine Path
				if (EnginePathLabel != null)
				{
					EnginePathLabel.Text = Services.ResourceHelper.GetString("EnginePath");
				}
				if (ProjectEnginePath != null)
				{
					HandyControl.Controls.InfoElement.SetPlaceholder(ProjectEnginePath, Services.ResourceHelper.GetString("EnginePathPlaceholder"));
				}
				if (ProjectEnginePathBrowse != null)
				{
					ProjectEnginePathBrowse.Content = Services.ResourceHelper.GetString("Browse");
				}

				// Build Options GroupBox
				if (BuildOptionsGroupBox != null)
				{
					BuildOptionsGroupBox.Header = Services.ResourceHelper.GetString("BuildOptions");
				}

				// Target Type
				if (TargetTypeLabel != null)
				{
					TargetTypeLabel.Text = Services.ResourceHelper.GetString("TargetType");
				}

				// Target Platform
				if (TargetPlatformLabel != null)
				{
					TargetPlatformLabel.Text = Services.ResourceHelper.GetString("TargetPlatform");
				}

				// Configuration
				if (ConfigurationLabel != null)
				{
					ConfigurationLabel.Text = Services.ResourceHelper.GetString("Configuration");
				}

				// Operation Options GroupBox
				if (OperationOptionsGroupBox != null)
				{
					OperationOptionsGroupBox.Header = Services.ResourceHelper.GetString("OperationOptions");
				}

				// Operation checkboxes
				if (ProjectBuild != null)
				{
					ProjectBuild.Content = Services.ResourceHelper.GetString("Build");
					ProjectBuild.ToolTip = Services.ResourceHelper.GetString("BuildToolTip");
				}
				if (ProjectCook != null)
				{
					ProjectCook.Content = Services.ResourceHelper.GetString("Cook");
					ProjectCook.ToolTip = Services.ResourceHelper.GetString("CookToolTip");
				}
				if (ProjectCookAll != null)
				{
					ProjectCookAll.Content = Services.ResourceHelper.GetString("CookAll");
					ProjectCookAll.ToolTip = Services.ResourceHelper.GetString("CookAllToolTip");
				}
				if (ProjectPackage != null)
				{
					ProjectPackage.Content = Services.ResourceHelper.GetString("Package");
					ProjectPackage.ToolTip = Services.ResourceHelper.GetString("PackageToolTip");
				}

				// Additional Arguments GroupBox
				if (AdditionalArgumentsGroupBox != null)
				{
					AdditionalArgumentsGroupBox.Header = Services.ResourceHelper.GetString("AdditionalArguments");
				}
				if (ProjectAdditionalArgs != null)
				{
					HandyControl.Controls.InfoElement.SetPlaceholder(ProjectAdditionalArgs, Services.ResourceHelper.GetString("AdditionalArgumentsPlaceholder"));
					ProjectAdditionalArgs.ToolTip = Services.ResourceHelper.GetString("AdditionalArgumentsToolTip");
				}

				// Buttons
				if (BuildProjectBtn != null)
				{
					BuildProjectBtn.Content = Services.ResourceHelper.GetString("BuildProject");
				}
				if (CopyProjectCommandLineBtn != null)
				{
					CopyProjectCommandLineBtn.Content = Services.ResourceHelper.GetString("CopyCommandLine");
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Error occurred while initializing project build UI");
			}
		}

		/// <summary>
		/// 订阅服务事件
		/// </summary>
		private void SubscribeToServiceEvents()
		{
			// 构建管理器事件
			BuildManager.BuildFinished += OnBuildFinished;

			// 进程管理器事件
			ProcessManager.OutputDataReceived += OnProcessOutputReceived;
			ProcessManager.ErrorDataReceived += OnProcessErrorReceived;
			ProcessManager.ProcessExited += OnProcessExited;

			// 更新管理器事件
			UpdateManager.UpdateCheckCompleted += OnUpdateCheckCompleted;
			UpdateManager.UpdateDownloadProgress += OnUpdateDownloadProgress;
			UpdateManager.UpdateDownloadFinished += OnUpdateDownloadFinished;
		}

		public static void OpenBrowser(string InURL)
		{
			InURL = InURL.Replace("&", "^&");
			Process.Start(new ProcessStartInfo("cmd", $"/c start {InURL}") { CreateNoWindow = true });
		}

		public void DownloadUpdate()
		{
			if (CurrentProcess == null)
			{
				if (bUpdateAvailable)
				{
					CheckUpdateBtn.IsEnabled = false;
					CheckUpdateBtn.Content = "Downloading...";
					unrealBinaryBuilderUpdater.UpdateDownloadStartedEventHandler += DownloadUpdateProgressStart;
					unrealBinaryBuilderUpdater.UpdateDownloadFinishedEventHandler += DownloadUpdateProgressFinish;
					unrealBinaryBuilderUpdater.UpdateProgressEventHandler += DownloadUpdateProgress;
					unrealBinaryBuilderUpdater.DownloadUpdate();
				}
			}
			else
			{
				CloseUpdateDialogWindow();
				ShowToastMessage($"{GetCurrentProcessName()} is currently running. You can check for updates after it is done.", LogViewer.EMessageType.Error);
			}
		}

		public void CloseUpdateDialogWindow()
		{
			if (downloadDialog != null)
			{
				downloadDialog.Close();
				downloadDialog = null;
				downloadDialogWindow = null;
			}
		}

		private void CheckForUpdates()
		{
			// 使用新的更新管理器
			if (!ProcessManager.IsProcessRunning && CurrentProcess == null)
			{
				CheckUpdateBtn.IsEnabled = false;
				CheckUpdateBtn.Content = "Checking...";
				GameAnalyticsCSharp.AddDesignEvent("Update:Check");
				UpdateManager.CheckForUpdatesSilently();
			}
			else
			{
				string processName = ProcessManager.GetCurrentProcessName() ?? GetCurrentProcessName() ?? "Process";
				string message = Services.ResourceHelper.GetString("WarningBuildInProgress", processName);
				if (string.IsNullOrEmpty(message))
				{
					message = $"{processName} is currently running. You can check for updates after it is done.";
				}
				NotificationService.ShowError(message);
			}
		}

		private void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
		{
			if (bUpdateAvailable)
			{
				DownloadUpdate();
			}
			else
			{
				CheckForUpdates();
			}
		}

		private void OnUpdateCheck(object sender, UpdateProgressFinishedEventArgs e)
		{
			CheckUpdateBtn.Content = "Check for Update";
			switch (e.appUpdateCheckStatus)
			{
				case AppUpdateCheckStatus.UpdateAvailable:
					bUpdateAvailable = true;
					CheckUpdateBtn.Content = $"Install Update {e.castItem.Version}";
					ShowToastMessage($"Update {e.castItem.Version} is available.", LogViewer.EMessageType.Info, true, false, "", 2);
					downloadDialogWindow = new DownloadDialog(this, e.castItem.Version);
					downloadDialog = Dialog.Show(downloadDialogWindow);
					break;
				case AppUpdateCheckStatus.NoUpdate:
					ShowToastMessage("You are running the latest version.", LogViewer.EMessageType.Info, true, false, "", 2);
					break;
				case AppUpdateCheckStatus.CouldNotDetermine:
					ShowToastMessage("Failed to determine update settings. Please try again later.", LogViewer.EMessageType.Error);
					break;
				case AppUpdateCheckStatus.UserSkip:
					break;
			}
			CheckUpdateBtn.IsEnabled = true;
		}

		private void DownloadUpdateProgressStart(object sender, UpdateProgressDownloadStartEventArgs e)
		{
			GameAnalyticsCSharp.AddDesignEvent($"Update:Download:{e.Version}");
			if (downloadDialogWindow == null)
			{
				downloadDialogWindow = new DownloadDialog(this, e.Version);
				downloadDialog = Dialog.Show(downloadDialogWindow);
			}
			downloadDialogWindow.Initialize(e.UpdateSize);
		}

		private void DownloadUpdateProgress(object sender, UpdateProgressDownloadEventArgs progressDownloadEventArgs)
		{
			downloadDialogWindow.SetProgress(progressDownloadEventArgs.AppUpdateProgress);
		}
		private void DownloadUpdateProgressFinish(object sender, UpdateProgressDownloadFinishEventArgs e)
		{
			string TargetDownloadDirectory = Path.Combine(BuilderSettings.PROGRAM_SAVED_PATH, "Updates", e.castItem.Version);
			if (Directory.Exists(TargetDownloadDirectory) == false)
			{
				Directory.CreateDirectory(TargetDownloadDirectory);
			}

			using (System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(e.UpdateFilePath))
			{
				foreach (System.IO.Compression.ZipArchiveEntry entry in archive.Entries)
				{
					string destinationPath = Path.Combine(TargetDownloadDirectory, entry.FullName);
					string destinationDir = Path.GetDirectoryName(destinationPath);
					
					if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
					{
						Directory.CreateDirectory(destinationDir);
					}
					
					if (!string.IsNullOrEmpty(entry.Name))
					{
						entry.ExtractToFile(destinationPath, overwrite: true);
					}
				}

				GameAnalyticsCSharp.AddDesignEvent($"Update:Install:{downloadDialogWindow.VersionText}");
				unrealBinaryBuilderUpdater.UpdateDownloadStartedEventHandler -= DownloadUpdateProgressStart;
				unrealBinaryBuilderUpdater.UpdateDownloadFinishedEventHandler -= DownloadUpdateProgressFinish;
				unrealBinaryBuilderUpdater.UpdateProgressEventHandler -= DownloadUpdateProgress;
				unrealBinaryBuilderUpdater.CloseApplicationEventHandler += CloseApplication;
				unrealBinaryBuilderUpdater.InstallUpdate();
				Process.Start("explorer.exe", TargetDownloadDirectory);
			}
		}

		private void CloseApplication(object sender, EventArgs e)
		{
			downloadDialog.Close();
			Close();
		}

		public void ShowToastMessage(string Message, LogViewer.EMessageType ToastType = LogViewer.EMessageType.Info, bool bShowCloseButton = true, bool bStaysOpen = false, string Token = "", int WaitTime = 3)
		{
			// 使用新的通知服务
			try
			{
				switch (ToastType)
				{
					case LogViewer.EMessageType.Info:
						NotificationService.ShowInfo(Message, bShowCloseButton, bStaysOpen, Token, WaitTime);
						break;
					case LogViewer.EMessageType.Warning:
						NotificationService.ShowWarning(Message, bShowCloseButton, bStaysOpen, Token, WaitTime);
						break;
					case LogViewer.EMessageType.Error:
						NotificationService.ShowError(Message, bShowCloseButton, bStaysOpen, Token, WaitTime);
						break;
				}
			}
			catch (Exception ex)
			{
				// 如果新服务失败，回退到旧方法
				Logger.LogException(ex, "显示通知时发生错误，使用旧方法");
				Growl.Clear(Token);
				GrowlInfo growlInfo = new GrowlInfo()
				{
					ShowDateTime = false,
					ShowCloseButton = bShowCloseButton,
					StaysOpen = bStaysOpen,
					Token = Token,
					WaitTime = WaitTime,
					Message = Message
				};

				switch (ToastType)
				{
					case LogViewer.EMessageType.Info:
						Growl.Info(growlInfo);
						break;
					case LogViewer.EMessageType.Warning:
						Growl.Warning(growlInfo);
						break;
					case LogViewer.EMessageType.Error:
						Growl.Error(growlInfo);
						break;
				}
			}
		}

		private void ChangeStatusLabel(string InStatus)
		{
			// 使用异步更新，避免阻塞 UI 线程
			Dispatcher.InvokeAsync(() =>
			{
				string processName = ProcessManager.GetCurrentProcessName() ?? GetCurrentProcessName();
				StatusLabel.Text = processName != null ? $"Status: Running [{processName} - {InStatus}]" : $"Status: {InStatus}";
				_viewModel.StatusText = StatusLabel.Text;
			});
		}

		private void ChangeStepLabel(string current, string total)
		{
			// 使用异步更新，避免阻塞
			Dispatcher.InvokeAsync(() => { StepLabel.Text = $"Step: [{current}/{total}] "; });
		}

		private string GetConditionalString(bool? bCondition)
		{
			return (bool)bCondition ? "true" : "false";
		}

		private void DispatchTimer_Tick(object sender, EventArgs e)
		{
			ChangeStatusLabel(string.Format("Building... Time Elapsed: {0:hh\\:mm\\:ss}", StopwatchTimer.Elapsed));
		}

		public void AddZipLog(string InMessage, ZipLogInclusionType InType)
		{
			LogEntry logEntry = new LogEntry();
			logEntry.Message = InMessage;
			LogControl.AddZipLog(logEntry, InType);
		}

		public void AddLogEntry(string InMessage, bool bIsError = false)
		{
			if (InMessage == null)
				return;

			// 使用新的日志服务
			if (bIsError)
			{
				Logger.LogError(InMessage);
			}
			else
			{
				Logger.LogInfo(InMessage);
			}

			// 使用新的日志解析器
			var parseResult = LogParser.ParseLogMessage(InMessage, bIsError);

			LogEntry logEntry = new LogEntry();
			logEntry.Message = InMessage;

			LogViewer.EMessageType InMessageType = parseResult.MessageType;

			// 处理步骤信息
			if (parseResult.IsStepInfo)
			{
				ChangeStepLabel(parseResult.StepCurrent.ToString(), parseResult.StepTotal.ToString());
				CompiledFiles = 0;
				_viewModel.CompiledFiles = 0;
			}

			// 处理编译文件
			if (parseResult.IsProcessedFile)
			{
				CompiledFiles++;
				CompiledFilesTotal++;
				_viewModel.CompiledFiles++;
				_viewModel.CompiledFilesTotal++;
				Dispatcher.InvokeAsync(() => 
				{ 
					ProcessedFilesLabel.Text = $"[Compiled: {CompiledFiles}. Total: {CompiledFilesTotal}]"; 
				});
			}

			// 更新错误和警告计数
			if (parseResult.IsWarning)
			{
				NumWarnings++;
				_viewModel.WarningCount++;
			}
			else if (parseResult.IsError)
			{
				NumErrors++;
				_viewModel.ErrorCount++;
				LogMessageErrors += InMessage + "\r\n";
			}

			// 添加到 UI 日志控件
			LogControl.AddLogEntry(logEntry, InMessageType);
			LogMessage += InMessage + "\r\n";
		}

		private void Internal_ShutdownWindows()
		{
			Process.Start("shutdown", "/s /t 5");
			Application.Current.Shutdown();
		}

		private void SaveAllSettings()
		{
			// 使用新的设置管理器
			try
			{
				SettingsManager.SaveSettings();
				// 同时保存到旧的位置（向后兼容）
				BuilderSettings.SaveSettings();
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "保存设置时发生错误");
				// 回退到旧方法
				BuilderSettings.SaveSettings();
			}
		}

		private void UnrealBinaryBuilderWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			// 使用新的构建管理器检查构建状态
			if (BuildManager.IsBuilding || bIsBuilding)
			{
				string processName = ProcessManager.GetCurrentProcessName() ?? GetCurrentProcessName() ?? "Process";
				if (currentProcessType == CurrentProcessType.BuildProject)
				{
					processName = "Project Build";
				}
				string question = Services.ResourceHelper.GetString("QuestionStopBuild", processName);
				if (string.IsNullOrEmpty(question))
				{
					question = $"{processName} is still running. Would you like to stop it and exit?";
				}

				if (HandyControl.Controls.MessageBox.Show(question, Services.ResourceHelper.GetString("TitleBuildInProgress", "Build in progress"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
				{
					GameAnalyticsCSharp.AddDesignEvent($"Build:{processName}:Killed:ExitProgram");
					BuildManager.StopBuild();
					CloseCurrentProcess(true);
				}
				else
				{
					e.Cancel = true;
					return;
				}
			}

			GameAnalyticsCSharp.EndSession();
			SaveAllSettings();

			// 释放服务资源
			_services?.Dispose();

			Application.Current.Shutdown();
		}

		private void CurrentProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
		{
			AddLogEntry(e.Data);
		}

		private void CurrentProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
		{
			NumErrors++;
			_viewModel.ErrorCount++;
			AddLogEntry(e.Data, true);
		}

		/// <summary>
		/// 进程输出数据接收事件（新服务）
		/// </summary>
		private void OnProcessOutputReceived(object sender, string data)
		{
			if (string.IsNullOrWhiteSpace(data))
				return;

			Dispatcher.InvokeAsync(() =>
			{
				AddLogEntry(data);
			});
		}

		/// <summary>
		/// 进程错误数据接收事件（新服务）
		/// </summary>
		private void OnProcessErrorReceived(object sender, string data)
		{
			if (string.IsNullOrWhiteSpace(data))
				return;

			Dispatcher.InvokeAsync(() =>
			{
				NumErrors++;
				_viewModel.ErrorCount++;
				AddLogEntry(data, true);
			});
		}

		/// <summary>
		/// 进程退出事件（新服务）
		/// </summary>
		private void OnProcessExited(object sender, ProcessExitedEventArgs e)
		{
			Dispatcher.InvokeAsync(() =>
			{
				Logger.LogInfo($"进程退出，退出代码: {e.ExitCode}");
				// 这里可以添加额外的处理逻辑
			});
		}

		/// <summary>
		/// 构建完成事件（新服务）
		/// </summary>
		private void OnBuildFinished(object sender, BuildFinishedEventArgs e)
		{
			Dispatcher.InvokeAsync(() =>
			{
				bIsBuilding = false;
				bLastBuildSuccess = e.Success;
				NumErrors = e.ErrorCount;
				NumWarnings = e.WarningCount;
				_viewModel.ErrorCount = e.ErrorCount;
				_viewModel.WarningCount = e.WarningCount;
				_viewModel.IsBuilding = false;

				string statusMessage = Services.ResourceHelper.GetString("StatusBuildFinished",
					e.ExitCode, e.ErrorCount, e.WarningCount, e.ElapsedTime.ToString(@"hh\:mm\:ss"));
				if (string.IsNullOrEmpty(statusMessage))
				{
					statusMessage = $"Build finished with code {e.ExitCode}. {e.ErrorCount} errors, {e.WarningCount} warnings. Time elapsed: {e.ElapsedTime:hh\\:mm\\:ss}";
				}
				ChangeStatusLabel(statusMessage);
				_viewModel.StatusText = statusMessage;

				AddLogEntry($"构建完成。成功: {e.Success}, 错误: {e.ErrorCount}, 警告: {e.WarningCount}");

				// 调用原有的构建完成处理逻辑
				OnBuildFinishedInternal(e.Success);
			});
		}

		/// <summary>
		/// 更新检查完成事件（新服务）
		/// </summary>
		private void OnUpdateCheckCompleted(object sender, UpdateCheckEventArgs e)
		{
			Dispatcher.InvokeAsync(() =>
			{
				switch (e.Status)
				{
					case UpdateCheckStatus.UpdateAvailable:
						bUpdateAvailable = true;
						CheckUpdateBtn.Content = $"Install Update {e.Version}";
						NotificationService.ShowInfo(e.Message);
						// 显示更新对话框
						downloadDialogWindow = new DownloadDialog(this, e.Version);
						downloadDialog = Dialog.Show(downloadDialogWindow);
						break;
					case UpdateCheckStatus.NoUpdate:
						NotificationService.ShowInfo(e.Message);
						break;
					case UpdateCheckStatus.Error:
						NotificationService.ShowError(e.Message);
						break;
				}
				CheckUpdateBtn.IsEnabled = true;
				CheckUpdateBtn.Content = "Check for Update";
			});
		}

		/// <summary>
		/// 更新下载进度事件（新服务）
		/// </summary>
		private void OnUpdateDownloadProgress(object sender, UpdateDownloadEventArgs e)
		{
			Dispatcher.InvokeAsync(() =>
			{
				if (downloadDialogWindow != null)
				{
					downloadDialogWindow.SetProgress((int)e.Progress);
				}
			});
		}

		/// <summary>
		/// 更新下载完成事件（新服务）
		/// </summary>
		private void OnUpdateDownloadFinished(object sender, EventArgs e)
		{
			Dispatcher.InvokeAsync(() =>
			{
				Logger.LogInfo("更新下载完成");
				// 原有的下载完成逻辑会在这里处理
			});
		}

		private void CurrentProcess_Exited(object sender, EventArgs e)
		{
			// 使用 ProcessManager 的事件处理器，这里保留作为回退
			DispatchTimer.Stop();
			StopwatchTimer.Stop();
			bLastBuildSuccess = CurrentProcess?.ExitCode == 0;
			string processName = ProcessManager.GetCurrentProcessName() ?? GetCurrentProcessName() ?? "进程";
			AddLogEntry(string.Format($"{processName} exited with code {0}\n", CurrentProcess?.ExitCode.ToString() ?? "0"));

			Dispatcher.InvokeAsync(() =>
			{
				BuildRocketUE.Content = "Build Unreal Engine";
				if (currentProcessType == CurrentProcessType.BuildProject)
				{
					BuildProjectBtn.Content = Services.ResourceHelper.GetString("BuildProject");
				}
				ChangeStatusLabel(string.Format("Build finished with code {0}. {1} errors, {2} warnings. Time elapsed: {3:hh\\:mm\\:ss}", CurrentProcess?.ExitCode ?? 0, NumErrors, NumWarnings, StopwatchTimer.Elapsed));
			});

			CloseCurrentProcess();

			NumErrors = 0;
			NumWarnings = 0;
			AddLogEntry("========================== BUILD FINISHED ==========================");
			AddLogEntry(string.Format("Compiled approximately {0} files.", CompiledFilesTotal));
			AddLogEntry(string.Format("Took {0:hh\\:mm\\:ss}", StopwatchTimer.Elapsed));
			AddLogEntry(string.Format("Build ended at {0}", DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss")));
			StopwatchTimer.Reset();
			Dispatcher.InvokeAsync(() =>
			{
				StartSetupBatFile.IsEnabled = true;
				StartPluginBuildsBtn.IsEnabled = true;
				if (currentProcessType == CurrentProcessType.BuildProject)
				{
					BuildProjectBtn.IsEnabled = true;
				}
				OnBuildFinishedInternal(bLastBuildSuccess); // 修复：调用内部方法而不是事件处理器
			});
		}

		/// <summary>
		/// 构建完成处理（内部方法，避免与事件处理器冲突）
		/// </summary>
		private void OnBuildFinishedInternal(bool bBuildSucess)
		{
			ZipStatusLabel.Content = "Idle";
			bIsBuilding = false;
			if (bBuildSucess)
			{
				switch (currentProcessType)
				{
					case CurrentProcessType.BuildUnrealEngine:
						if (postBuildSettings.CanSaveToZip())
						{
							EngineTabControl.SelectedIndex = 1;
							if (FinalBuildPath == null)
							{
								if (UnrealBinaryBuilderHelpers.IsUnrealEngine5)
								{
									FinalBuildPath = Path.GetFullPath(AutomationExePath).Replace(@$"\Engine\Binaries\DotNET\{UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME}", @"\LocalBuilds\Engine").Replace(Path.GetFileName(AutomationExePath), "");
								}
								else
								{
									FinalBuildPath = Path.GetFullPath(AutomationExePath).Replace(@"\Engine\Binaries\DotNET", @"\LocalBuilds\Engine").Replace(Path.GetFileName(AutomationExePath), "");
								}
								GameAnalyticsCSharp.LogEvent("Final Build Path was null. Fixed.", GameAnalyticsSDK.Net.EGAErrorSeverity.Info);
							}
							AddLogEntry($"Creating ZIP file. Installed build can be found in {FinalBuildPath}");
							postBuildSettings.PrepareToSave();
							postBuildSettings.SaveToZip(FinalBuildPath, ZipPath.Text);
							AddLogEntry($"Saving zip file to {ZipPath.Text}");
							WriteToLogFile();
							return;
						}
						break;
					case CurrentProcessType.SetupBat:
						GameAnalyticsCSharp.AddProgressEnd("Build", "Setup");
						GenerateProjectFiles();
						break;
					case CurrentProcessType.GenerateProjectFiles:
						GameAnalyticsCSharp.AddProgressEnd("Build", "ProjectFiles");
						BuildAutomationTool();
						break;
					case CurrentProcessType.BuildAutomationTool:
						GameAnalyticsCSharp.AddProgressEnd("Build", UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME);
						BuildAutomationToolLauncher();
						break;
					case CurrentProcessType.BuildAutomationToolLauncher:
						GameAnalyticsCSharp.AddProgressEnd("Build", UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_LAUNCHER_NAME);
						if (bContinueToEngineBuild.IsChecked == true)
						{
							BuildEngine();
						}
						break;
				}
			}

			if (currentProcessType == CurrentProcessType.BuildPlugin)
			{
				GameAnalyticsCSharp.AddProgressEnd("Build", "Plugin");
				CurrentPluginBeingBuilt.PluginFinishedBuild(bBuildSucess);
				CurrentPluginBeingBuilt = null;
				foreach (var C in PluginQueues.Children)
				{
					PluginCard pluginCard = (PluginCard)C;
					if (pluginCard.IsPending())
					{
						BuildPlugin(pluginCard);
						break;
					}
				}

				if (CurrentPluginBeingBuilt == null)
				{
					Growl.Clear("PluginBuild");
					ShowToastMessage($"Finished plugin queue build with {PluginQueues.Children.Count} plugin(s)");
				}
			}

			if (currentProcessType == CurrentProcessType.BuildProject)
			{
				GameAnalyticsCSharp.AddProgressEnd("Build", "Project");
				BuildProjectBtn.Content = Services.ResourceHelper.GetString("BuildProject");
				if (bBuildSucess)
				{
					NotificationService.ShowInfo(Services.ResourceHelper.GetString("MessageProjectBuildCompleted"));
					AddLogEntry(Services.ResourceHelper.GetString("MessageProjectBuildCompletedSuccess"));
				}
				else
				{
					NotificationService.ShowError(Services.ResourceHelper.GetString("ErrorProjectBuildFailed"));
					AddLogEntry(Services.ResourceHelper.GetString("ErrorProjectBuildFailedLog"), true);
				}
			}

			WriteToLogFile();
			TryShutdown();
			LogMessageErrors = null;
		}

		public void TryShutdown()
		{
			if (currentProcessType == CurrentProcessType.BuildUnrealEngine)
			{
				if (bShutdownWindows.IsChecked == true)
				{
					if (bShutdownIfSuccess.IsChecked == true)
					{
						if (bLastBuildSuccess)
						{
							GameAnalyticsCSharp.AddDesignEvent("Shutdown:BuildState:Success");
							Internal_ShutdownWindows();
						}
						else
						{
							GameAnalyticsCSharp.AddDesignEvent("Shutdown:BuildState:Failed");
						}
					}
					else
					{
						GameAnalyticsCSharp.AddDesignEvent("Shutdown:Started");
						Internal_ShutdownWindows();
					}
				}
			}
		}

		private void BrowseEngineFolder_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.FolderBrowserDialog NewFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
			NewFolderDialog.ShowDialog();
			SetupBatFilePath.Text = NewFolderDialog.SelectedPath;
			if (TryUpdateAutomationExePath() == false)
			{
				HandyControl.Controls.MessageBox.Error($"This is not the Unreal Engine root folder.\n\nPlease select the root folder where {UnrealBinaryBuilderHelpers.SetupBatFileName} and {UnrealBinaryBuilderHelpers.GenerateProjectBatFileName} exists.", "Incorrect folder");
			}
		}

		private bool TryUpdateAutomationExePath()
		{
			if (string.IsNullOrWhiteSpace(SetupBatFilePath.Text))
			{
				return false;
			}

			bool bRequiredFilesExist = File.Exists(Path.Combine(SetupBatFilePath.Text, UnrealBinaryBuilderHelpers.SetupBatFileName)) && 
			                           File.Exists(Path.Combine(SetupBatFilePath.Text, UnrealBinaryBuilderHelpers.GenerateProjectBatFileName));
			StartSetupBatFile.IsEnabled = bRequiredFilesExist;
			if (bRequiredFilesExist)
			{
				string resolvedAutomationExe = UnrealBinaryBuilderHelpers.GetAutomationToolExecutablePath(SetupBatFilePath.Text);
				if (string.IsNullOrEmpty(resolvedAutomationExe))
				{
					resolvedAutomationExe = UnrealBinaryBuilderHelpers.IsUnrealEngine5
						? Path.Combine(SetupBatFilePath.Text, "Engine", "Binaries", "DotNET", UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME, $"{UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME}.exe")
						: Path.Combine(SetupBatFilePath.Text, "Engine", "Binaries", "DotNET", $"{UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_LAUNCHER_NAME}.exe");
				}

				AutomationExePath = resolvedAutomationExe;
			}
			else
			{
				AutomationExePath = null;
			}

			UpdateCompilerOptions();

			return bRequiredFilesExist;
		}

		private string SetupBatCommandLineArgs()
		{
			// 使用新的命令行构建器
			try
			{
				return CommandLineBuilder.BuildSetupCommandLine(SettingsJSON);
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "构建 Setup.bat 命令行时发生错误");
				// 回退到旧方法
				return SetupBatCommandLineArgsLegacy();
			}
		}

		/// <summary>
		/// 旧版 Setup.bat 命令行构建方法（作为回退）
		/// </summary>
		private string SetupBatCommandLineArgsLegacy()
		{
			string CommandLines = "--force";

			if (SettingsJSON.GitDependencyAll)
			{
				CommandLines += " --all";
			}

			foreach (GitPlatform gp in SettingsJSON.GitDependencyPlatforms)
			{
				if (!gp.bIsIncluded)
				{
					CommandLines += $" --exclude={gp.Name}";
				}
			}

			CommandLines += $" --threads={SettingsJSON.GitDependencyThreads}";
			CommandLines += $" --max-retries={SettingsJSON.GitDependencyMaxRetries}";
			
			if (!SettingsJSON.GitDependencyEnableCache)
			{
				CommandLines += " --no-cache";
			}
			else if (!string.IsNullOrEmpty(SettingsJSON.GitDependencyCache))
			{
				CommandLines += $" --cache={SettingsJSON.GitDependencyCache.Replace("\\", "/")}";
				CommandLines += $" --cache-size-multiplier={SettingsJSON.GitDependencyCacheMultiplier}";
				CommandLines += $" --cache-days={SettingsJSON.GitDependencyCacheDays}";
			}

			if (!string.IsNullOrEmpty(SettingsJSON.GitDependencyProxy))
			{
				CommandLines += $" --proxy={SettingsJSON.GitDependencyProxy}";
			}

			return CommandLines;
		}

		private void StartSetupBatFile_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			// 使用新的服务优化启动逻辑
			try
			{
				if (string.IsNullOrWhiteSpace(SetupBatFilePath.Text))
				{
					Logger.LogError("引擎路径不能为空");
					NotificationService.ShowError("引擎路径不能为空");
					return;
				}

				string setupBatPath = Path.Combine(SetupBatFilePath.Text, UnrealBinaryBuilderHelpers.SetupBatFileName);
				string generateProjectBatPath = Path.Combine(SetupBatFilePath.Text, UnrealBinaryBuilderHelpers.GenerateProjectBatFileName);
				bool bRequiredFilesExist = File.Exists(setupBatPath) && File.Exists(generateProjectBatPath);
				
				if (bRequiredFilesExist == false)
				{
					string errorMsg = $"This is not the Unreal Engine root folder.\n\nPlease select the root folder where {UnrealBinaryBuilderHelpers.SetupBatFileName} and {UnrealBinaryBuilderHelpers.GenerateProjectBatFileName} exists.";
					Logger.LogError(errorMsg);
					HandyControl.Controls.MessageBox.Error(errorMsg, "Incorrect folder");
					return;
				}

				if (BuildManager.IsBuilding || bIsBuilding)
				{
					Logger.LogWarning("构建已在进行中，无法启动新的任务");
					NotificationService.ShowWarning("构建已在进行中，无法启动新的任务");
					return;
				}

				if (bBuildSetupBatFile.IsChecked == true)
				{
					bIsBuilding = true;
					_viewModel.IsBuilding = true;
					string Commandline = SetupBatCommandLineArgs();
					ProcessStartInfo processStartInfo = new ProcessStartInfo
					{
						FileName = setupBatPath,
						Arguments = Commandline,
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardError = true,
						RedirectStandardOutput = true
					};

					currentProcessType = CurrentProcessType.SetupBat;
					Logger.LogInfo($"开始执行 Setup.bat，命令行: {Commandline}");
					CreateProcess(processStartInfo);
					AddLogEntry($"Commandline: {Commandline}");
					ChangeStatusLabel("Building...");
					GameAnalyticsCSharp.AddProgressStart("Build", "Setup");
				}
				else if (bGenerateProjectFiles.IsChecked == true)
				{
					GenerateProjectFiles();
				}
				else if (bBuildAutomationTool.IsChecked == true)
				{
					BuildAutomationTool();
				}
				else
				{
					BuildEngine();
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "启动构建任务时发生错误");
				ErrorHandler.HandleError(ex, "启动构建任务失败");
				bIsBuilding = false;
				_viewModel.IsBuilding = false;
			}
		}

		private void CreateProcess(ProcessStartInfo processStartInfo, bool bClearLogs = true)
		{
			// 使用新的进程管理器
			try
			{
				if (!File.Exists(processStartInfo.FileName))
				{
					string errorMessage = $"File does not exist: {processStartInfo.FileName}";
					AddLogEntry(errorMessage, true);
					Logger.LogError(errorMessage);
					NotificationService.ShowError($"File does not exist: {Path.GetFileName(processStartInfo.FileName)}");
					return;
				}

				StartSetupBatFile.IsEnabled = false;
				DispatchTimer.Start();
				StopwatchTimer.Start();

				CompiledFiles = CompiledFilesTotal = 0;
				_viewModel.CompiledFiles = 0;
				_viewModel.CompiledFilesTotal = 0;
				ProcessedFilesLabel.Text = "[Compiled: 0. Total: 0]";

				if (bClearLogs)
				{
					LogControl.ClearAllLogs();
					string version = UnrealBinaryBuilderHelpers.GetProductVersionString();
					string welcomeMessage = Services.ResourceHelper.GetString("WelcomeMessage", version);
					if (string.IsNullOrEmpty(welcomeMessage))
					{
						welcomeMessage = $"Welcome to Unreal Binary Builder v{version}";
					}
					AddLogEntry(welcomeMessage);
				}

				AddLogEntry($"========================== RUNNING - {Path.GetFileName(processStartInfo.FileName)} ==========================");
				Logger.LogInfo($"启动进程: {processStartInfo.FileName}");

				// 使用新的进程管理器
				bool started = ProcessManager.StartProcess(processStartInfo);
				if (started)
				{
					// 保持 CurrentProcess 引用以便向后兼容
					CurrentProcess = ProcessManager.CurrentProcess;
				}
				else
				{
					Logger.LogError("启动进程失败");
					StartSetupBatFile.IsEnabled = true;
					DispatchTimer.Stop();
					StopwatchTimer.Stop();
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "创建进程时发生错误");
				// 回退到旧方法
				CreateProcessLegacy(processStartInfo, bClearLogs);
			}
		}

		/// <summary>
		/// 旧版进程创建方法（作为回退）
		/// </summary>
		private void CreateProcessLegacy(ProcessStartInfo processStartInfo, bool bClearLogs = true)
		{
			if (!File.Exists(processStartInfo.FileName))
			{
				AddLogEntry($"File does not exist: {processStartInfo.FileName}", true);
				ShowToastMessage($"File does not exist: {Path.GetFileName(processStartInfo.FileName)}", LogViewer.EMessageType.Error);
				return;
			}

			StartSetupBatFile.IsEnabled = false;
			DispatchTimer.Start();
			StopwatchTimer.Start();

			CompiledFiles = CompiledFilesTotal = 0;
			ProcessedFilesLabel.Text = "[Compiled: 0. Total: 0]";

			if (bClearLogs)
			{
				LogControl.ClearAllLogs();
				AddLogEntry($"Welcome to Unreal Binary Builder v{UnrealBinaryBuilderHelpers.GetProductVersionString()}");
			}

			AddLogEntry($"========================== RUNNING - {Path.GetFileName(processStartInfo.FileName)} ==========================");

			CurrentProcess = new Process();
			CurrentProcess.StartInfo = processStartInfo;
			CurrentProcess.EnableRaisingEvents = true;
			CurrentProcess.OutputDataReceived += CurrentProcess_OutputDataReceived;
			CurrentProcess.ErrorDataReceived += CurrentProcess_ErrorDataReceived;
			CurrentProcess.Exited += CurrentProcess_Exited;
			CurrentProcess.Start();
			CurrentProcess.BeginErrorReadLine();
			CurrentProcess.BeginOutputReadLine();
		}

		private void CloseCurrentProcess(bool bKillProcess = false)
		{
			// 使用新的进程管理器
			try
			{
				if (ProcessManager.IsProcessRunning)
				{
					ProcessManager.CloseProcess(bKillProcess);
					Logger.LogInfo(bKillProcess ? "进程已被强制终止" : "进程已关闭");
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "关闭进程时发生错误");
			}

			// 同时处理旧的 CurrentProcess（向后兼容）
			if (CurrentProcess != null)
			{
				try
				{
					if (bKillProcess)
					{
						CurrentProcess.Kill(true);
					}
					else
					{
						if (!CurrentProcess.HasExited)
						{
							CurrentProcess.Close();
						}
					}
				}
				finally
				{
					CurrentProcess?.Dispose();
					CurrentProcess = null;
				}
			}
		}

		private string GetCurrentProcessName()
		{
			// 优先使用新的进程管理器
			if (ProcessManager.IsProcessRunning)
			{
				return ProcessManager.GetCurrentProcessName();
			}

			// 回退到旧方法
			if (CurrentProcess != null)
			{
				return CurrentProcess.ProcessName;
			}

			return null;
		}

		private void WriteToLogFile()
		{
			BuilderSettings.WriteToLogFile(LogMessage);
			BuilderSettings.WriteErrorsToLogFile(LogMessageErrors);
		}

		private void UpdateSkin(SkinType skin)
		{
			SharedResourceDictionary.SharedDictionaries.Clear();
			Resources.MergedDictionaries.Add(HandyControl.Tools.ResourceHelper.GetSkin(skin));
			Resources.MergedDictionaries.Add(new ResourceDictionary
			{
				Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Theme.xaml")
			});
			Application.Current.MainWindow?.OnApplyTemplate();
			GameAnalyticsCSharp.AddDesignEvent($"Theme:{skin.ToString()}");
		}

		private void CustomBuildXMLBrowse_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog NewFileDialog = new OpenFileDialog
			{
				Filter = "xml file (*.xml)|*.xml"
			};

			ChangeStatusLabel("Waiting for custom build file...");
			if (NewFileDialog.ShowDialog() == true)
			{
				CustomBuildXMLFile.Text = NewFileDialog.FileName;
				CustomOptions.IsEnabled = true;
				GameAnalyticsCSharp.AddDesignEvent($"BuildXML:Custom:{NewFileDialog.FileName}");
			}

			ChangeStatusLabel("Idle.");
		}

		private void ResetDefaultBuildXML_Click(object sender, RoutedEventArgs e)
		{
			CustomBuildXMLFile.Text = UnrealBinaryBuilderHelpers.DEFAULT_BUILD_XML_FILE;
			GameAnalyticsCSharp.AddDesignEvent("BuildXML:ResetToDefault");
		}

		private string PrepareCommandline()
		{
			// 使用新的命令行构建器
			try
			{
				string buildXmlFile = CustomBuildXMLFile.Text;
				if (string.IsNullOrEmpty(buildXmlFile))
				{
					buildXmlFile = UnrealBinaryBuilderHelpers.DEFAULT_BUILD_XML_FILE;
				}

				if (GameConfigurations.Text == "")
				{
					GameConfigurations.Text = "Development;Shipping";
					GameAnalyticsCSharp.AddDesignEvent("CommandLine:GameConfiguration:Reset");
				}

				string commandLine = CommandLineBuilder.BuildEngineCommandLine(
					SettingsJSON,
					buildXmlFile,
					GameConfigurations.Text,
					CustomOptions.Text,
					AnalyticsOverride.Text,
					SetupBatFilePath.Text
				);

				Logger.LogDebug($"构建的命令行: {commandLine}");
				return commandLine;
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "构建命令行时发生错误");
				// 回退到旧方法
				return PrepareCommandlineLegacy();
			}
		}

		/// <summary>
		/// 旧版命令行构建方法（作为回退）
		/// </summary>
		private string PrepareCommandlineLegacy()
		{
			string BuildXMLFile = CustomBuildXMLFile.Text;
			if (CustomBuildXMLFile.Text == "")
			{
				BuildXMLFile = UnrealBinaryBuilderHelpers.DEFAULT_BUILD_XML_FILE;
			}

			if (BuildXMLFile != UnrealBinaryBuilderHelpers.DEFAULT_BUILD_XML_FILE)
			{
				BuildXMLFile = string.Format("\"{0}\"", CustomBuildXMLFile.Text);
			}

			if (GameConfigurations.Text == "")
			{
				GameConfigurations.Text = "Development;Shipping";
				GameAnalyticsCSharp.AddDesignEvent("CommandLine:GameConfiguration:Reset");
			}

			string CommandLineArgs = string.Format("BuildGraph -target=\"Make Installed Build Win64\" -script={0} -set:WithDDC={1} -set:SignExecutables={2} -set:EmbedSrcSrvInfo={3} -set:GameConfigurations={4} -set:WithFullDebugInfo={5} -set:HostPlatformEditorOnly={6} -set:AnalyticsTypeOverride={7}",
				BuildXMLFile,
				GetConditionalString(bWithDDC.IsChecked),
				GetConditionalString(bSignExecutables.IsChecked),
				GetConditionalString(bEnableSymStore.IsChecked),
				GameConfigurations.Text,
				GetConditionalString(bWithFullDebugInfo.IsChecked),
				GetConditionalString(bHostPlatformEditorOnly.IsChecked),
				AnalyticsOverride.Text);

			if (bWithDDC.IsChecked == true && bHostPlatformDDCOnly.IsChecked == true)
			{
				CommandLineArgs += " -set:HostPlatformDDCOnly=true";
			}

			if (bHostPlatformOnly.IsChecked == true)
			{
				CommandLineArgs += " -set:HostPlatformOnly=true";
				GameAnalyticsCSharp.AddDesignEvent("CommandLine:HostOnly");
			}
			else
			{
				if (SupportWin32)
				{
					CommandLineArgs += $" -set:WithWin32={GetConditionalString(bWithWin32.IsChecked)}";
				}

				CommandLineArgs += $" -set:WithWin64={GetConditionalString(bWithWin64.IsChecked)} -set:WithMac={GetConditionalString(bWithMac.IsChecked)} -set:WithAndroid={GetConditionalString(bWithAndroid.IsChecked)} -set:WithIOS={GetConditionalString(bWithIOS.IsChecked)} -set:WithTVOS={GetConditionalString(bWithTVOS.IsChecked)} -set:WithLinux={GetConditionalString(bWithLinux.IsChecked)} -set:WithLumin={GetConditionalString(bWithLumin.IsChecked)}";

				if (SupportHTML5)
				{
					CommandLineArgs += $" -set:WithHTML5={GetConditionalString(bWithHTML5.IsChecked)}";
				}

				if (SupportConsoles)
				{
					CommandLineArgs += $" -set:WithSwitch={GetConditionalString(bWithSwitch.IsChecked)} -set:WithPS4={GetConditionalString(bWithPS4.IsChecked)} -set:WithXboxOne={GetConditionalString(bWithXboxOne.IsChecked)}";
				}

				if (SupportLinuxArm64)
				{
					CommandLineArgs += $" -set:WithLinuxArm64={GetConditionalString(bWithLinuxAArch64.IsChecked)}";
				}
				else if (SupportLinuxAArch64)
				{
					CommandLineArgs += $" -set:WithLinuxAArch64={GetConditionalString(bWithLinuxAArch64.IsChecked)}";
				}
			}

			if (IsEngineSelection425OrAbove)
			{
				CommandLineArgs += $" -set:CompileDatasmithPlugins={GetConditionalString(bCompileDatasmithPlugins.IsChecked)}";
			}

			bool useVS2022 = SupportVisualStudio2022 && bVS2022.IsChecked == true;
			bool useVS2019 = SupportVisualStudio2019 && bVS2019.IsChecked == true && !useVS2022;

			if (SupportVisualStudio2022)
			{
				CommandLineArgs += $" -set:VS2022={GetConditionalString(useVS2022)}";
			}

			if (SupportVisualStudio2019)
			{
				CommandLineArgs += $" -set:VS2019={GetConditionalString(useVS2019)}";
			}

			if (SupportServerClientTargets)
			{
				CommandLineArgs += $" -set:WithServer={GetConditionalString(bWithServer.IsChecked)} -set:WithClient={GetConditionalString(bWithClient.IsChecked)} -set:WithHoloLens={GetConditionalString(bWithHololens.IsChecked)}";
			}

			if (BuildXMLFile != UnrealBinaryBuilderHelpers.DEFAULT_BUILD_XML_FILE && !string.IsNullOrEmpty(CustomOptions.Text))
			{
				CommandLineArgs += $" {CustomOptions.Text}";
				AddLogEntry("Using custom options...");
				GameAnalyticsCSharp.AddDesignEvent("CommandLine:UsingCustomOptions");
			}

			if (bCleanBuild.IsChecked == true)
			{
				CommandLineArgs += " -Clean";
				GameAnalyticsCSharp.AddDesignEvent("CommandLine:CleanEnabled");
			}

			return CommandLineArgs;
		}

		private void BuildRocketUE_Click(object sender, RoutedEventArgs e)
		{
			BuildEngine();
		}

		private async void BuildEngine()
		{
			// 使用新的构建管理器
			if (BuildManager.IsBuilding || bIsBuilding)
			{
				MessageBoxResult MessageResult;
				switch (currentProcessType)
				{
					case CurrentProcessType.SetupBat:
					case CurrentProcessType.GenerateProjectFiles:
						MessageResult = HandyControl.Controls.MessageBox.Show("Automation tool is currently running. Would you like to stop it and start building the Engine?\n\nPress Yes to force stop Automation Tool and begin Engine Build.\nPress No to continue current process.", "Automation Tool Running!", MessageBoxButton.YesNo, MessageBoxImage.Question);
						switch (MessageResult)
						{
							case MessageBoxResult.Yes:
								GameAnalyticsCSharp.AddDesignEvent($"Build:{UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME}:Killed");
								BuildManager.StopBuild();
								CloseCurrentProcess(true);
								break;
							case MessageBoxResult.No:
								return;
						}
						break;
					case CurrentProcessType.BuildUnrealEngine:
						MessageResult = HandyControl.Controls.MessageBox.Show("Unreal Engine is being compiled right now. Do you want to stop it?", "Compiling Engine", MessageBoxButton.YesNo, MessageBoxImage.Question);
						if (MessageResult == MessageBoxResult.Yes)
						{
							GameAnalyticsCSharp.AddDesignEvent($"Build:{UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME}:UnrealEngine:Killed");
							BuildManager.StopBuild();
							CloseCurrentProcess(true);
						}
						return;
				}
			}

			TryUpdateAutomationExePath();
			EngineTabControl.SelectedIndex = 2;
			currentProcessType = CurrentProcessType.BuildUnrealEngine;
			bLastBuildSuccess = false;

			if (FinalBuildPath == null && string.IsNullOrWhiteSpace(AutomationExePath) == false)
			{
				if (UnrealBinaryBuilderHelpers.IsUnrealEngine5)
				{
					FinalBuildPath = Path.GetFullPath(AutomationExePath).Replace(@$"\Engine\Binaries\DotNET\{UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME}", @"\LocalBuilds\Engine").Replace(Path.GetFileName(AutomationExePath), "");
				}
				else
				{
					FinalBuildPath = Path.GetFullPath(AutomationExePath).Replace(@"\Engine\Binaries\DotNET", @"\LocalBuilds\Engine").Replace(Path.GetFileName(AutomationExePath), "");
				}
			}

			if (Directory.Exists(FinalBuildPath))
			{
				MessageBoxResult MessageResult = HandyControl.Controls.MessageBox.Show($"Looks like an Engine build is already available at {FinalBuildPath}. Would you like to skip compiling the Engine and start zipping the existing build?\n\nPress Yes to Skip Engine build and start zipping (if enabled).\nPress No to continue with Engine Build.\nPress Cancel to do nothing.", "Zip Binary Version", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
				switch (MessageResult)
				{
					case MessageBoxResult.Yes:
						GameAnalyticsCSharp.AddDesignEvent("Build:EngineExists:FinishBuild");
						// We don't want the system to shutdown since user is interacting.
						bool? bOriginalShutdownState = bShutdownWindows.IsChecked;
						bShutdownWindows.IsChecked = false;
						OnBuildFinishedInternal(true);
						bShutdownWindows.IsChecked = bOriginalShutdownState;
						return;
					case MessageBoxResult.Cancel:
						GameAnalyticsCSharp.AddDesignEvent("Build:EngineExists:Exit");
						return;
					default:
						GameAnalyticsCSharp.AddDesignEvent("Build:EngineExists:IgnoreAndContinue");
						break;
				}
			}

			ChangeStatusLabel("Preparing to build...");

			if (postBuildSettings.ShouldSaveToZip() && postBuildSettings.DirectoryIsWritable(Path.GetDirectoryName(ZipPath.Text)) == false)
			{
				GameAnalyticsCSharp.AddDesignEvent("Build:ZipEnabled:InvalidSetting");
				HandyControl.Controls.MessageBox.Error(string.Format("You chose to save Engine build as a zip file but below directory is either not available or not writable.\n\n{0}", ZipPath.Text), "Error");
				return;
			}

			if (string.IsNullOrEmpty(CustomBuildXMLFile.Text))
			{
				CustomBuildXMLFile.Text = UnrealBinaryBuilderHelpers.DEFAULT_BUILD_XML_FILE;
			}
			else if (CustomBuildXMLFile.Text != UnrealBinaryBuilderHelpers.DEFAULT_BUILD_XML_FILE)
			{
				if (!File.Exists(CustomBuildXMLFile.Text))
				{
					GameAnalyticsCSharp.LogEvent("BuildXML does not exist.", GameAnalyticsSDK.Net.EGAErrorSeverity.Error);
					ChangeStatusLabel("Error. Build xml does not exist.");
					HandyControl.Controls.MessageBox.Error($"Build XML {CustomBuildXMLFile.Text} does not exist!", "Error");
					return;
				}
			}

			if (!SupportHTML5 && bWithHTML5.IsChecked == true)
			{
				GameAnalyticsCSharp.AddDesignEvent($"Build:HTML5:IncorrectEngine:{GetEngineName()}");
				bWithHTML5.IsChecked = false;
				if (SettingsJSON.bShowHTML5DeprecatedMessage)
				{
					HandyControl.Controls.MessageBox.Show("HTML5 support was removed from Unreal Engine 4.24 and higher. You had it enabled but since it is of no use, it is disabled.");
				}
			}

			if (!SupportConsoles && (bWithSwitch.IsChecked == true || bWithPS4.IsChecked == true || bWithXboxOne.IsChecked == true))
			{
				GameAnalyticsCSharp.AddDesignEvent($"Build:Console:IncorrectEngine:{GetEngineName()}");
				bWithSwitch.IsChecked = bWithPS4.IsChecked = bWithXboxOne.IsChecked = false;
				if (SettingsJSON.bShowConsoleDeprecatedMessage)
				{
					HandyControl.Controls.MessageBox.Show("Console support was removed from Unreal Engine 4.25 and higher. You had it enabled but since it is of no use, it is disabled.");
				}
			}

			bool bContinueToBuild = true;
			if (SettingsJSON.bEnableEngineBuildConfirmationMessage)
			{
				bContinueToBuild = HandyControl.Controls.MessageBox.Show("You are going to build a binary version of Unreal Engine 4. This is a long process and might take time to finish. Are you sure you want to continue? ", "Build Binary Version", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
			}

			if (!bContinueToBuild)
			{
				return;
			}

			if (bWithDDC.IsChecked == true && SettingsJSON.bEnableDDCMessages)
			{
				MessageBoxResult MessageResult = HandyControl.Controls.MessageBox.Show("Building Derived Data Cache (DDC) is one of the slowest aspect of the build. You can skip this step if you want to. Do you want to continue with DDC enabled?\n\nPress Yes to continue with build\nPress No to continue without DDC\nPress Cancel to stop build", "Warning", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

				switch (MessageResult)
				{
					case MessageBoxResult.No:
						bWithDDC.IsChecked = false;
						GameAnalyticsCSharp.AddDesignEvent("Build:DDC:AutoDisabled");
						break;
					case MessageBoxResult.Cancel:
						GameAnalyticsCSharp.AddDesignEvent("Build:DDC:Exit");
						return;
					default:
						GameAnalyticsCSharp.AddDesignEvent("Build:DDC:IgnoreAndContinue");
						break;
				}
			}

			GameAnalyticsCSharp.AddDesignEvent($"Build:Engine:{GetEngineName()}");
			BuildRocketUE.Content = "Stop Build";
			BuildRocketUE.IsEnabled = true;

			// 使用新的命令行构建器
			string CommandLineArgs = PrepareCommandline();

			// 使用新的构建管理器
			try
			{
				// 开始性能监控
				using (PerformanceMonitor.StartOperation("BuildEngine"))
				{
					_viewModel.IsBuilding = true;
					_viewModel.ResetCounters();
					bIsBuilding = true;

					bool success = await BuildManager.BuildEngineAsync(AutomationExePath, CommandLineArgs);
					if (success)
					{
						ChangeStatusLabel("Building...");
						_viewModel.StatusText = Services.ResourceHelper.GetString("StatusBuilding", "Building...");
						ZipStatusLabel.Content = "Waiting for Engine to finish building...";
						GameAnalyticsCSharp.AddDesignEvent("Build:Started");
						Logger.LogInfo(Services.ResourceHelper.GetString("MessageBuildStarted", "Build started"));

						if (Git.CommitHashShort != null)
						{
							AddLogEntry($"Building commit {Git.CommitHashShort}");
						}
					}
					else
					{
						Logger.LogError("启动构建失败");
						bIsBuilding = false;
						_viewModel.IsBuilding = false;
						BuildRocketUE.Content = "Build Unreal Engine";
						NotificationService.ShowError("启动构建失败");
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "构建引擎时发生错误");
				ErrorHandler.HandleError(ex, "构建引擎失败");
				bIsBuilding = false;
				_viewModel.IsBuilding = false;
				BuildRocketUE.Content = "Build Unreal Engine";
				NotificationService.ShowError("构建引擎时发生错误");
			}
		}

		private bool IsUnrealEngine4()
		{
			return GetEngineValue() < 5;
		}

		public bool SupportServerClientTargets => GetEngineValue() > 4.22;

		public bool SupportWin32 => IsUnrealEngine4();

		public bool SupportHTML5 => GetEngineValue() < 4.24;

		public bool SupportLinuxAArch64 => IsUnrealEngine4() && GetEngineValue() >= 4.24;

		public bool SupportLinuxArm64 => IsUnrealEngine4() == false;

		public bool SupportConsoles => GetEngineValue() <= 4.24;

		public bool SupportVisualStudio2019
		{
			get
			{
				double engineValue = GetEngineValue();
				bool isUE4WithSupport = IsUnrealEngine4() && IsEngineSelection425OrAbove && UnrealBinaryBuilderHelpers.VisualStudio2019Available;
				bool isUE5Fallback = engineValue >= 5.0 && UnrealBinaryBuilderHelpers.VisualStudio2022Available == false && UnrealBinaryBuilderHelpers.VisualStudio2019Available;
				return isUE4WithSupport || isUE5Fallback;
			}
		}

		public bool SupportVisualStudio2022 => GetEngineValue() >= 4.27 && UnrealBinaryBuilderHelpers.VisualStudio2022Available;

		private void InitializeVisualStudioPreferences()
		{
			// Force a detection so availability flags are populated.
			UnrealBinaryBuilderHelpers.GetMsBuildPath();
			UpdateCompilerOptions();
			UpdatePluginCompilerOptions();
		}

		private void UpdateCompilerOptions()
		{
			if (bVS2019 == null || bVS2022 == null)
			{
				return;
			}

			bool supportVS2022 = SupportVisualStudio2022;
			bool supportVS2019 = SupportVisualStudio2019;

			bVS2022.IsEnabled = supportVS2022;
			bVS2019.IsEnabled = supportVS2019;

			if (GetEngineValue() >= 4.27 && UnrealBinaryBuilderHelpers.VisualStudio2022Available == false && bMissingVS2022WarningShown == false)
			{
				bMissingVS2022WarningShown = true;
				if (UnrealBinaryBuilderHelpers.VisualStudio2019Available)
				{
					ShowToastMessage("未检测到 Visual Studio 2022。将尝试使用 Visual Studio 2019 继续编译。", LogViewer.EMessageType.Warning);
				}
				else
				{
					ShowToastMessage("未检测到 Visual Studio 2022 或 Visual Studio 2019。请安装受支持的编译器后重试。", LogViewer.EMessageType.Error);
				}
			}

			if (supportVS2022 == false && bVS2022.IsChecked == true)
			{
				bVS2022.IsChecked = false;
			}

			if (supportVS2019 == false && bVS2019.IsChecked == true)
			{
				bVS2019.IsChecked = false;
			}

			if (supportVS2022 && bVS2022.IsChecked != true && bVS2019.IsChecked != true)
			{
				bVS2022.IsChecked = true;
			}
			else if (supportVS2019 && bVS2019.IsChecked != true && bVS2022.IsChecked != true)
			{
				bVS2019.IsChecked = true;
			}
		}

		private void UpdatePluginCompilerOptions()
		{
			if (PluginEngineVersionSelection == null || bUse2019Compiler == null || bUse2022Compiler == null)
			{
				return;
			}

			double engineValue = GetSelectedPluginEngineValue();

			bool isUE427OrAbove = engineValue >= 4.27;
			bool isUE5OrAbove = engineValue >= 5.0;
			bool support2022 = isUE427OrAbove && UnrealBinaryBuilderHelpers.VisualStudio2022Available;
			bool support2019 = (engineValue >= 4.25 && engineValue < 5.0 && UnrealBinaryBuilderHelpers.VisualStudio2019Available) ||
							   (isUE427OrAbove && support2022 == false && UnrealBinaryBuilderHelpers.VisualStudio2019Available);

			bUse2019Compiler.IsEnabled = support2019;
			bUse2022Compiler.IsEnabled = support2022;

			if (support2022 == false && bUse2022Compiler.IsChecked == true)
			{
				bUse2022Compiler.IsChecked = false;
			}

			if (support2019 == false && bUse2019Compiler.IsChecked == true)
			{
				bUse2019Compiler.IsChecked = false;
			}

			if (support2022 && bUse2022Compiler.IsChecked != true && bUse2019Compiler.IsChecked != true)
			{
				bUse2022Compiler.IsChecked = true;
			}
			else if (support2019 && bUse2019Compiler.IsChecked != true && bUse2022Compiler.IsChecked != true)
			{
				bUse2019Compiler.IsChecked = true;
			}
		}

		private double GetSelectedPluginEngineValue()
		{
			if (PluginEngineVersionSelection == null || PluginEngineVersionSelection.SelectedValue == null)
			{
				return 0;
			}

			string selectedValue = PluginEngineVersionSelection.SelectedValue.ToString();
			Match match = Regex.Match(selectedValue, @"\d+(\.\d+)?");
			if (match.Success && double.TryParse(match.Value, out double parsedValue))
			{
				return parsedValue;
			}

			return 0;
		}

		public bool IsEngineSelection425OrAbove => GetEngineValue() >= 4.25;

		private string GetEngineName()
		{
			// 使用新的版本检测器
			try
			{
				return VersionDetector.DetectEngineVersion(SetupBatFilePath.Text) ?? 
				       UnrealBinaryBuilderHelpers.DetectEngineVersion(SetupBatFilePath.Text);
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "检测引擎版本时发生错误");
				return UnrealBinaryBuilderHelpers.DetectEngineVersion(SetupBatFilePath.Text);
			}
		}

		private double GetEngineValue()
		{
			// 使用新的版本检测器
			try
			{
				double value = VersionDetector.GetEngineVersionValue(SetupBatFilePath.Text);
				if (value > 0)
				{
					return value;
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "获取引擎版本值时发生错误");
			}

			// 回退到旧方法
			string MyEngineName = GetEngineName();
			if (MyEngineName != null)
			{
				string[] parts = MyEngineName.Split('.');
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

		private void CopyCommandLine_Click(object sender, RoutedEventArgs e)
		{
			GameAnalyticsCSharp.AddDesignEvent("CommandLine:CopyToClipboard");
			Clipboard.SetDataObject(PrepareCommandline());
			HandyControl.Controls.MessageBox.Show("Command line copied to clipboard!");
		}

		private void CompilerVersion_Checked(object sender, RoutedEventArgs e)
		{
			if (sender == bVS2022 && bVS2022.IsChecked == true)
			{
				if (bVS2019.IsChecked == true)
				{
					bVS2019.IsChecked = false;
				}
			}
			else if (sender == bVS2019 && bVS2019.IsChecked == true)
			{
				if (bVS2022.IsChecked == true)
				{
					bVS2022.IsChecked = false;
				}
			}

			UpdateCompilerOptions();
		}

		private void PluginCompiler_Checked(object sender, RoutedEventArgs e)
		{
			if (sender == bUse2022Compiler && bUse2022Compiler.IsChecked == true)
			{
				if (bUse2019Compiler.IsChecked == true)
				{
					bUse2019Compiler.IsChecked = false;
				}
			}
			else if (sender == bUse2019Compiler && bUse2019Compiler.IsChecked == true)
			{
				if (bUse2022Compiler.IsChecked == true)
				{
					bUse2022Compiler.IsChecked = false;
				}
			}

			UpdatePluginCompilerOptions();
		}

		private void SetZipPathLocation_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.SaveFileDialog SFD = new System.Windows.Forms.SaveFileDialog();
			SFD.DefaultExt = ".zip";
			SFD.Filter = "Zip File (.zip)|*.zip";
			if (Git.CommitHashShort != null)
			{
				SFD.FileName = Git.CommitHashShort;
			}
			System.Windows.Forms.DialogResult SaveDialogResult = SFD.ShowDialog();
			if (SaveDialogResult == System.Windows.Forms.DialogResult.OK)
			{
				ZipPath.Text = SFD.FileName;
			}
		}

		private void GenerateProjectFiles()
		{
			// 使用新的服务进行项目文件生成
			try
			{
				if (BuildManager.IsBuilding || bIsBuilding)
				{
					Logger.LogWarning("项目文件生成已在进行中，无法启动新的任务");
					return;
				}

				if (string.IsNullOrWhiteSpace(SetupBatFilePath.Text))
				{
					Logger.LogError("引擎路径不能为空");
					NotificationService.ShowError("引擎路径不能为空");
					return;
				}

				string generateProjectBatPath = Path.Combine(SetupBatFilePath.Text, UnrealBinaryBuilderHelpers.GenerateProjectBatFileName);
				if (!File.Exists(generateProjectBatPath))
				{
					Logger.LogError($"文件不存在: {generateProjectBatPath}");
					NotificationService.ShowError($"文件不存在: {generateProjectBatPath}");
					return;
				}

				bIsBuilding = true;
				_viewModel.IsBuilding = true;
				BuildRocketUE.IsEnabled = false;
				ProcessStartInfo processStartInfo = new ProcessStartInfo
				{
					FileName = generateProjectBatPath,
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardError = true,
					RedirectStandardOutput = true
				};

				currentProcessType = CurrentProcessType.GenerateProjectFiles;
				Logger.LogInfo($"开始生成项目文件: {generateProjectBatPath}");
				CreateProcess(processStartInfo, false);
				ChangeStatusLabel("Building...");
				GameAnalyticsCSharp.AddProgressStart("Build", "ProjectFiles");
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "生成项目文件时发生错误");
				ErrorHandler.HandleError(ex, "生成项目文件失败");
				bIsBuilding = false;
				_viewModel.IsBuilding = false;
			}
		}

		private bool? BuildAutomationTool()
		{
			// 使用新的服务构建 AutomationTool
			try
			{
				if (!UnrealBinaryBuilderHelpers.IsUnrealEngine5)
				{
					return BuildAutomationToolLauncher();
				}

				if (BuildManager.IsBuilding || bIsBuilding)
				{
					Logger.LogWarning("构建已在进行中，无法启动新的构建");
					return false;
				}

				if (string.IsNullOrEmpty(AutomationExePath))
				{
					if (!TryUpdateAutomationExePath())
					{
						string errorMsg = $"Failed to build {UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME}. AutomationExePath was null.";
						Logger.LogError(errorMsg);
						AddLogEntry(errorMsg, true);
						NotificationService.ShowError(errorMsg);
						return null;
					}
				}

				bIsBuilding = true;
				_viewModel.IsBuilding = true;
				BuildRocketUE.IsEnabled = false;
				currentProcessType = CurrentProcessType.BuildAutomationTool;
				if (UnrealBinaryBuilderHelpers.AutomationToolExists(SetupBatFilePath.Text))
				{
					string skipMsg = $"Skip building {UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME}. Already exists.";
					Logger.LogInfo(skipMsg);
					AddLogEntry(skipMsg);
					OnBuildFinishedInternal(true);
					return false;
				}

				string MsBuildFile = UnrealBinaryBuilderHelpers.GetMsBuildPath();
				if (File.Exists(MsBuildFile))
				{
					ProcessStartInfo processStartInfo = new ProcessStartInfo
					{
						FileName = MsBuildFile,
						Arguments = $"/restore /verbosity:minimal {UnrealBinaryBuilderHelpers.GetAutomationToolProjectFile(SetupBatFilePath.Text)}",
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardError = true,
						RedirectStandardOutput = true
					};

					Logger.LogInfo($"开始构建 {UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME}: {MsBuildFile}");
					CreateProcess(processStartInfo, false);
					ChangeStatusLabel("Building...");
					GameAnalyticsCSharp.AddProgressStart("Build", UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME);
					return true;
				}
				else
				{
					string errorMsg = $"Unable to build {UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME}. MsBuild not found in {MsBuildFile}";
					Logger.LogError(errorMsg);
					AddLogEntry(errorMsg, true);
					NotificationService.ShowError(errorMsg);
				}

				return false;
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, $"构建 {UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME} 时发生错误");
				ErrorHandler.HandleError(ex, $"构建 {UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_NAME} 失败");
				bIsBuilding = false;
				_viewModel.IsBuilding = false;
				return false;
			}
		}
		private bool? BuildAutomationToolLauncher()
		{
			// 使用新的服务构建 AutomationToolLauncher
			try
			{
				if (BuildManager.IsBuilding || bIsBuilding)
				{
					Logger.LogWarning("构建已在进行中，无法启动新的构建");
					return null;
				}

				if (string.IsNullOrEmpty(AutomationExePath))
				{
					if (!TryUpdateAutomationExePath())
					{
						string errorMsg = $"Failed to build {UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_LAUNCHER_NAME}. AutomationExePath was null.";
						Logger.LogError(errorMsg);
						AddLogEntry(errorMsg, true);
						NotificationService.ShowError(errorMsg);
						return null;
					}
				}

				bIsBuilding = true;
				_viewModel.IsBuilding = true;
				BuildRocketUE.IsEnabled = false;
				currentProcessType = CurrentProcessType.BuildAutomationToolLauncher;
				if (File.Exists(AutomationExePath))
				{
					string skipMsg = $"Skip building {UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_LAUNCHER_NAME}. Already exists.";
					Logger.LogInfo(skipMsg);
					AddLogEntry(skipMsg);
					OnBuildFinishedInternal(true);
					return false;
				}

				if (UnrealBinaryBuilderHelpers.IsUnrealEngine5)
				{
					string MsBuildFile = UnrealBinaryBuilderHelpers.GetMsBuildPath();
					if (File.Exists(MsBuildFile))
					{
						ProcessStartInfo processStartInfo = new ProcessStartInfo
						{
							FileName = MsBuildFile,
							Arguments = UnrealBinaryBuilderHelpers.GetAutomationToolLauncherProjectFile(SetupBatFilePath.Text),
							UseShellExecute = false,
							CreateNoWindow = true,
							RedirectStandardError = true,
							RedirectStandardOutput = true
						};

						Logger.LogInfo($"开始构建 {UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_LAUNCHER_NAME}: {MsBuildFile}");
						CreateProcess(processStartInfo, false);
						ChangeStatusLabel("Building...");
						GameAnalyticsCSharp.AddProgressStart("Build", UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_LAUNCHER_NAME);
						return true;
					}
					else
					{
						string errorMsg = $"Unable to build {UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_LAUNCHER_NAME}. MsBuild not found in {MsBuildFile}";
						Logger.LogError(errorMsg);
						AddLogEntry(errorMsg, true);
						NotificationService.ShowError(errorMsg);
					}
				}
				else
				{
					string RunUATFile = Path.Combine(SetupBatFilePath.Text, "Engine", "Build", "BatchFiles", "RunUAT.bat");
					if (File.Exists(RunUATFile))
					{
						ProcessStartInfo processStartInfo = new ProcessStartInfo
						{
							FileName = RunUATFile,
							Arguments = "-compileonly",
							UseShellExecute = false,
							CreateNoWindow = true,
							RedirectStandardError = true,
							RedirectStandardOutput = true
						};

						Logger.LogInfo($"开始构建 {UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_LAUNCHER_NAME}: {RunUATFile}");
						CreateProcess(processStartInfo, false);
						ChangeStatusLabel("Building...");
						GameAnalyticsCSharp.AddProgressStart("Build", UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_LAUNCHER_NAME);
						return true;
					}
					else
					{
						string errorMsg = $"RunUAT.bat 文件不存在: {RunUATFile}";
						Logger.LogError(errorMsg);
						AddLogEntry(errorMsg, true);
						NotificationService.ShowError(errorMsg);
					}
				}

				return null;
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, $"构建 {UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_LAUNCHER_NAME} 时发生错误");
				ErrorHandler.HandleError(ex, $"构建 {UnrealBinaryBuilderHelpers.AUTOMATION_TOOL_LAUNCHER_NAME} 失败");
				bIsBuilding = false;
				_viewModel.IsBuilding = false;
				return null;
			}
		}

		private string GetSelectedPluginCompilerArgument()
		{
			if (bUse2022Compiler != null && bUse2022Compiler.IsEnabled && bUse2022Compiler.IsChecked == true)
			{
				return "-VS2022";
			}

			if (bUse2019Compiler != null && bUse2019Compiler.IsEnabled && bUse2019Compiler.IsChecked == true)
			{
				return "-VS2019";
			}

			if (bUse2022Compiler != null && bUse2022Compiler.IsEnabled)
			{
				return "-VS2022";
			}

			if (bUse2019Compiler != null && bUse2019Compiler.IsEnabled)
			{
				return "-VS2019";
			}

			return "-VS2017";
		}

		private string BuildPlugin(PluginCard pluginCard)
		{
			// 使用新的服务进行插件构建
			try
			{
				// 开始性能监控
				using (PerformanceMonitor.StartOperation("BuildPlugin"))
				{
					if (BuildManager.IsBuilding || bIsBuilding)
					{
						return "Cannot build plugin while task is running";
					}

					if (!pluginCard.IsValid())
					{
						return $"{pluginCard.PluginName.Text} ({pluginCard.EngineVersionText.Text}) is already compiled.";
					}

					CurrentPluginBeingBuilt = pluginCard;
					bIsBuilding = true;
					_viewModel.IsBuilding = true;
					BuildRocketUE.IsEnabled = false;
					StartPluginBuildsBtn.IsEnabled = false;
					currentProcessType = CurrentProcessType.BuildPlugin;

					string pluginName = Path.GetFileNameWithoutExtension(pluginCard.PluginPath);
					Logger.LogInfo($"========================== BUILDING PLUGIN {pluginName.ToUpper()} ==========================");
					Logger.LogInfo($"Plugin: {pluginCard.PluginPath}");
					Logger.LogInfo($"Package Location: {pluginCard.DestinationPath}");
					Logger.LogInfo($"Target Engine: {pluginCard.EngineVersionText.Text}");
					AddLogEntry($"========================== BUILDING PLUGIN {pluginName.ToUpper()} ==========================");
					AddLogEntry($"Plugin: {pluginCard.PluginPath}");
					AddLogEntry($"Package Location: {pluginCard.DestinationPath}");
					AddLogEntry($"Target Engine: {pluginCard.EngineVersionText.Text}");

					ProcessStartInfo processStartInfo = new ProcessStartInfo
					{
						FileName = pluginCard.RunUATFile,
						Arguments = $"BuildPlugin -Plugin=\"{pluginCard.PluginPath}\" -Package=\"{pluginCard.DestinationPath}\" -Rocket {pluginCard.GetCompiler()} {pluginCard.GetTargetPlatforms()}",
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardError = true,
						RedirectStandardOutput = true
					};

					pluginCard.BuildStarted();
					CreateProcess(processStartInfo, false);
					ChangeStatusLabel($"Building Plugin - {pluginName}");
					NotificationService.ShowInfo($"Building Plugin - {pluginName}");
					GameAnalyticsCSharp.AddProgressStart("Build", "Plugin");
					return null;
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "构建插件时发生错误");
				ErrorHandler.HandleError(ex, "构建插件失败");
				bIsBuilding = false;
				_viewModel.IsBuilding = false;
				return $"构建插件时发生错误: {ex.Message}";
			}
		}

		private void CancelZipping_Click(object sender, RoutedEventArgs e)
		{
			postBuildSettings.CancelTask();
		}

		private void OpenBuildFolder_Click(object sender, RoutedEventArgs e)
		{
			Process.Start("explorer.exe", FinalBuildPath);
		}

		private void GitCachePathBrowse_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.FolderBrowserDialog NewFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
			NewFolderDialog.ShowDialog();
			GitCachePath.Text = NewFolderDialog.SelectedPath;
		}

		private void PluginQueueBtn_Click(object sender, RoutedEventArgs e)
		{
			if (File.Exists(PluginPath.Text) && Directory.Exists(PluginDestinationPath.Text))
			{
				if (PluginEngineVersionSelection.SelectedIndex < 0)
				{
					HandyControl.Controls.MessageBox.Fatal($"Cannot build \"{Path.GetFileNameWithoutExtension(PluginPath.Text)}\". Engine selection is invalid.");
					return;
				}

				List<string> TargetPlatformsList = null;
				if (bPluginOverrideTargetPlatforms.IsChecked == true)
				{
					TargetPlatformsList = new List<string>();					
					foreach (var C in PluginPlatforms.Children)
					{
						if (((CheckBox)C).IsChecked == true)
						{
							TargetPlatformsList.Add(((CheckBox)C).Name.Replace("bPlugin", ""));
						}
					}
				}

				string selectedCompiler = GetSelectedPluginCompilerArgument();
				PluginQueues.Children.Add(new PluginCard(this, PluginPath.Text, PluginDestinationPath.Text, PluginBuildEnginePath[PluginEngineVersionSelection.SelectedIndex], selectedCompiler, TargetPlatformsList, (bool)PluginZip.IsChecked, PluginZipPath.Text, (bool)PluginZipForMarketplace.IsChecked));
				PluginQueueBtn.IsEnabled = false;
				PluginPath.Text = "";
				PluginDestinationPath.Text = "";
				PluginEngineVersionSelection.SelectedIndex = -1;
				PluginZipForMarketplace.IsChecked = true;
				PluginZip.IsChecked = false;
				PluginZipPath.Text = "";
				foreach (var C in PluginPlatforms.Children)
				{
					if (((CheckBox)C).Name != "bPluginWin64")
					{
						((CheckBox)C).IsChecked = false;
					}
				}
			}
			else
			{
				HandyControl.Controls.MessageBox.Fatal($"Cannot build \"{Path.GetFileNameWithoutExtension(PluginPath.Text)}\". Either file does not exist or save location is not valid.");
			}
		}

		private void PluginPathBrowse_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog NewFileDialog = new OpenFileDialog
			{
				Filter = "Unreal Plugin file (*.uplugin)|*.uplugin"
			};

			if (NewFileDialog.ShowDialog() != true)
			{
				return;
			}

			PluginPath.Text = NewFileDialog.FileName;
			foreach (var C in PluginPlatforms.Children)
			{
				CheckBox checkBox = (CheckBox)C;
				if (checkBox.Name != "bPluginWin64")
				{
					checkBox.IsChecked = false;
				}
			}

			try
			{
				string pluginJsonContent = File.ReadAllText(PluginPath.Text);
				UE4PluginJson PluginJson = JsonConvert.DeserializeObject<UE4PluginJson>(pluginJsonContent);
				
				if (PluginJson?.Modules != null && PluginJson.Modules.Count > 0 && 
				    PluginJson.Modules[0].WhitelistPlatforms != null)
				{
					foreach (var C in PluginPlatforms.Children)
					{
						CheckBox checkBox = (CheckBox)C;
						if (PluginJson.Modules[0].WhitelistPlatforms.Contains(checkBox.Name.Replace("bPlugin", "")))
						{
							checkBox.IsChecked = true;
						}
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to read plugin file: {ex.Message}");
			}

			UpdatePluginQueueButtonState();
		}

		private void UpdatePluginQueueButtonState()
		{
			PluginQueueBtn.IsEnabled = File.Exists(PluginPath.Text) && Directory.Exists(PluginDestinationPath.Text);
		}

		private void PluginDestinationPathBrowse_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.FolderBrowserDialog NewFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
			NewFolderDialog.ShowDialog();
			PluginDestinationPath.Text = NewFolderDialog.SelectedPath;
			UpdatePluginQueueButtonState();
		}

		public void RemovePluginFromList(PluginCard pluginCard)
		{
			PluginQueues.Children.Remove(pluginCard);
		}

		private void StartPluginBuildsBtn_Click(object sender, RoutedEventArgs e)
		{
			if (PluginQueues.Children.Count == 0)
			{
				HandyControl.Controls.MessageBox.Fatal("Queue is empty. Add one or more plugin to queue and build.");
			}
			else
			{
				string PluginBuildMessage = null;
				foreach (var C in PluginQueues.Children)
				{
					PluginCard pluginCard = (PluginCard)C;
					if (pluginCard.IsPending())
					{
						AddLogEntry($"Building {PluginQueues.Children.Count} Plugin(s).");
						AddLogEntry("");
						ShowToastMessage($"Building {PluginQueues.Children.Count} Plugin(s).");
						PluginBuildMessage = BuildPlugin(pluginCard);
						break;
					}
				}

				if (PluginBuildMessage != null)
				{
					Growl.Clear();
					HandyControl.Controls.MessageBox.Fatal(PluginBuildMessage);
				}
			}
		}

		private void PluginEngineVersionSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdatePluginCompilerOptions();
		}

		private void PluginZipDestinationPathBrowse_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.FolderBrowserDialog NewFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
			NewFolderDialog.ShowDialog();
			PluginZipPath.Text = NewFolderDialog.SelectedPath;
		}

		private void GetSourceCode_Click(object sender, RoutedEventArgs e)
		{
			OpenBrowser("https://github.com/ryanjon2040/Unreal-Binary-Builder");
			GameAnalyticsCSharp.AddDesignEvent("Menu:Click:SourceCode");
		}

		private void SupportUnrealX_Click(object sender, RoutedEventArgs e)
		{
			OpenBrowser("https://www.buymeacoffee.com/ryanjon2040");
			GameAnalyticsCSharp.AddDesignEvent("Menu:Click:BuyMeACoffee");
		}

		private void SupportAgora_Click(object sender, RoutedEventArgs e)
		{
			OpenBrowser("https://www.patreon.com/ryanjon2040");
			GameAnalyticsCSharp.AddDesignEvent("Menu:Click:Agora");
		}

		private void FeedbackBtn_Click(object sender, RoutedEventArgs e)
		{
			OpenBrowser("https://forms.gle/LeZqAeqmV9fWQpxP7");
			GameAnalyticsCSharp.AddDesignEvent("Menu:Click:Feedback");
		}
		private void ChangelogBtn_Click(object sender, RoutedEventArgs e)
		{
			OpenBrowser("https://github.com/ryanjon2040/Unreal-Binary-Builder/blob/master/CHANGELOG.md");
			GameAnalyticsCSharp.AddDesignEvent("Menu:Click:Changelog");
		}

		private void OpenLogFolderBtn_Click(object sender, RoutedEventArgs e)
		{
			BuilderSettings.OpenLogFolder();
		}

		private void OpenSettingsBtn_Click(object sender, RoutedEventArgs e)
		{
			BuilderSettings.OpenSettings();
		}

		private void AboutBtn_Click(object sender, RoutedEventArgs e)
		{
			GameAnalyticsCSharp.AddDesignEvent("AboutDialog:Open");
			aboutDialog = Dialog.Show(new AboutDialog(this));
		}

		public void CloseAboutDialog()
		{
			GameAnalyticsCSharp.AddDesignEvent("AboutDialog:Close");
			aboutDialog.Close();
		}

		private void GitPlatform_CheckedChanged(object sender, RoutedEventArgs e)
		{
			string TargetPlatformName = ((Control)sender).Name.Replace("Git", "").Replace("Platform", "");
			BuilderSettings.UpdatePlatformInclusion(TargetPlatformName, (bool)((CheckBox)sender).IsChecked);
		}

		private void OpenCodeEditor(string FileType)
		{
			string FilePath = null;
			string UE4FileType = $"UE4{FileType}.Target.cs";
			string UE5FileType = $"Unreal{FileType}.Target.cs";
			if (!string.IsNullOrWhiteSpace(SetupBatFilePath.Text))
			{
				FilePath = Path.Combine(SetupBatFilePath.Text, "Engine", "Source", UE5FileType);
				if (!File.Exists(FilePath))
				{
					FilePath = Path.Combine(SetupBatFilePath.Text, "Engine", "Source", UE4FileType);
				}
			}
			else if (!string.IsNullOrWhiteSpace(AutomationExePath))
			{
				string Local_BaseEnginePath = Regex.Replace(AutomationExePath, @"\\Binaries.+", "");
				FilePath = Path.Combine(Local_BaseEnginePath, "Source", UE5FileType);
				if (!File.Exists(FilePath))
				{
					FilePath = Path.Combine(Local_BaseEnginePath, "Source", UE4FileType);
				}
			}

			if (string.IsNullOrWhiteSpace(FilePath))
			{
				HandyControl.Controls.MessageBox.Fatal("Please choose Engine root folder first.");
				return;
			}

			CodeEditor CE = new CodeEditor();
			CE.Owner = this;
			CE.Show();
			bool bLoaded = CE.LoadFile(FilePath);
			if (!bLoaded)
			{
				HandyControl.Controls.MessageBox.Error($"{FilePath} does not exist.");
				CE.Close();
			}
		}

		private void EditServerTargetCs_Click(object sender, RoutedEventArgs e)
		{
			OpenCodeEditor("Server");
		}

		private void EditGameTargetCs_Click(object sender, RoutedEventArgs e)
		{
			OpenCodeEditor("Game");
		}

		private void EditEditorTargetCs_Click(object sender, RoutedEventArgs e)
		{
			OpenCodeEditor("Editor");
		}

		private void EditClientTargetCs_Click(object sender, RoutedEventArgs e)
		{
			OpenCodeEditor("Client");
		}

		private void SetupBatFilePath_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(SetupBatFilePath.Text))
			{
				UpdateCompilerOptions();
				UpdatePluginCompilerOptions();
				return;
			}

			// 使用新的版本检测器
			string EngineVersion = null;
			try
			{
				EngineVersion = VersionDetector.DetectEngineVersion(SetupBatFilePath.Text, true);
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "检测引擎版本时发生错误");
			}

			if (string.IsNullOrEmpty(EngineVersion))
			{
				EngineVersion = UnrealBinaryBuilderHelpers.DetectEngineVersion(SetupBatFilePath.Text, true);
			}

			if (EngineVersion != null)
			{
				FoundEngineLabel.Content = Git.CommitHashShort != null 
					? $"Selected Unreal Engine {EngineVersion}. Commit - {Git.CommitHashShort}"
					: $"Selected Unreal Engine {EngineVersion}";
			}
			else
			{
				FoundEngineLabel.Content = "Unable to detect Engine version.";
				Logger.LogWarning("Unable to detect engine version");
			}

			AutomationExePath = null;
			TryUpdateAutomationExePath();

			System.Collections.ObjectModel.Collection<BindingExpressionBase> bindExps = CompileMainGrid.BindingGroup.BindingExpressions;
			foreach (BindingExpression bExp in bindExps)
			{
				if (bExp.BindingGroup.Name == "EngineChanged")
				{
					bExp.UpdateTarget();
				}
			}

			UpdateCompilerOptions();
			UpdatePluginCompilerOptions();
		}

		#region Project Build Related Methods

		private void ProjectPathBrowse_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog fileDialog = new OpenFileDialog
			{
				Filter = "Unreal Project file (*.uproject)|*.uproject",
				Title = Services.ResourceHelper.GetString("SelectUnrealProjectFile")
			};

			if (fileDialog.ShowDialog() == true)
			{
				ProjectPath.Text = fileDialog.FileName;
				SettingsJSON.ProjectPath = fileDialog.FileName;

				// Try to auto-detect engine path
				if (string.IsNullOrWhiteSpace(ProjectEnginePath.Text) && !string.IsNullOrWhiteSpace(SetupBatFilePath.Text))
				{
					ProjectEnginePath.Text = SetupBatFilePath.Text;
					SettingsJSON.ProjectEnginePath = SetupBatFilePath.Text;
				}
			}
		}

		private void ProjectEnginePathBrowse_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog
			{
				Description = Services.ResourceHelper.GetString("SelectUnrealEngineRootDirectory"),
				ShowNewFolderButton = false
			};

			if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				string selectedPath = folderDialog.SelectedPath;
				string runUatPath = Path.Combine(selectedPath, "Engine", "Build", "BatchFiles", "RunUAT.bat");

				if (File.Exists(runUatPath))
				{
					ProjectEnginePath.Text = selectedPath;
					SettingsJSON.ProjectEnginePath = selectedPath;
				}
				else
				{
					string errorMessage = Services.ResourceHelper.GetString("ErrorInvalidEnginePath", runUatPath);
					string errorTitle = Services.ResourceHelper.GetString("ErrorInvalidEnginePathTitle");
					HandyControl.Controls.MessageBox.Error(errorMessage, errorTitle);
				}
			}
		}

		private string PrepareProjectCommandLine()
		{
			try
			{
				string projectPath = ProjectPath.Text;
				string targetType = GetSelectedComboBoxItem(ProjectTargetType);
				string targetPlatform = GetSelectedComboBoxItem(ProjectTargetPlatform);
				string configuration = GetSelectedComboBoxItem(ProjectConfiguration);
				bool bBuild = ProjectBuild.IsChecked == true;
				bool bCook = ProjectCook.IsChecked == true;
				bool bCookAll = ProjectCookAll.IsChecked == true;
				bool bPackage = ProjectPackage.IsChecked == true;
				string additionalArgs = ProjectAdditionalArgs.Text ?? "";

				return CommandLineBuilder.BuildProjectCommandLine(
					projectPath,
					ProjectEnginePath.Text,
					targetType,
					targetPlatform,
					configuration,
					bCook,
					bCookAll,
					bPackage,
					bBuild,
					additionalArgs
				);
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Error occurred while building project command line");
				string errorMessage = Services.ResourceHelper.GetString("ErrorBuildCommandLineFailed", ex.Message);
				NotificationService.ShowError(errorMessage);
				return "";
			}
		}

		private string GetSelectedComboBoxItem(System.Windows.Controls.ComboBox comboBox)
		{
			if (comboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem item)
			{
				return item.Content?.ToString() ?? "";
			}
			return "";
		}

		private async void BuildProjectBtn_Click(object sender, RoutedEventArgs e)
		{
			// If building, stop the build
			if (BuildManager.IsBuilding || bIsBuilding)
			{
				if (currentProcessType == CurrentProcessType.BuildProject)
				{
					// Stop project build
					GameAnalyticsCSharp.AddDesignEvent("Build:Project:Stopped");
					BuildManager.StopBuild();
					CloseCurrentProcess(true);
					BuildProjectBtn.Content = Services.ResourceHelper.GetString("BuildProject");
					NotificationService.ShowInfo(Services.ResourceHelper.GetString("MessageProjectBuildStopped"));
					return;
				}
				else
				{
					// Other build task is running
					string question = Services.ResourceHelper.GetString("QuestionStopBuildTask");
					string title = Services.ResourceHelper.GetString("TitleBuildTaskRunning");
					MessageBoxResult result = HandyControl.Controls.MessageBox.Show(
						question,
						title,
						MessageBoxButton.YesNo,
						MessageBoxImage.Question
					);

					if (result == MessageBoxResult.Yes)
					{
						BuildManager.StopBuild();
						CloseCurrentProcess(true);
					}
					else
					{
						return;
					}
				}
			}

			// Validate input
			if (string.IsNullOrWhiteSpace(ProjectPath.Text) || !File.Exists(ProjectPath.Text))
			{
				NotificationService.ShowError(Services.ResourceHelper.GetString("ErrorProjectFileRequired"));
				return;
			}

			if (string.IsNullOrWhiteSpace(ProjectEnginePath.Text))
			{
				NotificationService.ShowError(Services.ResourceHelper.GetString("ErrorEnginePathRequired"));
				return;
			}

			string runUatPath = Path.Combine(ProjectEnginePath.Text, "Engine", "Build", "BatchFiles", "RunUAT.bat");
			if (!File.Exists(runUatPath))
			{
				string errorMessage = Services.ResourceHelper.GetString("ErrorRunUATNotFound", runUatPath);
				NotificationService.ShowError(errorMessage);
				return;
			}

			// Check if at least one operation is selected
			if (ProjectBuild.IsChecked != true && ProjectCook.IsChecked != true && ProjectPackage.IsChecked != true)
			{
				NotificationService.ShowWarning(Services.ResourceHelper.GetString("WarningSelectOperation"));
				return;
			}

			// Prepare build
			currentProcessType = CurrentProcessType.BuildProject;
			bLastBuildSuccess = false;

			string commandLine = PrepareProjectCommandLine();
			if (string.IsNullOrWhiteSpace(commandLine))
			{
				return;
			}

			ChangeStatusLabel(Services.ResourceHelper.GetString("MessagePreparingBuild"));

			try
			{
				using (PerformanceMonitor.StartOperation("BuildProject"))
				{
					_viewModel.IsBuilding = true;
					_viewModel.ResetCounters();
					bIsBuilding = true;

					bool success = await BuildManager.BuildProjectAsync(runUatPath, commandLine);
					if (success)
					{
						string buildingText = Services.ResourceHelper.GetString("MessageBuildingProject");
						ChangeStatusLabel(buildingText);
						_viewModel.StatusText = buildingText;
						BuildProjectBtn.Content = Services.ResourceHelper.GetString("StopBuild");
						GameAnalyticsCSharp.AddDesignEvent("Build:Project:Started");
						Logger.LogInfo(Services.ResourceHelper.GetString("MessageProjectBuildStarted"));

						AddLogEntry($"========================== BUILDING PROJECT ==========================");
						AddLogEntry($"Project: {ProjectPath.Text}");
						AddLogEntry($"Engine: {ProjectEnginePath.Text}");
						AddLogEntry($"Target Type: {GetSelectedComboBoxItem(ProjectTargetType)}");
						AddLogEntry($"Target Platform: {GetSelectedComboBoxItem(ProjectTargetPlatform)}");
						AddLogEntry($"Configuration: {GetSelectedComboBoxItem(ProjectConfiguration)}");
						AddLogEntry($"Command Line: {commandLine}");
					}
					else
					{
						Logger.LogError(Services.ResourceHelper.GetString("ErrorBuildStartFailed"));
						bIsBuilding = false;
						_viewModel.IsBuilding = false;
						BuildProjectBtn.Content = Services.ResourceHelper.GetString("BuildProject");
						NotificationService.ShowError(Services.ResourceHelper.GetString("ErrorBuildStartFailed"));
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, Services.ResourceHelper.GetString("ErrorBuildProjectFailed"));
				ErrorHandler.HandleError(ex, Services.ResourceHelper.GetString("ErrorBuildProjectFailed"));
				bIsBuilding = false;
				_viewModel.IsBuilding = false;
				BuildProjectBtn.Content = Services.ResourceHelper.GetString("BuildProject");
				NotificationService.ShowError(Services.ResourceHelper.GetString("ErrorBuildProjectFailed"));
			}
		}

		private void CopyProjectCommandLineBtn_Click(object sender, RoutedEventArgs e)
		{
			string commandLine = PrepareProjectCommandLine();
			if (!string.IsNullOrWhiteSpace(commandLine))
			{
				Clipboard.SetDataObject(commandLine);
				GameAnalyticsCSharp.AddDesignEvent("Project:CommandLine:CopyToClipboard");
				NotificationService.ShowInfo(Services.ResourceHelper.GetString("MessageCommandLineCopied"));
			}
			else
			{
				NotificationService.ShowWarning(Services.ResourceHelper.GetString("WarningCommandLineGenerationFailed"));
			}
		}

		#endregion
	}
}
