using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Nodes
{
	/// <summary>
	/// Interaction logic for TypeView.xaml
	/// </summary>
	public partial class TypeView : UserControl
	{
		private NodeViewModel ViewModel => DataContext as NodeViewModel;

		public TypeView()
		{
			InitializeComponent();
		}

		protected override void OnMouseWheel(MouseWheelEventArgs e) => ViewModel.OnMouseWheel(this, e);
	}
}
