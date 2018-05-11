using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	/// <summary>
	/// Interaction logic for ReferencesDialog.xaml
	/// </summary>
	public partial class ReferencesDialog : Window
	{
		internal ReferencesDialog(
			Window owner, Node node, IEnumerable<ReferenceItem> referenceItems, bool isIncoming)
		{
			Owner = owner;
			InitializeComponent();

			DataContext = new ReferencesViewModel(node, referenceItems, isIncoming);
		}
	}
}
