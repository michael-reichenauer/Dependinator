using System;


namespace Dependinator.Utils.ErrorHandling
{
    public class M
    {
        public static M Ok = new M(Error.None);
        public static Error NoValue = Error.NoValue;


        protected M(Error error)
        {
            Error = error;
        }


        public Error Error { get; }
        public string ErrorMessage => Error.Message;

        public bool IsOk => Error == Error.None;
        public bool IsFaulted => Error != Error.None;
        public bool Is<T>() => Error is T || Error.Exception is T;


        public static M<T> From<T>(T result) => new M<T>(result);

        public static implicit operator M(Error error) => new M(error);


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


        public static implicit operator M<T>(T value)
        {
            if (value == null)
            {
                throw Asserter.FailFast("Value cannot be null");
            }

            return new M<T>(value);
        }



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


        public T Or(T defaultValue) => IsFaulted ? defaultValue : Value;


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
