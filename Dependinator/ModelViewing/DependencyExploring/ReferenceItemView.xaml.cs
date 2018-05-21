using System.Windows.Controls;
using System.Windows.Input;


namespace Dependinator.ModelViewing.DependencyExploring
{
	/// <summary>
	/// Interaction logic for ReferenceItemView.xaml
	/// </summary>
	public partial class ReferenceItemView : UserControl
	{
		private ReferenceItemViewModel ViewModel => DataContext as ReferenceItemViewModel;

		public ReferenceItemView()
		{
			InitializeComponent();
		}


		private void UIElement_OnMouseEnter(object sender, MouseEventArgs e) => 
			ViewModel?.OnMouseEnter();

		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeave();
	}
}
