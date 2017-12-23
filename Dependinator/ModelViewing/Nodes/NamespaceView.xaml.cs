using System.Windows.Controls;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Nodes
{
	public partial class NamespaceView : UserControl
	{
		private MouseClicked mouseClicked;

		private NodeViewModel ViewModel => DataContext as NodeViewModel;

		public NamespaceView()
		{
			InitializeComponent();
			mouseClicked = new MouseClicked(this, e => ViewModel?.MouseClicked(e));
		}
	}
}
