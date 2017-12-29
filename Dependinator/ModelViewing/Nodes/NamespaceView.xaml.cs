using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Utils.UI;



namespace Dependinator.ModelViewing.Nodes
{
	public partial class NamespaceView : UserControl
	{
		private MouseClicked mouseClicked;

		private NodeViewModel ViewModel => DataContext as NodeViewModel;

		public NamespaceView()
		{
			InitializeComponent();
			mouseClicked = new MouseClicked(this, e => ViewModel?.MouseClicked(e));
		}
		
		protected override void OnMouseWheel(MouseWheelEventArgs e) => ViewModel.OnMouseWheel(this, e);
	}
}
