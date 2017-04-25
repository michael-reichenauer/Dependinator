using System.Windows.Controls;
using System.Windows.Input;
using Dependiator.Modeling.Nodes;


namespace Dependiator.Modeling.Links
{
	/// <summary>
	/// Interaction logic for LinkSegmentView.xaml
	/// </summary>
	public partial class LinkSegmentView : UserControl
	{
		public LinkSegmentView()
		{
			InitializeComponent();
		}



		private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2 && DataContext is LinkSegmentViewModel viewModel)
			{
				viewModel.ToggleLine();
				e.Handled = true;
			}
		}
	}
}
