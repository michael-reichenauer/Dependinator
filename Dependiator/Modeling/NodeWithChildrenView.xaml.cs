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
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling
{
	/// <summary>
	/// Interaction logic for NodeWithChildrenView.xaml
	/// </summary>
	public partial class NodeWithChildrenView : UserControl
	{
		public NodeWithChildrenView()
		{
			InitializeComponent();
		}


		private void UIElement_OnMouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				return;
			}

			NodeViewModel viewModel = DataContext as NodeViewModel;
			if (viewModel == null)
			{
				return;
			}

			int zoomDelta = e.Delta;
			Point viewPosition = e.GetPosition(sender as IInputElement);

			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
			{
				viewModel.Zoom(zoomDelta, viewPosition);
			}
			else
			{
				viewModel.Resize(zoomDelta, viewPosition);
			}

			e.Handled = true;
		}
	}
}
