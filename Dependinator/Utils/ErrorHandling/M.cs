using System;


namespace Dependinator.Utils.ErrorHandling
{
    public class M
    {
        public static M Ok = new M(Error.None);
        public static Error NoValue = Error.NoValue;


        public M(Error error)
        {
            Error = error;
        }


        public Error Error { get; }
     
        public bool IsFaulted => Error != Error.None;
        public bool IsOk => Error == Error.None;
        public bool Is<T>() => Error is T || Error.Exception is T;
        public string Message => Error.Message;

        public static M<T> From<T>(T result) => new M<T>(result);

        public static implicit operator M(Error error) => new M(error);
        public static implicit operator M(Exception e) => new M(Error.From(e));

        public static implicit operator bool(M m) => !m.IsFaulted;


        public override string ToString()
        {
            if (IsFaulted)
            {
                return $"Error: {Error}";
            }

            return "OK";
        }
    }


    public class M<T> : M
    {
        private readonly T storedValue;


        public M(T value)
            : base(Error.None)
        {
            storedValue = value;
        }


        public M(Error error)
            : base(error)
        {
        }


        public T Value
        {
            get
            {
                if (!IsFaulted)
                {
                    return storedValue;
                }

                throw Asserter.FailFast(Error.Message);
            }
        }


        public static implicit operator M<T>(Error error) => new M<T>(error);
        public static implicit operator M<T>(Exception e) => new M<T>(Error.From(e));
        public static implicit operator bool(M<T> m) => !m.IsFaulted;


        public static implicit operator M<T>(T value)
        {
            if (value == null)
            {
                throw Asserter.FailFast("Value cannot be null");
            }

            return new M<T>(value);
        }


        //public bool HasValue => ;


        public bool HasValue(out T value)
        {
            if (!IsFaulted)
            {
                value = storedValue;
                return true;
            }

            value = default(T);
            return false;
        }


        public T Or(T defaultValue)
        {
            if (IsFaulted)
            {
                return defaultValue;
            }

            return Value;
        }


        public override string ToString()
        {
            if (IsFaulted)
            {
                return $"Error: {Error}";
            }

            return storedValue?.ToString() ?? "";
        }
    }
}
