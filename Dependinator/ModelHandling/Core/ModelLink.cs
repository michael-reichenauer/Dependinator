using Dependinator.Utils;


namespace Dependinator.ModelHandling.Core
{
	internal class ModelLink : Equatable<ModelLink>, IModelItem
	{
		public ModelLink(string source, string target, NodeType targetType)
		{
			Source = source;
			Target = target;
			TargetType = targetType;

			IsEqualWhenSame(Source, Target);
		}


		public string Source { get; }
		public string Target { get; }
		public NodeType TargetType { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}