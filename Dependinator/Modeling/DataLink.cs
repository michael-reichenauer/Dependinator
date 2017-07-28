using Dependinator.Utils;

namespace Dependinator.Modeling
{
	internal class DataLink : Equatable<DataLink>
	{
		public DataLink(string source, string target)
		{
			Source = source;
			Target = target;

			IsEqualWhen(Source, Target);
		}

		public string Target { get; }
		public string Source { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}