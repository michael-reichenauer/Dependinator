using System.Windows.Controls;
using System.Windows.Input;


namespace Dependinator.ModelViewing.Nodes
{
	/// <summary>
	/// Interaction logic for MemberNodeView.xaml
	/// </summary>
	public partial class MemberNodeView : UserControl
	{
		private NodeViewModel ViewModel => DataContext as NodeViewModel;

		public MemberNodeView()
		{
			InitializeComponent();
		}

		protected override void OnMouseWheel(MouseWheelEventArgs e) => ViewModel.OnMouseWheel(this, e);
	}
}
