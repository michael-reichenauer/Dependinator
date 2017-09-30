using Dependinator.Utils;


namespace Dependinator.ModelParsing
{
	internal class ModelLine : Equatable<ModelLine>, IModelItem
	{
		public ModelLine(string source, string target, int linkCount)
		{
			Source = source;
			Target = target;
			LinkCount = linkCount;

			IsEqualWhenSame(Source, Target);
		}


		public string Source { get; }
		public string Target { get; }
		public int LinkCount { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}