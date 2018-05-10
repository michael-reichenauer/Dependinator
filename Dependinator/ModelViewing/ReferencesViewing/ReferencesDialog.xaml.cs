using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


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


		private void UIElement_OnMouseEnter(object sender, MouseEventArgs e)
		{
			ReferenceItemViewModel vm = (sender as FrameworkElement)?.DataContext as ReferenceItemViewModel;
			vm?.OnMouseEnter();
		}


		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e)
		{

			ReferenceItemViewModel vm = (sender as FrameworkElement)?.DataContext as ReferenceItemViewModel;
			vm?.OnMouseLeave();
		}
	}
}
