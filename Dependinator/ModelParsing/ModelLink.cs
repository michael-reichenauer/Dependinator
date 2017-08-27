using Dependinator.Utils;

namespace Dependinator.ModelParsing
{
	internal class ModelLink : Equatable<ModelLink>
	{
		public ModelLink(string source, string target)
		{
			Source = source;
			Target = target;

			IsEqualWhenSame(Source, Target);
		}

		public string Target { get; }
		public string Source { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}