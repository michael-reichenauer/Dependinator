using System.Windows;
using UserControl = System.Windows.Controls.UserControl;


namespace Dependinator.MainViews
{
	/// <summary>
	/// Interaction logic for MainView.xaml
	/// </summary>
	public partial class MainView : UserControl
	{
		private MainViewModel viewModel;


		public MainView()
		{
			InitializeComponent();
		}


		private async void MainView_OnLoaded(object sender, RoutedEventArgs e)
		{
			viewModel = (MainViewModel)DataContext;
			NodesView.SetFocus();
			await viewModel.LoadAsync();
		}




		//protected override void OnTouchDown(TouchEventArgs e)
		//{
		//	// Touch move is starting
		//	CaptureMouse();

		//	TouchPoint viewPosition = e.GetTouchPoint(NodesView.ItemsListBox);
		//	lastMousePosition = viewPosition.Position;
		//	e.Handled = true;
		//	//isTouchMove = true;
		//}

		//protected override void OnTouchUp(TouchEventArgs e)
		//{
		//	// Touch move is ending
		//	ReleaseMouseCapture();

		//	e.Handled = true;
		//	//isTouchMove = false;
		//}


		//protected override void OnTouchMove(TouchEventArgs e)
		//{
		//	TouchPoint viewPosition = e.GetTouchPoint(NodesView.ItemsListBox);
		//	Vector offset = viewPosition.Position - lastMousePosition;

		//	e.Handled = viewModel.MoveCanvas(offset);
		//	lastMousePosition = viewPosition.Position;
		//}


		//protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
		//{
		//	// Log.Debug($"Canvas offset {canvas.Offset}");

		//	if (e.ChangedButton == MouseButton.Left)
		//	{
		//		Point viewPosition = e.GetPosition(NodesView.ItemsListBox);

		//		viewModel.Clicked(viewPosition);
		//	}

		//	base.OnPreviewMouseUp(e);
		//}
	}
}
