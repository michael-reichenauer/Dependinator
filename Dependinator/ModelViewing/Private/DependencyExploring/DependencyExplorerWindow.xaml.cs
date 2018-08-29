using System;
using System.ComponentModel;
using System.Windows;
using Dependinator.Common;
using Dependinator.ModelViewing.Private.DependencyExploring.Private;
using Dependinator.ModelViewing.Private.ModelHandling;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.DependencyExploring
{
    /// <summary>
    /// Interaction logic for DependencyExplorerWindow.xaml
    /// </summary>
    public partial class DependencyExplorerWindow : Window
    {
        private readonly IModelNotifications modelNotifications;
        private readonly DependencyExplorerWindowViewModel viewModel;


        internal DependencyExplorerWindow(
            IDependencyWindowService dependencyWindowService,
            IModelNotifications modelNotifications,
            WindowOwner owner,
            Node node,
            Line line)
        {
            this.modelNotifications = modelNotifications;
            Owner = owner;
            InitializeComponent();
            Loaded += Window_Loaded;

            viewModel = new DependencyExplorerWindowViewModel(dependencyWindowService, node, line);
            DataContext = viewModel;
            modelNotifications.ModelUpdated += OnModelChanged;
        }


        protected override void OnClosing(CancelEventArgs e)
        {
            modelNotifications.ModelUpdated -= OnModelChanged;
            base.OnClosing(e);
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Try to ensure that multiple windows are not at exactly same position (hiding lower)
            Random random = new Random();
            Left = Math.Max(10, Left + random.Next(-100, 100));
            Top = Math.Max(10, Top + random.Next(-100, 100));
        }


        private void OnModelChanged(object sender, EventArgs e) => viewModel.ModelChanged();


        private void Suppressed_OnClick(object sender, RoutedEventArgs e)
        {
            SuppressedContextMenu.PlacementTarget = this;
            SuppressedContextMenu.IsOpen = true;
        }
    }
}
