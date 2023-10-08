namespace Dependinator.Utils
{
    internal interface IEquatable
    {
        object __EqValue1 { get; }
        object __EqValue2 { get; }
        object __EqValue3 { get; }
    }


    /// <summary>
    ///     Base class, which implement IEquatable'T' interface and makes it easier to prt
    ///     equality operators like "==" and "!=" and usage in Dictionary.
    ///     Just inherit from this class and call "IsEqual()" in the constructor.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class Equatable<T> : IEquatable<T>, IEquatable
    {
        private object eqValue1;
        private object eqValue2;
        private object eqValue3;
        private int? hashCode;
        private Func<T, bool> isEqualFunc;

        /* Sample class, which inherits Equatable<T>:
         
            private class Id : Equatable<Id>
            {
                private readonly string id;
        
                public Id(string id)
                {
                    this.id = id;
                    IsEqualWhen(other => id == other.id, Id);
                }
            }

        */

        object IEquatable.__EqValue1 => eqValue1;
        object IEquatable.__EqValue2 => eqValue2;
        object IEquatable.__EqValue3 => eqValue3;


        public bool Equals(T other)
        {
            if (isEqualFunc == null)
            {
                throw new InvalidOperationException(
                    $"To support Equals() or == for {GetType()},\n" +
                    "you need to call IsEqualWhenSame(), e.g. in the constructor");
            }

            return other != null && isEqualFunc(other);
        }


        protected void IsEqualWhenSame(object value1)
        {
            hashCode = GetHashFor(value1);
            eqValue1 = value1;

            isEqualFunc = other =>
            {
                if (other is IEquatable otherTyped)
                {
                    return Equals(otherTyped.__EqValue1, eqValue1);
                }

                return false;
            };
        }


        protected void IsEqualWhenSame(object value1, object value2)
        {
            hashCode = GetHashFor(value1, value2);
            eqValue1 = value1;
            eqValue2 = value2;

            isEqualFunc = other =>
            {
                if (other is IEquatable otherTyped)
                {
                    return Equals(otherTyped.__EqValue1, eqValue1)
                           && Equals(otherTyped.__EqValue2, eqValue2);
                }

                return false;
            };
        }


        protected void IsEqualWhenSame(object value1, object value2, object value3)
        {
            hashCode = GetHashFor(value1, value2, value3);
            eqValue1 = value1;
            eqValue2 = value2;
            eqValue3 = value3;

            isEqualFunc = other =>
            {
                if (other is IEquatable otherTyped)
                {
                    return Equals(otherTyped.__EqValue1, eqValue1)
                           && Equals(otherTyped.__EqValue2, eqValue2)
                           && Equals(otherTyped.__EqValue3, eqValue3);
                }

                return false;
            };
        }


        protected void IsEqualWhen(
            Func<T, bool> isEqual,
            object getHashCodeObject,
            params object[] getHashCodeObjects)
        {
            hashCode = GetHashFor(getHashCodeObject, getHashCodeObjects);

            isEqualFunc = isEqual;
        }


        public override int GetHashCode()
        {
            if (hashCode == null)
            {
                throw new InvalidOperationException(
                    $"To support GetHashCode() for {GetType()}, for use in e.g. a Dictionary,\n" +
                    "you need to call IsEqualWhenSame(), e.g. in the constructor");
            }

            return hashCode.Value;
        }


        public override bool Equals(object other) => other is T otherTyped && Equals(otherTyped);


        public static bool operator ==(Equatable<T> obj1, Equatable<T> obj2) =>
            Equatable.IsEqual(obj1, obj2);


        public static bool operator !=(Equatable<T> obj1, Equatable<T> obj2) => !(obj1 == obj2);


        private static int GetHashFor(object getHashCodeObject, params object[] getHashCodeObjects)
        {
            int code = getHashCodeObject?.GetHashCode() ?? 0;
            foreach (object item in getHashCodeObjects)
            {
                code += code * 17 + item?.GetHashCode() ?? 0;
            }

            return code;
        }
    }


    /// <summary>
    ///     Helper class to implement IEquatable'T'  interface
    /// </summary>
    public class Equatable
    {
        /* Example class, which implement Equatable:

            private class Id : IEquatable<Id>
            {
                private readonly string id;

                public Id(string id)
                {
                    this.id = id;
                }

                public bool Equals(Id other) => this == other;
                public override bool Equals(object other) => other is Id && Equals((Id)other);
                public static bool operator ==(Id obj1, Id obj2) =>
                    Equatable.IsEqualWhen(obj1, obj2, (o1, o2) => o1.id == o2.id);
                public static bool operator !=(Id obj1, Id obj2) => !(obj1 == obj2);
                public override int GetHashCode() => id?.GetHashCode() ?? 0;
            }

        */


        public static bool IsEqual<T>(T obj1, T obj2, Func<T, T, bool> predicate)
        {
            if (ReferenceEquals(obj1, null) && ReferenceEquals(obj2, null))
            {
                return true;
            }

            if (ReferenceEquals(obj1, null) || ReferenceEquals(obj2, null))
            {
                return false;
            }

            return predicate(obj1, obj2);
        }


        public static bool IsEqual<T>(IEquatable<T> obj1, IEquatable<T> obj2)
        {
            if (ReferenceEquals(obj1, null) && ReferenceEquals(obj2, null))
            {
                return true;
            }

            if (ReferenceEquals(obj1, null) || ReferenceEquals(obj2, null))
            {
                return false;
            }

            return obj1.Equals(obj2);
        }
    }
}
