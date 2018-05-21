using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring
{
	/// <summary>
	/// Interaction logic for ReferencesDialog.xaml
	/// </summary>
	public partial class ReferencesDialog : Window
	{
		internal ReferencesDialog(IReferenceItemService referenceItemService, Window owner, Node node)
		: this(referenceItemService, owner, node, null)
		{
		}

		internal ReferencesDialog(IReferenceItemService referenceItemService, Window owner, Line line)
			: this(referenceItemService, owner, null, line)
		{
		}

		private ReferencesDialog(
			IReferenceItemService referenceItemService,
			Window owner,
			Node node,
			Line line)
		{
			Owner = owner;
			InitializeComponent();

			DataContext = new ReferencesViewModel(referenceItemService, node, line);
		}
	}
}
