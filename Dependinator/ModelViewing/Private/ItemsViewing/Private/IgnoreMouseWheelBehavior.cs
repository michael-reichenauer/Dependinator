using System.Windows;
using System.Windows.Input;
using System.Windows.Interactivity;


namespace Dependinator.ModelViewing.Private.ItemsViewing.Private
{
    /// <summary>
    ///     Captures and eats MouseWheel events so that a nested ListBox does not
    ///     prevent an outer scrollable control from scrolling.
    /// </summary>
    public sealed class IgnoreMouseWheelBehavior : Behavior<UIElement>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseWheel += AssociatedObject_PreviewMouseWheel;
        }


        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseWheel -= AssociatedObject_PreviewMouseWheel;
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


        private static ItemsViewModel GetDataContext(object instance)
        {
            var element = instance as FrameworkElement;
            return element?.DataContext as ItemsViewModel;
        }
    }
}
