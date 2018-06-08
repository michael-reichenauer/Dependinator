using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	/// <summary>
	/// Interaction logic for DependencyExplorerWindow.xaml
	/// </summary>
	public partial class DependencyExplorerWindow : Window
	{
		internal DependencyExplorerWindow(
			IDependencyWindowService dependencyWindowService, Window owner, Node node, Line line)
		{
			Owner = owner;
			InitializeComponent();

			DataContext = new DependencyExplorerWindowViewModel(dependencyWindowService, owner, node, line);
		}
	}
}
