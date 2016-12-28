using System;


namespace Dependiator.Features.StatusHandling.Private
{
	internal class StatusChangedEventArgs : EventArgs
	{
		public Status NewStatus { get; }

		public DateTime DateTime { get; }


		public StatusChangedEventArgs(Status newStatus, DateTime dateTime)
		{
			NewStatus = newStatus;
			DateTime = dateTime;
		}
	}
}