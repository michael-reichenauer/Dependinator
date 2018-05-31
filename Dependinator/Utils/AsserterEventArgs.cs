using System;


namespace Dependinator.Utils
{
	public class AsserterEventArgs : EventArgs
	{
		public Exception Exception { get; }

		public AsserterEventArgs(Exception exception) => Exception = exception;
	}
}