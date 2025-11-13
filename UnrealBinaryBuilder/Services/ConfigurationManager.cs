using System;
using System.IO;
using Newtonsoft.Json;
using UnrealBinaryBuilder.Classes;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 配置管理器 - 处理敏感信息和外部配置
	/// </summary>
	public class ConfigurationManager
	{
		private readonly string _configFilePath;
		private readonly ILogger _logger;
		private AppConfiguration _configuration;

		public ConfigurationManager(ILogger logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			try
			{
				_configFilePath = Path.Combine(BuilderSettings.PROGRAM_SAVED_PATH, "Saved", "AppConfig.json");
				LoadConfiguration();
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "初始化配置管理器时发生错误");
				_configFilePath = Path.Combine(Path.GetTempPath(), "AppConfig.json");
				_configuration = new AppConfiguration();
			}
		}

		/// <summary>
		/// 获取配置
		/// </summary>
		public AppConfiguration GetConfiguration()
		{
			return _configuration ??= new AppConfiguration();
		}

		/// <summary>
		/// 保存配置
		/// </summary>
		public void SaveConfiguration()
		{
			try
			{
				string directory = Path.GetDirectoryName(_configFilePath);
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				string json = JsonConvert.SerializeObject(_configuration, Formatting.Indented);
				File.WriteAllText(_configFilePath, json);
				_logger.LogInfo($"配置已保存到: {_configFilePath}");
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "保存配置时发生错误");
			}
		}

		/// <summary>
		/// 加载配置
		/// </summary>
		private void LoadConfiguration()
		{
			try
			{
				if (File.Exists(_configFilePath))
				{
					string json = File.ReadAllText(_configFilePath);
					_configuration = JsonConvert.DeserializeObject<AppConfiguration>(json) ?? new AppConfiguration();
					_logger.LogInfo($"配置已从 {_configFilePath} 加载");
				}
				else
				{
					_configuration = new AppConfiguration();
					SaveConfiguration();
					_logger.LogInfo("创建了新的默认配置");
				}
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "加载配置时发生错误，使用默认配置");
				_configuration = new AppConfiguration();
			}
		}
	}

	/// <summary>
	/// 应用程序配置类 - 包含敏感信息
	/// </summary>
	public class AppConfiguration
	{
		/// <summary>
		/// API 密钥（示例）
		/// </summary>
		public string ApiKey { get; set; } = string.Empty;

		/// <summary>
		/// 代理服务器地址
		/// </summary>
		public string ProxyServer { get; set; } = string.Empty;

		/// <summary>
		/// 代理服务器端口
		/// </summary>
		public int ProxyPort { get; set; } = 0;

		/// <summary>
		/// 是否启用代理
		/// </summary>
		public bool UseProxy { get; set; } = false;

		/// <summary>
		/// 其他敏感配置项
		/// </summary>
		public string SecretToken { get; set; } = string.Empty;
	}
}

