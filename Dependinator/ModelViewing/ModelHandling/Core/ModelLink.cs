using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Core
{
	internal class ModelLink : Equatable<ModelLink>, IModelItem
	{
		public ModelLink(string source, string target, NodeType targetType, bool isAdded = false)
		{
			Source = source;
			Target = target;
			TargetType = targetType;
			IsAdded = isAdded;

			IsEqualWhenSame(Source, Target);
		}


		public string Source { get; }
		public string Target { get; }
		public NodeType TargetType { get; }

		public bool IsAdded { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}