using System.Windows;
using Dependinator.ModelViewing.DependencyExploring.Private;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring
{
	/// <summary>
	/// Interaction logic for DependencyExplorerWindow.xaml
	/// </summary>
	public partial class DependencyExplorerWindow : Window
	{
		internal DependencyExplorerWindow(
			IDependenciesService dependenciesService, Window owner, Node node)
		: this(dependenciesService, owner, node, null)
		{
		}

		internal DependencyExplorerWindow(
			IDependenciesService dependenciesService, Window owner, Line line)
			: this(dependenciesService, owner, null, line)
		{
		}

		private DependencyExplorerWindow(
			IDependenciesService dependenciesService, Window owner, Node node, Line line)
		{
			Owner = owner;
			InitializeComponent();

			DataContext = new DependencyExplorerWindowViewModel(dependenciesService, owner, node, line);
		}
	}
}
