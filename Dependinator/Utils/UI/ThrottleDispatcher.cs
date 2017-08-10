using System;
using System.Windows.Threading;

namespace Dependinator.Utils.UI
{
	/// <summary>
	/// Provides Throttle() methods.
	/// Use these methods to ensure that events aren't handled too frequently.
	/// 
	/// Throttle() ensures that events are throttled by the interval specified.
	/// Only the last event in the interval sequence of events fires.
	/// 
	/// Note: https://weblog.west-wind.com/posts/2017/Jul/02/Debouncing-and-Throttling-Dispatcher-Events
	/// </summary>
	public class ThrottleDispatcher
	{
		private DispatcherTimer timer;
		private DateTime timerStarted { get; set; } = DateTime.UtcNow.AddYears(-1);


		/// <summary>
		/// This method throttles events by allowing only 1 event to fire for the given
		/// timeout period. Only the last event fired is handled - all others are ignored.
		/// Throttle will fire events every timeout ms even if additional events are pending.
		/// 
		/// Use Throttle where you need to ensure that events fire at given intervals.
		/// </summary>
		/// <param name="interval">Timeout in Milliseconds</param>
		/// <param name="action">Action<object> to fire when debounced event fires</object></param>
		/// <param name="param">optional parameter</param>
		/// <param name="priority">optional priorty for the dispatcher</param>
		/// <param name="disp">optional dispatcher. If not passed or null CurrentDispatcher is used.</param>
		public void Throttle(int interval, Action<object> action,
			object param = null,
			DispatcherPriority priority = DispatcherPriority.ApplicationIdle,
			Dispatcher disp = null)
		{
			// kill pending timer and pending ticks
			timer?.Stop();
			timer = null;

			if (disp == null)
				disp = Dispatcher.CurrentDispatcher;

			var curTime = DateTime.UtcNow;

			// if timeout is not up yet - adjust timeout to fire 
			// with potentially new Action parameters           
			if (curTime.Subtract(timerStarted).TotalMilliseconds < interval)
				interval -= (int)curTime.Subtract(timerStarted).TotalMilliseconds;

			timer = new DispatcherTimer(TimeSpan.FromMilliseconds(interval), priority, (s, e) =>
			{
				if (timer == null)
					return;

				timer?.Stop();
				timer = null;
				action.Invoke(param);
			}, disp);

			timer.Start();
			timerStarted = curTime;
		}
	}
}