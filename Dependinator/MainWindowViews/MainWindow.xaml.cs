﻿using System;
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
			set { viewModel.IsNewVersionVisible = value; }
		}


		public void SetSearchFocus()
		{
			Search.SearchBox.Focus();
		}


		public void SetRepositoryViewFocus()
		{
			RepositoryView.NodesView.ItemsListBox.Focus();
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

		private void MainWindow_OnClosing(object sender, CancelEventArgs e)
		{
			viewModel.ClosingWindow();
			StoreWindowSettings();

			StoreLasteUsedFolder();
		}



		private void StoreWindowSettings()
		{
			ProgramSettings settings = Settings.Get<ProgramSettings>();

			settings.Top = Top;
			settings.Left = Left;
			settings.Height = Height;
			settings.Width = Width;
			settings.IsMaximized = WindowState == WindowState.Maximized;

			Settings.Set(settings);
		}


		private void RestoreWindowSettings()
		{
			ProgramSettings settings = Settings.Get<ProgramSettings>();

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

