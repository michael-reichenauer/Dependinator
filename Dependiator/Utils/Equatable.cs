using System;


namespace Dependiator.Utils
{
	/// <summary>
	/// Base class, which implement IEquatable'T' interface and makes it easier to prt
	/// equality operators like "==" and "!=" and usage in Dictionary.
	/// Just inherit from this class and call "IsEqual()" in the constructor.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class Equatable<T> : IEquatable<T>
	{
		private Func<T, bool> isEqualFunc;
		private int? hashCode;

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

		protected void IsEqualWhen(
			Func<T, bool> isEqual,
			object getHashCodeObject,
			params object[] getHashCodeObjects)
		{
			isEqualFunc = isEqual;

			int code = getHashCodeObject?.GetHashCode() ?? 0;
			foreach (object item in getHashCodeObjects)
			{
				code += code * 17 + item?.GetHashCode() ?? 0;
			}

			hashCode = code;
		}


		public bool Equals(T other)
		{
			if (isEqualFunc == null)
			{
				throw new NotImplementedException(
					$"To support Equals() or == for {GetType()},\n" +
					$"you need to call IsEqualWhen() in {GetType()}, e.g. in the constructor");
			}

			return (other != null) && isEqualFunc(other);
		}

		public override int GetHashCode()
		{
			if (hashCode == null)
			{
				throw new NotImplementedException(
					$"To support GetHashCode() for {GetType()}, for use in e.g. a Dictionary,\n" +
					$"you need to call IsEqualWhen() in {GetType()}, e.g. in the constructor");
			}

			return hashCode.Value;
		}

		public override bool Equals(object other) => other is T otherTyped && Equals(otherTyped);

		public static bool operator ==(Equatable<T> obj1, Equatable<T> obj2) =>
			Equatable.IsEqual(obj1, obj2);

		public static bool operator !=(Equatable<T> obj1, Equatable<T> obj2) => !(obj1 == obj2);
	}


	/// <summary>
	/// Helper class to implement IEquatable'T'  interface
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
			else if (ReferenceEquals(obj1, null) || ReferenceEquals(obj2, null))
			{
				return false;
			}
			else
			{
				return predicate(obj1, obj2);
			}
		}

		public static bool IsEqual<T>(IEquatable<T> obj1, IEquatable<T> obj2)
		{
			if (ReferenceEquals(obj1, null) && ReferenceEquals(obj2, null))
			{
				return true;
			}
			else if (ReferenceEquals(obj1, null) || ReferenceEquals(obj2, null))
			{
				return false;
			}
			else
			{
				return obj1.Equals(obj2);
			}
		}
	}
}