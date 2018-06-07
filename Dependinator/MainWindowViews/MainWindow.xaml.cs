using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Dependinator.Common;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.UI;


namespace Dependinator.MainWindowViews
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	[SingleInstance]
	public partial class MainWindow : Window, IMainWindow
	{
		private readonly ISettingsService settingsService;
		private readonly ModelMetadata folder;

		private readonly MainWindowViewModel viewModel;


		internal MainWindow(
			ISettingsService settingsService,
			Func<MainWindowViewModel> mainWindowViewModelProvider,
			ModelMetadata folder)
		{
			this.settingsService = settingsService;
			this.folder = folder;

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
			settingsService.Edit<WorkFolderSettings>(s =>
			{
				s.MainWindowBounds = new Rect(Left, Top, Width, Height);
				s.IsMaximized = WindowState == WindowState.Maximized;
			});
		}


		public void RestoreWindowSettings()
		{
			ResizeMode = folder.IsDefault ? ResizeMode.NoResize : ResizeMode.CanResize;
			WorkFolderSettings s = settingsService.Get<WorkFolderSettings>();

			Rectangle rect = new Rectangle(
				(int)s.MainWindowBounds.X,
				(int)s.MainWindowBounds.Y,
				(int)s.MainWindowBounds.Width,
				(int)s.MainWindowBounds.Height);

			// check if the saved bounds are nonzero and visible on any screen
			if (rect != Rectangle.Empty && VisibleWindow.IsVisibleOnAnyScreen(rect))
			{
				Left = s.MainWindowBounds.X;
				Top = s.MainWindowBounds.Y;
				Width = s.MainWindowBounds.Width;
				Height = s.MainWindowBounds.Height;
			}

			if (folder.IsDefault)
			{
				Width = 800;
				Height = 680;
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

