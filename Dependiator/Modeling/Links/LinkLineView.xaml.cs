using System.Windows.Controls;
using System.Windows.Input;
using Dependiator.Modeling.Nodes;


namespace Dependiator.Modeling.Links
{
	/// <summary>
	/// Interaction logic for LinkLineView.xaml
	/// </summary>
	public partial class LinkLineView : UserControl
	{
		public LinkLineView()
		{
			InitializeComponent();
		}



		private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2 && DataContext is LinkLineViewModel viewModel)
			{
				viewModel.ToggleLine();
				e.Handled = true;
			}
		}
	}
}
