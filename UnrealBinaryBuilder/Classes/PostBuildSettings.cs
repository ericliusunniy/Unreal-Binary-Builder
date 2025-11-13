using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UnrealBinaryBuilder.UserControls;

namespace UnrealBinaryBuilder.Classes
{
	public class PostBuildSettings
	{
		Task ZippingTask = null;
		private CancellationTokenSource ZipCancelTokenSource = new CancellationTokenSource();
		private CancellationToken ZipCancelToken;
		MainWindow mainWindow = null;

		public PostBuildSettings(MainWindow _mainWindow)
		{
			mainWindow = _mainWindow;
			ZipCancelToken = ZipCancelTokenSource.Token;
		}

		public bool CanSaveToZip()
		{
			return ShouldSaveToZip() && DirectoryIsWritable(Path.GetDirectoryName(mainWindow.ZipPath.Text));
		}

		public bool ShouldSaveToZip()
		{
			return (bool)mainWindow.bZipBuild.IsChecked && !string.IsNullOrEmpty(mainWindow.ZipPath.Text);
		}

		public bool DirectoryIsWritable(string DirectoryPath)
		{
			if (string.IsNullOrWhiteSpace(DirectoryPath))
			{
				return false;
			}

			DirectoryInfo ZipDirectory = new DirectoryInfo(DirectoryPath);
			bool bDirectoryExists = ZipDirectory.Exists;
			bool bHasWriteAccess = false;
			if (bDirectoryExists)
			{
				try
				{
					AuthorizationRuleCollection collection = new DirectoryInfo(ZipDirectory.FullName).GetAccessControl().GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount));
					foreach (FileSystemAccessRule rule in collection)
					{
						if (rule.AccessControlType == AccessControlType.Allow)
						{
							bHasWriteAccess = true;
							break;
						}
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Failed to check directory write access: {ex.Message}");
				}
			}

			return bDirectoryExists && bHasWriteAccess;
		}

		public void PrepareToSave()
		{
			if (!ZipCancelTokenSource.IsCancellationRequested)
			{
				ZipCancelTokenSource.Cancel();
			}
			ZipCancelTokenSource.Dispose();
			ZipCancelTokenSource = new CancellationTokenSource();
			ZipCancelToken = ZipCancelTokenSource.Token;
		}

		public async void SavePluginToZip(PluginCard pluginCard, string ZipLocationToSave, bool bZipForMarketplace)
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				GameAnalyticsCSharp.AddProgressStart("PluginZip", "Progress");
			});

			ZippingTask = Task.Run(() =>
			{
				IEnumerable<string> files = Directory.EnumerateFiles(pluginCard.DestinationPath, "*.*", SearchOption.AllDirectories);
				List<string> filesToAdd = new List<string>();

				foreach (string file in files)
				{
					string CurrentFilePath = Path.GetFullPath(file).ToLower();
					if (bZipForMarketplace && (CurrentFilePath.Contains(@"\binaries\") || CurrentFilePath.Contains(@"\intermediate\")))
					{
						continue;
					}

					filesToAdd.Add(file);
				}
				Application.Current.Dispatcher.Invoke(() =>
				{
					pluginCard.ZipProgressbar.IsIndeterminate = false;
					pluginCard.ZipProgressbar.Value = 0;
					pluginCard.ZipProgressbar.Maximum = filesToAdd.Count;
				});

				using (FileStream zipToOpen = new FileStream(ZipLocationToSave, FileMode.Create))
				{
					using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
					{
						int entryIndex = 0;
						foreach (string file in filesToAdd)
						{
							ZipCancelToken.ThrowIfCancellationRequested();
							
							string relativePath = Path.GetRelativePath(pluginCard.DestinationPath, file);
							relativePath = relativePath.Replace('\\', '/');
							ZipArchiveEntry entry = archive.CreateEntry(relativePath, CompressionLevel.Fastest);
							
							using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
							using (Stream entryStream = entry.Open())
							{
								fileStream.CopyTo(entryStream);
							}

							entryIndex++;
							Application.Current.Dispatcher.Invoke(() =>
							{
								pluginCard.ZipProgressbar.Value = entryIndex;
							});
						}
					}
				}

				Application.Current.Dispatcher.Invoke(() =>
				{
					GameAnalyticsCSharp.AddProgressEnd("PluginZip", "Progress");
					mainWindow.AddLogEntry($"Plugin zipped and saved to: {ZipLocationToSave}");
				});
			}, ZipCancelToken);

			try
			{
				await ZippingTask;
			}
			catch (OperationCanceledException)
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					mainWindow.AddLogEntry("Plugin zip operation was canceled.");
				});
			}
		}

		public async void SaveToZip(string InBuildDirectory, string ZipLocationToSave)
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				GameAnalyticsCSharp.AddProgressStart("Zip", "Progress");
				mainWindow.TotalResult.Content = "Hold on...Stats will be displayed soon.";
				mainWindow.CurrentFileSaving.Content = "Waiting...";
				mainWindow.FileSaveState.Content = "State: Preparing...";
				mainWindow.ZipStatusLabel.Visibility = Visibility.Collapsed;
				mainWindow.ZipStausStackPanel.Visibility = Visibility.Visible;
			});

			System.IO.Compression.CompressionLevel compressionLevel = (bool)mainWindow.bFastCompression.IsChecked ? 
				System.IO.Compression.CompressionLevel.Fastest : 
				System.IO.Compression.CompressionLevel.Optimal;

			ZippingTask = Task.Run(() =>
			{
				Application.Current.Dispatcher.Invoke(() => { mainWindow.FileSaveState.Content = $"State: Be Patient! This might take a long time. Ninjas are finding files in {InBuildDirectory}"; });
				IEnumerable<string> files = Directory.EnumerateFiles(InBuildDirectory, "*.*", SearchOption.AllDirectories);

				ZipCancelToken.ThrowIfCancellationRequested();

				List<string> filesToAdd = new List<string>();

				int SkippedFiles = 0;
				int AddedFiles = 0;
				int TotalFiles = files.Count();

				long TotalSize = 0;
				long TotalSizeToZip = 0;
				long SkippedSize = 0;
				string TotalSizeInString = "0B";
				string TotalSizeToZipInString = "0B";
				string SkippedSizeToZipInString = "0B";
				Application.Current.Dispatcher.Invoke(() => { mainWindow.FileSaveState.Content = "State: Preparing files for zipping..."; });
				foreach (string file in files)
				{
					bool bSkipFile = false;
					string CurrentFilePath = Path.GetFullPath(file).ToLower();
					if (mainWindow.bIncludePDB.IsChecked == false && Path.GetExtension(file).ToLower() == ".pdb")
					{
						bSkipFile = true;
					}

					if (mainWindow.bIncludeDEBUG.IsChecked == false && Path.GetExtension(file).ToLower() == ".debug")
					{
						bSkipFile = true;
					}

					if (mainWindow.bIncludeDocumentation.IsChecked == false && !CurrentFilePath.Contains(@"\source\") && CurrentFilePath.Contains(@"\documentation\"))
					{
						bSkipFile = true;
					}

					if (mainWindow.bIncludeExtras.IsChecked == false && !CurrentFilePath.Contains(@"\extras\redist\") && CurrentFilePath.Contains(@"\extras\"))
					{
						bSkipFile = true;
					}

					if (mainWindow.bIncludeSource.IsChecked == false && (CurrentFilePath.Contains(@"\source\developer\") ||
					                                                    CurrentFilePath.Contains(@"\source\editor\") ||
					                                                    CurrentFilePath.Contains(@"\source\programs\") ||
					                                                    CurrentFilePath.Contains(@"\source\runtime\") ||
					                                                    CurrentFilePath.Contains(@"\source\thirdparty\")))
					{
						bSkipFile = true;
					}

					if (mainWindow.bIncludeFeaturePacks.IsChecked == false && CurrentFilePath.Contains(@"\featurepacks\"))
					{
						bSkipFile = true;
					}

					if (mainWindow.bIncludeSamples.IsChecked == false && CurrentFilePath.Contains(@"\samples\"))
					{
						bSkipFile = true;
					}

					if (mainWindow.bIncludeTemplates.IsChecked == false && !CurrentFilePath.Contains(@"\source\") && !CurrentFilePath.Contains(@"\content\editor") && CurrentFilePath.Contains(@"\templates\"))
					{
						bSkipFile = true;
					}

					TotalSize += new FileInfo(file).Length;
					TotalSizeInString = BytesToString(TotalSize);
					if (bSkipFile)
					{
						SkippedFiles++;
						SkippedSize += new FileInfo(file).Length;
						SkippedSizeToZipInString = BytesToString(SkippedSize);
					}
					else
					{
						filesToAdd.Add(file);
						AddedFiles++;
						TotalSizeToZip += new FileInfo(file).Length;
						TotalSizeToZipInString = BytesToString(TotalSizeToZip);
					}

					Application.Current.Dispatcher.Invoke(() => { mainWindow.CurrentFileSaving.Content = string.Format("Total: {0}. Added: {1}. Skipped: {2}", TotalFiles, AddedFiles, SkippedFiles); });
					ZipCancelToken.ThrowIfCancellationRequested();
				}

				Application.Current.Dispatcher.Invoke(() =>
				{
					mainWindow.TotalResult.Content = string.Format("Total Size: {0}. To Zip: {1}. Skipped: {2}", TotalSizeInString, TotalSizeToZipInString, SkippedSizeToZipInString);
					mainWindow.FileSaveState.Content = "State: Verifying...";
					mainWindow.OverallProgressbar.Maximum = filesToAdd.Count;
					mainWindow.OverallProgressbar.IsIndeterminate = false;
					mainWindow.FileProgressbar.IsIndeterminate = false;
				});

				long ProcessedSize = 0;
				string ProcessSizeInString = "0B";
				int entryIndex = 0;

				Application.Current.Dispatcher.Invoke(() =>
				{
					mainWindow.FileSaveState.Content = string.Format("State: Saving zip file ({0} files) to {1}", TotalFiles, mainWindow.ZipPath.Text);
				});

				using (FileStream zipToOpen = new FileStream(ZipLocationToSave, FileMode.Create))
				{
					using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
					{
						foreach (string file in filesToAdd)
						{
							ZipCancelToken.ThrowIfCancellationRequested();

							string relativePath = Path.GetRelativePath(InBuildDirectory, file);
							relativePath = relativePath.Replace('\\', '/');
							ZipArchiveEntry entry = archive.CreateEntry(relativePath, compressionLevel);

							Application.Current.Dispatcher.Invoke(() =>
							{
								mainWindow.FileSaveState.Content = "State: Begin Writing...";
								mainWindow.CurrentFileSaving.Content = string.Format("Saving File: {0} ({1}/{2})", Path.GetFileName(file), entryIndex + 1, filesToAdd.Count);
								mainWindow.OverallProgressbar.Value = entryIndex + 1;
							});

							using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
							using (Stream entryStream = entry.Open())
							{
								long fileSize = fileStream.Length;
								long bytesRead = 0;
								byte[] buffer = new byte[8192];
								int bytesReadThisTime;

								while ((bytesReadThisTime = fileStream.Read(buffer, 0, buffer.Length)) > 0)
								{
									ZipCancelToken.ThrowIfCancellationRequested();
									entryStream.Write(buffer, 0, bytesReadThisTime);
									bytesRead += bytesReadThisTime;

									Application.Current.Dispatcher.Invoke(() =>
									{
										mainWindow.FileSaveState.Content = "State: Writing...";
										if (fileSize > 0)
										{
											mainWindow.FileProgressbar.Value = (int)((bytesRead * 100) / fileSize);
										}
									});
								}
							}

							ProcessedSize += new FileInfo(file).Length;
							ProcessSizeInString = BytesToString(ProcessedSize);
							entryIndex++;

							Application.Current.Dispatcher.Invoke(() =>
							{
								mainWindow.TotalResult.Content = string.Format("Total Size: {0}. To Zip: {1}. Skipped: {2}. Processed: {3}", TotalSizeInString, TotalSizeToZipInString, SkippedSizeToZipInString, ProcessSizeInString);
							});
						}
					}
				}

				Application.Current.Dispatcher.Invoke(() =>
				{
					GameAnalyticsCSharp.AddProgressEnd("Zip", "Progress");
					mainWindow.CurrentFileSaving.Visibility = mainWindow.OverallProgressbar.Visibility = mainWindow.CancelZipping.Visibility = Visibility.Collapsed;
					mainWindow.FileSaveState.Content = $"State: Saved to {ZipLocationToSave}";
					mainWindow.AddLogEntry($"Done zipping. {ZipLocationToSave}");
					mainWindow.TryShutdown();
				});
			}, ZipCancelToken);

			try
			{
				await ZippingTask;
			}
			catch (OperationCanceledException e)
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					mainWindow.CurrentFileSaving.Content = "";
					mainWindow.FileSaveState.Content = "Operation canceled.";
					mainWindow.AddLogEntry($"{nameof(OperationCanceledException)} with message: {e.Message}");
				});
			}
			finally
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					mainWindow.CancelZipping.Content = "Cancel Zipping";
					mainWindow.CancelZipping.IsEnabled = true;
				});
			}
		}

		public void CancelTask()
		{
			GameAnalyticsCSharp.AddProgressEnd("Zip", "Progress", true);
			mainWindow.CancelZipping.Content = "Canceling. Please wait...";
			mainWindow.CancelZipping.IsEnabled = false;
			if (ZipCancelTokenSource != null && !ZipCancelTokenSource.IsCancellationRequested)
			{
				ZipCancelTokenSource.Cancel();
			}
		}

		public static string BytesToString(long byteCount)
		{
			string[] suf = { "B", "KB", "MB", "GB", "TB" };
			if (byteCount == 0)
			{
				return "0" + suf[0];
			}
			long bytes = Math.Abs(byteCount);
			int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
			double num = Math.Round(bytes / Math.Pow(1024, place), 1);
			return (Math.Sign(byteCount) * num).ToString() + suf[place];
		}
	}
}
