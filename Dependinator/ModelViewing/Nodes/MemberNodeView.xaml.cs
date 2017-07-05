using System.Windows.Controls;
using System.Windows.Input;


namespace Dependinator.ModelViewing.Nodes
{
	/// <summary>
	/// Interaction logic for MemberNodeView.xaml
	/// </summary>
	public partial class MemberNodeView : UserControl
	{
		public MemberNodeView()
		{
			InitializeComponent();
		}

		private void UIElement_OnMouseMove(object sender, MouseEventArgs e) =>
			(DataContext as NodeViewModel)?.OnMouseMove(e);
	}
}
