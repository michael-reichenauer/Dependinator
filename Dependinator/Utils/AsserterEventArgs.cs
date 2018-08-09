using System;


namespace Dependinator.Utils
{
    public class AsserterEventArgs : EventArgs
    {
        public AsserterEventArgs(Exception exception) => Exception = exception;
        public Exception Exception { get; }
    }
}
