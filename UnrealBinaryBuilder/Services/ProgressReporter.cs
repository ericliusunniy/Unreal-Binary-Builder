using System;
using System.Windows;

namespace UnrealBinaryBuilder.Services
{
	/// <summary>
	/// 进度报告器 - 报告操作进度
	/// </summary>
	public class ProgressReporter
	{
		private readonly Action<string> _onStatusChanged;
		private readonly Action<int, int> _onProgressChanged;
		private readonly Action<double> _onPercentageChanged;

		public ProgressReporter(
			Action<string> onStatusChanged = null,
			Action<int, int> onProgressChanged = null,
			Action<double> onPercentageChanged = null)
		{
			_onStatusChanged = onStatusChanged;
			_onProgressChanged = onProgressChanged;
			_onPercentageChanged = onPercentageChanged;
		}

		/// <summary>
		/// 报告状态
		/// </summary>
		public void ReportStatus(string status)
		{
			if (string.IsNullOrWhiteSpace(status))
				return;

			Application.Current?.Dispatcher.InvokeAsync(() =>
			{
				_onStatusChanged?.Invoke(status);
			});
		}

		/// <summary>
		/// 报告进度
		/// </summary>
		public void ReportProgress(int current, int total)
		{
			Application.Current?.Dispatcher.InvokeAsync(() =>
			{
				_onProgressChanged?.Invoke(current, total);
				if (total > 0)
				{
					double percentage = (double)current / total * 100;
					_onPercentageChanged?.Invoke(percentage);
				}
			});
		}

		/// <summary>
		/// 报告百分比
		/// </summary>
		public void ReportPercentage(double percentage)
		{
			Application.Current?.Dispatcher.InvokeAsync(() =>
			{
				_onPercentageChanged?.Invoke(percentage);
			});
		}

		/// <summary>
		/// 报告完成
		/// </summary>
		public void ReportComplete()
		{
			ReportStatus("完成");
			ReportPercentage(100);
		}
	}
}

