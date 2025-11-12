using LibGit2Sharp;
using System.Windows;

namespace UnrealBinaryBuilder.Classes
{
    public static class Git
    {
		private static Repository repository = null;

		public static string CommitHash
		{
			get
			{
				UpdateRepository();
				return repository != null ? repository.Head.Tip.Sha : null;
			}
		}

		public static string CommitHashShort
	{
		get
		{
			if (string.IsNullOrWhiteSpace(CommitHash))
			{
				return null;
			}

			if (CommitHash.Length <= 7)
			{
				return CommitHash;
			}

			return CommitHash.Substring(0, 7);
		}
	}

		public static string BranchName
		{
			get
			{
				UpdateRepository();
				return repository != null ? repository.Head.FriendlyName : null;
			}
		}

		public static string TrackedBranchName
		{
			get
			{
				UpdateRepository();
				if (repository != null)
				{
					return repository.Head.IsTracking ? repository.Head.TrackedBranch.FriendlyName : null;
				}

				return null;
			}
		}

		private static void UpdateRepository()
		{
			MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
			if (repository == null && Repository.IsValid(mainWindow.SetupBatFilePath.Text))
			{
				repository = new Repository(mainWindow.SetupBatFilePath.Text);
			}
		}
	}
}
