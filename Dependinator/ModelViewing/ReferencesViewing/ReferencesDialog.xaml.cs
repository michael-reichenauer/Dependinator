using System.Collections.ObjectModel;
using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	/// <summary>
	/// Interaction logic for CrateBranchDialog.xaml
	/// </summary>
	public partial class ReferencesDialog : Window
	{
		private readonly ReferencesViewModel viewModel;


		internal ReferencesDialog(IReferenceItemService referenceItemService, Window owner, Node node)
		{
			Owner = owner;
			InitializeComponent();

			viewModel = new ReferencesViewModel(referenceItemService, node);
			DataContext = viewModel;
		}
	}
}
