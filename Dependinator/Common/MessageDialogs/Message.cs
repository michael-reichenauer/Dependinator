using System.Media;
using System.Windows;


namespace Dependinator.Common.MessageDialogs
{
	internal static class Message
	{
		public static void ShowInfo(string message, string title = null) =>
			ShowDialog(null, message, title, MessageBoxButton.OK, MessageBoxImage.Information);


		public static void ShowInfo(Window owner, string message, string title = null) =>
			ShowDialog(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);


		public static bool ShowAskOkCancel(string message, string title = null) =>
			ShowDialog(null, message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == true;

		public static bool ShowAskOkCancel(
			Window owner, string message, string title = null, MessageBoxImage image = MessageBoxImage.Question) =>
			ShowDialog(owner, message, title, MessageBoxButton.OKCancel, image) == true;


		public static void ShowWarning(Window owner, string message, string title = null)
		{
			ShowDialog(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
		}

		public static bool ShowWarningAskYesNo(Window owner, string message, string title = null) =>
			ShowDialog(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == true;

		public static void ShowError(string message, string title = null)
		{
			SystemSounds.Beep.Play();
			ShowDialog(null, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
		}


		public static void ShowError(
			Window owner,
			string message,
			string title = null)
		{
			SystemSounds.Beep.Play();

			ShowDialog(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
		}


		public static bool? ShowDialog(
			Window owner,
			string message,
			string title,
			MessageBoxButton button,
			MessageBoxImage image)
		{
			title = title ?? Program.Name;

			if (Application.Current?.MainWindow == null)
			{
				MessageBoxResult result = MessageBox.Show(message, title, button, image);
				return result == MessageBoxResult.OK || result == MessageBoxResult.Yes;
			}

			MessageDialog dialog = new MessageDialog(owner, message, title, button, image);
			return dialog.ShowDialog();
		}
	}
}