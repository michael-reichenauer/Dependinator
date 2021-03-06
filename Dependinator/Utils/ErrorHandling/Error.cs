﻿using System;


namespace Dependinator.Utils.ErrorHandling
{
    public class Error : Equatable<Error>
    {
        private static readonly Exception errorException = new Exception("Error");
        private static readonly Exception noErrorException = new Exception("No error");
        private static readonly Exception noValueException = new Exception("No value");


        public static Error None = new Error(noErrorException);

        public static Error NoValue = new Error(noValueException);


        private Error(string message = null)
            : this(null, message)
        {
        }


        private Error(Exception exception, string message = null)
        {
            exception = exception ?? errorException;

            if (message != null && exception != errorException)
            {
                Message = message;
                Text = $"{Message},\n{exception.GetType().Name}: {exception.Message}";
                Exception = exception;
            }
            else if (message != null)
            {
                Message = message;
                Text = message;
                Exception = exception;
            }
            else
            {
                Message = exception.Message;
                Text = $"{Message},\n{exception.GetType().Name}: {exception.Message}";
                Exception = exception;
            }

            if (exception != noErrorException && exception != noValueException)
            {
                Log.Warn($"Error: {Message}");
            }

            IsEqualWhen(IsSameError, 0);
        }


        public string Message { get; }
        public string Text { get; }


        public Exception Exception { get; }

        public static Error From(Exception e) => new Error(e);

        public static Error From(Exception e, string message) => new Error(e, message);

        public static Error From(string message) => new Error(message);



        public bool Is<T>() => this is T || Exception is T;


        public override string ToString() => Text;


        protected bool IsSameError(Error other)
        {
            if (ReferenceEquals(this, None) && !ReferenceEquals(other, None)
                || !ReferenceEquals(this, None) && ReferenceEquals(other, None))
            {
                return false;
            }

            return
                Exception == null && other.Exception == null && GetType() == other.GetType()
                || other.GetType().IsInstanceOfType(this)
                || GetType() == other.GetType() && Exception != null && other.Exception != null
                && other.Exception.GetType().IsInstanceOfType(this);
        }
    }
}
