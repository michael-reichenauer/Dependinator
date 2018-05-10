using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Dependinator.ModelViewing.ReferencesViewing
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
