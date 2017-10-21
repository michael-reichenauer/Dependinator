using Dependinator.Utils;

namespace Dependinator.ModelParsing
{
	internal class ModelLink : Equatable<ModelLink>, IModelItem
	{
		public ModelLink(string source, string target, string targetType)
		{
			Source = source;
			Target = target;
			TargetType = targetType;

			IsEqualWhenSame(Source, Target);
		}


		public string Source { get; }
		public string Target { get; }
		public string TargetType { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}