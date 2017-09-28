using Dependinator.Utils;


namespace Dependinator.ModelParsing
{
	internal class ModelLine : Equatable<ModelLine>, IModelItem
	{
		public ModelLine(NodeName source, NodeName target, int linkCount)
		{
			Source = source;
			Target = target;
			LinkCount = linkCount;

			IsEqualWhenSame(Source, Target);
		}


		public NodeName Source { get; }
		public NodeName Target { get; }
		public int LinkCount { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}