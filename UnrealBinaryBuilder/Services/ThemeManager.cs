using System;
using HandyControl.Data;
using HandyControl.Themes;
using HandyControl.Tools;
using UnrealBinaryBuilder.Services;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 主题管理器 - 管理应用程序主题
	/// </summary>
	public class ThemeManager
	{
		private readonly ILogger _logger;
		private SkinType _currentTheme;

		public SkinType CurrentTheme => _currentTheme;

		public ThemeManager(ILogger logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// 应用主题
		/// </summary>
		public void ApplyTheme(SkinType theme)
		{
			try
			{
				_currentTheme = theme;
				UpdateSkin(theme);
				_logger.LogInfo($"主题已切换为: {theme}");
			}
			catch (Exception ex)
			{
				_logger.LogException(ex, $"应用主题时发生错误: {theme}");
			}
		}

		/// <summary>
		/// 从字符串应用主题
		/// </summary>
		public void ApplyTheme(string themeName)
		{
			SkinType theme = ParseThemeName(themeName);
			ApplyTheme(theme);
		}

		/// <summary>
		/// 更新皮肤
		/// </summary>
		private void UpdateSkin(SkinType skin)
		{
			SharedResourceDictionary.SharedDictionaries.Clear();
			System.Windows.Application.Current.Resources.MergedDictionaries.Add(HandyControl.Tools.ResourceHelper.GetSkin(skin));
			System.Windows.Application.Current.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
			{
				Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Theme.xaml")
			});
			System.Windows.Application.Current.MainWindow?.OnApplyTemplate();
		}

		/// <summary>
		/// 解析主题名称
		/// </summary>
		private SkinType ParseThemeName(string themeName)
		{
			if (string.IsNullOrWhiteSpace(themeName))
				return SkinType.Dark;

			string lowerName = themeName.ToLower();
			return lowerName switch
			{
				"dark" => SkinType.Dark,
				"light" => SkinType.Default, // HandyControl 可能没有 Light，使用 Default
				"violet" => SkinType.Violet,
				_ => SkinType.Default
			};
		}

		/// <summary>
		/// 获取主题名称
		/// </summary>
		public string GetThemeName()
		{
			return _currentTheme.ToString();
		}
	}
}

