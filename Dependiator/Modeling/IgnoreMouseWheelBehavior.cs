﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interactivity;


namespace Dependiator.Modeling
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
		}

		protected override void OnDetaching()
		{
			AssociatedObject.PreviewMouseWheel -= AssociatedObject_PreviewMouseWheel;
			base.OnDetaching();
		}

		public void AssociatedObject_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{

			//AssociatedObject.RaiseEvent(e);

			//e.Handled = true;

			//var e2 = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
			//e2.RoutedEvent = UIElement.MouseWheelEvent;

			//UIElement uiElement = AssociatedObject;

			////(sender as UIElement).RaiseEvent(e2);
			//uiElement.RaiseEvent(e2);

		}

	}
}
