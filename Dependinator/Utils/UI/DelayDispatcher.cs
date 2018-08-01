using System;
using System.Windows.Threading;


namespace Dependinator.Utils.UI
{
    /// <summary>
    ///     Provides Delay() methods.
    ///     Use these methods to ensure that events aren't handled too frequently.
    ///     Delay() fires an event only after the specified interval has passed
    ///     in which no other pending event has fired. Only the last event in the
    ///     sequence is fired.
    ///     Based on:
    ///     https://weblog.west-wind.com/posts/2017/Jul/02/Debouncing-and-Throttling-Dispatcher-Events
    /// </summary>
    public class DelayDispatcher
    {
        private DispatcherTimer timer;


        /// <summary>
        ///     Delay an event by resetting the event timeout every time the event is
        ///     fired. The behavior is that the Action passed is fired only after events
        ///     stop firing for the given timeout period.
        ///     Use Delay when you want events to fire only after events stop firing
        ///     after the given interval timeout period.
        ///     Wrap the logic you would normally use in your event code into
        ///     the  Action you pass to this method to debounce the event.
        ///     Example: https://gist.github.com/RickStrahl/0519b678f3294e27891f4d4f0608519a
        /// </summary>
        /// <param name="delayTime">Timeout in Milliseconds</param>
        /// <param name="action">Action<object> to fire when debounced event fires</object></param>
        /// <param name="param">optional parameter</param>
        /// <param name="priority">optional priority for the dispatcher</param>
        /// <param name="dispatcher">
        ///     optional dispatcher. If not passed or null CurrentDispatcher is used.
        /// </param>
        public void Delay(TimeSpan delayTime, Action<object> action,
            object param = null,
            DispatcherPriority priority = DispatcherPriority.ApplicationIdle,
            Dispatcher dispatcher = null)
        {
            // kill pending timer and pending ticks
            timer?.Stop();
            timer = null;

            if (dispatcher == null)
                dispatcher = Dispatcher.CurrentDispatcher;

            // timer is recreated for each event and effectively
            // resets the timeout. Action only fires after timeout has fully
            // elapsed without other events firing in between
            timer = new DispatcherTimer(delayTime, priority, (s, e) =>
            {
                if (timer == null)
                    return;

                timer?.Stop();
                timer = null;
                action.Invoke(param);
            }, dispatcher);

            timer.Start();
        }


        public void Cancel()
        {
            // kill pending timer and pending ticks
            timer?.Stop();
            timer = null;
        }
    }
}
