using Dependinator.Utils;

namespace Dependinator.ModelParsing
{
	internal class ModelLink : Equatable<ModelLink>, IModelItem
	{
		public ModelLink(NodeName source, NodeName target)
		{
			Source = source;
			Target = target;

			IsEqualWhenSame(Source, Target);
		}

		public NodeName Target { get; }
		public NodeName Source { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}