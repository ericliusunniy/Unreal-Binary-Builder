using System;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 服务容器 - 管理所有服务的创建和生命周期
	/// </summary>
	public class ServiceContainer : IDisposable
	{
		private ResourceManager _resourceManager;
		private ILogger _logger;
		private IProcessManager _processManager;
		private IBuildManager _buildManager;
		private ConfigurationManager _configurationManager;
		private UpdateManager _updateManager;
		private EngineVersionDetector _versionDetector;
		private CommandLineBuilder _commandLineBuilder;
		private LogParser _logParser;
		private PerformanceMonitor _performanceMonitor;
		private ErrorHandler _errorHandler;
		private CacheManager _cacheManager;
		private EventAggregator _eventAggregator;
		private NotificationService _notificationService;
		private ThemeManager _themeManager;
		private SettingsManager _settingsManager;
		private BackgroundTaskManager _backgroundTaskManager;
		private bool _disposed = false;

		public ServiceContainer()
		{
			InitializeServices();
		}

		private void InitializeServices()
		{
			// 创建服务（按依赖顺序）
			_logger = new Logger();
			_resourceManager = new ResourceManager(_logger);
			_resourceManager.RegisterResource((IDisposable)_logger); // Logger 实现了 IDisposable

			_configurationManager = new ConfigurationManager(_logger);
			_processManager = new ProcessManager(_logger);
			_resourceManager.RegisterResource(_processManager);

			_buildManager = new BuildManager(_processManager, _logger);
			_versionDetector = new EngineVersionDetector(_logger);
			_commandLineBuilder = new CommandLineBuilder(_logger, _versionDetector);
			_logParser = new LogParser();
			_updateManager = new UpdateManager(_logger, _processManager);
			_resourceManager.RegisterResource(_updateManager);

			_performanceMonitor = new PerformanceMonitor(_logger);
			_resourceManager.RegisterResource(_performanceMonitor);

			_errorHandler = new ErrorHandler(_logger);

			_cacheManager = new CacheManager(_logger);
			_resourceManager.RegisterResource(_cacheManager);

			_eventAggregator = new EventAggregator();
			_resourceManager.RegisterResource(_eventAggregator);

			_notificationService = new NotificationService(_logger);
			_themeManager = new ThemeManager(_logger);
			_settingsManager = new SettingsManager(_logger);
			_backgroundTaskManager = new BackgroundTaskManager(_logger);
			_resourceManager.RegisterResource(_backgroundTaskManager);
		}

		public ILogger Logger => _logger;
		public IProcessManager ProcessManager => _processManager;
		public IBuildManager BuildManager => _buildManager;
		public ConfigurationManager ConfigurationManager => _configurationManager;
		public UpdateManager UpdateManager => _updateManager;
		public EngineVersionDetector VersionDetector => _versionDetector;
		public CommandLineBuilder CommandLineBuilder => _commandLineBuilder;
		public LogParser LogParser => _logParser;
		public PerformanceMonitor PerformanceMonitor => _performanceMonitor;
		public ErrorHandler ErrorHandler => _errorHandler;
		public CacheManager CacheManager => _cacheManager;
		public EventAggregator EventAggregator => _eventAggregator;
		public NotificationService NotificationService => _notificationService;
		public ThemeManager ThemeManager => _themeManager;
		public SettingsManager SettingsManager => _settingsManager;
		public BackgroundTaskManager BackgroundTaskManager => _backgroundTaskManager;
		public ResourceManager ResourceManager => _resourceManager;

		public void Dispose()
		{
			if (!_disposed)
			{
				_resourceManager?.Dispose();
				_disposed = true;
			}
		}
	}
}

