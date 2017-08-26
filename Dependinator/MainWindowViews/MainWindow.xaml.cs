using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;
using Dependinator.ApplicationHandling;
using Dependinator.ApplicationHandling.SettingsHandling;
using Dependinator.Utils;


namespace Dependinator.MainWindowViews
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	[SingleInstance]
	public partial class MainWindow : Window
	{
		private readonly WorkingFolder workingFolder;

		private readonly DispatcherTimer remoteCheckTimer = new DispatcherTimer();

		private readonly MainWindowViewModel viewModel;
		


		internal MainWindow(
			WorkingFolder workingFolder,
			Func<MainWindowViewModel> mainWindowViewModelProvider)
		{
			this.workingFolder = workingFolder;

			InitializeComponent();
	
			SetShowToolTipLonger();

			// Make sure maximize window does not cover the task bar
			MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight - 8;

			viewModel = mainWindowViewModelProvider();
			DataContext = viewModel;

			Activate();

			RestoreWindowSettings();
		}


		public bool IsNewVersionAvailable
		{
			set => viewModel.IsNewVersionVisible = value;
		}


		public void SetSearchFocus()
		{
			Search.SearchBox.Focus();
		}



		private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
		{
			await viewModel.FirstLoadAsync();

			StartRemoteCheck();
		}


		private void StartRemoteCheck()
		{
			int interval = Settings.Get<Options>().AutoRemoteCheckIntervalMin;

			if (interval == 0)
			{
				Log.Debug("AutoRemoteCheckIntervalMin is disabled");
				return;
			}

			Log.Debug($"AutoRemoteCheckIntervalMin is interval {interval}");

			remoteCheckTimer.Tick += RemoteCheck;
			remoteCheckTimer.Interval = TimeSpan.FromMinutes(interval);
			remoteCheckTimer.Start();
		}


		private void RemoteCheck(object sender, EventArgs e)
		{
			viewModel.AutoRemoteCheckAsync().RunInBackground();
		}


		protected override void OnActivated(EventArgs e)
		{
			viewModel.ActivateRefreshAsync().RunInBackground();
		}


		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			viewModel.WindowWith = (int)sizeInfo.NewSize.Width;
		}

		private void MainWindow_OnClosing(object sender, CancelEventArgs e)
		{
			viewModel.ClosingWindow();
			StoreWindowSettings();

			StoreLastUsedFolder();
		}



		private void StoreWindowSettings()
		{
			Settings.Edit<WorkFolderSettings>(settings =>
			{
				settings.WindowBounds = new Rect(Top, Left, Width, Height);
				settings.IsMaximized = WindowState == WindowState.Maximized;
			});
		}


		private void RestoreWindowSettings()
		{
			WorkFolderSettings settings = Settings.Get<WorkFolderSettings>();

			Rectangle rect = new Rectangle(
				(int)settings.WindowBounds.X, 
				(int)settings.WindowBounds.Y, 
				(int)settings.WindowBounds.Width, 
				(int)settings.WindowBounds.Height);

			// check if the saved bounds are nonzero and visible on any screen
			if (rect != Rectangle.Empty && IsVisibleOnAnyScreen(rect))
			{
				Top = settings.WindowBounds.X;
				Left = settings.WindowBounds.Y;
				Width = settings.WindowBounds.Width;
				Height = settings.WindowBounds.Height;
			}

			WindowState = settings.IsMaximized ? WindowState.Maximized : WindowState.Normal;
		}


		private bool IsVisibleOnAnyScreen(Rectangle rect)
		{
			foreach (Screen screen in Screen.AllScreens)
			{
				if (screen.WorkingArea.IntersectsWith(rect) && screen.WorkingArea.Top < rect.Top)
				{
					return true;
				}
			}

			return false;
		}


		private void StoreLastUsedFolder()
		{
			Settings.Edit<ProgramSettings>(s => s.LastUsedWorkingFolder = workingFolder);
		}


		private static void SetShowToolTipLonger()
		{
			ToolTipService.ShowDurationProperty.OverrideMetadata(
				typeof(DependencyObject), new FrameworkPropertyMetadata(int.MaxValue));
		}
	}
}

