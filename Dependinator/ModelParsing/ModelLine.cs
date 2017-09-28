using System.Collections.Generic;
using Dependinator.Utils;


namespace Dependinator.ModelParsing
{
	internal class ModelLine : Equatable<ModelLine>, IModelItem
	{
		public ModelLine(NodeName source, NodeName target)
		{
			Source = source;
			Target = target;

			IsEqualWhenSame(Source, Target);
		}


		public NodeName Source { get; }
		public NodeName Target { get; }
		public IReadOnlyList<ModelLink> Links { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}