using System.Windows.Controls;
using System.Windows.Input;

namespace Dependinator.ModelViewing.Links
{
	/// <summary>
	/// Interaction logic for LineView.xaml
	/// </summary>
	public partial class LineView : UserControl
	{
		public LineView()
		{
			InitializeComponent();
		}

		private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
		{
		}

		private void UIElement_OnMouseEnter(object sender, MouseEventArgs e)
		{
			(DataContext as LineViewModel)?.OnMouseEnter();
		}

		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e)
		{
			(DataContext as LineViewModel)?.OnMouseLeave();
		}
	}
}
