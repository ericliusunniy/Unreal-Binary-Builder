using System;
using System.IO;
using Newtonsoft.Json;
using UnrealBinaryBuilder.Classes;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 设置管理器 - 管理应用程序设置
	/// </summary>
	public class SettingsManager
	{
		private readonly ILogger _logger;
		private readonly string _settingsFilePath;
		private BuilderSettingsJson _settings;

		public BuilderSettingsJson Settings => _settings ??= LoadSettings();

		public SettingsManager(ILogger logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_settingsFilePath = Path.Combine(BuilderSettings.PROGRAM_SAVED_PATH, "Saved", "Settings.json");
		}

		/// <summary>
		/// 加载设置
		/// </summary>
		public BuilderSettingsJson LoadSettings()
		{
			try
			{
				if (File.Exists(_settingsFilePath))
				{
					string json = File.ReadAllText(_settingsFilePath);
					_settings = JsonConvert.DeserializeObject<BuilderSettingsJson>(json);
					_logger.LogInfo($"设置已从 {_settingsFilePath} 加载");
					return _settings;
				}
				else
				{
					_settings = CreateDefaultSettings();
					SaveSettings();
					_logger.LogInfo("创建了默认设置");
					return _settings;
				}
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "加载设置时发生错误");
				_settings = CreateDefaultSettings();
				return _settings;
			}
		}

		/// <summary>
		/// 保存设置
		/// </summary>
		public void SaveSettings()
		{
			try
			{
				string directory = Path.GetDirectoryName(_settingsFilePath);
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
				File.WriteAllText(_settingsFilePath, json);
				_logger.LogInfo($"设置已保存到 {_settingsFilePath}");
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, "保存设置时发生错误");
			}
		}

		/// <summary>
		/// 重置为默认设置
		/// </summary>
		public void ResetToDefaults()
		{
			_settings = CreateDefaultSettings();
			SaveSettings();
			_logger.LogInfo("设置已重置为默认值");
		}

		/// <summary>
		/// 创建默认设置
		/// </summary>
		private BuilderSettingsJson CreateDefaultSettings()
		{
			return BuilderSettings.GetSettingsFile(false);
		}
	}
}

