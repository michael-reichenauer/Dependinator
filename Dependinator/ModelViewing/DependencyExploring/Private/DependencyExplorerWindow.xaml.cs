using System;
using System.ComponentModel;
using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	/// <summary>
	/// Interaction logic for DependencyExplorerWindow.xaml
	/// </summary>
	public partial class DependencyExplorerWindow : Window
	{
		private readonly IModelNotifications modelNotifications;
		private DependencyExplorerWindowViewModel viewModel;

		internal DependencyExplorerWindow(
			IDependencyWindowService dependencyWindowService,
			IModelNotifications modelNotifications,
			Window owner, 
			Node node, 
			Line line)
		{
			this.modelNotifications = modelNotifications;
			Owner = owner;
			InitializeComponent();

			viewModel = new DependencyExplorerWindowViewModel(dependencyWindowService, node, line);
			DataContext = viewModel;
			modelNotifications.ModelUpdated += OnModelChanged;
		}


		private void OnModelChanged(object sender, EventArgs e) => viewModel.ModelChanged();


		protected override void OnClosing(CancelEventArgs e)
		{
			modelNotifications.ModelUpdated -= OnModelChanged;
			base.OnClosing(e);
		}
	}
}
