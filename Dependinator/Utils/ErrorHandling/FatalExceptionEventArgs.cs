namespace System
{
    internal class FatalExceptionEventArgs : EventArgs
    {
        public FatalExceptionEventArgs(string message, Exception exception)
        {
            Message = message;
            Exception = exception;
        }


        public string Message { get; }

        public Exception Exception { get; }
    }
}
