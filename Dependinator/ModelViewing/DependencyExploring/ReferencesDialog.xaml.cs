using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring
{
	/// <summary>
	/// Interaction logic for ReferencesDialog.xaml
	/// </summary>
	public partial class ReferencesDialog : Window
	{
		internal ReferencesDialog(IDependenciesService dependenciesService, Window owner, Node node)
		: this(dependenciesService, owner, node, null)
		{
		}

		internal ReferencesDialog(IDependenciesService dependenciesService, Window owner, Line line)
			: this(dependenciesService, owner, null, line)
		{
		}

		private ReferencesDialog(
			IDependenciesService dependenciesService,
			Window owner,
			Node node,
			Line line)
		{
			Owner = owner;
			InitializeComponent();

			DataContext = new ReferencesViewModel(dependenciesService, owner, node, line);
		}
	}
}
