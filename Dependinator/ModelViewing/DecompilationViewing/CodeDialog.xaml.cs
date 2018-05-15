using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DecompilationViewing
{
	/// <summary>
	/// Interaction logic for ReferencesDialog.xaml
	/// </summary>
	public partial class CodeDialog : Window
	{
		internal CodeDialog(Window owner, Node node)
		{
			Owner = owner;
			InitializeComponent();

			DataContext = new CodeViewModel(node);
		}
	}
}
