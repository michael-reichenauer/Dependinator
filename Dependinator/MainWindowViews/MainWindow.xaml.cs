using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils;
using Dependinator.Utils.UI;


namespace Dependinator.MainWindowViews
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	[SingleInstance]
	public partial class MainWindow : Window
	{
		private readonly ISettings settings;

		private readonly MainWindowViewModel viewModel;


		internal MainWindow(ISettings settings, Func<MainWindowViewModel> mainWindowViewModelProvider)
		{
			this.settings = settings;

			InitializeComponent();
			SetShowToolTipLonger();

			// Make sure maximize window does not cover the task bar
			MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight - 8;

			viewModel = mainWindowViewModelProvider();
			DataContext = viewModel;

			// Bring the window to the foreground and activates
			Activate();

			RestoreWindowSettings();
		}


		public bool IsNewVersionAvailable { set => viewModel.IsNewVersionVisible = value; }

		public void SetSearchFocus() => Search.SearchBox.Focus();

		private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e) =>
			await viewModel.LoadAsync();

		protected override void OnActivated(EventArgs e) =>
			viewModel.ActivateRefreshAsync().RunInBackground();


		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			viewModel.WindowWith = (int)sizeInfo.NewSize.Width;
		}


		private void MainWindow_OnClosing(object sender, CancelEventArgs e)
		{
			viewModel.ClosingWindow();
			StoreWindowSettings();
		}


		private void StoreWindowSettings()
		{
			settings.Edit<WorkFolderSettings>(s =>
			{
				s.WindowBounds = new Rect(Top, Left, Width, Height);
				s.IsMaximized = WindowState == WindowState.Maximized;
			});
		}


		private void RestoreWindowSettings()
		{
			WorkFolderSettings s = settings.Get<WorkFolderSettings>();

			Rectangle rect = new Rectangle(
				(int)s.WindowBounds.X,
				(int)s.WindowBounds.Y,
				(int)s.WindowBounds.Width,
				(int)s.WindowBounds.Height);

			// check if the saved bounds are nonzero and visible on any screen
			if (rect != Rectangle.Empty && VisibleWindow.IsVisibleOnAnyScreen(rect))
			{
				Top = s.WindowBounds.X;
				Left = s.WindowBounds.Y;
				Width = s.WindowBounds.Width;
				Height = s.WindowBounds.Height;
			}

			WindowState = s.IsMaximized ? WindowState.Maximized : WindowState.Normal;
		}


		public static void SetShowToolTipLonger()
		{
			ToolTipService.ShowDurationProperty.OverrideMetadata(
				typeof(DependencyObject), new FrameworkPropertyMetadata(int.MaxValue));
		}
	}
}

