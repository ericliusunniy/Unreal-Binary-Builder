using System;
using Sentry;

namespace UnrealBinaryBuilder.UserControls
{
	/// <summary>
	/// Interaction logic for CrashReporter.xaml
	/// </summary>
	public partial class CrashReporter
	{
		public SentryId CurrentSentryId;

		public CrashReporter(Exception InException)
		{
			InitializeComponent();
			Username.Text = Environment.UserName;
			string StackTraceMessage = $"Source ->\t{InException.Source}\nMessage ->\t{InException.Message}\nTarget ->\t{InException.TargetSite}\nStackTrace ->\n{InException.StackTrace}";
			StackTraceText.Text = StackTraceMessage;
		}

		private void SubmitBtn_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			string CommentText = $"{Comment.Text}\n\nExceptionDetails ->\n{StackTraceText.Text}";
			SentryFeedback feedback = new SentryFeedback(CurrentSentryId.ToString(), Username.Text, Email.Text, CommentText);
			SentrySdk.CaptureFeedback(feedback);
			HandyControl.Controls.MessageBox.Success("Thank you for submitting the crash report!");
			Close();
		}

		private void CancelBtn_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Close();
		}
	}
}
