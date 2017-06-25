using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interactivity;


namespace Dependinator.ModelViewing.Nodes
{
	/// <summary>
	/// Captures and eats MouseWheel events so that a nested ListBox does not
	/// prevent an outer scrollable control from scrolling.
	/// </summary>
	public sealed class IgnoreMouseWheelBehavior : Behavior<UIElement>
	{

		protected override void OnAttached()
		{
			base.OnAttached();
			AssociatedObject.PreviewMouseWheel += AssociatedObject_PreviewMouseWheel;
			//AssociatedObject.PreviewTouchDown += AssociatedObject_PreviewTouchDown;
			//AssociatedObject.PreviewTouchUp += AssociatedObject_PreviewTouchUp;
			//AssociatedObject.PreviewTouchMove += AssociatedObject_PreviewTouchMove;
		}


		protected override void OnDetaching()
		{
			AssociatedObject.PreviewMouseWheel -= AssociatedObject_PreviewMouseWheel;
			//AssociatedObject.PreviewTouchDown -= AssociatedObject_PreviewTouchDown;
			//AssociatedObject.PreviewTouchUp -= AssociatedObject_PreviewTouchUp;
			//AssociatedObject.PreviewTouchMove -= AssociatedObject_PreviewTouchMove;

			base.OnDetaching();
		}


		private void AssociatedObject_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			var sourceDataContext = GetDataContext(sender);
			var targetDataContext = GetDataContext(e.OriginalSource);

			if (sourceDataContext != null && sourceDataContext == targetDataContext)
			{
				// The event was tunneled to the target element, lets bubble the event down again
				// This prevents the list box scroll viewer to "eat" the mouse wheel event
				e.Handled = true;

				var e2 = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
				e2.RoutedEvent = UIElement.MouseWheelEvent;

				AssociatedObject.RaiseEvent(e2);
			}	
		}


		private void AssociatedObject_PreviewTouchDown(object sender, TouchEventArgs e)
		{
			var sourceDataContext = GetDataContext(sender);
			var targetDataContext = GetDataContext(e.OriginalSource);

			if (sourceDataContext != null && sourceDataContext == targetDataContext)
			{
				// The event was tunneled to the target element, lets bubble the event down again
				// This prevents the list box scroll viewer to "eat" the mouse wheel event
				e.Handled = true;

				var e2 = new TouchEventArgs(e.TouchDevice, e.Timestamp);
				e2.RoutedEvent = UIElement.TouchDownEvent;

				AssociatedObject.RaiseEvent(e2);
			}
		}


		private void AssociatedObject_PreviewTouchUp(object sender, TouchEventArgs e)
		{
			var sourceDataContext = GetDataContext(sender);
			var targetDataContext = GetDataContext(e.OriginalSource);

			if (sourceDataContext != null && sourceDataContext == targetDataContext)
			{
				// The event was tunneled to the target element, lets bubble the event down again
				// This prevents the list box scroll viewer to "eat" the mouse wheel event
				e.Handled = true;

				var e2 = new TouchEventArgs(e.TouchDevice, e.Timestamp);
				e2.RoutedEvent = UIElement.TouchUpEvent;

				AssociatedObject.RaiseEvent(e2);
			}
		}


		private void AssociatedObject_PreviewTouchMove(object sender, TouchEventArgs e)
		{
			var sourceDataContext = GetDataContext(sender);
			var targetDataContext = GetDataContext(e.OriginalSource);

			if (sourceDataContext != null && sourceDataContext == targetDataContext)
			{
				// The event was tunneled to the target element, lets bubble the event down again
				// This prevents the list box scroll viewer to "eat" the mouse wheel event
				e.Handled = true;

				var e2 = new TouchEventArgs(e.TouchDevice, e.Timestamp);
				e2.RoutedEvent = UIElement.TouchMoveEvent;

				AssociatedObject.RaiseEvent(e2);
			}
		}


		private static ModelViewModel GetDataContext(object instance)
		{
			var element = instance as FrameworkElement;
			return element?.DataContext as ModelViewModel;
		}
	}
}
