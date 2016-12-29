using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using Dependiator.ApplicationHandling;
using Dependiator.ApplicationHandling.SettingsHandling;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;


namespace Dependiator.MainWindowViews
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
		private System.Windows.Point lastMousePosition;


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

			RestoreWindowSettings(workingFolder);
		}


		public bool IsNewVersionAvailable
		{
			set { viewModel.IsNewVersionVisible = value; }
		}


		public void SetSearchFocus()
		{
			Search.SearchBox.Focus();
		}


		public void SetRepositoryViewFocus()
		{
			RepositoryView.ItemsListBox.Focus();
		}


		private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
		{
			await viewModel.FirstLoadAsync();
			SetRepositoryViewFocus();
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


		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			//if ((Keyboard.Modifiers & ModifierKeys.Control) > 0)
			{
				// Adjust X in "e.Delta / X" to adjust zoom speed
				double zoom = Math.Pow(2, e.Delta / 10.0 / Mouse.MouseWheelDeltaForOneLine);

				ZoomableCanvas canvas = viewModel.MainViewModel.Canvas;
				double newScale = canvas.Scale * zoom;

				Log.Debug($"Zoom {zoom}, scale {canvas.Scale}, offset {canvas.Offset}");
				if (newScale < 0.5 || newScale > 10)
				{
					Log.Warn($"Zoom to large");
					e.Handled = true;
					return;
				}

				canvas.Scale = newScale;

				// Adjust the offset to make the point under the mouse stay still.
				Point point = e.GetPosition(RepositoryView.ItemsListBox);
				point = new Point(point.X - 10, point.Y - 30);
				Vector position = (Vector)point;
				canvas.Offset = (System.Windows.Point)((Vector)
					(canvas.Offset + position) * zoom - position);

				Log.Debug($"Scroll {zoom}, scale {canvas.Scale}, offset {canvas.Offset}");

				e.Handled = true;
			}
		}

		protected override void OnPreviewMouseMove(MouseEventArgs e)
		{
			System.Windows.Point position = e.GetPosition(RepositoryView.ItemsListBox);
			ZoomableCanvas canvas = viewModel.MainViewModel.Canvas;

			if (e.LeftButton == MouseButtonState.Pressed && position.Y < 0 )
			{
				ReleaseMouseCapture();
				return;
			}

			if (e.LeftButton == MouseButtonState.Pressed
					&& !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
			{
				Log.Debug($"Mouse {position}");
				CaptureMouse();
				canvas.Offset -= position - lastMousePosition;
				e.Handled = true;
			}
			else
			{
				ReleaseMouseCapture();
			}

			lastMousePosition = position;
		}



		private void MainWindow_OnClosed(object sender, EventArgs e)
		{
			StoreWindowSettings();

			StoreLasteUsedFolder();
		}


		private void StoreWindowSettings()
		{
			WorkFolderSettings settings = Settings.GetWorkFolderSetting(workingFolder);

			settings.Top = Top;
			settings.Left = Left;
			settings.Height = Height;
			settings.Width = Width;
			settings.IsMaximized = WindowState == WindowState.Maximized;
			settings.IsShowCommitDetails = viewModel.MainViewModel.IsShowCommitDetails;

			//settings.ShownBranches = viewModel.MainViewModel.Branches
			//	.Select(b => b.Branch.Name.ToString())
			//	.Distinct()
			//	.ToList();

			Settings.SetWorkFolderSetting(workingFolder, settings);
		}


		private void RestoreWindowSettings(string workingFolder)
		{
			WorkFolderSettings settings = Settings.GetWorkFolderSetting(workingFolder);

			Rectangle rect = new Rectangle(
				(int)settings.Left, (int)settings.Top, (int)settings.Width, (int)settings.Height);

			// check if the saved bounds are nonzero and visible on any screen
			if (rect != Rectangle.Empty && IsVisibleOnAnyScreen(rect))
			{
				Top = settings.Top;
				Left = settings.Left;
				Height = settings.Height;
				Width = settings.Width;
			}

			WindowState = settings.IsMaximized ? WindowState.Maximized : WindowState.Normal;

			viewModel.MainViewModel.IsShowCommitDetails = settings.IsShowCommitDetails;
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


		private void StoreLasteUsedFolder()
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

