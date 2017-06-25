using Dependinator.ModelViewing.Modeling.Serializing;

namespace Dependinator.ModelViewing.Modeling.Analyzing
{
	internal interface IReflectionService
	{
		DataModel Analyze(string path);
	}
}