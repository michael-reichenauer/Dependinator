using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Utils.UI;



namespace Dependinator.ModelViewing.Nodes
{
	public partial class NamespaceView : UserControl
	{
		private static readonly double ZoomSpeed = 2000.0;

		private MouseClicked mouseClicked;

		private NodeViewModel ViewModel => DataContext as NodeViewModel;

		public NamespaceView()
		{
			InitializeComponent();
			mouseClicked = new MouseClicked(this, e => ViewModel?.MouseClicked(e));
		}


		protected override void OnMouseWheel(MouseWheelEventArgs e)
		{
			int wheelDelta = e.Delta;
			double zoom = Math.Pow(2, wheelDelta / ZoomSpeed);

			Point viewPosition = e.GetPosition(Application.Current.MainWindow);
			ViewModel.ZoomRoot(zoom, viewPosition);
			
			e.Handled = true;
		}
	}
}
