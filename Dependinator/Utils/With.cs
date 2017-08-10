using System;

namespace Dependinator.Utils
{
	/// <summary>
	/// A helper struct, to make it easier to implement With functions in immutable classes.
	/// </summary>
	public struct With<T> : IEquatable<With<T>>
	{
		private readonly bool hasValue;
		private readonly T value;

		private With(T value)
		{
			// Private to prevent instance to be created unless using the cast operator
			this.value = value;
			hasValue = true;
		}

		public static implicit operator With<T>(T value) => new With<T>(value);

		public T Or(T otherValue) => hasValue ? value : otherValue;

		public bool Equals(With<T> other) => hasValue == other.hasValue && Equals(value, other.value);
		public override bool Equals(object obj) => obj is With<T> && Equals((With<T>)obj);
		public override int GetHashCode() => value?.GetHashCode() ?? 0;
		public static bool operator ==(With<T> left, With<T> right) => left.Equals(right);
		public static bool operator !=(With<T> left, With<T> right) => !(left == right);	
	}

	
	public class TestWith
	{
		public string Name { get; }
		public int Count { get; }

		public TestWith(string name, int count)
		{
			Name = name;
			Count = count;
		}

		public TestWith With(
			With<string> name = default(With<string>), 
			With<int> count = default(With<int>))
		{
			return new TestWith(name.Or(Name), count.Or(Count));
		}
	}
}