using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UnrealBinaryBuilder.Classes;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// MainWindow 的视图模型 - 分离业务逻辑和 UI
	/// </summary>
	public class MainWindowViewModel : INotifyPropertyChanged
	{
		private string _statusText = "空闲";
		private string _stepText = "步骤: ";
		private bool _isBuilding = false;
		private int _compiledFiles = 0;
		private int _compiledFilesTotal = 0;
		private int _errorCount = 0;
		private int _warningCount = 0;

		public string StatusText
		{
			get => _statusText;
			set
			{
				if (_statusText != value)
				{
					_statusText = value;
					OnPropertyChanged();
				}
			}
		}

		public string StepText
		{
			get => _stepText;
			set
			{
				if (_stepText != value)
				{
					_stepText = value;
					OnPropertyChanged();
				}
			}
		}

		public bool IsBuilding
		{
			get => _isBuilding;
			set
			{
				if (_isBuilding != value)
				{
					_isBuilding = value;
					OnPropertyChanged();
				}
			}
		}

		public int CompiledFiles
		{
			get => _compiledFiles;
			set
			{
				if (_compiledFiles != value)
				{
					_compiledFiles = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CompiledFilesText));
				}
			}
		}

		public int CompiledFilesTotal
		{
			get => _compiledFilesTotal;
			set
			{
				if (_compiledFilesTotal != value)
				{
					_compiledFilesTotal = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CompiledFilesText));
				}
			}
		}

		public string CompiledFilesText => $"[已编译: {CompiledFiles}. 总计: {CompiledFilesTotal}]";

		public int ErrorCount
		{
			get => _errorCount;
			set
			{
				if (_errorCount != value)
				{
					_errorCount = value;
					OnPropertyChanged();
				}
			}
		}

		public int WarningCount
		{
			get => _warningCount;
			set
			{
				if (_warningCount != value)
				{
					_warningCount = value;
					OnPropertyChanged();
				}
			}
		}

		public BuilderSettingsJson Settings { get; set; }

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public void ResetCounters()
		{
			CompiledFiles = 0;
			CompiledFilesTotal = 0;
			ErrorCount = 0;
			WarningCount = 0;
		}
	}
}

