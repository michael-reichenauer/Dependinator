using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Private.Nodes
{
	/// <summary>
	/// Interaction logic for TypeView.xaml
	/// </summary>
	public partial class TypeView : UserControl
	{
		private NodeViewModel ViewModel => DataContext as NodeViewModel;
		private MouseClicked mouseClicked;

		public TypeView()
		{
			InitializeComponent();
			mouseClicked = new MouseClicked(this, e => ViewModel?.MouseClicked(e));
		}

		protected override void OnMouseWheel(MouseWheelEventArgs e) => ViewModel.OnMouseWheel(this, e);
	}
}
