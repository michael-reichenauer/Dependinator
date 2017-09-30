using Dependinator.Utils;

namespace Dependinator.ModelParsing
{
	internal class ModelLink : Equatable<ModelLink>, IModelItem
	{
		public ModelLink(string source, string target)
		{
			Source = source;
			Target = target;

			IsEqualWhenSame(Source, Target);
		}


		public string Source { get; }
		public string Target { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}