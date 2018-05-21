using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	/// <summary>
	/// Interaction logic for DependencyItemView.xaml
	/// </summary>
	public partial class DependencyItemView : UserControl
	{
		private DependencyItemViewModel ViewModel => DataContext as DependencyItemViewModel;

		public DependencyItemView()
		{
			InitializeComponent();
		}


		private void UIElement_OnMouseEnter(object sender, MouseEventArgs e) => 
			ViewModel?.OnMouseEnter();

		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeave();

		private void ToolTip_OnOpened(object sender, RoutedEventArgs e) => ViewModel?.UpdateToolTip();
	}
}
